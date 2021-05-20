﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SiliconeTrader.Core
{
    public interface INotificationConfig
    {
        bool Enabled { get; }
        bool TelegramEnabled { get; }
        string TelegramBotToken { get; }
        long TelegramChatId { get; }
        bool TelegramAlertsEnabled { get; }
    }
}
