using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows.Media.Imaging;

namespace Visunovia.Player.WPF.Player;

public class ResourceLoader
{
    private readonly Dictionary<string, object> _resourceCache = new();
    private ZipArchive? _zipArchive;

    public void LoadFromArchive(ZipArchive archive)
    {
        _zipArchive = archive;
        _resourceCache.Clear();
    }

    public string? GetText(string virtualPath)
    {
        if (_zipArchive == null) return null;

        var entry = _zipArchive.GetEntry(virtualPath);
        if (entry == null) return null;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public BitmapImage? GetImage(string virtualPath)
    {
        if (_zipArchive == null) return null;

        if (_resourceCache.TryGetValue(virtualPath, out var cached) && cached is BitmapImage bitmap)
        {
            return bitmap;
        }

        var entry = _zipArchive.GetEntry(virtualPath);
        if (entry == null) return null;

        using var stream = entry.Open();
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        _resourceCache[virtualPath] = bitmapImage;
        return bitmapImage;
    }

    public byte[]? GetBinary(string virtualPath)
    {
        if (_zipArchive == null) return null;

        var entry = _zipArchive.GetEntry(virtualPath);
        if (entry == null) return null;

        using var stream = entry.Open();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public List<string> ListFiles(string directory)
    {
        var result = new List<string>();
        if (_zipArchive == null) return result;

        var normalizedDir = directory.Replace("\\", "/");
        if (!normalizedDir.EndsWith("/"))
            normalizedDir += "/";

        foreach (var entry in _zipArchive.Entries)
        {
            if (entry.FullName.StartsWith(normalizedDir) && !entry.FullName.EndsWith("/"))
            {
                var relativePath = entry.FullName.Substring(normalizedDir.Length);
                var firstSlash = relativePath.IndexOf('/');
                if (firstSlash < 0 || firstSlash == relativePath.Length - 1)
                {
                    result.Add(relativePath);
                }
            }
        }

        return result;
    }

    public bool FileExists(string virtualPath)
    {
        if (_zipArchive == null) return false;
        return _zipArchive.GetEntry(virtualPath) != null;
    }

    public void ClearCache()
    {
        _resourceCache.Clear();
    }
}
