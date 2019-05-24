using PancoinWallet.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PancoinWallet.Core
{
    public class Miner
    {
        int hashesPerRound = 100000;

        bool stop = false;

        long numberOfAttempts = 0;
        

        LargeInteger nonce = new LargeInteger(CryptographyHelper.GenerateSecureRandomByteArray(4));
        Block minedBlock;
        Stopwatch stopWatch = new Stopwatch();

        public Miner(Block block)
        {
            minedBlock = block;
            stopWatch.Start();
        }

        public async Task<Block> MineRound(Wallet wallet)
        {
            minedBlock.MinerAddress = wallet.PublicKey;

            var difficultyBytes = minedBlock.Difficulty.GetBytes();

            for (int i = 0; i < hashesPerRound; i++)
            {
                if (stop)
                {
                    break;
                }

                minedBlock.Nonce = nonce.GetBytes();

                numberOfAttempts = numberOfAttempts + 1;

                if (ArrayManipulator.IsGreater(ByteManipulator.BigEndianTruncate(difficultyBytes, 32), minedBlock.GetHash(), difficultyBytes.Length)) //new block found
                {
                    stopWatch.Stop();
                    return minedBlock;
                }

                nonce = nonce + 1;
            }

            return null;
        }

        public void Stop()
        {
            stop = true;
        }

        public double GetHashrate()
        {
            return numberOfAttempts / stopWatch.Elapsed.TotalSeconds;
        }

        public async static Task<Block> GetMineableBlock(HttpProvider httpProvider)
        {
            try
            {
                return new Block(HexConverter.ToBytes(await httpProvider.PeerPost<string, string>("", @"http://127.0.0.1:8125/block/getwork")));
            }
            catch
            {
                return null;
            }
        }
    }
}
