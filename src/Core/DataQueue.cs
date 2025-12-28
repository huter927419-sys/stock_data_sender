using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Runtime.InteropServices;

namespace StockDataMQClient
{

    /// <summary>
    /// 数据包类型枚举
    /// </summary>
    public enum DataPacketType
    {
        RealTimeData,      // 实时行情数据
        MinuteData,        // 分钟数据
        Minute5Data,       // 5分钟数据
        MarketTableData,   // 码表数据
        DailyData,         // 日线数据
        ExRightsData,      // 除权数据
        // F10功能已禁用
        // StockBaseData      // F10个股资料数据
    }

    /// <summary>
    /// 数据包结构（用于队列传递）
    /// </summary>
    public class DataPacket
    {
        private DataPacketType _type;
        public DataPacketType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private IntPtr _lParam;
        public IntPtr LParam
        {
            get { return _lParam; }
            set { _lParam = value; }
        }

        private int _wParam;
        public int WParam
        {
            get { return _wParam; }
            set { _wParam = value; }
        }

        private DateTime _receiveTime;
        public DateTime ReceiveTime
        {
            get { return _receiveTime; }
            set { _receiveTime = value; }
        }

        private byte[] _dataBuffer;
        public byte[] DataBuffer
        {
            get { return _dataBuffer; }
            set { _dataBuffer = value; }
        }

        private int _dataSize;
        public int DataSize
        {
            get { return _dataSize; }
            set { _dataSize = value; }
        }
        
        /// <summary>
        /// 获取数据包的优先级（用于优先级队列）
        /// 实时数据优先级最高，F10数据优先级最低
        /// </summary>
        public int Priority
        {
            get
            {
                switch (Type)
                {
                    case DataPacketType.RealTimeData:
                        return 0;  // 最高优先级
                    case DataPacketType.MarketTableData:
                        return 1;  // 次高优先级
                    case DataPacketType.MinuteData:
                    case DataPacketType.Minute5Data:
                        return 2;  // 中等优先级
                    case DataPacketType.DailyData:
                        return 2;  // 中等优先级（日线数据）
                    case DataPacketType.ExRightsData:
                        return 2;  // 中等优先级（除权数据）
                    // F10功能已禁用
                    // case DataPacketType.StockBaseData:
                    //     return 3;  // 最低优先级
                    default:
                        return 2;
                }
            }
        }

