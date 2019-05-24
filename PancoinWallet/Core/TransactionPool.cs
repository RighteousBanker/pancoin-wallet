using ByteOperation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Panode.Core
{
    public class TransactionPool
    {
        readonly BalanceLedger _balanceLedger;

        Dictionary<string, Transaction> transactionByHash = new Dictionary<string, Transaction>();
        Dictionary<string, DateTime> dateByHash = new Dictionary<string, DateTime>();

        public int Count { get { return transactionByHash.Count; } }

        public TransactionPool(BalanceLedger balanceLedger)
        {
            _balanceLedger = balanceLedger;
        }

        public List<Transaction> GetUnknown(List<string> txHashes)
        {
            Clean();
            var ret = new List<Transaction>();

            foreach (var kvp in transactionByHash)
            {
                if (!txHashes.Contains(kvp.Key))
                {
                    ret.Add(kvp.Value);
                }
            }

            return ret;
        }

        public List<Transaction> GetTransactions(int count)
        {
            Clean();
            var ret = new List<Transaction>();

            count = count > transactionByHash.Count ? transactionByHash.Count : count;

            for (int i = 0; i < count; i++)
            {
                ret.Add(transactionByHash.Values.ToArray()[i]);
            }

            return ret;
        }

        public List<Transaction> GetMineableTransactions()
        {
            var allTransactions = GetTransactions(Count);

            var txBySource = new Dictionary<string, List<Transaction>>();

            foreach (var tx in allTransactions)
            {
                var senderHex = HexConverter.ToPrefixString(tx.Source);

                if (txBySource.ContainsKey(senderHex))
                {
                    txBySource[senderHex].Add(tx);
                }
                else
                {
                    txBySource.Add(senderHex, new List<Transaction>() { tx });
                }
            }

            var ret = new List<Transaction>();

            foreach (var kvp in txBySource)
            {
                uint nextNonce;

                lock (GateKeeper.BalanceLedgerLock)
                {
                    nextNonce = _balanceLedger.GetTransactionCount(kvp.Value[0].Source);
                }

                var sortedByNonce = kvp.Value.OrderBy(x => x.Nonce).ToList();

                for (int i = 0; i < sortedByNonce.Count; i++)
                {
                    if (sortedByNonce[i].Nonce == nextNonce)
                    {
                        ret.Add(sortedByNonce[i]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return ret;
        }

        public void AddTransactions(List<Transaction> addedTransactions)
        {
            foreach (var transaction in addedTransactions)
            {
                var hash = HexConverter.ToPrefixString(transaction.Hash());

                if (!transactionByHash.ContainsKey(hash))
                {
                    if (VerifyTransaction(transaction))
                    {
                        transactionByHash.Add(hash, transaction);
                        dateByHash.Add(hash, DateTime.UtcNow);
                    }
                }
            }
        }

        public void Clean()
        {
            foreach (var kvp in transactionByHash)
            {
                if (!VerifyTransaction(kvp.Value))
                {
                    transactionByHash.Remove(kvp.Key);
                    dateByHash.Remove(kvp.Key);
                }
            }
            foreach (var kvp in dateByHash)
            {
                if (kvp.Value < DateTime.UtcNow.AddHours(-1))
                {
                    transactionByHash.Remove(kvp.Key);
                    dateByHash.Remove(kvp.Key);
                }
            }
        }

        private bool VerifyTransaction(Transaction transaction)
        {
            var ret = false;

            if (transaction.Verify())
            {
                var nonce = _balanceLedger.GetTransactionCount(transaction.Source);

                if (transaction.Nonce >= nonce)
                {
                    var sourceBalance = _balanceLedger.GetBalance(transaction.Source);

                    if (sourceBalance != null)
                    {
                        if (sourceBalance + 1 > (transaction.Ammount + transaction.Fee))
                        {
                            ret = true;
                        }
                    }
                }
            }

            return ret;
        }
    }
}
