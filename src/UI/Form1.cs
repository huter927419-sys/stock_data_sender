using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;        //注册表命名空间
using StockDataMQClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Configuration;   // 用于读取配置文件


namespace StockDataMQClient
{
    public partial class Form1 : Form
    {

        //要加载的驱动dll实例
        static private int instance;
        //要加载方法的委托
        public delegate int Stock_Init(IntPtr nHwnd, int nMsg, int nWorkMode);
        public delegate int Stock_Quit(IntPtr nHwnd);
        public delegate int GetStockDrvInfo(int nInfo, IntPtr pBuf);
        public delegate int SetupReceiver(bool bShowWindow);  // 激活接收程序,进行设置
        public delegate int ReInitStockInfo();  // 保留函数，暂无用
        
        // 扩展API函数
        public delegate int AskStockDay(string pszStockCode, int nTimePeriod);  // 取日线数据
        public delegate int AskStockMn5(string pszStockCode, int nTimePeriod);  // 取五分钟数据
        // public delegate int AskStockBase(string pszStockCode);  // 取个股资料F10 - 已禁用
        public delegate int AskStockNews();  // 取财经新闻
        public delegate int AskStockHalt();  // 中止补数
        public delegate int AskStockMin(string pszStockCode);  // 取分时数据
        public delegate int AskStockPRP(string pszStockCode);  // 取分笔数据
        public delegate int AskStockPwr();  // 取除权数据
        public delegate int AskStockFin();  // 取财务数据


        [DllImport("Kernel32")]
        public static extern int LoadLibrary(String funcname);
        [DllImport("Kernel32")]
        public static extern int GetProcAddress(int handle, String funcname);
        [DllImport("Kernel32")]
        public static extern int FreeLibrary(int handle);

        public static Delegate GetAddress(int dllModule, string functionname, Type t)
        {
            int addr = GetProcAddress(dllModule, functionname);
            if (addr == 0)
                return null;
            else
                return Marshal.GetDelegateForFunctionPointer(new IntPtr(addr), t);
        }

        // 数据管理层
        private DataManager dataManager = new DataManager();
        private DataReceiver dataReceiver = new DataReceiver();
        private DataCollector dataCollector = new DataCollector();
        private DataQueue dataQueue;  // 数据队列（用于异步处理，避免阻塞回调）
        private MinuteDataManager minuteDataManager = new MinuteDataManager();
        // private F10DataManager f10DataManager;  // F10数据管理器 - 已禁用
        private DailyDataProcessorMQ dailyDataProcessorMQ;  // 日线数据处理器（发送到MQ）
        private RealTimeDataProcessorMQ realTimeDataProcessorMQ;  // 实时数据处理器（发送到MQ）
        private MarketTableDataProcessorMQ marketTableDataProcessorMQ;  // 码表数据处理器（发送到MQ）
        private ExRightsDataProcessorMQ exRightsDataProcessorMQ;  // 除权数据处理器（发送到MQ）
        private MQStatistics mqStatistics;  // MQ统计数据
        
        // 数据接收状态
        private bool isReceivingData = false;  // 是否正在接收数据

        // F10数据请求跟踪 - 已禁用
        // private string pendingF10StockCode = "";
        // private bool isF10WindowOpen = false;
        // private DateTime f10RequestTime = DateTime.MinValue;
        // private System.Windows.Forms.Timer f10TimeoutTimer;

        public Form1()
        {
            InitializeComponent();
            InitializeMenuStrip();  // 初始化菜单栏样式
            InitializePanels();  // 初始化面板（只保留日志面板）
            InitializeDataManager();
            InitializeLogger();
            // 延迟执行，确保控件已完全初始化
            this.Load += Form1_Load;
        }

        /// <summary>
        /// 初始化面板（只保留日志面板）
        /// </summary>
        private void InitializePanels()
        {
            // 设置日志面板样式
            panelLog.BackColor = Color.FromArgb(30, 30, 30);
            panelLog.Dock = DockStyle.Fill;
            panelLog.Visible = true;
            
            // 隐藏所有不需要的面板和控件
            if (splitContainerMain != null)
            {
                splitContainerMain.Visible = false;
            }
            if (splitContainerRight != null)
            {
                splitContainerRight.Visible = false;
            }
            if (panelData != null)
            {
                panelData.Visible = false;
            }
            if (panelF10 != null)
            {
                panelF10.Visible = false;
            }
            if (txtSearchStock != null)
            {
                txtSearchStock.Visible = false;
            }
            if (btnSearchStock != null)
            {
                btnSearchStock.Visible = false;
            }
            if (lblSearchStock != null)
            {
                lblSearchStock.Visible = false;
            }
            if (lblStockDataTitle != null)
            {
                lblStockDataTitle.Visible = false;
            }
            if (dgvStockData != null)
            {
                dgvStockData.Visible = false;
            }
            
            // 将日志面板直接添加到窗体，占据整个客户区（除了菜单栏）
            if (panelLog.Parent != this)
            {
                this.Controls.Add(panelLog);
            }
            panelLog.BringToFront();
        }

        // 板块管理相关方法已删除

        /// <summary>
        /// 初始化菜单栏样式
        /// </summary>
        private void InitializeMenuStrip()
        {
            // 设置菜单栏样式，使其与深色主题一致
            menuStrip1.Renderer = new ToolStripProfessionalRenderer(new MenuColorTable());
            menuStrip1.Padding = new Padding(0);  // 去除菜单栏的内边距
        }
        
        /// <summary>
        /// 菜单颜色表（深色主题）
        /// </summary>
        private class MenuColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected
            {
                get { return Color.FromArgb(60, 60, 60); }
            }
            
            public override Color MenuItemSelectedGradientBegin
            {
                get { return Color.FromArgb(60, 60, 60); }
            }
            
            public override Color MenuItemSelectedGradientEnd
            {
                get { return Color.FromArgb(60, 60, 60); }
            }
            
            public override Color MenuItemBorder
            {
                get { return Color.FromArgb(80, 80, 80); }
            }
            
            public override Color MenuBorder
            {
                get { return Color.FromArgb(50, 50, 50); }
            }
            
            public override Color MenuItemPressedGradientBegin
            {
                get { return Color.FromArgb(70, 70, 70); }
            }
            
            public override Color MenuItemPressedGradientEnd
            {
                get { return Color.FromArgb(70, 70, 70); }
            }
            
            public override Color ImageMarginGradientBegin
            {
                get { return Color.FromArgb(40, 40, 40); }
            }
            
            public override Color ImageMarginGradientMiddle
            {
                get { return Color.FromArgb(40, 40, 40); }
            }
            
            public override Color ImageMarginGradientEnd
            {
                get { return Color.FromArgb(40, 40, 40); }
            }
        }
        
