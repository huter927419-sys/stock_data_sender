using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StockDataMQClient
{
    /// <summary>
    /// 日线数据处理器 - MQ版本
    /// 负责解析龙卷风推送的日线数据并发送到消息队列
    /// 用于虚拟机（32位）环境
    /// </summary>
    public class DailyDataProcessorMQ
    {
        private readonly DailyDataMQSender mqSender;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DailyDataProcessorMQ(MQConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            
            mqSender = new DailyDataMQSender(config);
        }

        /// <summary>
        /// 测试MQ连接
        /// </summary>
        public bool TestConnection()
        {
            return mqSender.TestConnection();
        }
        
        /// <summary>
        /// 设置统计对象（用于跟踪MQ发送统计）
        /// </summary>
        public void SetStatistics(MQStatistics statistics)
        {
            if (mqSender != null)
            {
                mqSender.SetStatistics(statistics);
            }
        }

        /// <summary>
        /// 处理日线数据包
        /// </summary>
        /// <param name="lParam">Windows消息参数</param>
        public void ProcessDailyData(IntPtr lParam)
        {
            try
            {
                Logger.Instance.Info(string.Format("ProcessDailyData: 开始处理日线数据, lParam={0}", lParam.ToInt64()));

                // 1. 解析数据包头
                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.RCV_DATA));

                Logger.Instance.Info(string.Format("ProcessDailyData: m_wDataType={0}, m_nPacketNum={1}, m_pData={2}",
                    pHeader.m_wDataType, pHeader.m_nPacketNum, pHeader.m_pData.ToInt64()));

                if (pHeader.m_wDataType != StockDrv.FILE_HISTORY_EX)
                {
                    Logger.Instance.Warning(string.Format("日线数据类型不匹配，期望: {0}(FILE_HISTORY_EX), 实际: {1}",
                        StockDrv.FILE_HISTORY_EX, pHeader.m_wDataType));
                    return;
                }

                Logger.Instance.Info(string.Format("收到日线数据包，记录数: {0}", pHeader.m_nPacketNum));

                // 2. 解析数据
                List<DailyDataRecord> dailyDataList = ParseDailyData(pHeader);

                // 3. 发送到MQ
                if (dailyDataList.Count > 0)
                {
                    if (mqSender.SendDailyData(dailyDataList))
                    {
                        Logger.Instance.Success(string.Format("成功发送 {0} 条日线数据到MQ", dailyDataList.Count));
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("发送 {0} 条日线数据到MQ失败", dailyDataList.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("处理日线数据失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }

        /// <summary>
        /// 解析日线数据（复用PostgreSQL版本的解析逻辑）
        /// </summary>
        private List<DailyDataRecord> ParseDailyData(StockDataMQClient.RCV_DATA pHeader)
        {
            List<DailyDataRecord> dailyDataList = new List<DailyDataRecord>();
            string currentStockCode = "";
            ushort currentMarketCode = 0;

            // 计算结构大小
            int historyStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_HISTORY_STRUCTEx));
            int headStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_EKE_HEADEx));

            // 遍历所有记录
            for (int i = 0; i < pHeader.m_nPacketNum; i++)
            {
                try
                {
                    // 计算当前记录的指针位置
                    IntPtr recordPtr = new IntPtr(pHeader.m_pData.ToInt64() + historyStructSize * i);

                    // 检查是否是数据头（通过读取第一个int值判断）
                    int timeValue = Marshal.ReadInt32(recordPtr);

                    if ((uint)timeValue == StockDrv.EKE_HEAD_TAG)
                    {
                        // 这是数据头，解析股票代码
                        StockDataMQClient.RCV_EKE_HEADEx head = (StockDataMQClient.RCV_EKE_HEADEx)Marshal.PtrToStructure(
                            recordPtr,
                            typeof(StockDataMQClient.RCV_EKE_HEADEx));

                        currentStockCode = new string(head.m_szLabel).Trim('\0');
                        currentMarketCode = head.m_wMarket;

                        Logger.Instance.Debug(string.Format("解析到股票代码: {0}, 市场代码: {1}", 
                            currentStockCode, currentMarketCode));
                    }
                    else
                    {
                        // 这是日线数据
                        if (string.IsNullOrEmpty(currentStockCode))
                        {
                            Logger.Instance.Warning(string.Format("第 {0} 条记录：未找到股票代码，跳过", i));
                            continue;
                        }

                        StockDataMQClient.RCV_HISTORY_STRUCTEx history = (StockDataMQClient.RCV_HISTORY_STRUCTEx)Marshal.PtrToStructure(
                            recordPtr,
                            typeof(StockDataMQClient.RCV_HISTORY_STRUCTEx));

                        // 转换为业务对象
                        DailyDataRecord record = ConvertToDailyDataRecord(
                            currentStockCode, 
                            currentMarketCode, 
                            history);

                        dailyDataList.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("解析第 {0} 条记录失败: {1}", i, ex.Message));
                }
            }

            return dailyDataList;
        }

        /// <summary>
        /// 转换为日线数据记录
        /// </summary>
        private DailyDataRecord ConvertToDailyDataRecord(
            string stockCode, 
            ushort marketCode, 
            StockDataMQClient.RCV_HISTORY_STRUCTEx history)
        {
            // UTC时间戳转换为本地时间
            DateTime tradeDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(history.m_time)
                .ToLocalTime();

            return new DailyDataRecord
            {
                StockCode = stockCode,
                MarketCode = marketCode,
                TradeDate = tradeDateTime.Date,
                TradeDateTime = tradeDateTime,
                TimeStamp = history.m_time,
                OpenPrice = (decimal)history.m_fOpen,
                HighPrice = (decimal)history.m_fHigh,
                LowPrice = (decimal)history.m_fLow,
                ClosePrice = (decimal)history.m_fClose,
                Volume = (decimal)history.m_fVolume,
                Amount = (decimal)history.m_fAmount,
                AdvanceCount = history.m_wAdvance,
                DeclineCount = history.m_wDecline
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (mqSender != null)
            {
                mqSender.Dispose();
            }
        }
    }

    /// <summary>
    /// 日线数据记录实体
    /// </summary>
    public class DailyDataRecord
    {
        public string StockCode { get; set; }
        public ushort MarketCode { get; set; }
        public DateTime TradeDate { get; set; }
        public DateTime TradeDateTime { get; set; }
        public int TimeStamp { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
        public ushort AdvanceCount { get; set; }
        public ushort DeclineCount { get; set; }
    }
}

