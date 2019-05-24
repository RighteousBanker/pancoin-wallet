using PancoinWallet.Utilities;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PancoinWallet.Jobs
{
    public class ConnectionJob : IJob
    {
        HttpProvider httpProvider = new HttpProvider();
        public static MainWindow mainWindow;

        public async Task Execute(IJobExecutionContext context)
        {
            bool connected = Handshake(httpProvider);

            double? balance = null;

            if (mainWindow.ActiveWallet != null)
            {
                var smallUnitsBalance = GetSmallUnitsBalance(httpProvider, mainWindow.ActiveWallet.PublicKey);

                if (smallUnitsBalance != null)
                {
                    balance = SendTransactionWindow.GetStandardUnits(smallUnitsBalance);
                }
            }

            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.RefreshUi(connected, balance);
            });
        }

        public static bool Handshake(HttpProvider httpProvider)
        {
            var response = httpProvider.PeerPost<string, string>("hello", @"http://127.0.0.1:8125/contact/handshake").Result;

            if (response != null && response == "ok")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static LargeInteger GetSmallUnitsBalance(HttpProvider httpProvider, byte[] destination)
        {
            LargeInteger ret = null;
            var balanceHex = httpProvider.PeerPost<string, string>(HexConverter.ToPrefixString(destination), @"http://127.0.0.1:8125/transaction/balance").Result;

            if (balanceHex != null)
            {
                var balanceBytes = HexConverter.ToBytes(balanceHex);

                if (balanceBytes != null)
                {
                    ret = new LargeInteger(balanceBytes);
                }
            }

            return ret;
        }

        public static double? GetStandardBalance(HttpProvider httpProvider, byte[] destination)
        {
            LargeInteger smallUnitsBalance = GetSmallUnitsBalance(httpProvider, mainWindow.ActiveWallet.PublicKey);

            double? ret = null;

            if (smallUnitsBalance != null)
            {
                ret = SendTransactionWindow.GetStandardUnits(smallUnitsBalance);
            }

            return ret;
        }
    }
}
