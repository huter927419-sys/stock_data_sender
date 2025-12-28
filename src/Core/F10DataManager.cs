using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// F10数据管理器 - 负责F10数据的本地缓存、自动同步和定时更新
    /// </summary>
    public class F10DataManager
    {
        private string cacheDirectory;
        private Dictionary<string, F10CacheInfo> cacheInfoDict;  // 股票代码 -> 缓存信息
        private System.Windows.Forms.Timer updateTimer;  // 定时更新定时器
        private DataCollector dataCollector;  // 数据采集器引用
        // F10功能已禁用 - 保留字段以避免编译警告
        #pragma warning disable 0414
        private bool isAutoSyncEnabled = false;  // 是否启用自动同步
        #pragma warning restore 0414
        private int updateIntervalHours = 24;  // 更新间隔（小时）
        private object lockObject = new object();  // 线程锁
        
        /// <summary>
        /// F10缓存信息
        /// </summary>
        private class F10CacheInfo
        {
            private string _stockCode;
            public string StockCode 
            {
                get { return _stockCode; }
                set { _stockCode = value; }
            }
            private string _filePath;
            public string FilePath 
            {
                get { return _filePath; }
                set { _filePath = value; }
            }
            private DateTime _lastUpdateTime;
            public DateTime LastUpdateTime 
            {
                get { return _lastUpdateTime; }
                set { _lastUpdateTime = value; }
            }
            private long _fileSize;
            public long FileSize 
            {
                get { return _fileSize; }
                set { _fileSize = value; }
            }
            private bool _isUpdating;
            public bool IsUpdating // 是否正在更新
            {
                get { return _isUpdating; }
                set { _isUpdating = value; }
            }
        }
        
        /// <summary>
        /// F10数据更新事件（保留用于未来扩展）
        /// </summary>
#pragma warning disable CS0067 // 事件从未使用
        public event EventHandler<F10DataUpdatedEventArgs> F10DataUpdated;
#pragma warning restore CS0067
        
        public class F10DataUpdatedEventArgs : EventArgs
        {
            private string _stockCode;
            public string StockCode 
            {
                get { return _stockCode; }
                set { _stockCode = value; }
            }
            private string _content;
            public string Content 
            {
                get { return _content; }
                set { _content = value; }
            }
            private string _fileName;
            public string FileName 
            {
                get { return _fileName; }
                set { _fileName = value; }
            }
            private bool _fromCache;
            public bool FromCache // 是否来自缓存
            {
                get { return _fromCache; }
                set { _fromCache = value; }
            }
        }
        
        public F10DataManager(DataCollector collector)
        {
            dataCollector = collector;
            cacheInfoDict = new Dictionary<string, F10CacheInfo>();
            
            // 设置缓存目录（应用程序目录下的F10Cache文件夹）
            // 注意：.NET Framework 3.5 不支持 Application.StartupPath，使用 AppDomain.CurrentDomain.BaseDirectory
            cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "F10Cache");
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
                Logger.Instance.Info(string.Format("创建F10缓存目录: {0}", cacheDirectory));
            }
            
            // 加载现有缓存信息
            LoadCacheInfo();
            
            // 初始化定时更新定时器
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 60 * 60 * 1000;  // 每小时检查一次
            // F10功能已禁用
            // updateTimer.Tick += UpdateTimer_Tick;
        }
        
        /// <summary>
        /// 启用自动同步
        /// </summary>
        public void EnableAutoSync(int intervalHours)
        {
            lock (lockObject)
            {
                isAutoSyncEnabled = true;
                updateIntervalHours = intervalHours;
                updateTimer.Interval = 60 * 60 * 1000;  // 每小时检查一次
                updateTimer.Start();
                Logger.Instance.Info(string.Format("F10自动同步已启用，更新间隔: {0} 小时", updateIntervalHours));
            }
        }
        
        /// <summary>
        /// 禁用自动同步
        /// </summary>
        public void DisableAutoSync()
        {
            lock (lockObject)
            {
                isAutoSyncEnabled = false;
                updateTimer.Stop();
                Logger.Instance.Info("F10自动同步已禁用");
            }
        }
        
        /// <summary>
        /// 加载缓存信息
        /// </summary>
        private void LoadCacheInfo()
        {
            try
            {
                if (!Directory.Exists(cacheDirectory))
                    return;
                
                string[] files = Directory.GetFiles(cacheDirectory, "*.txt", SearchOption.TopDirectoryOnly);
                int loadedCount = 0;
                
                foreach (string filePath in files)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        string stockCode = DataConverter.NormalizeStockCode(fileName);
                        
                        if (!string.IsNullOrEmpty(stockCode))
                        {
                            FileInfo fileInfo = new FileInfo(filePath);
                            cacheInfoDict[stockCode] = new F10CacheInfo
                            {
                                StockCode = stockCode,
                                FilePath = filePath,
                                LastUpdateTime = fileInfo.LastWriteTime,
                                FileSize = fileInfo.Length,
                                IsUpdating = false
                            };
                            loadedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning(string.Format("加载F10缓存文件信息失败: {0}, 错误: {1}", filePath, ex.Message));
                    }
                }
                
                Logger.Instance.Info(string.Format("F10缓存信息加载完成: 共 {0} 个文件", loadedCount));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载F10缓存信息失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 保存F10数据到本地
        /// </summary>
        public void SaveF10Data(string stockCode, string content, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(stockCode) || string.IsNullOrEmpty(content))
                {
                    Logger.Instance.Warning("保存F10数据失败: 股票代码或内容为空");
                    return;
                }
                
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                string filePath = Path.Combine(cacheDirectory, normalizedCode + ".txt");
                
                lock (lockObject)
                {
                    // 保存文件
                    File.WriteAllText(filePath, content, System.Text.Encoding.Default);
                    
                    // 更新缓存信息
                    FileInfo fileInfo = new FileInfo(filePath);
                    cacheInfoDict[normalizedCode] = new F10CacheInfo
                    {
                        StockCode = normalizedCode,
                        FilePath = filePath,
                        LastUpdateTime = DateTime.Now,
                        FileSize = fileInfo.Length,
                        IsUpdating = false
                    };
                    
                    Logger.Instance.Info(string.Format("F10数据已保存到本地: {0}, 大小={1} 字节", normalizedCode, fileInfo.Length));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("保存F10数据失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 从本地缓存读取F10数据
        /// </summary>
        public string LoadF10DataFromCache(string stockCode)
        {
            try
            {
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                
                lock (lockObject)
                {
                    if (cacheInfoDict.ContainsKey(normalizedCode))
                    {
                        F10CacheInfo cacheInfo = cacheInfoDict[normalizedCode];
                        if (File.Exists(cacheInfo.FilePath))
                        {
                            string content = File.ReadAllText(cacheInfo.FilePath, System.Text.Encoding.Default);
                            Logger.Instance.Debug(string.Format("从缓存加载F10数据: {0}, 大小={1} 字符, 更新时间={2:yyyy-MM-dd HH:mm:ss}", normalizedCode, content.Length, cacheInfo.LastUpdateTime));
                            return content;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("从缓存加载F10数据失败: {0}", ex.Message));
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查F10数据是否需要更新
        /// </summary>
        public bool NeedUpdate(string stockCode)
        {
            try
            {
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                
                lock (lockObject)
                {
                    if (!cacheInfoDict.ContainsKey(normalizedCode))
                    {
                        return true;  // 没有缓存，需要更新
                    }
                    
                    F10CacheInfo cacheInfo = cacheInfoDict[normalizedCode];
                    TimeSpan elapsed = DateTime.Now - cacheInfo.LastUpdateTime;
                    
                    // 如果超过更新间隔，需要更新
                    if (elapsed.TotalHours >= updateIntervalHours)
                    {
                        return true;
                    }
                    
                    // 如果文件不存在，需要更新
                    if (!File.Exists(cacheInfo.FilePath))
                    {
                        return true;
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("检查F10更新状态失败: {0}", ex.Message));
                return true;  // 出错时默认需要更新
            }
        }
        
        /* // F10功能已禁用
        /// <summary>
        /// 批量请求F10数据（自动同步）
        /// </summary>
        public void BatchRequestF10Data(List<string> stockCodes, int batchSize = 10, int delayMs = 1000)
        {
            if (!isAutoSyncEnabled || dataCollector == null)
            {
                Logger.Instance.Warning("F10自动同步未启用或数据采集器未初始化");
                return;
            }

            Logger.Instance.Info(string.Format("开始批量请求F10数据: 共 {0} 只股票, 批次大小={1}, 延迟={2}ms", stockCodes.Count, batchSize, delayMs));

            // 使用线程池异步处理，避免阻塞UI
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int requested = 0;
                    int skipped = 0;

                    foreach (string stockCode in stockCodes)
                    {
                        try
                        {
                            string normalizedCode = DataConverter.NormalizeStockCode(stockCode);

                            // 检查是否需要更新
                            if (!NeedUpdate(normalizedCode))
                            {
                                skipped++;
                                continue;
                            }

                            // 检查是否正在更新
                            lock (lockObject)
                            {
                                if (cacheInfoDict.ContainsKey(normalizedCode) &&
                                    cacheInfoDict[normalizedCode].IsUpdating)
                                {
                                    skipped++;
                                    continue;
                                }

                                // 标记为正在更新
                                if (!cacheInfoDict.ContainsKey(normalizedCode))
                                {
                                    cacheInfoDict[normalizedCode] = new F10CacheInfo
                                    {
                                        StockCode = normalizedCode,
                                        IsUpdating = true
                                    };
                                }
                                else
                                {
                                    cacheInfoDict[normalizedCode].IsUpdating = true;
                                }
                            }

                            // 请求F10数据
                            bool success = dataCollector.RequestStockBaseData(normalizedCode);
                            if (success)
                            {
                                requested++;
                                if (requested % batchSize == 0)
                                {
                                    Logger.Instance.Info(string.Format("F10批量请求进度: 已请求 {0} 只, 跳过 {1} 只", requested, skipped));
                                }
                            }

                            // 延迟，避免请求过快
                            Thread.Sleep(delayMs);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Warning(string.Format("批量请求F10数据失败: {0}, 错误: {1}", stockCode, ex.Message));
                        }
                    }

                    Logger.Instance.Success(string.Format("F10批量请求完成: 已请求 {0} 只, 跳过 {1} 只", requested, skipped));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("批量请求F10数据异常: {0}", ex.Message));
                }
            });
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// 定时更新检查
        /// </summary>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!isAutoSyncEnabled)
                return;

            try
            {
                Logger.Instance.Info("F10定时更新检查开始...");

                // 获取需要更新的股票列表
                List<string> needUpdateStocks = new List<string>();

                lock (lockObject)
                {
                    foreach (var kvp in cacheInfoDict)
                    {
                        if (NeedUpdate(kvp.Key) && !kvp.Value.IsUpdating)
                        {
                            needUpdateStocks.Add(kvp.Key);
                        }
                    }
                }

                if (needUpdateStocks.Count > 0)
                {
                    Logger.Instance.Info(string.Format("发现 {0} 只股票需要更新F10数据", needUpdateStocks.Count));
                    BatchRequestF10Data(needUpdateStocks, batchSize: 5, delayMs: 2000);  // 定时更新时使用更小的批次和更长的延迟
                }
                else
                {
                    Logger.Instance.Debug("F10定时更新检查: 无需更新的股票");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("F10定时更新检查失败: {0}", ex.Message));
            }
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// 获取F10数据（优先从缓存，如果不存在或过期则请求）
        /// </summary>
        public string GetF10Data(string stockCode, bool forceUpdate = false)
        {
            string normalizedCode = DataConverter.NormalizeStockCode(stockCode);

            // 如果不强制更新，先尝试从缓存读取
            if (!forceUpdate)
            {
                string cachedContent = LoadF10DataFromCache(normalizedCode);
                if (!string.IsNullOrEmpty(cachedContent) && !NeedUpdate(normalizedCode))
                {
                    Logger.Instance.Debug(string.Format("从缓存返回F10数据: {0}", normalizedCode));
                    return cachedContent;
                }
            }

            // 需要更新，请求新数据
            if (dataCollector != null)
            {
                Logger.Instance.Info(string.Format("请求F10数据: {0} (缓存不存在或已过期)", normalizedCode));
                dataCollector.RequestStockBaseData(normalizedCode);
            }

            return null;
        }
        */
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public string GetCacheStatistics()
        {
            lock (lockObject)
            {
                int totalCount = cacheInfoDict.Count;
                int needUpdateCount = 0;
                long totalSize = 0;
                
                foreach (var kvp in cacheInfoDict)
                {
                    if (NeedUpdate(kvp.Key))
                    {
                        needUpdateCount++;
                    }
                    totalSize += kvp.Value.FileSize;
                }
                
                return string.Format("F10缓存统计: 总数={0}, 需要更新={1}, 总大小={2}MB", totalCount, needUpdateCount, totalSize / 1024 / 1024);
            }
        }
        
        /// <summary>
        /// 清理过期缓存
        /// </summary>
        /// <summary>
        /// 清理资源（程序退出时调用，防止内存泄漏）
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (updateTimer != null)
                {
                    updateTimer.Stop();
                    updateTimer.Dispose();
                    updateTimer = null;
                }
            }
            catch { }
        }
        
        public void CleanupExpiredCache(int daysToKeep)
        {
            try
            {
                DateTime cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                int deletedCount = 0;
                
                lock (lockObject)
                {
                    List<string> toDelete = new List<string>();
                    
                    foreach (var kvp in cacheInfoDict)
                    {
                        if (kvp.Value.LastUpdateTime < cutoffDate)
                        {
                            toDelete.Add(kvp.Key);
                        }
                    }
                    
                    foreach (string stockCode in toDelete)
                    {
                        try
                        {
                            F10CacheInfo cacheInfo = cacheInfoDict[stockCode];
                            if (File.Exists(cacheInfo.FilePath))
                            {
                                File.Delete(cacheInfo.FilePath);
                            }
                            cacheInfoDict.Remove(stockCode);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Warning(string.Format("删除过期F10缓存失败: {0}, 错误: {1}", stockCode, ex.Message));
                        }
                    }
                }
                
                Logger.Instance.Info(string.Format("F10缓存清理完成: 删除 {0} 个过期文件（超过 {1} 天）", deletedCount, daysToKeep));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("清理F10缓存失败: {0}", ex.Message));
            }
        }
    }
}

