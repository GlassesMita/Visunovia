using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using Visunovia.Engine.Core;

namespace Visunovia.Engine.Localization;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private string _currentLocale = "zh-CN";
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService()
    {
        _resourceManager = new ResourceManager("Visunovia.Resources.Strings.Strings", typeof(LocalizationService).Assembly);
        _currentCulture = CultureInfo.GetCultureInfo(_currentLocale);
        LoadSettings();
    }

    public string CurrentLocale
    {
        get => _currentLocale;
        set
        {
            if (_currentLocale != value)
            {
                _currentLocale = value;
                _currentCulture = CultureInfo.GetCultureInfo(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentCulture));
            }
        }
    }

    public CultureInfo CurrentCulture => _currentCulture;

    public CultureInfo[] AvailableCultures { get; } = new[]
    {
        CultureInfo.GetCultureInfo("zh-CN"),
        CultureInfo.GetCultureInfo("en-US")
    };

    public void LoadLocale(string locale)
    {
        CurrentLocale = locale;
        OnPropertyChanged();
    }

    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key;
        }
        catch
        {
            return key;
        }
    }

    public string GetEventTypeDisplayName(VNEventType eventType)
    {
        return GetString($"EventTypes_{eventType}");
    }

    public string GetDialogueTypeDisplayName(VNDialogueType dialogueType)
    {
        return GetString($"DialogueTypes_{dialogueType}");
    }

    public string[] GetLocalizedEventTypes()
    {
        var types = Enum.GetNames<VNEventType>();
        return types.Select(t => GetString($"EventTypes_{t}")).ToArray();
    }

    public VNEventType ParseEventType(string localizedName)
    {
        var types = Enum.GetNames<VNEventType>();
        foreach (var type in types)
        {
            var localized = GetString($"EventTypes_{type}");
            if (localized == localizedName)
            {
                return Enum.Parse<VNEventType>(type);
            }
        }
        return VNEventType.Custom;
    }

    public void SaveSettings()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine.tlorcfg");
        var ini = IniFileHelper.LoadOrCreate(settingsPath);
        ini.SetValue("General", "Language", _currentLocale);
        ini.Save();
    }

    public void LoadSettings()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine.tlorcfg");
        var language = "zh-CN";

        if (File.Exists(settingsPath))
        {
            var ini = new IniFileHelper(settingsPath);
            var configuredLanguage = ini.GetValue("General", "Language", null);
            if (!string.IsNullOrEmpty(configuredLanguage))
            {
                language = configuredLanguage;
            }
        }

        _currentLocale = language;
        _currentCulture = CultureInfo.GetCultureInfo(language);
        OnPropertyChanged(nameof(CurrentLocale));
    }

    public string GetLanguageDisplayName(string code)
    {
        return code switch
        {
            "zh-CN" => GetString("Language_Name"),
            "en-US" => GetString("Language_Name"),
            _ => code
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
