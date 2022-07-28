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
using System.Windows.Controls;

namespace Binance_Example
{
    public class OrderSync
    {
        public delegate void EventNewOrder(List<BinanceFuturesOrder> OrdersList);
        public EventNewOrder NewOrder;

        public delegate void EventNewOrder1(DataEvent<BinanceFuturesStreamOrderUpdate> orderupdate);
        public EventNewOrder1 NewOrder1;

        //private CryptoExchange.Net.Objects.CallResult<UpdateSubscription> subOkay;

        public BinanceSocketClient SocketClient { get; set; }

        public BinanceClient BinanceClient { get; set; }

        public TextBox TextLog { get; set; }

        public ExtendedTelegram TelegramBot { get; set; }

        public List<BinanceFuturesOrder> OrdersDataBase = new List<BinanceFuturesOrder>() { };


        public string Symbol { get; set; }
        public Timer timer { get; private set; }

        public async void Start()
        {

             timer = new Timer(1200);//1.2 сек
             timer.Elapsed += (s, e) =>
             {
                 RestoreOrders();
             };

           
             var startOkay = await BinanceClient.UsdFuturesApi.Account.StartUserStreamAsync();//фьючерсный 

            var subOkay = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, null, null, null, OnOrderUpdate, null, new System.Threading.CancellationToken());
            if (!subOkay.Success) Debug.WriteLine("Ошибка подписки на ордера");
            else Debug.WriteLine("успешно подписка!");

            /*
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
                MainWindow.LogMessage(String.Format("{0} Соединение по ордерам восстановлен", Symbol), TextLog, TelegramBot);
                RestoreOrders();

            };
            subOkay.Data.ConnectionLost += () =>
            {
                Debug.WriteLine("Потеря соединения [стратегия] *" );
                MainWindow.LogMessage(String.Format("{0} Потеря соединения по ордерам", Symbol), TextLog, TelegramBot);
            };

            subOkay.Data.Exception += ex =>
            {
                Debug.WriteLine("ОШИБКА ордеров СТРАТЕГИЯ *");
            };
            */
            timer.Start();

        }

        public void Stop()
        {
            if (timer != null) timer.Stop();
        }
        public async void RestoreOrders()
        {
            try
            {
               
                var orders = BinanceClient.UsdFuturesApi.Trading.GetOrdersAsync(Symbol).Result.Data.Where(order => order.Symbol == Symbol);
                if (orders != null)
                {
                    OrdersDataBase.Clear();
                    foreach (var order in orders)
                    {
                        if (order.Symbol == Symbol /*&& order.Status == OrderStatus.Filled*/)
                        {
                            //i++;
                            //Debug.WriteLine(i+ "{0} {1} {2}", order.Symbol, order.Status, order.Id);
                            OrdersDataBase.Add(order);
                           //NewOrder?.Invoke(order);
                        }
                    }

                    if (OrdersDataBase.Count != 0)
                    {
                        //MainWindow.LogMessage(String.Format("{0} Загружаю последние ордера в базу", Symbol), TextLog, TelegramBot);
                        Debug.WriteLine("Загружаю последние ордера в базу");
                        NewOrder?.Invoke(OrdersDataBase);
                    }
                }
            }
            catch (Exception ex) { MainWindow.LogMessage(String.Format("{0} Не критично! Ошибка получения ордеров {1}", Symbol,ex.Message), TextLog, TelegramBot); ; }
        }

        
        private void OnOrderUpdate(DataEvent<BinanceFuturesStreamOrderUpdate> orderupdate)
        {
           NewOrder1?.Invoke(orderupdate);
        }
    }
}
