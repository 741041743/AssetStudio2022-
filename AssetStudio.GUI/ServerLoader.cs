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
        public Dictionary<string, string[]> list;
        public Dictionary<string, string[]> key2mark;
        public Dictionary<string, string[]> mark2list;
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
            if (string.IsNullOrEmpty(key)) return "current";
            var hashL = (list.ContainsKey(key) ? list[key] : null);
            if (hashL != null && hashL.Length >= 1)
            {
                return hashL[0];
            }
            return "current";
        }

     // ChooseMark扩展
     public static hashCheck ChooseMark(hashCheck n, params string[] keys)
     {
         var key2mark = n.key2mark;
         if (key2mark == null)
             return n;

         if (keys == null || keys.Length <= 1)
             return ChooseMark(n, keys.Length == 1 ? keys[0] : string.Empty); // 0个或1个参数，跟能跟之前保持一致

         string key = keys[0];
         if (string.IsNullOrEmpty(key))
         {
             key = "allmarks";
         }
         HashSet<string> markSet = new HashSet<string>();
         if (key2mark.TryGetValue(key, out var marks))
         {
             markSet.UnionWith(marks);
         }

         if (marks == null || marks.Length == 0)
         {
             markSet.Add(key);
         }

         for(int i=1; i<keys.Length; i++) // 将剩余的key列表对应的marks添加到markset
         {
             if (!string.IsNullOrEmpty(keys[i]))
             {
                 if(key2mark.TryGetValue(keys[i], out var marks2))
                 {
                     markSet.UnionWith(marks2);
                 }

                 if (marks2 == null || marks2.Length == 0)
                 {
                     markSet.Add(keys[i]); // 添加默认的key
                 }
             }
         }

         var mark2eclude = new HashSet<string>();
         foreach (var k in key2mark.Keys)
         {
             mark2eclude.UnionWith(key2mark[k]);
         }
         mark2eclude.ExceptWith(markSet);

         var dic = n.list;
         var mark2list = n.mark2list;
         foreach (var k in mark2eclude)
         {
             if (mark2list.TryGetValue(k, out var abs))
             {
                 foreach (var ab in abs)
                 {
                     dic.Remove(ab);
                     //"".PrintEditor("remove ab",ab);
                 }
             }
         }

         return n;
     }

     public static hashCheck ChooseMark(hashCheck n,string key)
     {
         if (string.IsNullOrEmpty(key)) {
             key = "allmarks";
         }
         HashSet<string> markSet = new HashSet<string>();
         var key2mark = n.key2mark;
         var mark2list = n.mark2list;
         if (key2mark == null) return n;
         

         if (key2mark.TryGetValue(key, out var marks))
         {
             markSet.UnionWith(marks);
         }
         if(marks == null || marks.Length==0)
         {
             markSet.Add(key);
         }
         var mark2eclude = new HashSet<string>();
         foreach (var k in key2mark.Keys)
         {
             mark2eclude.UnionWith(key2mark[k]);  
         }
         mark2eclude.ExceptWith(markSet);

         var dic = n.list;
         

         foreach (var k in mark2eclude)
         {
             if(mark2list.TryGetValue(k,out var abs))
             {
                 foreach (var ab in abs)
                 {
                     dic.Remove(ab);
                     //"".PrintEditor("remove ab",ab);
                 }
             }
         }

         return n;
     }
     public static hashCheck Parse(string s,string res_key = "", string ch_res_key = "")
     {
         if (string.IsNullOrEmpty(s)) return null;
         hashCheck n = null;
         try
         {
             n = Newtonsoft.Json.JsonConvert.DeserializeObject<hashCheck>(s);
                 ChooseMark(n, res_key, ch_res_key);
             //if(!string.IsNullOrEmpty(res_key))
             //{
             //}
         }
         catch (System.Exception e)
         {
             Logger.Info($"{e.ToString()}|{e.StackTrace}");
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
            if (string.IsNullOrEmpty(hashCheck[assetbundleName]))
            {
                Logger.Error($"not find assetbundleName: {assetbundleName}");
                return null;
            }

            var h5Folder = "";
            if (resUrl.Contains("MiniGame"))
            {
                h5Folder = "MiniGame";
            }else if (resUrl.Contains("WebGL"))
            {
                h5Folder = "WebGL";
            }
            
            if (!string.IsNullOrEmpty(h5Folder))
            {
                var hash = hashCheck[assetbundleName];
                var fileName = Path.GetFileNameWithoutExtension(assetbundleName);
                var extension = Path.GetExtension(assetbundleName);
                var directory = Path.GetDirectoryName(assetbundleName);

                string newAbName;
                if (!string.IsNullOrEmpty(extension))
                {
                    newAbName = $"{fileName}_{hash}{extension}";
                }
                else
                {
                    newAbName = $"{fileName}_{hash}";
                }

                if (!string.IsNullOrEmpty(directory))
                {
                    newAbName = Path.Combine(directory, newAbName).Replace("\\", "/");
                }

                return Path.Combine(resUrl.Replace("resource",$"StreamingAssets/AssetBundles/{h5Folder}"), newAbName).Replace("\\", "/");
            }

            var uri = new Uri(resUrl);
            return new Uri(uri, $"{hashCheck.GetVer(assetbundleName)}/{assetbundleName}").ToString();
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
                            Logger.Error($"Failed to download { GetResUrl(abName, resourcesHashCheck, baseServerResourcePath)}: {ex.Message}");
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
                Logger.Error($"Failed to download file {savePath}: {ex.Message}");
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