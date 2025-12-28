using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StockDataMQClient
{
    /// <summary>
    /// 数据接收器 - 负责从DLL接收原始数据
    /// </summary>
    public class DataReceiver
    {
        /// <summary>
        /// 实时行情数据接收事件（单条）- 保留用于UI更新
        /// </summary>
        public event EventHandler<StockDataEventArgs> StockDataReceived;

        /// <summary>
        /// 实时行情数据批量接收事件 - 用于MQ批量发送（性能优化）
        /// </summary>
        public event EventHandler<StockDataBatchEventArgs> StockDataBatchReceived;

        /// <summary>
        /// 码表数据接收事件
        /// </summary>
        public event EventHandler<MarketTableEventArgs> MarketTableReceived;

        /// <summary>
        /// 分钟数据接收事件
        /// </summary>
        public event EventHandler<MinuteDataEventArgs> MinuteDataReceived;

        /// <summary>
        /// 5分钟数据接收事件
        /// </summary>
        public event EventHandler<Minute5DataEventArgs> Minute5DataReceived;

        /// <summary>
        /// 数据接收统计
        /// </summary>
        public class DataStatistics
        {
            private int _totalPacketsReceived;
            public int TotalPacketsReceived
            {
                get { return _totalPacketsReceived; }
                set { _totalPacketsReceived = value; }
            }
            private int _totalStocksProcessed;
            public int TotalStocksProcessed
            {
                get { return _totalStocksProcessed; }
                set { _totalStocksProcessed = value; }
            }
            private DateTime _lastUpdateTime;
            public DateTime LastUpdateTime
            {
                get { return _lastUpdateTime; }
                set { _lastUpdateTime = value; }
            }
            private int _errorCount;
            public int ErrorCount
            {
                get { return _errorCount; }
                set { _errorCount = value; }
            }
            private int _marketTableCount;
            public int MarketTableCount
            {
                get { return _marketTableCount; }
                set { _marketTableCount = value; }
            }
            private int _totalMarketTableStocks;
            public int TotalMarketTableStocks
            {
                get { return _totalMarketTableStocks; }
                set { _totalMarketTableStocks = value; }
            }
        }

        private DataStatistics statistics = new DataStatistics();

        public DataStatistics Statistics
        {
            get { return statistics; }
        }

        // 上次数据接收时间（用于检测是否停止接收）
        private DateTime lastDataReceiveTime = DateTime.MinValue;

        /// <summary>
        /// 处理实时行情数据（优化性能版本）
        /// </summary>
        public void ProcessRealTimeData(IntPtr lParam)
        {
            try
            {
                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.RCV_DATA));

                if (pHeader.m_pData == IntPtr.Zero)
                {
                    Logger.Instance.Error(string.Format("ProcessRealTimeData: m_pData指针为空, lParam={0}, m_nPacketNum={1}", lParam.ToInt64(), pHeader.m_nPacketNum));
                    return;
                }

                bool isDetailedLog = statistics.TotalPacketsReceived < 10;

                if (isDetailedLog)
                {
                    Logger.Instance.Info(string.Format("ProcessRealTimeData: lParam={0}, m_pData={1}, m_nPacketNum={2}", lParam.ToInt64(), pHeader.m_pData.ToInt64(), pHeader.m_nPacketNum));

                    if (pHeader.m_nPacketNum > 0 && pHeader.m_pData != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr firstRecordPtr = new IntPtr(pHeader.m_pData.ToInt64());
                            RCV_REPORT_STRUCTExV3 firstRecord = (RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                firstRecordPtr, typeof(RCV_REPORT_STRUCTExV3));
                            string testCode = new string(firstRecord.m_szLabel).Trim('\0');
                            float testPrice = firstRecord.m_fNewPrice;
                            Logger.Instance.Info(string.Format("第一条数据验证: 代码={0}, 价格={1}", testCode, testPrice));
                        }
                        catch (Exception verifyEx)
                        {
                            Logger.Instance.Warning(string.Format("验证第一条数据失败: {0}", verifyEx.Message));
                        }
                    }
                }

                int previousCount = statistics.TotalPacketsReceived;
                statistics.TotalPacketsReceived += pHeader.m_nPacketNum;
                statistics.LastUpdateTime = DateTime.Now;
                lastDataReceiveTime = DateTime.Now;

                if (statistics.TotalPacketsReceived % 1000 == 0)
                {
                    Logger.Instance.Debug(string.Format("收到数据包: 包含 {0} 条记录, 累计 {1} 条", pHeader.m_nPacketNum, statistics.TotalPacketsReceived));
                }

                // 批量收集数据
                List<StockData> batchData = new List<StockData>(pHeader.m_nPacketNum);

                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        IntPtr recordPtr = new IntPtr(pHeader.m_pData.ToInt64() + 158 * i);

                        StockDataMQClient.RCV_REPORT_STRUCTExV3 report =
                            (StockDataMQClient.RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                recordPtr,
                                typeof(StockDataMQClient.RCV_REPORT_STRUCTExV3));

                        StockData stockData = DataConverter.ConvertFromReport(report);

                        if (stockData != null)
                        {
                            statistics.TotalStocksProcessed++;
                            batchData.Add(stockData);
                        }
                        else if (i < 3)
                        {
                            Logger.Instance.Warning(string.Format("股票数据转换失败: 索引={0}", i));
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        if (i < 3)
                        {
                            Logger.Instance.Error(string.Format("数据包解析失败 [{0}]: {1}", i, ex.Message));
                        }
                    }
                }

                // 触发事件
                if (batchData.Count > 0)
                {
                    if (isDetailedLog)
                    {
                        Logger.Instance.Info(string.Format("ProcessRealTimeData: 处理了 {0}/{1} 条实时数据", batchData.Count, pHeader.m_nPacketNum));
                        if (batchData.Count > 0)
                        {
                            var firstStock = batchData[0];
                            Logger.Instance.Info(string.Format("示例数据: 股票={0}, 名称={1}, 价格={2}", firstStock.Code, firstStock.Name, firstStock.NewPrice));
                        }
                    }

                    // 优先触发批量事件（用于MQ发送，高效）
                    if (StockDataBatchReceived != null)
                    {
                        StockDataBatchReceived(this, new StockDataBatchEventArgs(batchData));
                    }

                    // 然后触发单条事件（用于UI更新，保持兼容性）
                    if (StockDataReceived != null)
                    {
                        foreach (var stockData in batchData)
                        {
                            StockDataReceived(this, new StockDataEventArgs(stockData));
                        }
                    }
                }
                else if (statistics.TotalPacketsReceived <= 10)
                {
                    Logger.Instance.Warning(string.Format("ProcessRealTimeData: 数据包包含 {0} 条记录，但转换后数据为空", pHeader.m_nPacketNum));
                }

                // 记录数据接收进度
                int currentCount = statistics.TotalPacketsReceived;
                int previousHundred = previousCount / 100;
                int currentHundred = currentCount / 100;

                if (currentHundred > previousHundred)
                {
                    Logger.Instance.Info(string.Format("已接收 {0} 条数据包，已处理 {1} 只股票", currentCount, statistics.TotalStocksProcessed));
                }

                int previousThousand = previousCount / 1000;
                int currentThousand = currentCount / 1000;

                if (currentThousand > previousThousand)
                {
                    Logger.Instance.Success(string.Format("数据接收进度: {0} 条数据包, {1} 只股票", currentCount, statistics.TotalStocksProcessed));
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("实时数据处理失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }

        /// <summary>
        /// 处理分钟数据（分时线数据）
        /// </summary>
        public void ProcessMinuteData(IntPtr lParam)
        {
            try
            {
                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.RCV_DATA));

                if (pHeader.m_wDataType != StockDrv.FILE_MINUTE_EX)
                    return;

                Logger.Instance.Info(string.Format("收到分钟数据包, 记录数: {0}", pHeader.m_nPacketNum));

                int minuteStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_MINUTE_STRUCTEx));
                int headStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_EKE_HEADEx));

                Dictionary<string, List<MinuteData>> stockMinuteDataDict = new Dictionary<string, List<MinuteData>>();
                string currentStockCode = "";
                int currentOffset = 0;

                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        int timeValue = Marshal.ReadInt32(new IntPtr((int)pHeader.m_pData + currentOffset));

                        if ((uint)timeValue == StockDrv.EKE_HEAD_TAG)
                        {
                            if (!string.IsNullOrEmpty(currentStockCode) &&
                                stockMinuteDataDict.ContainsKey(currentStockCode) &&
                                stockMinuteDataDict[currentStockCode].Count > 0)
                            {
                                Logger.Instance.Success(string.Format("分钟数据处理完成: {0}, 共 {1} 条数据", currentStockCode, stockMinuteDataDict[currentStockCode].Count));
                                if (MinuteDataReceived != null)
                                {
                                    MinuteDataReceived(this, new MinuteDataEventArgs(currentStockCode, stockMinuteDataDict[currentStockCode]));
                                }
                            }

                            StockDataMQClient.RCV_EKE_HEADEx head = (StockDataMQClient.RCV_EKE_HEADEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + currentOffset),
                                typeof(StockDataMQClient.RCV_EKE_HEADEx));
                            currentStockCode = new string(head.m_szLabel).Trim('\0');
                            currentStockCode = DataConverter.NormalizeStockCode(currentStockCode);

                            if (!stockMinuteDataDict.ContainsKey(currentStockCode))
                            {
                                stockMinuteDataDict[currentStockCode] = new List<MinuteData>();
                            }

                            currentOffset += headStructSize;
                        }
                        else
                        {
                            StockDataMQClient.RCV_MINUTE_STRUCTEx minute = (StockDataMQClient.RCV_MINUTE_STRUCTEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + currentOffset),
                                typeof(StockDataMQClient.RCV_MINUTE_STRUCTEx));

                            if (!string.IsNullOrEmpty(currentStockCode))
                            {
                                DateTime tradeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                    .AddSeconds(minute.m_time)
                                    .ToLocalTime();

                                MinuteData data = new MinuteData(
                                    currentStockCode,
                                    tradeTime,
                                    minute.m_fPrice,
                                    minute.m_fVolume,
                                    minute.m_fAmount);

                                if (!stockMinuteDataDict.ContainsKey(currentStockCode))
                                {
                                    stockMinuteDataDict[currentStockCode] = new List<MinuteData>();
                                }
                                stockMinuteDataDict[currentStockCode].Add(data);
                            }

                            currentOffset += minuteStructSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        Logger.Instance.Error(string.Format("分钟数据解析失败 [{0}]: {1}", i, ex.Message));
                        currentOffset += minuteStructSize;
                    }
                }

                if (!string.IsNullOrEmpty(currentStockCode) &&
                    stockMinuteDataDict.ContainsKey(currentStockCode) &&
                    stockMinuteDataDict[currentStockCode].Count > 0)
                {
                    Logger.Instance.Success(string.Format("分钟数据处理完成: {0}, 共 {1} 条数据", currentStockCode, stockMinuteDataDict[currentStockCode].Count));
                    if (MinuteDataReceived != null)
                    {
                        MinuteDataReceived(this, new MinuteDataEventArgs(currentStockCode, stockMinuteDataDict[currentStockCode]));
                    }
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("分钟数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 处理5分钟K线数据
        /// </summary>
        public void Process5MinuteData(IntPtr lParam)
        {
            try
            {
                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.RCV_DATA));

                if (pHeader.m_wDataType != StockDrv.FILE_5MINUTE_EX)
                    return;

                List<Minute5Data> minute5DataList = new List<Minute5Data>();
                string currentStockCode = "";

                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        int timeValue = Marshal.ReadInt32(new IntPtr((int)pHeader.m_pData + 32 * i));

                        if ((uint)timeValue == StockDrv.EKE_HEAD_TAG)
                        {
                            StockDataMQClient.RCV_EKE_HEADEx head = (StockDataMQClient.RCV_EKE_HEADEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + 32 * i),
                                typeof(StockDataMQClient.RCV_EKE_HEADEx));
                            currentStockCode = new string(head.m_szLabel).Trim('\0');
                        }
                        else
                        {
                            StockDataMQClient.RCV_HISMINUTE_STRUCTEx minute5 = (StockDataMQClient.RCV_HISMINUTE_STRUCTEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + 32 * i),
                                typeof(StockDataMQClient.RCV_HISMINUTE_STRUCTEx));

                            if (!string.IsNullOrEmpty(currentStockCode))
                            {
                                DateTime tradeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                    .AddSeconds(minute5.m_time)
                                    .ToLocalTime();

                                Minute5Data data = new Minute5Data
                                {
                                    StockCode = currentStockCode,
                                    TradeTime = tradeTime,
                                    Open = minute5.m_fOpen,
                                    High = minute5.m_fHigh,
                                    Low = minute5.m_fLow,
                                    Close = minute5.m_fClose,
                                    Volume = minute5.m_fVolume,
                                    Amount = minute5.m_fAmount,
                                    ActiveBuyVol = minute5.m_fActiveBuyVol
                                };

                                minute5DataList.Add(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        System.Diagnostics.Debug.WriteLine(string.Format("5分钟数据解析失败 [{0}]: {1}", i, ex.Message));
                    }
                }

                if (minute5DataList.Count > 0 && !string.IsNullOrEmpty(currentStockCode))
                {
                    if (Minute5DataReceived != null)
                    {
                        Minute5DataReceived(this, new Minute5DataEventArgs(currentStockCode, minute5DataList));
                    }
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                System.Diagnostics.Debug.WriteLine(string.Format("5分钟数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 处理码表数据
        /// </summary>
        public void ProcessMarketTableData(IntPtr lParam)
        {
            try
            {
                StockDataMQClient.HLMarketType mHeader = (StockDataMQClient.HLMarketType)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.HLMarketType));

                string marketName = new string(mHeader.m_Name).Trim('\0');
                int stockCount = mHeader.m_nCount;

                statistics.MarketTableCount++;
                statistics.TotalMarketTableStocks += stockCount;

                Logger.Instance.Info(string.Format("收到码表数据包 #{0}: 市场={1}, 本包股票数量={2}", statistics.MarketTableCount, marketName, stockCount));

                Dictionary<string, string> codeDict = new Dictionary<string, string>();

                for (int i = 0; i < mHeader.m_nCount; i++)
                {
                    try
                    {
                        StockDataMQClient.RCV_TABLE_STRUCT table = (StockDataMQClient.RCV_TABLE_STRUCT)Marshal.PtrToStructure(
                            new IntPtr((int)lParam + 54 + 44 * i),
                            typeof(StockDataMQClient.RCV_TABLE_STRUCT));

                        string stockCode = new string(table.m_szLabel).Trim('\0');
                        string stockName = new string(table.m_szName).Trim('\0');

                        if (!string.IsNullOrEmpty(stockCode) && !string.IsNullOrEmpty(stockName))
                        {
                            codeDict[stockCode] = stockName;
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        System.Diagnostics.Debug.WriteLine(string.Format("码表项解析失败 [{0}]: {1}", i, ex.Message));
                    }
                }

                if (MarketTableReceived != null && codeDict.Count > 0)
                {
                    MarketTableReceived(this, new MarketTableEventArgs(codeDict));
                    Logger.Instance.Success(string.Format("码表数据已处理: 市场={0}, 有效股票数={1}", marketName, codeDict.Count));
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("码表数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            statistics = new DataStatistics();
        }
    }

    /// <summary>
    /// 股票数据事件参数（单条）
    /// </summary>
    public class StockDataEventArgs : EventArgs
    {
        private StockData _stockData;
        public StockData StockData
        {
            get { return _stockData; }
            private set { _stockData = value; }
        }

        public StockDataEventArgs(StockData stockData)
        {
            StockData = stockData;
        }
    }

    /// <summary>
    /// 股票数据批量事件参数（用于MQ批量发送）
    /// </summary>
    public class StockDataBatchEventArgs : EventArgs
    {
        private List<StockData> _stockDataList;
        public List<StockData> StockDataList
        {
            get { return _stockDataList; }
            private set { _stockDataList = value; }
        }

        public StockDataBatchEventArgs(List<StockData> stockDataList)
        {
            StockDataList = stockDataList;
        }
    }

    /// <summary>
    /// 码表数据事件参数
    /// </summary>
    public class MarketTableEventArgs : EventArgs
    {
        private Dictionary<string, string> _codeDictionary;
        public Dictionary<string, string> CodeDictionary
        {
            get { return _codeDictionary; }
            private set { _codeDictionary = value; }
        }

        public MarketTableEventArgs(Dictionary<string, string> codeDict)
        {
            CodeDictionary = codeDict;
        }
    }

    /// <summary>
    /// 分钟数据事件参数
    /// </summary>
    public class MinuteDataEventArgs : EventArgs
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            private set { _stockCode = value; }
        }
        private List<MinuteData> _minuteDataList;
        public List<MinuteData> MinuteDataList
        {
            get { return _minuteDataList; }
            private set { _minuteDataList = value; }
        }

        public MinuteDataEventArgs(string stockCode, List<MinuteData> minuteDataList)
        {
            StockCode = stockCode;
            MinuteDataList = minuteDataList;
        }
    }

    /// <summary>
    /// 5分钟数据事件参数
    /// </summary>
    public class Minute5DataEventArgs : EventArgs
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            private set { _stockCode = value; }
        }
        private List<Minute5Data> _minute5DataList;
        public List<Minute5Data> Minute5DataList
        {
            get { return _minute5DataList; }
            private set { _minute5DataList = value; }
        }

        public Minute5DataEventArgs(string stockCode, List<Minute5Data> minute5DataList)
        {
            StockCode = stockCode;
            Minute5DataList = minute5DataList;
        }
    }
}
