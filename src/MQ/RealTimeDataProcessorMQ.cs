using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StockDataMQClient
{
    /// <summary>
    /// 实时数据处理器 - MQ版本
    /// 负责解析龙卷风推送的实时数据并发送到消息队列
    /// 优化版本：支持批量处理，减少MQ发送次数
    /// </summary>
    public class RealTimeDataProcessorMQ : IDisposable
    {
        private readonly RealTimeDataMQSender mqSender;

        // 批量发送统计
        private volatile int totalBatchesSent = 0;
        private volatile int totalRecordsSent = 0;
        private DateTime lastLogTime = DateTime.MinValue;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RealTimeDataProcessorMQ(MQConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            mqSender = new RealTimeDataMQSender(config);
        }

        /// <summary>
        /// 启动MQ发送（必须调用）
        /// </summary>
        public void Start()
        {
            mqSender.Start();
        }

        /// <summary>
        /// 停止MQ发送
        /// </summary>
        public void Stop()
        {
            mqSender.Stop();
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
        /// 处理实时数据包（直接从IntPtr处理）
        /// </summary>
        /// <param name="lParam">Windows消息参数</param>
        public void ProcessRealTimeData(IntPtr lParam)
        {
            // 此方法保留但不再使用，实际通过ProcessRealTimeDataList处理
        }

        /// <summary>
        /// 处理实时数据列表（批量发送到MQ）
        /// </summary>
        public void ProcessRealTimeDataList(List<StockData> stockDataList)
        {
            try
            {
                if (stockDataList == null || stockDataList.Count == 0)
                    return;

                // 转换为RealTimeDataRecord列表
                List<RealTimeDataRecord> records = new List<RealTimeDataRecord>(stockDataList.Count);

                for (int i = 0; i < stockDataList.Count; i++)
                {
                    RealTimeDataRecord record = ConvertToRealTimeDataRecord(stockDataList[i]);
                    if (record != null)
                    {
                        records.Add(record);
                    }
                }

                // 批量发送到MQ（异步，非阻塞）
                if (records.Count > 0)
                {
                    mqSender.SendRealTimeData(records);

                    // 更新统计
                    totalBatchesSent++;
                    totalRecordsSent += records.Count;

                    // 每5秒输出一次日志
                    if ((DateTime.Now - lastLogTime).TotalSeconds >= 5)
                    {
                        lastLogTime = DateTime.Now;
                        Logger.Instance.Info(string.Format("实时数据MQ发送统计: 本次={0}条, 累计={1}批/{2}条, {3}",
                            records.Count, totalBatchesSent, totalRecordsSent, mqSender.GetStatistics()));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("处理实时数据列表失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 转换为实时数据记录
        /// </summary>
        private RealTimeDataRecord ConvertToRealTimeDataRecord(StockData stockData)
        {
            if (stockData == null)
                return null;

            try
            {
                RealTimeDataRecord record = new RealTimeDataRecord
                {
                    StockCode = stockData.Code ?? "",
                    StockName = stockData.Name ?? "",
                    MarketCode = stockData.MarketType,
                    UpdateTime = stockData.UpdateTime,
                    TimeStamp = stockData.TradeTime,
                    LastClose = (decimal)stockData.LastClose,
                    Open = (decimal)stockData.Open,
                    High = (decimal)stockData.High,
                    Low = (decimal)stockData.Low,
                    NewPrice = (decimal)stockData.NewPrice,
                    Volume = (decimal)stockData.Volume,
                    Amount = (decimal)stockData.Amount
                };

                // 提取买卖盘数据（5档）
                if (stockData.BuyPrice != null && stockData.BuyPrice.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        record.BuyPrice[i] = (decimal)stockData.BuyPrice[i];
                    }
                }

                if (stockData.BuyVolume != null && stockData.BuyVolume.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        record.BuyVolume[i] = (decimal)stockData.BuyVolume[i];
                    }
                }

                if (stockData.SellPrice != null && stockData.SellPrice.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        record.SellPrice[i] = (decimal)stockData.SellPrice[i];
                    }
                }

                if (stockData.SellVolume != null && stockData.SellVolume.Length >= 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        record.SellVolume[i] = (decimal)stockData.SellVolume[i];
                    }
                }

                return record;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("转换实时数据记录失败: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStatistics()
        {
            return string.Format("处理器: {0}批/{1}条, {2}", totalBatchesSent, totalRecordsSent, mqSender.GetStatistics());
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
