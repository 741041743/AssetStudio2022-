using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AssetStudio.GUI
{
    public class hashCheck
    {
        public bool isDirty = false;
        public Dictionary<string, string[]> list;
        public Dictionary<string, string[]> key2mark;
        public Dictionary<string, string[]> mark2list;
        public Dictionary<string, string[]> langkey2mark;
        public Dictionary<string, string[]> langmark2list;
        public Dictionary<string, object> collection2mark;
        public string[] acc;
        [NonSerialized] public Func<string, string> pathFunc;
        [NonSerialized] public string name;
        [NonSerialized] public List<string> listKeys;
        [NonSerialized] private object m_lockObj = new object();


        public string this[string key]
        {
            get
            {
                string[] strArray;
                return this.list.TryGetValue(key, out strArray) && strArray != null ? strArray[1] : "";
            }
        }

        public string GetVer(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "current";
            string[] strArray = this.list.ContainsKey(key) ? this.list[key] : (string[])null;
            return strArray != null && strArray.Length >= 1 ? strArray[0] : "current";
        }

        public long GetCheckSum(string key)
        {
            long result = 0;
            if (string.IsNullOrEmpty(key))
                return result;
            string[] strArray;
            this.list.TryGetValue(key, out strArray);
            if (strArray == null || strArray.Length < 4)
                return result;
            long.TryParse(strArray[3], out result);
            return result;
        }

        public string GetCheckSumStr(string key)
        {
            string checkSumStr = "0";
            if (!string.IsNullOrEmpty(key))
            {
                string[] strArray;
                this.list.TryGetValue(key, out strArray);
                if (strArray != null && strArray.Length >= 4)
                    checkSumStr = strArray[3];
            }

            return checkSumStr;
        }

        public long GetUnityCrc(string key)
        {
            long result = 0;
            if (string.IsNullOrEmpty(key))
                return result;
            string[] strArray = this.list.ContainsKey(key) ? this.list[key] : (string[])null;
            if (strArray == null || strArray.Length < 5)
                return result;
            long.TryParse(strArray[4], out result);
            return result;
        }

        public int GetSize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;
            string[] strArray = this.list.ContainsKey(key) ? this.list[key] : (string[])null;
            return strArray != null && strArray.Length >= 3 ? int.Parse(strArray[2]) : 0;
        }

        public bool CheckSame(string key, string[] hashR)
        {
            if (hashR == null)
                return false;
            string[] strArray = (string[])null;
            if (!this.list.TryGetValue(key, out strArray))
                return false;
            if (hashR == strArray)
                return true;
            if (strArray.Length != hashR.Length)
                return false;
            int length = strArray.Length;
            for (int index = 0; index < length; ++index)
            {
                if (strArray[index] != hashR[index])
                    return false;
            }

            return true;
        }

        public bool Update(string key, string[] hashR)
        {
            lock (this.m_lockObj)
            {
                if (string.IsNullOrEmpty(key) || hashR == null)
                    return false;
                this.list[key] = hashR;
                this.isDirty = true;
                return true;
            }
        }

        public bool Remove(string abname)
        {
            if (string.IsNullOrEmpty(abname))
                return false;
            lock (this.m_lockObj)
            {
                this.list.Remove(abname);
                this.isDirty = true;
                return true;
            }
        }

        public string ToJson()
        {
            lock (this.m_lockObj)
                return JsonConvert.SerializeObject((object)this);
        }

        public List<string> GetListKeys()
        {
            if (this.listKeys == null)
                this.listKeys = new List<string>();
            this.listKeys.Clear();
            this.listKeys.AddRange((IEnumerable<string>)this.list.Keys);
            return this.listKeys;
        }

        public string[] GetListKeyContent(string key)
        {
            string[] strArray;
            return string.IsNullOrEmpty(key) || !this.list.TryGetValue(key, out strArray) ? (string[])null : strArray;
        }

        public static hashCheck ChooseMark(hashCheck n, params string[] keys)
        {
            Dictionary<string, string[]> key2mark = n.key2mark;
            if (key2mark == null)
                return n;
            if (keys == null || keys.Length <= 1)
                return hashCheck.ChooseMark(n, keys.Length == 1 ? keys[0] : string.Empty);
            string key1 = keys[0];
            if (string.IsNullOrEmpty(key1))
                key1 = "allmarks";
            string key3 = "";
            HashSet<string> other1 = new HashSet<string>();
            string[] other2;
            if (key2mark.TryGetValue(key1, out other2))
                other1.UnionWith((IEnumerable<string>)other2);
            if (other2 == null || other2.Length == 0)
                other1.Add(key1);
            if (!string.IsNullOrEmpty(key3))
            {
                string[] other3;
                if (key2mark.TryGetValue(key3, out other3))
                    other1.UnionWith((IEnumerable<string>)other3);
                if (other3 == null || other3.Length == 0)
                    other1.Add(key3);
            }

            for (int index = 1; index < keys.Length; ++index)
            {
                if (!string.IsNullOrEmpty(keys[index]))
                {
                    string[] other4;
                    if (key2mark.TryGetValue(keys[index], out other4))
                        other1.UnionWith((IEnumerable<string>)other4);
                    if (other4 == null || other4.Length == 0)
                        other1.Add(keys[index]);
                }
            }

            HashSet<string> stringSet = new HashSet<string>();
            foreach (string key5 in key2mark.Keys)
                stringSet.UnionWith((IEnumerable<string>)key2mark[key5]);
            stringSet.ExceptWith((IEnumerable<string>)other1);
            Dictionary<string, string[]> list = n.list;
            Dictionary<string, string[]> mark2list = n.mark2list;
            foreach (string key6 in stringSet)
            {
                string[] strArray;
                if (mark2list.TryGetValue(key6, out strArray))
                {
                    foreach (string key7 in strArray)
                        list.Remove(key7);
                }
            }

            return n;
        }

        public static hashCheck ChooseMark(hashCheck n, string key)
        {
            if (string.IsNullOrEmpty(key))
                key = "allmarks";
            HashSet<string> other1 = new HashSet<string>();
            Dictionary<string, string[]> key2mark = n.key2mark;
            Dictionary<string, string[]> mark2list = n.mark2list;
            if (key2mark == null)
                return n;
            string[] other2;
            if (key2mark.TryGetValue(key, out other2))
                other1.UnionWith((IEnumerable<string>)other2);
            if (other2 == null || other2.Length == 0)
                other1.Add(key);
            HashSet<string> stringSet = new HashSet<string>();
            foreach (string key2 in key2mark.Keys)
                stringSet.UnionWith((IEnumerable<string>)key2mark[key2]);
            stringSet.ExceptWith((IEnumerable<string>)other1);
            Dictionary<string, string[]> list = n.list;
            foreach (string key3 in stringSet)
            {
                string[] strArray;
                if (mark2list.TryGetValue(key3, out strArray))
                {
                    foreach (string key4 in strArray)
                        list.Remove(key4);
                }
            }

            return n;
        }

        public static hashCheck Parse(string s, string res_key = "", string ch_res_key = "")
        {
            if (string.IsNullOrEmpty(s))
                return (hashCheck)null;
            hashCheck n = (hashCheck)null;
            try
            {
                n = JsonConvert.DeserializeObject<hashCheck>(s);
                hashCheck.ChooseMark(n, res_key, ch_res_key);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse hash check: {ex.Message}|{ex.StackTrace}");
            }

            return n;
        }
    }

    class ParallelInfo
    {
        public System.Threading.Tasks.TaskCompletionSource<bool> taskCompletionSource;
        public int currentIdx = 0;
        public string displayTxt = string.Empty;
        public int totalCount = 1;
        public bool cancel = false;
    }

    /// <summary>
    /// 处理服务器资源加载的类
    /// </summary>
    public class ServerLoader
    {
        private static readonly Regex regexFilesVersion = new Regex(@"/resource/(\d+)/(files[2]*.txt)");
        /// <summary>
        /// 从服务器加载资源
        /// </summary>
        /// <param name="url">远程服务器地址</param>
        /// <param name="inputVersion">用户输入的版本号，0表示使用服务器最新版本</param>
        /// <param name="progress">进度报告</param>
        /// <returns>返回下载后的缓存路径，失败返回null</returns>
        public static async Task<string> LoadFromServer(string url, string inputVersion, IProgress<int> progress = null)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // 设置超时时间
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    Logger.Info($"Requesting server: {url}");

                    // 请求URL获取JSON内容
                    var response = await httpClient.GetStringAsync(url);

                    Logger.Info("Successfully received server response");

                    // 解析JSON
                    var jsonObject = JObject.Parse(response);

                    // 获取resource_url和version
                    var resourceUrl = jsonObject["resource_url"]?.ToString();
                    var filesUrl = jsonObject["files_url"]?.ToString();
                    var serverVersion = ParseVersion(filesUrl, out var fileName).ToString();

                    if (string.IsNullOrEmpty(resourceUrl))
                    {
                        Logger.Error("resource_url field not found in JSON");
                        return null;
                    }

                    if (string.IsNullOrEmpty(serverVersion))
                    {
                        Logger.Error("version field not found in JSON");
                        return null;
                    }

                    // 如果用户输入版本号为0，使用服务器的最新版本
                    var actualVersion = inputVersion == "0" ? serverVersion : inputVersion;
                    
                    Logger.Info($"Parsing successful - resource_url: {resourceUrl}, server_version: {serverVersion}, actual version: {actualVersion}");

                    // 在应用程序根目录创建本地缓存目录
                    var appRootPath = AppDomain.CurrentDomain.BaseDirectory;
                    var serverCachePath = Path.Combine(appRootPath, "ServerCache");
                    var resourcesFolder = Path.Combine(serverCachePath, $"{actualVersion}_full");

                    Logger.Info($"Local cache directory: {resourcesFolder}");

                    // 检查是否已经下载完成
                    var completeMarkerPath = Path.Combine(resourcesFolder, ".download_complete");
                    if (Directory.Exists(resourcesFolder) && File.Exists(completeMarkerPath))
                    {
                        // 读取标记文件，检查URL是否匹配
                        try
                        {
                            var markerContent = File.ReadAllText(completeMarkerPath);
                            var markerData = JObject.Parse(markerContent);
                            var savedUrl = markerData["url"]?.ToString();
                            
                            var savedVersion = markerData["version"]?.ToString();
                            
                            
                            if (savedUrl == url && savedVersion == actualVersion)
                            {
                                Logger.Info("Complete local cache detected, using directly");
                                return resourcesFolder;
                            }
                            else
                            {
                                Logger.Info("Local cache URL or version mismatch, will re-download");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Failed to read download marker file: {ex.Message}, will re-download");
                        }
                    }
                    
                    // 如果目录已存在但未完成下载，删除旧缓存
                    if (Directory.Exists(resourcesFolder))
                    {
                        Logger.Info($"Incomplete cache directory detected, deleting: {resourcesFolder}");
                        try
                        {
                            Directory.Delete(resourcesFolder, true);
                            Logger.Info("Old cache directory deleted");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to delete old cache directory: {ex.Message}");
                            return null;
                        }
                    }
                    
                    // 创建新的缓存目录
                    Directory.CreateDirectory(resourcesFolder);
                    Logger.Info("New cache directory created");

                    // 替换resource_url中的$file_ver占位符
                    var finalUrl = resourceUrl.Replace("$file_ver", actualVersion);
                    
                    // 下载资源列表文件
                    Logger.Info("Starting to download resource list file...");
                    var filesListUrl = Path.Combine(finalUrl,fileName);
                    var filesListPath = Path.Combine(resourcesFolder, "files.txt");
                    await DownloadFileAsync(filesListUrl, filesListPath);

                    // 读取文件列表
                    var filesContent = File.ReadAllText(filesListPath);
                    
                    var newHashCheck = hashCheck.Parse(filesContent);
                    if (newHashCheck == null || newHashCheck.list == null)
                    {
                        Logger.Error("Failed to parse files.txt");
                        return null;
                    }
                    
                    Logger.Info($"Resource list contains {newHashCheck.list.Count} files");
                    
                    // 获取基础资源URL（去掉$file_ver占位符）
                    var baseResourceUrl = resourceUrl.TrimEnd('/').Replace("$file_ver", "");
                    
                    await DownloadResourcesParallel(newHashCheck.list, newHashCheck, int.Parse(actualVersion), $"{actualVersion}_full", serverCachePath, baseResourceUrl, progress);

                    Logger.Info("Resource download completed");
                    
                    // 创建下载完成标记文件
                    try
                    {
                        var markerData = new JObject
                        {
                            ["url"] = url,
                            ["version"] = actualVersion,
                            ["download_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            ["file_count"] = newHashCheck.list.Count
                        };
                        File.WriteAllText(completeMarkerPath, markerData.ToString());
                        Logger.Info("Download completion marker file created");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to create marker file: {ex.Message}");
                    }
                    
                    return resourcesFolder;
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"Network request failed: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Server loading failed: {ex.Message}");
                return null;
            }
        }
        
        static int ParseVersion(string fileUrl, out string fileName)
        {
            fileUrl = fileUrl.Trim();
            fileName = null;

            var versionMatch = regexFilesVersion.Match(fileUrl);
            if (!versionMatch.Success || versionMatch.Groups.Count <= 2)
            {
                Logger.Error($"Invalid files path format: {fileUrl}");
                return -1;
            }
            if (!int.TryParse(versionMatch.Groups[1].Value, out int fileVersion))
            {
                Logger.Error($"Invalid files path version format: {fileUrl}");
                return -1;
            }
            fileName = versionMatch.Groups[2].Value;
            return fileVersion;
        }
        
        static public string GetResUrl(string assetbundleName, hashCheck hashCheck, string resUrl)
        {
            if (hashCheck?.list.ContainsKey(assetbundleName) == false)
            {
                return null;
            }
            return resUrl + "/" + hashCheck.GetVer(assetbundleName) + "/" + assetbundleName;
        }
        
        static async Task DownloadResourcesParallel<T>(Dictionary<string, T> resourcesSet, hashCheck resourcesHashCheck, int version, string versionFolder, string tempResourceFolder, string baseServerResourcePath, IProgress<int> progress = null)
        {
            var parallelDownloadInfo = new ParallelInfo
            {
                taskCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>(),
                currentIdx = 0,
                totalCount = resourcesSet.Count,
                cancel = false
            };

            try
            {
                var stopWatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Info($"Starting parallel download of {resourcesSet.Count} resource files...");
                
                // 报告初始进度
                progress?.Report(0);

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(resourcesSet, parallelOptions, (keyPair, loopState) =>
                    {
                        var abName = keyPair.Key;
                        try
                        {
                            if (parallelDownloadInfo.cancel)
                            {
                                loopState.Break();
                                return;
                            }

                            var downloadUrl = GetResUrl(abName, resourcesHashCheck, baseServerResourcePath);
                            if (downloadUrl != null)
                            {
                                string savePath;
                                if (string.IsNullOrEmpty(versionFolder))
                                {
                                    savePath = Path.Combine(tempResourceFolder, version.ToString(), abName);
                                }
                                else
                                {
                                    savePath = Path.Combine(tempResourceFolder, versionFolder, abName);
                                }
                                
                                // 同步下载（在Parallel.ForEach中，每个线程独立执行）
                                DownloadFileSync(downloadUrl, savePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to download {abName}: {ex.Message}");
                        }
                        finally
                        {
                            lock (parallelDownloadInfo)
                            {
                                parallelDownloadInfo.displayTxt = abName;
                                parallelDownloadInfo.currentIdx++;
                                
                                // 计算并报告进度百分比
                                var progressValue = (int)((double)parallelDownloadInfo.currentIdx / parallelDownloadInfo.totalCount * 100);
                                progress?.Report(progressValue);
                                
                                // 每下载10个文件输出一次进度
                                if (parallelDownloadInfo.currentIdx % 10 == 0 || parallelDownloadInfo.currentIdx == parallelDownloadInfo.totalCount)
                                {
                                    Logger.Info($"Download progress: {parallelDownloadInfo.currentIdx}/{parallelDownloadInfo.totalCount} ({progressValue}%)");
                                }
                            }
                        }
                    });

                    parallelDownloadInfo.taskCompletionSource.SetResult(true);
                });

                // 等待下载完成
                await parallelDownloadInfo.taskCompletionSource.Task;
                
                // 报告完成进度
                progress?.Report(100);
                
                stopWatch.Stop();
                Logger.Info($"Download of version [{version}] resources completed, time elapsed: {stopWatch.ElapsedMilliseconds} ms ({stopWatch.Elapsed.TotalSeconds:F2} seconds)");
            }
            catch (Exception e)
            {
                Logger.Error($"Parallel download failed: {e.Message}");
                throw;
            }
            finally
            {
                if (parallelDownloadInfo.taskCompletionSource != null && !parallelDownloadInfo.taskCompletionSource.Task.IsCompleted)
                {
                    parallelDownloadInfo.taskCompletionSource.SetResult(true);
                }
            }
        }
        
        /// <summary>
        /// 同步下载文件（用于Parallel.ForEach中）
        /// </summary>
        private static void DownloadFileSync(string fileUrl, string savePath)
        {
            try
            {
                using (var client = new WebClient())
                {
                    // 确保目录存在
                    var folder = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    // 同步下载文件
                    client.DownloadFile(fileUrl, savePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download file {Path.GetFileName(savePath)}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 下载文件到指定路径
        /// </summary>
        /// <param name="fileUrl">文件URL</param>
        /// <param name="savePath">保存路径（完整的文件路径，包含文件名）</param>
        public static async Task DownloadFileAsync(string fileUrl, string savePath)
        {
            try
            {
                Logger.Info($"Downloading: {fileUrl}");

                using (var client = new WebClient())
                {
                    // 确保目录存在
                    var folder = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    // 异步下载文件
                    await client.DownloadFileTaskAsync(fileUrl, savePath);

                    Logger.Info($"Download completed: {Path.GetFileName(savePath)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download file {fileUrl}: {ex.Message}");
                throw;
            }
        }
    }
}