using PancoinWallet.Utilities;
using System;

namespace PancoinWallet.Core
{
    public class Wallet
    {
        public byte[] PublicKey { get; set; }
        public byte[] PrivateKey { get; set; }

        public Wallet()
        {
            byte[] privateKey = CryptographyHelper.GeneratePrivateKeySecp256k1();

            GenerateWallet(privateKey);
        }

        public Wallet(byte[] privateKey)
        {
            if (privateKey.Length == 32)
            {
                GenerateWallet(privateKey);
            }
            else
            {
                throw new Exception("Private key length is not 32 bytes");
            }
        }

        private void GenerateWallet(byte[] privateKey)
        {
            PrivateKey = privateKey;

            PublicKey = CryptographyHelper.GeneratePublicKeySecp256k1(privateKey);
        }

        public string FileSerialize()
        {
            return HexConverter.ToPrefixString(PublicKey) + Environment.NewLine + HexConverter.ToPrefixString(PrivateKey);
        }

        public static Wallet FileDeserialize(string walletFile)
        {
            var lines = walletFile.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            if (lines.Length == 2)
            {
                return new Wallet(HexConverter.ToBytes(lines[1]));
            }
            else
            {
                return null;
            }
        }
    }
}
