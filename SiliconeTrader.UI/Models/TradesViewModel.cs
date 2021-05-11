﻿using SiliconeTrader.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiliconeTrader.UI.Models
{
    public class TradesViewModel : BaseViewModel
    {
        public double TimezoneOffset { get; set; }
        public DateTimeOffset Date { get; set; }
        public List<TradeResult> Trades { get; set; }
    }
}
