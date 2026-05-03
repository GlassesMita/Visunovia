using System.Collections.Generic;
using System.IO;

namespace Visunovia.Engine.Core;

public interface IVirtualFileSystem
{
    string ReadTextFile(string path);
    bool FileExists(string path);
    string GetFullPath(string relativePath);
    Stream? OpenRead(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
}
