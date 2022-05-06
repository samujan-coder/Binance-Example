using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;

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
        public decimal StopLevel { get; set; }

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
       

        /// <summary>
        /// Ожидать стопа, который сработает в другую сторону 
        /// </summary>
        public bool WaitForEntryStop { get; set; }

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

        public void LogInitialSettings ()
        {
            var textstop = string.Format("{0} Создан стоп {1} цена стопа {2} цена условия {3} противоположный стоп {4}", Id, Direction, StopLevel, LevelActivator, StopLevelComission);
            MainWindow.LogMessage(textstop, TextLog);

        }

        public MiniStopStrategy(MiniStopStrategy miniStopStrategy)
        {
            Instrument = miniStopStrategy.Instrument;
            SocketClient = miniStopStrategy.SocketClient;
            startOkay = miniStopStrategy.startOkay;
            Volume = miniStopStrategy.Volume;
            Direction = miniStopStrategy.Direction;
            Comission = miniStopStrategy.Comission;
            Id = miniStopStrategy.Id;
            TextLog = miniStopStrategy.TextLog;
            AlgoPunkts = miniStopStrategy.AlgoPunkts;
            StopPunkts = miniStopStrategy.StopPunkts;
            StopPunkts2 = miniStopStrategy.StopPunkts2;

        }

        public async void Stop()
        {
           
                StrategyOn = false;

                try
                {
                    if (stoporderid != 0)
                        using (var client = new BinanceClient())
                        {
                            var subOkay = await client.UsdFuturesApi.Trading.CancelOrderAsync(Instrument.Code, orderId: stoporderid);

                            if (!subOkay.Success) MainWindow.LogMessage(String.Format("{0} Ордер не получилось отменить", Id), TextLog);

                        }
                }
                catch (Exception ex)
                {
                    MainWindow.LogMessage(String.Format("{0} Ошибка отмены ордера {1}", Id, ex.ToString), TextLog);
                }

                Instrument.PropertyChanged -= CheckConditions;
            
        }

        public void StartChild(bool waitForEntry = false)
        {
            // дописать уровни и объемы 
            //дописать комиссию, которая должна быть учтена при выставлении стопа 

            //waitForEntry - выставляет ордер в оппозитную сторону и ждет его исполнения
            //!waitForEntry - сразу выставляет "рабочий стоп"

            var childbot = new MiniStopStrategy(this);
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

        public async void Start()
        {

            
            

            using (var client = new BinanceClient())
            {
                var subOkay = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, null, null, null, OnOrderUpdate, null, new System.Threading.CancellationToken());

                if (!subOkay.Success) MainWindow.LogMessage(String.Format("{0} Ошибка подписки на обновление ордеров", Id), TextLog);
            
            }

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
            MainWindow.LogMessage(startText, TextLog);
        }

        private void OnOrderUpdate (DataEvent<BinanceFuturesStreamOrderUpdate> orderupdate)
        {
            lock (locker)
            {
                if (stoporderid != 0)
                {
                    if (orderupdate.Data.UpdateData.OrderId == stoporderid && orderupdate.Data.UpdateData.Status == OrderStatus.Filled)
                    {
                        stoporderid = 0;

                        Debug.WriteLine("Стоп исполнился!");

                        if (!WaitForEntryStop)
                        {
                            
                            MainWindow.LogMessage(string.Format("{0} Обычный стоп исполнен!Проверяем условия {1} цена стопа {2} цена условия {3}", Id, Direction, StopLevel, LevelActivator), TextLog);
                            CheckConditions();
                        }
                        else
                        {
                            MainWindow.LogMessage(string.Format("{0} Исполнился оппозитный стоп! Останавливаем и запускаем новую", Id), TextLog);
                            Stop();
                            //создаем новую мини стоп бот стратегию, но уже с классическим 
                            StartChild();

                        }
                    }
                }
                Debug.WriteLine("New info about orders {0}", orderupdate.Data.UpdateData.TradeId);
                Debug.Print("order update {0} {1}", orderupdate.Data.UpdateData.Price, orderupdate.Data.UpdateData.Quantity);
            }
            
        }

        public async Task PlaceStopOrderAsync(Direction direction, string code, decimal stopprice, decimal volume)
        {
            StopLevel = stopprice;
            using (var client = new BinanceClient())
            {
               
                var result = await client.UsdFuturesApi.Trading.PlaceOrderAsync(code, direction==Direction.Buy? OrderSide.Buy: OrderSide.Sell, FuturesOrderType.StopMarket, volume, stopPrice: stopprice, timeInForce: TimeInForce.GoodTillCanceled);

                if (result.Success)
                {
                    stoporderid = result.Data.Id;
                    //Debug.WriteLine("Stop order placed!", "Sucess");
                    MainWindow.LogMessage(string.Format("{0} Стоп ордер успешно размещен", Id), TextLog);
                }
                else
                {
                    MainWindow.LogMessage(string.Format("{0} !Ордер не выставлен! {1} {2} opposite:{3} {4}", Id, Direction,StopLevel, WaitForEntryStop,result.Error.Message), TextLog,true);
                    //Debug.WriteLine($"Order placing failed: {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error); 
                }

            }
        }

        private void CheckConditions()
        {
            //подписка на изменение цены 
            Instrument.PropertyChanged += CheckConditions;
        }

        decimal lastprice = 0;
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
                    MainWindow.LogMessage(string.Format("{0} Сработало условие! Остановка и запуск новой. Цена выше уровня {1}", Id, LevelActivator), TextLog);

                    Stop();
                    StartChild(true);
                }

                if (Direction == Direction.Sell && price < LevelActivator)
                {
                    Debug.Print("Сработало условие. Цена ниже уровня {0}", LevelActivator);
                    MainWindow.LogMessage(string.Format("{0} Сработало условие! Остановка и запуск новой. Цена ниже уровня {1}", Id, LevelActivator), TextLog);

                    Stop();
                    StartChild(true);

                }
            }
        }
    }
}
