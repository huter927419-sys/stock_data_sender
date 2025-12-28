using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;

namespace StockDataMQClient
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // 记录程序启动信息
                Logger.Instance.Info("=".PadRight(80, '='));
                Logger.Instance.Info("【程序启动】应用程序开始运行");
                Logger.Instance.Info(string.Format("启动时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                Logger.Instance.Info(string.Format("操作系统: {0}", Environment.OSVersion));
                Logger.Instance.Info(string.Format("CLR版本: {0}", Environment.Version));
                Logger.Instance.Info(string.Format("工作目录: {0}", Environment.CurrentDirectory));
                Logger.Instance.Info(string.Format("机器名: {0}", Environment.MachineName));
                Logger.Instance.Info(string.Format("用户名: {0}", Environment.UserName));
                Logger.Instance.Info(string.Format("是否64位进程: {0}", (IntPtr.Size == 8)));
                Logger.Instance.Info(string.Format("是否64位操作系统: {0}", (IntPtr.Size == 8)));
                Logger.Instance.Info("=".PadRight(80, '='));
                
                // 添加全局异常处理，防止未捕获的异常导致程序退出
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                
                Logger.Instance.Info("全局异常处理已启用");
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                Logger.Instance.Info("开始运行主窗体...");
                Application.Run(new Form1());
                
                // 正常退出
                Logger.Instance.Info("=".PadRight(80, '='));
                Logger.Instance.Info("【程序退出】应用程序正常退出");
                Logger.Instance.Info(string.Format("退出时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                Logger.Instance.Info("退出原因: 用户关闭主窗体或调用Application.Exit()");
                Logger.Instance.Info("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("=".PadRight(80, '='));
                Logger.Instance.Error("【程序启动失败】Main方法异常");
                Logger.Instance.Error(string.Format("异常类型: {0}", ex.GetType().FullName));
                Logger.Instance.Error(string.Format("异常消息: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈:\n{0}", ex.StackTrace));
                Logger.Instance.Error(string.Format("退出时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                Logger.Instance.Error("=".PadRight(80, '='));
                
                MessageBox.Show(string.Format("程序启动失败:\n\n{0}\n\n详细错误请查看日志文件。", ex.Message), 
                    "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// UI线程异常处理
        /// </summary>
        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                Logger.Instance.Error("=".PadRight(80, '='));
                Logger.Instance.Error("【程序异常】UI线程未捕获异常");
                Logger.Instance.Error(string.Format("异常类型: {0}", e.Exception.GetType().FullName));
                Logger.Instance.Error(string.Format("异常消息: {0}", e.Exception.Message));
                Logger.Instance.Error(string.Format("异常来源: {0}", e.Exception.Source ?? "未知"));
                Logger.Instance.Error(string.Format("目标方法: {0}", e.Exception.TargetSite.ToString() ?? "未知"));
                Logger.Instance.Error(string.Format("异常堆栈:\n{0}", e.Exception.StackTrace));
                
                // 记录内部异常
                if (e.Exception.InnerException != null)
                {
                    Logger.Instance.Error(string.Format("内部异常类型: {0}", e.Exception.InnerException.GetType().FullName));
                    Logger.Instance.Error(string.Format("内部异常消息: {0}", e.Exception.InnerException.Message));
                    Logger.Instance.Error(string.Format("内部异常堆栈:\n{0}", e.Exception.InnerException.StackTrace));
                }
                
                Logger.Instance.Error("程序状态: 继续运行（异常已被捕获）");
                Logger.Instance.Error("=".PadRight(80, '='));
                
                string errorMsg = string.Format("发生未处理的异常:\n\n{0}\n\n详细错误请查看日志文件。", e.Exception.Message);
                MessageBox.Show(errorMsg, "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception logEx)
            {
                // 如果日志系统也出错了，至少显示一个基本的错误消息
                try
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("日志系统异常: {0}", logEx.Message));
                    System.Diagnostics.Debug.WriteLine(string.Format("原始异常: {0}", e.Exception.Message));
                }
                catch { }
                MessageBox.Show(string.Format("发生严重错误: {0}", e.Exception.Message), "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 非UI线程异常处理
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.Instance.Error("=".PadRight(80, '='));
                Logger.Instance.Error("【程序异常】非UI线程未捕获异常");
                Logger.Instance.Error(string.Format("是否将终止程序: {0}", e.IsTerminating));
                
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    Logger.Instance.Error(string.Format("异常类型: {0}", ex.GetType().FullName));
                    Logger.Instance.Error(string.Format("异常消息: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常来源: {0}", ex.Source ?? "未知"));
                    Logger.Instance.Error(string.Format("目标方法: {0}", ex.TargetSite.ToString() ?? "未知"));
                    Logger.Instance.Error(string.Format("异常堆栈:\n{0}", ex.StackTrace));
                    
                    // 记录内部异常
                    if (ex.InnerException != null)
                    {
                        Logger.Instance.Error(string.Format("内部异常类型: {0}", ex.InnerException.GetType().FullName));
                        Logger.Instance.Error(string.Format("内部异常消息: {0}", ex.InnerException.Message));
                        Logger.Instance.Error(string.Format("内部异常堆栈:\n{0}", ex.InnerException.StackTrace));
                    }
                    
                    if (e.IsTerminating)
                    {
                        Logger.Instance.Error("程序状态: 即将退出（异常导致程序终止）");
                        Logger.Instance.Error(string.Format("退出时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                    }
                    else
                    {
                        Logger.Instance.Error("程序状态: 继续运行（异常已被捕获）");
                    }
                }
                else
                {
                    Logger.Instance.Error(string.Format("异常对象类型: {0}", e.ExceptionObject.GetType().FullName ?? "未知"));
                    Logger.Instance.Error(string.Format("异常对象: {0}", e.ExceptionObject.ToString() ?? "null"));
                    if (e.IsTerminating)
                    {
                        Logger.Instance.Error("程序状态: 即将退出（未知异常导致程序终止）");
                        Logger.Instance.Error(string.Format("退出时间: {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                    }
                }
                Logger.Instance.Error("=".PadRight(80, '='));
            }
            catch
            {
                // 忽略日志错误，避免无限循环
                try
                {
                    System.Diagnostics.Debug.WriteLine("非UI线程异常处理失败，程序可能即将退出");
                }
                catch { }
            }
        }
    }
}