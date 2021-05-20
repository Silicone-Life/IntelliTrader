﻿using SiliconeTrader.Core;
using SiliconeTrader.Exchange.Base;

namespace SiliconeTrader.Exchange
{
    internal class TickersMonitorTimedTask : HighResolutionTimedTask
    {
        private readonly ILoggingService loggingService;
        private readonly IExchangeService exchangeService;

        public TickersMonitorTimedTask(ILoggingService loggingService, IExchangeService exchangeService)
        {
            this.loggingService = loggingService;
            this.exchangeService = exchangeService;
        }

        protected override void Run()
        {
            if (exchangeService.GetTimeElapsedSinceLastTickersUpdate().TotalMilliseconds > ExchangeService.MAX_TICKERS_AGE_TO_RECONNECT_MILLISECONDS)
            {
                loggingService.Info("Exchange max tickers age reached, reconnecting...");
                exchangeService.DisconnectTickersWebsocket();
                exchangeService.ConnectTickersWebsocket();
            }
        }
    }
}