        public DataPacket(DataPacketType type, IntPtr lParam, int wParam)
        {
            Type = type;
            LParam = lParam;
            WParam = wParam;
            ReceiveTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 数据队列管理器 - 用于异步处理数据，避免阻塞回调函数
    /// 使用优先级队列确保实时数据优先处理
    /// </summary>
    public class DataQueue
    {
        // 线程安全的数据队列（使用 ConcurrentQueue - 无锁高性能队列）
        // 实时数据优先处理，避免被分钟数据阻塞
        private ConcurrentQueue<DataPacket> realTimeQueue = new ConcurrentQueue<DataPacket>();  // 实时数据队列（高优先级）
        private ConcurrentQueue<DataPacket> otherDataQueue = new ConcurrentQueue<DataPacket>();  // 其他数据队列（低优先级，包含分钟数据、码表数据）
        // F10功能已禁用
        // private ConcurrentQueue<DataPacket> f10DataQueue = new ConcurrentQueue<DataPacket>();    // F10数据队列（独立线程，最低优先级）

        // 用于记录平台信息的静态变量（只记录一次）
        // 这些变量在条件分支中使用，编译器可能无法正确识别，所以抑制警告
#pragma warning disable CS0414 // 字段已被赋值，但从未使用过它的值
        private static bool platformInfoLogged = false;
        private static int debugPacketCount = 0;
#pragma warning restore CS0414

        // 为了兼容，保留原队列用于统计
        private ConcurrentQueue<DataPacket> dataQueue = new ConcurrentQueue<DataPacket>();

        // 用于通知处理线程的信号量（使用 ManualResetEventSlim 替代 AutoResetEvent，性能更好）
        private ManualResetEventSlim realTimeDataAvailableEvent = new ManualResetEventSlim(false);  // 实时数据信号
        private ManualResetEventSlim otherDataAvailableEvent = new ManualResetEventSlim(false);     // 其他数据信号
        // F10功能已禁用
        // private AutoResetEvent f10DataAvailableEvent = new AutoResetEvent(false);        // F10数据信号
        
        // 处理线程控制
        private Thread realTimeProcessingThread;  // 实时数据处理线程（高优先级）
        private Thread otherDataProcessingThread; // 其他数据处理线程（普通优先级）
        // F10功能已禁用
        // private Thread f10DataProcessingThread;  // F10数据处理线程（独立线程，低优先级）
        private volatile bool isRunning = false;
        private volatile bool shouldStop = false;
        
        // 数据接收器引用（用于实际处理数据）
        private DataReceiver dataReceiver;
        
        // 日线数据处理器引用（可选，如果为null则不处理日线数据）
        private DailyDataProcessorMQ dailyDataProcessorMQ;  // 日线数据发送到MQ
        private RealTimeDataProcessorMQ realTimeDataProcessorMQ;  // 实时数据发送到MQ
        private ExRightsDataProcessorMQ exRightsDataProcessorMQ;  // 除权数据发送到MQ

        // 防止重复日志的标记
        private bool dailyDataWarningLogged = false;
        private bool exRightsWarningLogged = false;
        
        // 统计信息（使用 volatile 和 Interlocked 确保线程安全，避免锁竞争）
        private volatile int totalEnqueued = 0;
        private volatile int totalProcessed = 0;
        private volatile int maxQueueSize = 0;

        /// <summary>
        /// 队列大小警告阈值
        /// </summary>
        private const int QUEUE_WARNING_THRESHOLD = 1000;
        
        /// <summary>
        /// 队列最大大小（超过此值会丢弃数据）
        /// </summary>
        private const int MAX_QUEUE_SIZE = 5000;
        
        /// <summary>
        /// 内存压力监控：当队列积压过多时，强制清理旧数据
        /// </summary>
        private const int MEMORY_PRESSURE_THRESHOLD = 3000;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DataQueue(DataReceiver receiver)
        {
            dataReceiver = receiver;
            dailyDataProcessorMQ = null;  // 默认不处理日线数据，需要外部设置
            realTimeDataProcessorMQ = null;  // 默认不处理实时数据，需要外部设置
            exRightsDataProcessorMQ = null;  // 默认不处理除权数据，需要外部设置

            // 订阅批量事件，用于MQ高效发送（替代逐条事件）
            if (receiver != null)
            {
                receiver.StockDataBatchReceived += OnStockDataBatchReceived;
            }
        }

        /// <summary>
        /// 批量数据接收事件处理（用于MQ批量发送，高效）
        /// </summary>
        private void OnStockDataBatchReceived(object sender, StockDataBatchEventArgs e)
        {
            if (e.StockDataList == null || e.StockDataList.Count == 0)
            {
                Logger.Instance.Debug("OnStockDataBatchReceived: 数据为空，跳过");
                return;
            }

            if (realTimeDataProcessorMQ == null)
            {
                Logger.Instance.Debug(string.Format("OnStockDataBatchReceived: 收到{0}条数据，但realTimeDataProcessorMQ为null", e.StockDataList.Count));
                return;
            }

            // 直接批量发送到MQ（高效，一次发送整个批次）
            try
            {
                Logger.Instance.Debug(string.Format("OnStockDataBatchReceived: 准备发送{0}条实时数据到MQ", e.StockDataList.Count));
                realTimeDataProcessorMQ.ProcessRealTimeDataList(e.StockDataList);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("批量发送实时数据失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 设置日线数据MQ处理器（发送到MQ）
        /// </summary>
        public void SetDailyDataProcessorMQ(DailyDataProcessorMQ processor)
        {
            dailyDataProcessorMQ = processor;
        }
        
        /// <summary>
        /// 设置实时数据MQ处理器（发送到MQ）
        /// </summary>
        public void SetRealTimeDataProcessorMQ(RealTimeDataProcessorMQ processor)
        {
            realTimeDataProcessorMQ = processor;
        }
        
        /// <summary>
        /// 设置除权数据MQ处理器（发送到MQ）
        /// </summary>
        public void SetExRightsDataProcessorMQ(ExRightsDataProcessorMQ processor)
        {
            exRightsDataProcessorMQ = processor;
        }

        /// <summary>
        /// 启动数据处理线程（多线程优化版本）
        /// </summary>
        public void Start()
        {
            if (isRunning)
            {
                Logger.Instance.Warning("数据队列处理线程已在运行");
                return;
            }

            shouldStop = false;
            isRunning = true;
            
            // 创建并启动实时数据处理线程（高优先级）
            realTimeProcessingThread = new Thread(ProcessRealTimeDataLoop)
            {
                Name = "RealTimeDataProcessingThread",
                IsBackground = true,
                Priority = ThreadPriority.Highest  // 最高优先级，确保实时数据及时处理
            };
            realTimeProcessingThread.Start();
            
            // 创建并启动其他数据处理线程（普通优先级）
            otherDataProcessingThread = new Thread(ProcessOtherDataLoop)
            {
                Name = "OtherDataProcessingThread",
                IsBackground = true,
                Priority = ThreadPriority.Normal  // 普通优先级
            };
            otherDataProcessingThread.Start();

            // F10功能已禁用
            // // 创建并启动F10数据处理线程（独立线程，低优先级）
            // f10DataProcessingThread = new Thread(ProcessF10DataLoop)
            // {
            //     Name = "F10DataProcessingThread",
            //     IsBackground = true,
            //     Priority = ThreadPriority.Lowest  // 最低优先级，确保不影响实时数据和其他数据
            // };
            // f10DataProcessingThread.Start();

            Logger.Instance.Info("数据队列处理线程已启动（实时数据线程：Highest，其他数据线程：Normal）");
        }

        /// <summary>
        /// 停止数据处理线程
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            shouldStop = true;
            
            // 批量处理功能已移除，不再需要清理
            
            // 唤醒所有线程以便退出
            try
            {
                if (realTimeDataAvailableEvent != null) realTimeDataAvailableEvent.Set();
                if (otherDataAvailableEvent != null) otherDataAvailableEvent.Set();
            }
            catch { }
            // F10功能已禁用
            // f10DataAvailableEvent.Set();
            
            // 优化：减少等待时间，加快关闭速度
            int timeoutMs = 1000;  // 1秒超时
            
            // 等待实时数据处理线程结束
            if (realTimeProcessingThread != null && realTimeProcessingThread.IsAlive)
            {
                if (!realTimeProcessingThread.Join(timeoutMs))
                {
                    try { realTimeProcessingThread.Abort(); } catch { }
                }
            }
            
            // 等待其他数据处理线程结束
            if (otherDataProcessingThread != null && otherDataProcessingThread.IsAlive)
            {
                if (!otherDataProcessingThread.Join(timeoutMs))
                {
                    try { otherDataProcessingThread.Abort(); } catch { }
                }
            }
            
            // F10功能已禁用
            // // 等待F10数据处理线程结束
            // if (f10DataProcessingThread != null && f10DataProcessingThread.IsAlive)
            // {
            //     if (!f10DataProcessingThread.Join(timeoutMs))
            //     {
            //         try { f10DataProcessingThread.Abort(); } catch { }
            //     }
            // }
            
            isRunning = false;
            
            // 重要：清空队列并释放所有数据包的缓冲区（防止内存泄漏）
            ClearQueues();
            
            // 释放 ManualResetEventSlim 资源（防止内存泄漏）
            try
            {
                if (realTimeDataAvailableEvent != null)
                {
                    realTimeDataAvailableEvent.Dispose();
                    realTimeDataAvailableEvent = null;
                }
                if (otherDataAvailableEvent != null)
                {
                    otherDataAvailableEvent.Dispose();
                    otherDataAvailableEvent = null;
                }
            }
            catch { }
        }
        
        /// <summary>
        /// 清理旧数据包以释放内存（在内存压力时调用）
        /// </summary>
        private void ClearOldPackets(int count)
        {
            try
            {
                int clearedCount = 0;
                DataPacket packet;

                // 优先清理其他数据队列（实时数据更重要）
                while (clearedCount < count && otherDataQueue.TryDequeue(out packet))
                {
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                        clearedCount++;
                    }
                }

                // 如果还需要清理更多，清理实时数据队列（但保留最新的）
                while (clearedCount < count && realTimeQueue.Count > 100 && realTimeQueue.TryDequeue(out packet))
                {
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                        clearedCount++;
                    }
                }

                if (clearedCount > 0)
                {
                    Logger.Instance.Info(string.Format("内存压力处理: 已清理 {0} 个旧数据包", clearedCount));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("清理旧数据包时出错: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 清空所有队列并释放数据包资源（防止内存泄漏）
        /// </summary>
        private void ClearQueues()
        {
            try
            {
                int clearedCount = 0;
                DataPacket packet;

                // 清空实时数据队列
                while (realTimeQueue.TryDequeue(out packet))
                {
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                        clearedCount++;
                    }
                }

                // 清空其他数据队列
                while (otherDataQueue.TryDequeue(out packet))
                {
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                        clearedCount++;
                    }
                }

                // 清空兼容队列
                while (dataQueue.TryDequeue(out packet))
                {
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                    }
                }

                if (clearedCount > 0)
                {
                    Logger.Instance.Info(string.Format("已清空队列并释放 {0} 个数据包的缓冲区", clearedCount));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(string.Format("清空队列时出错: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 将数据包加入队列（在回调函数中快速调用，不进行耗时操作）
        /// </summary>
        public void Enqueue(DataPacketType type, IntPtr lParam, int wParam)
        {
            if (!isRunning)
            {
                Logger.Instance.Warning("数据队列未启动，无法接收数据");
                return;
            }

            // 根据数据类型选择队列
            ConcurrentQueue<DataPacket> targetQueue;
            if (type == DataPacketType.RealTimeData)
            {
                targetQueue = realTimeQueue;
            }
            // F10功能已禁用
            // else if (type == DataPacketType.StockBaseData)
            // {
            //     // F10数据使用独立队列
            //     targetQueue = f10DataQueue;
            // }
            else
            {
                targetQueue = otherDataQueue;
            }
            
            // 检查队列大小，防止内存溢出
            // F10功能已禁用，不再统计f10DataQueue
            int currentQueueSize = realTimeQueue.Count + otherDataQueue.Count; // + f10DataQueue.Count;
            if (currentQueueSize >= MAX_QUEUE_SIZE)
            {
                // 队列已满，丢弃数据（记录警告）
                Logger.Instance.Error(string.Format("数据队列已满（实时={0}, 其他={1}），丢弃数据包", realTimeQueue.Count, otherDataQueue.Count));
                return;
            }

            // 对于需要复制数据的情况，这里需要复制数据
            // 注意：IntPtr 指向的数据可能在处理前被覆盖，所以需要复制
            DataPacket packet = null;
            
            try
            {
                // 根据数据类型决定是否需要复制数据
                // 注意：IntPtr 指向的数据可能在处理前被覆盖，所以需要复制
                if (type == DataPacketType.RealTimeData)
                {
                    // 实时数据：采用与分钟数据相同的简单方法，直接复制整个数据块
                    // 避免使用 Marshal.StructureToPtr，这在32位系统上可能有问题
                    RCV_DATA header;
                    try
                    {
                        header = (RCV_DATA)Marshal.PtrToStructure(
                            lParam, typeof(RCV_DATA));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("读取RCV_DATA结构失败: {0}, lParam={1}", ex.Message, lParam.ToInt64()));
                        return;
                    }
                    
                    // 验证数据有效性
                    if (header.m_nPacketNum <= 0)
                    {
                        Logger.Instance.Warning(string.Format("实时数据包数量为0或负数: {0}，跳过此数据包", header.m_nPacketNum));
                        return;
                    }
                    if (header.m_nPacketNum > 100000)
                    {
                        Logger.Instance.Warning(string.Format("实时数据包数量过大: {0}，限制为10000", header.m_nPacketNum));
                        header.m_nPacketNum = 10000;
                    }
                    
                    // 计算需要复制的数据大小
                    int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                    
                    // 记录平台信息（仅第一次，用于调试）
                    if (!platformInfoLogged)
                    {
                        bool is64Bit = IntPtr.Size == 8;
                        Logger.Instance.Info(string.Format("平台信息: {0}, IntPtr大小={1}字节, RCV_DATA结构体大小={2}字节", is64Bit ? "64位" : "32位", IntPtr.Size, headerSize));
                        platformInfoLogged = true;
                    }
                    int recordSize = 158; // 实时数据：每条记录约158字节（RCV_REPORT_STRUCTExV3）
                    int dataSize = (int)header.m_nPacketNum * recordSize;
                    dataSize = Math.Min(dataSize, 10 * 1024 * 1024); // 最大10MB
                    
                    // 验证m_pData指针有效性
                    if (header.m_pData == IntPtr.Zero)
                    {
                        Logger.Instance.Warning("实时数据m_pData指针为空，跳过此数据包");
                        return;
                    }
                    
                    // 估算总大小：结构体 + 数据 + 一些padding
                    // 使用与分钟数据类似的简单方法：直接复制整个数据块
                    int estimatedTotalSize = headerSize + dataSize + 1024; // 添加1KB padding
                    estimatedTotalSize = Math.Min(estimatedTotalSize, 50 * 1024 * 1024); // 最大50MB
                    
                    // 记录内存分配信息（仅前几次，用于调试）
                    if (debugPacketCount < 3)
                    {
                        Logger.Instance.Debug(string.Format("实时数据内存分配: headerSize={0}, dataSize={1}, estimatedTotalSize={2}", headerSize, dataSize, estimatedTotalSize));
                        Interlocked.Increment(ref debugPacketCount);
                    }
                    
                    // 直接复制整个数据块到缓冲区
                    // 重要：将数据重新排列，使数据紧跟在结构体后面，便于后续处理
                    long originalDataOffset = header.m_pData.ToInt64() - lParam.ToInt64();
                    
                    // 计算总大小：结构体 + 数据
                    int totalSize = headerSize + dataSize;
                    totalSize = Math.Min(totalSize, estimatedTotalSize);
                    
                    byte[] buffer = new byte[totalSize];
                    
                    try
                    {
                        // 第一步：复制结构体部分到buffer[0:headerSize]
                        Marshal.Copy(lParam, buffer, 0, headerSize);
                        
                        // 第二步：复制数据部分到buffer[headerSize:headerSize+dataSize]
                        // 这样数据就紧跟在结构体后面，m_pData指针可以简单地指向headerSize位置
                        if (dataSize > 0 && header.m_pData != IntPtr.Zero)
                        {
                            int actualDataSize = Math.Min(dataSize, totalSize - headerSize);
                            if (actualDataSize > 0)
                            {
                                Marshal.Copy(header.m_pData, buffer, headerSize, actualDataSize);
                                
                                // 验证数据是否复制成功（仅第一次，减少性能开销）
                                if (debugPacketCount < 1)
                                {
                                    // 验证第一条数据记录
                                    GCHandle verifyHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                                    try
                                    {
                                        IntPtr bufferBasePtr = verifyHandle.AddrOfPinnedObject();
                                        IntPtr bufferDataPtr = new IntPtr(bufferBasePtr.ToInt64() + headerSize);
                                        RCV_REPORT_STRUCTExV3 testRecord = (RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                            bufferDataPtr, typeof(RCV_REPORT_STRUCTExV3));
                                        string testCode = new string(testRecord.m_szLabel).Trim('\0');
                                        float testPrice = testRecord.m_fNewPrice;
                                        Logger.Instance.Debug(string.Format("Enqueue数据验证: 代码={0}, 价格={1}, buffer数据位置={2}, 原始m_pData={3}", testCode, testPrice, headerSize, header.m_pData.ToInt64()));
                                    }
                                    catch (Exception verifyEx)
                                    {
                                        Logger.Instance.Warning(string.Format("Enqueue数据验证失败: {0}", verifyEx.Message));
                                    }
                                    finally
                                    {
                                        verifyHandle.Free();
                                    }
                                }
                            }
                            else
                            {
                                Logger.Instance.Warning(string.Format("数据大小计算错误: actualDataSize={0}, dataSize={1}, totalSize={2}, headerSize={3}", actualDataSize, dataSize, totalSize, headerSize));
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("无法复制数据: dataSize={0}, m_pData={1}", dataSize, header.m_pData.ToInt64()));
                        }
                        
                        // 第三步：修改buffer中的m_pData指针值，使其指向buffer内的数据位置（headerSize）
                        // 这样当buffer被复制到非托管内存时，m_pData指针就是正确的
                        if (buffer.Length >= headerSize + Marshal.SizeOf(typeof(IntPtr)))
                        {
                            IntPtr m_pDataOffsetInBuffer = new IntPtr(
                                Marshal.OffsetOf(typeof(RCV_DATA), "m_pData").ToInt64());
                            int offset = (int)m_pDataOffsetInBuffer.ToInt64();
                            if (offset >= 0 && offset + Marshal.SizeOf(typeof(IntPtr)) <= headerSize)
                            {
                                // 将m_pData指针写入buffer（指向buffer内的headerSize位置）
                                // 注意：这里写入的是相对偏移，后续在ProcessPacket中需要转换为绝对地址
                                // 但为了简化，我们暂时不修改，在ProcessPacket中统一修复
                            }
                        }
                    }
                    catch (Exception copyEx)
                    {
                        // 确保异常时也释放buffer（虽然GC会回收，但显式释放更好）
                        buffer = null;
                        Logger.Instance.Error(string.Format("复制实时数据失败: {0}, lParam={1}, headerSize={2}, dataSize={3}, originalDataOffset={4}", copyEx.Message, lParam.ToInt64(), headerSize, dataSize, originalDataOffset));
                        return;
                    }
                    
                    packet = new DataPacket(type, IntPtr.Zero, wParam)
                    {
                        DataBuffer = buffer,
                        DataSize = totalSize  // 使用实际的总大小，而不是estimatedTotalSize
                    };
                    
                    Logger.Instance.Debug(string.Format("实时数据包创建成功: m_nPacketNum={0}, dataSize={1}, totalSize={2}, buffer.Length={3}", header.m_nPacketNum, dataSize, totalSize, buffer.Length));
                }
                else if (type == DataPacketType.MarketTableData)
                {
                    // 码表数据：使用 HLMarketType 结构
                    // 先读取头部信息
                    HLMarketType header;
                    try
                    {
                        header = (HLMarketType)Marshal.PtrToStructure(
                            lParam, typeof(HLMarketType));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("读取码表数据HLMarketType结构失败: {0}, lParam={1}", ex.Message, lParam.ToInt64()));
                        // 不抛出异常，避免阻塞后续数据接收
                        return;
                    }
                    
                    // 验证数据有效性
                    // 注意：m_nCount是UInt16类型，最大值是65535
                    if (header.m_nCount == 0 || header.m_nCount > 65535)
                    {
                        Logger.Instance.Warning(string.Format("码表数据数量异常: {0}，跳过此数据包", header.m_nCount));
                        return;
                    }
                    
                    // 计算需要复制的数据大小
                    // HLMarketType 结构大小 + 股票数据（每条约44字节 RCV_TABLE_STRUCT）
                    int headerSize = Marshal.SizeOf(typeof(HLMarketType));
                    int recordSize = 44; // RCV_TABLE_STRUCT 大小
                    int totalSize = headerSize + (int)header.m_nCount * recordSize;
                    
                    // 限制最大大小，防止异常数据
                    totalSize = Math.Min(totalSize, 10 * 1024 * 1024); // 最大10MB
                    
                    // 复制数据
                    byte[] buffer = new byte[totalSize];
                    int copySize = Math.Min(totalSize, buffer.Length);
                    if (copySize > 0)
                    {
                        try
                        {
                            Marshal.Copy(lParam, buffer, 0, copySize);
                        }
                        catch (Exception copyEx)
                        {
                            // 确保异常时也释放buffer（防止内存泄漏）
                            buffer = null;
                            Logger.Instance.Error(string.Format("复制码表数据失败: {0}, lParam={1}, copySize={2}, m_nCount={3}", copyEx.Message, lParam.ToInt64(), copySize, header.m_nCount));
                            // 不抛出异常，避免阻塞后续数据接收
                            return;
                        }
                    }
                    
                    packet = new DataPacket(type, IntPtr.Zero, wParam)
                    {
                        DataBuffer = buffer,
                        DataSize = totalSize
                    };
                }
                // F10功能已禁用
                // else if (type == DataPacketType.StockBaseData)
                // {
                //     // F10数据：使用 RCV_DATA 结构
                //     RCV_DATA header;
                //     try
                //     {
                //         header = (RCV_DATA)Marshal.PtrToStructure(
                //             lParam, typeof(RCV_DATA));
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.Instance.Error(string.Format("读取F10数据RCV_DATA结构失败: {0}, lParam={1}", ex.Message, lParam.ToInt64()));
                //         // 不抛出异常，避免阻塞后续数据接收
                //         return;
                //     }
                //
                //     // 计算需要复制的数据大小
                //     // RCV_DATA 结构大小 + 文件数据长度
                //     int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                //     int dataLength = (int)header.m_File.m_dwLen;
                //
                //     // 验证数据长度有效性
                //     if (dataLength < 0 || dataLength > 10 * 1024 * 1024)
                //     {
                //         Logger.Instance.Warning(string.Format("F10数据长度异常: {0}，限制为10MB", dataLength));
                //         dataLength = Math.Min(Math.Max(dataLength, 0), 10 * 1024 * 1024);
                //     }
                //
                //     int totalSize = headerSize + dataLength;
                //
                //     // 限制最大大小，防止异常数据
                //     totalSize = Math.Min(totalSize, 10 * 1024 * 1024); // 最大10MB
                //
                //     // 复制数据（只复制实际可用的数据）
                //     byte[] buffer = new byte[totalSize];
                //     int copySize = Math.Min(totalSize, buffer.Length);
                //     if (copySize > 0)
                //     {
                //         try
                //         {
                //             Marshal.Copy(lParam, buffer, 0, copySize);
                //         }
                //         catch (Exception copyEx)
                //         {
                //             Logger.Instance.Error(string.Format("复制F10数据失败: {0}, lParam={1}, copySize={2}, dataLength={3}", copyEx.Message, lParam.ToInt64(), copySize, dataLength));
                //             // 不抛出异常，避免阻塞后续数据接收
                //             return;
                //         }
                //     }
                //
                //     packet = new DataPacket(type, IntPtr.Zero, wParam)
                //     {
                //         DataBuffer = buffer,
                //         DataSize = totalSize
                //     };
                // }
                else if (type == DataPacketType.MinuteData || type == DataPacketType.Minute5Data)
                {
                    // 分钟数据：需要复制 RCV_DATA 结构
                    RCV_DATA header;
                    try
                    {
                        header = (RCV_DATA)Marshal.PtrToStructure(
                            lParam, typeof(RCV_DATA));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("读取分钟数据RCV_DATA结构失败: {0}, lParam={1}", ex.Message, lParam.ToInt64()));
                        // 不抛出异常，避免阻塞后续数据接收
                        return;
                    }
                    
                    // 验证数据有效性
                    if (header.m_nPacketNum <= 0 || header.m_nPacketNum > 100000)
                    {
                        Logger.Instance.Warning(string.Format("分钟数据包数量异常: {0}，跳过此数据包", header.m_nPacketNum));
                        return;
                    }
                    
                    // 估算数据大小（分钟数据大小不固定）
                    int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                    // 分钟数据头：约40字节（RCV_EKE_HEADEx）
                    // 分钟数据：约20字节（RCV_MINUTE_STRUCTEx）
                    int estimatedDataSize = headerSize + (int)header.m_nPacketNum * 60; // 估算每条记录60字节
                    
                    // 限制最大大小
                    estimatedDataSize = Math.Min(estimatedDataSize, 10 * 1024 * 1024); // 最大10MB
                    
                    // 复制数据（只复制结构体部分，实际数据大小可能不同）
                    // 注意：分钟数据大小不固定，这里只复制结构体和估算的数据大小
                    byte[] buffer = new byte[estimatedDataSize];
                    int copySize = Math.Min(estimatedDataSize, buffer.Length);
                    if (copySize > 0)
                    {
                        try
                        {
                            // 只复制结构体部分，避免读取超出实际数据大小
                            int safeCopySize = Math.Min(copySize, headerSize + 1024); // 至少复制结构体+1KB
                            Marshal.Copy(lParam, buffer, 0, safeCopySize);
                        }
                        catch (Exception copyEx)
                        {
                            // 确保异常时也释放buffer（防止内存泄漏）
                            buffer = null;
                            Logger.Instance.Error(string.Format("复制分钟数据失败: {0}, lParam={1}, copySize={2}", copyEx.Message, lParam.ToInt64(), copySize));
                            // 不抛出异常，避免阻塞后续数据接收
                            return;
                        }
                    }
                    
                    packet = new DataPacket(type, IntPtr.Zero, wParam)
                    {
                        DataBuffer = buffer,
                        DataSize = estimatedDataSize
                    };
                }
                else if (type == DataPacketType.DailyData || type == DataPacketType.ExRightsData)
                {
                    // 日线数据和除权数据：需要复制 RCV_DATA 结构
                    // 这些数据也是异步处理的，原始指针可能在处理前被DLL覆盖
                    RCV_DATA header;
                    try
                    {
                        header = (RCV_DATA)Marshal.PtrToStructure(
                            lParam, typeof(RCV_DATA));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("读取{0}数据RCV_DATA结构失败: {1}, lParam={2}",
                            type == DataPacketType.DailyData ? "日线" : "除权", ex.Message, lParam.ToInt64()));
                        return;
                    }

                    // 验证数据有效性
                    if (header.m_nPacketNum <= 0 || header.m_nPacketNum > 100000)
                    {
                        Logger.Instance.Warning(string.Format("{0}数据包数量异常: {1}，跳过此数据包",
                            type == DataPacketType.DailyData ? "日线" : "除权", header.m_nPacketNum));
                        return;
                    }

                    // 计算需要复制的数据大小
                    int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                    // 日线数据：RCV_HISTORY_STRUCTEx 约32字节
                    // 除权数据：RCV_POWER_STRUCTEx 约24字节
                    int recordSize = type == DataPacketType.DailyData ? 32 : 24;
                    int dataSize = (int)header.m_nPacketNum * recordSize;
                    dataSize = Math.Min(dataSize, 10 * 1024 * 1024); // 最大10MB

                    // 验证m_pData指针有效性
                    if (header.m_pData == IntPtr.Zero)
                    {
                        Logger.Instance.Warning(string.Format("{0}数据m_pData指针为空，跳过此数据包",
                            type == DataPacketType.DailyData ? "日线" : "除权"));
                        return;
                    }

                    // 计算总大小：结构体 + 数据
                    int totalSize = headerSize + dataSize;
                    byte[] buffer = new byte[totalSize];

                    try
                    {
                        // 复制结构体部分
                        Marshal.Copy(lParam, buffer, 0, headerSize);

                        // 复制数据部分
                        if (dataSize > 0 && header.m_pData != IntPtr.Zero)
                        {
                            int actualDataSize = Math.Min(dataSize, totalSize - headerSize);
                            if (actualDataSize > 0)
                            {
                                Marshal.Copy(header.m_pData, buffer, headerSize, actualDataSize);
                            }
                        }

                        Logger.Instance.Info(string.Format("【入队】{0}数据包创建成功: m_wDataType={1}, m_nPacketNum={2}, totalSize={3}",
                            type == DataPacketType.DailyData ? "日线" : "除权", header.m_wDataType, header.m_nPacketNum, totalSize));
                    }
                    catch (Exception copyEx)
                    {
                        buffer = null;
                        Logger.Instance.Error(string.Format("复制{0}数据失败: {1}",
                            type == DataPacketType.DailyData ? "日线" : "除权", copyEx.Message));
                        return;
                    }

                    packet = new DataPacket(type, IntPtr.Zero, wParam)
                    {
                        DataBuffer = buffer,
                        DataSize = totalSize
                    };
                }
                else
                {
                    // 其他类型：直接使用指针（如果数据不会被覆盖）
                    packet = new DataPacket(type, lParam, wParam);
                }

                // 将数据包加入对应的队列
                if (packet != null)
                {
                    targetQueue.Enqueue(packet);
                    dataQueue.Enqueue(packet); // 兼容统计

                    // 更新统计（使用 Interlocked 无锁操作，提升性能）
                    int newEnqueued = Interlocked.Increment(ref totalEnqueued);
                    int totalSize = realTimeQueue.Count + otherDataQueue.Count;
                    // 使用 CompareExchange 更新最大值（无锁方式）
                    int currentMax = maxQueueSize;
                    while (totalSize > currentMax)
                    {
                        int oldMax = Interlocked.CompareExchange(ref maxQueueSize, totalSize, currentMax);
                        if (oldMax == currentMax) break;
                        currentMax = oldMax;
                    }

                    // 记录入队成功（实时数据每1000条记录一次，减少日志开销）
                    if (type == DataPacketType.RealTimeData)
                    {
                        if (newEnqueued % 1000 == 0)
                        {
                            Logger.Instance.Debug(string.Format("实时数据包入队成功: 队列大小={0}, 累计入队={1}", realTimeQueue.Count, newEnqueued));
                        }
                    }
                }
                else
                {
                    Logger.Instance.Warning(string.Format("数据包创建失败，类型: {0}，未加入队列", type));
                }

                // 警告队列积压
                int totalQueueSize = realTimeQueue.Count + otherDataQueue.Count;
                if (totalQueueSize >= QUEUE_WARNING_THRESHOLD)
                {
                    Logger.Instance.Warning(string.Format("数据队列积压: 实时={0}, 其他={1}, 总计={2}", realTimeQueue.Count, otherDataQueue.Count, totalQueueSize));
                }
                
                // 内存压力处理：当队列积压过多时，丢弃部分旧数据以释放内存
                if (totalQueueSize >= MEMORY_PRESSURE_THRESHOLD)
                {
                    Logger.Instance.Warning(string.Format("检测到内存压力，开始清理旧数据: 队列大小={0}", totalQueueSize));
                    ClearOldPackets(500);  // 丢弃500个旧数据包
                }

                // 根据数据类型通知相应的处理线程
                if (type == DataPacketType.RealTimeData)
                {
                    realTimeDataAvailableEvent.Set();  // 通知实时数据处理线程
                }
                // F10功能已禁用
                // else if (type == DataPacketType.StockBaseData)
                // {
                //     f10DataAvailableEvent.Set();        // 通知F10数据处理线程
                // }
                else
                {
                    otherDataAvailableEvent.Set();     // 通知其他数据处理线程
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("数据包入队失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                // 不抛出异常，避免阻塞后续数据接收
                // 记录错误后返回，让其他数据继续处理
            }
        }

        /// <summary>
        /// 实时数据处理循环（在独立的高优先级线程中运行）
        /// 使用 SpinWait 优化，减少延迟
        /// </summary>
        private void ProcessRealTimeDataLoop()
        {
            Logger.Instance.Info("实时数据处理线程开始运行（优先级：Highest，使用 SpinWait 优化）");
            SpinWait spinner = new SpinWait();

            while (!shouldStop)
            {
                try
                {
                    // 优先检查队列是否有数据（低延迟处理）
                    if (!realTimeQueue.IsEmpty)
                    {
                        ProcessRealTimeQueue();
                        spinner.Reset(); // 有数据时重置自旋计数器
                    }
                    else
                    {
                        // 队列为空时，使用自适应等待策略
                        spinner.SpinOnce();

                        // 当自旋次数过多时，使用事件等待（节省 CPU）
                        if (spinner.NextSpinWillYield)
                        {
                            // 使用较短的等待时间（10ms），保持低延迟
                            realTimeDataAvailableEvent.Wait(10);
                            realTimeDataAvailableEvent.Reset(); // 重置事件，准备下次触发
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    Logger.Instance.Info("实时数据处理线程被终止");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("实时数据处理循环异常: {0}", ex.Message));
                    Thread.Sleep(10); // 出错后短暂休息，快速恢复
                }
            }

            // 处理剩余实时数据
            ProcessRealTimeQueue();

            Logger.Instance.Info("实时数据处理线程已退出");
        }
        
        /// <summary>
        /// 其他数据处理循环（在独立的普通优先级线程中运行）
        /// </summary>
        private void ProcessOtherDataLoop()
        {
            Logger.Instance.Info("其他数据处理线程开始运行（优先级：Normal）");

            while (!shouldStop)
            {
                try
                {
                    // 检查队列是否有数据
                    if (!otherDataQueue.IsEmpty)
                    {
                        ProcessOtherQueue();
                    }
                    else
                    {
                        // 队列为空时，使用事件等待（节省 CPU）
                        otherDataAvailableEvent.Wait(500); // 等待 500ms
                        otherDataAvailableEvent.Reset();
                    }
                }
                catch (ThreadAbortException)
                {
                    Logger.Instance.Info("其他数据处理线程被终止");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("其他数据处理循环异常: {0}", ex.Message));
                    Thread.Sleep(100); // 出错后稍作休息
                }
            }

            // 处理剩余其他数据
            ProcessOtherQueue();
            
            Logger.Instance.Info("其他数据处理线程已退出");
        }

        /// <summary>
        /// 处理实时数据队列（在实时数据处理线程中运行）
        /// 使用动态批处理大小，根据队列积压情况调整
        /// </summary>
        private void ProcessRealTimeQueue()
        {
            int processedCount = 0;
            // 动态批处理大小：根据队列积压情况调整，最大500条
            int batchSize = Math.Min(Math.Max(realTimeQueue.Count, 100), 500);
            DataPacket packet;

            while (processedCount < batchSize && realTimeQueue.TryDequeue(out packet))
            {
                try
                {
                    ProcessPacket(packet);
                    processedCount++;

                    // 使用 Interlocked 无锁更新统计
                    int newProcessed = Interlocked.Increment(ref totalProcessed);

                    // 记录处理成功（每10000条记录一次，减少日志量）
                    if (newProcessed % 10000 == 0)
                    {
                        Logger.Instance.Debug(string.Format("实时数据包处理: 累计处理={0}, 队列剩余={1}", newProcessed, realTimeQueue.Count));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("处理实时数据包失败: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                }
                finally
                {
                    // 确保数据包的缓冲区被释放（即使处理失败）
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                    }
                }
            }
        }
        
        /// <summary>
        /// 处理其他数据队列（在其他数据处理线程中运行）
        /// </summary>
        private void ProcessOtherQueue()
        {
            int processedCount = 0;
            const int MAX_BATCH_SIZE = 50; // 其他数据批次较小，避免长时间占用线程
            DataPacket packet;

            while (processedCount < MAX_BATCH_SIZE && otherDataQueue.TryDequeue(out packet))
            {
                try
                {
                    ProcessPacket(packet);
                    processedCount++;

                    // 使用 Interlocked 无锁更新统计
                    Interlocked.Increment(ref totalProcessed);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("处理其他数据包失败: {0}, 类型: {1}", ex.Message, packet.Type));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                }
                finally
                {
                    // 确保数据包的缓冲区被释放（即使处理失败）
                    if (packet != null && packet.DataBuffer != null)
                    {
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                    }
                }
            }
        }
        
        // F10功能已禁用
        // /// <summary>
        // /// F10数据处理循环（在独立的低优先级线程中运行）
        // /// </summary>
        // private void ProcessF10DataLoop()
        // {
        //     Logger.Instance.Info("F10数据处理线程开始运行（优先级：Lowest）");
        //
        //     while (!shouldStop)
        //     {
        //         try
        //         {
        //             // 等待F10数据到达或停止信号
        //             if (f10DataAvailableEvent.WaitOne(2000)) // 最多等待2秒，F10数据不紧急
        //             {
        //                 // 处理F10数据队列
        //                 ProcessF10Queue();
        //             }
        //             else
        //             {
        //                 // 超时，检查是否有数据（防止遗漏）
        //                 if (!f10DataQueue.IsEmpty)
        //                 {
        //                     ProcessF10Queue();
        //                 }
        //             }
        //         }
        //         catch (ThreadAbortException)
        //         {
        //             Logger.Instance.Info("F10数据处理线程被终止");
        //             break;
        //         }
        //         catch (Exception ex)
        //         {
        //             Logger.Instance.Error(string.Format("F10数据处理循环异常: {0}", ex.Message));
        //             System.Threading.Thread.Sleep(200); // 出错后稍作休息，F10数据不紧急
        //         }
        //     }
        //
        //     // 处理剩余F10数据
        //     ProcessF10Queue();
        //
        //     Logger.Instance.Info("F10数据处理线程已退出");
        // }
        //
        // /// <summary>
        // /// 处理F10数据队列（在F10数据处理线程中运行）
        // /// </summary>
        // private void ProcessF10Queue()
        // {
        //     int processedCount = 0;
        //     const int MAX_BATCH_SIZE = 10; // F10数据批次很小，因为文件I/O操作较慢
        //
        //     while (processedCount < MAX_BATCH_SIZE && !f10DataQueue.IsEmpty)
        //     {
        //         if (f10DataQueue.TryDequeue(out DataPacket packet))
        //         {
        //             try
        //             {
        //                 // F10数据处理可能涉及文件I/O，在独立线程中执行，不阻塞实时数据
        //                 ProcessPacket(packet);
        //                 processedCount++;
        //
        //                 lock (statsLock)
        //                 {
        //                     totalProcessed++;
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 Logger.Instance.Error(string.Format("处理F10数据包失败: {0}", ex.Message));
        //                 Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
        //             }
        //         }
        //         else
        //         {
        //             break;
        //         }
        //     }
        // }

        /// <summary>
        /// 处理单个数据包
        /// </summary>
        private void ProcessPacket(DataPacket packet)
        {
            IntPtr dataPtr = IntPtr.Zero;
            bool needFree = false;
            GCHandle? pinnedHandle = null;
            
            try
            {
                // 如果LParam不为空，说明直接使用了龙卷风DLL的原始指针
                // 注意：这个指针是DLL提供的，不是我们分配的，不能释放！
                if (packet.LParam != IntPtr.Zero)
                {
                    dataPtr = packet.LParam;
                    needFree = false;  // 不能释放！这是DLL提供的指针，不是我们分配的
                    
                    // 验证m_pData指针是否正确（仅用于调试）
                    if (packet.Type == DataPacketType.RealTimeData)
                    {
                        try
                        {
                            RCV_DATA header = (RCV_DATA)Marshal.PtrToStructure(dataPtr, typeof(RCV_DATA));
                            int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                            IntPtr expectedDataPtr = new IntPtr(dataPtr.ToInt64() + headerSize);
                            
                            // 如果m_pData指针不正确，修复它
                            if (header.m_pData != expectedDataPtr)
                            {
                                Logger.Instance.Debug(string.Format("修复m_pData指针: 原值={0}, 期望值={1}", header.m_pData.ToInt64(), expectedDataPtr.ToInt64()));
                                // 使用直接修改内存的方式，避免Marshal.StructureToPtr在32位系统上的问题
                                IntPtr m_pDataOffset = new IntPtr(dataPtr.ToInt64() + 
                                    Marshal.OffsetOf(typeof(RCV_DATA), "m_pData").ToInt64());
                                Marshal.WriteIntPtr(m_pDataOffset, expectedDataPtr);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Warning(string.Format("验证m_pData指针时出错: {0}", ex.Message));
                        }
                    }
                }
                // 如果数据被复制到缓冲区，需要重新创建 IntPtr
                else if (packet.DataBuffer != null && packet.DataBuffer.Length > 0)
                {
                    // 分配非托管内存并复制数据
                    // 在32位系统上，需要确保有足够的空间来写入结构体
                    int headerSize = Marshal.SizeOf(typeof(RCV_DATA));
                    int safeAllocSize = Math.Max(packet.DataSize, headerSize * 3); // 至少是结构体大小的3倍
                    
                    // 先分配内存，立即设置needFree，确保即使后续出错也能释放
                    dataPtr = Marshal.AllocHGlobal(safeAllocSize);
                    needFree = true;  // 立即标记需要释放
                    
                    // 复制数据
                    int copySize = Math.Min(packet.DataSize, safeAllocSize);
                    Marshal.Copy(packet.DataBuffer, 0, dataPtr, copySize);
                    
                    // 对于需要 m_pData 指针的数据类型，需要修复指针
                    if (packet.Type == DataPacketType.RealTimeData ||
                        packet.Type == DataPacketType.DailyData ||
                        packet.Type == DataPacketType.ExRightsData)
                    {
                        try
                        {
                            // 先读取结构体，检查数据是否正确
                            RCV_DATA header = (RCV_DATA)Marshal.PtrToStructure(dataPtr, typeof(RCV_DATA));
                            
                            // 如果m_nPacketNum为0，说明结构体可能没有正确复制，尝试从buffer重新读取
                            if (header.m_nPacketNum == 0 && packet.DataBuffer != null && packet.DataBuffer.Length >= headerSize)
                            {
                                // 从buffer重新读取结构体
                                // 使用局部变量确保handle在finally中释放
                                GCHandle tempHandle = GCHandle.Alloc(packet.DataBuffer, GCHandleType.Pinned);
                                // 如果之前已经有pinnedHandle，先释放它
                                if (pinnedHandle.HasValue)
                                {
                                    try
                                    {
                                        pinnedHandle.Value.Free();
                                    }
                                    catch { }
                                }
                                pinnedHandle = tempHandle;  // 保存引用以便在finally中释放
                                try
                                {
                                    IntPtr bufferPtr = tempHandle.AddrOfPinnedObject();
                                    header = (RCV_DATA)Marshal.PtrToStructure(bufferPtr, typeof(RCV_DATA));
                                    
                                    // 重新复制整个buffer（包括结构体和数据）到dataPtr
                                    // 确保分配的内存足够大（不同数据类型记录大小不同）
                                    int recordSize = 158; // 默认实时数据记录大小
                                    if (packet.Type == DataPacketType.DailyData)
                                        recordSize = 32; // 日线数据记录大小
                                    else if (packet.Type == DataPacketType.ExRightsData)
                                        recordSize = 24; // 除权数据记录大小
                                    int requiredSize = Math.Max(packet.DataSize, headerSize + (int)header.m_nPacketNum * recordSize);
                                    if (safeAllocSize < requiredSize)
                                    {
                                        // 需要重新分配更大的内存
                                        Marshal.FreeHGlobal(dataPtr);
                                        safeAllocSize = Math.Max(requiredSize, headerSize * 3);
                                        dataPtr = Marshal.AllocHGlobal(safeAllocSize);
                                    }
                                    
                                    // 复制整个buffer（包括结构体和数据）
                                    int fullCopySize = Math.Min(packet.DataSize, safeAllocSize);
                                    Marshal.Copy(packet.DataBuffer, 0, dataPtr, fullCopySize);
                                }
                                finally
                                {
                                    // 确保GCHandle被释放（即使发生异常）
                                    if (pinnedHandle.HasValue)
                                    {
                                        try
                                        {
                                            pinnedHandle.Value.Free();
                                        }
                                        catch (Exception freeEx)
                                        {
                                            Logger.Instance.Warning(string.Format("释放GCHandle失败: {0}", freeEx.Message));
                                        }
                                        pinnedHandle = null;
                                    }
                                }
                            }
                            
                            // 修复 m_pData 指针（指向复制后的数据位置）
                            // 计算m_pData应该指向的位置（紧跟在结构体后面）
                            IntPtr newDataPtr = new IntPtr(dataPtr.ToInt64() + headerSize);
                            
                            // 直接修改内存中的指针值（最安全的方法，避免Marshal.StructureToPtr的问题）
                            IntPtr m_pDataOffset = new IntPtr(dataPtr.ToInt64() + 
                                Marshal.OffsetOf(typeof(RCV_DATA), "m_pData").ToInt64());
                            Marshal.WriteIntPtr(m_pDataOffset, newDataPtr);
                            
                            // 验证修复后的结构体
                            RCV_DATA verifyHeader = (RCV_DATA)Marshal.PtrToStructure(dataPtr, typeof(RCV_DATA));
                            
                            // 详细验证数据
                            if (verifyHeader.m_nPacketNum == 0)
                            {
                                Logger.Instance.Warning("修复m_pData指针后，m_nPacketNum仍为0，数据可能不正确");
                                Logger.Instance.Warning(string.Format("dataPtr={0}, m_pData={1}, headerSize={2}", dataPtr.ToInt64(), verifyHeader.m_pData.ToInt64(), headerSize));
                            }
                            else
                            {
                                // 验证第一条数据是否可以读取
                                if (verifyHeader.m_pData != IntPtr.Zero && verifyHeader.m_nPacketNum > 0)
                                {
                                    try
                                    {
                                        IntPtr firstRecordPtr = new IntPtr(verifyHeader.m_pData.ToInt64());
                                        RCV_REPORT_STRUCTExV3 firstRecord = (RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                            firstRecordPtr, typeof(RCV_REPORT_STRUCTExV3));
                                        string testCode = new string(firstRecord.m_szLabel).Trim('\0');
                                        if (string.IsNullOrEmpty(testCode))
                                        {
                                            Logger.Instance.Warning("修复m_pData指针后，第一条记录的股票代码为空，数据可能不正确");
                                            Logger.Instance.Warning(string.Format("m_pData={0}, dataPtr={1}, offset={2}", verifyHeader.m_pData.ToInt64(), dataPtr.ToInt64(), verifyHeader.m_pData.ToInt64() - dataPtr.ToInt64()));
                                        }
                                    }
                                    catch (Exception verifyEx)
                                    {
                                        Logger.Instance.Warning(string.Format("验证第一条数据失败: {0}", verifyEx.Message));
                                    }
                                }
                            }
                        }
                        catch (Exception ptrEx)
                        {
                            Logger.Instance.Warning(string.Format("修复实时数据m_pData指针失败: {0}，将尝试直接修改内存", ptrEx.Message));
                            // 如果失败，尝试直接修改内存中的指针值
                            try
                            {
                                IntPtr m_pDataOffset = new IntPtr(dataPtr.ToInt64() + 
                                    Marshal.OffsetOf(typeof(RCV_DATA), "m_pData").ToInt64());
                                IntPtr newDataPtr = new IntPtr(dataPtr.ToInt64() + headerSize);
                                Marshal.WriteIntPtr(m_pDataOffset, newDataPtr);
                            }
                            catch
                            {
                                // 如果还是失败，记录警告但继续处理
                                Logger.Instance.Warning("无法修复m_pData指针，数据可能无法正确处理");
                            }
                        }
                    }
                }
                else
                {
                    // 直接使用原始指针（注意：此时数据可能已被覆盖，但某些情况下仍可用）
                    dataPtr = packet.LParam;
                }

                // 根据类型调用相应的处理方法
                switch (packet.Type)
                {
                    case DataPacketType.RealTimeData:
                        dataReceiver.ProcessRealTimeData(dataPtr);
                        break;
                        
                    case DataPacketType.MinuteData:
                        dataReceiver.ProcessMinuteData(dataPtr);
                        break;
                        
                    case DataPacketType.Minute5Data:
                        dataReceiver.Process5MinuteData(dataPtr);
                        break;
                        
                    case DataPacketType.MarketTableData:
                        dataReceiver.ProcessMarketTableData(dataPtr);
                        // 注意：码表数据处理后会触发 MarketTableReceived 事件
                        // 事件处理中会通过 Invoke 更新UI，所以这里不需要额外处理
                        break;
                        
                    case DataPacketType.DailyData:
                        // 日线数据处理（优先使用MQ发送器）
                        Logger.Instance.Info(string.Format("【出队】日线数据开始处理, dailyDataProcessorMQ={0}",
                            dailyDataProcessorMQ != null ? "已设置" : "未设置"));
                        if (dailyDataProcessorMQ != null)
                        {
                            // 调试：打印数据类型
                            try
                            {
                                RCV_DATA debugHeader = (RCV_DATA)Marshal.PtrToStructure(dataPtr, typeof(RCV_DATA));
                                Logger.Instance.Info(string.Format("【出队】日线数据处理前: m_wDataType={0}, m_nPacketNum={1}, m_pData={2}",
                                    debugHeader.m_wDataType, debugHeader.m_nPacketNum, debugHeader.m_pData.ToInt64()));
                            }
                            catch (Exception debugEx)
                            {
                                Logger.Instance.Warning(string.Format("调试日线数据结构失败: {0}", debugEx.Message));
                            }
                            dailyDataProcessorMQ.ProcessDailyData(dataPtr);
                            Logger.Instance.Info("【出队】日线数据处理完成");
                        }
                        else
                        {
                            // 只打印一次警告，避免日志刷屏
                            if (!dailyDataWarningLogged)
                            {
                                Logger.Instance.Warning("收到日线数据，但未设置日线数据MQ处理器（此警告只显示一次）");
                                dailyDataWarningLogged = true;
                            }
                        }
                        break;
                        
                    case DataPacketType.ExRightsData:
                        // 除权数据处理（优先使用MQ发送器）
                        if (exRightsDataProcessorMQ != null)
                        {
                            // 调试：打印数据类型
                            try
                            {
                                RCV_DATA debugHeader = (RCV_DATA)Marshal.PtrToStructure(dataPtr, typeof(RCV_DATA));
                                Logger.Instance.Debug(string.Format("除权数据处理前: m_wDataType={0}, m_nPacketNum={1}, m_pData={2}",
                                    debugHeader.m_wDataType, debugHeader.m_nPacketNum, debugHeader.m_pData.ToInt64()));
                            }
                            catch (Exception debugEx)
                            {
                                Logger.Instance.Warning(string.Format("调试除权数据结构失败: {0}", debugEx.Message));
                            }
                            exRightsDataProcessorMQ.ProcessExRightsData(dataPtr);
                        }
                        else
                        {
                            // 只打印一次警告，避免日志刷屏
                            if (!exRightsWarningLogged)
                            {
                                Logger.Instance.Warning("收到除权数据，但未设置除权数据MQ处理器（此警告只显示一次）");
                                exRightsWarningLogged = true;
                            }
                        }
                        break;

                    // F10功能已禁用
                    // case DataPacketType.StockBaseData:
                    //     dataReceiver.ProcessStockBaseData(dataPtr);
                    //     // F10数据处理后会触发 StockBaseDataReceived 事件
                    //     break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("处理数据包异常: {0}, 类型: {1}", ex.Message, packet.Type));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
            finally
            {
                // 释放GCHandle（如果已分配）
                if (pinnedHandle.HasValue)
                {
                    try
                    {
                        pinnedHandle.Value.Free();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning(string.Format("释放GCHandle失败: {0}", ex.Message));
                    }
                    pinnedHandle = null;
                }
                
                // 释放分配的非托管内存
                if (needFree && dataPtr != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(dataPtr);
                        dataPtr = IntPtr.Zero;  // 重置指针，防止重复释放
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("释放非托管内存失败: {0}", ex.Message));
                        Logger.Instance.Error(string.Format("dataPtr={0}, needFree={1}", dataPtr.ToInt64(), needFree));
                    }
                }
                
                // 重要：清空数据包的缓冲区，释放内存（防止内存泄漏）
                if (packet != null && packet.DataBuffer != null)
                {
                    try
                    {
                        // 清空缓冲区引用，让GC可以回收
                        packet.DataBuffer = null;
                        packet.DataSize = 0;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning(string.Format("清空数据包缓冲区失败: {0}", ex.Message));
                    }
                }
            }
        }

        /// <summary>
        /// 获取队列统计信息（无锁读取，使用 volatile 保证可见性）
        /// </summary>
        public string GetStatistics()
        {
            int realTimeCount = realTimeQueue.Count;
            int otherCount = otherDataQueue.Count;
            return string.Format("队列: 实时={0}, 其他={1}, 总计={2}, 已入队: {3}, 已处理: {4}, 最大队列: {5}",
                realTimeCount, otherCount, realTimeCount + otherCount, totalEnqueued, totalProcessed, maxQueueSize);
        }
        
        /// <summary>
        /// 检查队列是否为空
        /// </summary>
        public bool IsEmpty
        {
            get { return realTimeQueue.IsEmpty && otherDataQueue.IsEmpty; }
        }

        /// <summary>
        /// 获取当前队列大小
        /// </summary>
        public int GetQueueSize()
        {
            return dataQueue.Count;
        }

        /// <summary>
        /// 检查队列是否正在运行
        /// </summary>
        public bool IsRunning()
        {
            return isRunning;
        }
    }
}

