using System;
using System.IO;
using System.Text;

namespace Visunovia.Editor.Security;

public static class SimpleCryptoHelper
{
    public static string ObfuscateKeyString(string rawKeyString)
    {
        return rawKeyString;
    }

    public static byte[] XorEncrypt(byte[] data, string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ passwordBytes[i % passwordBytes.Length]);
        }
        return result;
    }

    public static byte[] XorDecrypt(byte[] encryptedData, string password)
    {
        return XorEncrypt(encryptedData, password);
    }

    public static void EncryptPackage(string sourceZipPath, string outputLorpkgPath, string rawKeyString)
    {
        byte[] zipData = File.ReadAllBytes(sourceZipPath);
        byte[] encryptedData = XorEncrypt(zipData, rawKeyString);
        File.WriteAllBytes(outputLorpkgPath, encryptedData);
    }

    public static byte[] EncryptToBytes(byte[] data, string rawKeyString)
    {
        return XorEncrypt(data, rawKeyString);
    }

    public static byte[] DecryptFromBytes(byte[] encryptedData, string rawKeyString)
    {
        return XorDecrypt(encryptedData, rawKeyString);
    }
}
