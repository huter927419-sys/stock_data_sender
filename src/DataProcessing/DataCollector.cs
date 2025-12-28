using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StockDataMQClient
{
    /// <summary>
    /// 数据采集器 - 负责从龙卷风软件采集数据
    /// </summary>
    public class DataCollector
    {
        [DllImport("Kernel32")]
        private static extern int LoadLibrary(string funcname);
        [DllImport("Kernel32")]
        private static extern int GetProcAddress(int handle, string funcname);
        [DllImport("Kernel32")]
        private static extern int FreeLibrary(int handle);

        private static int LoadLibraryNative(string dllPath)
        {
            return LoadLibrary(dllPath);
        }

        private static void FreeLibraryNative(int handle)
        {
            FreeLibrary(handle);
        }

        private IntPtr dllHandle;
        private IntPtr formHandle;
        private bool isCollecting = false;

        // DLL函数委托 - 核心函数
        public delegate int Stock_Init(IntPtr nHwnd, int nMsg, int nWorkMode);
        public delegate int Stock_Quit(IntPtr nHwnd);
        public delegate int GetStockDrvInfo(int nInfo, IntPtr pBuf);
        public delegate int SetupReceiver(bool bShowWindow);
        public delegate int ReInitStockInfo();
        
        // DLL函数委托 - 扩展API函数
        public delegate int AskStockDay(string pszStockCode, int nTimePeriod);  // 取日线数据
        public delegate int AskStockMn5(string pszStockCode, int nTimePeriod);  // 取5分钟数据
        public delegate int AskStockMin(string pszStockCode);                   // 取分钟数据
        // F10功能已禁用 - 取个股资料
        // public delegate int AskStockBase(string pszStockCode);                  // 取个股资料F10
        public delegate int AskStockNews();                                     // 取财经新闻
        public delegate int AskStockHalt();                                     // 中止补数
        public delegate int AskStockPRP(string pszStockCode);                    // 取分笔数据
        public delegate int AskStockPwr();                                      // 取除权数据
        public delegate int AskStockFin();                                      // 取财务数据

        // 函数指针（仅初始化已使用的函数）
        private Stock_Init stockInitProc;
        private Stock_Quit stockQuitProc;
        private SetupReceiver setupReceiverProc;
        private AskStockDay askStockDayProc;
        private AskStockMn5 askStockMn5Proc;
        private AskStockMin askStockMinProc;
        // F10功能已禁用
        // private AskStockBase askStockBaseProc;
        private AskStockHalt askStockHaltProc;

        // 采集状态
        public bool IsCollecting
        {
            get { return isCollecting; }
        }

        // 采集事件
        public event EventHandler<CollectionStatusEventArgs> CollectionStatusChanged;
        public event EventHandler<BasicDataCollectedEventArgs> BasicDataCollected;
        public event EventHandler<MinuteDataCollectedEventArgs> MinuteDataCollected;

        /// <summary>
        /// 初始化连接（加载DLL并初始化）
        /// </summary>
        public bool Initialize(IntPtr hWnd, string dllPath)
        {
            try
            {
                Logger.Instance.Info("=== 开始初始化数据采集器 ===");
                Logger.Instance.Info(string.Format("窗口句柄: {0}", hWnd.ToInt64()));
                
                formHandle = hWnd;

                // 查找DLL路径
                if (string.IsNullOrEmpty(dllPath))
                {
                    Logger.Instance.Info("正在查找StockDrv.dll...");
                    dllPath = FindDllPath();
                }

                if (string.IsNullOrEmpty(dllPath))
                {
                    Logger.Instance.Error("未找到StockDrv.dll");
                    Logger.Instance.Error("查找路径：");
                    Logger.Instance.Error("  1. 注册表: HKEY_LOCAL_MACHINE\\SOFTWARE\\StockDrv\\Driver");
                    Logger.Instance.Error(string.Format("  2. 程序目录: {0}\\StockDrv.dll", System.Windows.Forms.Application.StartupPath));
                    OnStatusChanged("未找到StockDrv.dll", false);
                    return false;
                }
                
                Logger.Instance.Success(string.Format("找到DLL: {0}", dllPath));

                // 加载DLL
                Logger.Instance.Info("正在加载DLL...");
                dllHandle = new IntPtr(LoadLibraryNative(dllPath));
                if (dllHandle == IntPtr.Zero)
                {
                    int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    Logger.Instance.Error(string.Format("加载DLL失败，错误代码: {0}", errorCode));
                    Logger.Instance.Error(string.Format("DLL路径: {0}", dllPath));
                    OnStatusChanged("加载DLL失败", false);
                    return false;
                }
                
                Logger.Instance.Success(string.Format("DLL加载成功，句柄: {0}", dllHandle.ToInt64()));

                // 注意：不在这里分析DLL，避免重复加载DLL干扰初始化
                // DLL分析可以延迟到初始化成功后再进行，或者完全移除
                // 参考C212版本：直接获取函数地址，不做额外分析

                // 获取函数地址（参考VC版本的GetAdress方法）
                Logger.Instance.Info("正在获取DLL函数地址...");
                // 核心函数（必需）
                stockInitProc = (Stock_Init)GetAddress(dllHandle.ToInt32(), "Stock_Init", typeof(Stock_Init));
                if (stockInitProc == null)
                {
                    Logger.Instance.Error("获取Stock_Init函数地址失败");
                }
                else
                {
                    Logger.Instance.Success("Stock_Init函数地址获取成功");
                }
                
                stockQuitProc = (Stock_Quit)GetAddress(dllHandle.ToInt32(), "Stock_Quit", typeof(Stock_Quit));
                if (stockQuitProc == null)
                {
                    Logger.Instance.Warning("Stock_Quit函数地址获取失败（可能不需要）");
                }
                else
                {
                    Logger.Instance.Success("Stock_Quit函数地址获取成功");
                }
                
                setupReceiverProc = (SetupReceiver)GetAddress(dllHandle.ToInt32(), "SetupReceiver", typeof(SetupReceiver));
                if (setupReceiverProc == null)
                {
                    Logger.Instance.Warning("SetupReceiver函数地址获取失败（可能不需要）");
                }
                else
                {
                    Logger.Instance.Success("SetupReceiver函数地址获取成功");
                }
                
                // 扩展函数（可能不存在，允许为null，但需要尝试加载）
                // 参考VC版本：m_pfnAskStockMin = (int (WINAPI *)(char*)) GetProcAddress(m_hSTKDrv,"AskStockMin");
                Logger.Instance.Info("正在获取扩展函数地址...");
                askStockDayProc = (AskStockDay)GetAddress(dllHandle.ToInt32(), "AskStockDay", typeof(AskStockDay));
                if (askStockDayProc != null)
                {
                    Logger.Instance.Success("AskStockDay函数地址获取成功");
                }
                else
                {
                    Logger.Instance.Warning("AskStockDay函数地址获取失败（可能不需要）");
                }
                
                askStockMn5Proc = (AskStockMn5)GetAddress(dllHandle.ToInt32(), "AskStockMn5", typeof(AskStockMn5));
                if (askStockMn5Proc != null)
                {
                    Logger.Instance.Success("AskStockMn5函数地址获取成功");
                }
                else
                {
                    Logger.Instance.Warning("AskStockMn5函数地址获取失败（可能不需要）");
                }
                
                askStockMinProc = (AskStockMin)GetAddress(dllHandle.ToInt32(), "AskStockMin", typeof(AskStockMin));
                if (askStockMinProc != null)
                {
                    Logger.Instance.Success("AskStockMin函数地址获取成功");
                }
                else
                {
                    Logger.Instance.Warning("AskStockMin函数地址获取失败（可能不需要）");
                }
                
                // F10功能已禁用
                // askStockBaseProc = (AskStockBase)GetAddress(dllHandle.ToInt32(), "AskStockBase", typeof(AskStockBase));
                // if (askStockBaseProc != null)
                // {
                //     Logger.Instance.Success("AskStockBase函数地址获取成功");
                // }
                // else
                // {
                //     Logger.Instance.Warning("AskStockBase函数地址获取失败（可能不需要）");
                // }
                
                askStockHaltProc = (AskStockHalt)GetAddress(dllHandle.ToInt32(), "AskStockHalt", typeof(AskStockHalt));
                if (askStockHaltProc != null)
                {
                    Logger.Instance.Success("AskStockHalt函数地址获取成功");
                }
                else
                {
                    Logger.Instance.Warning("AskStockHalt函数地址获取失败（可能不需要）");
                }

                // 检测并记录所有可用函数（输出到日志）
                Logger.Instance.Info("=== 开始检测DLL中所有可用的函数 ===");
                CheckAvailableFunctions();

                if (stockInitProc == null)
                {
                    Logger.Instance.Error("Stock_Init函数未找到，无法初始化");
                    OnStatusChanged("获取Stock_Init函数失败", false);
                    return false;
                }

                // 初始化股票接收
                // 注意：c212项目会调用SetupReceiver来启动龙卷风软件，然后调用Stock_Init连接
                // 正确的顺序：先调用SetupReceiver启动龙卷风软件，然后调用Stock_Init连接
                
                // 1. 先尝试调用SetupReceiver启动龙卷风软件（如果可用）
                // 注意：这是启动龙卷风软件的关键步骤，c212项目调用了此函数
                if (setupReceiverProc != null)
                {
                    try
                    {
                        Logger.Instance.Info("正在调用SetupReceiver启动龙卷风软件...");
                        int setupResult = setupReceiverProc(true);  // true表示显示窗口
                        if (setupResult == 1)
                        {
                            Logger.Instance.Success("SetupReceiver调用成功，龙卷风软件已启动");
                            // 给一点时间让龙卷风软件完全启动
                            System.Threading.Thread.Sleep(500);
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("SetupReceiver调用返回: {0}（可能龙卷风软件已在运行）", setupResult));
                        }
                    }
                    catch (Exception setupEx)
                    {
                        Logger.Instance.Warning(string.Format("SetupReceiver调用异常: {0}（可能不需要此步骤）", setupEx.Message));
                        Logger.Instance.Warning(string.Format("异常堆栈: {0}", setupEx.StackTrace));
                    }
                }
                else
                {
                    Logger.Instance.Warning("SetupReceiver函数不可用，无法自动启动龙卷风软件");
                    Logger.Instance.Warning("请手动启动龙卷风软件，然后重新连接");
                }
                
                // 2. 然后调用Stock_Init连接龙卷风软件
                Logger.Instance.Info("正在调用Stock_Init连接龙卷风软件...");
                Logger.Instance.Info(string.Format("参数: hWnd={0}, uMsg={1}, nWorkMode={2}", hWnd.ToInt64(), StockDrv.RCV_MSG_STKDATA, StockDrv.RCV_WORK_SENDMSG));
                
                // 参考C212版本：直接调用，不检查返回值（C212版本：Stock_InitProc(this.Handle, ...)）
                int result = stockInitProc(hWnd, StockDrv.RCV_MSG_STKDATA, StockDrv.RCV_WORK_SENDMSG);
                
                Logger.Instance.Info(string.Format("Stock_Init调用完成，返回值: {0}", result));
                
                // 参考C212版本：不检查返回值，直接认为成功
                // 但为了安全，我们记录返回值，如果明显失败（返回值 <= 0）才报错
                if (result <= 0)
                {
                    Logger.Instance.Warning(string.Format("Stock_Init返回值: {0}（可能表示失败，但继续执行）", result));
                    Logger.Instance.Warning("提示：如果数据接收不正常，请检查：");
                    Logger.Instance.Warning("  1. 龙卷风软件是否已启动");
                    Logger.Instance.Warning("  2. 龙卷风软件是否正常运行");
                    Logger.Instance.Warning("  3. DLL版本是否匹配");
                    // 注意：C212版本不检查返回值，所以这里也不返回false，继续执行
                }
                else
                {
                    Logger.Instance.Success(string.Format("Stock_Init调用成功，返回值: {0}", result));
                }
                
                Logger.Instance.Success("已调用Stock_Init，数据接收将自动开始（如果龙卷风软件已启动）");

                OnStatusChanged("连接成功，已初始化数据接收", true);
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged(string.Format("初始化异常: {0}", ex.Message), false);
                return false;
            }
        }

        /// <summary>
        /// 获取函数地址的辅助方法
        /// </summary>
        private Delegate GetAddress(int dllModule, string functionname, Type t)
        {
            int addr = GetProcAddress(dllModule, functionname);
            if (addr == 0)
                return null;
            else
                return Marshal.GetDelegateForFunctionPointer(new IntPtr(addr), t);
        }

        /// <summary>
        /// 查找DLL路径
        /// </summary>
        private string FindDllPath()
        {
            string dllPath = "";

            // 先从注册表查找
            try
            {
                Microsoft.Win32.RegistryKey rsg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\StockDrv", true);
                if (rsg != null && rsg.GetValue("Driver") != null)
                {
                    dllPath = rsg.GetValue("Driver").ToString();
                    rsg.Close();
                }
            }
            catch { }

            // 如果注册表中没有，从程序目录查找
            if (string.IsNullOrEmpty(dllPath))
            {
                string appPath = Application.StartupPath;
                dllPath = System.IO.Path.Combine(appPath, "StockDrv.dll");
                if (!System.IO.File.Exists(dllPath))
                {
                    dllPath = null;
                }
            }

            return dllPath;
        }

        /// <summary>
        /// 开始采集基本数据（股票名称/代码）
        /// </summary>
        public void StartCollectBasicData()
        {
            if (stockInitProc != null)
            {
                // 基本数据（码表）会在RCV_MKTTBLDATA消息中自动接收
                // 这里可以主动请求码表数据（如果有相关API）
                Logger.Instance.Info("开始采集基本数据（码表）...");
                OnStatusChanged("开始采集基本数据（码表）...", true);
                isCollecting = true;
            }
            else
            {
                Logger.Instance.Warning("数据采集器未初始化，无法采集基本数据");
            }
        }

        /// <summary>
        /// 开始采集分钟数据
        /// </summary>
        public void StartCollectMinuteData(string stockCode)
        {
            // 检查DLL是否已加载
            if (dllHandle == IntPtr.Zero)
            {
                Logger.Instance.Error("DLL未加载，请先调用Initialize方法");
                throw new InvalidOperationException("DLL未加载，请先调用Initialize方法初始化连接");
            }
            
            // 检查AskStockMin函数是否已初始化
            if (askStockMinProc == null)
            {
                Logger.Instance.Error("AskStockMin函数未初始化");
                Logger.Instance.Error("可能原因：1. DLL版本不支持分钟数据功能 2. DLL中未找到AskStockMin函数");
                Logger.Instance.Error("请检查日志中的函数检测信息，确认AskStockMin是否可用");
                throw new InvalidOperationException("AskStockMin函数未初始化，无法请求分钟数据");
            }

            try
            {
                Logger.Instance.Info(string.Format("请求分钟数据: {0}", stockCode ?? "全部股票"));
                int result = askStockMinProc(stockCode ?? "");
                if (result == 1)
                {
                    Logger.Instance.Success(string.Format("开始采集分钟数据: {0}", stockCode ?? "全部股票"));
                    OnStatusChanged(string.Format("开始采集分钟数据: {0}", stockCode ?? "全部股票"), true);
                    isCollecting = true;
                }
                else
                {
                    Logger.Instance.Error(string.Format("请求分钟数据失败，返回值: {0}", result));
                    OnStatusChanged(string.Format("请求分钟数据失败，返回值: {0}", result), false);
                    throw new InvalidOperationException(string.Format("请求分钟数据失败，返回值: {0}", result));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("采集分钟数据异常: {0}", ex.Message));
                OnStatusChanged(string.Format("采集分钟数据异常: {0}", ex.Message), false);
                throw;
            }
        }

        /// <summary>
        /// 请求日线数据
        /// </summary>
        /// <param name="stockCode">股票代码（如 "SH600000" 或空字符串表示全部股票）</param>
        /// <param name="timePeriod">时间周期: 1=周, 2=月, 3=全部</param>
        /// <returns>是否请求成功</returns>
        public bool RequestDailyData(string stockCode, int timePeriod)
        {
            if (askStockDayProc == null)
            {
                Logger.Instance.Warning("AskStockDay函数未初始化，无法请求日线数据");
                return false;
            }

            try
            {
                string periodDesc = timePeriod == 1 ? "周" : (timePeriod == 2 ? "月" : "全部");
                Logger.Instance.Info(string.Format("请求日线数据: 股票={0}, 周期={1}({2})",
                    string.IsNullOrEmpty(stockCode) ? "全部" : stockCode, timePeriod, periodDesc));

                int result = askStockDayProc(stockCode ?? "", timePeriod);

                if (result == 1)
                {
                    Logger.Instance.Success(string.Format("日线数据请求成功: {0}",
                        string.IsNullOrEmpty(stockCode) ? "全部股票" : stockCode));
                    return true;
                }
                else
                {
                    Logger.Instance.Warning(string.Format("日线数据请求返回: {0}", result));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("请求日线数据异常: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 开始采集5分钟数据
        /// </summary>
        public void StartCollect5MinuteData(string stockCode, int timePeriod)
        {
            if (askStockMn5Proc != null)
            {
                try
                {
                    int result = askStockMn5Proc(stockCode ?? "", timePeriod);
                    if (result == 1)
                    {
                        OnStatusChanged(string.Format("开始采集5分钟数据: {0} (周期: {1})", stockCode ?? "全部股票", timePeriod), true);
                        isCollecting = true;
                    }
                    else
                    {
                        OnStatusChanged(string.Format("请求5分钟数据失败，返回值: {0}", result), false);
                    }
                }
                catch (Exception ex)
                {
                    OnStatusChanged(string.Format("采集5分钟数据异常: {0}", ex.Message), false);
                }
            }
        }

        /// <summary>
        /// 请求个股资料F10 - F10功能已禁用
        /// </summary>
        /// <param name="stockCode">股票代码（如 "SH600026"）</param>
        // F10功能已禁用 - 此方法已不再使用
        /*
        public bool RequestStockBaseData(string stockCode)
        {
            try
            {
                if (askStockBaseProc == null)
                {
                    Logger.Instance.Warning("AskStockBase函数未初始化，无法请求F10数据");
                    Logger.Instance.Warning("可能原因：1. DLL版本不支持F10数据功能 2. DLL中未找到AskStockBase函数");
                    OnStatusChanged("AskStockBase函数未初始化，无法请求F10数据", false);
                    return false;
                }

                if (string.IsNullOrEmpty(stockCode))
                {
                    Logger.Instance.Warning("股票代码为空，无法请求F10数据");
                    OnStatusChanged("股票代码为空，无法请求F10数据", false);
                    return false;
                }

                // 标准化股票代码
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);

                Logger.Instance.Info(string.Format("请求个股F10资料: {0}", normalizedCode));

                // 安全调用DLL函数
                int result = 0;
                try
                {
                    Logger.Instance.Info(string.Format("准备调用DLL函数AskStockBase，参数: {0}", normalizedCode));
                    long funcPtrAddr = 0;
                    if (askStockBaseProc != null && askStockBaseProc.Method != null)
                    {
                        funcPtrAddr = askStockBaseProc.Method.MethodHandle.GetFunctionPointer().ToInt64();
                    }
                    Logger.Instance.Info(string.Format("函数指针地址: {0}", funcPtrAddr));

                    result = askStockBaseProc(normalizedCode);

                    Logger.Instance.Info(string.Format("AskStockBase调用成功，返回值: {0}", result));
                }
                catch (AccessViolationException avEx)
                {
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10 DLL调用错误】访问冲突异常");
                    Logger.Instance.Error(string.Format("异常消息: {0}", avEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", avEx.StackTrace));
                    Logger.Instance.Error("可能原因：DLL函数指针无效或参数错误");
                    Logger.Instance.Error(string.Format("调用参数: {0}", normalizedCode));
                    Logger.Instance.Error("=".PadRight(80, '='));
                    OnStatusChanged("调用F10函数失败: 访问冲突", false);
                    return false;
                }
                catch (NullReferenceException nullEx)
                {
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10 DLL调用错误】空引用异常");
                    Logger.Instance.Error(string.Format("异常消息: {0}", nullEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", nullEx.StackTrace));
                    Logger.Instance.Error("可能原因：函数指针为null或DLL未正确加载");
                    Logger.Instance.Error("=".PadRight(80, '='));
                    OnStatusChanged("调用F10函数失败: 空引用", false);
                    return false;
                }
                catch (DllNotFoundException dllEx)
                {
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10 DLL调用错误】DLL未找到");
                    Logger.Instance.Error(string.Format("异常消息: {0}", dllEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", dllEx.StackTrace));
                    Logger.Instance.Error("=".PadRight(80, '='));
                    OnStatusChanged("DLL未找到", false);
                    return false;
                }
                catch (BadImageFormatException badImgEx)
                {
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10 DLL调用错误】DLL格式错误");
                    Logger.Instance.Error(string.Format("异常消息: {0}", badImgEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", badImgEx.StackTrace));
                    Logger.Instance.Error("可能原因：DLL架构不匹配（32位/64位）");
                    Logger.Instance.Error("=".PadRight(80, '='));
                    OnStatusChanged("调用F10函数失败: DLL格式错误", false);
                    return false;
                }
                catch (Exception callEx)
                {
                    Logger.Instance.Error("=".PadRight(80, '='));
                    Logger.Instance.Error("【F10 DLL调用错误】未知异常");
                    Logger.Instance.Error(string.Format("异常类型: {0}", callEx.GetType().FullName));
                    Logger.Instance.Error(string.Format("异常消息: {0}", callEx.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", callEx.StackTrace));
                    if (callEx.InnerException != null)
                    {
                        Logger.Instance.Error(string.Format("内部异常类型: {0}", callEx.InnerException.GetType().FullName));
                        Logger.Instance.Error(string.Format("内部异常消息: {0}", callEx.InnerException.Message));
                        Logger.Instance.Error(string.Format("内部异常堆栈: {0}", callEx.InnerException.StackTrace));
                    }
                    Logger.Instance.Error(string.Format("调用参数: {0}", normalizedCode));
                    Logger.Instance.Error("=".PadRight(80, '='));
                    OnStatusChanged(string.Format("调用F10函数异常: {0}", callEx.Message), false);
                    return false;
                }

                if (result == 1)
                {
                    OnStatusChanged(string.Format("已请求F10数据: {0}", normalizedCode), true);
                    Logger.Instance.Success(string.Format("F10数据请求成功: {0}", normalizedCode));
                    return true;
                }
                else
                {
                    OnStatusChanged(string.Format("请求F10数据失败，返回值: {0}", result), false);
                    Logger.Instance.Error(string.Format("F10数据请求失败: {0}, 返回值: {1}", normalizedCode, result));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("RequestStockBaseData异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                OnStatusChanged(string.Format("请求F10数据异常: {0}", ex.Message), false);
                return false;
            }
        }
        */

        /// <summary>
        /// 停止采集
        /// </summary>
        public void StopCollecting()
        {
            if (isCollecting && askStockHaltProc != null)
            {
                try
                {
                    askStockHaltProc();
                    isCollecting = false;
                    OnStatusChanged("已停止数据采集", false);
                }
                catch (Exception ex)
                {
                    OnStatusChanged(string.Format("停止采集异常: {0}", ex.Message), false);
                }
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                Logger.Instance.Info("=== 开始断开连接 ===");
                
                // 1. 先停止数据采集
                if (isCollecting)
                {
                    Logger.Instance.Info("正在停止数据采集...");
                    StopCollecting();
                    // 给一点时间让正在处理的消息完成
                    System.Threading.Thread.Sleep(100);
                }

                // 2. 调用Stock_Quit（参考C212版本：先调用Stock_Quit，再FreeLibrary）
                if (stockQuitProc != null && formHandle != IntPtr.Zero)
                {
                    Logger.Instance.Info("正在调用Stock_Quit...");
                    try
                    {
                        int quitResult = stockQuitProc(formHandle);
                        Logger.Instance.Info(string.Format("Stock_Quit返回值: {0}", quitResult));
                    }
                    catch (Exception quitEx)
                    {
                        Logger.Instance.Warning(string.Format("Stock_Quit调用异常: {0}", quitEx.Message));
                    }
                    // 给一点时间让DLL完成清理
                    System.Threading.Thread.Sleep(100);
                }

                // 3. 清空函数指针（避免在DLL卸载后调用）
                stockInitProc = null;
                stockQuitProc = null;
                setupReceiverProc = null;
                askStockDayProc = null;
                askStockMn5Proc = null;
                askStockMinProc = null;
                // F10功能已禁用
                // askStockBaseProc = null;
                askStockHaltProc = null;

                // 4. 卸载DLL（最后执行）
                if (dllHandle != IntPtr.Zero)
                {
                    Logger.Instance.Info("正在卸载DLL...");
                    try
                    {
                        FreeLibraryNative(dllHandle.ToInt32());
                        Logger.Instance.Info("DLL卸载成功");
                    }
                    catch (Exception freeEx)
                    {
                        Logger.Instance.Warning(string.Format("FreeLibrary异常: {0}", freeEx.Message));
                    }
                    dllHandle = IntPtr.Zero;
                }

                formHandle = IntPtr.Zero;
                OnStatusChanged("已断开连接", false);
                Logger.Instance.Info("=== 断开连接完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("断开连接异常: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                OnStatusChanged(string.Format("断开连接异常: {0}", ex.Message), false);
            }
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void OnStatusChanged(string message, bool isActive)
        {
            // 记录日志
            if (isActive)
            {
                Logger.Instance.Success(message);
            }
            else
            {
                Logger.Instance.Warning(message);
            }
            
            if (CollectionStatusChanged != null)
            {
                CollectionStatusChanged(this, new CollectionStatusEventArgs(message, isActive));
            }
        }

        /// <summary>
        /// 通知基本数据已采集
        /// </summary>
        public void NotifyBasicDataCollected(Dictionary<string, string> codeDict)
        {
            if (BasicDataCollected != null)
            {
                BasicDataCollected(this, new BasicDataCollectedEventArgs(codeDict));
            }
        }

        /// <summary>
        /// 通知分钟数据已采集
        /// </summary>
        public void NotifyMinuteDataCollected(string stockCode, List<MinuteData> minuteDataList)
        {
            if (MinuteDataCollected != null)
            {
                MinuteDataCollected(this, new MinuteDataCollectedEventArgs(stockCode, minuteDataList));
            }
        }
        
        /// <summary>
        /// 检测DLL中所有可用的函数
        /// </summary>
        private void CheckAvailableFunctions()
        {
            if (dllHandle == IntPtr.Zero)
                return;
                
            Logger.Instance.Info("=== 检测DLL中可用的函数 ===");
            
            // 定义所有可能的函数名称
            string[] functionNames = new string[]
            {
                "Stock_Init",           // 必需函数
                "Stock_Quit",           // 必需函数
                "GetStockDrvInfo",      // 获取驱动信息
                "SetupReceiver",        // 激活接收程序
                "ReInitStockInfo",      // 重新初始化（保留函数）
                "AskStockDay",          // 取日线数据
                "AskStockMn5",          // 取5分钟数据
                "AskStockMin",          // 取分钟数据
                // F10功能已禁用
                // "AskStockBase",         // 取个股资料F10
                "AskStockNews",         // 取财经新闻
                "AskStockHalt",         // 中止补数
                "AskStockPRP",          // 取分笔数据
                "AskStockPwr",          // 取除权数据
                "AskStockFin"           // 取财务数据
            };
            
            List<string> availableFunctions = new List<string>();
            List<string> missingFunctions = new List<string>();
            
            foreach (string funcName in functionNames)
            {
                int addr = GetProcAddress(dllHandle.ToInt32(), funcName);
                if (addr != 0)
                {
                    availableFunctions.Add(funcName);
                    Logger.Instance.Success(string.Format("✓ {0} - 可用", funcName));
                }
                else
                {
                    missingFunctions.Add(funcName);
                    Logger.Instance.Info(string.Format("✗ {0} - 未找到", funcName));
                }
            }
            
            Logger.Instance.Info(string.Format("=== 函数检测完成: 可用 {0} 个, 缺失 {1} 个 ===", availableFunctions.Count, missingFunctions.Count));
            
            // 特别提示关键函数的状态
            if (askStockMinProc == null)
            {
                Logger.Instance.Info("注意：AskStockMin函数未找到，分钟数据功能将不可用，但实时行情数据接收不受影响");
            }
            else
            {
                Logger.Instance.Success("AskStockMin函数可用，分钟数据功能正常");
            }
        }
    }

    /// <summary>
    /// 采集状态事件参数
    /// </summary>
    public class CollectionStatusEventArgs : EventArgs
    {
        private string _message;
        public string Message
        {
            get { return _message; }
            private set { _message = value; }
        }
        private bool _isActive;
        public bool IsActive
        {
            get { return _isActive; }
            private set { _isActive = value; }
        }

        public CollectionStatusEventArgs(string message, bool isActive)
        {
            Message = message;
            IsActive = isActive;
        }
    }

    /// <summary>
    /// 基本数据采集事件参数
    /// </summary>
    public class BasicDataCollectedEventArgs : EventArgs
    {
        private Dictionary<string, string> _codeDictionary;
        public Dictionary<string, string> CodeDictionary
        {
            get { return _codeDictionary; }
            private set { _codeDictionary = value; }
        }

        public BasicDataCollectedEventArgs(Dictionary<string, string> codeDict)
        {
            CodeDictionary = codeDict;
        }
    }

    /// <summary>
    /// 分钟数据采集事件参数
    /// </summary>
    public class MinuteDataCollectedEventArgs : EventArgs
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            private set { _stockCode = value; }
        }
        private List<MinuteData> _minuteDataList;
        public List<MinuteData> MinuteDataList
        {
            get { return _minuteDataList; }
            private set { _minuteDataList = value; }
        }

        public MinuteDataCollectedEventArgs(string stockCode, List<MinuteData> minuteDataList)
        {
            StockCode = stockCode;
            MinuteDataList = minuteDataList;
        }
    }
}

