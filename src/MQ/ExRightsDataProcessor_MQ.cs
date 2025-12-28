using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StockDataMQClient
{
    /// <summary>
    /// 除权数据处理器 - MQ版本
    /// 负责解析龙卷风推送的除权数据并发送到消息队列
    /// 用于虚拟机（32位）环境
    /// </summary>
    public class ExRightsDataProcessorMQ
    {
        private readonly ExRightsDataMQSender mqSender;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ExRightsDataProcessorMQ(MQConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            
            mqSender = new ExRightsDataMQSender(config);
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
        /// 处理除权数据包
        /// </summary>
        /// <param name="lParam">Windows消息参数</param>
        public void ProcessExRightsData(IntPtr lParam)
        {
            try
            {
                // 1. 解析数据包头
                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                    lParam,
                    typeof(StockDataMQClient.RCV_DATA));

                if (pHeader.m_wDataType != StockDrv.FILE_POWER_EX)
                {
                    Logger.Instance.Warning(string.Format("数据类型不匹配，期望: {0}, 实际: {1}", 
                        StockDrv.FILE_POWER_EX, pHeader.m_wDataType));
                    return;
                }

                Logger.Instance.Info(string.Format("收到除权数据包，记录数: {0}", pHeader.m_nPacketNum));

                // 2. 解析数据
                List<ExRightsDataRecord> exRightsDataList = ParseExRightsData(pHeader);

                // 3. 发送到MQ
                if (exRightsDataList.Count > 0)
                {
                    if (mqSender.SendExRightsData(exRightsDataList))
                    {
                        Logger.Instance.Success(string.Format("成功发送 {0} 条除权数据到MQ", exRightsDataList.Count));
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("发送 {0} 条除权数据到MQ失败", exRightsDataList.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("处理除权数据失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }

        /// <summary>
        /// 解析除权数据
        /// </summary>
        private List<ExRightsDataRecord> ParseExRightsData(StockDataMQClient.RCV_DATA pHeader)
        {
            List<ExRightsDataRecord> exRightsDataList = new List<ExRightsDataRecord>();
            string currentStockCode = "";
            ushort currentMarketCode = 0;

            // 计算结构大小
            int powerStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_POWER_STRUCTEx));
            int headStructSize = Marshal.SizeOf(typeof(StockDataMQClient.RCV_EKE_HEADEx));

            // 遍历所有记录
            for (int i = 0; i < pHeader.m_nPacketNum; i++)
            {
                try
                {
                    // 计算当前记录的指针位置
                    IntPtr recordPtr = new IntPtr(pHeader.m_pData.ToInt64() + powerStructSize * i);

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
                        // 这是除权数据
                        if (string.IsNullOrEmpty(currentStockCode))
                        {
                            Logger.Instance.Warning(string.Format("第 {0} 条记录：未找到股票代码，跳过", i));
                            continue;
                        }

                        StockDataMQClient.RCV_POWER_STRUCTEx power = (StockDataMQClient.RCV_POWER_STRUCTEx)Marshal.PtrToStructure(
                            recordPtr,
                            typeof(StockDataMQClient.RCV_POWER_STRUCTEx));

                        // 转换为业务对象
                        ExRightsDataRecord record = ConvertToExRightsDataRecord(
                            currentStockCode, 
                            currentMarketCode, 
                            power);

                        exRightsDataList.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("解析第 {0} 条记录失败: {1}", i, ex.Message));
                }
            }

            return exRightsDataList;
        }

        /// <summary>
        /// 转换为除权数据记录
        /// </summary>
        private ExRightsDataRecord ConvertToExRightsDataRecord(
            string stockCode, 
            ushort marketCode, 
            StockDataMQClient.RCV_POWER_STRUCTEx power)
        {
            // UTC时间戳转换为本地时间
            DateTime exRightsDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(power.m_time)
                .ToLocalTime();

            return new ExRightsDataRecord
            {
                StockCode = stockCode,
                MarketCode = marketCode,
                ExRightsDate = exRightsDateTime.Date,
                ExRightsDateTime = exRightsDateTime,
                TimeStamp = power.m_time,
                GivePer10Shares = (decimal)power.m_fGive,      // 每10股送股数
                PeiPer10Shares = (decimal)power.m_fPei,        // 每10股配股数
                PeiPrice = (decimal)power.m_fPeiPrice,         // 配股价
                ProfitPerShare = (decimal)power.m_fProfit      // 每股红利
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
    /// 除权数据记录实体
    /// </summary>
    public class ExRightsDataRecord
    {
        public string StockCode { get; set; }          // 股票代码
        public ushort MarketCode { get; set; }          // 市场代码
        public DateTime ExRightsDate { get; set; }      // 除权日期
        public DateTime ExRightsDateTime { get; set; } // 除权日期时间
        public int TimeStamp { get; set; }             // UTC时间戳
        public decimal GivePer10Shares { get; set; }    // 每10股送股数
        public decimal PeiPer10Shares { get; set; }    // 每10股配股数
        public decimal PeiPrice { get; set; }          // 配股价（当配股数>0时有效）
        public decimal ProfitPerShare { get; set; }     // 每股红利
    }
}

