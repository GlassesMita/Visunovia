using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.IO;

namespace Visunovia.Engine.Data;

public class DataManager : INotifyPropertyChanged
{
    private static DataManager? _instance;
    public static DataManager Instance => _instance ??= new DataManager();

    private string? _currentProjectPath;

    public ObservableCollection<CharacterData> Characters { get; } = new();
    public ObservableCollection<VariableData> Variables { get; } = new();
    public ObservableCollection<SceneData> Scenes { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? DataChanged;

    private DataManager()
    {
    }

    public void SetProjectPath(string? projectPath)
    {
        _currentProjectPath = projectPath;
        if (!string.IsNullOrEmpty(projectPath))
        {
            LoadAllData(projectPath);
        }
        else
        {
            ClearAllData();
        }
    }

    public void LoadAllData(string projectPath)
    {
        var dataFolder = Path.Combine(Path.GetDirectoryName(projectPath) ?? "", "Data");
        Directory.CreateDirectory(dataFolder);

        LoadCharacters(Path.Combine(dataFolder, "Characters"));
        LoadVariables(Path.Combine(dataFolder, "Variables"));
        LoadScenes(Path.Combine(dataFolder, "Scenes"));
    }

    public void SaveAllData()
    {
        if (string.IsNullOrEmpty(_currentProjectPath))
            return;

        var dataFolder = Path.Combine(Path.GetDirectoryName(_currentProjectPath) ?? "", "Data");
        Directory.CreateDirectory(dataFolder);

        SaveCharacters(Path.Combine(dataFolder, "Characters"));
        SaveVariables(Path.Combine(dataFolder, "Variables"));
        SaveScenes(Path.Combine(dataFolder, "Scenes"));
    }

    private void LoadCharacters(string folder)
    {
        Characters.Clear();
        Directory.CreateDirectory(folder);

        var charFiles = Directory.GetFiles(folder, "*.char");
        foreach (var file in charFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var charData = JsonSerializer.Deserialize<CharacterData>(json);
                if (charData != null)
                {
                    Characters.Add(charData);
                }
            }
            catch
            {
            }
        }

        if (Characters.Count == 0)
        {
            CreateDefaultCharacters();
        }
    }

    private void CreateDefaultCharacters()
    {
        AddCharacter("角色 1", "#FF6B6B");
        AddCharacter("角色 2", "#4ECDC4");
        AddCharacter("角色 3", "#45B7D1");
    }

    private void SaveCharacters(string folder)
    {
        Directory.CreateDirectory(folder);

        var existingFiles = Directory.GetFiles(folder, "*.char");
        foreach (var file in existingFiles)
        {
            File.Delete(file);
        }

        foreach (var charData in Characters)
        {
            var file = Path.Combine(folder, $"{charData.Guid}.char");
            var json = JsonSerializer.Serialize(charData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
    }

    private void LoadVariables(string folder)
    {
        Variables.Clear();
        Directory.CreateDirectory(folder);

        var varFiles = Directory.GetFiles(folder, "*.var");
        foreach (var file in varFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var varData = JsonSerializer.Deserialize<VariableData>(json);
                if (varData != null)
                {
                    Variables.Add(varData);
                }
            }
            catch
            {
            }
        }
    }

    private void SaveVariables(string folder)
    {
        Directory.CreateDirectory(folder);

        var existingFiles = Directory.GetFiles(folder, "*.var");
        foreach (var file in existingFiles)
        {
            File.Delete(file);
        }

        foreach (var varData in Variables)
        {
            var file = Path.Combine(folder, $"{varData.Guid}.var");
            var json = JsonSerializer.Serialize(varData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
    }

    private void LoadScenes(string folder)
    {
        Scenes.Clear();
        Directory.CreateDirectory(folder);

        var sceneFiles = Directory.GetFiles(folder, "*.scene");
        foreach (var file in sceneFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var sceneData = JsonSerializer.Deserialize<SceneData>(json);
                if (sceneData != null)
                {
                    Scenes.Add(sceneData);
                }
            }
            catch
            {
            }
        }
    }

    private void SaveScenes(string folder)
    {
        Directory.CreateDirectory(folder);

        var existingFiles = Directory.GetFiles(folder, "*.scene");
        foreach (var file in existingFiles)
        {
            File.Delete(file);
        }

        foreach (var sceneData in Scenes)
        {
            var file = Path.Combine(folder, $"{sceneData.Guid}.scene");
            var json = JsonSerializer.Serialize(sceneData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
    }

    private void ClearAllData()
    {
        Characters.Clear();
        Variables.Clear();
        Scenes.Clear();
    }

    public CharacterData AddCharacter(string name, string color = "#FFFFFF")
    {
        var charData = new CharacterData
        {
            Guid = Guid.NewGuid().ToString(),
            Name = name,
            DisplayName = name,
            Color = color
        };
        Characters.Add(charData);
        DataChanged?.Invoke();
        return charData;
    }

    public bool RemoveCharacter(string guid)
    {
        var charData = Characters.FirstOrDefault(c => c.Guid == guid);
        if (charData != null)
        {
            Characters.Remove(charData);
            DataChanged?.Invoke();
            return true;
        }
        return false;
    }

    public VariableData AddVariable(string name, object defaultValue = null!)
    {
        var varData = new VariableData
        {
            Guid = Guid.NewGuid().ToString(),
            Name = name,
            DefaultValue = defaultValue,
            CurrentValue = defaultValue
        };
        Variables.Add(varData);
        DataChanged?.Invoke();
        return varData;
    }

    public bool RemoveVariable(string guid)
    {
        var varData = Variables.FirstOrDefault(v => v.Guid == guid);
        if (varData != null)
        {
            Variables.Remove(varData);
            DataChanged?.Invoke();
            return true;
        }
        return false;
    }

    public SceneData AddScene(string name)
    {
        var sceneData = new SceneData
        {
            Guid = Guid.NewGuid().ToString(),
            Name = name
        };
        Scenes.Add(sceneData);
        DataChanged?.Invoke();
        return sceneData;
    }

    public bool RemoveScene(string guid)
    {
        var sceneData = Scenes.FirstOrDefault(s => s.Guid == guid);
        if (sceneData != null)
        {
            Scenes.Remove(sceneData);
            DataChanged?.Invoke();
            return true;
        }
        return false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class CharacterData
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public List<ExpressionData> Expressions { get; set; } = new();
}

public class ExpressionData
{
    public string Name { get; set; } = "default";
    public string ImagePath { get; set; } = string.Empty;
}

public class VariableData
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public object DefaultValue { get; set; } = null!;
    public object CurrentValue { get; set; } = null!;
    public string Type { get; set; } = "string";
}

public class SceneData
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Bgm { get; set; } = string.Empty;
}
