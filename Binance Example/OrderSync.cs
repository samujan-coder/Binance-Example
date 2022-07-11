using Binance.Net.Clients;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;

namespace Binance_Example
{
    public class OrderSync
    {
        public delegate void EventNewOrder(BinanceFuturesOrder Order);
        public EventNewOrder NewOrder;

        public BinanceSocketClient SocketClient { get; set; }

        public BinanceClient BinanceClient { get; set; }

        public CryptoExchange.Net.Objects.WebCallResult<string> startOkay { get; set; }

        public string Symbol { get; set; }

        public async void Start()
        {
            var timer = new Timer(1000);
            timer.Elapsed += (s, e) => 
            {
                try
                {
                    var i = 0;
                    var orders = BinanceClient.UsdFuturesApi.Trading.GetOrdersAsync(Symbol).Result.Data;
                    if (orders != null)
                        foreach (var order in orders)
                        {
                            if (order.Symbol == Symbol && order.Status == OrderStatus.Filled)
                            {
                                //i++;
                                //Debug.WriteLine(i+ "{0} {1} {2}", order.Symbol, order.Status, order.Id);
                                NewOrder?.Invoke(order);
                            }
                        }
                }
                catch (Exception ex) { Debug.WriteLine(ex.ToString()); }
            
            };

            timer.Start();

            /*
             var subOkay = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, null, null, null, OnOrderUpdate, null, new System.Threading.CancellationToken());
             if (!subOkay.Success) Debug.WriteLine("Ошибка подписки на ордера");


            subOkay.Data.ActivityUnpaused += () =>
            {
                Debug.WriteLine("Активность возвращена [стратегия] *" );
                //RestoreOrders();
            };

            subOkay.Data.ActivityPaused += () =>
            {
                Debug.WriteLine("Активность на паузе [стратегия] *" );
            };

            subOkay.Data.ConnectionRestored += (ex) =>
            {
                Debug.WriteLine("Соединение восстановлено [стратегия] *" );
                //RestoreOrders();
            };
            subOkay.Data.ConnectionLost += () =>
            {
                Debug.WriteLine("Потеря соединения [стратегия] *" );
            };

            subOkay.Data.Exception += ex =>
            {
                Debug.WriteLine("ОШИБКА ордеров СТРАТЕГИЯ *");
            };*/

        }

        /*private void OnOrderUpdate(DataEvent<BinanceFuturesStreamOrderUpdate> orderupdate)
        {
            NewOrder?.Invoke(orderupdate);
        }*/
    }
}
