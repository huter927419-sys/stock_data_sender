using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace StockDataMQClient
{
    /// <summary>
    /// DLL分析工具 - 分析DLL的导出函数
    /// </summary>
    public class DllAnalyzer
    {
        // 使用Kernel32的GetProcAddress枚举
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        /// <summary>
        /// 分析DLL文件，列出所有可能的导出函数
        /// </summary>
        public static List<string> AnalyzeDll(string dllPath)
        {
            List<string> foundFunctions = new List<string>();
            List<string> allPossibleFunctions = new List<string>
            {
                // 核心函数
                "Stock_Init",
                "Stock_Quit",
                "GetStockDrvInfo",
                "SetupReceiver",
                "ReInitStockInfo",
                
                // 扩展API函数
                "AskStockDay",
                "AskStockMn5",
                "AskStockMin",
                "AskStockBase",
                "AskStockNews",
                "AskStockHalt",
                "AskStockPRP",
                "AskStockPwr",
                "AskStockFin"
            };

            if (!File.Exists(dllPath))
            {
                Logger.Instance.Error(string.Format("DLL文件不存在: {0}", dllPath));
                return foundFunctions;
            }

            IntPtr hModule = IntPtr.Zero;
            try
            {
                Logger.Instance.Info(string.Format("正在分析DLL: {0}", dllPath));
                Logger.Instance.Info(string.Format("文件大小: {0} 字节", new FileInfo(dllPath).Length));
                
                // 加载DLL
                hModule = LoadLibrary(dllPath);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Instance.Error(string.Format("加载DLL失败，错误代码: {0}", error));
                    return foundFunctions;
                }

                Logger.Instance.Info("DLL加载成功，开始检测导出函数...");

                // 检测每个可能的函数
                foreach (string funcName in allPossibleFunctions)
                {
                    IntPtr procAddress = GetProcAddress(hModule, funcName);
                    if (procAddress != IntPtr.Zero)
                    {
                        foundFunctions.Add(funcName);
                        Logger.Instance.Success(string.Format("✓ 找到函数: {0} (地址: 0x{1:X})", funcName, procAddress.ToInt64()));
                    }
                    else
                    {
                        Logger.Instance.Info(string.Format("✗ 未找到函数: {0}", funcName));
                    }
                }

                Logger.Instance.Info(string.Format("=== 分析完成: 找到 {0} 个导出函数 ===", foundFunctions.Count));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("分析DLL时发生异常: {0}", ex.Message));
            }
            finally
            {
                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                }
            }

            return foundFunctions;
        }

        /// <summary>
        /// 获取DLL文件信息
        /// </summary>
        public static void GetDllInfo(string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                Logger.Instance.Error(string.Format("DLL文件不存在: {0}", dllPath));
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(dllPath);
                Logger.Instance.Info("=== DLL文件信息 ===");
                Logger.Instance.Info(string.Format("文件路径: {0}", dllPath));
                Logger.Instance.Info(string.Format("文件大小: {0} 字节 ({1:F2} KB)", fileInfo.Length, fileInfo.Length / 1024.0));
                Logger.Instance.Info(string.Format("创建时间: {0}", fileInfo.CreationTime));
                Logger.Instance.Info(string.Format("修改时间: {0}", fileInfo.LastWriteTime));
                
                // 尝试获取版本信息
                try
                {
                    System.Diagnostics.FileVersionInfo versionInfo = 
                        System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                    if (versionInfo != null)
                    {
                        Logger.Instance.Info(string.Format("文件版本: {0}", versionInfo.FileVersion));
                        Logger.Instance.Info(string.Format("产品版本: {0}", versionInfo.ProductVersion));
                        Logger.Instance.Info(string.Format("产品名称: {0}", versionInfo.ProductName));
                        Logger.Instance.Info(string.Format("公司名称: {0}", versionInfo.CompanyName));
                        Logger.Instance.Info(string.Format("文件描述: {0}", versionInfo.FileDescription));
                    }
                }
                catch
                {
                    Logger.Instance.Info("无法获取版本信息（可能DLL没有版本资源）");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("获取DLL信息时发生异常: {0}", ex.Message));
            }
        }
    }
}

