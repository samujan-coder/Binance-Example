using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.CommonObjects;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using Telegram.Bot;

namespace Binance_Example
{

    public enum Direction
    {
        Buy, Sell
    }
    public class MiniStopStrategy : ObservableObject
    {
        public Instrument Instrument { get; set; }

        public Direction Direction { get; set; }

        /// <summary>
        /// Уровень установки стопа
        /// </summary>

        private decimal _stoplevel;
        public decimal StopLevel { get => Direction == Direction.Buy ? _stoplevel + N : _stoplevel - N; set { _stoplevel = value; } }

        public decimal Comission { get; set; }

        /// <summary>
        /// Специальный уровень, после которого мы выставляем уже другой стоп 
        /// </summary>
        public decimal LevelActivator { get; set; }

        /// <summary>
        /// Специальный уровень для нового уже стопа...
        /// </summary>
        public decimal StopLevelComission { get => Direction == Direction.Sell ? StopLevel - Comission : StopLevel + Comission; }
        //public BinanceClient BinanceClient { get; set; }
        public BinanceSocketClient SocketClient { get; set; }
        public BinanceClient Client { get; set; }

        private decimal punktsForActivatorPrice;
        private ObservableCollection<MiniStopStrategy> AllStopStrategies;

        private long stoporderid = 0;

        public CryptoExchange.Net.Objects.WebCallResult<string> startOkay { get; set; }

        private object locker = new object();

        private bool StrategyOn = true;

        public decimal Volume { get; set; }

        public bool Opposite { get; set; }

       //public decimal PunktsForLevel2 { get; set; }

        public TextBox TextLog { get; set; }

        public int Id { get; set; }

        /// <summary>
        /// алгопункты
        /// </summary>
        public decimal AlgoPunkts { get; set; }

        
        public decimal StopPunkts { get; set; }

        public decimal StopPunkts2 { get; set; }

        public ExtendedTelegram TelegramBot { get; set; }

        private ObservableCollection<MiniStopStrategy> childStopStrategies { get; set; } = new ObservableCollection<MiniStopStrategy>() { };

        public OrderSync OrderUpdate { get; set; }


        /// <summary>
        /// Ожидать стопа, который сработает в другую сторону 
        /// </summary>
        public bool WaitForEntryStop { get; set; }

        /// <summary>
        /// специальный отступ
        /// </summary>
        public decimal N { get; set; }

        //дописать. Тут надо много всего указать
        public MiniStopStrategy (Direction direction, Instrument instrument, decimal _stoplevel, decimal _punktsForActivatorPrice, decimal algopunkts)
        {
            Direction = direction;

            //проще передать все параметры так 
            Instrument = instrument;
            StopLevel = _stoplevel;
            Comission = _punktsForActivatorPrice;
            AlgoPunkts = algopunkts;
            LevelActivator = Direction == Direction.Buy ? StopLevel + AlgoPunkts : StopLevel - AlgoPunkts;

        }

       // public decimal GetStopLevelWithOffset() => Direction == Direction.Buy ? StopLevel - N : StopLevel +  N; 

        public void LogInitialSettings ()
        {
            var textstop = string.Format("{0} Создан стоп {1} цена стопа {2} цена условия {3} противоположный стоп {4}", Id, Direction, StopLevel, LevelActivator, StopLevelComission);
            MainWindow.LogMessage(textstop, TextLog, TelegramBot);

        }

        public MiniStopStrategy(MiniStopStrategy miniStopStrategy)
        {
            Instrument = miniStopStrategy.Instrument;
            SocketClient = miniStopStrategy.SocketClient;
            Client = miniStopStrategy.Client;
            startOkay = miniStopStrategy.startOkay;
            Volume = miniStopStrategy.Volume;
            Direction = miniStopStrategy.Direction;
            Comission = miniStopStrategy.Comission;
            Id = miniStopStrategy.Id;
            TextLog = miniStopStrategy.TextLog;
            AlgoPunkts = miniStopStrategy.AlgoPunkts;
            StopPunkts = miniStopStrategy.StopPunkts;
            StopPunkts2 = miniStopStrategy.StopPunkts2;
            TelegramBot = miniStopStrategy.TelegramBot;
            OrderUpdate = miniStopStrategy.OrderUpdate;

        }

