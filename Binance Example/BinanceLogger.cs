using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Binance_Example
{
    internal class BinanceLogger : ILogger
    {

        public TextBox TextLog { get; set; }
        public ExtendedTelegram TelegramBot { get; set; }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning || logLevel == LogLevel.Critical || logLevel == LogLevel.Error) 
            {
                if (state != null)
                    MainWindow.LogMessage(state.ToString(), TextLog, TelegramBot);

                if (exception != null)
                    MainWindow.LogMessage(exception.Message, TextLog, TelegramBot);
            }
            //throw new NotImplementedException();
        }
    }
}
