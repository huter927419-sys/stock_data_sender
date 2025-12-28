using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// 实时数据MQ发送器 - 将实时数据发送到消息队列（TCP Socket实现）
    /// 优化版本：使用异步发送队列，避免阻塞数据处理线程
    /// </summary>
    public class RealTimeDataMQSender : IDisposable
    {
        private readonly MQConfig config;
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private readonly object connectionLock = new object();
        private bool isConnected = false;
        private DateTime lastConnectAttempt = DateTime.MinValue;
        private const int RECONNECT_INTERVAL_SECONDS = 2; // 重连间隔（秒）- 缩短以提高可用性

        // 异步发送队列
        private readonly ConcurrentQueue<List<RealTimeDataRecord>> sendQueue = new ConcurrentQueue<List<RealTimeDataRecord>>();
        private readonly ManualResetEventSlim sendSignal = new ManualResetEventSlim(false);
        private Thread sendThread;
        private volatile bool shouldStop = false;
        private volatile bool isRunning = false;

        // 发送队列配置
        private const int MAX_SEND_QUEUE_SIZE = 100;  // 最大待发送批次数
        private const int SEND_TIMEOUT_MS = 500;      // 发送超时（毫秒）- 缩短以快速失败

        // 统计回调（可选）
        private MQStatistics statistics;

        // 发送统计
        private volatile int totalBatchesSent = 0;
        private volatile int totalRecordsSent = 0;
        private volatile int totalBatchesDropped = 0;
        private volatile int connectionFailCount = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RealTimeDataMQSender(MQConfig config)
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
        /// 启动异步发送线程
        /// </summary>
        public void Start()
        {
            if (isRunning)
                return;

            shouldStop = false;
            isRunning = true;

            sendThread = new Thread(SendLoop)
            {
                Name = "RealTimeMQSendThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal  // 高于普通优先级
            };
            sendThread.Start();

            Logger.Instance.Info("实时数据MQ异步发送线程已启动");
        }

        /// <summary>
        /// 停止异步发送线程
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            shouldStop = true;
            sendSignal.Set();

            if (sendThread != null && sendThread.IsAlive)
            {
                if (!sendThread.Join(2000))
                {
                    try { sendThread.Abort(); } catch { }
                }
            }

            isRunning = false;
            CloseConnection();

            Logger.Instance.Info(string.Format("实时数据MQ发送线程已停止, 统计: 已发送={0}批/{1}条, 丢弃={2}批",
                totalBatchesSent, totalRecordsSent, totalBatchesDropped));
        }

        /// <summary>
        /// 异步发送循环
        /// </summary>
        private void SendLoop()
        {
            Logger.Instance.Info("实时数据MQ发送循环开始");
            SpinWait spinner = new SpinWait();

            while (!shouldStop)
            {
                try
                {
                    List<RealTimeDataRecord> records;

                    // 批量取出队列中的数据，合并发送
                    List<RealTimeDataRecord> mergedRecords = new List<RealTimeDataRecord>();
                    int batchCount = 0;

                    while (sendQueue.TryDequeue(out records) && batchCount < 10)  // 最多合并10批
                    {
                        mergedRecords.AddRange(records);
                        batchCount++;
                    }

                    if (mergedRecords.Count > 0)
                    {
                        // 发送合并后的数据
                        bool success = SendRealTimeDataInternal(mergedRecords);

                        if (success)
                        {
                            Interlocked.Add(ref totalRecordsSent, mergedRecords.Count);
                            Interlocked.Add(ref totalBatchesSent, batchCount);
                        }

                        spinner.Reset();
                    }
                    else
                    {
                        // 队列为空，等待信号
                        spinner.SpinOnce();
                        if (spinner.NextSpinWillYield)
                        {
                            sendSignal.Wait(50);  // 最多等待50ms
                            sendSignal.Reset();
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("MQ发送循环异常: {0}", ex.Message));
                    Thread.Sleep(100);
                }
            }

            // 退出前尝试发送剩余数据
            List<RealTimeDataRecord> remaining;
            while (sendQueue.TryDequeue(out remaining))
            {
                try
                {
                    SendRealTimeDataInternal(remaining);
                }
                catch { }
            }

            Logger.Instance.Info("实时数据MQ发送循环结束");
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
                Logger.Instance.Warning(string.Format("实时数据MQ连接测试失败: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 确保连接已建立
        /// </summary>
        private bool EnsureConnected()
        {
            lock (connectionLock)
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
                    CloseConnectionInternal();

                    // 建立新连接
                    tcpClient = new TcpClient();
                    tcpClient.NoDelay = true;  // 禁用Nagle算法，立即发送
                    tcpClient.SendBufferSize = 64 * 1024;  // 64KB发送缓冲区
                    tcpClient.ReceiveBufferSize = 8 * 1024;  // 8KB接收缓冲区

                    // 启用TCP KeepAlive，防止连接被中断
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    // 设置KeepAlive参数：10秒后开始探测，每5秒探测一次
                    SetTcpKeepAlive(tcpClient.Client, true, 10000, 5000);

                    var result = tcpClient.BeginConnect(config.Host, config.Port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(config.ConnectTimeout));

                    if (success)
                    {
                        tcpClient.EndConnect(result);
                        networkStream = tcpClient.GetStream();
                        networkStream.WriteTimeout = SEND_TIMEOUT_MS;
                        networkStream.ReadTimeout = SEND_TIMEOUT_MS;
                        isConnected = true;
                        Logger.Instance.Success(string.Format("实时数据MQ连接成功: {0}:{1}", config.Host, config.Port));
                        return true;
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("实时数据MQ连接超时: {0}:{1}", config.Host, config.Port));
                        CloseConnectionInternal();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning(string.Format("实时数据MQ连接失败: {0}:{1}, 错误: {2}",
                        config.Host, config.Port, ex.Message));
                    CloseConnectionInternal();
                    return false;
                }
            }
        }

        /// <summary>
        /// 关闭连接（内部方法，需要在锁内调用）
        /// </summary>
        private void CloseConnectionInternal()
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
        /// 关闭连接（公开方法）
        /// </summary>
        private void CloseConnection()
        {
            lock (connectionLock)
            {
                CloseConnectionInternal();
            }
        }

        /// <summary>
        /// 异步发送实时数据到MQ（非阻塞，放入发送队列）
        /// </summary>
        public bool SendRealTimeData(List<RealTimeDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return true;

            // 检查队列大小，防止内存溢出
            if (sendQueue.Count >= MAX_SEND_QUEUE_SIZE)
            {
                // 队列已满，丢弃最旧的数据（保留最新的）
                List<RealTimeDataRecord> dropped;
                if (sendQueue.TryDequeue(out dropped))
                {
                    Interlocked.Increment(ref totalBatchesDropped);
                    if (totalBatchesDropped % 100 == 1)
                    {
                        Logger.Instance.Warning(string.Format("MQ发送队列已满，丢弃旧数据, 累计丢弃: {0} 批", totalBatchesDropped));
                    }
                }
            }

            // 复制数据到队列（避免外部修改）
            List<RealTimeDataRecord> copy = new List<RealTimeDataRecord>(records);
            sendQueue.Enqueue(copy);
            sendSignal.Set();

            return true;
        }

        /// <summary>
        /// 内部同步发送方法（在发送线程中调用）
        /// </summary>
        private bool SendRealTimeDataInternal(List<RealTimeDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return true;

            // 详细日志：记录发送尝试（每100条记录一次，避免刷屏）
            int totalRecords = records.Count;
            if (totalRecords % 100 == 0 || totalRecords == 1)
            {
                Logger.Instance.Info(string.Format("【MQ发送】尝试发送 {0} 条实时数据到 {1}:{2}/realtime_data_queue", 
                    totalRecords, config.Host, config.Port));
            }

            if (!EnsureConnected())
            {
                // 每100次失败只记录一次日志
                int failCount = Interlocked.Increment(ref connectionFailCount);
                if (failCount == 1 || failCount % 100 == 0)
                {
                    Logger.Instance.Warning(string.Format("【MQ发送失败】无法连接MQ服务器 {0}:{1}，无法发送 {2} 条实时数据 (失败次数: {3})", 
                        config.Host, config.Port, totalRecords, failCount));
                }
                return false;
            }

            try
            {
                // 将数据序列化为JSON格式
                string jsonData = SerializeToJson(records);

                // 构建消息
                string realtimeQueueName = config.RealtimeQueueName ?? "realtime_data_queue";
                byte[] queueNameBytes = Encoding.UTF8.GetBytes(realtimeQueueName);
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
                lock (connectionLock)
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

                        // 更新统计
                        if (statistics != null)
                        {
                            statistics.RecordRealTimeDataSent(records.Count, message.Length);
                        }

                        // 详细日志：记录发送成功（每100条记录一次，避免刷屏）
                        if (records.Count % 100 == 0 || records.Count == 1)
                        {
                            if (ackReceived)
                            {
                                Logger.Instance.Success(string.Format("【MQ发送成功】已发送 {0} 条实时数据到 {1}:{2}/realtime_data_queue，消息大小: {3} 字节，已收到ACK",
                                    records.Count, config.Host, config.Port, message.Length));
                            }
                            else
                            {
                                Logger.Instance.Warning(string.Format("【MQ发送】已发送 {0} 条实时数据，但未收到ACK确认", records.Count));
                            }
                        }

                        return true;
                    }
                    else
                    {
                        CloseConnectionInternal();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("【MQ发送异常】发送 {0} 条实时数据到 {1}:{2} 失败: {3}", 
                    records.Count, config.Host, config.Port, ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// 将实时数据记录列表序列化为JSON（优化版本）
        /// </summary>
        private string SerializeToJson(List<RealTimeDataRecord> records)
        {
            // 预估容量，减少StringBuilder扩容
            int estimatedSize = records.Count * 500;
            StringBuilder json = new StringBuilder(estimatedSize);
            json.Append("{\"records\":[");

            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) json.Append(",");

                RealTimeDataRecord record = records[i];
                json.Append("{");
                json.AppendFormat("\"stock_code\":\"{0}\",", EscapeJson(record.StockCode ?? ""));
                json.AppendFormat("\"stock_name\":\"{0}\",", EscapeJson(record.StockName ?? ""));
                json.AppendFormat("\"market_code\":{0},", record.MarketCode);
                json.AppendFormat("\"update_time\":\"{0:yyyy-MM-dd HH:mm:ss}\",", record.UpdateTime);
                json.AppendFormat("\"time_stamp\":{0},", record.TimeStamp);
                json.AppendFormat("\"last_close\":{0},", record.LastClose);
                json.AppendFormat("\"open\":{0},", record.Open);
                json.AppendFormat("\"high\":{0},", record.High);
                json.AppendFormat("\"low\":{0},", record.Low);
                json.AppendFormat("\"new_price\":{0},", record.NewPrice);
                json.AppendFormat("\"volume\":{0},", record.Volume);
                json.AppendFormat("\"amount\":{0},", record.Amount);

                // 买卖盘数据
                json.Append("\"buy_price\":[");
                for (int j = 0; j < 5; j++)
                {
                    if (j > 0) json.Append(",");
                    json.Append(record.BuyPrice[j]);
                }
                json.Append("],");

                json.Append("\"buy_volume\":[");
                for (int j = 0; j < 5; j++)
                {
                    if (j > 0) json.Append(",");
                    json.Append(record.BuyVolume[j]);
                }
                json.Append("],");

                json.Append("\"sell_price\":[");
                for (int j = 0; j < 5; j++)
                {
                    if (j > 0) json.Append(",");
                    json.Append(record.SellPrice[j]);
                }
                json.Append("],");

                json.Append("\"sell_volume\":[");
                for (int j = 0; j < 5; j++)
                {
                    if (j > 0) json.Append(",");
                    json.Append(record.SellVolume[j]);
                }
                json.Append("]");

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
        /// 获取发送统计信息
        /// </summary>
        public string GetStatistics()
        {
            return string.Format("MQ发送: 队列={0}, 已发送={1}批/{2}条, 丢弃={3}批",
                sendQueue.Count, totalBatchesSent, totalRecordsSent, totalBatchesDropped);
        }

        /// <summary>
        /// 设置TCP KeepAlive参数
        /// </summary>
        /// <param name="socket">Socket对象</param>
        /// <param name="on">是否启用</param>
        /// <param name="keepAliveTime">首次探测前的空闲时间（毫秒）</param>
        /// <param name="keepAliveInterval">探测间隔（毫秒）</param>
        private void SetTcpKeepAlive(Socket socket, bool on, uint keepAliveTime, uint keepAliveInterval)
        {
            try
            {
                // Windows平台使用IOControl设置KeepAlive参数
                // 结构: onoff(4字节) + keepalivetime(4字节) + keepaliveinterval(4字节)
                byte[] inValue = new byte[12];
                BitConverter.GetBytes((uint)(on ? 1 : 0)).CopyTo(inValue, 0);
                BitConverter.GetBytes(keepAliveTime).CopyTo(inValue, 4);
                BitConverter.GetBytes(keepAliveInterval).CopyTo(inValue, 8);

                // SIO_KEEPALIVE_VALS = 0x98000004
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
            Stop();

            if (sendSignal != null)
            {
                sendSignal.Dispose();
            }
        }
    }
}
