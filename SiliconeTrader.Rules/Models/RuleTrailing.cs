﻿using SiliconeTrader.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SiliconeTrader.Rules
{
    internal class RuleTrailing : IRuleTrailing
    {
        public bool Enabled { get; set; }
        public int MinDuration { get; set; }
        public int MaxDuration { get; set; }
        public IEnumerable<RuleCondition> StartConditions { get; set; }

        IEnumerable<IRuleCondition> IRuleTrailing.StartConditions => this.StartConditions;
    }
}
