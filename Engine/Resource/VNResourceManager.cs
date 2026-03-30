using System.Collections.Generic;
using System.IO;

namespace Visunovia.Engine.Core;

public class VNResourceManager
{
    private string _projectPath = string.Empty;
    private readonly Dictionary<string, string> _imageCache = new();
    private readonly Dictionary<string, string> _audioCache = new();
    private IVirtualFileSystem? _fileSystem;

    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            _projectPath = value;
            _imageCache.Clear();
            _audioCache.Clear();
            _fileSystem = new LocalFileSystem(value);
        }
    }

    public IVirtualFileSystem FileSystem
    {
        get => _fileSystem ??= new LocalFileSystem(_projectPath);
        set => _fileSystem = value;
    }

    public string GetImagePath(string imageName)
    {
        if (string.IsNullOrEmpty(imageName))
            return string.Empty;

        if (_imageCache.TryGetValue(imageName, out var cachedPath))
            return cachedPath;

        var possiblePaths = new[]
        {
            Path.Combine(_projectPath, "assets", imageName),
            Path.Combine(_projectPath, "images", $"{imageName}.png"),
            Path.Combine(_projectPath, "images", $"{imageName}.jpg"),
            Path.Combine(_projectPath, "bg", $"{imageName}.png"),
            Path.Combine(_projectPath, "bg", $"{imageName}.jpg"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _imageCache[imageName] = path;
                return path;
            }
        }

        return imageName;
    }

    public string GetAudioPath(string audioName)
    {
        if (string.IsNullOrEmpty(audioName))
            return string.Empty;

        if (_audioCache.TryGetValue(audioName, out var cachedPath))
            return cachedPath;

        var possiblePaths = new[]
        {
            Path.Combine(_projectPath, "assets", audioName),
            Path.Combine(_projectPath, "audio", $"{audioName}.mp3"),
            Path.Combine(_projectPath, "audio", $"{audioName}.wav"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _audioCache[audioName] = path;
                return path;
            }
        }

        return audioName;
    }

    public void LoadProject(string projectPath)
    {
        _projectPath = projectPath;
        _imageCache.Clear();
        _audioCache.Clear();
        _fileSystem = new LocalFileSystem(projectPath);
    }

    public string LoadScript(string sceneName)
    {
        if (sceneName == "start")
        {
            return "Visunovia: 欢迎使用 Visunovia 视觉小说引擎！";
        }
        return string.Empty;
    }
}
