using System.Collections.Generic;
using System.IO;

namespace Visunovia.Engine.Core;

public class LocalFileSystem : IVirtualFileSystem
{
    private readonly string _rootPath;

    public LocalFileSystem(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
    }

    public string ReadTextFile(string path)
    {
        var fullPath = GetFullPath(path);
        return File.ReadAllText(fullPath);
    }

    public bool FileExists(string path)
    {
        var fullPath = GetFullPath(path);
        return File.Exists(fullPath);
    }

    public string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;
        return Path.Combine(_rootPath, relativePath);
    }

    public Stream? OpenRead(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
            return File.OpenRead(fullPath);
        return null;
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var fullPath = GetFullPath(path);
        if (Directory.Exists(fullPath))
            return Directory.EnumerateFiles(fullPath, searchPattern, searchOption);
        return Enumerable.Empty<string>();
    }
}
