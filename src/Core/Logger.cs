using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Info,       // 信息（白色）
        Success,    // 成功（绿色）
        Warning,    // 警告（黄色）
        Error,      // 错误（红色）
        Debug       // 调试（灰色）
    }

    /// <summary>
    /// 日志管理器
    /// </summary>
    public class Logger
    {
        private static Logger instance;
        private RichTextBox logTextBox;
        private int maxLogLines = 5000;  // 最大日志行数（增加到5000，避免过早截断）
        private bool isPaused = false;
        // filterDailyDataOnly已删除，不再过滤日志
        
        // 文件输出相关
        private string logDirectory = null;  // 日志目录
        private string currentLogFile = null;  // 当前日志文件路径
        private StreamWriter logFileWriter = null;  // 文件写入器
        private readonly object fileLock = new object();  // 文件写入锁
        private Queue<string> fileLogQueue = new Queue<string>();  // 文件日志队列
        private object fileLogQueueLock = new object();  // 文件日志队列锁
        private System.Windows.Forms.Timer fileFlushTimer;  // 文件刷新定时器
        private DateTime currentLogDate = DateTime.MinValue;  // 当前日志文件日期

        public static Logger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Logger();
                }
                return instance;
            }
        }

        /// <summary>
        /// 设置日志显示控件
        /// </summary>
        public void SetLogTextBox(RichTextBox textBox)
        {
            logTextBox = textBox;
            if (logTextBox != null)
            {
                logTextBox.ReadOnly = true;
                logTextBox.BackColor = Color.FromArgb(20, 20, 20);
                logTextBox.ForeColor = Color.White;
                logTextBox.Font = new Font("Consolas", 9);
            }
        }
        
        /// <summary>
        /// 启用文件日志输出
        /// </summary>
        /// <param name="logDirectory">日志文件目录（如果为null，使用应用程序目录下的Logs文件夹）</param>
        public void EnableFileLogging(string logDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(logDirectory))
                {
                    // 默认使用应用程序目录下的Logs文件夹
                    string appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    logDirectory = Path.Combine(appDirectory, "Logs");
                }
                
                // 确保目录存在
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                this.logDirectory = logDirectory;
                currentLogDate = DateTime.Now.Date;
                UpdateLogFile();
                
                // 启动文件刷新定时器
                if (fileFlushTimer == null)
                {
                    fileFlushTimer = new System.Windows.Forms.Timer();
                    fileFlushTimer.Interval = 1000;  // 每1秒刷新一次文件
                    fileFlushTimer.Tick += FileFlushTimer_Tick;
                    fileFlushTimer.Start();
                }
                
                Info(string.Format("文件日志已启用，日志目录: {0}", logDirectory));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("启用文件日志失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 禁用文件日志输出
        /// </summary>
        public void DisableFileLogging()
        {
            try
            {
                if (fileFlushTimer != null)
                {
                    fileFlushTimer.Stop();
                    fileFlushTimer = null;
                }
                
                // 刷新并关闭文件
                FlushFileLogs();
                if (logFileWriter != null)
                {
                    logFileWriter.Close();
                    logFileWriter = null;
                }
                
                logDirectory = null;
                currentLogFile = null;
                Info("文件日志已禁用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("禁用文件日志失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 更新日志文件（按日期轮转）
        /// </summary>
        private void UpdateLogFile()
        {
            try
            {
                DateTime today = DateTime.Now.Date;
                
                // 如果日期变化，需要切换日志文件
                if (currentLogDate != today || logFileWriter == null)
                {
                    // 关闭旧文件
                    if (logFileWriter != null)
                    {
                        logFileWriter.Flush();
                        logFileWriter.Close();
                        logFileWriter = null;
                    }
                    
                    // 创建新文件
                    currentLogDate = today;
                    string fileName = string.Format("StockDataMQClient_{0:yyyyMMdd}.log", today);
                    currentLogFile = Path.Combine(logDirectory, fileName);

                    // 以追加模式打开文件
                    logFileWriter = new StreamWriter(currentLogFile, true, System.Text.Encoding.UTF8)
                    {
                        AutoFlush = false  // 手动刷新，提高性能
                    };
                    
                    // 如果是新文件，写入文件头
                    if (new FileInfo(currentLogFile).Length == 0)
                    {
                        logFileWriter.WriteLine(string.Format("========== 日志文件创建时间: {0:yyyy-MM-dd HH:mm:ss} ==========", DateTime.Now));
                        logFileWriter.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("更新日志文件失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 文件刷新定时器事件
        /// </summary>
        private void FileFlushTimer_Tick(object sender, EventArgs e)
        {
            FlushFileLogs();
        }
        
        /// <summary>
        /// 刷新文件日志（批量写入）
        /// </summary>
        private void FlushFileLogs()
        {
            if (logDirectory == null || logFileWriter == null)
                return;
            
            try
            {
                // 检查是否需要切换日志文件（日期变化）
                UpdateLogFile();
                
                // 批量写入日志
                List<string> logsToWrite = new List<string>();
                int maxBatchSize = 100;
                int processedCount = 0;

                lock (fileLogQueueLock)
                {
                    while (fileLogQueue.Count > 0 && processedCount < maxBatchSize)
                    {
                        string logEntry = fileLogQueue.Dequeue();
                        logsToWrite.Add(logEntry);
                        processedCount++;
                    }
                }
                
                if (logsToWrite.Count > 0 && logFileWriter != null)
                {
                    lock (fileLock)
                    {
                        foreach (string logItem in logsToWrite)
                        {
                            logFileWriter.WriteLine(logItem);
                        }
                        logFileWriter.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("刷新文件日志失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        public void Log(string message, LogLevel level)
        {
            if (isPaused)
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = string.Format("[{0}] [{1}] {2}", timestamp, GetLevelText(level), message);

            // 输出到Debug
            System.Diagnostics.Debug.WriteLine(logMessage);

            // 输出到文件（如果已启用）
            if (logDirectory != null)
            {
                lock (fileLogQueueLock)
                {
                    fileLogQueue.Enqueue(logMessage);
                }
            }

            // 输出到UI
            if (logTextBox != null && logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => AppendLog(logMessage, level)));
            }
            else if (logTextBox != null)
            {
                AppendLog(logMessage, level);
            }
        }

        // 日志批量处理队列
        private Queue<string> logQueue = new Queue<string>();
        private object logQueueLock = new object();  // 日志队列锁
        private System.Windows.Forms.Timer logFlushTimer;
        private readonly object logLock = new object();

        /// <summary>
        /// 追加日志到文本框（优化性能版本 - 批量处理）
        /// </summary>
        private void AppendLog(string message, LogLevel level)
        {
            if (logTextBox == null)
                return;

            try
            {
                // 将日志加入队列（message已经包含时间戳和级别信息）
                lock (logQueueLock)
                {
                    logQueue.Enqueue(message);
                }

                // 初始化批量刷新定时器（如果还没有）
                if (logFlushTimer == null)
                {
                    logFlushTimer = new System.Windows.Forms.Timer();
                    logFlushTimer.Interval = 200;  // 200毫秒批量刷新一次
                    logFlushTimer.Tick += LogFlushTimer_Tick;
                    logFlushTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("日志输出失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 批量刷新日志到UI
        /// </summary>
        private void LogFlushTimer_Tick(object sender, EventArgs e)
        {
            if (logTextBox == null)
                return;

            try
            {
                // 批量获取日志（增加每次处理的数量，避免积压导致内存问题）
                List<string> logsToAppend = new List<string>();
                int maxBatchSize = 500;  // 增加批量大小到500，避免队列积压
                int processedCount = 0;

                lock (logQueueLock)
                {
                    // 如果队列为空，直接返回
                    if (logQueue.Count == 0)
                        return;

                    while (logQueue.Count > 0 && processedCount < maxBatchSize)
                    {
                        string log2 = logQueue.Dequeue();
                        logsToAppend.Add(log2);
                        processedCount++;
                    }
                }

                if (logsToAppend.Count == 0)
                    return;

                lock (logLock)
                {

                    // 限制日志行数（优化清理逻辑，防止内存无限增长）
                    if (logTextBox.Lines.Length > maxLogLines)
                    {
                        // 删除前一半的日志，而不是逐行删除
                        int removeCount = maxLogLines / 2;
                        string[] lines = logTextBox.Lines;
                        if (lines.Length > removeCount)
                        {
                            string newText = string.Join(Environment.NewLine, lines, removeCount, lines.Length - removeCount);
                            logTextBox.Text = newText;
                        }
                    }
                    
                    // 如果队列中还有大量日志未处理，记录警告（防止内存问题）
                    int remainingLogs = 0;
                    lock (logQueueLock)
                    {
                        remainingLogs = logQueue.Count;
                    }
                    if (remainingLogs > 1000)
                    {
                        // 只在第一次检测到积压时记录，避免日志过多
                        if (remainingLogs % 500 == 0)
                        {
                            Logger.Instance.Warning(string.Format("日志队列积压: 还有 {0} 条日志未处理，可能存在性能问题", remainingLogs));
                        }
                    }

                    // 批量追加日志（带颜色）
                    logTextBox.SelectionStart = logTextBox.Text.Length;
                    logTextBox.SelectionLength = 0;
                    
                    foreach (string log in logsToAppend)
                    {
                        // 根据日志级别设置颜色
                        Color logColor = GetLogColorFromMessage(log);
                        logTextBox.SelectionColor = logColor;
                        logTextBox.AppendText(log + Environment.NewLine);
                    }
                    
                    logTextBox.SelectionColor = logTextBox.ForeColor;

                    // 自动滚动到底部（只滚动一次）
                    logTextBox.SelectionStart = logTextBox.Text.Length;
                    logTextBox.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("日志刷新失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 获取日志级别文本
        /// </summary>
        private string GetLevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info: return "信息";
                case LogLevel.Success: return "成功";
                case LogLevel.Warning: return "警告";
                case LogLevel.Error: return "错误";
                case LogLevel.Debug: return "调试";
                default: return "信息";
            }
        }

        /// <summary>
        /// 获取日志级别颜色
        /// </summary>
        private Color GetLevelColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info: return Color.White;
                case LogLevel.Success: return Color.LightGreen;
                case LogLevel.Warning: return Color.Yellow;
                case LogLevel.Error: return Color.Red;
                case LogLevel.Debug: return Color.Gray;
                default: return Color.White;
            }
        }
        
        /// <summary>
        /// 从日志消息中提取级别并返回对应颜色
        /// </summary>
        private Color GetLogColorFromMessage(string message)
        {
            if (message.Contains("[成功]"))
                return Color.LightGreen;
            else if (message.Contains("[警告]"))
                return Color.Yellow;
            else if (message.Contains("[错误]"))
                return Color.Red;
            else if (message.Contains("[调试]"))
                return Color.Gray;
            else
                return Color.White;  // 信息
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void Clear()
        {
            if (logTextBox != null)
            {
                if (logTextBox.InvokeRequired)
                {
                    logTextBox.Invoke(new Action(() => logTextBox.Clear()));
                }
                else
                {
                    logTextBox.Clear();
                }
            }
        }

        /// <summary>
        /// 暂停日志
        /// </summary>
        public void Pause()
        {
            isPaused = true;
        }

        /// <summary>
        /// 恢复日志
        /// </summary>
        public void Resume()
        {
            isPaused = false;
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        public string GetCurrentLogFile()
        {
            return currentLogFile;
        }
        
        /// <summary>
        /// 获取日志目录
        /// </summary>
        public string GetLogDirectory()
        {
            return logDirectory;
        }
        
        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        public string GetLogFilePath()
        {
            if (string.IsNullOrEmpty(currentLogFile))
            {
                // 如果当前日志文件路径为空，尝试构建一个默认路径
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    string fileName = string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now);
                    return Path.Combine(logDirectory, fileName);
                }
                else
                {
                    // 如果日志目录也为空，返回默认路径
                    string appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    string defaultLogDir = Path.Combine(appDirectory, "Logs");
                    string fileName = string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now);
                    return Path.Combine(defaultLogDir, fileName);
                }
            }
            return currentLogFile;
        }
        
        /// <summary>
        /// 清理资源（程序退出时调用）
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // 刷新所有待写入的日志
                FlushFileLogs();
                
                // 关闭文件写入器
                if (logFileWriter != null)
                {
                    logFileWriter.Flush();
                    logFileWriter.Close();
                    logFileWriter = null;
                }
                
                // 停止并释放定时器（防止内存泄漏）
                if (fileFlushTimer != null)
                {
                    fileFlushTimer.Stop();
                    fileFlushTimer.Dispose();
                    fileFlushTimer = null;
                }
                if (logFlushTimer != null)
                {
                    logFlushTimer.Stop();
                    logFlushTimer.Dispose();
                    logFlushTimer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("清理日志资源失败: {0}", ex.Message));
            }
        }
        
        // 便捷方法
        public void Info(string message)
        {
            Log(message, LogLevel.Info);
        }

        public void Success(string message)
        {
            Log(message, LogLevel.Success);
        }

        public void Warning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        public void Error(string message)
        {
            Log(message, LogLevel.Error);
        }

        public void Debug(string message)
        {
            Log(message, LogLevel.Debug);
        }
    }
}

