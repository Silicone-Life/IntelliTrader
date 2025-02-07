﻿using SiliconeTrader.Core;
using SiliconeTrader.Signals.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SiliconeTrader.Backtesting
{
    public class BacktestingSignalsService : ConfigrableServiceBase<SignalsConfig>, ISignalsService
    {
        public override string ServiceName => Constants.ServiceNames.SignalsService;

        ISignalsConfig ISignalsService.Config => this.Config;

        public IModuleRules Rules { get; private set; }
        public ISignalRulesConfig RulesConfig { get; private set; }

        private readonly ILoggingService loggingService;
        private readonly IHealthCheckService healthCheckService;
        private readonly ITasksService tasksService;
        private readonly ITradingService tradingService;
        private readonly IRulesService rulesService;
        private readonly IBacktestingService backtestingService;

        private SignalRulesTimedTask signalRulesTimedTask;
        private IEnumerable<string> signalNames;

        public BacktestingSignalsService(ILoggingService loggingService, IHealthCheckService healthCheckService, ITasksService tasksService, ITradingService tradingService, IRulesService rulesService, IBacktestingService backtestingService)
        {
            this.loggingService = loggingService;
            this.healthCheckService = healthCheckService;
            this.tasksService = tasksService;
            this.tradingService = tradingService;
            this.rulesService = rulesService;
            this.backtestingService = backtestingService;
        }

        public void Start()
        {
            loggingService.Info("Start Backtesting Signals service...");

            this.OnSignalRulesChanged();
            rulesService.RegisterRulesChangeCallback(this.OnSignalRulesChanged);

            signalRulesTimedTask = tasksService.AddTask(
                name: nameof(SignalRulesTimedTask),
                task: new SignalRulesTimedTask(loggingService, healthCheckService, tradingService, rulesService, this),
                interval: this.RulesConfig.CheckInterval * 1000 / Application.Speed,
                startDelay: Constants.TaskDelays.LowDelay,
                startTask: false,
                runNow: false,
                skipIteration: 0);

            loggingService.Info("Backtesting Signals service started");
        }

        public void Stop()
        {
            loggingService.Info("Stop Backtesting Signals service...");

            tasksService.RemoveTask(nameof(SignalRulesTimedTask), stopTask: true);

            rulesService.UnregisterRulesChangeCallback(this.OnSignalRulesChanged);

            healthCheckService.RemoveHealthCheck(Constants.HealthChecks.SignalRulesProcessed);

            loggingService.Info("Backtesting Signals service stopped");
        }

        public void ProcessPair(string pair, Dictionary<string, ISignal> signals)
        {
            IEnumerable<IRule> enabledRules = this.Rules.Entries.Where(r => r.Enabled);
            foreach (IRule rule in enabledRules)
            {
                signalRulesTimedTask.ProcessRule(rule, signals, pair, signalRulesTimedTask.GetExcludedPairs(), this.GetGlobalRating());
            }
        }

        public void StopTrailing()
        {
            signalRulesTimedTask.StopTrailing();
        }

        public List<string> GetTrailingSignals()
        {
            return signalRulesTimedTask.GetTrailingSignals();
        }

        public IEnumerable<ISignalTrailingInfo> GetTrailingInfo(string pair)
        {
            return signalRulesTimedTask.GetTrailingInfo(pair);
        }

        public IEnumerable<string> GetSignalNames()
        {
            if (signalNames == null)
            {
                signalNames = backtestingService.GetCurrentSignals().Values.SelectMany(val => val.Select(s => s.Name)).Distinct().ToList();
            }
            return signalNames;

        }

        public IEnumerable<ISignal> GetAllSignals()
        {
            return this.GetSignalsByName(null);
        }

        public IEnumerable<ISignal> GetSignalsByName(string signalName)
        {
            IEnumerable<ISignal> allSignals = backtestingService.GetCurrentSignals().SelectMany(s => s.Value);
            if (signalName == null)
            {
                return allSignals;
            }
            else
            {
                return allSignals.Where(s => s.Name == signalName);
            }
        }

        public IEnumerable<ISignal> GetSignalsByPair(string pair)
        {
            if (backtestingService.GetCurrentSignals().TryGetValue(pair, out IEnumerable<ISignal> signalsByPair))
            {
                return signalsByPair;
            }
            else
            {
                return null;
            }
        }

        public ISignal GetSignal(string pair, string signalName)
        {
            return this.GetSignalsByName(signalName)?.FirstOrDefault(s => s.Pair == pair);
        }

        public double? GetRating(string pair, string signalName)
        {
            return this.GetSignalsByName(signalName)?.FirstOrDefault(s => s.Pair == pair)?.Rating;
        }

        public double? GetRating(string pair, IEnumerable<string> signalNames)
        {
            if (signalNames != null && signalNames.Count() > 0)
            {
                double ratingSum = 0;

                foreach (string signalName in signalNames)
                {
                    double? rating = this.GetSignalsByName(signalName)?.FirstOrDefault(s => s.Pair == pair)?.Rating;
                    if (rating != null)
                    {
                        ratingSum += rating.Value;
                    }
                    else
                    {
                        return null;
                    }
                }

                return Math.Round(ratingSum / signalNames.Count(), 8);
            }
            else
            {
                return null;
            }
        }

        public double? GetGlobalRating()
        {
            try
            {
                double ratingSum = 0;
                double ratingCount = 0;

                Dictionary<string, IEnumerable<ISignal>> currentSignals = backtestingService.GetCurrentSignals();
                if (currentSignals != null)
                {
                    IEnumerable<IGrouping<string, ISignal>> signalGroups = currentSignals.Values.SelectMany(s => s).GroupBy(s => s.Name);
                    foreach (IGrouping<string, ISignal> signalGroup in signalGroups)
                    {
                        if (this.Config.GlobalRatingSignals.Contains(signalGroup.Key))
                        {
                            double? averageRating = signalGroup.Average(s => s.Rating);
                            if (averageRating != null)
                            {
                                ratingSum += averageRating.Value;
                                ratingCount++;
                            }
                        }
                    }
                }

                if (ratingCount > 0)
                {
                    return Math.Round(ratingSum / ratingCount, 8);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                loggingService.Error("Unable to get global rating", ex);
                return null;
            }
        }

        private void OnSignalRulesChanged()
        {
            this.Rules = rulesService.GetRules(this.ServiceName);
            this.RulesConfig = this.Rules.GetConfiguration<SignalRulesConfig>();
        }
    }
}