        public async void Stop()
        {

            StrategyOn = false;

            try
            {
                MainWindow.LogMessage(String.Format("{0} Остановка бота, удаление ордеров + Отписка от обновления ордеров", Id), TextLog, TelegramBot);
                OrderUpdate.NewOrder -= OnOrderUpdate;

                if (stoporderid == 0) return;

                var subOkay = await Client.UsdFuturesApi.Trading.CancelOrderAsync(Instrument.Code, orderId: stoporderid);

                if (!subOkay.Success) MainWindow.LogMessage(String.Format("{0} Ордер не получилось отменить", Id), TextLog, TelegramBot);

            }
            catch (Exception ex)
            {
                MainWindow.LogMessage(String.Format("{0} Ошибка отмены ордера {1}", Id, ex.ToString), TextLog, TelegramBot);
            }

            Instrument.PropertyChanged -= CheckConditions;

        }

        public async void StopChild()
        {
            foreach (var stopStrategy in childStopStrategies) stopStrategy.Stop();
        }

        public void StartChild(bool waitForEntry = false)
        {
            // дописать уровни и объемы 
            //дописать комиссию, которая должна быть учтена при выставлении стопа 

            //waitForEntry - выставляет ордер в оппозитную сторону и ждет его исполнения
            //!waitForEntry - сразу выставляет "рабочий стоп"

            var childbot = new MiniStopStrategy(this);
            childStopStrategies.Add(childbot);
            childbot.WaitForEntryStop = waitForEntry;

            if (!waitForEntry)
            { // обычная мини стоп стратегия
                childbot.StopLevel = Direction == Direction.Buy ? StopLevel + StopPunkts2 : StopLevel - StopPunkts2;
                childbot.LevelActivator = Direction == Direction.Buy ? childbot.StopLevel + AlgoPunkts : childbot.StopLevel - AlgoPunkts;
            }
            else
            {
                childbot.StopLevel = StopLevel;
            }

            childbot.Start();

        }

        public async void RestoreOrders()
        {
            if (stoporderid != 0)
            {
                MainWindow.LogMessage("Насильно проверяю последнее состоянее ордера" + Id, TextLog, TelegramBot);
               restoredOrder = Client.UsdFuturesApi.CommonFuturesClient.GetOrderAsync(stoporderid.ToString(), Instrument.Code, new System.Threading.CancellationToken()).Result;
               CheckOrderCondition(decimal.Parse(restoredOrder.Data.Id), restoredOrder.Data.Status ==CommonOrderStatus.Filled);
            } 
        }

        public async void Start()
        {

            if (OrderUpdate != null)
                OrderUpdate.NewOrder += OnOrderUpdate;
            //var subOkay = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, null, null, null, OnOrderUpdate, null, new System.Threading.CancellationToken());
            //if (!subOkay.Success) MainWindow.LogMessage(String.Format("{0} Ошибка подписки на обновление ордеров", Id), TextLog, TelegramBot);

            var startText = "";

            if (!WaitForEntryStop) // классический старт
            {
                startText = string.Format("{0} Размещаем стоп {1} цена стопа {2} цена условия {3} противоположный стоп {4}", Id, Direction, StopLevel, LevelActivator, StopLevelComission);
                PlaceStopOrderAsync(Direction, Instrument.Code, StopLevelComission, Volume);
            }
            else
            {
                var oppositedirection = Direction == Direction.Buy ? Direction.Sell : Direction.Buy;
                startText = string.Format("{0} Размещаем стоп {1} цена стопа {2}", Id, oppositedirection, StopLevel);
                PlaceStopOrderAsync(oppositedirection, Instrument.Code, StopLevelComission, Volume);
            }
            MainWindow.LogMessage(startText, TextLog, TelegramBot);
        }

