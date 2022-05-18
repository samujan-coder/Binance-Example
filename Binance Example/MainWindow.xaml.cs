using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Binance.Net.Clients;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Binance.Net.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using Telegram.Bot;

namespace Binance_Example
{

    /// <summary>
    /// Производный класс
    /// </summary>
    public partial class ExtendedTelegram
    {
        public TelegramBotClient telegramBotClient { get; set; } =  new TelegramBotClient("5176340248:AAEIkNFIRQyuJg-dcF0cVP81V8YqzSodwAc");
        public string TelegramUserKey { get; set; }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private ExtendedTelegram bot { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            RealAllSettings();
            
          

        }
        private string apiKey;
        private string apiSecret;
        private BinanceSocketClient socketClient = new BinanceSocketClient() { };


        private string Symbol { get => (string)Instruments.SelectedItem; }
        private decimal lastprice;

        private CryptoExchange.Net.Objects.WebCallResult<string> startOkay;

        public ObservableCollection<AssetViewModel> Assets { get; set; }

        public ObservableCollection<MiniStopStrategy> stopStrategies { get; set; } = new ObservableCollection<MiniStopStrategy>() { };

        public ObservableCollection<Instrument> SpecialInstruments { get; set; } = new ObservableCollection<Instrument>() { };



        private void RealAllSettings()
        {
            try
            {
                string[] lines = File.ReadAllLines("api.txt");

                apiKey = lines[1];
                apiSecret = lines[2];
                if(bot == null) bot = new ExtendedTelegram();
                bot.TelegramUserKey = lines[3];

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }



        public async static void LogMessage(string message, TextBox LogTextBox,ExtendedTelegram extendedTelegram, bool error = false)
        {
            var logmessage = DateTime.Now + " | " + message;
            LogTextBox.Dispatcher.Invoke(() =>
            {
                
                LogTextBox.AppendText(logmessage + Environment.NewLine);
                LogTextBox.ScrollToEnd();
                var logger = new StreamWriter(DateTime.Now.ToString("dd_MM_yyyy") + ".txt", true);
                logger.WriteLineAsync(logmessage);
                logger.Close();
            });

            extendedTelegram.telegramBotClient.SendTextMessageAsync(extendedTelegram.TelegramUserKey, logmessage);

            if (error)
            MessageBox.Show(message,"Ошибка");

        }

        /// <summary>
        /// сразу возвращает готовый подписанный инструмент 
        /// </summary>
        private Instrument SelectedInstrument 
        {
            get
            {
                return SubscribeInitialInstrument();

            } 
        }

 

        private Instrument SubscribeInitialInstrument()
        {
            var instrument = SpecialInstruments.FirstOrDefault(s => s.Code == Symbol);
            if (instrument == null)
            {
                instrument = new Instrument(Symbol, socketClient);
                instrument.SubscribeLastPriceAsync();
            }
            SpecialInstruments.Add(instrument);
            return instrument;
        }

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

                    LogMessage("Подписка на инструменты успешно",LogTextBox,bot);
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
                else Debug.WriteLine("Ошибка подключения!");

                // var accountResult = await client.SpotApi.Account.GetAccountInfoAsync();//спотовый рынок 
                var accountResult = await client.UsdFuturesApi.Account.GetAccountInfoAsync();//фьючерсный рынок 

                if (accountResult.Success)
                {
                    //спотовый рынок
                    //Assets = new ObservableCollection<AssetViewModel>(accountResult.Data.Balances.Where(b => b.Available != 0 || b.Locked != 0).Select(b => new AssetViewModel() { Asset = b.Asset, Free = b.Available, Locked = b.Locked }).ToList());
                    //фьючерсный 
                    Assets = new ObservableCollection<AssetViewModel>(accountResult.Data.Assets.Where(b => b.AvailableBalance != 0).Select(b => new AssetViewModel() { Asset = b.Asset, Free = b.AvailableBalance }).ToList());
                    foreach (var asset in Assets) LogMessage( string.Format("{0} {1} {2}", asset.Asset, asset.Free, asset.Locked),LogTextBox,bot);
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

            stopStrategies.ToList().ForEach(s => s.Stop());
            stopStrategies.Clear();
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

            using (var client = new BinanceClient())
            {

               var ordersucess = (await client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, SellRadio.IsChecked == true ? OrderSide.Sell : OrderSide.Buy, FuturesOrderType.Market, quantity: decimal.Parse(Volume.Text))).Success;
               stopStrategies.ToList().ForEach(s => s.Start());

            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {/*
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
            */
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {

                if (SelectedInstrument.LastPrice == 0) 
                { MessageBox.Show("Нет последней цены инструмента"); return; }

                string[] lines = File.ReadAllLines("stops.txt");

                for (int i = 1; i < lines.Count(); i++)
                {
                    string[] variables = lines[i].Split(";");
                    Direction direction = variables[0] == "Buy" ? Direction.Buy : Direction.Sell;

                    var StopLevelPoints = decimal.Parse(variables[1]);
                    //первый расчет идет из последней цены, поэтому насильно передаем
                    var stoplevel = direction == Direction.Buy ? SelectedInstrument.LastPrice + StopLevelPoints : SelectedInstrument.LastPrice - StopLevelPoints;
                    var algopunkts = decimal.Parse(variables[2]);
                    var comission = decimal.Parse(variables[3]);
                    var stoppunkts2 = decimal.Parse(variables[4]);
                    var volume = decimal.Parse(variables[5]);


                    var stopbot = new MiniStopStrategy(direction,
                        SelectedInstrument, stoplevel, comission, algopunkts)
                    {
                        SocketClient = socketClient,
                        startOkay = startOkay,
                        Volume = volume,
                        WaitForEntryStop = false,
                        TextLog = LogTextBox,
                        Id = i,
                        StopPunkts = StopLevelPoints,
                        StopPunkts2 = stoppunkts2,
                        TelegramBot = bot,
                    };

                    stopbot.LogInitialSettings();
                    stopStrategies.Add(stopbot);
                   
                }
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
            }

        }

        private void Instruments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SubscribeInitialInstrument();
        }
    }
}
