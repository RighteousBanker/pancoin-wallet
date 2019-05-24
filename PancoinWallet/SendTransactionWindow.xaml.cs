using PancoinWallet.Core;
using PancoinWallet.Jobs;
using PancoinWallet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PancoinWallet
{
    /// <summary>
    /// Interaction logic for SendTransactionWindow.xaml
    /// </summary>
    public partial class SendTransactionWindow : Window
    {
        public Wallet SenderWallet;

        uint? _nonce;

        public SendTransactionWindow(Wallet wallet, uint? nonce)
        {
            InitializeComponent();
            SenderWallet = wallet;

            _nonce = nonce;

            double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            double windowWidth = this.Width;
            double windowHeight = this.Height;
            this.Left = (screenWidth / 2) - (windowWidth / 2);
            this.Top = (screenHeight / 2) - (windowHeight / 2);
        }
        
        HttpProvider httpProvider = new HttpProvider();

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            var destination = HexConverter.ToBytes(textBoxDestination.Text);

            if (destination != null)
            {
                double ammountDouble;

                if (double.TryParse(textBoxAmmount.Text, out ammountDouble))
                {
                    double feeDouble;

                    if (double.TryParse(textBoxFee.Text, out feeDouble))
                    {
                        var balance = ConnectionJob.GetSmallUnitsBalance(httpProvider, SenderWallet.PublicKey);

                        if (balance != null)
                        {
                            var ammount = GetSmallestUnits(ammountDouble);
                            var fee = GetSmallestUnits(feeDouble);

                            if (_nonce == null)
                            {
                                _nonce = httpProvider.PeerPost<string, uint>(HexConverter.ToPrefixString(SenderWallet.PublicKey), @"http://127.0.0.1:8125/transaction/count").Result;
                            }
                            else
                            {
                                _nonce = _nonce + 1;
                            }

                            if ((balance + fee - 1) > ammount)
                            {
                                var tx = new Transaction()
                                {
                                    Ammount = ammount,
                                    Destination = ByteManipulator.TruncateMostSignificatZeroBytes(destination),
                                    Fee = fee,
                                    Network = new byte[] { 1 },
                                    Nonce = _nonce.Value,
                                    Source = ByteManipulator.TruncateMostSignificatZeroBytes(SenderWallet.PublicKey)
                                };
                                
                                tx.Sign(SenderWallet.PrivateKey);

                                var response = httpProvider.PeerPost<string, string>(HexConverter.ToPrefixString(tx.Serialize()), @"http://127.0.0.1:8125/transaction/relay").Result;

                                if (response != "ok")
                                {
                                    MessageBox.Show("Sending transaction failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else //insufficient balance
                            {
                                MessageBox.Show("Insufficient balance", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else //could not fetch balance
                        {
                            MessageBox.Show("Could not fetch nonce", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else //invalid fee
                    {
                        MessageBox.Show("Invalid fee", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else //invalid ammount
                {
                    MessageBox.Show("Invalid invalid ammount", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else //invalid destination
            {
                MessageBox.Show("Invalid invalid destination", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static double GetStandardUnits(LargeInteger ammount)
        {
            return double.Parse(ammount.ToString()) / Math.Pow(10, 12);
        }

        public static LargeInteger GetSmallestUnits(double ammount)
        {
            return new LargeInteger(Convert.ToUInt64(Math.Pow(10, 12) * ammount));
        }
    }
}
