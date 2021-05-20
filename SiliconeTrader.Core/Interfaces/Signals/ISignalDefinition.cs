﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SiliconeTrader.Core
{
    public interface ISignalDefinition
    {
        string Name { get; }
        string Receiver { get; }
        IConfigurationSection Configuration { get; }
    }
}
