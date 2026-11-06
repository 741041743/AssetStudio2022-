using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

// 清单 DTO 的字段由 Newtonsoft 在反序列化时赋值，编译器看不到
#pragma warning disable CS0649

namespace AssetStudio.GUI
{
    /// <summary>
    /// 从 APK 里读取 YooAsset 清单，按 bundle 展开依赖闭包，并把指定 bundle
    /// 抽取到本地目录。AssetStudio 只认文件，因此抽取目录可以直接当普通目录加载。
    /// </summary>
    internal sealed class ApkBundleSource
    {
        public string ApkPath { get; private set; }
        public string PackageName { get; private set; }
        public string Version { get; private set; }
        public ApkManifest Manifest { get; private set; }

        /// <summary>包内 bundle 文件名 -> zip 内完整条目名</summary>
        public Dictionary<string, string> BuiltinEntries { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>清单中在 APK 内实际存在的 bundle 下标</summary>
        public HashSet<int> BuiltinBundleIds { get; } = new HashSet<int>();

        private string _packagePrefix;
        private string[] _searchTexts = Array.Empty<string>();

        /// <summary>
        /// 找出 APK 里包含的资源包名（assets/yoo/<包名>/）
        /// </summary>
        public static string[] GetPackages(string apkPath)
        {
            using (FileStream stream = new FileStream(apkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                HashSet<string> packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string package = ExtractPackageFromEntry(entry.FullName);
                    if (package != null)
                    {
                        packages.Add(package);
                    }
                }

                return packages
                    .OrderBy(x => string.Equals(x, "HotfixPkg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        /// <summary>
        /// 读取 APK 内指定包的清单与内置 bundle 列表
        /// </summary>
        public static ApkBundleSource Read(string apkPath, string packageName)
        {
            if (string.IsNullOrEmpty(apkPath) || !File.Exists(apkPath))
            {
                throw new FileNotFoundException("APK 不存在", apkPath);
            }

            ApkBundleSource source = new ApkBundleSource
            {
                ApkPath = Path.GetFullPath(apkPath),
                PackageName = packageName
            };

            using (FileStream stream = new FileStream(source.ApkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string prefix = FindPackagePrefix(archive, packageName);
                if (prefix == null)
                {
                    throw new InvalidOperationException(
                        "APK 内没有找到 assets/yoo/" + packageName + "/。\n" +
                        "请使用包含资源的完整 APK，不要用 split APK。");
                }
                source._packagePrefix = prefix;

                string versionEntry = prefix + "PackageManifest_" + packageName + ".version";
                string versionText = ReadEntryText(archive, versionEntry);
                string version = versionText == null ? string.Empty : versionText.Trim();

                string manifestEntry = null;
                if (!string.IsNullOrEmpty(version))
                {
                    string candidate = prefix + "PackageManifest_" + packageName + "_" + version + ".json";
                    if (FindEntry(archive, candidate) != null)
                    {
                        manifestEntry = candidate;
                    }
                }
                if (manifestEntry == null)
                {
                    manifestEntry = FindLatestManifestEntry(archive, prefix, packageName);
                }
                if (manifestEntry == null)
                {
                    throw new InvalidOperationException(
                        "APK 内没有找到 PackageManifest_" + packageName + "_*.json。\n" +
                        "该 APK 可能被裁剪过清单。");
                }

                string json = ReadEntryText(archive, manifestEntry);
                if (string.IsNullOrEmpty(json))
                {
                    throw new InvalidOperationException("清单内容为空：" + manifestEntry);
                }

                source.Manifest = ApkManifest.Parse(json);
                source.Version = string.IsNullOrEmpty(version)
                    ? source.Manifest.PackageVersion
                    : version;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Name.Length == 0 ||
                        !entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !entry.FullName.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string leaf = entry.FullName.Substring(entry.FullName.LastIndexOf('/') + 1);
                    if (!source.BuiltinEntries.ContainsKey(leaf))
                    {
                        source.BuiltinEntries[leaf] = entry.FullName;
                    }
                }

                for (int i = 0; i < source.Manifest.BundleList.Count; i++)
                {
                    if (source.GetBuiltinEntryName(source.Manifest.BundleList[i]) != null)
                    {
                        source.BuiltinBundleIds.Add(i);
                    }
                }

                source.PrepareSearch();
            }

            return source;
        }

        /// <summary>
        /// 取 bundle 在 APK 内的 zip 条目名，找不到返回 null
        /// </summary>
        public string GetBuiltinEntryName(ApkBundle bundle)
        {
            if (bundle == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(bundle.RemoteFileName) &&
                BuiltinEntries.TryGetValue(bundle.RemoteFileName, out string entryName))
            {
                return entryName;
            }

            string hashName = bundle.FileHash + ".bundle";
            if (!string.IsNullOrEmpty(bundle.FileHash) &&
                BuiltinEntries.TryGetValue(hashName, out entryName))
            {
                return entryName;
            }

            return null;
        }

        /// <summary>
        /// 展开依赖闭包，依赖在前、自身在后
        /// </summary>
        public List<int> GetClosure(int bundleId)
        {
            List<int> ordered = new List<int>(8);
            HashSet<int> visited = new HashSet<int>();
            Visit(bundleId, visited, ordered);
            return ordered;
        }

        /// <summary>
        /// 取 bundle 的搜索文本（bundle 名 + 清单内资源 Address/AssetPath，小写）
        /// </summary>
        public string GetSearchText(int bundleId)
        {
            if (bundleId < 0 || bundleId >= _searchTexts.Length)
            {
                return string.Empty;
            }
            return _searchTexts[bundleId];
        }

        /// <summary>
        /// 取 bundle 在清单里登记的资源
        /// </summary>
        public IReadOnlyList<ApkBundleAsset> GetAssets(int bundleId)
        {
            if (bundleId < 0 || bundleId >= Manifest.BundleList.Count)
            {
                return Array.Empty<ApkBundleAsset>();
            }
            return Manifest.GetAssets(bundleId);
        }

        /// <summary>
        /// 取 APK 内实际存在的全部 bundle 下标，按清单顺序返回
        /// </summary>
        public List<int> GetAllBuiltinBundleIds()
        {
            List<int> ids = new List<int>(BuiltinBundleIds.Count);
            for (int i = 0; i < Manifest.BundleList.Count; i++)
            {
                if (BuiltinBundleIds.Contains(i))
                {
                    ids.Add(i);
                }
            }
            return ids;
        }

        /// <summary>
        /// 把 bundle 集合抽取到本地目录，文件名统一用 FileHash.bundle，
        /// 避免 Android 上超长的 bundle 名撞 Windows 路径上限。
        /// </summary>
        public ApkExtractResult Extract(
            IReadOnlyList<int> bundleIds,
            string destDir,
            IProgress<int> progress,
            CancellationToken cancellationToken = default)
        {
            ApkExtractResult result = new ApkExtractResult();

            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);

            using (FileStream stream = new FileStream(ApkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                int total = bundleIds.Count;
                int done = 0;
                foreach (int bundleId in bundleIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (bundleId < 0 || bundleId >= Manifest.BundleList.Count)
                    {
                        ReportProgress(progress, ++done, total);
                        continue;
                    }

                    ApkBundle bundle = Manifest.BundleList[bundleId];
                    string entryName = GetBuiltinEntryName(bundle);
                    ZipArchiveEntry entry = entryName == null ? null : archive.GetEntry(entryName);
                    if (entry == null)
                    {
                        result.Missing.Add(bundle.BundleName);
                        ReportProgress(progress, ++done, total);
                        continue;
                    }

                    string fileName = string.IsNullOrEmpty(bundle.FileHash)
                        ? "bundle_" + bundleId
                        : bundle.FileHash;
                    string destPath = Path.Combine(destDir, fileName + ".bundle");

                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(
                               destPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                    {
                        input.CopyTo(output, 1 << 16);
                    }

                    result.Extracted++;
                    result.Bytes += entry.Length;
                    ReportProgress(progress, ++done, total);
                }
            }

            return result;
        }

        private static void ReportProgress(IProgress<int> progress, int done, int total)
        {
            if (progress != null && (done % 16 == 0 || done == total))
            {
                progress.Report(done);
            }
        }

        private void PrepareSearch()
        {
            int count = Manifest.BundleList.Count;
            StringBuilder[] builders = new StringBuilder[count];
            for (int i = 0; i < count; i++)
            {
                builders[i] = new StringBuilder(64);
                if (!string.IsNullOrEmpty(Manifest.BundleList[i].BundleName))
                {
                    builders[i].Append(Manifest.BundleList[i].BundleName).Append(' ');
                }
            }

            foreach (ApkBundleAsset asset in Manifest.AssetList)
            {
                if (asset.BundleID < 0 || asset.BundleID >= count)
                {
                    continue;
                }

                StringBuilder builder = builders[asset.BundleID];
                if (!string.IsNullOrEmpty(asset.Address))
                {
                    builder.Append(asset.Address).Append(' ');
                }
                if (!string.IsNullOrEmpty(asset.AssetPath))
                {
                    builder.Append(asset.AssetPath).Append(' ');
                }
            }

            _searchTexts = new string[count];
            for (int i = 0; i < count; i++)
            {
                _searchTexts[i] = builders[i].ToString().ToLowerInvariant();
            }
        }

        private void Visit(int bundleId, HashSet<int> visited, List<int> ordered)
        {
            if (bundleId < 0 || bundleId >= Manifest.BundleList.Count || !visited.Add(bundleId))
            {
                return;
            }

            int[] dependIds = Manifest.BundleList[bundleId].DependIDs;
            if (dependIds != null)
            {
                foreach (int dependId in dependIds)
                {
                    Visit(dependId, visited, ordered);
                }
            }
            ordered.Add(bundleId);
        }

        private static string ExtractPackageFromEntry(string entryName)
        {
            const string prefixA = "assets/yoo/";
            const string prefixB = "assets/StreamingAssets/yoo/";

            string prefix = null;
            if (entryName.StartsWith(prefixA, StringComparison.OrdinalIgnoreCase))
            {
                prefix = prefixA;
            }
            else if (entryName.StartsWith(prefixB, StringComparison.OrdinalIgnoreCase))
            {
                prefix = prefixB;
            }
            if (prefix == null)
            {
                return null;
            }

            int slash = entryName.IndexOf('/', prefix.Length);
            if (slash <= prefix.Length)
            {
                return null;
            }
            return entryName.Substring(prefix.Length, slash - prefix.Length);
        }

        private static string FindPackagePrefix(ZipArchive archive, string packageName)
        {
            string[] candidates =
            {
                "assets/yoo/" + packageName + "/",
                "assets/StreamingAssets/yoo/" + packageName + "/"
            };

            foreach (string candidate in candidates)
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string entryName)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        private static string FindLatestManifestEntry(
            ZipArchive archive,
            string packagePrefix,
            string packageName)
        {
            string prefix = packagePrefix + "PackageManifest_" + packageName + "_";
            string result = null;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (result == null ||
                    string.Compare(entry.FullName, result, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    result = entry.FullName;
                }
            }
            return result;
        }

        private static string ReadEntryText(ZipArchive archive, string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
            {
                return null;
            }

            ZipArchiveEntry entry = FindEntry(archive, entryName);
            if (entry == null)
            {
                return null;
            }

            using (Stream stream = entry.Open())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd().TrimStart('\uFEFF');
            }
        }
    }

    /// <summary>一次抽取的结果</summary>
    internal sealed class ApkExtractResult
    {
        public int Extracted;
        public long Bytes;
        public List<string> Missing = new List<string>();
    }

    /// <summary>YooAsset PackageManifest 的顶层结构</summary>
    internal sealed class ApkManifest
    {
        public string FileVersion;
        public bool EnableAddressable;
        public bool LocationToLower;
        public bool IncludeAssetGUID;
        public int OutputNameStyle;
        public string BuildPipeline;
        public string PackageName;
        public string PackageVersion;
        public List<ApkBundleAsset> AssetList = new List<ApkBundleAsset>();
        public List<ApkBundle> BundleList = new List<ApkBundle>();

        private Dictionary<string, int> _bundleNameToId =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private List<ApkBundleAsset>[] _assetsByBundle = Array.Empty<List<ApkBundleAsset>>();

        public static ApkManifest Parse(string json)
        {
            ApkManifest manifest = JsonConvert.DeserializeObject<ApkManifest>(json);
            if (manifest == null)
            {
                throw new InvalidOperationException("清单内容不是合法的 PackageManifest JSON");
            }
            manifest.Prepare();
            return manifest;
        }

        public void Prepare()
        {
            if (AssetList == null)
            {
                AssetList = new List<ApkBundleAsset>();
            }
            if (BundleList == null)
            {
                BundleList = new List<ApkBundle>();
            }

            _bundleNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _assetsByBundle = new List<ApkBundleAsset>[BundleList.Count];

            for (int i = 0; i < BundleList.Count; i++)
            {
                ApkBundle bundle = BundleList[i];
                bundle.Index = i;
                bundle.RemoteFileName = GetRemoteFileName(bundle);
                _assetsByBundle[i] = new List<ApkBundleAsset>(2);

                if (!string.IsNullOrEmpty(bundle.BundleName))
                {
                    _bundleNameToId[bundle.BundleName] = i;
                }
                if (!string.IsNullOrEmpty(bundle.RemoteFileName))
                {
                    _bundleNameToId[bundle.RemoteFileName] = i;
                }
            }

            foreach (ApkBundleAsset asset in AssetList)
            {
                if (asset.BundleID >= 0 && asset.BundleID < BundleList.Count)
                {
                    _assetsByBundle[asset.BundleID].Add(asset);
                }
            }
        }

        public IReadOnlyList<ApkBundleAsset> GetAssets(int bundleId)
        {
            if (bundleId < 0 || bundleId >= _assetsByBundle.Length)
            {
                return Array.Empty<ApkBundleAsset>();
            }
            return _assetsByBundle[bundleId];
        }

        /// <summary>
        /// 按 YooAsset 的 FileNameStyle 计算包内文件名
        /// </summary>
        public string GetRemoteFileName(ApkBundle bundle)
        {
            string bundleName = bundle.BundleName ?? string.Empty;
            string extension = Path.GetExtension(bundleName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".bundle";
            }

            if (OutputNameStyle == 0)
            {
                return bundle.FileHash + extension;
            }
            if (OutputNameStyle == 1)
            {
                return bundleName;
            }

            int dotIndex = bundleName.LastIndexOf('.');
            string shortName = dotIndex > 0 ? bundleName.Substring(0, dotIndex) : bundleName;
            return shortName + "_" + bundle.FileHash + extension;
        }
    }

    /// <summary>清单里的资源项</summary>
    internal sealed class ApkBundleAsset
    {
        public string Address;
        public string AssetPath;
        public string AssetGUID;
        public string[] AssetTags;
        public int BundleID;
    }

    /// <summary>清单里的资源包项</summary>
    internal sealed class ApkBundle
    {
        public string BundleName;
        public uint UnityCRC;
        public string FileHash;
        public string FileCRC;
        public long FileSize;
        public bool Encrypted;
        public string[] Tags;
        public int[] DependIDs;

        [JsonIgnore]
        public int Index;

        [JsonIgnore]
        public string RemoteFileName;
    }
}

#pragma warning restore CS0649
