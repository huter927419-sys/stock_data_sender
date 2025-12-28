using System;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// MQ统计数据类 - 线程安全，用于跟踪MQ数据发送统计
    /// </summary>
    public class MQStatistics
    {
        private readonly object lockObject = new object();
        
        // 日线数据统计
        private long dailyDataSentCount = 0;        // 已发送的日线数据记录数
        private long dailyDataSentBytes = 0;       // 已发送的日线数据字节数
        private DateTime lastDailyDataTime = DateTime.MinValue;  // 最后发送日线数据的时间
        
        // 实时数据统计
        private long realTimeDataSentCount = 0;    // 已发送的实时数据记录数
        private long realTimeDataSentBytes = 0;    // 已发送的实时数据字节数
        private DateTime lastRealTimeDataTime = DateTime.MinValue;  // 最后发送实时数据的时间
        
        // 除权数据统计
        private long exRightsDataSentCount = 0;    // 已发送的除权数据记录数
        private long exRightsDataSentBytes = 0;    // 已发送的除权数据字节数
        private DateTime lastExRightsDataTime = DateTime.MinValue;  // 最后发送除权数据的时间
        
        // 码表数据统计
        private long marketTableDataSentCount = 0;    // 已发送的码表数据记录数
        private long marketTableDataSentBytes = 0;    // 已发送的码表数据字节数
        private DateTime lastMarketTableDataTime = DateTime.MinValue;  // 最后发送码表数据的时间
        
        // 错误统计
        private long dailyDataErrorCount = 0;       // 日线数据发送错误次数
        private long realTimeDataErrorCount = 0;   // 实时数据发送错误次数
        private long exRightsDataErrorCount = 0;    // 除权数据发送错误次数
        private long marketTableDataErrorCount = 0;    // 码表数据发送错误次数
        
        /// <summary>
        /// 记录日线数据发送
        /// </summary>
        public void RecordDailyDataSent(int recordCount, int bytesSent)
        {
            lock (lockObject)
            {
                dailyDataSentCount += recordCount;
                dailyDataSentBytes += bytesSent;
                lastDailyDataTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// 记录实时数据发送
        /// </summary>
        public void RecordRealTimeDataSent(int recordCount, int bytesSent)
        {
            lock (lockObject)
            {
                realTimeDataSentCount += recordCount;
                realTimeDataSentBytes += bytesSent;
                lastRealTimeDataTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// 记录除权数据发送
        /// </summary>
        public void RecordExRightsDataSent(int recordCount, int bytesSent)
        {
            lock (lockObject)
            {
                exRightsDataSentCount += recordCount;
                exRightsDataSentBytes += bytesSent;
                lastExRightsDataTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// 记录码表数据发送
        /// </summary>
        public void RecordMarketTableDataSent(int recordCount, int bytesSent)
        {
            lock (lockObject)
            {
                marketTableDataSentCount += recordCount;
                marketTableDataSentBytes += bytesSent;
                lastMarketTableDataTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// 记录日线数据发送错误
        /// </summary>
        public void RecordDailyDataError()
        {
            lock (lockObject)
            {
                dailyDataErrorCount++;
            }
        }
        
        /// <summary>
        /// 记录实时数据发送错误
        /// </summary>
        public void RecordRealTimeDataError()
        {
            lock (lockObject)
            {
                realTimeDataErrorCount++;
            }
        }
        
        /// <summary>
        /// 记录除权数据发送错误
        /// </summary>
        public void RecordExRightsDataError()
        {
            lock (lockObject)
            {
                exRightsDataErrorCount++;
            }
        }
        
        /// <summary>
        /// 记录码表数据发送错误
        /// </summary>
        public void RecordMarketTableDataError()
        {
            lock (lockObject)
            {
                marketTableDataErrorCount++;
            }
        }
        
        /// <summary>
        /// 获取日线数据统计（线程安全）
        /// </summary>
        public void GetDailyDataStats(out long count, out long bytes, out DateTime lastTime, out long errorCount)
        {
            lock (lockObject)
            {
                count = dailyDataSentCount;
                bytes = dailyDataSentBytes;
                lastTime = lastDailyDataTime;
                errorCount = dailyDataErrorCount;
            }
        }
        
        /// <summary>
        /// 获取实时数据统计（线程安全）
        /// </summary>
        public void GetRealTimeDataStats(out long count, out long bytes, out DateTime lastTime, out long errorCount)
        {
            lock (lockObject)
            {
                count = realTimeDataSentCount;
                bytes = realTimeDataSentBytes;
                lastTime = lastRealTimeDataTime;
                errorCount = realTimeDataErrorCount;
            }
        }
        
        /// <summary>
        /// 获取除权数据统计（线程安全）
        /// </summary>
        public void GetExRightsDataStats(out long count, out long bytes, out DateTime lastTime, out long errorCount)
        {
            lock (lockObject)
            {
                count = exRightsDataSentCount;
                bytes = exRightsDataSentBytes;
                lastTime = lastExRightsDataTime;
                errorCount = exRightsDataErrorCount;
            }
        }
        
        /// <summary>
        /// 获取码表数据统计（线程安全）
        /// </summary>
        public void GetMarketTableDataStats(out long count, out long bytes, out DateTime lastTime, out long errorCount)
        {
            lock (lockObject)
            {
                count = marketTableDataSentCount;
                bytes = marketTableDataSentBytes;
                lastTime = lastMarketTableDataTime;
                errorCount = marketTableDataErrorCount;
            }
        }
        
        /// <summary>
        /// 重置所有统计
        /// </summary>
        public void Reset()
        {
            lock (lockObject)
            {
                dailyDataSentCount = 0;
                dailyDataSentBytes = 0;
                lastDailyDataTime = DateTime.MinValue;
                dailyDataErrorCount = 0;
                
                realTimeDataSentCount = 0;
                realTimeDataSentBytes = 0;
                lastRealTimeDataTime = DateTime.MinValue;
                realTimeDataErrorCount = 0;
                
                exRightsDataSentCount = 0;
                exRightsDataSentBytes = 0;
                lastExRightsDataTime = DateTime.MinValue;
                exRightsDataErrorCount = 0;
                
                marketTableDataSentCount = 0;
                marketTableDataSentBytes = 0;
                lastMarketTableDataTime = DateTime.MinValue;
                marketTableDataErrorCount = 0;
            }
        }
        
        /// <summary>
        /// 获取统计信息字符串（用于显示）
        /// </summary>
        public string GetStatisticsString()
        {
            long dailyCount, dailyBytes, dailyErrors;
            DateTime dailyLastTime;
            GetDailyDataStats(out dailyCount, out dailyBytes, out dailyLastTime, out dailyErrors);
            
            long realtimeCount, realtimeBytes, realtimeErrors;
            DateTime realtimeLastTime;
            GetRealTimeDataStats(out realtimeCount, out realtimeBytes, out realtimeLastTime, out realtimeErrors);
            
            long exRightsCount, exRightsBytes, exRightsErrors;
            DateTime exRightsLastTime;
            GetExRightsDataStats(out exRightsCount, out exRightsBytes, out exRightsLastTime, out exRightsErrors);
            
            long marketTableCount, marketTableBytes, marketTableErrors;
            DateTime marketTableLastTime;
            GetMarketTableDataStats(out marketTableCount, out marketTableBytes, out marketTableLastTime, out marketTableErrors);
            
            string dailyStatus = dailyLastTime != DateTime.MinValue 
                ? string.Format("日线: {0}条 ({1})", dailyCount, FormatBytes(dailyBytes))
                : "日线: 0条";
            if (dailyErrors > 0) dailyStatus += string.Format(" [错误:{0}]", dailyErrors);
            
            string realtimeStatus = realtimeLastTime != DateTime.MinValue
                ? string.Format("实时: {0}条 ({1})", realtimeCount, FormatBytes(realtimeBytes))
                : "实时: 0条";
            if (realtimeErrors > 0) realtimeStatus += string.Format(" [错误:{0}]", realtimeErrors);
            
            string exRightsStatus = exRightsLastTime != DateTime.MinValue
                ? string.Format("除权: {0}条 ({1})", exRightsCount, FormatBytes(exRightsBytes))
                : "除权: 0条";
            if (exRightsErrors > 0) exRightsStatus += string.Format(" [错误:{0}]", exRightsErrors);
            
            string marketTableStatus = marketTableLastTime != DateTime.MinValue
                ? string.Format("码表: {0}条 ({1})", marketTableCount, FormatBytes(marketTableBytes))
                : "码表: 0条";
            if (marketTableErrors > 0) marketTableStatus += string.Format(" [错误:{0}]", marketTableErrors);
            
            return string.Format("MQ同步 | {0} | {1} | {2} | {3}", dailyStatus, realtimeStatus, exRightsStatus, marketTableStatus);
        }
        
        /// <summary>
        /// 格式化字节数
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return string.Format("{0}B", bytes);
            else if (bytes < 1024 * 1024)
                return string.Format("{0:F1}KB", bytes / 1024.0);
            else
                return string.Format("{0:F1}MB", bytes / (1024.0 * 1024.0));
        }
    }
}

