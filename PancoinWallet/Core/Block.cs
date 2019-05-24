using Encoders;
using PancoinWallet.Utilities;
using System;
using System.Collections.Generic;

namespace PancoinWallet.Core
{
    public class Block
    {
        public int Height { get; set; }
        public uint Timestamp { get; set; }
        public LargeInteger Difficulty { get; set; }
        public byte[] Nonce { get; set; }
        public byte[] MinerAddress { get; set; }
        public byte[] PreviousBlockHash { get; set; }

        public List<Transaction> Transactions { get; set; }

        public static readonly LargeInteger LowestDifficulty = new LargeInteger(HexConverter.ToBytes("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));

        public Block()
        {

        }

        public Block(byte[] rlp)
        {
            var decoded = RLP.Decode(rlp);

            if (decoded.Count == 7 && decoded[1] != null)
            {
                Height = (int)(decoded[0] != null ? ByteManipulator.GetUInt32(decoded[0]) : 0);
                Timestamp = ByteManipulator.GetUInt32(decoded[1]);
                Difficulty = new LargeInteger(decoded[2] ?? new byte[32]);
                Nonce = decoded[3] != null ? ByteManipulator.BigEndianTruncate(decoded[3], 32) : new byte[32];
                MinerAddress = decoded[4] != null ? ByteManipulator.BigEndianTruncate(decoded[4], 33) : new byte[33];
                PreviousBlockHash = decoded[5] != null ? ByteManipulator.BigEndianTruncate(decoded[5], 32) : new byte[32];

                var decodedTransactions = RLP.Decode(decoded[6]);

                Transactions = new List<Transaction>();

                if (decodedTransactions != null)
                {
                    foreach (var rlpTransaction in decodedTransactions)
                    {
                        var tx = new Transaction(rlpTransaction);
                        Transactions.Add(tx);
                    }
                }
            }
            else
            {
                throw new Exception("Invalid block");
            }
        }

        public byte[] Serialize()
        {
            var transactionByteList = new List<byte[]>();

            foreach (var transaction in Transactions)
            {
                transactionByteList.Add(transaction.Serialize());
            }

            var serializedTransactions = RLP.Encode(transactionByteList);

            var block = new byte[][]
            {
                ByteManipulator.GetBytes((uint)Height),
                ByteManipulator.GetBytes(Timestamp),
                Difficulty.GetBytes(),
                Nonce,
                MinerAddress,
                PreviousBlockHash,
                serializedTransactions
            };

            return RLP.Encode(block);
        }

        public bool SingleVerify()
        {
            var ret = new LargeInteger(GetHash()) < Difficulty;

            if (ret)
            {
                foreach (var transaction in Transactions)
                {
                    if (!transaction.Verify())
                    {
                        ret = false;
                        break;
                    }
                }
            }

            return ret;
        }

        public bool Verify(Block previousBlock)
        {
            var ret = false;

            var single = SingleVerify();
            var hash = ArrayManipulator.Compare(previousBlock.GetHash(), PreviousBlockHash);
            var height = (Height - previousBlock.Height) == 1;
            var timestamp = Timestamp >= previousBlock.Timestamp;

            ret = hash && height && single && timestamp;

            return ret;
        }

        public byte[] GetHash()
        {
            return CryptographyHelper.Sha3256(Serialize());
        }

        public static readonly Block Genesis =
            new Block()
            {
                Height = 0,
                Difficulty = LowestDifficulty,
                MinerAddress = new byte[33],
                Timestamp = 1552489000,
                PreviousBlockHash = new byte[32],
                Nonce = new byte[] { 1 },
                Transactions = new List<Transaction>()
            };
    }
}
