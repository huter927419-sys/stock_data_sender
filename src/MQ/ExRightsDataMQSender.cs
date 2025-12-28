using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace StockDataMQClient
{
    /// <summary>
    /// 除权数据MQ发送器
    /// 负责将除权数据序列化为JSON并通过TCP Socket发送到消息队列
    /// </summary>
    public class ExRightsDataMQSender : IDisposable
    {
        private readonly MQConfig config;
        private TcpClient tcpClient;
        private NetworkStream stream;
        private readonly object sendLock = new object();
        private bool isConnected = false;
        private DateTime lastConnectAttempt = DateTime.MinValue;
        private const int RECONNECT_INTERVAL_SECONDS = 5; // 重连间隔（秒）
        
        // 统计回调（可选）
        private MQStatistics statistics;

        public ExRightsDataMQSender(MQConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            
            this.config = config;
        }
        
        /// <summary>
        /// 设置统计对象（可选）
        /// </summary>
        public void SetStatistics(MQStatistics stats)
        {
            this.statistics = stats;
        }

        /// <summary>
        /// 测试MQ连接
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                using (var testClient = new TcpClient())
                {
                    var result = testClient.BeginConnect(config.Host, config.Port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(config.ConnectTimeout));
                    
                    if (success)
                    {
                        testClient.EndConnect(result);
                        testClient.Close();
                        return true;
                    }
                    else
                    {
                        testClient.Close();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("除权数据MQ连接测试失败: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 发送除权数据到MQ
        /// </summary>
        public bool SendExRightsData(List<ExRightsDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return true;

            // 详细日志：记录发送尝试
            Logger.Instance.Info(string.Format("【MQ发送】尝试发送 {0} 条除权数据到 {1}:{2}/ex_rights_data_queue", 
                records.Count, config.Host, config.Port));

            if (!EnsureConnection())
            {
                Logger.Instance.Warning(string.Format("【MQ发送失败】MQ未连接，无法发送 {0} 条除权数据到 {1}:{2}", 
                    records.Count, config.Host, config.Port));
                return false;
            }

            Logger.Instance.Info(string.Format("【MQ发送】连接已建立，准备发送 {0} 条除权数据", records.Count));

            try
            {
                // 将数据序列化为JSON格式
                string jsonData = SerializeToJson(records);
                
                // 构建消息：消息长度(4字节) + 队列名称长度(4字节) + 队列名称 + JSON数据
                // 除权数据使用独立的队列名称
                string exRightsQueueName = "ex_rights_data_queue";
                byte[] queueNameBytes = Encoding.UTF8.GetBytes(exRightsQueueName);
                byte[] dataBytes = Encoding.UTF8.GetBytes(jsonData);
                
                int messageLength = 4 + 4 + queueNameBytes.Length + dataBytes.Length;
                byte[] message = new byte[messageLength];
                
                int offset = 0;
                
                // 写入消息总长度
                byte[] lengthBytes = BitConverter.GetBytes(messageLength);
                Array.Copy(lengthBytes, 0, message, offset, 4);
                offset += 4;
                
                // 写入队列名称长度
                byte[] queueNameLengthBytes = BitConverter.GetBytes(queueNameBytes.Length);
                Array.Copy(queueNameLengthBytes, 0, message, offset, 4);
                offset += 4;
                
                // 写入队列名称
                Array.Copy(queueNameBytes, 0, message, offset, queueNameBytes.Length);
                offset += queueNameBytes.Length;
                
                // 写入JSON数据
                Array.Copy(dataBytes, 0, message, offset, dataBytes.Length);

                // 发送消息
                lock (sendLock)
                {
                    if (stream != null && stream.CanWrite)
                    {
                        stream.Write(message, 0, message.Length);
                        stream.Flush();

                        // 等待ACK确认响应
                        byte[] ackBuffer = new byte[4];
                        int ackRead = 0;
                        try
                        {
                            ackRead = stream.Read(ackBuffer, 0, 4);
                        }
                        catch (Exception ackEx)
                        {
                            Logger.Instance.Warning(string.Format("等待ACK超时或失败: {0}", ackEx.Message));
                        }

                        bool ackReceived = ackRead == 4 && ackBuffer[0] == 'A' && ackBuffer[1] == 'C' && ackBuffer[2] == 'K';

                        // 更新统计（异步，不阻塞）
                        if (statistics != null)
                        {
                            statistics.RecordExRightsDataSent(records.Count, message.Length);
                        }

                        if (ackReceived)
                        {
                            Logger.Instance.Success(string.Format("【MQ发送成功】已发送 {0} 条除权数据到 {1}:{2}/{3}，消息大小: {4} 字节，已收到ACK",
                                records.Count, config.Host, config.Port, exRightsQueueName, message.Length));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("【MQ发送】已发送 {0} 条除权数据，但未收到ACK确认", records.Count));
                        }
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                // 记录错误统计
                if (statistics != null)
                {
                    statistics.RecordExRightsDataError();
                }
                
                Logger.Instance.Error(string.Format("【MQ发送异常】发送 {0} 条除权数据到 {1}:{2} 失败: {3}", 
                    records.Count, config.Host, config.Port, ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// 确保MQ连接
        /// </summary>
        private bool EnsureConnection()
        {
            lock (sendLock)
            {
                if (isConnected && tcpClient != null && tcpClient.Connected)
                {
                    return true;
                }

                // 防止频繁重连
                if ((DateTime.Now - lastConnectAttempt).TotalSeconds < RECONNECT_INTERVAL_SECONDS)
                {
                    return false;
                }

                lastConnectAttempt = DateTime.Now;

                try
                {
                    // 关闭旧连接
                    CloseConnection();

                    // 建立新连接
                    tcpClient = new TcpClient();
                    tcpClient.NoDelay = true;  // 禁用Nagle算法，立即发送，减少延迟

                    // 启用TCP KeepAlive，防止连接被中断
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    SetTcpKeepAlive(tcpClient.Client, true, 10000, 5000);

                    var result = tcpClient.BeginConnect(config.Host, config.Port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(config.ConnectTimeout));

                    if (success)
                    {
                        tcpClient.EndConnect(result);
                        stream = tcpClient.GetStream();
                        stream.WriteTimeout = config.SendTimeout;
                        stream.ReadTimeout = config.SendTimeout;
                        isConnected = true;
                        Logger.Instance.Success(string.Format("除权数据MQ连接成功: {0}:{1}", config.Host, config.Port));
                        return true;
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("除权数据MQ连接超时: {0}:{1}", config.Host, config.Port));
                        CloseConnection();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning(string.Format("除权数据MQ连接失败: {0}:{1}, 错误: {2}", 
                        config.Host, config.Port, ex.Message));
                    CloseConnection();
                    return false;
                }
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        private void CloseConnection()
        {
            try
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }
            catch { }

            try
            {
                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient = null;
                }
            }
            catch { }

            isConnected = false;
        }

        /// <summary>
        /// 将除权数据记录列表序列化为JSON
        /// </summary>
        private string SerializeToJson(List<ExRightsDataRecord> records)
        {
            // 简单的JSON序列化（不依赖外部库，兼容.NET Framework 3.5）
            StringBuilder json = new StringBuilder();
            json.Append("{\"records\":[");
            
            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) json.Append(",");
                
                ExRightsDataRecord record = records[i];
                json.Append("{");
                json.AppendFormat("\"stock_code\":\"{0}\",", EscapeJson(record.StockCode ?? ""));
                json.AppendFormat("\"market_code\":{0},", record.MarketCode);
                json.AppendFormat("\"ex_rights_date\":\"{0:yyyy-MM-dd}\",", record.ExRightsDate);
                json.AppendFormat("\"ex_rights_datetime\":\"{0:yyyy-MM-dd HH:mm:ss}\",", record.ExRightsDateTime);
                json.AppendFormat("\"time_stamp\":{0},", record.TimeStamp);
                json.AppendFormat("\"give_per_10_shares\":{0},", record.GivePer10Shares);
                json.AppendFormat("\"pei_per_10_shares\":{0},", record.PeiPer10Shares);
                json.AppendFormat("\"pei_price\":{0},", record.PeiPrice);
                json.AppendFormat("\"profit_per_share\":{0}", record.ProfitPerShare);
                json.Append("}");
            }
            
            json.Append("]}");
            return json.ToString();
        }

        /// <summary>
        /// JSON字符串转义
        /// </summary>
        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            return value.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }

        /// <summary>
        /// 设置TCP KeepAlive参数
        /// </summary>
        private void SetTcpKeepAlive(Socket socket, bool on, uint keepAliveTime, uint keepAliveInterval)
        {
            try
            {
                byte[] inValue = new byte[12];
                BitConverter.GetBytes((uint)(on ? 1 : 0)).CopyTo(inValue, 0);
                BitConverter.GetBytes(keepAliveTime).CopyTo(inValue, 4);
                BitConverter.GetBytes(keepAliveInterval).CopyTo(inValue, 8);
                socket.IOControl(unchecked((int)0x98000004), inValue, null);
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("设置TCP KeepAlive失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            CloseConnection();
        }
    }
}

