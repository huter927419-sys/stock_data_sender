using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// 数据管理器 - 统一管理所有股票数据的缓存和更新
    /// </summary>
    public class DataManager
    {
        /// <summary>
        /// 全局股票数据缓存（代码 -> 数据）
        /// 注意：这个缓存会不断更新，不会无限增长（因为股票数量是固定的）
        /// </summary>
        private ConcurrentDictionary<string, StockData> globalStockCache = new ConcurrentDictionary<string, StockData>();
        
        // 缓存大小限制（防止内存溢出，但实际上股票数量是固定的，不会超过这个值）
        private const int MAX_CACHE_SIZE = 50000;  // 最多缓存5万只股票

        /// <summary>
        /// 股票代码字典（代码 -> 名称）
        /// </summary>
        private ConcurrentDictionary<string, string> stockCodeDict = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// 数据更新事件
        /// </summary>
        public event EventHandler DataUpdated;

        // 数据更新事件防抖控制
        private System.Threading.Timer dataUpdateTimer;
        private bool dataUpdatePending = false;
        private readonly object dataUpdateLock = new object();

        /// <summary>
        /// 更新基础代码表（股票代码和名称）- 优化版本，只存储标准化代码
        /// 注意：代码和名称单独存储，不存储不带前缀的版本（避免重复）
        /// </summary>
        public void UpdateStockCodeTable(string code, string name)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                return;

            // 标准化股票代码
            string normalizedCode = DataConverter.NormalizeStockCode(code);
            
            // 更新代码表（只存储标准化代码，避免重复）
            // 代码和名称单独存储，不存储不带前缀的版本
            if (!string.IsNullOrEmpty(name))
            {
                stockCodeDict[normalizedCode] = name;
                
                // 拼音索引功能已移除
            }
        }

        /// <summary>
        /// 更新股票数据（更新全局缓存和所有相关板块）- 优化性能版本
        /// 注意：此方法只更新实时数据（价格、涨跌幅、成交量等），不更新基础代码表
        /// </summary>
        public void UpdateStockData(StockData stockData)
        {
            if (stockData == null || string.IsNullOrEmpty(stockData.Code))
                return;

            // 标准化股票代码
            string normalizedCode = DataConverter.NormalizeStockCode(stockData.Code);
            stockData.Code = normalizedCode;

            // 如果实时数据中包含股票名称，更新基础代码表
            if (!string.IsNullOrEmpty(stockData.Name))
            {
                UpdateStockCodeTable(normalizedCode, stockData.Name);
            }
            else
            {
                // 如果实时数据中没有名称，尝试从代码表中获取
                if (stockCodeDict.ContainsKey(normalizedCode))
                {
                    stockData.Name = stockCodeDict[normalizedCode];
                }
            }

            // 更新全局缓存（直接更新，不触发事件）
            // 注意：这里直接覆盖更新，不会导致内存无限增长
            // 因为股票数量是固定的（约1-2万只），只是不断更新这些股票的数据
            // 如果已存在，直接更新现有对象，避免创建新对象（减少GC压力）
            if (globalStockCache.ContainsKey(normalizedCode))
            {
                // 更新现有对象，而不是替换（减少对象创建）
                StockData existingData = globalStockCache[normalizedCode];
                existingData.Name = stockData.Name ?? existingData.Name;
                existingData.LastClose = stockData.LastClose;
                existingData.Open = stockData.Open;
                existingData.High = stockData.High;
                existingData.Low = stockData.Low;
                existingData.NewPrice = stockData.NewPrice;
                existingData.Volume = stockData.Volume;
                existingData.Amount = stockData.Amount;
                existingData.UpdateTime = stockData.UpdateTime;
                existingData.MarketType = stockData.MarketType;
                existingData.TradeTime = stockData.TradeTime;
                
                // 更新买卖盘数据（5档）
                if (stockData.BuyPrice != null && stockData.BuyPrice.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        existingData.BuyPrice[i] = stockData.BuyPrice[i];
                    }
                }
                if (stockData.BuyVolume != null && stockData.BuyVolume.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        existingData.BuyVolume[i] = stockData.BuyVolume[i];
                    }
                }
                if (stockData.SellPrice != null && stockData.SellPrice.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        existingData.SellPrice[i] = stockData.SellPrice[i];
                    }
                }
                if (stockData.SellVolume != null && stockData.SellVolume.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        existingData.SellVolume[i] = stockData.SellVolume[i];
                    }
                }
                
                existingData.CalculateChangePercent();
                existingData.CalculateChangeAmount();
            }
            else
            {
                // 新股票，添加到缓存
                globalStockCache[normalizedCode] = stockData;
            }
            
            // 如果缓存过大，记录警告（用于调试）
            if (globalStockCache.Count > MAX_CACHE_SIZE)
            {
                Logger.Instance.Warning(string.Format("股票数据缓存异常: {0} 只股票（超过限制 {1}），可能存在内存问题", globalStockCache.Count, MAX_CACHE_SIZE));
            }

            // 板块管理功能已移除

            // 使用防抖机制，减少事件触发频率
            lock (dataUpdateLock)
            {
                if (!dataUpdatePending)
                {
                    dataUpdatePending = true;
                    if (dataUpdateTimer == null)
                    {
                        dataUpdateTimer = new System.Threading.Timer(OnDataUpdateTimer, null, 500, System.Threading.Timeout.Infinite);
                    }
                    else
                    {
                        dataUpdateTimer.Change(500, System.Threading.Timeout.Infinite);
                    }
                }
            }
        }
        
        /// <summary>
        /// 数据更新定时器回调（防抖机制）
        /// </summary>
        private void OnDataUpdateTimer(object state)
        {
            lock (dataUpdateLock)
            {
                dataUpdatePending = false;
                if (DataUpdated != null)
                {
                    DataUpdated(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 批量更新股票数据
        /// </summary>
        public void UpdateStockDataBatch(List<StockData> stockDataList)
        {
            foreach (var stockData in stockDataList)
            {
                UpdateStockData(stockData);
            }
        }

        /// <summary>
        /// 更新股票代码字典（码表数据）- 优化版本，只存储标准化代码
        /// 注意：代码和名称单独存储，避免重复存储
        /// </summary>
        public void UpdateStockCodeDictionary(Dictionary<string, string> codeDict)
        {
            if (codeDict == null)
                return;

            // 批量更新，使用标准化代码作为键，避免重复
            foreach (var kvp in codeDict)
            {
                string code = DataConverter.NormalizeStockCode(kvp.Key);
                // 只存储标准化代码，不存储不带前缀的版本（避免重复）
                stockCodeDict[code] = kvp.Value;
            }
        }

        /// <summary>
        /// 根据代码获取股票名称
        /// </summary>
        public string GetStockName(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            code = code.Trim().ToUpper();

            // 直接查找（支持各种格式）
            if (stockCodeDict.ContainsKey(code))
            {
                return stockCodeDict[code];
            }

            // 尝试标准化后查找
            string normalizedCode = DataConverter.NormalizeStockCode(code);
            if (!string.IsNullOrEmpty(normalizedCode) && normalizedCode != code)
            {
                if (stockCodeDict.ContainsKey(normalizedCode))
                {
                    return stockCodeDict[normalizedCode];
                }
            }

            // 如果输入的是带前缀的代码（如sh600026），尝试去掉前缀查找
            if (code.Length > 2 && (code.StartsWith("SH") || code.StartsWith("SZ") || code.StartsWith("BJ")))
            {
                string codeWithoutPrefix = code.Substring(2);
                if (stockCodeDict.ContainsKey(codeWithoutPrefix))
                {
                    return stockCodeDict[codeWithoutPrefix];
                }
            }

            // 如果输入的是6位数字，尝试添加市场前缀查找
            if (code.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(code, @"^\d{6}$"))
            {
                // 上海市场
                if (code.StartsWith("60") || code.StartsWith("68") || code.StartsWith("605"))
                {
                    string shCode = "SH" + code;
                    if (stockCodeDict.ContainsKey(shCode))
                    {
                        return stockCodeDict[shCode];
                    }
                    // 也尝试不带前缀的
                    if (stockCodeDict.ContainsKey(code))
                    {
                        return stockCodeDict[code];
                    }
                }
                // 深圳市场
                else if (code.StartsWith("00") || code.StartsWith("300"))
                {
                    string szCode = "SZ" + code;
                    if (stockCodeDict.ContainsKey(szCode))
                    {
                        return stockCodeDict[szCode];
                    }
                    // 也尝试不带前缀的
                    if (stockCodeDict.ContainsKey(code))
                    {
                        return stockCodeDict[code];
                    }
                }
                // 北交所
                else if (code.StartsWith("8") || code.StartsWith("43") || code.StartsWith("83") || code.StartsWith("87"))
                {
                    string bjCode = "BJ" + code;
                    if (stockCodeDict.ContainsKey(bjCode))
                    {
                        return stockCodeDict[bjCode];
                    }
                    // 也尝试不带前缀的
                    if (stockCodeDict.ContainsKey(code))
                    {
                        return stockCodeDict[code];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取全局缓存中的股票数据
        /// </summary>
        public StockData GetStockData(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            string normalizedCode = DataConverter.NormalizeStockCode(code);
            if (globalStockCache.ContainsKey(normalizedCode))
            {
                return globalStockCache[normalizedCode];
            }

            return null;
        }

        /// <summary>
        /// 清除所有数据（开盘前调用）
        /// </summary>
        public void ClearAllData()
        {
            globalStockCache.Clear();
            stockCodeDict.Clear();
            
            // 强制垃圾回收，释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        
        /// <summary>
        /// 清理资源（程序退出时调用，防止内存泄漏）
        /// </summary>
        public void Cleanup()
        {
            // 停止并释放定时器
            lock (dataUpdateLock)
            {
                if (dataUpdateTimer != null)
                {
                    dataUpdateTimer.Dispose();
                    dataUpdateTimer = null;
                }
                dataUpdatePending = false;
            }
            
            // 清理事件订阅
            DataUpdated = null;
        }
        
        /// <summary>
        /// 获取缓存统计信息（用于内存监控）
        /// </summary>
        public string GetCacheStatistics()
        {
            return string.Format("股票数据缓存: {0} 只, 代码表: {1} 只", globalStockCache.Count, stockCodeDict.Count);
        }

        /// <summary>
        /// 获取股票代码字典的副本（线程安全）
        /// </summary>
        public Dictionary<string, string> GetStockCodeDictionary()
        {
            // ConcurrentDictionary的ToArray()是线程安全的
            return stockCodeDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// 获取股票代码字典的数量
        /// </summary>
        public int GetStockCodeCount()
        {
            return stockCodeDict.Count;
        }
        
        /// <summary>
        /// 更新拼音首字母索引（已移除，不再需要）
        /// </summary>
        private void UpdatePinyinIndex(string code, string name)
        {
            // 拼音索引功能已移除
        }
    }
}

