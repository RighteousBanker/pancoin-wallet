using PancoinWallet.Jobs;
using PancoinWallet.Core;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PancoinWallet.Utilities;
using System.Collections.ObjectModel;

namespace PancoinWallet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly string everyMinuteCron = "0 0/1 * 1/1 * ? *";

        public IScheduler scheduler;

        public FileStream ContactStream;
        public Wallet ActiveWallet;
        public double? Balance;
        SendTransactionWindow sendTransactionWindow;

        bool isMining = false;
        Miner miner = null;
        Task minerTask;

        public bool Connected = false;
        public static uint? Nonce = null;

        OpenFileDialog loadWalletDialog;


        public static HttpProvider httpProvider = new HttpProvider();

        ObservableCollection<string> log { get; } = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();

            listView.ItemsSource = log;

            ConnectionJob.mainWindow = this;

            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }
            if (!Directory.Exists("Wallets"))
            {
                Directory.CreateDirectory("Wallets");
            }

            loadWalletDialog = new OpenFileDialog();
            loadWalletDialog.Title = "Load wallet";

            buttonSend.IsEnabled = false;
            buttonMine.IsEnabled = false;

            ContactStream = new FileStream("Data\\contacts", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var jobs = new List<Tuple<Type, string>>()
            {
                new Tuple<Type, string>(typeof(ConnectionJob), everyMinuteCron),
            };

            ScheduleJobs(jobs).Wait();

            var connected = ConnectionJob.Handshake(httpProvider);
            RefreshUi(connected, null);
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            if (sendTransactionWindow == null)
            {
                sendTransactionWindow = new SendTransactionWindow(ActiveWallet, Nonce);
                sendTransactionWindow.Show();

                sendTransactionWindow.Closed += (senderObject, eventArgs) => { sendTransactionWindow = null; };
            }
        }

        private void ButtonContacts_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {loadWalletDialog.ShowDialog();

            if (loadWalletDialog.FileName == "")
                return;

            try
            {
                Nonce = null;
                var fileBytes = File.ReadAllBytes(loadWalletDialog.FileName);
                ActiveWallet = Wallet.FileDeserialize(Encoding.ASCII.GetString(fileBytes));
            }
            catch
            {
                ActiveWallet = null;
            }

            double? balance = null;

            if (ActiveWallet != null)
            {
                balance = ConnectionJob.GetStandardBalance(httpProvider, ActiveWallet.PublicKey);
            }

            RefreshUi(Connected, balance);
        }

        private void ButtonCreate_Click(object sender, RoutedEventArgs e)
        {
            ActiveWallet = new Wallet();

            Nonce = null;

            File.WriteAllText(@"Wallets\" + HexConverter.ToPrefixString(ActiveWallet.PublicKey), ActiveWallet.FileSerialize());

            RefreshUi(Connected, ConnectionJob.GetStandardBalance(httpProvider, ActiveWallet.PublicKey));
        }

        private void ButtonMine_Click(object sender, RoutedEventArgs e)
        {
            if (isMining)
            {
                StopMiner();
            }
            else
            {
                var minerTask = Task.Run(() => { StartMiner(); });
            }
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            var connected = ConnectionJob.Handshake(httpProvider);

            double? balance = null;

            if (ActiveWallet != null)
            {
                balance = ConnectionJob.GetStandardBalance(httpProvider, ActiveWallet.PublicKey);
            }

            RefreshUi(connected, balance);
        }

        async Task ScheduleJobs(List<Tuple<Type, string>> jobs)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            scheduler = await schedulerFactory.GetScheduler();

            foreach (var jobDescriptor in jobs)
            {
                IJobDetail job = JobBuilder.Create(jobDescriptor.Item1).Build();

                ITrigger jobTrigger = TriggerBuilder.Create().WithCronSchedule(jobDescriptor.Item2).WithIdentity(jobDescriptor.Item1.Name).Build();
                await scheduler.ScheduleJob(job, jobTrigger);
            }

            await scheduler.Start();
        }

        public void RefreshUi(bool connected, double? balance)
        {
            Connected = connected;
            Balance = balance;

            if (sendTransactionWindow != null && ActiveWallet != null)
            {
                sendTransactionWindow.SenderWallet = ActiveWallet;
            }

            if (!connected)
            {
                Write("No listening node on http://127.0.0.1:8125/");
            }

            if (ActiveWallet != null && Connected)
            {
                buttonSend.IsEnabled = true;
                buttonMine.IsEnabled = true;
            }
            else
            {
                buttonSend.IsEnabled = false;
                buttonMine.IsEnabled = false;
            }

            if (ActiveWallet != null)
            {
                addressLabel.Content = HexConverter.ToPrefixString(ActiveWallet.PublicKey);
                addressLabel.Foreground = Brushes.Green;
            }
            else
            {
                addressLabel.Content = "Not available";
                addressLabel.Foreground = Brushes.Red;
            }

            if (Connected)
            {
                statusLabel.Content = "Online";
                statusLabel.Foreground = Brushes.Green;
            }
            else
            {
                statusLabel.Content = "Offline";
                statusLabel.Foreground = Brushes.Red;
            }

            if (Balance != null)
            {
                balanceLabel.Content = Balance.Value.ToString();
                balanceLabel.Foreground = Brushes.Green;
            }
            else
            {
                balanceLabel.Content = "Not available";
                balanceLabel.Foreground = Brushes.Red;
            }
        }
    
        public async Task StartMiner()
        {
            Dispatcher.Invoke(() => 
            {
                isMining = true;
                buttonMine.Content = "Stop mining";
                Write("Miner started");
            });

            try
            {
                var mineableBlock = await Miner.GetMineableBlock(httpProvider);

                if (mineableBlock != null)
                {
                    Dispatcher.Invoke(() => { miner = new Miner(mineableBlock); });

                    while (isMining)
                    {
                        var minedBlock = await miner.MineRound(ActiveWallet);

                        if (minedBlock != null) //relay new block
                        {
                            Dispatcher.Invoke(() =>
                            {
                                Write($"Miner has found new block! Block height: {minedBlock.Height}, Hashrate: {miner.GetHashrate()}");
                                httpProvider.PeerPost<string, string>(HexConverter.ToPrefixString(minedBlock.Serialize()), @"http://127.0.0.1:8125/block/relay").Wait();
                            });

                            mineableBlock = await Miner.GetMineableBlock(new HttpProvider());

                            if (mineableBlock != null)
                            {
                                Dispatcher.Invoke(() => { miner = new Miner(mineableBlock); });
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            var newMineableBlock = await Miner.GetMineableBlock(httpProvider);

                            if (newMineableBlock.Height != mineableBlock.Height)
                            {
                                mineableBlock = newMineableBlock;
                            }
                        }
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => { Write("Miner could not get work from node. Check connection"); });
                }
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() => { Write($"Miner has encountered an exception\nStack trace: {e.StackTrace}\nMessage: {e.Message}"); });
            }

            Dispatcher.Invoke(() =>
            {
                StopMiner();
                Write("Miner stopped");
            });
        }

        public void StopMiner()
        {
            if (miner != null)
            {
                miner.Stop();
            }

            isMining = false;
            buttonMine.Content = "Mine";
        }

        public void Write(string message)
        {
            var time = DateTime.Now.TimeOfDay;
            log.Add($"[{time.Hours.ToString("00")}:{time.Minutes.ToString("00")}:{time.Seconds.ToString("00")}] {message}");
            scrollViewer.ScrollToBottom();
        }
    }
}
