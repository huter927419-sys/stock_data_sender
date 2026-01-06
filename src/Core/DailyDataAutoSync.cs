using System;
using System.Configuration;
using System.Threading;

namespace StockDataMQClient
{
    /// <summary>
    /// 日线数据自动同步管理器
    /// 功能：
    /// 1. 每天指定时间自动请求日线数据
    /// 2. 程序启动时检查当天数据是否已同步
    /// 3. 支持交易日检测（简单实现：周一至周五）
    /// </summary>
    public class DailyDataAutoSync
    {
        private static DailyDataAutoSync instance;
        private System.Windows.Forms.Timer syncTimer;
        private DateTime lastSyncDate = DateTime.MinValue;
        private bool isEnabled = false;
        private TimeSpan syncTime = new TimeSpan(15, 35, 0);  // 默认15:35
        private bool syncOnStartup = true;
        private Action requestDailyDataAction;
        private bool isInitialized = false;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static DailyDataAutoSync Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DailyDataAutoSync();
                }
                return instance;
            }
        }

        /// <summary>
        /// 初始化自动同步（传入请求日线数据的回调）
        /// </summary>
        /// <param name="requestDailyData">请求日线数据的回调函数</param>
        public void Initialize(Action requestDailyData)
        {
            if (isInitialized)
            {
                Logger.Instance.Warning("日线数据自动同步已初始化，跳过重复初始化");
                return;
            }

            requestDailyDataAction = requestDailyData;
            LoadConfig();

            if (!isEnabled)
            {
                Logger.Instance.Info("日线数据自动同步功能未启用");
                return;
            }

            Logger.Instance.Info(string.Format("日线数据自动同步已启用: 每天 {0} 自动同步", syncTime.ToString(@"hh\:mm")));

            // 初始化定时器（每分钟检查一次是否到达同步时间）
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 60000;  // 每60秒检查一次
            syncTimer.Tick += SyncTimer_Tick;
            syncTimer.Start();

            isInitialized = true;

            // 启动时检查是否需要同步
            if (syncOnStartup)
            {
                CheckAndSyncOnStartup();
            }
        }

        /// <summary>
        /// 从配置文件加载设置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                // 读取是否启用
                string enabledStr = ConfigurationManager.AppSettings["DailyDataAutoSyncEnabled"];
                if (!string.IsNullOrEmpty(enabledStr))
                {
                    bool.TryParse(enabledStr, out isEnabled);
                }

                // 读取同步时间
                string timeStr = ConfigurationManager.AppSettings["DailyDataAutoSyncTime"];
                if (!string.IsNullOrEmpty(timeStr))
                {
                    TimeSpan parsedTime;
                    if (TimeSpan.TryParse(timeStr, out parsedTime))
                    {
                        syncTime = parsedTime;
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("日线同步时间配置格式错误: {0}，使用默认值 15:35", timeStr));
                    }
                }

                // 读取启动时是否同步
                string startupStr = ConfigurationManager.AppSettings["DailyDataSyncOnStartup"];
                if (!string.IsNullOrEmpty(startupStr))
                {
                    bool.TryParse(startupStr, out syncOnStartup);
                }

                Logger.Instance.Info(string.Format("日线自动同步配置: 启用={0}, 同步时间={1}, 启动时同步={2}",
                    isEnabled, syncTime.ToString(@"hh\:mm"), syncOnStartup));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载日线自动同步配置失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 定时器事件：检查是否到达同步时间
        /// </summary>
        private void SyncTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                DateTime now = DateTime.Now;

                // 检查是否是交易日（周一至周五）
                if (!IsTradingDay(now))
                {
                    return;
                }

                // 检查是否到达同步时间（在同步时间的前后5分钟内触发）
                TimeSpan currentTime = now.TimeOfDay;
                TimeSpan timeDiff = currentTime - syncTime;

                // 如果当前时间在同步时间的0-5分钟内，且今天还没同步过
                if (timeDiff.TotalMinutes >= 0 && timeDiff.TotalMinutes < 5 && lastSyncDate.Date != now.Date)
                {
                    Logger.Instance.Info(string.Format("到达日线数据自动同步时间: {0}", now.ToString("yyyy-MM-dd HH:mm:ss")));
                    ExecuteSync("定时同步");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("日线自动同步定时器异常: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 启动时检查并同步
        /// </summary>
        private void CheckAndSyncOnStartup()
        {
            try
            {
                DateTime now = DateTime.Now;

                // 检查是否是交易日
                if (!IsTradingDay(now))
                {
                    Logger.Instance.Info("今天不是交易日，跳过启动时日线数据同步");
                    return;
                }

                // 如果当前时间已经过了同步时间，说明可能错过了今天的同步
                TimeSpan currentTime = now.TimeOfDay;
                if (currentTime > syncTime)
                {
                    Logger.Instance.Info(string.Format("程序启动时已过同步时间 ({0})，将在3秒后自动同步今日日线数据",
                        syncTime.ToString(@"hh\:mm")));

                    // 延迟3秒后执行（等待DLL连接稳定）
                    System.Windows.Forms.Timer delayTimer = new System.Windows.Forms.Timer();
                    delayTimer.Interval = 3000;
                    delayTimer.Tick += (s, args) =>
                    {
                        delayTimer.Stop();
                        delayTimer.Dispose();
                        ExecuteSync("启动时补充同步");
                    };
                    delayTimer.Start();
                }
                else
                {
                    Logger.Instance.Info(string.Format("当前时间 {0} 尚未到达同步时间 {1}，将在指定时间自动同步",
                        currentTime.ToString(@"hh\:mm"), syncTime.ToString(@"hh\:mm")));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("启动时检查日线同步失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 执行同步
        /// </summary>
        /// <param name="reason">触发原因</param>
        private void ExecuteSync(string reason)
        {
            try
            {
                if (requestDailyDataAction == null)
                {
                    Logger.Instance.Warning("日线数据请求回调未设置，无法执行同步");
                    return;
                }

                DateTime now = DateTime.Now;

                // 检查今天是否已经同步过
                if (lastSyncDate.Date == now.Date)
                {
                    Logger.Instance.Info(string.Format("今天 ({0}) 已经同步过日线数据，跳过重复同步", now.ToString("yyyy-MM-dd")));
                    return;
                }

                Logger.Instance.Info("=".PadRight(60, '='));
                Logger.Instance.Info(string.Format("【日线数据自动同步】触发原因: {0}", reason));
                Logger.Instance.Info(string.Format("同步时间: {0}", now.ToString("yyyy-MM-dd HH:mm:ss")));
                Logger.Instance.Info("=".PadRight(60, '='));

                // 执行同步
                requestDailyDataAction();

                // 记录同步日期
                lastSyncDate = now;

                Logger.Instance.Success(string.Format("日线数据同步请求已发送 (日期: {0})", now.ToString("yyyy-MM-dd")));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("执行日线数据同步失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 判断是否是交易日（简单实现：周一至周五）
        /// 注意：这里没有考虑节假日，如需精确判断需要接入交易日历
        /// </summary>
        private bool IsTradingDay(DateTime date)
        {
            DayOfWeek day = date.DayOfWeek;
            return day != DayOfWeek.Saturday && day != DayOfWeek.Sunday;
        }

        /// <summary>
        /// 手动触发同步（供外部调用）
        /// </summary>
        public void ManualSync()
        {
            ExecuteSync("手动触发");
        }

        /// <summary>
        /// 获取上次同步时间
        /// </summary>
        public DateTime GetLastSyncDate()
        {
            return lastSyncDate;
        }

        /// <summary>
        /// 获取下次同步时间
        /// </summary>
        public DateTime GetNextSyncTime()
        {
            DateTime now = DateTime.Now;
            DateTime todaySync = now.Date.Add(syncTime);

            if (now < todaySync && IsTradingDay(now))
            {
                return todaySync;
            }

            // 找下一个交易日
            DateTime nextDay = now.Date.AddDays(1);
            while (!IsTradingDay(nextDay))
            {
                nextDay = nextDay.AddDays(1);
            }
            return nextDay.Add(syncTime);
        }

        /// <summary>
        /// 停止自动同步
        /// </summary>
        public void Stop()
        {
            if (syncTimer != null)
            {
                syncTimer.Stop();
                syncTimer.Dispose();
                syncTimer = null;
            }
            isInitialized = false;
            Logger.Instance.Info("日线数据自动同步已停止");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            Stop();
        }
    }
}
