using System.ComponentModel;
using System.Runtime.CompilerServices;
using Visunovia.Engine.Core;

namespace Visunovia.Engine.Localization;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private string _currentLocale = "zh-CN";

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["zh-CN"] = new Dictionary<string, string>
        {
            ["VNEventType.JumpScene"] = "JumpScene - 跳转场景",
            ["VNEventType.SetVariable"] = "SetVariable - 设置变量",
            ["VNEventType.PlaySound"] = "PlaySound - 播放音效",
            ["VNEventType.ChangeBackground"] = "ChangeBackground - 更改背景",
            ["VNEventType.ShowCharacter"] = "ShowCharacter - 显示角色",
            ["VNEventType.HideCharacter"] = "HideCharacter - 隐藏角色",
            ["VNEventType.Pause"] = "Pause - 暂停",
            ["VNEventType.Custom"] = "Custom - 自定义",

            ["VNDialogueType.Dialogue"] = "Dialogue - 对话",
            ["VNDialogueType.Branch"] = "Branch - 分支",
            ["VNDialogueType.Event"] = "Event - 事件"
        },
        ["en-US"] = new Dictionary<string, string>
        {
            ["VNEventType.JumpScene"] = "JumpScene",
            ["VNEventType.SetVariable"] = "SetVariable",
            ["VNEventType.PlaySound"] = "PlaySound",
            ["VNEventType.ChangeBackground"] = "ChangeBackground",
            ["VNEventType.ShowCharacter"] = "ShowCharacter",
            ["VNEventType.HideCharacter"] = "HideCharacter",
            ["VNEventType.Pause"] = "Pause",
            ["VNEventType.Custom"] = "Custom",

            ["VNDialogueType.Dialogue"] = "Dialogue",
            ["VNDialogueType.Branch"] = "Branch",
            ["VNDialogueType.Event"] = "Event"
        }
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLocale
    {
        get => _currentLocale;
        set
        {
            if (_currentLocale != value && _translations.ContainsKey(value))
            {
                _currentLocale = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableLocales));
            }
        }
    }

    public string[] AvailableLocales => _translations.Keys.ToArray();

    public string GetString(string key)
    {
        if (_translations.TryGetValue(_currentLocale, out var localeDict))
        {
            if (localeDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return key;
    }

    public string GetEventTypeDisplayName(VNEventType eventType)
    {
        return GetString($"VNEventType.{eventType}");
    }

    public string GetDialogueTypeDisplayName(VNDialogueType dialogueType)
    {
        return GetString($"VNDialogueType.{dialogueType}");
    }

    public string[] GetLocalizedEventTypes()
    {
        var types = Enum.GetNames<VNEventType>();
        return types.Select(t => GetString($"VNEventType.{t}")).ToArray();
    }

    public VNEventType ParseEventType(string localizedName)
    {
        var types = Enum.GetNames<VNEventType>();
        foreach (var type in types)
        {
            var localized = GetString($"VNEventType.{type}");
            if (localized == localizedName)
            {
                return Enum.Parse<VNEventType>(type);
            }
        }
        return VNEventType.Custom;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
