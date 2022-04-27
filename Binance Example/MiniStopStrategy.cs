using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.CommonObjects;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        public decimal NewStopLevel { get => Direction == Direction.Sell ? StopLevel - Comission : StopLevel + Comission; }
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

        public decimal PunktsForLevel2 { get; set; }

        public TextBox TextLog { get; set; }

        public int Id { get; set; }
       

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
            LevelActivator = Direction == Direction.Buy ? StopLevel + algopunkts : StopLevel - algopunkts;

        }

        public MiniStopStrategy (MiniStopStrategy miniStopStrategy)
        {
            Instrument = miniStopStrategy.Instrument;
            SocketClient = miniStopStrategy.SocketClient;
            startOkay = miniStopStrategy.startOkay;
            Volume = miniStopStrategy.Volume;
            PunktsForLevel2 = miniStopStrategy.PunktsForLevel2;
            Direction = miniStopStrategy.Direction;
            Comission = miniStopStrategy.Comission;
            Id = miniStopStrategy.Id;

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

        public void StartChild(bool special = false)
        {
            // дописать уровни и объемы 
            //дописать комиссию, которая должна быть учтена при выставлении стопа 

            //special - выставляет ордер в оппозитную сторону и ждет его исполнения
            //!special - сразу выставляет "рабочий стоп"

            var childbot = new MiniStopStrategy(this);
            if (!special)
            { // обычная мини стоп стратегия
                childbot.WaitForEntryStop = false;
                childbot.StopLevel = Direction == Direction.Sell ? StopLevel - PunktsForLevel2 : StopLevel + PunktsForLevel2;
            }
            if(special)
            {
                childbot.StopLevel = StopLevel;
                childbot.WaitForEntryStop = true;
            }

            childbot.Start();

        }

        public async void Start()
        {

            var startText = string.Format("{0} Добавлен стоп {1} цена стопа {2} цена условия {3} второй стоп {4}", Id,Direction, StopLevel, LevelActivator, NewStopLevel);
            MainWindow.LogMessage(startText, TextLog);

            using (var client = new BinanceClient())
            {
                var subOkay = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, null, null, null, OnOrderUpdate, null, new System.Threading.CancellationToken());

                if (!subOkay.Success) MainWindow.LogMessage(String.Format("{0} Ошибка подписки на обновление ордеров", Id), TextLog);
            
            }

            if (!WaitForEntryStop) // классический старт
            { 
                PlaceStopOrderAsync(Direction, Instrument.Code, StopLevel, Volume); 
            }
            else
            {
                var oppositedirection = Direction == Direction.Buy ? Direction.Sell : Direction.Buy;
                PlaceStopOrderAsync(oppositedirection, Instrument.Code, NewStopLevel, Volume);
            }
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
                            
                            MainWindow.LogMessage(string.Format("{0} Начинаем проверять условия", Id), TextLog);
                            CheckConditions();
                        }
                        else
                        {
                            MainWindow.LogMessage(string.Format("{0} Останавливаем и запускаем новую", Id), TextLog);
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
                    MainWindow.LogMessage(string.Format("{0} Стоп ордеру успешно размещен {1} {2}", Id, result.Data.PositionSide, result.Data.Price), TextLog);
                }
                else
                {
                    MainWindow.LogMessage(string.Format("{0} !Ордер не выставлен! {1} {2} opposite:{3}", Id, Direction,StopLevel, WaitForEntryStop), TextLog);
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
                    MainWindow.LogMessage(string.Format("{0} Сработало условие. Цена выше уровня {0}", Id, LevelActivator), TextLog);

                    Stop();
                    StartChild(true);
                }

                if (Direction == Direction.Sell && price < LevelActivator)
                {
                    Debug.Print("Сработало условие. Цена ниже уровня {0}", LevelActivator);
                    MainWindow.LogMessage(string.Format("{0} Сработало условие. Цена ниже уровня {0}", Id, LevelActivator), TextLog);

                    Stop();
                    StartChild(true);

                }
            }
        }
    }
}
