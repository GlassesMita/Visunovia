using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Visunovia.Editor.Security;

public static class SimpleCryptoHelper
{
    private const string Salt = "Visunovia_LoR_2024_Simple";
    private const int Iterations = 100000;
    private const int KeyByteLength = 32;
    private const int IVByteLength = 16;
    private const byte XorKey = 0x4E;

    public static string ObfuscateKeyString(string rawKeyString)
    {
        byte[] base64Bytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(rawKeyString)));
        byte[] obfuscated = new byte[base64Bytes.Length];
        for (int i = 0; i < base64Bytes.Length; i++)
        {
            obfuscated[i] = (byte)(base64Bytes[i] ^ XorKey);
        }
        return Convert.ToBase64String(obfuscated);
    }

    public static byte[] DeriveAesKey(string rawKeyString)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(rawKeyString);
        byte[] saltBytes = Encoding.UTF8.GetBytes(Salt);
        using (var pbkdf2 = new Rfc2898DeriveBytes(keyBytes, saltBytes, Iterations, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(KeyByteLength);
        }
    }

    public static byte[] GenerateRandomIV()
    {
        var iv = new byte[IVByteLength];
        RandomNumberGenerator.Fill(iv);
        return iv;
    }

    public static void EncryptPackage(string sourceZipPath, string outputLorpkgPath, string rawKeyString)
    {
        byte[] key = DeriveAesKey(rawKeyString);
        byte[] iv = GenerateRandomIV();

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var outputFs = File.Create(outputLorpkgPath))
            {
                outputFs.Write(iv, 0, iv.Length);
                using (var encryptor = aes.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(outputFs, encryptor, CryptoStreamMode.Write))
                using (var sourceFs = File.OpenRead(sourceZipPath))
                {
                    sourceFs.CopyTo(cryptoStream);
                }
            }
        }
    }

    public static byte[] EncryptToBytes(byte[] data, string rawKeyString)
    {
        byte[] key = DeriveAesKey(rawKeyString);
        byte[] iv = GenerateRandomIV();

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length);
                using (var encryptor = aes.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }
    }

    public static byte[] DecryptFromBytes(byte[] encryptedData, string rawKeyString)
    {
        if (encryptedData.Length < IVByteLength)
        {
            throw new ArgumentException("Encrypted data is too short");
        }

        byte[] key = DeriveAesKey(rawKeyString);
        byte[] iv = new byte[IVByteLength];
        Array.Copy(encryptedData, 0, iv, 0, IVByteLength);
        byte[] encrypted = new byte[encryptedData.Length - IVByteLength];
        Array.Copy(encryptedData, IVByteLength, encrypted, 0, encrypted.Length);

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var decryptor = aes.CreateDecryptor())
            using (var ms = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(encrypted, 0, encrypted.Length);
                }
                return ms.ToArray();
            }
        }
    }
}