        private void OnOrderUpdate (DataEvent<BinanceFuturesStreamOrderUpdate> orderupdate)
        {
            lock (locker)
            {
                CheckOrderCondition(orderupdate.Data.UpdateData.OrderId, orderupdate.Data.UpdateData.Status==OrderStatus.Filled);
            }
            
        }

        public void CheckOrderCondition(decimal OrderId, bool IsFilled )
        {
            if (stoporderid != 0)
            {
                if (OrderId == stoporderid && IsFilled)
                {
                    stoporderid = 0;

                    Debug.WriteLine("Стоп исполнился!");

                    if (!WaitForEntryStop)
                    {

                        MainWindow.LogMessage(string.Format("{0} Обычный стоп исполнен!Проверяем условия {1} цена стопа {2} цена условия {3}", Id, Direction, StopLevel, LevelActivator), TextLog, TelegramBot);
                        CheckConditions();
                    }
                    else
                    {
                        MainWindow.LogMessage(string.Format("{0} Исполнился оппозитный стоп! Останавливаем и запускаем новую", Id), TextLog, TelegramBot);
                        Stop();
                        //создаем новую мини стоп бот стратегию, но уже с классическим 
                        StartChild();

                    }
                }
            }
           // Debug.WriteLine("New info about orders {0}", orderupdate.Data.UpdateData.TradeId);
            //Debug.Print("order update {0} {1}", orderupdate.Data.UpdateData.Price, orderupdate.Data.UpdateData.Quantity);
        }

        public async Task PlaceStopOrderAsync(Direction direction, string code, decimal stopprice, decimal volume)
        {
            StopLevel = stopprice;


            var result = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(code, direction == Direction.Buy ? OrderSide.Buy : OrderSide.Sell, FuturesOrderType.StopMarket, volume, stopPrice: stopprice, timeInForce: TimeInForce.GoodTillCanceled);

            if (result.Success)
            {
                stoporderid = result.Data.Id;
                //Debug.WriteLine("Stop order placed!", "Sucess");
                MainWindow.LogMessage(string.Format("{0} Стоп ордер успешно размещен", Id), TextLog, TelegramBot);
            }
            else
            {
                MainWindow.LogMessage(string.Format("{0} !Ордер не выставлен! {1} {2} opposite:{3} {4}", Id, Direction, StopLevel, WaitForEntryStop, result.Error.Message), TextLog, TelegramBot, true);
                //Debug.WriteLine($"Order placing failed: {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error); 
            }


        }

        private void CheckConditions()
        {
            //подписка на изменение цены 
            Instrument.PropertyChanged += CheckConditions;
        }

        decimal lastprice = 0;
        private CryptoExchange.Net.Objects.WebCallResult<Order> restoredOrder;

        ///проверка основных условий 
        private void CheckConditions(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            lock (locker)
            {
                if (!StrategyOn) return;
               
                var price = Instrument.LastPrice;

                if (lastprice == price) return;
                lastprice= price;

                Debug.Print("Отслеживаем условия {0}", price);

                if (Direction == Direction.Buy && price > LevelActivator)
                {
                    Debug.Print("Сработало условие. Цена выше уровня {0}", LevelActivator);
                    MainWindow.LogMessage(string.Format("{0} Сработало условие! Остановка и запуск новой. Цена выше уровня {1}", Id, LevelActivator), TextLog, TelegramBot);

                    Stop();
                    StartChild(true);
                }

                if (Direction == Direction.Sell && price < LevelActivator)
                {
                    Debug.Print("Сработало условие. Цена ниже уровня {0}", LevelActivator);
                    MainWindow.LogMessage(string.Format("{0} Сработало условие! Остановка и запуск новой. Цена ниже уровня {1}", Id, LevelActivator), TextLog, TelegramBot);

                    Stop();
                    StartChild(true);

                }
            }
        }
    }
}