        /// <summary>
        /// 初始化股票数据表格
        /// </summary>
        private void InitializeStockDataGridView()
        {
            dgvStockData.AutoGenerateColumns = false;
            dgvStockData.AllowUserToAddRows = false;
            dgvStockData.AllowUserToDeleteRows = false;
            dgvStockData.ReadOnly = true;
            dgvStockData.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvStockData.MultiSelect = false;
            dgvStockData.RowHeadersVisible = false;
            dgvStockData.ColumnHeadersVisible = true;  // 确保列标题可见

            // 清空现有列
            dgvStockData.Columns.Clear();
            
            // 添加详细数据列（类似CnStkDemo的显示方式）
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StockCode",
                HeaderText = "代码",
                DataPropertyName = "StockCode",
                Width = 100
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StockName",
                HeaderText = "名称",
                DataPropertyName = "StockName",
                Width = 120
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NewPrice",
                HeaderText = "最新价",
                DataPropertyName = "NewPrice",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Open",
                HeaderText = "开盘",
                DataPropertyName = "Open",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastClose",
                HeaderText = "昨收",
                DataPropertyName = "LastClose",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "High",
                HeaderText = "最高",
                DataPropertyName = "High",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Low",
                HeaderText = "最低",
                DataPropertyName = "Low",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Volume",
                HeaderText = "成交量",
                DataPropertyName = "Volume",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F0", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Amount",
                HeaderText = "成交额",
                DataPropertyName = "Amount",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ChangePercent",
                HeaderText = "涨跌幅(%)",
                DataPropertyName = "ChangePercent",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ChangeAmount",
                HeaderText = "涨跌额",
                DataPropertyName = "ChangeAmount",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UpdateTime",
                HeaderText = "更新时间",
                DataPropertyName = "UpdateTime",
                Width = 150,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" }
            });
            
            dgvStockData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MarketType",
                HeaderText = "市场",
                DataPropertyName = "MarketType",
                Width = 60
            });
            
            // F10功能已禁用 - 双击事件已禁用
            // // 添加双击事件，双击某行时查看F10数据
            // dgvStockData.CellDoubleClick += DgvStockData_CellDoubleClick;
        }

        // F10功能已禁用
        // /// <summary>
        // /// DataGridView双击事件 - 双击某行时查看F10数据
        // /// </summary>
        /*
        private void DgvStockData_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || dgvStockData == null || e.RowIndex >= dgvStockData.Rows.Count)
                    return;

                var row = dgvStockData.Rows[e.RowIndex];
                if (row == null || row.Cells == null)
                    return;

                var stockCodeCell = row.Cells["StockCode"];
                if (stockCodeCell == null || stockCodeCell.Value == null)
                    return;

                string stockCode = stockCodeCell.Value.ToString();
                
                if (!string.IsNullOrEmpty(stockCode))
                {
                    // 标准化股票代码
                    string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                    
                    Logger.Instance.Info(string.Format("双击股票行，请求F10数据: {0}", normalizedCode));
                    
                    // 记录当前请求的股票代码（用于过滤后续收到的F10数据）
                    pendingF10StockCode = normalizedCode;
                    f10RequestTime = DateTime.Now;  // 记录请求时间
                    Logger.Instance.Info(string.Format("已设置待处理F10股票代码: {0}, 请求时间: {1:yyyy-MM-dd HH:mm:ss.fff}", pendingF10StockCode, f10RequestTime));
                    
                    // 启动超时定时器（30秒后清除请求标记）
                    if (f10TimeoutTimer == null)
                    {
                        f10TimeoutTimer = new System.Windows.Forms.Timer();
                        f10TimeoutTimer.Interval = 30000;  // 30秒
                        f10TimeoutTimer.Tick += (s, args) => {
                            if (!string.IsNullOrEmpty(pendingF10StockCode))
                            {
                                TimeSpan elapsed = DateTime.Now - f10RequestTime;
                                if (elapsed.TotalSeconds >= 30)
                                {
                                    Logger.Instance.Warning(string.Format("F10请求超时（30秒），清除请求标记: {0}", pendingF10StockCode));
                                    pendingF10StockCode = "";
                                    f10RequestTime = DateTime.MinValue;
                                    f10TimeoutTimer.Stop();
                                }
                            }
                            else
                            {
                                f10TimeoutTimer.Stop();
                            }
                        };
                    }
                    f10TimeoutTimer.Start();
                    
                    // 请求F10数据
                    if (!isReceivingData)
                    {
                        pendingF10StockCode = "";  // 清除请求标记
                        f10RequestTime = DateTime.MinValue;
                        if (f10TimeoutTimer != null)
                        {
                            f10TimeoutTimer.Stop();
                        }
                        MessageBox.Show("请先点击'开启接收数据'按钮", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (dataCollector == null)
                    {
                        pendingF10StockCode = "";  // 清除请求标记
                        f10RequestTime = DateTime.MinValue;
                        if (f10TimeoutTimer != null)
                        {
                            f10TimeoutTimer.Stop();
                        }
                        Logger.Instance.Error("数据采集器未初始化");
                        MessageBox.Show("数据采集器未初始化，请先连接DLL", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    bool success = dataCollector.RequestStockBaseData(normalizedCode);
                    if (success)
                    {
                        Logger.Instance.Success(string.Format("已请求股票 {0} 的F10数据，等待数据返回...", normalizedCode));
                    }
                    else
                    {
                        pendingF10StockCode = "";  // 清除请求标记
                        f10RequestTime = DateTime.MinValue;
                        if (f10TimeoutTimer != null)
                        {
                            f10TimeoutTimer.Stop();
                        }
                        Logger.Instance.Error(string.Format("请求F10数据失败: {0}", normalizedCode));
                        MessageBox.Show("请求F10数据失败，请检查DLL是否支持AskStockBase函数", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                pendingF10StockCode = "";  // 清除请求标记
                Logger.Instance.Error(string.Format("双击股票行异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                MessageBox.Show(string.Format("双击股票行失败: {0}\n\n详细错误请查看日志。", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        */

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        private void InitializeLogger()
        {
            Logger.Instance.SetLogTextBox(txtLog);
            // SetFilterDailyDataOnly方法已删除
            
            // 启用文件日志输出（日志文件保存在应用程序目录下的Logs文件夹）
            try
            {
                Logger.Instance.EnableFileLogging(null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("启用文件日志失败: {0}", ex.Message));
            }
            
            Logger.Instance.Info("系统启动");
            Logger.Instance.Info("日志系统初始化完成（已启用文件输出）");
        }

        /// <summary>
        /// 初始化刷新定时器 - 已禁用（UI组件已删除）
        /// </summary>
        private void InitializeRefreshTimer()
        {
            // refreshTimer已删除，功能已禁用
        }
        
        /// <summary>
        /// 初始化布局刷新定时器 - 已禁用（UI组件已删除）
        /// </summary>
        private void InitializeLayoutRefreshTimer()
        {
            // layoutRefreshTimer已删除，功能已禁用
        }

        // 数据接收监控定时器
        private System.Windows.Forms.Timer dataReceiveMonitorTimer;
        private DateTime lastDataReceiveTime = DateTime.MinValue;

        /// <summary>
        /// 初始化数据管理器
        /// </summary>
        private void InitializeDataManager()
        {
            // 创建数据队列（用于异步处理，避免阻塞回调函数）
            // 注意：队列在窗体初始化时创建，但只有在连接DLL后才启动处理线程
            dataQueue = new DataQueue(dataReceiver);
            
            // 初始化日线数据处理器（PostgreSQL）
            InitializeDailyDataProcessor();
            
            // F10功能已禁用
            // // 初始化F10数据管理器
            // f10DataManager = new F10DataManager(dataCollector);
            // f10DataManager.F10DataUpdated += F10DataManager_F10DataUpdated;
            
            // 订阅数据接收事件
            dataReceiver.StockDataReceived += DataReceiver_StockDataReceived;
            dataReceiver.MarketTableReceived += DataReceiver_MarketTableReceived;
            // 暂时不需要分钟数据
            // dataReceiver.MinuteDataReceived += DataReceiver_MinuteDataReceived;
            // dataReceiver.Minute5DataReceived += DataReceiver_Minute5DataReceived;
            // F10功能已禁用
            // dataReceiver.StockBaseDataReceived += DataReceiver_StockBaseDataReceived;
            
            // 订阅数据更新事件
            dataManager.DataUpdated += DataManager_DataUpdated;
            
            // 订阅数据采集器事件
            dataCollector.CollectionStatusChanged += DataCollector_CollectionStatusChanged;
            dataCollector.BasicDataCollected += DataCollector_BasicDataCollected;
            // 暂时不需要分钟数据
            // dataCollector.MinuteDataCollected += DataCollector_MinuteDataCollected;
            
            // 初始化数据接收监控定时器
            InitializeDataReceiveMonitor();
        }
        
        /// <summary>
        /// 初始化日线数据处理器（使用MQ发送到宿主机）
        /// </summary>
        private void InitializeDailyDataProcessor()
        {
            try
            {
                // 从配置文件读取MQ配置
                MQConfig mqConfig = MQConfig.LoadFromConfig();
                
                // 如果配置无效或未启用，则不初始化
                if (!mqConfig.IsValid() || !mqConfig.Enabled)
                {
                    Logger.Instance.Info("MQ发送功能未启用或配置无效，跳过初始化");
                    dailyDataProcessorMQ = null;
                    realTimeDataProcessorMQ = null;
                    exRightsDataProcessorMQ = null;
                    marketTableDataProcessorMQ = null;
                    return;
                }
                
                // 创建MQ统计对象
                mqStatistics = new MQStatistics();
                
                // 创建日线数据MQ处理器
                dailyDataProcessorMQ = new DailyDataProcessorMQ(mqConfig);
                dailyDataProcessorMQ.SetStatistics(mqStatistics);
                
                // 创建实时数据MQ处理器（使用相同的MQ配置）
                realTimeDataProcessorMQ = new RealTimeDataProcessorMQ(mqConfig);
                realTimeDataProcessorMQ.SetStatistics(mqStatistics);
                
                // 创建除权数据MQ处理器（使用相同的MQ配置）
                exRightsDataProcessorMQ = new ExRightsDataProcessorMQ(mqConfig);
                exRightsDataProcessorMQ.SetStatistics(mqStatistics);
                
                // 创建码表数据MQ处理器（使用相同的MQ配置，但使用码表队列名称）
                MQConfig marketTableConfig = new MQConfig
                {
                    Host = mqConfig.Host,
                    Port = mqConfig.Port,
                    QueueName = mqConfig.MarketTableQueueName,
                    Enabled = mqConfig.Enabled,
                    ConnectTimeout = mqConfig.ConnectTimeout,
                    SendTimeout = mqConfig.SendTimeout
                };
                marketTableDataProcessorMQ = new MarketTableDataProcessorMQ(marketTableConfig);
                marketTableDataProcessorMQ.SetStatistics(mqStatistics);
                
                // 测试MQ连接（改为独立测试，每个成功的都设置处理器）
                Logger.Instance.Info(string.Format("========== 开始测试MQ连接: {0}:{1} ==========", mqConfig.Host, mqConfig.Port));

                // 日线数据连接测试
                bool dailyConnected = false;
                try
                {
                    dailyConnected = dailyDataProcessorMQ.TestConnection();
                    Logger.Instance.Info(string.Format("日线数据MQ连接测试: {0}", dailyConnected ? "成功" : "失败"));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("日线数据MQ连接测试异常: {0}", ex.Message));
                }

                // 实时数据连接测试
                bool realtimeConnected = false;
                try
                {
                    realtimeConnected = realTimeDataProcessorMQ.TestConnection();
                    Logger.Instance.Info(string.Format("实时数据MQ连接测试: {0}", realtimeConnected ? "成功" : "失败"));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("实时数据MQ连接测试异常: {0}", ex.Message));
                }

                // 除权数据连接测试
                bool exRightsConnected = false;
                try
                {
                    exRightsConnected = exRightsDataProcessorMQ.TestConnection();
                    Logger.Instance.Info(string.Format("除权数据MQ连接测试: {0}", exRightsConnected ? "成功" : "失败"));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("除权数据MQ连接测试异常: {0}", ex.Message));
                }

                // 码表数据连接测试
                bool marketTableConnected = false;
                try
                {
                    marketTableConnected = marketTableDataProcessorMQ.TestConnection();
                    Logger.Instance.Info(string.Format("码表数据MQ连接测试: {0}", marketTableConnected ? "成功" : "失败"));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("码表数据MQ连接测试异常: {0}", ex.Message));
                }

                Logger.Instance.Info(string.Format("========== MQ连接测试完成: 日线={0}, 实时={1}, 除权={2}, 码表={3} ==========",
                    dailyConnected, realtimeConnected, exRightsConnected, marketTableConnected));

                // 设置所有处理器到数据队列（即使连接测试失败也设置，让发送器在实际发送时尝试连接）
                // 这样可以利用MQ发送器的自动重连机制
                if (dataQueue != null)
                {
                    // 日线数据处理器：即使连接测试失败也设置，让它在实际发送时尝试连接
                    if (dailyDataProcessorMQ != null)
                    {
                        dataQueue.SetDailyDataProcessorMQ(dailyDataProcessorMQ);
                        if (dailyConnected)
                        {
                            Logger.Instance.Success(string.Format("日线数据MQ处理器已设置: {0}:{1}/daily_data_queue (连接测试成功)", mqConfig.Host, mqConfig.Port));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("日线数据MQ处理器已设置: {0}:{1}/daily_data_queue (连接测试失败，将在实际发送时尝试连接)", mqConfig.Host, mqConfig.Port));
                        }
                    }

                    // 实时数据处理器
                    if (realTimeDataProcessorMQ != null)
                    {
                        realTimeDataProcessorMQ.Start();
                        dataQueue.SetRealTimeDataProcessorMQ(realTimeDataProcessorMQ);
                        if (realtimeConnected)
                        {
                            Logger.Instance.Success(string.Format("实时数据MQ处理器已设置: {0}:{1}/realtime_data_queue (连接测试成功)", mqConfig.Host, mqConfig.Port));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("实时数据MQ处理器已设置: {0}:{1}/realtime_data_queue (连接测试失败，将在实际发送时尝试连接)", mqConfig.Host, mqConfig.Port));
                        }
                    }

                    // 除权数据处理器
                    if (exRightsDataProcessorMQ != null)
                    {
                        dataQueue.SetExRightsDataProcessorMQ(exRightsDataProcessorMQ);
                        if (exRightsConnected)
                        {
                            Logger.Instance.Success(string.Format("除权数据MQ处理器已设置: {0}:{1}/ex_rights_data_queue (连接测试成功)", mqConfig.Host, mqConfig.Port));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("除权数据MQ处理器已设置: {0}:{1}/ex_rights_data_queue (连接测试失败，将在实际发送时尝试连接)", mqConfig.Host, mqConfig.Port));
                        }
                    }

                    // 码表数据处理器（不需要设置到DataQueue，在MarketTableReceived事件中直接调用）
                    if (marketTableDataProcessorMQ != null)
                    {
                        if (marketTableConnected)
                        {
                            Logger.Instance.Success(string.Format("码表数据MQ处理器已设置: {0}:{1}/{2} (连接测试成功)", mqConfig.Host, mqConfig.Port, mqConfig.MarketTableQueueName));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("码表数据MQ处理器已设置: {0}:{1}/{2} (连接测试失败，将在实际发送时尝试连接)", mqConfig.Host, mqConfig.Port, mqConfig.MarketTableQueueName));
                        }
                    }
                }
                else
                {
                    // 如果dataQueue为null，记录警告
                    Logger.Instance.Warning("数据队列未初始化，无法设置MQ处理器");
                    Logger.Instance.Warning("请确保在初始化MQ处理器之前已创建数据队列");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("初始化数据MQ处理器失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                dailyDataProcessorMQ = null;
                realTimeDataProcessorMQ = null;
                exRightsDataProcessorMQ = null;
                marketTableDataProcessorMQ = null;
            }
        }
        
        /// <summary>
        /// 初始化数据接收监控定时器
        /// </summary>
        private void InitializeDataReceiveMonitor()
        {
            dataReceiveMonitorTimer = new System.Windows.Forms.Timer();
            dataReceiveMonitorTimer.Interval = 5000;  // 每5秒检查一次
            dataReceiveMonitorTimer.Tick += DataReceiveMonitorTimer_Tick;
            dataReceiveMonitorTimer.Start();
        }
        
        /// <summary>
        /// 数据接收监控定时器事件（检测数据是否停止接收）
        /// </summary>
        private void DataReceiveMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!isReceivingData)
                return;
                
            var stats = dataReceiver.Statistics;
            DateTime now = DateTime.Now;
            
            // 检查内存使用情况
            long memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024;  // MB
            int codeTableSize = dataManager.GetStockCodeCount();
            string cacheStats = dataManager.GetCacheStatistics();
            
            // 如果超过10秒没有新数据，记录警告
            if (stats.LastUpdateTime != DateTime.MinValue && 
                (now - stats.LastUpdateTime).TotalSeconds > 10)
            {
                Logger.Instance.Warning(string.Format("数据接收可能已停止: 最后更新时间 {0:HH:mm:ss}, 当前时间 {1:HH:mm:ss}, 已停止 {2:F1} 秒", stats.LastUpdateTime, now, (now - stats.LastUpdateTime).TotalSeconds));
                Logger.Instance.Warning(string.Format("当前统计: 已接收 {0} 条数据包, 已处理 {1} 只股票", stats.TotalPacketsReceived, stats.TotalStocksProcessed));
                Logger.Instance.Warning(string.Format("内存使用: {0} MB, {1}", memoryUsed, cacheStats));
            }
            
            // 每1000条数据记录一次内存使用情况（用于调试内存问题）
            if (stats.TotalPacketsReceived > 0 && stats.TotalPacketsReceived % 1000 == 0)
            {
                Logger.Instance.Info(string.Format("内存状态: 使用 {0} MB, {1}, 已接收 {2} 条数据包", memoryUsed, cacheStats, stats.TotalPacketsReceived));
            }
            
            // 如果内存使用超过500MB，记录警告
            if (memoryUsed > 500)
            {
                Logger.Instance.Warning(string.Format("内存使用较高: {0} MB, 建议检查是否有内存泄漏", memoryUsed));
            }
        }

        /// <summary>
        /// 数据接收事件处理（实时行情数据）
        /// </summary>
        private void DataReceiver_StockDataReceived(object sender, StockDataEventArgs e)
        {
            // 更新最后接收时间
            lastDataReceiveTime = DateTime.Now;
            
            // 验证实时数据是否有效
            if (e.StockData == null)
            {
                Logger.Instance.Warning("收到空的实时数据事件");
                return;
            }
            
            // 前10次详细记录，之后每100条记录一次
            var stats = dataReceiver.Statistics;
            bool isDetailedLog = stats.TotalPacketsReceived <= 10;
            
            if (isDetailedLog)
            {
                Logger.Instance.Info(string.Format("DataReceiver_StockDataReceived: Code={0}, Name={1}, Price={2}, 累计接收={3}", e.StockData.Code, e.StockData.Name, e.StockData.NewPrice, stats.TotalPacketsReceived));
            }
            // 减少日志频率：每1000条记录一次（避免频繁创建字符串对象）
            else if (stats.TotalStocksProcessed % 1000 == 0)
            {
                Logger.Instance.Debug(string.Format("处理实时数据: 已处理 {0} 条", stats.TotalStocksProcessed));
            }
            
            // 实时数据中包含代码和名称，先更新基础代码表
            if (!string.IsNullOrEmpty(e.StockData.Code) && !string.IsNullOrEmpty(e.StockData.Name))
            {
                dataManager.UpdateStockCodeTable(e.StockData.Code, e.StockData.Name);
            }
            
            // 然后更新实时数据（价格、涨跌幅、成交量等）
            dataManager.UpdateStockData(e.StockData);
        }

        /// <summary>
        /// 码表数据接收事件处理
        /// </summary>
        private void DataReceiver_MarketTableReceived(object sender, MarketTableEventArgs e)
        {
            // 更新股票代码字典
            int beforeCount = dataManager.GetStockCodeCount();
            dataManager.UpdateStockCodeDictionary(e.CodeDictionary);
            int afterCount = dataManager.GetStockCodeCount();
            
            // 记录码表更新信息
            var stats = dataReceiver.Statistics;
            Logger.Instance.Info(string.Format("码表已更新: 本次新增 {0} 只股票, 码表总数从 {1} 增加到 {2}, 累计接收 {3} 个码表数据包", e.CodeDictionary.Count, beforeCount, afterCount, stats.MarketTableCount));
            
            // 发送码表数据到MQ
            if (marketTableDataProcessorMQ != null && e.CodeDictionary != null && e.CodeDictionary.Count > 0)
            {
                try
                {
                    marketTableDataProcessorMQ.ProcessMarketTableData(e.CodeDictionary);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("发送码表数据到MQ失败: {0}", ex.Message));
                }
            }
            
            // isBasicDataLoaded已删除，不再标记基本数据加载状态
            if (afterCount > 100)  // 至少加载100只股票才认为基本数据已加载
            {
                Logger.Instance.Success(string.Format("基本数据加载完成: 已加载 {0} 只股票", afterCount));
            }
            
            // 更新界面提示和数据显示
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => {
                    UpdateCurrentBoardLabel();
                    RefreshStockDataDisplay();
                }));
            }
            else
            {
                UpdateCurrentBoardLabel();
                RefreshStockDataDisplay();
            }
            
            /* // F10功能已禁用
            // 自动同步F10数据：当码表数据接收完成后，自动请求F10数据
            if (f10DataManager != null && isReceivingData)
            {
                // 获取所有股票代码列表
                var stockCodeDict = dataManager.GetStockCodeDictionary();
                if (stockCodeDict != null && stockCodeDict.Count > 0)
                {
                    List<string> stockCodes = new List<string>(stockCodeDict.Keys);
                    Logger.Instance.Info(string.Format("码表数据接收完成，开始自动同步F10数据: 共 {0} 只股票", stockCodes.Count));

                    // 批量请求F10数据（使用较小的批次和延迟，避免对服务器造成压力）
                    f10DataManager.BatchRequestF10Data(stockCodes, batchSize: 10, delayMs: 1000);
                }
            }
            */
        }

        /* // F10功能已禁用
        /// <summary>
        /// F10数据管理器数据更新事件处理
        /// </summary>
        private void F10DataManager_F10DataUpdated(object sender, F10DataManager.F10DataUpdatedEventArgs e)
        {
            // 当F10数据从缓存加载时，可以在这里处理
            Logger.Instance.Debug(string.Format("F10数据更新事件: 股票={0}, 来自缓存={1}", e.StockCode, e.FromCache));
        }
        */
        
        /// <summary>
        /// 数据管理器数据更新事件处理
        /// </summary>
        private void DataManager_DataUpdated(object sender, EventArgs e)
        {
            // needRefresh已删除，不再标记需要刷新UI
            
            // 如果表格已显示数据，则刷新表格显示（实时更新行情数据）
            if (dgvStockData.DataSource != null)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        RefreshStockDataDisplay();
                    }));
                }
                else
                {
                    RefreshStockDataDisplay();
                }
            }
        }

        /// <summary>
        /// 分钟数据接收事件处理（暂时不需要，已禁用）
        /// </summary>
        private void DataReceiver_MinuteDataReceived(object sender, MinuteDataEventArgs e)
        {
            // 暂时不需要分钟数据，此方法保留但不处理
            // 存储到分钟数据管理器
            // minuteDataManager.AddMinuteDataBatch(e.StockCode, e.MinuteDataList);
            
            // 通知采集器
            // dataCollector.NotifyMinuteDataCollected(e.StockCode, e.MinuteDataList);
            
            // 只记录日志，不自动弹窗（避免频繁弹窗干扰用户）
            // Logger.Instance.Info(string.Format("已接收股票 {0} 的 {1} 条分钟数据", e.StockCode, e.MinuteDataList.Count));
        }

        /// <summary>
        /// 5分钟数据接收事件处理（暂时不需要，已禁用）
        /// </summary>
        private void DataReceiver_Minute5DataReceived(object sender, Minute5DataEventArgs e)
        {
            // 暂时不需要分钟数据，此方法保留但不处理
            // 存储到分钟数据管理器
            // foreach (var data in e.Minute5DataList)
            // {
            //     minuteDataManager.AddMinute5Data(e.StockCode, data);
            // }
        }

        /* // F10功能已禁用
        /// <summary>
        /// F10数据接收事件处理
        /// </summary>
        private void DataReceiver_StockBaseDataReceived(object sender, StockBaseDataEventArgs e)
        {
            Logger.Instance.Info(string.Format("收到F10数据: 股票={0}, 文件名={1}, 内容长度={2} 字符", e.StockCode, e.FileName, e.Content.Length ?? 0));

            // 自动保存F10数据到本地缓存（无论是否匹配用户请求）
            // 使用异步方式保存，避免阻塞事件处理线程
            if (f10DataManager != null && !string.IsNullOrEmpty(e.Content))
            {
                string stockCode = e.StockCode;
                if (string.IsNullOrEmpty(stockCode) && !string.IsNullOrEmpty(e.FileName))
                {
                    // 如果股票代码为空，尝试从文件名提取
                    stockCode = System.IO.Path.GetFileNameWithoutExtension(e.FileName);
                }

                if (!string.IsNullOrEmpty(stockCode))
                {
                    // 创建副本，避免闭包问题
                    string stockCodeCopy = stockCode;
                    string contentCopy = e.Content;
                    string fileNameCopy = e.FileName;

                    // 使用线程池异步保存，不阻塞事件处理
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            f10DataManager.SaveF10Data(stockCodeCopy, contentCopy, fileNameCopy);
                        }
                        catch (Exception saveEx)
                        {
                            Logger.Instance.Warning(string.Format("异步保存F10数据失败: {0}, 错误: {1}", stockCodeCopy, saveEx.Message));
                        }
                    });
                }
            }

            // 检查是否有待处理的F10请求，且收到的数据是否匹配
            // 重要：只有在有明确的待处理请求时才显示F10数据，确保只显示选中股票的F10数据
            if (!string.IsNullOrEmpty(pendingF10StockCode))
            {
                bool isMatch = false;
                string matchedCode = pendingF10StockCode;

                // 标准化收到的股票代码，用于比较
                string receivedStockCode = DataConverter.NormalizeStockCode(e.StockCode ?? "");

                // 计算请求到现在的耗时
                TimeSpan elapsed = f10RequestTime != DateTime.MinValue ? DateTime.Now - f10RequestTime : TimeSpan.Zero;

                // 方法1: 检查股票代码是否精确匹配（优先使用此方法）
                if (!string.IsNullOrEmpty(receivedStockCode))
                {
                    isMatch = receivedStockCode.Equals(pendingF10StockCode, StringComparison.OrdinalIgnoreCase);
                    if (isMatch)
                    {
                        Logger.Instance.Debug(string.Format("F10数据匹配（通过股票代码）: 请求={0}, 收到={1}, 耗时={2:F0}ms", pendingF10StockCode, receivedStockCode, elapsed.TotalMilliseconds));
                    }
                }

                // 方法2: 如果股票代码为空，尝试从文件名提取并匹配
                if (!isMatch && string.IsNullOrEmpty(receivedStockCode) && !string.IsNullOrEmpty(e.FileName))
                {
                    string fileNameCode = DataConverter.NormalizeStockCode(
                        System.IO.Path.GetFileNameWithoutExtension(e.FileName));
                    if (!string.IsNullOrEmpty(fileNameCode))
                    {
                        isMatch = fileNameCode.Equals(pendingF10StockCode, StringComparison.OrdinalIgnoreCase);
                        if (isMatch)
                        {
                            Logger.Instance.Debug(string.Format("F10数据匹配（通过文件名）: 请求={0}, 文件名={1}, 耗时={2:F0}ms", pendingF10StockCode, e.FileName, elapsed.TotalMilliseconds));
                            matchedCode = fileNameCode;
                        }
                    }
                }

                // 如果仍然不匹配，记录详细信息用于调试
                if (!isMatch)
                {
                    Logger.Instance.Debug(string.Format("F10数据不匹配（忽略）: 请求={0}, 收到代码={1}, 文件名={2}, 耗时={3:F1}秒", pendingF10StockCode, receivedStockCode, e.FileName, elapsed.TotalSeconds));

                    // 如果超时（超过10秒），记录警告
                    if (elapsed.TotalSeconds >= 10)
                    {
                        Logger.Instance.Warning(string.Format("F10数据等待超时（10秒），但收到不匹配的数据: 请求={0}, 收到={1}", pendingF10StockCode, receivedStockCode ?? e.FileName));
                    }

                    // 不匹配的数据直接忽略，不显示
                    return;
                }

                // 匹配成功，清除请求标记并显示数据
                pendingF10StockCode = "";  // 清除请求标记，避免重复显示
                f10RequestTime = DateTime.MinValue;  // 清除请求时间

                // 停止超时定时器
                if (f10TimeoutTimer != null)
                {
                    f10TimeoutTimer.Stop();
                }

                Logger.Instance.Success(string.Format("F10数据匹配成功: {0}，耗时={1:F0}ms，准备显示", matchedCode, elapsed.TotalMilliseconds));

                // 显示F10数据（只显示匹配的股票数据）
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => {
                        try
                        {
                            DisplayStockBaseData(e, matchedCode);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error(string.Format("显示F10数据异常: {0}", ex.Message));
                            Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                        }
                    }));
                }
                else
                {
                    try
                    {
                        DisplayStockBaseData(e, matchedCode);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("显示F10数据异常: {0}", ex.Message));
                        Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    }
                }
            }
            else
            {
                // 没有待处理的请求，可能是自动推送的数据或其他股票的F10数据，记录但不显示
                // 这确保了只有用户明确请求的股票的F10数据才会显示
                Logger.Instance.Debug("收到F10数据但无待处理请求（忽略）: 股票={e.StockCode ?? "未知"}，文件名={e.FileName ?? "未知"}，不显示");
            }
        }
        */

        /* // F10功能已禁用
        /// <summary>
        /// 显示F10数据
        /// </summary>
        private void DisplayStockBaseData(StockBaseDataEventArgs e, string stockCode = null)
        {
            try
            {
                if (e == null)
                {
                    Logger.Instance.Error("F10数据事件参数为空");
                    MessageBox.Show("F10数据为空，无法显示", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 使用传入的股票代码，如果没有则使用事件中的股票代码
                string displayStockCode = stockCode ?? e.StockCode ?? "未知";
                string normalizedCode = DataConverter.NormalizeStockCode(displayStockCode);

                // 优先从缓存读取F10数据（如果缓存存在且较新）
                string content = e.Content ?? "";

                if (f10DataManager != null && string.IsNullOrEmpty(content))
                {
                    string cachedContent = f10DataManager.LoadF10DataFromCache(normalizedCode);
                    if (!string.IsNullOrEmpty(cachedContent))
                    {
                        content = cachedContent;
                        Logger.Instance.Info(string.Format("从缓存加载F10数据: {0}", normalizedCode));
                    }
                }

                // 如果仍然为空，显示提示信息
                if (string.IsNullOrEmpty(content))
                {
                    Logger.Instance.Warning(string.Format("F10数据内容为空: 股票={0}, 文件名={1}", normalizedCode, e.FileName));
                    content = string.Format("[F10数据内容为空]\n\n股票代码: {0}\n文件名: {e.FileName ?? ", normalizedCode)未知"}\n\n可能原因：\n1. 数据尚未完全接收\n2. 该股票暂无F10数据\n3. 数据格式异常\n\n提示：F10数据正在后台自动同步，请稍后再试";
                }

                Logger.Instance.Info(string.Format("准备显示F10数据: 股票={0}, 内容长度={1} 字符", displayStockCode, content.Length));

                // 在主窗口的F10面板中显示数据
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        DisplayF10DataInPanel(displayStockCode, content, e.FileName);
                    }));
                }
                else
                {
                    DisplayF10DataInPanel(displayStockCode, content, e.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("显示F10数据异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// 在F10面板中显示数据（只显示指定股票的F10数据）
        /// </summary>
        private void DisplayF10DataInPanel(string stockCode, string content, string fileName)
        {
            try
            {
                // 验证股票代码不为空
                if (string.IsNullOrEmpty(stockCode))
                {
                    Logger.Instance.Warning("F10面板显示：股票代码为空，使用默认值");
                    stockCode = "未知股票";
                }

                // 更新F10面板标题，明确显示当前查看的股票代码
                lblF10Title.Text = string.Format("F10个股资料 - {0} ({fileName ?? ", stockCode)未知文件"})";

                // 确保F10面板可见
                if (!panelF10.Visible)
                {
                    menuItemShowF10Panel.Checked = true;
                    panelF10.Visible = true;
                    splitContainerRight.Panel1Collapsed = false;
                }

                // 对于大文件，使用异步方式设置文本，避免阻塞UI
                if (content.Length > 100000)  // 大于100KB时使用异步加载
                {
                    txtF10Content.Text = "正在加载F10数据，请稍候...";

                    // 异步加载内容（使用 ThreadPool，兼容 .NET Framework 3.5）
                    string contentCopy = content;  // 创建副本，避免闭包问题
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            if (this != null && !this.IsDisposed && txtF10Content != null && !txtF10Content.IsDisposed)
                            {
                                if (this.InvokeRequired)
                                {
                                    this.Invoke(new Action(() =>
                                    {
                                        try
                                        {
                                            if (!txtF10Content.IsDisposed && this != null && !this.IsDisposed)
                                            {
                                                txtF10Content.Text = contentCopy;
                                                Logger.Instance.Info(string.Format("F10内容已异步加载完成: {0} 字符", contentCopy.Length));
                                            }
                                        }
                                        catch (Exception invokeEx)
                                        {
                                            Logger.Instance.Warning(string.Format("Invoke设置F10内容失败: {0}", invokeEx.Message));
                                        }
                                    }));
                                }
                                else
                                {
                                    if (!txtF10Content.IsDisposed)
                                    {
                                        txtF10Content.Text = contentCopy;
                                    }
                                }
                            }
                        }
                        catch (Exception loadEx)
                        {
                            Logger.Instance.Warning(string.Format("异步加载F10内容失败: {0}", loadEx.Message));
                        }
                    });
                }
                else
                {
                    // 小文件直接设置
                    txtF10Content.Text = content;
                }

                Logger.Instance.Success(string.Format("F10数据已显示在面板中: 股票={0}", stockCode));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("在F10面板中显示数据异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// 显示F10数据（保留旧方法用于兼容，但改为调用新方法）
        /// </summary>
        private void DisplayStockBaseDataOld(StockBaseDataEventArgs e, string stockCode = null)
        {
            try
            {
                if (e == null)
                {
                    Logger.Instance.Error("F10数据事件参数为空");
                    MessageBox.Show("F10数据为空，无法显示", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 使用传入的股票代码，如果没有则使用事件中的股票代码
                string displayStockCode = stockCode ?? e.StockCode ?? "未知";
                string normalizedCode = DataConverter.NormalizeStockCode(displayStockCode);

                // 优先从缓存读取F10数据（如果缓存存在且较新）
                string content = e.Content ?? "";

                if (f10DataManager != null && string.IsNullOrEmpty(content))
                {
                    string cachedContent = f10DataManager.LoadF10DataFromCache(normalizedCode);
                    if (!string.IsNullOrEmpty(cachedContent))
                    {
                        content = cachedContent;
                        Logger.Instance.Info(string.Format("从缓存加载F10数据: {0}", normalizedCode));
                    }
                }

                // 如果仍然为空，显示提示信息
                if (string.IsNullOrEmpty(content))
                {
                    Logger.Instance.Warning(string.Format("F10数据内容为空: 股票={0}, 文件名={1}", normalizedCode, e.FileName));
                    content = string.Format("[F10数据内容为空]\n\n股票代码: {0}\n文件名: {e.FileName ?? ", normalizedCode)未知"}\n\n可能原因：\n1. 数据尚未完全接收\n2. 该股票暂无F10数据\n3. 数据格式异常\n\n提示：F10数据正在后台自动同步，请稍后再试";
                }

                // 在主窗口的F10面板中显示数据
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        DisplayF10DataInPanel(displayStockCode, content, e.FileName);
                    }));
                }
                else
                {
                    DisplayF10DataInPanel(displayStockCode, content, e.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("显示F10数据异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// 旧版F10显示方法（保留用于兼容）
        /// </summary>
        private void DisplayStockBaseDataOldWindow(StockBaseDataEventArgs e, string stockCode = null)
        {
            try
            {
                if (e == null)
                {
                    Logger.Instance.Error("F10数据事件参数为空");
                    MessageBox.Show("F10数据为空，无法显示", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 使用传入的股票代码，如果没有则使用事件中的股票代码
                string displayStockCode = stockCode ?? e.StockCode ?? "未知";
                string normalizedCode = DataConverter.NormalizeStockCode(displayStockCode);

                // 优先从缓存读取F10数据（如果缓存存在且较新）
                string content = e.Content ?? "";

                if (f10DataManager != null && string.IsNullOrEmpty(content))
                {
                    string cachedContent = f10DataManager.LoadF10DataFromCache(normalizedCode);
                    if (!string.IsNullOrEmpty(cachedContent))
                    {
                        content = cachedContent;
                        Logger.Instance.Info(string.Format("从缓存加载F10数据: {0}", normalizedCode));
                    }
                }

                // 如果仍然为空，显示提示信息
                if (string.IsNullOrEmpty(content))
                {
                    Logger.Instance.Warning(string.Format("F10数据内容为空: 股票={0}, 文件名={1}", normalizedCode, e.FileName));
                    content = string.Format("[F10数据内容为空]\n\n股票代码: {0}\n文件名: {e.FileName ?? ", normalizedCode)未知"}\n\n可能原因：\n1. 数据尚未完全接收\n2. 该股票暂无F10数据\n3. 数据格式异常\n\n提示：F10数据正在后台自动同步，请稍后再试";
                }

                // 检查是否已有F10窗口打开
                if (isF10WindowOpen)
                {
                    Logger.Instance.Warning(string.Format("F10窗口已打开，忽略重复请求: {0}", displayStockCode));
                    return;
                }

                Logger.Instance.Info(string.Format("准备显示F10窗口: 股票={0}, 内容长度={1} 字符", displayStockCode, content.Length));

                // 标记F10窗口正在打开
                isF10WindowOpen = true;

                // 创建显示F10数据的窗口
                Form f10Form = null;
                try
                {
                    f10Form = new Form
                    {
                        Text = string.Format("F10个股资料 - {0} ({e.FileName ?? ", displayStockCode)未知文件"})",
                        Size = new System.Drawing.Size(800, 600),
                        StartPosition = FormStartPosition.CenterParent,
                        BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                        MinimumSize = new System.Drawing.Size(400, 300)
                    };

                // 优化：使用RichTextBox而不是TextBox，对于大文本性能更好
                RichTextBox txtContent = new RichTextBox
                {
                    Multiline = true,
                    ScrollBars = RichTextBoxScrollBars.Both,
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    Font = new System.Drawing.Font("Consolas", 9F),
                    BackColor = System.Drawing.Color.FromArgb(40, 40, 40),
                    ForeColor = System.Drawing.Color.White,
                    DetectUrls = false,  // 禁用URL检测，提高性能
                    WordWrap = true
                };

                // 优化：对于大文件，使用异步方式设置文本，避免阻塞UI
                if (content.Length > 100000)  // 大于100KB时使用异步加载
                {
                    txtContent.Text = "正在加载F10数据，请稍候...";
                    f10Form.Controls.Add(txtContent);

                    // 异步加载内容（使用 ThreadPool，兼容 .NET Framework 3.5）
                    string contentCopy = content;  // 创建副本，避免闭包问题
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            if (f10Form != null && !f10Form.IsDisposed && txtContent != null && !txtContent.IsDisposed)
                            {
                                if (f10Form.InvokeRequired)
                                {
                                    f10Form.Invoke(new Action(() =>
                                    {
                                        try
                                        {
                                            if (!txtContent.IsDisposed && f10Form != null && !f10Form.IsDisposed)
                                            {
                                                txtContent.Text = contentCopy;
                                                Logger.Instance.Info(string.Format("F10内容已异步加载完成: {0} 字符", contentCopy.Length));
                                            }
                                        }
                                        catch (Exception invokeEx)
                                        {
                                            Logger.Instance.Warning(string.Format("Invoke设置F10内容失败: {0}", invokeEx.Message));
                                        }
                                    }));
                                }
                                else
                                {
                                    if (!txtContent.IsDisposed)
                                    {
                                        txtContent.Text = contentCopy;
                                    }
                                }
                            }
                        }
                        catch (Exception loadEx)
                        {
                            Logger.Instance.Warning(string.Format("异步加载F10内容失败: {0}", loadEx.Message));
                        }
                    });
                }
                else
                {
                    // 小文件直接设置
                    txtContent.Text = content;
                    f10Form.Controls.Add(txtContent);
                }

                    // 添加窗口关闭事件，确保资源释放
                    f10Form.FormClosed += (sender, args) =>
                    {
                        try
                        {
                            isF10WindowOpen = false;  // 清除标记
                            if (f10Form != null)
                            {
                                f10Form.Controls.Clear();
                                Logger.Instance.Debug(string.Format("F10窗口资源已释放: 股票={0}", displayStockCode));
                            }
                        }
                        catch (Exception closeEx)
                        {
                            isF10WindowOpen = false;  // 确保标记被清除
                            Logger.Instance.Warning(string.Format("F10窗口关闭事件异常: {0}", closeEx.Message));
                        }
                    };

                    Logger.Instance.Success(string.Format("F10窗口已创建，准备显示: 股票={0}", displayStockCode));

                    // 使用ShowDialog而不是Show，避免窗口关闭时导致程序退出
                    if (this != null && !this.IsDisposed && this.IsHandleCreated)
                    {
                        f10Form.ShowDialog(this);
                    }
                    else
                    {
                        Logger.Instance.Warning("主窗体已关闭或无效，使用无父窗口方式显示F10数据");
                        f10Form.ShowDialog();
                    }

                    Logger.Instance.Info(string.Format("F10窗口已关闭: 股票={0}", displayStockCode));
                }
                catch (ObjectDisposedException disposedEx)
                {
                    Logger.Instance.Warning(string.Format("F10窗口显示失败（对象已释放）: {0}", disposedEx.Message));
                    if (f10Form != null && !f10Form.IsDisposed)
                    {
                        try
                        {
                            f10Form.ShowDialog();
                        }
                        catch (Exception retryEx)
                        {
                            Logger.Instance.Error(string.Format("F10窗口重试显示失败: {0}", retryEx.Message));
                        }
                    }
                }
                catch (InvalidOperationException invalidOpEx)
                {
                    Logger.Instance.Error(string.Format("F10窗口显示失败（无效操作）: {0}", invalidOpEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", invalidOpEx.StackTrace));
                }
                catch (AccessViolationException avEx)
                {
                    Logger.Instance.Error(string.Format("F10窗口显示失败（访问冲突）: {0}", avEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", avEx.StackTrace));
                    // 不抛出异常，避免程序退出
                }
                finally
                {
                    // 确保窗口资源被释放
                    isF10WindowOpen = false;  // 清除标记
                    if (f10Form != null)
                    {
                        try
                        {
                            if (!f10Form.IsDisposed)
                            {
                                f10Form.Dispose();
                            }
                        }
                        catch (Exception disposeEx)
                        {
                            Logger.Instance.Warning(string.Format("释放F10窗口资源失败: {0}", disposeEx.Message));
                        }
                        f10Form = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("显示F10数据异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                MessageBox.Show(string.Format("显示F10数据失败: {0}\n\n详细错误请查看日志。", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        */

        /// <summary>
        /// 采集器状态变化事件处理
        /// </summary>
        private void DataCollector_CollectionStatusChanged(object sender, CollectionStatusEventArgs e)
        {
            // 可以在这里更新UI状态显示
            System.Diagnostics.Debug.WriteLine(string.Format("采集状态: {0}", e.Message));
        }

        /// <summary>
        /// 基本数据采集完成事件处理
        /// </summary>
        private void DataCollector_BasicDataCollected(object sender, BasicDataCollectedEventArgs e)
        {
            // 基本数据（码表）已采集完成
            System.Diagnostics.Debug.WriteLine(string.Format("基本数据采集完成，共 {0} 只股票", e.CodeDictionary.Count));
        }

        /// <summary>
        /// 分钟数据采集完成事件处理（暂时不需要，已禁用）
        /// </summary>
        private void DataCollector_MinuteDataCollected(object sender, MinuteDataCollectedEventArgs e)
        {
            // 暂时不需要分钟数据，此方法保留但不处理
            // 分钟数据已采集完成
            // System.Diagnostics.Debug.WriteLine(string.Format("分钟数据采集完成: {0}，共 {1} 条数据", e.StockCode, e.MinuteDataList.Count));
        }

        // 定时器和布局刷新相关方法已删除（不再需要UI刷新）

        private void Form1_Load(object sender, EventArgs e)
        {
            Logger.Instance.Info("窗体加载完成");
            
            // 禁用所有界面操作
            EnableUI(false);
            
            // 设置窗体最大化
            this.WindowState = FormWindowState.Maximized;
            
            // 显示启动进度条并开始自动启动
            StartStartupProcess();
        }
        
        /// <summary>
        /// 启动启动流程
        /// </summary>
        private void StartStartupProcess()
        {
            // StartupProgressForm已删除，直接执行启动流程
            // progressForm = new StartupProgressForm();
            // progressForm.Show();
            // progressForm.UpdateProgress(0, "正在初始化系统...");
            
            // 在后台线程中执行启动流程
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    // 步骤1: 加载板块配置 (10%)
                    // this.Invoke(new Action(() =>
                    // {
                    //     progressForm.UpdateProgress(10, "正在加载板块配置...");
                    // }));
                    // this.Invoke(new Action(() => LoadBoards()));
                    System.Threading.Thread.Sleep(300);
                    
                    // 步骤2: 初始化菜单项状态 (20%)
                    this.Invoke(new Action(() =>
                    {
                        // progressForm.UpdateProgress(20, "正在初始化界面...");
                        if (menuItemStartReceive != null)
                        {
                            menuItemStartReceive.Enabled = false;  // 启动过程中禁用
                        }
                        if (menuItemStopReceive != null)
                        {
                            menuItemStopReceive.Enabled = false;
                        }
                    }));
                    System.Threading.Thread.Sleep(200);
                    
                    // 步骤3: 加载DLL并连接 (40%)
                    // this.Invoke(new Action(() =>
                    // {
                    //     progressForm.UpdateProgress(30, "正在加载驱动库...");
                    // }));
                    System.Threading.Thread.Sleep(200);
                    
                    // this.Invoke(new Action(() =>
                    // {
                    //     progressForm.UpdateProgress(40, "正在连接龙卷风软件...");
                    // }));
                    
                    // 在主线程中初始化连接
                    bool success = false;
                    this.Invoke(new Action(() =>
                    {
                        try
                        {
                            success = dataCollector.Initialize(this.Handle, null);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error(string.Format("初始化连接异常: {0}", ex.Message));
                            success = false;
                        }
                    }));
                    
                    if (!success)
                    {
                        this.Invoke(new Action(() =>
                        {
                            // progressForm.Close();
                            EnableUI(true);
                            MessageBox.Show("连接龙卷风软件失败！\n\n请检查：\n1. StockDrv.dll是否存在\n2. 龙卷风软件是否已启动\n3. 查看日志文件获取详细信息", 
                                "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                        return;
                    }
                    
                    // 步骤4: 启动数据接收 (60%)
                    this.Invoke(new Action(() =>
                    {
                        // progressForm.UpdateProgress(60, "正在启动数据接收...");
                        HandleInitializationResult(true);
                    }));
                    System.Threading.Thread.Sleep(500);
                    
                    // 步骤5: 等待基本数据加载 (60-90%)
                    // this.Invoke(new Action(() =>
                    // {
                    //     progressForm.UpdateProgress(70, "正在等待基本数据加载...");
                    // }));
                    
                    // 等待码表数据接收完成（最多等待30秒）
                    int waitCount = 0;
                    int maxWait = 300;  // 30秒 = 300 * 100ms
                    int codeCount = 0;
                    while (codeCount < 100 && waitCount < maxWait)  // 至少加载100只股票
                    {
                        System.Threading.Thread.Sleep(100);
                        waitCount++;
                        
                        // 每2秒更新一次进度（已禁用进度条）
                        // if (waitCount % 20 == 0)
                        // {
                        //     int progress = 70 + (waitCount * 20 / maxWait);  // 70-90之间
                        //     this.Invoke(new Action(() =>
                        //     {
                        //         int codeCount = dataManager.GetStockCodeCount();
                        //         progressForm.UpdateProgress(progress, string.Format("正在加载基本数据... (已加载 {0} 只股票)", codeCount));
                        //     }));
                        // }
                    }
                    
                    // 步骤6: 完成启动 (100%)
                    this.Invoke(new Action(() =>
                    {
                        // progressForm.UpdateProgress(100, "启动完成！");
                        System.Threading.Thread.Sleep(500);
                        // progressForm.Close();
                        // progressForm = null;
                        
                        // isStartupComplete已删除
                        EnableUI(true);
                        
                        Logger.Instance.Success("系统启动完成，界面已启用");
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("启动流程异常: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    this.Invoke(new Action(() =>
                    {
                        // if (progressForm != null)
                        // {
                        //     progressForm.Close();
                        //     progressForm = null;
                        // }
                        EnableUI(true);
                        MessageBox.Show(string.Format("启动过程发生错误: {0}", ex.Message), "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }
        
        /// <summary>
        /// 启用/禁用界面操作
        /// </summary>
        private void EnableUI(bool enable)
        {
            // 启用/禁用所有菜单项
            if (menuItemStartReceive != null)
            {
                menuItemStartReceive.Enabled = enable && !isReceivingData;
            }
            if (menuItemStopReceive != null)
            {
                menuItemStopReceive.Enabled = enable && isReceivingData;
            }
            
            // 启用/禁用其他控件
            this.Enabled = enable;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Instance.Info("=".PadRight(80, '='));
                Logger.Instance.Info("【程序退出】用户点击退出按钮");
                Logger.Instance.Info(string.Format("退出时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                Logger.Instance.Info("退出方式: Environment.Exit(0)");
                Logger.Instance.Info("程序状态: 强制退出");
                Logger.Instance.Info("=".PadRight(80, '='));
                
                // 先尝试正常关闭
                this.Close();
                
                // 如果正常关闭失败，强制退出
                System.Threading.Thread.Sleep(100);
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("退出按钮处理失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                System.Environment.Exit(1);  // 异常退出，使用非零退出码
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // isStartupComplete已删除，不再检查启动状态
            // isStartupComplete已删除，不再检查启动状态
            // return;
            
            Logger.Instance.Info("点击启动接收菜单（重新初始化连接）");
            
            // 禁用菜单项，防止重复点击
            if (menuItemStartReceive != null)
            {
                menuItemStartReceive.Enabled = false;
            }
            this.Cursor = Cursors.WaitCursor;
            
            // 注意：改为在主线程同步执行，与c212项目保持一致
            // 这样可以确保窗口句柄和消息处理在正确的线程中，特别是在远程桌面环境下
            try
            {
                // 在主线程中初始化连接（与c212项目一致）
                bool success = dataCollector.Initialize(this.Handle, null);
                HandleInitializationResult(success);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("初始化连接异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                this.Cursor = Cursors.Default;
                if (menuItemStartReceive != null)
                {
                    menuItemStartReceive.Enabled = true;
                }
                MessageBox.Show(string.Format("初始化连接失败: {0}\n\n请检查：\n1. StockDrv.dll是否存在\n2. 龙卷风软件是否已启动\n3. 查看日志文件获取详细信息", ex.Message), "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 处理初始化结果
        /// </summary>
        private void HandleInitializationResult(bool success)
        {
            this.Cursor = Cursors.Default;
            
            if (success)
            {
                Logger.Instance.Info("=== 开始处理初始化成功结果 ===");
                
                // 保存DLL句柄（用于后续操作）
                instance = 1; // 标记已连接
                Logger.Instance.Info("已设置连接标记: instance = 1");
                
                // 重置统计信息
                dataReceiver.ResetStatistics();
                Logger.Instance.Info("已重置数据接收统计信息");
                
                // 启动数据队列处理线程（异步处理数据，避免阻塞回调）
                if (dataQueue != null)
                {
                    dataQueue.Start();
                    Logger.Instance.Info("数据队列处理线程已启动");
                }
                else
                {
                    Logger.Instance.Warning("数据队列未初始化，无法启动处理线程");
                }
                
                // 连接成功后，数据接收会自动开始，所以直接设置状态
                dataCollector.StartCollectBasicData();
                isReceivingData = true;
                Logger.Instance.Info("已启动基本数据采集，数据接收状态已开启");
                
                /* // F10功能已禁用
                // 启用F10自动同步（24小时更新一次）
                if (f10DataManager != null)
                {
                    f10DataManager.EnableAutoSync(24);
                    Logger.Instance.Info("F10自动同步已启用，将在码表数据加载完成后自动同步F10数据");
                }
                else
                {
                    Logger.Instance.Warning("F10数据管理器未初始化");
                }
                */
                
                Logger.Instance.Success("DLL连接成功，数据接收已自动开启");
                Logger.Instance.Info("基本数据（股票名称/代码）将自动接收");
                Logger.Instance.Info("实时行情数据将自动接收");
                
                UpdateStatusLabel();
                
                // 更新菜单项状态
                if (menuItemStartReceive != null)
                {
                    menuItemStartReceive.Enabled = false;
                }
                if (menuItemStopReceive != null)
                {
                    menuItemStopReceive.Enabled = true;
                }
                
                Logger.Instance.Success("=== 初始化成功处理完成 ===");
                
                // isStartupComplete已删除，直接显示提示消息
                MessageBox.Show("已连接龙卷风软件，数据接收已开启！\n\n" +
                              "实时数据、日线数据、除权数据将自动同步到MQ。",
                              "连接成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Logger.Instance.Error("=== 初始化失败处理 ===");
                Logger.Instance.Error("连接失败，请检查以下内容：");
                Logger.Instance.Error("1. StockDrv.dll是否存在（检查注册表或程序目录）");
                Logger.Instance.Error("2. 龙卷风软件是否已启动");
                Logger.Instance.Error("3. 是否有足够的权限（可能需要管理员权限）");
                Logger.Instance.Error("4. 查看日志文件获取详细错误信息");
                Logger.Instance.Error(string.Format("日志文件位置: {0}", Logger.Instance.GetLogFilePath()));
                
                if (menuItemStartReceive != null)
                {
                    menuItemStartReceive.Enabled = true;
                }
                
                string errorMsg = "连接失败！\n\n" +
                                "请检查：\n" +
                                "1. StockDrv.dll是否存在\n" +
                                "2. 龙卷风软件是否已启动\n" +
                                "3. 是否有足够的权限\n\n" +
                                "详细错误信息请查看日志文件：\n" +
                                Logger.Instance.GetLogFilePath();
                
                MessageBox.Show(errorMsg, "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 开启接收数据按钮点击事件
        /// </summary>
        private void btnStartReceive_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("点击开启接收数据按钮");
            
            if (instance == 0)
            {
                Logger.Instance.Warning("请先点击'启动接收'按钮初始化连接");
                MessageBox.Show("请先点击'启动接收'按钮初始化连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (isReceivingData)
            {
                Logger.Instance.Warning("数据接收已开启，无需重复开启");
                MessageBox.Show("数据接收已开启", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 启动数据队列处理线程（如果还未启动）
                if (dataQueue == null)
                {
                    // 如果队列未创建（理论上不应该发生，因为InitializeDataManager中已创建），创建并启动
                    Logger.Instance.Warning("数据队列未创建，正在创建...");
                    dataQueue = new DataQueue(dataReceiver);
                    dataQueue.Start();
                    Logger.Instance.Info("数据队列已创建并启动");
                }
                else if (!dataQueue.IsRunning())
                {
                    // 如果队列已创建但未运行，启动它
                    dataQueue.Start();
                    Logger.Instance.Info("数据队列处理线程已启动");
                }
                
                // 开始采集基本数据（码表会自动接收）
                dataCollector.StartCollectBasicData();
                isReceivingData = true;

                Logger.Instance.Success("已开启数据接收");
                Logger.Instance.Info("基本数据（股票名称/代码）将自动接收");
                Logger.Instance.Info("实时行情数据将自动接收");

                // 请求日线数据（日线数据需要主动请求，不会自动推送）
                // 参数说明：pszStockCode 空字符串=全部股票, nTimePeriod: 1=周, 2=月, 3=全部
                Logger.Instance.Info("正在请求日线数据（全部股票，全部数据）...");
                bool dailyDataRequested = dataCollector.RequestDailyData("", 3);
                if (dailyDataRequested)
                {
                    Logger.Instance.Success("日线数据请求已发送");
                }
                else
                {
                    Logger.Instance.Warning("日线数据请求失败（AskStockDay函数可能不可用）");
                }
                
                UpdateStatusLabel();
                
                // 更新菜单项状态
                if (menuItemStartReceive != null)
                {
                    menuItemStartReceive.Enabled = false;
                }
                if (menuItemStopReceive != null)
                {
                    menuItemStopReceive.Enabled = true;
                }
                
                MessageBox.Show("已开启数据接收！\n\n" +
                              "1. 基本数据（股票名称/代码）将自动接收\n" +
                              "2. 实时行情数据将自动接收\n" +
                              "3. 点击'加载基础数据'查看码表数据状态\n" +
                              "4. 点击'加载分钟数据'加载指定股票的分钟数据", 
                              "接收已开启", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("开启数据接收失败: {0}", ex.Message));
                MessageBox.Show(string.Format("开启数据接收失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 窗体关闭事件处理
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 优化：减少日志输出，加快关闭速度
            try
            {
                Logger.Instance.Info("=== 程序开始退出，保存配置 ===");
                
                // 1. 停止所有定时器（防止内存泄漏）
                try
                {
                    // refreshTimer和layoutRefreshTimer已删除
                    // if (refreshTimer != null)
                    // {
                    //     refreshTimer.Stop();
                    //     refreshTimer.Dispose();
                    //     refreshTimer = null;
                    // }
                    // if (layoutRefreshTimer != null)
                    // {
                    //     layoutRefreshTimer.Stop();
                    //     layoutRefreshTimer.Dispose();
                    //     layoutRefreshTimer = null;
                    // }
                    if (dataReceiveMonitorTimer != null)
                    {
                        dataReceiveMonitorTimer.Stop();
                        dataReceiveMonitorTimer.Dispose();
                        dataReceiveMonitorTimer = null;
                    }
                    if (statusUpdateTimer != null)
                    {
                        statusUpdateTimer.Stop();
                        statusUpdateTimer.Dispose();
                        statusUpdateTimer = null;
                    }
                    // 清理f10TimeoutTimer（如果存在）
                    // 注意：f10TimeoutTimer被注释掉了，但为了安全，检查并清理
                    var f10TimerField = typeof(Form1).GetField("f10TimeoutTimer", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f10TimerField != null)
                    {
                        var f10Timer = f10TimerField.GetValue(this) as System.Windows.Forms.Timer;
                        if (f10Timer != null)
                        {
                            f10Timer.Stop();
                            f10Timer.Dispose();
                            f10TimerField.SetValue(this, null);
                        }
                    }
                }
                catch { }
                
                // 3. 取消事件订阅（防止内存泄漏）
                try
                {
                    if (dataReceiver != null)
                    {
                        dataReceiver.StockDataReceived -= DataReceiver_StockDataReceived;
                        dataReceiver.MarketTableReceived -= DataReceiver_MarketTableReceived;
                    }
                    if (dataManager != null)
                    {
                        dataManager.DataUpdated -= DataManager_DataUpdated;
                    }
                    if (dataCollector != null)
                    {
                        dataCollector.CollectionStatusChanged -= DataCollector_CollectionStatusChanged;
                        dataCollector.BasicDataCollected -= DataCollector_BasicDataCollected;
                    }
                }
                catch { }
                
                // 4. boardPanels已删除，不再需要清理
                // if (boardPanels != null)
                // {
                //     foreach (var panel in boardPanels)
                //     {
                //         try
                //         {
                //             if (panel != null && !panel.IsDisposed)
                //             {
                //                 panel.Dispose();
                //             }
                //         }
                //         catch { }
                //     }
                //     boardPanels.Clear();
                // }
                
                // 5. 清理DataManager资源（释放定时器）
                if (dataManager != null)
                {
                    try
                    {
                        dataManager.Cleanup();
                    }
                    catch { }
                }
                
                // 6. 停止数据队列（设置超时，避免长时间等待）
                if (dataQueue != null)
                {
                    try
                    {
                        dataQueue.Stop();
                    }
                    catch { }
                }
                
                // 7. 停止数据接收（重要：在窗体关闭前完成所有消息处理）
                if (isReceivingData && dataCollector != null)
                {
                    try
                    {
                        // 先处理完所有待处理的消息，避免在DLL卸载时还有未完成的消息
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(50);
                        
                        dataCollector.Disconnect();
                        
                        // 再次处理消息，确保DLL清理完成
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(50);
                    }
                    catch (Exception disconnectEx)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("断开连接异常: {0}", disconnectEx.Message));
                    }
                }
                
                // 8. 清理MinuteDataManager资源
                if (minuteDataManager != null)
                {
                    try
                    {
                        minuteDataManager.Cleanup();
                    }
                    catch { }
                }
                
                // 9. 清理日志资源（最后执行）
                try
                {
                    Logger.Instance.Cleanup();
                }
                catch { }
                
                Logger.Instance.Info("=== 程序退出清理完成 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("程序退出清理失败: {0}", ex.Message));
            }
            
            base.OnFormClosing(e);
        }
        
        /// <summary>
        /// 获取关闭原因描述
        /// </summary>
        private string GetCloseReason(CloseReason reason)
        {
            switch (reason)
            {
                case CloseReason.None:
                    return "未知原因";
                case CloseReason.WindowsShutDown:
                    return "Windows系统关闭";
                case CloseReason.MdiFormClosing:
                    return "MDI父窗体关闭";
                case CloseReason.UserClosing:
                    return "用户关闭（点击关闭按钮）";
                case CloseReason.TaskManagerClosing:
                    return "任务管理器关闭";
                case CloseReason.FormOwnerClosing:
                    return "所有者窗体关闭";
                case CloseReason.ApplicationExitCall:
                    return "调用Application.Exit()";
                default:
                    return string.Format("其他原因 ({0})", reason);
            }
        }
        
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == Demo.MY_MSG_BEGIN)
            {
                MessageBox.Show("Demo开始.");
            }
            else if (m.Msg == Demo.MY_MSG_END)
            {
                MessageBox.Show("Demo结束.");
            }
            else if (m.Msg == StockDataMQClient.StockDrv.RCV_MSG_STKDATA)
            {
                try
                {
                    // 重要：回调函数中只负责快速接收数据并放入队列，不进行耗时处理
                    // 这样可以避免阻塞下一个数据包的接收，减少行情延迟
                    switch (m.WParam.ToInt32())  
                    {
                        case StockDataMQClient.StockDrv.RCV_REPORT:  
                        {
                            // 更新最后接收时间
                            lastDataReceiveTime = DateTime.Now;
                            
                            // 记录实时数据接收（前10条详细记录，之后每100条记录一次）
                            var stats = dataReceiver.Statistics;
                            if (stats.TotalPacketsReceived < 10 || stats.TotalPacketsReceived % 100 == 0)
                            {
                                Logger.Instance.Info(string.Format("收到实时行情数据包 (RCV_REPORT), LParam={0}, 累计接收={1}", m.LParam.ToInt64(), stats.TotalPacketsReceived));
                            }
                            
                            // 快速将数据放入队列，不进行耗时处理
                            if (dataQueue != null)
                            {
                                dataQueue.Enqueue(DataPacketType.RealTimeData, m.LParam, m.WParam.ToInt32());
                                if (stats.TotalPacketsReceived < 10)
                                {
                                    Logger.Instance.Debug(string.Format("实时数据已入队: LParam={0}", m.LParam.ToInt64()));
                                }
                            }
                            else
                            {
                                // 如果队列未初始化，使用同步处理（向后兼容）
                                Logger.Instance.Warning("数据队列未初始化，使用同步处理");
                                dataReceiver.ProcessRealTimeData(m.LParam);
                            }
                            break;
                        }
                        case StockDataMQClient.StockDrv.RCV_FILEDATA:
                        {
                            try
                            {
                                // 快速识别数据类型并放入队列
                                StockDataMQClient.RCV_DATA pHeader = (StockDataMQClient.RCV_DATA)Marshal.PtrToStructure(
                                    m.LParam,
                                    typeof(StockDataMQClient.RCV_DATA));

                                // 调试：打印所有收到的文件数据类型（使用Info级别确保可见）
                                // FILE_HISTORY_EX=2(日线), FILE_MINUTE_EX=4(分钟), FILE_POWER_EX=6(除权), FILE_5MINUTE_EX=81(5分钟)
                                if (pHeader.m_wDataType == StockDataMQClient.StockDrv.FILE_HISTORY_EX ||
                                    pHeader.m_wDataType == StockDataMQClient.StockDrv.FILE_POWER_EX)
                                {
                                    Logger.Instance.Info(string.Format("【收到文件数据】m_wDataType={0}, m_nPacketNum={1}",
                                        pHeader.m_wDataType, pHeader.m_nPacketNum));
                                }

                                if (dataQueue != null)
                                {
                                    switch (pHeader.m_wDataType)
                                    {
                                        case StockDataMQClient.StockDrv.FILE_MINUTE_EX:
                                            // 分时线数据
                                            dataQueue.Enqueue(DataPacketType.MinuteData, m.LParam, m.WParam.ToInt32());
                                            break;
                                            
                                        case StockDataMQClient.StockDrv.FILE_5MINUTE_EX:
                                            // 5分钟K线数据
                                            dataQueue.Enqueue(DataPacketType.Minute5Data, m.LParam, m.WParam.ToInt32());
                                            break;
                                        
                                        case StockDataMQClient.StockDrv.FILE_HISTORY_EX:
                                            // 日线数据
                                            if (dataQueue != null)
                                            {
                                                dataQueue.Enqueue(DataPacketType.DailyData, m.LParam, m.WParam.ToInt32());
                                            }
                                            break;
                                        
                                        case StockDataMQClient.StockDrv.FILE_POWER_EX:
                                            // 除权数据
                                            if (dataQueue != null)
                                            {
                                                dataQueue.Enqueue(DataPacketType.ExRightsData, m.LParam, m.WParam.ToInt32());
                                            }
                                            break;
                                        
                                        /* // F10功能已禁用
                                        case StockDataMQClient.StockDrv.FILE_BASE_EX:
                                            // F10个股资料数据
                                            dataQueue.Enqueue(DataPacketType.StockBaseData, m.LParam, m.WParam.ToInt32());
                                            break;
                                        */
                                    }
                                }
                                else
                                {
                                    // 如果队列未初始化，使用同步处理（向后兼容）
                                    Logger.Instance.Warning("数据队列未初始化，使用同步处理");
                                    switch (pHeader.m_wDataType)
                                    {
                                        case StockDataMQClient.StockDrv.FILE_MINUTE_EX:
                                            dataReceiver.ProcessMinuteData(m.LParam);
                                            break;
                                        case StockDataMQClient.StockDrv.FILE_5MINUTE_EX:
                                            dataReceiver.Process5MinuteData(m.LParam);
                                            break;
                                        /* // F10功能已禁用
                                        case StockDataMQClient.StockDrv.FILE_BASE_EX:
                                            dataReceiver.ProcessStockBaseData(m.LParam);
                                            break;
                                        */
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(string.Format("处理文件数据异常: {0}", ex.Message));
                            }
                            break;
                        }
                        case StockDataMQClient.StockDrv.RCV_FENBIDATA:
                        {
                            // 分笔数据（可选处理，当前版本不处理）
                            break;
                        }
                        case StockDataMQClient.StockDrv.RCV_MKTTBLDATA:
                        {
                            try
                            {
                                // 码表数据也放入队列异步处理
                                if (dataQueue != null)
                                {
                                    dataQueue.Enqueue(DataPacketType.MarketTableData, m.LParam, m.WParam.ToInt32());
                                }
                                else
                                {
                                    // 如果队列未初始化，使用同步处理（向后兼容）
                                    Logger.Instance.Warning("数据队列未初始化，使用同步处理");
                                    dataReceiver.ProcessMarketTableData(m.LParam);
                                    // 更新状态标签
                                    if (this.InvokeRequired)
                                    {
                                        this.Invoke(new Action(() => UpdateStatusLabel()));
                                    }
                                    else
                                    {
                                        UpdateStatusLabel();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(string.Format("处理码表数据异常: {0}", ex.Message));
                            }
                            break;
                        }
                        case StockDataMQClient.StockDrv.RCV_FINANCEDATA:
                        {
                            // 财务数据不需要处理
                            break;
                        }
                        default:
                            break;  
                    }
                }
                catch (Exception ex)
                {
                    // 捕获所有异常，确保不会阻塞消息队列
                    Logger.Instance.Error(string.Format("WndProc处理消息异常: {0}, Msg={1}, WParam={2}", ex.Message, m.Msg, m.WParam));
                    System.Diagnostics.Debug.WriteLine(string.Format("WndProc异常: {0}", ex.Message));
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("点击停止接收按钮");
            
            if (!isReceivingData && instance == 0)
            {
                Logger.Instance.Warning("数据接收未开启，无需停止");
                MessageBox.Show("数据接收未开启", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (isReceivingData)
                {
                    // 停止采集
                    dataCollector.StopCollecting();
                    Logger.Instance.Info("已停止数据采集");
                    isReceivingData = false;
                }

                // 停止数据队列处理线程
                if (dataQueue != null)
                {
                    dataQueue.Stop();
                    Logger.Instance.Info("数据队列处理线程已停止");
                }

                // 断开连接
                if (instance != 0)
                {
                    dataCollector.Disconnect();
                    Logger.Instance.Info("已断开DLL连接");
                    instance = 0;
                }
                
                // 重置统计信息
                dataReceiver.ResetStatistics();
                UpdateStatusLabel();
                
                // 更新菜单项状态
                if (menuItemStartReceive != null)
                {
                    menuItemStartReceive.Enabled = true;
                }
                if (menuItemStopReceive != null)
                {
                    menuItemStopReceive.Enabled = false;
                }
                
                Logger.Instance.Success("已停止接收数据并断开连接");
                MessageBox.Show("已停止接收数据并断开连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("停止接收数据失败: {0}", ex.Message));
                MessageBox.Show(string.Format("停止接收数据失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        #region 数据加载功能

        /// <summary>
        /// 更新状态标签
        /// </summary>
        private void UpdateStatusLabel()
        {
            int stockCount = dataManager.GetStockCodeCount();
            var stats = dataReceiver.Statistics;
            
            string stockInfo = stockCount > 0 ? string.Format(" | 已加载 {0} 只股票", stockCount) : "";
            string dataInfo = stats.TotalPacketsReceived > 0 ? string.Format(" | 已接收 {0} 条数据", stats.TotalPacketsReceived) : "";
            string statusInfo = isReceivingData ? " | 接收中" : "";
            
            // 状态标签已移除，不再更新
        }
        
        // 状态更新定时器
        private System.Windows.Forms.Timer statusUpdateTimer;
        // MQ状态更新定时器
        private System.Windows.Forms.Timer mqStatusUpdateTimer;
        
        /// <summary>
        /// 初始化状态更新定时器
        /// </summary>
        private void InitializeStatusUpdateTimer()
        {
            statusUpdateTimer = new System.Windows.Forms.Timer();
            statusUpdateTimer.Interval = 1000;  // 每秒更新一次状态
            statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            statusUpdateTimer.Start();
            
            // 初始化MQ状态更新定时器（异步更新，不阻塞数据同步）
            mqStatusUpdateTimer = new System.Windows.Forms.Timer();
            mqStatusUpdateTimer.Interval = 1000;  // 每秒更新一次MQ状态
            mqStatusUpdateTimer.Tick += MQStatusUpdateTimer_Tick;
            mqStatusUpdateTimer.Start();
        }
        
        /// <summary>
        /// 状态更新定时器事件
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (isReceivingData)
            {
                UpdateStatusLabel();
            }
        }
        
        /// <summary>
        /// MQ状态更新定时器事件（异步更新，不阻塞数据同步线程）
        /// </summary>
        private void MQStatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (mqStatistics != null && lblMQStatus != null)
            {
                // Timer在UI线程，直接更新即可
                UpdateMQStatusLabel();
            }
        }
        
        /// <summary>
        /// 更新MQ状态标签
        /// </summary>
        private void UpdateMQStatusLabel()
        {
            if (lblMQStatus == null || mqStatistics == null)
                return;
            
            try
            {
                string statusText = mqStatistics.GetStatisticsString();
                lblMQStatus.Text = statusText;
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免影响数据同步
                System.Diagnostics.Debug.WriteLine(string.Format("更新MQ状态标签失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 更新当前板块标签（兼容方法，已改为UpdateStatusLabel）
        /// </summary>
        private void UpdateCurrentBoardLabel()
        {
            UpdateStatusLabel();
        }

        /// <summary>
        /// 刷新所有面板数据（兼容方法，当前版本不需要）
        /// </summary>
        private void RefreshAllPanels()
        {
            // 当前版本不需要面板刷新
        }

        /// <summary>
        /// 股票代码输入框文本变化事件（使用防抖机制，自动显示股票信息）
        /// </summary>
        


        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void btnClearLog_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("清空日志");
            Logger.Instance.Clear();
            Logger.Instance.Info("日志已清空");
        }

        /// <summary>
        /// 加载基础数据按钮点击事件
        /// </summary>
        private void btnLoadBasicData_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("点击加载基础数据按钮");
            
            if (!isReceivingData)
            {
                Logger.Instance.Warning("数据接收未开启，无法加载基础数据");
                MessageBox.Show("请先点击'开启接收数据'按钮", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Logger.Instance.Info("开始加载基础数据（码表数据）...");
                
                // 基础数据（码表）会在连接后自动接收，这里可以主动请求
                // 如果DLL支持主动请求码表，可以调用相应API
                // 目前码表数据是通过RCV_MKTTBLDATA消息自动接收的
                
                // 检查是否已有基础数据
                int codeCount = dataManager.GetStockCodeCount();
                var stats = dataReceiver.Statistics;
                
                // 刷新数据显示
                RefreshStockDataDisplay();
                
                if (codeCount > 0)
                {
                    // 统计有实时数据的股票数量
                    int dataCount = 0;
                    if (dgvStockData.Rows.Count > 0)
                    {
                        foreach (DataGridViewRow row in dgvStockData.Rows)
                        {
                            if (row.Cells["NewPrice"].Value != null)
                            {
                                float price = Convert.ToSingle(row.Cells["NewPrice"].Value);
                                if (price > 0) dataCount++;
                            }
                        }
                    }
                    
                    string message = string.Format("基础数据已加载，共 {0} 只股票", codeCount);
                    if (stats.MarketTableCount > 0)
                    {
                        message += string.Format("\n已接收 {0} 个码表数据包", stats.MarketTableCount);
                        message += string.Format("\n累计码表股票数: {0}", stats.TotalMarketTableStocks);
                        if (stats.TotalMarketTableStocks > codeCount)
                        {
                            message += string.Format("\n注意: 累计数({0}) > 当前数({1})，可能有重复或数据还在接收中", stats.TotalMarketTableStocks, codeCount);
                        }
                    }
                    message += "\n\n详细数据已显示在右侧表格中";
                    message += string.Format("\n共显示 {0} 条记录", codeCount);
                    if (dataCount > 0)
                    {
                        message += string.Format("，其中 {0} 条包含实时行情数据（价格、成交量等）", dataCount);
                    }
                    else
                    {
                        message += "\n（实时行情数据正在接收中，请稍候...）";
                    }
                    
                    Logger.Instance.Success(message);
                    MessageBox.Show(message, "基础数据状态", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Logger.Instance.Info("基础数据尚未接收，等待码表数据...");
                    string waitMessage = "基础数据正在接收中，请稍候...\n码表数据会在连接后自动接收。";
                    if (stats.MarketTableCount > 0)
                    {
                        waitMessage += string.Format("\n已接收 {0} 个码表数据包", stats.MarketTableCount);
                    }
                    MessageBox.Show(waitMessage, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载基础数据异常: {0}", ex.Message));
                MessageBox.Show(string.Format("加载基础数据失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 刷新股票数据显示（显示详细数据，包括实时行情）
        /// </summary>
        private void RefreshStockDataDisplay()
        {
            try
            {
                // 获取所有股票代码和名称（码表数据）
                var codeDict = dataManager.GetStockCodeDictionary();
                
                if (codeDict == null || codeDict.Count == 0)
                {
                    dgvStockData.DataSource = null;
                    lblStockDataTitle.Text = "基础数据（码表）: 暂无数据";
                    return;
                }
                
                // 使用Dictionary来存储，键为标准化代码，确保完全去重
                Dictionary<string, StockDisplayItem> uniqueStocks = new Dictionary<string, StockDisplayItem>();
                int dataCount = 0;
                
                foreach (var kvp in codeDict)
                {
                    string code = kvp.Key;
                    string normalizedCode = DataConverter.NormalizeStockCode(code);
                    
                    // 如果已经处理过这个标准化代码，跳过（完全去重）
                    if (uniqueStocks.ContainsKey(normalizedCode))
                    {
                        // 如果已有数据，但当前有实时数据，则更新为实时数据
                        StockDisplayItem existingItem = uniqueStocks[normalizedCode];
                        if (existingItem.NewPrice == 0)
                        {
                            StockData realtimeData = dataManager.GetStockData(normalizedCode);
                            if (realtimeData != null && realtimeData.NewPrice > 0)
                            {
                                // 更新为实时数据
                                existingItem.StockCode = realtimeData.Code ?? normalizedCode;
                                existingItem.StockName = realtimeData.Name ?? existingItem.StockName;
                                existingItem.NewPrice = realtimeData.NewPrice;
                                existingItem.Open = realtimeData.Open;
                                existingItem.LastClose = realtimeData.LastClose;
                                existingItem.High = realtimeData.High;
                                existingItem.Low = realtimeData.Low;
                                existingItem.Volume = realtimeData.Volume;
                                existingItem.Amount = realtimeData.Amount;
                                existingItem.ChangePercent = realtimeData.ChangePercent;
                                // 确保涨跌额已计算
                                if (realtimeData.ChangeAmount == 0 && realtimeData.LastClose > 0)
                                {
                                    realtimeData.CalculateChangeAmount();
                                }
                                existingItem.ChangeAmount = realtimeData.ChangeAmount;
                                existingItem.UpdateTime = realtimeData.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                existingItem.MarketType = GetMarketTypeName(realtimeData.Code ?? normalizedCode);
                                dataCount++;
                            }
                        }
                        continue;
                    }
                    
                    string name = kvp.Value;
                    
                    // 尝试从全局缓存中获取实时行情数据（使用标准化代码）
                    StockData stockData = dataManager.GetStockData(normalizedCode);
                    
                    // 使用统一方法创建显示项
                    StockDisplayItem item = CreateDisplayItem(normalizedCode, name, stockData);
                    
                    if (stockData != null && stockData.NewPrice > 0)
                    {
                        dataCount++;
                    }
                    
                    // 使用标准化代码作为键，确保完全去重
                    uniqueStocks[normalizedCode] = item;
                }
                
                // 转换为列表并按涨幅排序（降序：涨幅高的排在前面）
                var displayList = uniqueStocks.Values.ToList();
                if (displayList.Count > 1)
                {
                    displayList = displayList
                        .OrderByDescending(x => x.ChangePercent)  // 按涨幅降序排序
                        .ThenBy(x => x.StockCode)  // 涨幅相同时按代码排序，确保排序稳定
                        .ToList();
                }
                
                // 绑定数据（显示所有数据，不限制数量）
                dgvStockData.DataSource = displayList;
                
                // 更新标题
                int totalCount = displayList.Count;
                if (dataCount > 0)
                {
                    lblStockDataTitle.Text = string.Format("股票数据: 共 {0} 只股票，其中 {1} 只有实时行情数据", totalCount, dataCount);
                }
                else
                {
                    lblStockDataTitle.Text = string.Format("基础数据（码表）: 共 {0} 只股票（等待实时行情数据）", totalCount);
                }
                
                Logger.Instance.Debug(string.Format("已刷新股票数据显示: 共 {0} 只股票（去重后），其中 {1} 只有实时行情数据", totalCount, dataCount));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("刷新股票数据显示失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// 股票显示项（用于DataGridView绑定）
        /// </summary>
        private class StockDisplayItem
        {
            private string stockCode;
            private string stockName;
            private float newPrice;
            private float open;
            private float lastClose;
            private float high;
            private float low;
            private float volume;
            private float amount;
            private float changePercent;
            private float changeAmount;
            private string updateTime;
            private string marketType;

            public string StockCode
            {
                get { return stockCode; }
                set { stockCode = value; }
            }

            public string StockName
            {
                get { return stockName; }
                set { stockName = value; }
            }

            public float NewPrice
            {
                get { return newPrice; }
                set { newPrice = value; }
            }

            public float Open
            {
                get { return open; }
                set { open = value; }
            }

            public float LastClose
            {
                get { return lastClose; }
                set { lastClose = value; }
            }

            public float High
            {
                get { return high; }
                set { high = value; }
            }

            public float Low
            {
                get { return low; }
                set { low = value; }
            }

            public float Volume
            {
                get { return volume; }
                set { volume = value; }
            }

            public float Amount
            {
                get { return amount; }
                set { amount = value; }
            }

            public float ChangePercent
            {
                get { return changePercent; }
                set { changePercent = value; }
            }

            public float ChangeAmount
            {
                get { return changeAmount; }
                set { changeAmount = value; }
            }

            public string UpdateTime
            {
                get { return updateTime; }
                set { updateTime = value; }
            }

            public string MarketType
            {
                get { return marketType; }
                set { marketType = value; }
            }
        }

        /// <summary>
        /// 加载分钟数据按钮点击事件
        /// </summary>
        private void btnLoadMinuteData_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("点击加载分钟数据按钮");
            
            if (!isReceivingData)
            {
                Logger.Instance.Warning("数据接收未开启，无法加载分钟数据");
                MessageBox.Show("请先点击'开启接收数据'按钮", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 输入框已移除，通过输入对话框获取股票代码
            string input = "";
            using (var inputForm = new Form())
            {
                inputForm.Text = "加载分钟数据";
                inputForm.Size = new Size(300, 120);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                
                var label = new Label() { Text = "请输入股票代码（如：SH600026）:", Location = new Point(10, 10), AutoSize = true };
                var textBox = new TextBox() { Location = new Point(10, 35), Width = 260 };
                var btnOK = new Button() { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(100, 65) };
                var btnCancel = new Button() { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(180, 65) };
                
                inputForm.Controls.AddRange(new Control[] { label, textBox, btnOK, btnCancel });
                inputForm.AcceptButton = btnOK;
                inputForm.CancelButton = btnCancel;
                
                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    input = textBox.Text.Trim().ToUpper();
                }
                else
                {
                    return;  // 用户取消
                }
            }
            
            string stockCode = input;
            
            if (string.IsNullOrEmpty(stockCode))
            {
                Logger.Instance.Warning("未输入股票代码，无法加载分钟数据");
                MessageBox.Show("请输入股票代码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 标准化股票代码
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                
                // 检查是否已有该股票的分钟数据
                var minuteDataList = minuteDataManager.GetMinuteData(normalizedCode);
                
                if (minuteDataList.Count > 0)
                {
                    Logger.Instance.Success(string.Format("股票 {0} 已有 {1} 条分钟数据", normalizedCode, minuteDataList.Count));
                    // 显示已有数据
                    DisplayMinuteData(normalizedCode);
                }
                else
                {
                    Logger.Instance.Info(string.Format("股票 {0} 暂无分钟数据，开始请求...", normalizedCode));
                    try
                    {
                        // 请求分钟数据
                        dataCollector.StartCollectMinuteData(normalizedCode);
                        Logger.Instance.Success(string.Format("已请求股票 {0} 的分钟数据", normalizedCode));
                        MessageBox.Show(string.Format("已请求股票 {0} 的分钟数据，请等待数据接收...", normalizedCode), "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 如果函数未初始化，给出友好提示
                        Logger.Instance.Warning(string.Format("无法请求分钟数据: {0}", ex.Message));
                        MessageBox.Show(string.Format("无法请求分钟数据：{0}\n\n", ex.Message) +
                                      "可能原因：\n" +
                                      "1. DLL版本不支持分钟数据功能\n" +
                                      "2. AskStockMin函数未在DLL中找到\n\n" +
                                      "实时行情数据接收不受影响，可以正常使用。", 
                                      "功能不可用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载分钟数据异常: {0}", ex.Message));
                MessageBox.Show(string.Format("加载分钟数据失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 查看F10数据按钮点击事件
        /// </summary>
        /* // F10功能已禁用
        private void btnLoadF10Data_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Instance.Info("=".PadRight(80, '='));
                Logger.Instance.Info("【F10按钮】点击查看F10数据按钮");
                Logger.Instance.Info(string.Format("点击时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));

                // 检查程序状态
                Logger.Instance.Info("数据接收状态: {(isReceivingData ? "运行中" : "已停止")}");
                Logger.Instance.Info("F10窗口状态: {(isF10WindowOpen ? "已打开" : "未打开")}");
                Logger.Instance.Info("数据采集器状态: {(dataCollector != null ? "已初始化" : "未初始化")}");

                if (!isReceivingData)
                {
                    Logger.Instance.Warning("数据接收未开启，无法查看F10数据");
                    MessageBox.Show("请先点击'开启接收数据'按钮", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 优先从DataGridView中选择的股票获取代码
                string stockCode = null;
                
                try
                {
                    if (dgvStockData != null && dgvStockData.SelectedRows != null && dgvStockData.SelectedRows.Count > 0)
                    {
                        // 从选中的行获取股票代码
                        var selectedRow = dgvStockData.SelectedRows[0];
                        if (selectedRow != null && selectedRow.Cells != null && selectedRow.Cells["StockCode"] != null)
                        {
                            var cellValue = selectedRow.Cells["StockCode"].Value;
                            if (cellValue != null)
                            {
                                stockCode = cellValue.ToString();
                                Logger.Instance.Info(string.Format("从表格获取股票代码: {0}", stockCode));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning(string.Format("从表格获取股票代码失败: {0}", ex.Message));
                    Logger.Instance.Warning(string.Format("异常类型: {0}", ex.GetType().Name));
                    // 继续尝试从文本框获取
                }
                
                // 如果没有选中行，尝试从文本框获取
                if (string.IsNullOrEmpty(stockCode))
                {
                    try
                    {
                        // 输入框已移除，无法从文本框获取股票代码
                        // 如果需要，可以通过其他方式获取（如从选中的板块面板）
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning(string.Format("从文本框获取股票代码失败: {0}", ex.Message));
                        Logger.Instance.Warning(string.Format("异常类型: {0}", ex.GetType().Name));
                    }
                }
                
                if (string.IsNullOrEmpty(stockCode))
                {
                    Logger.Instance.Warning("未选择或输入股票代码，无法查看F10数据");
                    MessageBox.Show("请先在表格中选择股票，或在输入框中输入股票代码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 标准化股票代码
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                Logger.Instance.Info(string.Format("标准化后的股票代码: {0}", normalizedCode));
                
                // 优先从缓存读取F10数据
                if (f10DataManager != null)
                {
                    string cachedContent = f10DataManager.LoadF10DataFromCache(normalizedCode);
                    if (!string.IsNullOrEmpty(cachedContent) && !f10DataManager.NeedUpdate(normalizedCode))
                    {
                        Logger.Instance.Info(string.Format("从缓存加载F10数据: {0}", normalizedCode));
                        // 直接显示缓存的数据
                        StockBaseDataEventArgs cachedEvent = new StockBaseDataEventArgs(
                            normalizedCode, 
                            normalizedCode + ".TXT", 
                            cachedContent, 
                            cachedContent.Length);
                        DisplayStockBaseData(cachedEvent, normalizedCode);
                        return;  // 从缓存读取成功，直接返回
                    }
                    else if (!string.IsNullOrEmpty(cachedContent))
                    {
                        Logger.Instance.Info(string.Format("缓存数据已过期，显示缓存数据并请求更新: {0}", normalizedCode));
                        // 显示缓存数据，同时请求更新
                        StockBaseDataEventArgs cachedEvent = new StockBaseDataEventArgs(
                            normalizedCode, 
                            normalizedCode + ".TXT", 
                            cachedContent, 
                            cachedContent.Length);
                        DisplayStockBaseData(cachedEvent, normalizedCode);
                        // 继续执行下面的请求逻辑，更新缓存
                    }
                }
                
                // 记录当前请求的股票代码（用于过滤后续收到的F10数据）
                pendingF10StockCode = normalizedCode;
                f10RequestTime = DateTime.Now;  // 记录请求时间
                Logger.Instance.Info(string.Format("已设置待处理F10股票代码: {0}, 请求时间: {1:yyyy-MM-dd HH:mm:ss.fff}", pendingF10StockCode, f10RequestTime));
                
                // 启动超时定时器（30秒后清除请求标记）
                if (f10TimeoutTimer == null)
                {
                    f10TimeoutTimer = new System.Windows.Forms.Timer();
                    f10TimeoutTimer.Interval = 30000;  // 30秒
                    f10TimeoutTimer.Tick += (s, args) => {
                        if (!string.IsNullOrEmpty(pendingF10StockCode))
                        {
                            TimeSpan elapsed = DateTime.Now - f10RequestTime;
                            if (elapsed.TotalSeconds >= 30)
                            {
                                Logger.Instance.Warning(string.Format("F10请求超时（30秒），清除请求标记: {0}", pendingF10StockCode));
                                pendingF10StockCode = "";
                                f10RequestTime = DateTime.MinValue;
                                f10TimeoutTimer.Stop();
                            }
                        }
                        else
                        {
                            f10TimeoutTimer.Stop();
                        }
                    };
                }
                f10TimeoutTimer.Start();
                
                // 检查dataCollector是否已初始化
                if (dataCollector == null)
                {
                    pendingF10StockCode = "";  // 清除请求标记
                    Logger.Instance.Error("数据采集器未初始化");
                    MessageBox.Show("数据采集器未初始化，请先连接DLL", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // 请求F10数据（添加详细的异常处理）
                Logger.Instance.Info("准备调用RequestStockBaseData...");
                bool success = false;
                try
                {
                    success = dataCollector.RequestStockBaseData(normalizedCode);
                    Logger.Instance.Info(string.Format("RequestStockBaseData返回结果: {0}", success));
                }
                catch (AccessViolationException avEx)
                {
                    pendingF10StockCode = "";  // 清除请求标记
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10错误】访问冲突异常");
                    Logger.Instance.Error(string.Format("异常消息: {0}", avEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", avEx.StackTrace));
                    Logger.Instance.Error("=".PadRight(80, '='));
                    MessageBox.Show("调用F10函数时发生访问冲突，程序可能不稳定。\n\n详细错误请查看日志文件。", 
                        "访问冲突", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (NullReferenceException nullEx)
                {
                    pendingF10StockCode = "";  // 清除请求标记
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10错误】空引用异常");
                    Logger.Instance.Error(string.Format("异常消息: {0}", nullEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", nullEx.StackTrace));
                    Logger.Instance.Error("=".PadRight(80, '='));
                    MessageBox.Show("调用F10函数时发生空引用异常。\n\n详细错误请查看日志文件。", 
                        "空引用错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (Exception callEx)
                {
                    pendingF10StockCode = "";  // 清除请求标记
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10错误】调用异常");
                    Logger.Instance.Error(string.Format("异常类型: {0}", callEx.GetType().FullName));
                    Logger.Instance.Error(string.Format("异常消息: {0}", callEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", callEx.StackTrace));
                    if (callEx.InnerException != null)
                    {
                        Logger.Instance.Error(string.Format("内部异常: {0}", callEx.InnerException.Message));
                    }
                    Logger.Instance.Error("=".PadRight(80, '='));
                    MessageBox.Show(string.Format("调用F10函数时发生异常: {0}\n\n详细错误请查看日志文件。", callEx.Message), 
                        "调用错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                if (success)
                {
                    Logger.Instance.Success(string.Format("已请求股票 {0} 的F10数据，等待数据返回...", normalizedCode));
                    Logger.Instance.Info("=".PadRight(80, '='));
                    // 不显示MessageBox，改为在日志中提示，避免干扰用户
                    // 数据返回后会自动显示F10窗口
                }
                else
                {
                    pendingF10StockCode = "";  // 清除请求标记
                    Logger.Instance.Error(string.Format("请求F10数据失败: {0}", normalizedCode));
                    Logger.Instance.Info("=".PadRight(80, '='));
                    MessageBox.Show("请求F10数据失败，请检查DLL是否支持AskStockBase函数", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                pendingF10StockCode = "";  // 确保清除请求标记
                Logger.Instance.Error("=".PadRight(80, '='));
                Logger.Instance.Error("【F10错误】按钮点击处理异常");
                Logger.Instance.Error(string.Format("异常类型: {0}", ex.GetType().FullName));
                Logger.Instance.Error(string.Format("异常消息: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                if (ex.InnerException != null)
                {
                    Logger.Instance.Error(string.Format("内部异常类型: {0}", ex.InnerException.GetType().FullName));
                    Logger.Instance.Error(string.Format("内部异常消息: {0}", ex.InnerException.Message));
                    Logger.Instance.Error(string.Format("内部异常堆栈: {0}", ex.InnerException.StackTrace));
                }
                Logger.Instance.Error("=".PadRight(80, '='));
                
                try
                {
                    MessageBox.Show(string.Format("查看F10数据失败: {0}\n\n详细错误请查看日志文件。", ex.Message), 
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception msgEx)
                {
                    // 如果MessageBox也失败了，至少记录到Debug
                    System.Diagnostics.Debug.WriteLine(string.Format("显示错误消息失败: {0}", msgEx.Message));
                }
            }
        }
        */

        /// <summary>
        /// 显示分钟数据
        /// </summary>
        private void DisplayMinuteData(string stockCode)
        {
            try
            {
                var minuteDataList = minuteDataManager.GetMinuteData(stockCode);
                
                if (minuteDataList.Count == 0)
                {
                    Logger.Instance.Warning(string.Format("股票 {0} 暂无分钟数据", stockCode));
                    return;
                }

                // 获取股票名称
                string stockName = dataManager.GetStockName(stockCode) ?? stockCode;
                
                // 创建显示窗口
                Form dataForm = new Form();
                dataForm.Text = string.Format("{0} ({1}) - 分钟数据 - 共 {2} 条", stockName, stockCode, minuteDataList.Count);
                dataForm.Size = new Size(800, 600);
                dataForm.StartPosition = FormStartPosition.CenterParent;
                
                DataGridView dgv = new DataGridView();
                dgv.Dock = DockStyle.Fill;
                dgv.AutoGenerateColumns = false;
                dgv.AllowUserToAddRows = false;
                dgv.ReadOnly = true;
                dgv.BackgroundColor = Color.FromArgb(40, 40, 40);
                dgv.DefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
                dgv.DefaultCellStyle.ForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                
                // 添加列
                dgv.Columns.Add(new DataGridViewTextBoxColumn { 
                    Name = "Time", 
                    HeaderText = "时间", 
                    DataPropertyName = "TradeTime",
                    Width = 150
                });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { 
                    Name = "Price", 
                    HeaderText = "成交价", 
                    DataPropertyName = "Price",
                    Width = 100
                });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { 
                    Name = "Volume", 
                    HeaderText = "成交量", 
                    DataPropertyName = "Volume",
                    Width = 120
                });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { 
                    Name = "Amount", 
                    HeaderText = "成交额", 
                    DataPropertyName = "Amount",
                    Width = 120
                });
                
                // 绑定数据
                dgv.DataSource = minuteDataList.Select(d => new {
                    TradeTime = d.TradeTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Price = d.Price.ToString("F2"),
                    Volume = d.Volume.ToString("F0"),
                    Amount = d.Amount.ToString("F2")
                }).ToList();
                
                dataForm.Controls.Add(dgv);
                dataForm.Show();
                
                Logger.Instance.Success(string.Format("已显示股票 {0} 的 {1} 条分钟数据", stockCode, minuteDataList.Count));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("显示分钟数据失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 获取市场类型名称
        /// </summary>
        private string GetMarketTypeName(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "";
            
            if (code.StartsWith("SH"))
                return "上海";
            else if (code.StartsWith("SZ"))
                return "深圳";
            else if (code.StartsWith("BJ"))
                return "北京";
            else
                return "";
        }
        
        /// <summary>
        /// 查询股票按钮点击事件
        /// </summary>
        private void btnSearchStock_Click(object sender, EventArgs e)
        {
            string searchCode = txtSearchStock.Text.Trim();
            if (string.IsNullOrEmpty(searchCode))
            {
                // 如果查询为空，显示所有数据
                RefreshStockDataDisplay();
                Logger.Instance.Info("查询为空，显示所有股票数据");
            }
            else
            {
                SearchStock(searchCode);
            }
        }
        
        /// <summary>
        /// 查询输入框按键事件（按Enter键查询）
        /// </summary>
        private void txtSearchStock_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSearchStock_Click(sender, e);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 查询股票信息（根据股票代码过滤）- 优化版本，支持精确查询和模糊查询
        /// </summary>
        private void SearchStock(string searchCode)
        {
            try
            {
                if (string.IsNullOrEmpty(searchCode))
                {
                    // 如果查询为空，显示所有数据
                    RefreshStockDataDisplay();
                    return;
                }
                
                // 标准化查询代码
                string normalizedSearchCode = DataConverter.NormalizeStockCode(searchCode.Trim().ToUpper());
                string searchCodeUpper = searchCode.Trim().ToUpper();
                
                // 获取所有股票代码和名称（码表数据）
                var codeDict = dataManager.GetStockCodeDictionary();
                
                if (codeDict == null || codeDict.Count == 0)
                {
                    dgvStockData.DataSource = null;
                    lblStockDataTitle.Text = "基础数据（码表）: 暂无数据";
                    return;
                }
                
                // 优化：如果是精确匹配（标准化后完全相等），直接查找，避免遍历
                StockDisplayItem exactMatch = null;
                if (codeDict.ContainsKey(normalizedSearchCode))
                {
                    // 精确匹配，快速查询
                    string name = codeDict[normalizedSearchCode];
                    StockData stockData = dataManager.GetStockData(normalizedSearchCode);
                    
                    exactMatch = CreateDisplayItem(normalizedSearchCode, name, stockData);
                    
                    // 如果是精确匹配，只显示这一条
                    var displayList = new List<StockDisplayItem> { exactMatch };
                    dgvStockData.DataSource = displayList;
                    
                    int dataCount = stockData != null && stockData.NewPrice > 0 ? 1 : 0;
                    lblStockDataTitle.Text = string.Format("查询结果: 找到 1 只股票（精确匹配: {0}）", normalizedSearchCode) + 
                                           (dataCount > 0 ? "，包含实时行情数据" : "，等待实时行情数据");
                    Logger.Instance.Info(string.Format("精确查询股票: {0}，找到 1 只股票", normalizedSearchCode));
                    return;
                }
                
                // 模糊查询：遍历所有股票（优化：使用并行查询提高性能）
                var displayList2 = new List<StockDisplayItem>();
                int dataCount2 = 0;
                
                // 使用并行查询优化性能（对于大量数据）
                if (codeDict.Count > 1000)
                {
                    var results = codeDict
                        .Where(kvp => {
                            string code = kvp.Key;
                            string normalizedCode = DataConverter.NormalizeStockCode(code);
                            return normalizedCode.Contains(normalizedSearchCode) || 
                                   normalizedSearchCode.Contains(normalizedCode) ||
                                   code.Contains(searchCodeUpper);
                        })
                        .Select(kvp => {
                            string normalizedCode = DataConverter.NormalizeStockCode(kvp.Key);
                            StockData stockData = dataManager.GetStockData(normalizedCode);
                            return CreateDisplayItem(normalizedCode, kvp.Value, stockData);
                        })
                        .ToList();
                    
                    displayList2 = results;
                    dataCount2 = results.Count(r => r.NewPrice > 0);
                }
                else
                {
                    // 数据量少时，直接遍历
                    foreach (var kvp in codeDict)
                    {
                        string code = kvp.Key;
                        string normalizedCode = DataConverter.NormalizeStockCode(code);
                        
                        // 过滤：只显示匹配的股票代码
                        if (!normalizedCode.Contains(normalizedSearchCode) && 
                            !normalizedSearchCode.Contains(normalizedCode) &&
                            !code.Contains(searchCodeUpper))
                        {
                            continue;
                        }
                        
                        StockData stockData = dataManager.GetStockData(normalizedCode);
                        var item = CreateDisplayItem(normalizedCode, kvp.Value, stockData);
                        if (item.NewPrice > 0) dataCount2++;
                        displayList2.Add(item);
                    }
                }
                
                // 排序
                displayList2.Sort((x, y) => string.Compare(x.StockCode, y.StockCode, StringComparison.Ordinal));
                
                // 绑定数据
                dgvStockData.DataSource = displayList2;
                
                // 更新标题
                int totalCount = displayList2.Count;
                if (totalCount > 0)
                {
                    if (dataCount2 > 0)
                    {
                        lblStockDataTitle.Text = string.Format("查询结果: 找到 {0} 只股票（查询: {1}），其中 {2} 只有实时行情数据", totalCount, searchCode, dataCount2);
                    }
                    else
                    {
                        lblStockDataTitle.Text = string.Format("查询结果: 找到 {0} 只股票（查询: {1}），等待实时行情数据", totalCount, searchCode);
                    }
                }
                else
                {
                    lblStockDataTitle.Text = string.Format("查询结果: 未找到匹配的股票（查询: {0}）", searchCode);
                }
                
                Logger.Instance.Info(string.Format("查询股票: {0}，找到 {1} 只股票，其中 {2} 只有实时行情数据", searchCode, totalCount, dataCount2));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("查询股票失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// 创建显示项（优化：统一创建逻辑，避免重复代码）
        /// </summary>
        private StockDisplayItem CreateDisplayItem(string normalizedCode, string name, StockData stockData)
        {
            string marketTypeName = GetMarketTypeName(normalizedCode);
            
            StockDisplayItem item = new StockDisplayItem
            {
                StockCode = normalizedCode,
                StockName = name,
                NewPrice = 0f,
                Open = 0f,
                LastClose = 0f,
                High = 0f,
                Low = 0f,
                Volume = 0f,
                Amount = 0f,
                ChangePercent = 0f,
                ChangeAmount = 0f,
                UpdateTime = "",
                MarketType = marketTypeName
            };
            
            if (stockData != null && stockData.NewPrice > 0)
            {
                // 如果有实时数据，显示详细数据
                item.StockCode = stockData.Code ?? normalizedCode;
                item.StockName = stockData.Name ?? name;
                item.NewPrice = stockData.NewPrice;
                item.Open = stockData.Open;
                item.LastClose = stockData.LastClose;
                item.High = stockData.High;
                item.Low = stockData.Low;
                item.Volume = stockData.Volume;
                item.Amount = stockData.Amount;
                item.ChangePercent = stockData.ChangePercent;
                // 确保涨跌额已计算
                if (stockData.ChangeAmount == 0 && stockData.LastClose > 0)
                {
                    stockData.CalculateChangeAmount();
                }
                item.ChangeAmount = stockData.ChangeAmount;
                item.UpdateTime = stockData.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss");
                item.MarketType = GetMarketTypeName(stockData.Code ?? normalizedCode);
            }
            
            return item;
        }
        
        /// <summary>
        /// 显示/隐藏数据面板菜单项点击事件
        /// </summary>
        private void menuItemShowDataPanel_Click(object sender, EventArgs e)
        {
            bool isChecked = menuItemShowDataPanel.Checked;
            panelData.Visible = isChecked;
            
            // 根据数据面板所在位置，折叠相应的Panel
            if (splitContainerMain.Panel1.Controls.Contains(panelData))
            {
                splitContainerMain.Panel1Collapsed = !isChecked;
            }
            else if (splitContainerMain.Panel2.Controls.Contains(panelData))
            {
                splitContainerMain.Panel2Collapsed = !isChecked;
            }
            
            if (!isChecked && !panelLog.Visible)
            {
                // 如果两个面板都隐藏，至少显示一个
                menuItemShowLogPanel.Checked = true;
                panelLog.Visible = true;
                if (splitContainerMain.Panel1.Controls.Contains(panelLog))
                {
                    splitContainerMain.Panel1Collapsed = false;
                }
                else if (splitContainerMain.Panel2.Controls.Contains(panelLog))
                {
                    splitContainerMain.Panel2Collapsed = false;
                }
            }
            
            Logger.Instance.Info(string.Format("数据面板已{0}", isChecked ? "显示" : "隐藏"));
        }
        
        /// <summary>
        /// 显示/隐藏日志面板菜单项点击事件
        /// </summary>
        private void menuItemShowLogPanel_Click(object sender, EventArgs e)
        {
            bool isChecked = menuItemShowLogPanel.Checked;
            panelLog.Visible = isChecked;
            
            // 根据日志面板所在位置，折叠相应的Panel
            if (splitContainerMain.Panel1.Controls.Contains(panelLog))
            {
                splitContainerMain.Panel1Collapsed = !isChecked;
            }
            else if (splitContainerMain.Panel2.Controls.Contains(panelLog))
            {
                splitContainerMain.Panel2Collapsed = !isChecked;
            }
            
            if (!isChecked && !panelData.Visible)
            {
                // 如果两个面板都隐藏，至少显示一个
                menuItemShowDataPanel.Checked = true;
                panelData.Visible = true;
                if (splitContainerMain.Panel1.Controls.Contains(panelData))
                {
                    splitContainerMain.Panel1Collapsed = false;
                }
                else if (splitContainerMain.Panel2.Controls.Contains(panelData))
                {
                    splitContainerMain.Panel2Collapsed = false;
                }
            }
            
            Logger.Instance.Info(string.Format("日志面板已{0}", isChecked ? "显示" : "隐藏"));
        }
        
        /// <summary>
        /// 数据面板停靠顶部菜单项点击事件
        /// </summary>
        private void menuItemDockDataTop_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool dataInPanel1 = splitContainerMain.Panel1.Controls.Contains(panelData);
            
            if (!dataInPanel1)
            {
                // 如果数据面板在Panel2，交换它们
                splitContainerMain.Panel1.Controls.Clear();
                splitContainerMain.Panel2.Controls.Clear();
                splitContainerMain.Panel1.Controls.Add(panelData);
                splitContainerMain.Panel2.Controls.Add(panelLog);
            }
            
            // 更新菜单状态（已移除停靠菜单项）
            // menuItemDockDataTop.Checked = true;
            // menuItemDockDataBottom.Checked = false;
            // menuItemDockLogTop.Checked = false;
            // menuItemDockLogBottom.Checked = true;
            
            Logger.Instance.Info("数据面板已停靠到顶部");
        }
        
        /// <summary>
        /// 数据面板停靠底部菜单项点击事件
        /// </summary>
        private void menuItemDockDataBottom_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool dataInPanel1 = splitContainerMain.Panel1.Controls.Contains(panelData);
            
            if (dataInPanel1)
            {
                // 如果数据面板在Panel1，交换它们
                splitContainerMain.Panel1.Controls.Clear();
                splitContainerMain.Panel2.Controls.Clear();
                splitContainerMain.Panel1.Controls.Add(panelLog);
                splitContainerMain.Panel2.Controls.Add(panelData);
            }
            
            // 更新菜单状态（已移除停靠菜单项）
            // menuItemDockDataTop.Checked = false;
            // menuItemDockDataBottom.Checked = true;
            // menuItemDockLogTop.Checked = true;
            // menuItemDockLogBottom.Checked = false;
            
            Logger.Instance.Info("数据面板已停靠到底部");
        }
        
        /// <summary>
        /// 日志面板停靠顶部菜单项点击事件
        /// </summary>
        private void menuItemDockLogTop_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool logInPanel1 = splitContainerMain.Panel1.Controls.Contains(panelLog);
            
            if (!logInPanel1)
            {
                // 如果日志面板在Panel2，交换它们
                splitContainerMain.Panel1.Controls.Clear();
                splitContainerMain.Panel2.Controls.Clear();
                splitContainerMain.Panel1.Controls.Add(panelLog);
                splitContainerMain.Panel2.Controls.Add(panelData);
            }
            
            // 更新菜单状态（已移除停靠菜单项）
            // menuItemDockDataTop.Checked = false;
            // menuItemDockDataBottom.Checked = true;
            // menuItemDockLogTop.Checked = true;
            // menuItemDockLogBottom.Checked = false;
            
            Logger.Instance.Info("日志面板已停靠到顶部");
        }
        
        /// <summary>
        /// 日志面板停靠底部菜单项点击事件
        /// </summary>
        private void menuItemDockLogBottom_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool logInPanel1 = splitContainerMain.Panel1.Controls.Contains(panelLog);
            
            if (logInPanel1)
            {
                // 如果日志面板在Panel1，交换它们
                splitContainerMain.Panel1.Controls.Clear();
                splitContainerMain.Panel2.Controls.Clear();
                splitContainerMain.Panel1.Controls.Add(panelData);
                splitContainerMain.Panel2.Controls.Add(panelLog);
            }
            
            // 更新菜单状态（已移除停靠菜单项）
            // menuItemDockDataTop.Checked = true;
            // menuItemDockDataBottom.Checked = false;
            // menuItemDockLogTop.Checked = false;
            // menuItemDockLogBottom.Checked = true;
            
            Logger.Instance.Info("日志面板已停靠到底部");
        }

        /// <summary>
        /// 全局筛选设置菜单项点击事件
        /// </summary>
        private void menuItemGlobalFilter_Click(object sender, EventArgs e)
        {
            Logger.Instance.Info("全局筛选设置菜单项被点击");
            MessageBox.Show("筛选功能已禁用（FilterSettingsDialog已删除）", "功能已禁用", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // FilterSettingsDialog已删除，功能已禁用
            // using (FilterSettingsDialog dialog = new FilterSettingsDialog(globalFilterSettings))
            // {
            //     if (dialog.ShowDialog(this) == DialogResult.OK)
            //     {
            //         globalFilterSettings = dialog.FilterSettings;
            //         ApplyGlobalFilterToAllBoards();
            //     }
            // }
        }
        
        /// <summary>
        /// 深色主题菜单点击事件
        /// </summary>
        private void menuItemDarkTheme_Click(object sender, EventArgs e)
        {
            // ThemeManager已删除，功能已禁用
            MessageBox.Show("主题切换功能已禁用（ThemeManager已删除）", "功能已禁用", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // ThemeManager.SwitchTheme(ThemeManager.ThemeType.Dark);
            // ThemeManager.ApplyTheme(this);
            // RefreshAllBoardPanels();
        }

        /// <summary>
        /// 浅色主题菜单点击事件
        /// </summary>
        private void menuItemLightTheme_Click(object sender, EventArgs e)
        {
            // ThemeManager已删除，功能已禁用
            MessageBox.Show("主题切换功能已禁用（ThemeManager已删除）", "功能已禁用", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // ThemeManager.SwitchTheme(ThemeManager.ThemeType.Light);
            // ThemeManager.ApplyTheme(this);
            // RefreshAllBoardPanels();
        }

        /// <summary>
        /// 刷新所有板块面板（用于主题切换后的界面更新）- 已禁用
        /// </summary>
        private void RefreshAllBoardPanels()
        {
            // ThemeManager已删除，功能已禁用
            // try
            // {
            //     ThemeManager.ThemeColors colors = ThemeManager.CurrentColors;
            //     foreach (var panel in boardPanels)
            //     {
            //         if (panel != null && !panel.IsDisposed)
            //         {
            //             panel.BackColor = colors.BackgroundPrimary;
            //             panel.Invalidate();
            //             panel.Refresh();
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     Logger.Instance.Error(string.Format("刷新板块面板失败: {0}", ex.Message));
            // }
        }

        /// <summary>
        /// 应用全局筛选到所有板块 - 已禁用
        /// </summary>
        private void ApplyGlobalFilterToAllBoards()
        {
            // FilterSettings和StockBoardPanel已删除，功能已禁用
            // try
            // {
            //     if (boardPanels == null || boardPanels.Count == 0)
            //     {
            //         Logger.Instance.Warning("没有板块需要应用筛选");
            //         return;
            //     }
            //     // ... 其余代码已禁用
            // }
            // catch (Exception ex)
            // {
            //     Logger.Instance.Error(string.Format("应用全局筛选失败: {0}", ex.Message));
            // }
        }

        /// <summary>
        /// 加载板块配置
        /// </summary>
        private void LoadBoards()
        {
            // StockBoardPanel和BoardManager已删除，功能已禁用
            Logger.Instance.Warning("加载板块配置功能已禁用（StockBoardPanel和BoardManager已删除）");
            return;
            /*
            try
            {
                Logger.Instance.Info("=== 开始加载板块配置 ===");
                
                // string configFilePath = boardManager.ConfigFilePath;  // BoardManager已删除
                string configFilePath = "";  // BoardManager已删除
                bool configFileExists = false;  // BoardManager已删除，不再检查文件
                
                if (configFileExists)
                {
                    // 检查文件是否为空
                    FileInfo fileInfo = new FileInfo(configFilePath);
                    if (fileInfo.Length == 0)
                    {
                        Logger.Instance.Warning(string.Format("配置文件存在但为空: {0}，将重新初始化", configFilePath));
                        InitializeDefaultBoards();
                        return;
                    }
                    
                    Logger.Instance.Info(string.Format("配置文件存在: {0} (大小: {1} 字节)", configFilePath, fileInfo.Length));
                }
                else
                {
                    Logger.Instance.Info(string.Format("配置文件不存在: {0}，将重新初始化", configFilePath));
                    InitializeDefaultBoards();
                    return;
                }
                
                // 尝试加载配置 - BoardConfig和BoardManager已删除，功能已禁用
                // Logger.Instance.Info("调用 boardManager.LoadBoards() 开始加载配置...");
                // var configs = boardManager.LoadBoards();  // BoardManager已删除
                var configs = (List<object>)null;  // BoardConfig已删除，使用object代替
                
                // BoardConfig和BoardManager已删除，所有相关代码已禁用
                Logger.Instance.Warning("将使用默认初始化（30个板块：板块1到板块30），这将覆盖现有配置！");
                InitializeDefaultBoards();
                return;
                /*
                Logger.Instance.Info(string.Format("配置加载结果: configs={0}, Count={1}", configs != null ? "非null" : "null", (configs != null ? configs.Count : 0)));
                
                bool shouldUseConfig = false;
                if (configs != null && configs.Count > 0)
                {
                    Logger.Instance.Info(string.Format("配置文件包含 {0} 个板块，将使用配置文件加载", configs.Count));
                    shouldUseConfig = true;
                }
                else
                {
                    if (configs == null)
                    {
                        Logger.Instance.Warning("boardManager.LoadBoards() 返回 null");
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("配置文件存在但数据为空（空数组[]，Count={0}），将使用默认初始化", configs.Count));
                    }
                    shouldUseConfig = false;
                }
                
                if (!shouldUseConfig)
                {
                    Logger.Instance.Warning("将使用默认初始化（30个板块：板块1到板块30），这将覆盖现有配置！");
                    InitializeDefaultBoards();
                    return;
                }
                
                if (boardPanels != null && boardPanels.Count > 0)
                {
                    Logger.Instance.Info(string.Format("清空现有 {0} 个板块，准备加载新配置", boardPanels.Count));
                    foreach (var panel in boardPanels)
                    {
                        if (panel != null)
                        {
                            panel.Dispose();
                        }
                    }
                    boardPanels.Clear();
                    
                    if (boardContainer != null)
                    {
                        boardContainer.Controls.Clear();
                    }
                }
                
                if (configs != null && configs.Count > 0)
                {
                    Logger.Instance.Info(string.Format("配置文件数据有效，开始加载 {0} 个板块配置...", configs.Count));
                    foreach (var config in configs)
                    {
                        if (config == null)
                        {
                            Logger.Instance.Warning("发现 null 配置，跳过");
                            continue;
                        }
                        
                        CreateBoardFromConfig(config);
                        int stockCount = (config.StockCodes != null) ? config.StockCodes.Count : 0;
                        Logger.Instance.Info(string.Format("已加载板块: [{0}]，包含 {1} 只股票", config.Name, stockCount));
                    }
                    
                    int totalStocks = configs.Sum(c => (c.StockCodes != null) ? c.StockCodes.Count : 0);
                    Logger.Instance.Success(string.Format("=== 板块配置加载完成: 共 {0} 个板块，总计 {1} 只股票 ===", boardPanels.Count, totalStocks));
                }
                else
                {
                    Logger.Instance.Info("配置文件为空，不创建任何板块");
                }
                
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        RefreshBoardContainerLayout();
                    }));
                }
                else
                {
                    this.HandleCreated += (s, e) =>
                    {
                        RefreshBoardContainerLayout();
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载板块配置失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                Logger.Instance.Info("加载失败，将重新初始化");
                InitializeDefaultBoards();
            }
            */
        }
        
        /// <summary>
        /// 初始化默认板块 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void InitializeDefaultBoards()
        {
            // StockBoardPanel已删除，功能已禁用
            Logger.Instance.Warning("初始化默认板块功能已禁用（StockBoardPanel已删除）");
            return;
            /*
            try
            {
                Logger.Instance.Info("=== 开始初始化默认板块 ===");
                
                // 清空现有的板块（如果有）
                if (boardPanels != null && boardPanels.Count > 0)
                {
                    Logger.Instance.Info(string.Format("清空现有 {0} 个板块", boardPanels.Count));
                    foreach (var panel in boardPanels)
                    {
                        if (panel != null)
                        {
                            panel.Dispose();
                        }
                    }
                    boardPanels.Clear();
                    
                    // 清空容器
                    if (boardContainer != null)
                    {
                        boardContainer.Controls.Clear();
                    }
                }
                
                // 创建30个默认板块
                Logger.Instance.Info("创建30个默认板块");
                int defaultBoardCount = 30;
                for (int i = 1; i <= defaultBoardCount; i++)
                {
                    CreateBoard(string.Format("板块{0}", i));
                }
                
                // 创建后确保所有面板统一大小
                // 检查句柄是否已创建
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        // EnsureAllPanelsUniformSize(); // 已废弃 - FlowLayoutPanel自动处理大小
                        RefreshBoardContainerLayout();  // 刷新布局
                    }));
                }
                else
                {
                    // 句柄未创建，使用HandleCreated事件延迟执行
                    this.HandleCreated += (s, e) =>
                    {
                        // EnsureAllPanelsUniformSize(); // 已废弃 - FlowLayoutPanel自动处理大小
                        RefreshBoardContainerLayout();  // 刷新布局
                    };
                }
                
                // 注意：不要在这里自动保存，避免覆盖用户配置
                // 只有在用户明确操作时才保存
                // SaveBoards();
                
                Logger.Instance.Success(string.Format("=== 默认板块初始化完成: 共 {0} 个板块 ===", boardPanels.Count));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("初始化默认板块失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// 保存板块配置 - 已禁用（BoardConfig和BoardManager已删除）
        /// </summary>
        private void SaveBoards()
        {
            // BoardConfig和BoardManager已删除，功能已禁用
            Logger.Instance.Warning("保存板块配置功能已禁用（BoardConfig和BoardManager已删除）");
            return;
            /*
            try
            {
                Logger.Instance.Info("=== 开始保存板块配置 ===");
                
                if (boardPanels == null)
                {
                    Logger.Instance.Error("boardPanels 为 null，无法保存");
                    return;
                }
                
                if (boardPanels.Count == 0)
                {
                    Logger.Instance.Warning("boardPanels 为空，没有板块需要保存");
                    // boardManager.SaveBoards(new List<BoardConfig>());  // BoardConfig和BoardManager已删除
                    Logger.Instance.Info("已保存空配置到文件");
                    return;
                }
                
                Logger.Instance.Info(string.Format("准备保存 {0} 个板块", boardPanels.Count));
                // var configs = new List<BoardConfig>();  // BoardConfig已删除
                
                // StockBoardPanel已删除，以下代码已禁用
                // foreach (var panel in boardPanels)
                // {
                //     if (panel == null)
                //     {
                //         Logger.Instance.Warning("发现 null 面板，跳过");
                //         continue;
                //     }
                //     
                //     List<string> stockCodesList = panel.StockCodes ?? new List<string>();
                //     var stockNames = new Dictionary<string, string>();
                //     // ... 其余代码已禁用
                // }
                
                // 检查是否有配置需要保存
                // if (configs.Count == 0)  // BoardConfig已删除
                // {
                //     Logger.Instance.Warning("没有有效的板块配置需要保存");
                //     return;
                // }
                
                // 保存到文件
                // boardManager.SaveBoards(configs);  // BoardManager已删除
                
                // 详细记录保存的股票信息
                // int totalStocks = configs.Sum(c => c.StockCodes != null ? c.StockCodes.Count : 0);
                // Logger.Instance.Success(string.Format("=== 板块配置保存完成: {0} 个板块，共 {1} 只股票 ===", configs.Count, totalStocks));
                
                // 记录每个板块的股票数量
                // foreach (var config in configs)
                // {
                //     int stockCount = config.StockCodes != null ? config.StockCodes.Count : 0;
                //     Logger.Instance.Info(string.Format("  板块[{0}]: {1} 只股票", config.Name, stockCount));
                // }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("保存板块配置失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
            */
        }
        
        /// <summary>
        /// 创建新板块 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void CreateBoard(string boardName)
        {
            // StockBoardPanel已删除，功能已禁用
            Logger.Instance.Warning("创建板块功能已禁用（StockBoardPanel已删除）");
        }
        
        /// <summary>
        /// 从配置创建板块 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void CreateBoardFromConfig(object config)  // BoardConfig已删除，改为object
        {
            // StockBoardPanel已删除，功能已禁用
            Logger.Instance.Warning("从配置创建板块功能已禁用（StockBoardPanel已删除）");
        }
        
        /* 以下代码已禁用 - StockBoardPanel已删除
        private void CreateBoardFromConfig_Old(object config)  // BoardConfig已删除，改为object
        {
            try
            {
                if (config == null)
                {
                    Logger.Instance.Warning("配置为null，跳过创建板块");
                    return;
                }
                
                // BoardConfig已删除，以下代码已禁用
                // if (config.StockCodes == null)
                // {
                //     config.StockCodes = new List<string>();
                //     Logger.Instance.Warning(string.Format("板块[{0}]的StockCodes为null，已初始化为空列表", config.Name));
                // }
                // 
                // if (config.StockNames != null && config.StockNames.Count > 0)
                // {
                //     foreach (var kvp in config.StockNames)
                //     {
                //         dataManager.UpdateStockCodeTable(kvp.Key, kvp.Value);
                //     }
                //     Logger.Instance.Info(string.Format("已恢复 {0} 个股票名称到DataManager", config.StockNames.Count));
                // }
                // 
                // string boardNameToUse = config.Name;
                // StockBoardPanel已删除，以下代码已禁用
                // if (string.IsNullOrEmpty(boardNameToUse) || boardNameToUse == "未命名板块")
                // {
                //     Logger.Instance.Warning(string.Format("配置中的板块名称为空或默认值: [{0}]，使用配置索引作为名称", boardNameToUse));
                //     int configIndex = boardPanels.Count + 1;
                //     boardNameToUse = string.Format("板块{0}", configIndex);
                // }
                // else if (boardNameToUse == "板块1" && boardPanels.Count > 0)
                // {
                //     int configIndex = boardPanels.Count + 1;
                //     boardNameToUse = string.Format("板块{0}", configIndex);
                //     Logger.Instance.Warning(string.Format("配置中的板块名称为默认值'板块1'，但已有 {0} 个板块，使用生成名称: [{1}]", boardPanels.Count, boardNameToUse));
                // }
                // 
                // Logger.Instance.Info(string.Format("从配置创建板块: 配置名称=[{0}], 将使用名称=[{1}]", config.Name, boardNameToUse));
                // 
                // StockBoardPanel panel = new StockBoardPanel(boardNameToUse, dataManager);
                // 
                // panel.BoardNameChanged += Panel_BoardNameChanged;
                // panel.BoardNameValidating += Panel_BoardNameValidating;
                // panel.StockListChanged += Panel_StockListChanged;
                // panel.PanelSizeChanged += Panel_SizeChanged;
                // panel.DeleteBoardRequested += Panel_DeleteBoardRequested;
                // 
                // List<string> stockCodesToSet = config.StockCodes ?? new List<string>();
                // panel.SetStockCodes(stockCodesToSet, false);
                // 
                // if (panel.BoardName != boardNameToUse)
                // {
                //     panel.BoardName = boardNameToUse;
                // }
                // 
                // int panelStockCount = panel.StockCodes != null ? panel.StockCodes.Count : 0;
                // Logger.Instance.Info(string.Format("板块[{0}]设置完成: 面板中股票数量={1}", boardNameToUse, panelStockCount));
                // 
                // boardPanels.Add(panel);
                // AddBoardToContainer(panel);
                // 
                // if (panel.IsHandleCreated)
                // {
                //     panel.BeginInvoke(new Action(() =>
                //     {
                //         if (panel != null && !panel.IsDisposed)
                //         {
                //         }
                //     }));
                // }
                // else
                // {
                //     panel.HandleCreated += (s, e) =>
                //     {
                //         if (panel != null && !panel.IsDisposed)
                //         {
                //         }
                //     };
                // }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("从配置创建板块失败: {0}", ex.Message));
            }
        }
        */
        
        /* 以下方法已废弃 - 改用FlowLayoutPanel后不再需要
        /// <summary>
        /// 确保所有面板使用统一的大小（用于初始化）
        /// </summary>
        private void EnsureAllPanelsUniformSize()
        {
            if (boardContainer == null || boardPanels.Count == 0)
                return;
            
            try
            {
                // 计算统一的单元格大小（基于TableLayoutPanel的可用空间）
                int availableWidth = boardContainer.Width - boardContainer.Padding.Left - boardContainer.Padding.Right;
                int availableHeight = boardContainer.Height - boardContainer.Padding.Top - boardContainer.Padding.Bottom;
                
                int uniformCellWidth = availableWidth / boardContainer.ColumnCount;
                int uniformCellHeight = availableHeight / boardContainer.RowCount;
                
                // 统计有多少面板没有保存的大小（Width或Height为0）
                int panelsWithoutSize = 0;
                foreach (var panel in boardPanels)
                {
                    if (panel.Width == 0 || panel.Height == 0)
                    {
                        panelsWithoutSize++;
                    }
                }
                
                // 如果有面板没有保存的大小，统一设置
                if (panelsWithoutSize > 0)
                {
                    Logger.Instance.Info(string.Format("统一设置 {0} 个面板的初始大小", panelsWithoutSize));
                    
                    foreach (var panel in boardPanels)
                    {
                        // 只对没有保存大小的面板设置统一大小
                        if (panel.Width == 0 || panel.Height == 0)
                        {
                            EnsureUniformPanelSize(panel, uniformCellWidth, uniformCellHeight);
                        }
                    }
                    
                    // 触发布局更新
                    boardContainer.PerformLayout();
                    boardContainer.Invalidate(true);
                    boardContainer.Update();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("确保所有面板统一大小失败: {0}", ex.Message));
            }
        }

        /* 以下方法已废弃 - 改用FlowLayoutPanel后不再需要
        /// <summary>
        /// 确保面板使用统一的大小（用于初始化）
        /// </summary>
        private void EnsureUniformPanelSize(StockBoardPanel panel)
        {
            if (boardContainer == null || panel == null)
                return;

            try
            {
                // 计算统一的单元格大小（基于TableLayoutPanel的可用空间）
                int availableWidth = boardContainer.Width - boardContainer.Padding.Left - boardContainer.Padding.Right;
                int availableHeight = boardContainer.Height - boardContainer.Padding.Top - boardContainer.Padding.Bottom;

                int uniformCellWidth = availableWidth / boardContainer.ColumnCount;
                int uniformCellHeight = availableHeight / boardContainer.RowCount;

                EnsureUniformPanelSize(panel, uniformCellWidth, uniformCellHeight);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("确保统一面板大小失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 确保面板使用指定的大小（用于初始化）
        /// </summary>
        private void EnsureUniformPanelSize(object panel, int cellWidth, int cellHeight)  // StockBoardPanel已删除，改为object
        {
            if (boardContainer == null || panel == null)
                return;
            
            try
            {
                TableLayoutPanelCellPosition position = boardContainer.GetPositionFromControl(panel);
                if (position.Row >= 0 && position.Column >= 0)
                {
                    // 考虑Margin，计算面板实际大小
                    int panelWidth = cellWidth - panel.Margin.Left - panel.Margin.Right;
                    int panelHeight = cellHeight - panel.Margin.Top - panel.Margin.Bottom;
                    
                    // 设置面板大小（确保最小尺寸）
                    if (panelWidth > 0 && panelHeight > 0)
                    {
                        panel.Size = new Size(Math.Max(150, panelWidth), Math.Max(100, panelHeight));
                    }
                    
                    // 更新TableLayoutPanel中对应列和行的大小（使用统一大小）
                    // 更新列宽（使用绝对大小）
                    if (boardContainer.ColumnStyles[position.Column].SizeType != SizeType.Absolute)
                    {
                        boardContainer.ColumnStyles[position.Column] = new ColumnStyle(SizeType.Absolute, cellWidth);
                    }
                    else
                    {
                        // 如果已经是绝对大小，检查是否需要更新为统一大小
                        // 只在第一次初始化时统一大小（差异大于10像素才更新）
                        if (Math.Abs(boardContainer.ColumnStyles[position.Column].Width - cellWidth) > 10)
                        {
                            boardContainer.ColumnStyles[position.Column].Width = cellWidth;
                        }
                    }
                    
                    // 更新行高（使用绝对大小）
                    if (boardContainer.RowStyles[position.Row].SizeType != SizeType.Absolute)
                    {
                        boardContainer.RowStyles[position.Row] = new RowStyle(SizeType.Absolute, cellHeight);
                    }
                    else
                    {
                        // 如果已经是绝对大小，检查是否需要更新为统一大小
                        // 只在第一次初始化时统一大小（差异大于10像素才更新）
                        if (Math.Abs(boardContainer.RowStyles[position.Row].Height - cellHeight) > 10)
                        {
                            boardContainer.RowStyles[position.Row].Height = cellHeight;
                        }
                    }
                    
                    // 触发布局更新
                    boardContainer.PerformLayout();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("确保统一面板大小失败: {0}", ex.Message));
            }
        }
        */ // 结束废弃方法块

        /* 已废弃 - FlowLayoutPanel不需要此方法
        /// <summary>
        /// 更新TableLayoutPanel中面板对应列和行的大小
        /// </summary>
        private void UpdatePanelSizeInTableLayout(object panel, int width, int height)  // StockBoardPanel已删除，改为object
        {
            if (boardContainer == null || panel == null)
                return;

            try
            {
                TableLayoutPanelCellPosition position = boardContainer.GetPositionFromControl(panel);
                if (position.Row >= 0 && position.Column >= 0)
                {
                    // 计算新的列宽和行高（考虑 Margin）
                    int newWidth = width + panel.Margin.Left + panel.Margin.Right;
                    int newHeight = height + panel.Margin.Top + panel.Margin.Bottom;

                    // 更新列宽（使用绝对大小）
                    if (boardContainer.ColumnStyles[position.Column].SizeType != SizeType.Absolute)
                    {
                        boardContainer.ColumnStyles[position.Column] = new ColumnStyle(SizeType.Absolute, newWidth);
                    }
                    else
                    {
                        boardContainer.ColumnStyles[position.Column].Width = newWidth;
                    }

                    // 更新行高（使用绝对大小）
                    if (boardContainer.RowStyles[position.Row].SizeType != SizeType.Absolute)
                    {
                        boardContainer.RowStyles[position.Row] = new RowStyle(SizeType.Absolute, newHeight);
                    }
                    else
                    {
                        boardContainer.RowStyles[position.Row].Height = newHeight;
                    }

                    // 触发布局更新
                    boardContainer.PerformLayout();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("更新面板大小失败: {0}", ex.Message));
            }
        }
        */

        /// <summary>
        /// 将板块添加到容器 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void AddBoardToContainer(object panel)  // StockBoardPanel已删除，改为object
        {
            // StockBoardPanel已删除，功能已禁用
            // if (boardContainer == null)
            //     return;
            // panel.Margin = new Padding(0);
            // panel.Dock = DockStyle.Fill;
            // RecalculateLayout();
        }
        
        /// <summary>
        /// 板块名称验证事件（检查名称是否重复）- 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void Panel_BoardNameValidating(object sender, EventArgs e)  // StockBoardPanel已删除，改为EventArgs
        {
            // StockBoardPanel已删除，功能已禁用
            // EventArgs没有CurrentPanel和NewName属性，功能已禁用
        }
        
        /// <summary>
        /// 板块名称改变事件（自动保存）- 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void Panel_BoardNameChanged(object sender, EventArgs e)  // StockBoardPanel已删除，改为EventArgs
        {
            // StockBoardPanel已删除，功能已禁用
            // EventArgs没有NewName属性，功能已禁用
        }
        
        /// <summary>
        /// 股票列表改变事件（自动保存）- 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void Panel_StockListChanged(object sender, EventArgs e)
        {
            // StockBoardPanel已删除，功能已禁用
        }

        /// <summary>
        /// 删除板块请求事件处理 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void Panel_DeleteBoardRequested(object sender, EventArgs e)
        {
            // StockBoardPanel已删除，功能已禁用
            Logger.Instance.Warning("删除板块功能已禁用（StockBoardPanel已删除）");
            return;
            /*
            try
            {
                StockBoardPanel panel = sender as StockBoardPanel;
                if (panel == null)
                {
                    Logger.Instance.Warning("删除板块请求的发送者不是 StockBoardPanel");
                    return;
                }

                string boardName = panel.BoardName;
                Logger.Instance.Info(string.Format("开始删除板块: {0}", boardName));

                // 从容器中移除面板
                if (boardContainer != null && boardContainer.Controls.Contains(panel))
                {
                    boardContainer.Controls.Remove(panel);
                }

                // 从列表中移除
                if (boardPanels.Contains(panel))
                {
                    boardPanels.Remove(panel);
                }

                // 取消订阅事件
                panel.BoardNameChanged -= Panel_BoardNameChanged;
                panel.BoardNameValidating -= Panel_BoardNameValidating;
                panel.StockListChanged -= Panel_StockListChanged;
                panel.PanelSizeChanged -= Panel_SizeChanged;
                panel.DeleteBoardRequested -= Panel_DeleteBoardRequested;

                // 释放资源
                panel.Dispose();

                Logger.Instance.Info(string.Format("已从容器和列表中移除板块: {0}", boardName));

                // 动态重新计算列数和行数，所有面板重新排列（从左到右，从上到下）
                RecalculateLayout();

                // 保存配置
                // SaveBoards();  // SaveBoards方法已禁用

                Logger.Instance.Success(string.Format("板块 [{0}] 删除完成，布局已更新并保存", boardName));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("删除板块失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
            */
        }

        /// <summary>
        /// 面板大小改变事件 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void Panel_SizeChanged(object sender, EventArgs e)
        {
            // SaveBoards已删除，功能已禁用
        }
        
        /// <summary>
        /// 创建板块菜单项点击事件 - 已禁用（StockBoardPanel已删除）
        /// </summary>
        private void menuItemCreateBoard_Click(object sender, EventArgs e)
        {
            // StockBoardPanel已删除，功能已禁用
            Logger.Instance.Warning("创建板块功能已禁用（StockBoardPanel已删除）");
        }
        
        /// <summary>
        /// 显示/隐藏F10面板菜单项点击事件
        /// </summary>
        /* // F10功能已禁用
        private void menuItemShowF10Panel_Click(object sender, EventArgs e)
        {
            bool isChecked = menuItemShowF10Panel.Checked;
            panelF10.Visible = isChecked;

            // 根据F10面板所在位置，折叠相应的Panel
            if (splitContainerRight.Panel1.Controls.Contains(panelF10))
            {
                splitContainerRight.Panel1Collapsed = !isChecked;
            }
            else if (splitContainerRight.Panel2.Controls.Contains(panelF10))
            {
                splitContainerRight.Panel2Collapsed = !isChecked;
            }

            Logger.Instance.Info("F10面板已{(isChecked ? "显示" : "隐藏")}");
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// F10面板停靠顶部菜单项点击事件
        /// </summary>
        private void menuItemDockF10Top_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool f10InPanel1 = splitContainerRight.Panel1.Controls.Contains(panelF10);

            if (!f10InPanel1)
            {
                // 如果F10面板在Panel2，交换它们
                splitContainerRight.Panel1.Controls.Clear();
                splitContainerRight.Panel2.Controls.Clear();
                splitContainerRight.Panel1.Controls.Add(panelF10);
                splitContainerRight.Panel2.Controls.Add(panelLog);
            }

            // 更新菜单状态
            menuItemDockF10Top.Checked = true;
            menuItemDockF10Bottom.Checked = false;

            Logger.Instance.Info("F10面板已停靠到顶部");
        }
        */
        
        /* // F10功能已禁用
        /// <summary>
        /// F10面板停靠底部菜单项点击事件
        /// </summary>
        private void menuItemDockF10Bottom_Click(object sender, EventArgs e)
        {
            // 检查当前面板位置
            bool f10InPanel1 = splitContainerRight.Panel1.Controls.Contains(panelF10);

            if (f10InPanel1)
            {
                // 如果F10面板在Panel1，交换它们
                splitContainerRight.Panel1.Controls.Clear();
                splitContainerRight.Panel2.Controls.Clear();
                splitContainerRight.Panel1.Controls.Add(panelLog);
                splitContainerRight.Panel2.Controls.Add(panelF10);
            }

            // 更新菜单状态
            menuItemDockF10Top.Checked = false;
            menuItemDockF10Bottom.Checked = true;

            Logger.Instance.Info("F10面板已停靠到底部");
        }
        */

        #endregion

    }

    public class Demo
    {
        private int m_hWnd = 0;

        public Demo(int hWnd)
        {
            m_hWnd = hWnd;
        }

        private const int WM_USER = 0x0400;
        public static int MY_MSG_BEGIN = WM_USER + 100;
        public static int MY_MSG_END = WM_USER + 101;

        [DllImport("User32.DLL")]
        public static extern int SendMessage(int hWnd, int Msg, int wParam, int lParam);

        public void Test()
        {
            SendMessage(m_hWnd, MY_MSG_BEGIN, 0, 0);
            for (int i = 0; i < 100000; i++)
            {
                Application.DoEvents();
            }
            SendMessage(m_hWnd, MY_MSG_END, 0, 0);
        }
    }
}