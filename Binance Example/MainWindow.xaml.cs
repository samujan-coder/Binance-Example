using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Binance.Net.Clients;
using Binance.Net.Objects.Models.Spot.Socket;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Sockets;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Binance.Net.Enums;
using System.Linq;
using Binance.Net.Objects.Models.Futures.Socket;
using System;
using System.Collections.Generic;

namespace Binance_Example
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private string apiKey = "rH7bAyC2deozqWXYFrSCikoCfkxVjkeyvthppiUTnvSZyiFbcHdnXIyTgiauWFSk";//"8EW31T8PJKG14M1U5WLPVz7PTpy6OdQplbHEh1n9sMqPZfTY2O3t6CpAG5x49LRP";
        private string apiSecret = "AnyqX8c9NqPu0YhQOuzzQPpsH52fRdZ7zTvFg6wfCxQdCnlsW8J3aq87hqfi8qsl";//"XSE4AXN198B2przYws4JuDyWvAdoIbxrluA0IGsHAk47T2yqzlcLvRYJkWRapnuM";
        private BinanceSocketClient socketClient = new BinanceSocketClient() { };

        private object orderLock;// нужен для блокировки процесса 

        private string Symbol { get => (string)Instruments.SelectedItem; }
        private decimal lastprice;

        private CryptoExchange.Net.Objects.WebCallResult<string> startOkay;

        public ObservableCollection<AssetViewModel> Assets { get; set; }

        public ObservableCollection<MiniStopStrategy> stopStrategies { get; set; } = new ObservableCollection<MiniStopStrategy>() { };

        public ObservableCollection<Instrument> SpecialInstruments { get; set; } = new ObservableCollection<Instrument>() { };
        
        /// <summary>
        /// сразу возвращает готовый подписанный инструмент 
        /// </summary>
        private Instrument SelectedInstrument 
        {get
            {
                var instrument = SpecialInstruments.FirstOrDefault(s => s.Code == Symbol);
                if (instrument == null)
                {
                    instrument = new Instrument(Symbol, socketClient);
                    instrument.SubscribeLastPriceAsync();
                }
                SpecialInstruments.Add(instrument);
                return instrument;

            } }
        /// <summary
        /// Получение всего списка инструментов и подписка на изменение тикеров 
        /// </summary>
        /// <returns></returns>
        private async Task SubscribeToSymbols()
        {
            using (var client = new BinanceClient())
            {
                
                //спотовый рынок 
                //var result = await client.SpotApi.ExchangeData.GetPricesAsync();
                //фьючерсный 
                var result = await client.UsdFuturesApi.ExchangeData.GetPricesAsync();

                if (result.Success)
                {
                    Debug.WriteLine("получены инструменты {0}", result.Data.Count());
                    var listinstruments = new List<string>();
                    result.Data.ToList().ForEach(item => listinstruments.Add(item.Symbol));
                    Instruments.ItemsSource = listinstruments;
                }
                else
                    MessageBox.Show($"Error requesting data: {result.Error.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }


        }


        /// <summary>
        /// Подключение к Бинаня
        /// и подписка на разные событие 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            //подписаться на инструменты 
            SubscribeToSymbols();

            //установить ключи для подключения 
            BinanceClient.SetDefaultOptions(new BinanceClientOptions() { ApiCredentials = new ApiCredentials(apiKey, apiSecret), ReceiveWindow = TimeSpan.FromMilliseconds(1000), });

            using (var client = new BinanceClient())
            {
                //начать стриминг пользовательских данных 
                // var startOkay = await client.SpotApi.Account.StartUserStreamAsync();// спотовый 
                startOkay = await client.UsdFuturesApi.Account.StartUserStreamAsync();//фьючерсный 
                if (!startOkay.Success)
                {
                    MessageBox.Show($"Error starting user stream: {startOkay.Error.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else Debug.WriteLine("Успешное подключение!");


                //подписка на событие изменени ордеров и обновления балансов СПОТ
                // var subOkay = await socketClient.SpotStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data, OnOrderUpdate, null, OnAccountUpdate, null);
                // var subOkay = await socketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data,
                var subOkay = await socketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(startOkay.Data,
                    leverage =>
                    {
                        Debug.Print("Levarage {0}", leverage.Data.LeverageUpdateData.Leverage);
                    },
                    marginupdate =>
                    {
                        Debug.Print("margin {0}", marginupdate.Data.Positions.FirstOrDefault());// первая из списка позиция 
                    },
                    accountupdate =>
                    {
                        Debug.Print("account {0}", accountupdate.Data.UpdateData.Balances.FirstOrDefault());//первый баланс из списка
                    },
                    orderupdate =>
                    {
                        Debug.WriteLine("New info about orders {0}", orderupdate.Data.UpdateData.TradeId);
                        Debug.Print("order update {0} {1}", orderupdate.Data.UpdateData.Price, orderupdate.Data.UpdateData.Quantity);
                    },
                    keyexpired =>
                    {

                    }, new System.Threading.CancellationToken());

                if (!subOkay.Success)
                {
                    MessageBox.Show($"Error subscribing to user stream: {subOkay.Error.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //получение информации по балансам
                // var accountResult = await client.SpotApi.Account.GetAccountInfoAsync();//спотовый рынок 
                var accountResult = await client.UsdFuturesApi.Account.GetAccountInfoAsync();//фьючерсный рынок 

                if (accountResult.Success)
                {
                    //спотовый рынок
                    //Assets = new ObservableCollection<AssetViewModel>(accountResult.Data.Balances.Where(b => b.Available != 0 || b.Locked != 0).Select(b => new AssetViewModel() { Asset = b.Asset, Free = b.Available, Locked = b.Locked }).ToList());
                    //фьючерсный 
                    Assets = new ObservableCollection<AssetViewModel>(accountResult.Data.Assets.Where(b => b.AvailableBalance != 0).Select(b => new AssetViewModel() { Asset = b.Asset, Free = b.AvailableBalance }).ToList());
                    foreach (var asset in Assets) Debug.WriteLine("{0} {1} {2}", asset.Asset, asset.Free, asset.Locked);
                }
                else
                    MessageBox.Show($"Error requesting account info: {accountResult.Error.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }


        private async void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            using (var client = new BinanceClient())
            {

                //var result = await client.SpotApi.Trading.PlaceOrderAsync(Symbol, OrderSide.Buy, SpotOrderType.Limit, (decimal?)0.00022,  46300, timeInForce: TimeInForce.GoodTillCanceled);
                var result = await client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, OrderSide.Buy, FuturesOrderType.Limit, (decimal?)0.002, 2700, timeInForce: TimeInForce.GoodTillCanceled);
                if (result.Success)
                    MessageBox.Show("Order placed!", "Sucess", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Order placing failed: {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            stopStrategies.Clear();
            using (var client = new BinanceClient())
            {
                var result = await client.UsdFuturesApi.Trading.CancelAllOrdersAsync(Symbol, null, new System.Threading.CancellationToken());
                if (result.Success)
                    MessageBox.Show("All Canceled", "Sucess", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Failed to cancel : {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopOrder_Click(object sender, RoutedEventArgs e)
        {
            using (var client = new BinanceClient())
            {
                var stopprice = Math.Abs(lastprice * 0.98m); //2%

                var result = await client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, OrderSide.Sell, FuturesOrderType.StopMarket, (decimal?)10, stopPrice: lastprice - 0.0005m, timeInForce: TimeInForce.GoodTillCanceled);
                if (result.Success)
                    MessageBox.Show("Stop order placed!", "Sucess", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Order placing failed: {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);


            }
        }

        private async void PlaceStopOrder(decimal price)
        {
            using (var client = new BinanceClient())
            {
                var stopprice = Math.Abs(lastprice * 0.98m); //2%

                var result = await client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, OrderSide.Sell, FuturesOrderType.StopMarket, (decimal?)10, stopPrice: lastprice - 0.0005m, timeInForce: TimeInForce.GoodTillCanceled);
                if (result.Success)
                    MessageBox.Show("Stop order placed!", "Sucess", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Order placing failed: {result.Error.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        /// <summary>
        /// Старт стратегии 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var entry = true;

            if (BuyRadio.IsChecked == true & SellRadio.IsChecked == true)
            {
                MessageBox.Show("Выбраны два направления, выберите один");
                return;
            }
            if (BuyRadio.IsChecked == false & SellRadio.IsChecked == false) 
            {
                Debug.WriteLine("Выбран режим входа без входа в рынок"); 
                entry = false;
            }

            using (var client = new BinanceClient() )
            {
                
                var ordersucess = false;

                if (entry)
                    ordersucess = (await client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, SellRadio.IsChecked == true ? OrderSide.Sell : OrderSide.Buy, FuturesOrderType.Market, quantity: decimal.Parse(Volume.Text))).Success;

                if (ordersucess || !entry)
                {
                    stopStrategies.ToList().ForEach(s => s.Start());
                    
                }
                else
                {
                    MessageBox.Show("Нет условий для входа, либо заявка не исполнилась");
                }

            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var stopbot = new MiniStopStrategy(BuyStop.IsChecked == true ? Direction.Buy : Direction.Sell,
                    SelectedInstrument, decimal.Parse(StopLevel.Text), decimal.Parse(Comission.Text), decimal.Parse(AlgoLevel.Text))
                {
                    SocketClient = socketClient,
                    startOkay = startOkay,
                    Volume = decimal.Parse(VolumeTextBox.Text),
                    WaitForEntryStop = false,
                    PunktsForLevel2 = decimal.Parse(Level2.Text)
                };

                stopStrategies.Add(stopbot);
                var textstop = string.Format("Добавлен стоп {0} цена стопа {1} цена условия {2} второй стоп {3}", stopbot.Direction, stopbot.StopLevel, stopbot.LevelActivator, stopbot.NewStopLevel);
                MessageBox.Show(textstop);
                Debug.Print(textstop);
             }
            catch (Exception ex)
            {
              MessageBox.Show(ex.ToString());
            }

        }


    }
}
