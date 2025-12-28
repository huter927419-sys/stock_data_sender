using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// 日线数据MQ发送器 - 将日线数据发送到消息队列（TCP Socket实现）
    /// 用于虚拟机（32位）环境，将数据发送到宿主机（64位）的MQ接收器
    /// </summary>
    public class DailyDataMQSender
    {
        private readonly MQConfig config;
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private readonly object lockObject = new object();
        private bool isConnected = false;
        private DateTime lastConnectAttempt = DateTime.MinValue;
        private const int RECONNECT_INTERVAL_SECONDS = 5; // 重连间隔（秒）
        
        // 统计回调（可选）
        private MQStatistics statistics;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DailyDataMQSender(MQConfig config)
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
                Logger.Instance.Warning(string.Format("MQ连接测试失败: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 确保连接已建立
        /// </summary>
        private bool EnsureConnected()
        {
            lock (lockObject)
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
                        networkStream = tcpClient.GetStream();
                        networkStream.WriteTimeout = config.SendTimeout;
                        networkStream.ReadTimeout = config.SendTimeout;
                        isConnected = true;
                        Logger.Instance.Success(string.Format("【MQ连接成功】已连接到 {0}:{1}", config.Host, config.Port));
                        return true;
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("【MQ连接超时】连接 {0}:{1} 超时（{2}ms）", 
                            config.Host, config.Port, config.ConnectTimeout));
                        CloseConnection();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning(string.Format("【MQ连接失败】连接 {0}:{1} 失败: {2}", 
                        config.Host, config.Port, ex.Message));
                    Logger.Instance.Warning(string.Format("请检查：1. 宿主机接收器是否已启动 2. 网络连接是否正常 3. 防火墙是否允许连接"));
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
                if (networkStream != null)
                {
                    networkStream.Close();
                    networkStream = null;
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
        /// 发送日线数据到MQ
        /// </summary>
        public bool SendDailyData(List<DailyDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return true;

            // 详细日志：记录发送尝试
            Logger.Instance.Info(string.Format("【MQ发送】尝试发送 {0} 条日线数据到 {1}:{2}/{3}", 
                records.Count, config.Host, config.Port, config.QueueName));

            if (!EnsureConnected())
            {
                Logger.Instance.Warning(string.Format("【MQ发送失败】MQ未连接，无法发送 {0} 条日线数据到 {1}:{2}", 
                    records.Count, config.Host, config.Port));
                return false;
            }

            Logger.Instance.Info(string.Format("【MQ发送】连接已建立，准备发送 {0} 条日线数据", records.Count));

            try
            {
                // 将数据序列化为JSON格式
                string jsonData = SerializeToJson(records);
                
                // 构建消息：消息长度(4字节) + 队列名称长度(4字节) + 队列名称 + JSON数据
                byte[] queueNameBytes = Encoding.UTF8.GetBytes(config.QueueName);
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
                lock (lockObject)
                {
                    if (networkStream != null && isConnected)
                    {
                        networkStream.Write(message, 0, message.Length);
                        networkStream.Flush();

                        // 等待ACK确认响应
                        byte[] ackBuffer = new byte[4];
                        int ackRead = 0;
                        try
                        {
                            ackRead = networkStream.Read(ackBuffer, 0, 4);
                        }
                        catch (Exception ackEx)
                        {
                            Logger.Instance.Warning(string.Format("等待ACK超时或失败: {0}", ackEx.Message));
                        }

                        bool ackReceived = ackRead == 4 && ackBuffer[0] == 'A' && ackBuffer[1] == 'C' && ackBuffer[2] == 'K';

                        // 更新统计（异步，不阻塞）
                        if (statistics != null)
                        {
                            statistics.RecordDailyDataSent(records.Count, message.Length);
                        }

                        // 详细日志：记录发送成功
                        if (ackReceived)
                        {
                            Logger.Instance.Success(string.Format("【MQ发送成功】已发送 {0} 条日线数据到 {1}:{2}/{3}，消息大小: {4} 字节，已收到ACK",
                                records.Count, config.Host, config.Port, config.QueueName, message.Length));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("【MQ发送】已发送 {0} 条日线数据，但未收到ACK确认", records.Count));
                        }
                        return true;
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("【MQ发送失败】连接已断开，无法发送 {0} 条日线数据", records.Count));
                        CloseConnection();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误统计
                if (statistics != null)
                {
                    statistics.RecordDailyDataError();
                }
                
                Logger.Instance.Error(string.Format("【MQ发送异常】发送 {0} 条日线数据到 {1}:{2} 失败: {3}", 
                    records.Count, config.Host, config.Port, ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// 将日线数据记录列表序列化为JSON
        /// </summary>
        private string SerializeToJson(List<DailyDataRecord> records)
        {
            // 简单的JSON序列化（不依赖外部库，兼容.NET Framework 3.5）
            StringBuilder json = new StringBuilder();
            json.Append("{\"records\":[");
            
            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) json.Append(",");
                
                DailyDataRecord record = records[i];
                json.Append("{");
                json.AppendFormat("\"stock_code\":\"{0}\",", EscapeJson(record.StockCode ?? ""));
                json.AppendFormat("\"market_code\":{0},", record.MarketCode);
                json.AppendFormat("\"trade_date\":\"{0:yyyy-MM-dd}\",", record.TradeDate);
                json.AppendFormat("\"trade_datetime\":\"{0:yyyy-MM-dd HH:mm:ss}\",", record.TradeDateTime);
                json.AppendFormat("\"time_stamp\":{0},", record.TimeStamp);
                json.AppendFormat("\"open_price\":{0},", record.OpenPrice);
                json.AppendFormat("\"high_price\":{0},", record.HighPrice);
                json.AppendFormat("\"low_price\":{0},", record.LowPrice);
                json.AppendFormat("\"close_price\":{0},", record.ClosePrice);
                json.AppendFormat("\"volume\":{0},", record.Volume);
                json.AppendFormat("\"amount\":{0},", record.Amount);
                
                if (record.AdvanceCount > 0)
                {
                    json.AppendFormat("\"advance_count\":{0},", record.AdvanceCount);
                }
                else
                {
                    json.Append("\"advance_count\":null,");
                }
                
                if (record.DeclineCount > 0)
                {
                    json.AppendFormat("\"decline_count\":{0}", record.DeclineCount);
                }
                else
                {
                    json.Append("\"decline_count\":null");
                }
                
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

