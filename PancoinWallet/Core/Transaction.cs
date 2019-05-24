using Encoders;
using PancoinWallet.Utilities;

namespace PancoinWallet.Core
{
    public class Transaction
    {
        public static byte[] MainNetwork = new byte[] { 1 };

        public uint Nonce { get; set; } //first nonce is 0
        public LargeInteger Ammount { get; set; } //integer ammount in smallest units
        public LargeInteger Fee { get; set; }
        public byte[] Source { get; set; } //ecdsa public key of sender
        public byte[] Destination { get; set; } //ecdsa public key of recipient
        public byte[] Signature { get; set; } //XY, not DER
        public byte[] Network { get; set; }

        public Transaction() { }

        public Transaction(string hexRLP)
        {
            Deserialize(HexConverter.ToBytes(hexRLP));
        }

        public Transaction(byte[] hexRLP)
        {
            Deserialize(hexRLP);
        }

        private void Deserialize(byte[] rlp)
        {
            var data = RLP.Decode(rlp);

            Nonce = ByteManipulator.GetUInt32(data[0] ?? new byte[] { 0 });
            Ammount = new LargeInteger(data[1] ?? new byte[] { 0 });
            Fee = new LargeInteger(data[2] ?? new byte[] { 0 });
            Source = ByteManipulator.BigEndianTruncate(data[3], 33) ?? new byte[33];
            Destination = ByteManipulator.BigEndianTruncate(data[4], 33) ?? new byte[33];
            Signature = ByteManipulator.BigEndianTruncate(data[5], 64) ?? new byte[64];
            Network = data[6] ?? new byte[] { 0 };
        }

        public byte[] Serialize()
        {
            var tx = new byte[][]
            {
                ByteManipulator.GetBytes(Nonce),
                Ammount.GetBytes(),
                Fee.GetBytes(),
                Source,
                Destination,
                Signature,
                Network
            };

            return RLP.Encode(tx);
        }

        public void Sign(byte[] privateKey)
        {
            Signature = CryptographyHelper.GenerateSignatureSecp256k1(privateKey, SigningData());
        }

        public byte[] SigningData()
        {
            var hashTransaction = new Transaction()
            {
                Nonce = Nonce,
                Ammount = Ammount,
                Fee = Fee,
                Source = Source,
                Destination = Destination,
                Signature = null,
                Network = Network
            };

            return hashTransaction.Serialize();
        }

        public byte[] Hash()
        {
            return CryptographyHelper.Sha3256(Serialize());
        }

        public bool Verify()
        {
            return ArrayManipulator.Compare(MainNetwork, Network) && CryptographyHelper.Secp256k1Verify(Source, Signature, SigningData());
        }

        public override bool Equals(object obj)
        {
            var transaction = obj as Transaction;
            return ArrayManipulator.Compare(Serialize(), transaction.Serialize());
        }

        public static bool operator ==(Transaction a, Transaction b)
        {
            return ArrayManipulator.Compare(a.Serialize(), b.Serialize());
        }

        public static bool operator !=(Transaction a, Transaction b)
        {
            return !ArrayManipulator.Compare(a.Serialize(), b.Serialize());
        }
    }
}
 