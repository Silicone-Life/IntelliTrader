﻿using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SiliconeTrader.Core;

namespace SiliconeTrader.Trading
{
    internal class VirtualAccount : TradingAccountBase
    {
        public override bool IsVirtual => true;

        public VirtualAccount(ILoggingService loggingService, INotificationService notificationService, IHealthCheckService healthCheckService, ISignalsService signalsService, ITradingService tradingService)
            : base(loggingService, notificationService, healthCheckService, signalsService, tradingService)
        {

        }

        public override void Refresh()
        {
            lock (this.SyncRoot)
            {
                // Only done once, since all the data is always up to date
                if (isInitialRefresh)
                {
                    this.Load();
                    isInitialRefresh = false;
                }

                healthCheckService.UpdateHealthCheck(Constants.HealthChecks.AccountRefreshed);
            }
        }

        public override void Save()
        {
            lock (this.SyncRoot)
            {
                ITradingService tradingService = Application.Resolve<ITradingService>();
                string virtualAccountFilePath = Path.Combine(Directory.GetCurrentDirectory(), tradingService.Config.VirtualAccountFilePath);

                var data = new TradingAccountData
                {
                    Balance = balance,
                    TradingPairs = tradingPairs
                };

                string virtualAccountJson = JsonConvert.SerializeObject(data, Formatting.Indented);
                var virtualAccountFile = new FileInfo(virtualAccountFilePath);
                virtualAccountFile.Directory.Create();
                File.WriteAllText(virtualAccountFile.FullName, virtualAccountJson);
            }
        }

        public void Load()
        {
            lock (this.SyncRoot)
            {
                ITradingService tradingService = Application.Resolve<ITradingService>();
                string virtualAccountFilePath = Path.Combine(Directory.GetCurrentDirectory(), tradingService.Config.VirtualAccountFilePath);

                if (File.Exists(virtualAccountFilePath))
                {
                    string virtualAccountJson = File.ReadAllText(virtualAccountFilePath);
                    TradingAccountData virtualAccountData = JsonConvert.DeserializeObject<TradingAccountData>(virtualAccountJson);

                    balance = virtualAccountData.Balance;
                    tradingPairs = virtualAccountData.TradingPairs;
                }
                else
                {
                    balance = tradingService.Config.VirtualAccountInitialBalance;
                    tradingPairs = new ConcurrentDictionary<string, TradingPair>();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            healthCheckService.RemoveHealthCheck(Constants.HealthChecks.AccountRefreshed);
        }
    }
}
