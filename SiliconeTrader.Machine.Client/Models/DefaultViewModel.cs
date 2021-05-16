﻿namespace SiliconeTrader.Machine.Client.Models
{
    public class DefaultViewModel : IInstanceVersion
    {
        public static DefaultViewModel Default => new DefaultViewModel
        {
            InstanceName = "-0-",
            ReadOnlyMode = true,
            Version = "-0-"
        };

        public string Error { get; set; }

        public string InstanceName { get; set; }

        public bool ReadOnlyMode { get; set; }

        public string Version { get; set; }
    }
}