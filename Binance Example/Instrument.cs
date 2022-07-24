using Binance.Net.Clients;
using Binance.Net.Objects;
using Binance.Net.SymbolOrderBooks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Binance_Example
{
    public class Instrument : ObservableObject
    {
        public string Code;

        private decimal _price;

        public TextBox TextLog { get; set; }
        public ExtendedTelegram TelegramBot { get; set; }

        public decimal LastPrice
        {
            get => _price; set
            {
                _price = value;
                RaisePropertyChangedEvent("LastPrice");
            }
        }
        private BinanceSocketClient socketClient;
        CancellationToken subscriveforprice = new CancellationToken();

        public Instrument(string code, BinanceSocketClient binanceSocket)
        {
            Code = code;
            socketClient = binanceSocket;
        }

        public async Task SubscribeLastPriceAsync()
        {

            try
            {
                //потом дописать, получение цены каждые 250 мс
                /*  using (var client = new BinanceClient())
                  {
                     var result =   await client.UsdFuturesApi.CommonFuturesClient.GetTickerAsync(Code);
                  }*/

                var oneinstrument = socketClient.UsdFuturesStreams.SubscribeToTickerUpdatesAsync(Code, data =>
               {
                   LastPrice = data.Data.LastPrice;
                   Debug.WriteLine("Tick " + LastPrice.ToString());

               }, subscriveforprice).Result;


               var oneinstrument2 =  socketClient.UsdFuturesStreams.SubscribeToOrderBookUpdatesAsync("BTCUSDT",100,data => 
                {

                    Debug.WriteLine( "Котировки {0} {1}",data.Data.Bids.Count(), data.Data.Bids.Count());
                   // foreach (var bid in data.Data.Bids) Debug.WriteLine("{0} {1}", bid.Price, bid.Quantity);
                    Debug.WriteLine("-----------------------");

                },new CancellationToken());

               // var orderbook = new BinanceFuturesUsdtSymbolOrderBook("BTCUSDT",new BinanceOrderBookOptions() { }

                if (oneinstrument2.Result.Success)
                {

                }

                oneinstrument.Data.ActivityUnpaused += () =>
                 {
                     MainWindow.LogMessage("Активность возвращена! " + Code, TextLog, TelegramBot);
                 };

                oneinstrument.Data.ActivityPaused += () =>
                {
                    MainWindow.LogMessage("Активность инструмента на паузе! " + Code, TextLog, TelegramBot);
                };

                oneinstrument.Data.ConnectionRestored += (ex) =>
                  {
                      MainWindow.LogMessage("Соединение инструмента восстановлено! " + Code, TextLog, TelegramBot);
                  };
                oneinstrument.Data.ConnectionLost += () =>
                {
                    MainWindow.LogMessage("Потеря соединения " + Code, TextLog, TelegramBot);
                };

                oneinstrument.Data.Exception += (ex) =>
                  {
                      MainWindow.LogMessage("Tick Error " + Code + " " + ex.Message, TextLog, TelegramBot);
                  };

                if (!oneinstrument.Success)
                    MessageBox.Show($"Failed to subscribe to price updates: {oneinstrument.Error}", "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            { MessageBox.Show(ex.ToString()); }


        }


        public async Task CancelSubscribe()
        {

        }

    }
}
