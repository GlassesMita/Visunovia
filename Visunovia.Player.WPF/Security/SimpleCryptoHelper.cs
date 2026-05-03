using System.IO;
using System.IO.Compression;
using System.Text;

namespace Visunovia.Player.WPF.Security;

public static class SimpleCryptoHelper
{
    public static byte[] XorDecrypt(byte[] encryptedData, string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] result = new byte[encryptedData.Length];
        for (int i = 0; i < encryptedData.Length; i++)
        {
            result[i] = (byte)(encryptedData[i] ^ passwordBytes[i % passwordBytes.Length]);
        }
        return result;
    }

    public static byte[] DecryptFile(string filePath, string password)
    {
        byte[] encryptedData = File.ReadAllBytes(filePath);
        return XorDecrypt(encryptedData, password);
    }

    public static MemoryStream DecryptToMemoryStream(string filePath, string password)
    {
        byte[] decryptedData = DecryptFile(filePath, password);
        return new MemoryStream(decryptedData);
    }

    public static ZipArchive? OpenEncryptedPackage(string filePath, string password)
    {
        try
        {
            var encryptedData = File.ReadAllBytes(filePath);
            var decryptedData = XorDecrypt(encryptedData, password);
            var memoryStream = new MemoryStream(decryptedData);
            return new ZipArchive(memoryStream, ZipArchiveMode.Read, false);
        }
        catch
        {
            return null;
        }
    }

    public static ZipArchive? OpenZipPackage(string filePath)
    {
        try
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new ZipArchive(fileStream, ZipArchiveMode.Read, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open ZIP package: {ex.Message}");
            return null;
        }
    }
}
