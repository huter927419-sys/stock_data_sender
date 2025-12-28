using System;
using System.Collections.Generic;

namespace StockDataMQClient
{
    /// <summary>
    /// 码表数据处理器 - MQ版本
    /// 负责处理码表数据并发送到消息队列
    /// 用于虚拟机（32位）环境
    /// </summary>
    public class MarketTableDataProcessorMQ
    {
        private readonly MarketTableDataMQSender mqSender;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MarketTableDataProcessorMQ(MQConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            
            mqSender = new MarketTableDataMQSender(config);
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
        /// 处理码表数据（从字典转换）
        /// </summary>
        /// <param name="codeDictionary">股票代码和名称的字典</param>
        public void ProcessMarketTableData(Dictionary<string, string> codeDictionary)
        {
            try
            {
                if (codeDictionary == null || codeDictionary.Count == 0)
                    return;

                // 转换为码表数据记录列表
                List<MarketTableDataRecord> records = new List<MarketTableDataRecord>();
                DateTime updateTime = DateTime.Now;
                
                foreach (var kvp in codeDictionary)
                {
                    string stockCode = kvp.Key;
                    string stockName = kvp.Value;
                    
                    if (string.IsNullOrEmpty(stockCode) || string.IsNullOrEmpty(stockName))
                        continue;
                    
                    // 标准化股票代码
                    string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                    
                    // 判断市场代码（0=深圳, 1=上海）
                    int marketCode = 0; // 默认深圳
                    if (normalizedCode.StartsWith("SH", StringComparison.OrdinalIgnoreCase))
                    {
                        marketCode = 1; // 上海
                    }
                    else if (normalizedCode.StartsWith("SZ", StringComparison.OrdinalIgnoreCase))
                    {
                        marketCode = 0; // 深圳
                    }
                    
                    MarketTableDataRecord record = new MarketTableDataRecord
                    {
                        StockCode = normalizedCode,
                        StockName = stockName,
                        MarketCode = marketCode,
                        UpdateTime = updateTime
                    };
                    
                    records.Add(record);
                }

                // 发送到MQ（批量发送，减少网络开销）
                if (records.Count > 0)
                {
                    if (mqSender.SendMarketTableData(records))
                    {
                        Logger.Instance.Success(string.Format("成功发送 {0} 条码表数据到MQ", records.Count));
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("发送 {0} 条码表数据到MQ失败", records.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("处理码表数据失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
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
}

