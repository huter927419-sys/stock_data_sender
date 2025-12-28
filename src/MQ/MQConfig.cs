using System;
using System.Configuration;

namespace StockDataMQClient
{
    /// <summary>
    /// MQ配置类 - 用于读取消息队列连接配置
    /// </summary>
    public class MQConfig
    {
        /// <summary>
        /// MQ服务器地址（宿主机IP）
        /// </summary>
        public string Host { get; set; }
        
        /// <summary>
        /// MQ服务器端口
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// 是否启用MQ发送
        /// </summary>
        public bool Enabled { get; set; }
        
        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        public int ConnectTimeout { get; set; }
        
        /// <summary>
        /// 发送超时时间（毫秒）
        /// </summary>
        public int SendTimeout { get; set; }
        
        /// <summary>
        /// 队列名称（日线数据）
        /// </summary>
        public string QueueName { get; set; }
        
        /// <summary>
        /// 实时数据队列名称
        /// </summary>
        public string RealtimeQueueName { get; set; }
        
        /// <summary>
        /// 除权数据队列名称
        /// </summary>
        public string ExRightsQueueName { get; set; }
        
        /// <summary>
        /// 码表数据队列名称
        /// </summary>
        public string MarketTableQueueName { get; set; }
        
        /// <summary>
        /// 从配置文件读取MQ配置
        /// </summary>
        public static MQConfig LoadFromConfig()
        {
            MQConfig config = new MQConfig();
            
            try
            {
                // 从 App.config 的 appSettings 读取配置
                config.Host = GetConfigValue("MQHost", "localhost");
                config.Port = int.Parse(GetConfigValue("MQPort", "5678"));
                config.QueueName = GetConfigValue("MQQueueName", "daily_data_queue");
                config.RealtimeQueueName = GetConfigValue("MQRealtimeQueueName", "realtime_data_queue");
                config.ExRightsQueueName = GetConfigValue("MQExRightsQueueName", "ex_rights_data_queue");
                config.MarketTableQueueName = GetConfigValue("MQMarketTableQueueName", "market_table_queue");
                config.Enabled = bool.Parse(GetConfigValue("MQEnabled", "true"));
                config.ConnectTimeout = int.Parse(GetConfigValue("MQConnectTimeout", "5000"));
                config.SendTimeout = int.Parse(GetConfigValue("MQSendTimeout", "10000"));
                
                Logger.Instance.Info(string.Format("从配置文件读取MQ配置: Host={0}, Port={1}, QueueName={2}, RealtimeQueue={3}, ExRightsQueue={4}, MarketTableQueue={5}, Enabled={6}",
                    config.Host, config.Port, config.QueueName, config.RealtimeQueueName, config.ExRightsQueueName, config.MarketTableQueueName, config.Enabled));
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("读取MQ配置失败，使用默认值: {0}", ex.Message));
                // 使用默认值
                config.Host = "localhost";
                config.Port = 5678;
                config.QueueName = "daily_data_queue";
                config.RealtimeQueueName = "realtime_data_queue";
                config.ExRightsQueueName = "ex_rights_data_queue";
                config.MarketTableQueueName = "market_table_queue";
                config.Enabled = false;  // 默认不启用
                config.ConnectTimeout = 5000;
                config.SendTimeout = 10000;
            }
            
            return config;
        }
        
        /// <summary>
        /// 获取配置值（带默认值）
        /// </summary>
        private static string GetConfigValue(string key, string defaultValue)
        {
            try
            {
                string value = ConfigurationManager.AppSettings[key];
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return Enabled && 
                   !string.IsNullOrEmpty(Host) && 
                   Port > 0 && Port < 65536 &&
                   !string.IsNullOrEmpty(QueueName) &&
                   !string.IsNullOrEmpty(RealtimeQueueName) &&
                   !string.IsNullOrEmpty(ExRightsQueueName) &&
                   !string.IsNullOrEmpty(MarketTableQueueName);
        }
    }
}

