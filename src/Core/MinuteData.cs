using System;
using System.Collections.Generic;

namespace StockDataMQClient
{
    /// <summary>
    /// 分钟数据模型
    /// </summary>
    public class MinuteData
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            set { _stockCode = value; }
        }
        private DateTime _tradeTime;
        public DateTime TradeTime
        {
            get { return _tradeTime; }
            set { _tradeTime = value; }
        }
        private float _price;
        public float Price
        {
            get { return _price; }
            set { _price = value; }
        }
        private float _volume;
        public float Volume
        {
            get { return _volume; }
            set { _volume = value; }
        }
        private float _amount;
        public float Amount
        {
            get { return _amount; }
            set { _amount = value; }
        }

        public MinuteData()
        {
        }

        public MinuteData(string code, DateTime time, float price, float volume, float amount)
        {
            StockCode = code;
            TradeTime = time;
            Price = price;
            Volume = volume;
            Amount = amount;
        }
    }

    /// <summary>
    /// 5分钟K线数据模型
    /// </summary>
    public class Minute5Data
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            set { _stockCode = value; }
        }
        private DateTime _tradeTime;
        public DateTime TradeTime
        {
            get { return _tradeTime; }
            set { _tradeTime = value; }
        }
        private float _open;
        public float Open
        {
            get { return _open; }
            set { _open = value; }
        }
        private float _high;
        public float High
        {
            get { return _high; }
            set { _high = value; }
        }
        private float _low;
        public float Low
        {
            get { return _low; }
            set { _low = value; }
        }
        private float _close;
        public float Close
        {
            get { return _close; }
            set { _close = value; }
        }
        private float _volume;
        public float Volume
        {
            get { return _volume; }
            set { _volume = value; }
        }
        private float _amount;
        public float Amount
        {
            get { return _amount; }
            set { _amount = value; }
        }
        private float _activeBuyVol;
        public float ActiveBuyVol
        {
            get { return _activeBuyVol; }
            set { _activeBuyVol = value; }
        }

        public Minute5Data()
        {
        }
    }

    /// <summary>
    /// 股票分钟数据管理器 - 支持数据一致化和去重
    /// </summary>
    public class MinuteDataManager
    {
        // 缓存大小限制常量
        private const int MAX_MINUTE_DATA_COUNT = 10000;    // 分钟数据最大缓存条数（约7天的数据，每天240条）
        private const int MAX_MINUTE5_DATA_COUNT = 5000;    // 5分钟数据最大缓存条数（约17天的数据，每天约48条）
        
        // 股票代码 -> 分钟数据列表（按时间排序，去重）
        private Dictionary<string, List<MinuteData>> minuteDataCache = new Dictionary<string, List<MinuteData>>();
        
        // 股票代码 -> 5分钟数据列表（按时间排序，去重）
        private Dictionary<string, List<Minute5Data>> minute5DataCache = new Dictionary<string, List<Minute5Data>>();
        
        // 用于快速查找和去重的索引（股票代码 -> 时间 -> 数据索引）
        private Dictionary<string, Dictionary<DateTime, int>> minuteDataIndex = new Dictionary<string, Dictionary<DateTime, int>>();
        private Dictionary<string, Dictionary<DateTime, int>> minute5DataIndex = new Dictionary<string, Dictionary<DateTime, int>>();

        /// <summary>
        /// 添加分钟数据（支持更新和去重）
        /// </summary>
        public void AddMinuteData(string stockCode, MinuteData data)
        {
            if (string.IsNullOrEmpty(stockCode) || data == null)
                return;

            // 标准化股票代码
            stockCode = DataConverter.NormalizeStockCode(stockCode);
            data.StockCode = stockCode;

            if (!minuteDataCache.ContainsKey(stockCode))
            {
                minuteDataCache[stockCode] = new List<MinuteData>();
                minuteDataIndex[stockCode] = new Dictionary<DateTime, int>();
            }

            // 检查是否已存在相同时间的数据（去重和更新）
            if (minuteDataIndex[stockCode].ContainsKey(data.TradeTime))
            {
                // 更新现有数据（使用最新数据）
                int index = minuteDataIndex[stockCode][data.TradeTime];
                minuteDataCache[stockCode][index] = data;
            }
            else
            {
                // 添加新数据
                minuteDataCache[stockCode].Add(data);
                minuteDataIndex[stockCode][data.TradeTime] = minuteDataCache[stockCode].Count - 1;
            }

            // 保持数据按时间排序
            minuteDataCache[stockCode].Sort((a, b) => a.TradeTime.CompareTo(b.TradeTime));
            
            // 重建索引（排序后索引会变化）
            RebuildMinuteDataIndex(stockCode);

            // 保持最近的数据（限制最大缓存条数）
            if (minuteDataCache[stockCode].Count > MAX_MINUTE_DATA_COUNT)
            {
                int removeCount = minuteDataCache[stockCode].Count - MAX_MINUTE_DATA_COUNT;
                minuteDataCache[stockCode].RemoveRange(0, removeCount);
                RebuildMinuteDataIndex(stockCode);
            }
        }

        /// <summary>
        /// 批量添加分钟数据（支持更新和去重）
        /// </summary>
        public void AddMinuteDataBatch(string stockCode, List<MinuteData> dataList)
        {
            if (string.IsNullOrEmpty(stockCode) || dataList == null || dataList.Count == 0)
                return;

            // 标准化股票代码
            stockCode = DataConverter.NormalizeStockCode(stockCode);

            if (!minuteDataCache.ContainsKey(stockCode))
            {
                minuteDataCache[stockCode] = new List<MinuteData>();
                minuteDataIndex[stockCode] = new Dictionary<DateTime, int>();
            }

            // 批量处理：去重和更新
            foreach (var data in dataList)
            {
                data.StockCode = stockCode;
                
                // 检查是否已存在相同时间的数据
                if (minuteDataIndex[stockCode].ContainsKey(data.TradeTime))
                {
                    // 更新现有数据（使用最新数据）
                    int index = minuteDataIndex[stockCode][data.TradeTime];
                    minuteDataCache[stockCode][index] = data;
                }
                else
                {
                    // 添加新数据
                    minuteDataCache[stockCode].Add(data);
                }
            }

            // 保持数据按时间排序
            minuteDataCache[stockCode].Sort((a, b) => a.TradeTime.CompareTo(b.TradeTime));
            
            // 重建索引
            RebuildMinuteDataIndex(stockCode);

            // 保持最近的数据（限制最大缓存条数）
            if (minuteDataCache[stockCode].Count > MAX_MINUTE_DATA_COUNT)
            {
                int removeCount = minuteDataCache[stockCode].Count - MAX_MINUTE_DATA_COUNT;
                minuteDataCache[stockCode].RemoveRange(0, removeCount);
                RebuildMinuteDataIndex(stockCode);
            }
        }

        /// <summary>
        /// 重建分钟数据索引
        /// </summary>
        private void RebuildMinuteDataIndex(string stockCode)
        {
            if (!minuteDataIndex.ContainsKey(stockCode))
            {
                minuteDataIndex[stockCode] = new Dictionary<DateTime, int>();
            }
            
            minuteDataIndex[stockCode].Clear();
            for (int i = 0; i < minuteDataCache[stockCode].Count; i++)
            {
                minuteDataIndex[stockCode][minuteDataCache[stockCode][i].TradeTime] = i;
            }
        }

        /// <summary>
        /// 添加5分钟数据（支持更新和去重）
        /// </summary>
        public void AddMinute5Data(string stockCode, Minute5Data data)
        {
            if (string.IsNullOrEmpty(stockCode) || data == null)
                return;

            // 标准化股票代码
            stockCode = DataConverter.NormalizeStockCode(stockCode);
            data.StockCode = stockCode;

            if (!minute5DataCache.ContainsKey(stockCode))
            {
                minute5DataCache[stockCode] = new List<Minute5Data>();
                minute5DataIndex[stockCode] = new Dictionary<DateTime, int>();
            }

            // 检查是否已存在相同时间的数据（去重和更新）
            if (minute5DataIndex[stockCode].ContainsKey(data.TradeTime))
            {
                // 更新现有数据（使用最新数据）
                int index = minute5DataIndex[stockCode][data.TradeTime];
                minute5DataCache[stockCode][index] = data;
            }
            else
            {
                // 添加新数据
                minute5DataCache[stockCode].Add(data);
                minute5DataIndex[stockCode][data.TradeTime] = minute5DataCache[stockCode].Count - 1;
            }

            // 保持数据按时间排序
            minute5DataCache[stockCode].Sort((a, b) => a.TradeTime.CompareTo(b.TradeTime));
            
            // 重建索引
            RebuildMinute5DataIndex(stockCode);

            // 保持最近的数据（限制最大缓存条数）
            if (minute5DataCache[stockCode].Count > MAX_MINUTE5_DATA_COUNT)
            {
                int removeCount = minute5DataCache[stockCode].Count - MAX_MINUTE5_DATA_COUNT;
                minute5DataCache[stockCode].RemoveRange(0, removeCount);
                RebuildMinute5DataIndex(stockCode);
            }
        }

        /// <summary>
        /// 重建5分钟数据索引
        /// </summary>
        private void RebuildMinute5DataIndex(string stockCode)
        {
            if (!minute5DataIndex.ContainsKey(stockCode))
            {
                minute5DataIndex[stockCode] = new Dictionary<DateTime, int>();
            }
            
            minute5DataIndex[stockCode].Clear();
            for (int i = 0; i < minute5DataCache[stockCode].Count; i++)
            {
                minute5DataIndex[stockCode][minute5DataCache[stockCode][i].TradeTime] = i;
            }
        }

        /// <summary>
        /// 获取股票的分钟数据（返回最新的、去重后的、按时间排序的数据）
        /// </summary>
        public List<MinuteData> GetMinuteData(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return new List<MinuteData>();

            // 标准化股票代码
            stockCode = DataConverter.NormalizeStockCode(stockCode);

            if (minuteDataCache.ContainsKey(stockCode))
            {
                // 返回副本，确保数据一致性（按时间排序，已去重）
                return new List<MinuteData>(minuteDataCache[stockCode]);
            }
            return new List<MinuteData>();
        }

        /// <summary>
        /// 获取股票的最新分钟数据（最后一条）
        /// </summary>
        public MinuteData GetLatestMinuteData(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return null;

            stockCode = DataConverter.NormalizeStockCode(stockCode);

            if (minuteDataCache.ContainsKey(stockCode) && minuteDataCache[stockCode].Count > 0)
            {
                // 数据已按时间排序，返回最后一条（最新的）
                return minuteDataCache[stockCode][minuteDataCache[stockCode].Count - 1];
            }
            return null;
        }

        /// <summary>
        /// 获取股票的5分钟数据（返回最新的、去重后的、按时间排序的数据）
        /// </summary>
        public List<Minute5Data> GetMinute5Data(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return new List<Minute5Data>();

            // 标准化股票代码
            stockCode = DataConverter.NormalizeStockCode(stockCode);

            if (minute5DataCache.ContainsKey(stockCode))
            {
                // 返回副本，确保数据一致性（按时间排序，已去重）
                return new List<Minute5Data>(minute5DataCache[stockCode]);
            }
            return new List<Minute5Data>();
        }

        /// <summary>
        /// 获取股票的最新5分钟数据（最后一条）
        /// </summary>
        public Minute5Data GetLatestMinute5Data(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return null;

            stockCode = DataConverter.NormalizeStockCode(stockCode);

            if (minute5DataCache.ContainsKey(stockCode) && minute5DataCache[stockCode].Count > 0)
            {
                // 数据已按时间排序，返回最后一条（最新的）
                return minute5DataCache[stockCode][minute5DataCache[stockCode].Count - 1];
            }
            return null;
        }

        /// <summary>
        /// 清除指定股票的数据
        /// </summary>
        public void ClearStockData(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return;

            stockCode = DataConverter.NormalizeStockCode(stockCode);
            
            minuteDataCache.Remove(stockCode);
            minute5DataCache.Remove(stockCode);
            minuteDataIndex.Remove(stockCode);
            minute5DataIndex.Remove(stockCode);
        }

        /// <summary>
        /// 清除所有数据
        /// </summary>
        public void ClearAllData()
        {
            minuteDataCache.Clear();
            minute5DataCache.Clear();
            minuteDataIndex.Clear();
            minute5DataIndex.Clear();
        }
        
        /// <summary>
        /// 清理资源（程序退出时调用，防止内存泄漏）
        /// </summary>
        public void Cleanup()
        {
            ClearAllData();
        }
    }
}

