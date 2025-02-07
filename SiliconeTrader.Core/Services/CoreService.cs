﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SiliconeTrader.Core
{
    internal class CoreService : ConfigrableServiceBase<CoreConfig>, ICoreService
    {
        public event Action Started;

        public override string ServiceName => Constants.ServiceNames.CoreService;

        ICoreConfig ICoreService.Config => this.Config;

        public string Version { get; private set; }
        public string VersionType { get; private set; } = " Beta";

        private readonly ILoggingService loggingService;
        private readonly ITasksService tasksService;
        private readonly INotificationService notificationService;
        private readonly IHealthCheckService healthCheckService;
        private readonly ITradingService tradingService; 
        private readonly IBacktestingService backtestingService;

        public CoreService(ILoggingService loggingService, ITasksService tasksService, INotificationService notificationService, IHealthCheckService healthCheckService, ITradingService tradingService, IBacktestingService backtestingService)
        {
            this.loggingService = loggingService;
            this.tasksService = tasksService;
            this.notificationService = notificationService;
            this.healthCheckService = healthCheckService;
            this.tradingService = tradingService; 
            this.backtestingService = backtestingService;

            // Log unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            tasksService.SetUnhandledExceptionHandler(this.OnUnhandledException);

            // Set decimal separator to a dot for all cultures
            var cultureInfo = new CultureInfo(CultureInfo.CurrentCulture.Name);
            cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            this.Version = this.GetType().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version + this.VersionType;
        }

        public void Start()
        {
            loggingService.Info($"Start Core service (Version: {this.Version})...");

            if (backtestingService.Config.Enabled)
            {
                backtestingService.Start();
            }
            if (this.Config.HealthCheckInterval > 0 && (!backtestingService.Config.Enabled || !backtestingService.Config.Replay))
            {
                healthCheckService.Start();
            }
            if (tradingService.Config.Enabled)
            {
                tradingService.Start();
            }
            if (notificationService.Config.Enabled)
            {
                notificationService.Start();
            } 
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Thread.Sleep(2000);
                Started?.Invoke();
                tasksService.StartAllTasks();
            });

            loggingService.Info("Core service started");
            notificationService.Notify($"SiliconeTrader started");
        }

        public void Stop()
        {
            notificationService.Notify("SiliconeTrader stopped");
            loggingService.Info("Stop Core service...");
            if (tradingService.Config.Enabled)
            {
                tradingService.Stop();
            }
            if (notificationService.Config.Enabled)
            {
                notificationService.Stop();
            } 
            if (this.Config.HealthCheckInterval > 0 && (!backtestingService.Config.Enabled || !backtestingService.Config.Replay))
            {
                healthCheckService.Stop();
            }
            if (backtestingService.Config.Enabled)
            {
                backtestingService.Stop();
            }

            tasksService.StopAllTasks();
            tasksService.RemoveAllTasks();
            loggingService.Info("Core service stopped");
        }

        public void Restart()
        {
            notificationService.Notify("SiliconeTrader restarting...");
            loggingService.Info("Restart Core service...");
            Task.Run(() => this.Stop()).Wait(TimeSpan.FromSeconds(20));
            this.Start();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = "Unhandled exception occured";
            if (e.ExceptionObject != null)
            {
                message = $"{message} - {e.ExceptionObject}";
            }
            try
            {
                loggingService.Error(message);
                notificationService.Notify(message);
            } catch { }
        }
    }
}
