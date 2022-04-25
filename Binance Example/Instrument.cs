using Binance.Net.Clients;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Binance_Example
{
    public class Instrument : ObservableObject
    {
        public string Code;

        private decimal _price;
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

                var oneinstrument = await socketClient.UsdFuturesStreams.SubscribeToTickerUpdatesAsync(Code, data =>
                {
                    LastPrice = data.Data.LastPrice;

                }, subscriveforprice);

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
