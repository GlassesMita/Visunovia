using System.Xml;
using Visunovia.Services.Configuration;

namespace Visunovia.Services;

/// <summary>
/// 配置管理服务，提供对应用程序 XML 配置文件的统一访问接口。
/// 封装配置的读取、写入、保存和重置操作，支持类型安全的数据访问。
/// 通过依赖注入以单例方式注册，确保整个应用程序共享同一份配置状态。
///
/// 配置文件格式（语义化 XML）：
/// <code>
/// &lt;configuration&gt;
///   &lt;appSettings&gt;
///     &lt;Language&gt;zh-CN&lt;/Language&gt;
///     &lt;UI theme="dark" /&gt;
///     &lt;FontSize&gt;14&lt;/FontSize&gt;
///   &lt;/appSettings&gt;
/// &lt;/configuration&gt;
/// </code>
/// </summary>
public class SettingsService : IDisposable
{
    #region 事件定义

    /// <summary>
    /// 配置变更事件。当任何配置项被修改并保存后触发。
    /// 订阅者可通过此事件实时响应配置变化（如刷新 UI、重新加载资源等）。
    /// </summary>
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    #endregion

    #region 私有字段

    private AppSettingsSection _section;
    private readonly string _configFilePath;
    private readonly object _syncRoot = new();
    private bool _isDirty;
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化 SettingsService 实例。
    /// 自动定位配置文件路径，加载已有配置或创建默认配置。
    /// </summary>
    public SettingsService()
    {
        var baseDir = AppContext.BaseDirectory;
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        if (string.IsNullOrEmpty(assemblyName))
        {
            assemblyName = "Visunovia";
        }

        _configFilePath = Path.Combine(baseDir, $"{assemblyName}.exe.config");
        _section = LoadOrCreateConfig();

        // Ensure Placeholder defaults exist in config (migration for existing installs)
        EnsurePlaceholderDefaults();
    }

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取当前配置文件完整路径（只读）。
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    /// <summary>
    /// 获取当前已加载的 AppSettingsSection 配置节实例（只读）。
    /// </summary>
    public AppSettingsSection Section => _section;

    /// <summary>
    /// 获取当前语言设置。
    /// </summary>
    public string? Language => _section[DefaultSettings.LanguageKey];

    /// <summary>
    /// 获取当前主题设置。
    /// </summary>
    public string? Theme => _section[DefaultSettings.ThemeKey];

    /// <summary>
    /// 指示自上次保存以来配置是否已被修改。
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// 获取或设置自动保存模式。当启用时，每次 Set&lt;T&gt;() 调用都会立即持久化到配置文件。
    /// </summary>
    public bool AutoSave { get; set; }

    #endregion

    #region 类型安全的配置读取 (Get<T>)

    /// <summary>
    /// 以类型安全的方式获取指定键的配置值。
    /// </summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var rawValue = _section[key];
        return ConvertValue<T>(rawValue, defaultValue);
    }

    /// <summary>
    /// 获取指定键的原始字符串值。
    /// </summary>
    public string? GetRawValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _section[key];
    }

    #endregion

    #region 类型安全的配置写入 (Set<T>)

    /// <summary>
    /// 以类型安全的方式设置指定键的配置值。
    /// </summary>
    public void Set<T>(string key, T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var stringValue = value?.ToString() ?? string.Empty;
        var oldValue = _section[key];

        lock (_syncRoot)
        {
            _section[key] = stringValue;
            _isDirty = true;

            if (AutoSave)
            {
                try
                {
                    WriteConfigFile();
                    _isDirty = false;

                    OnConfigurationChanged(new ConfigurationChangedEventArgs(
                        isSave: true, isReset: false,
                        changedKey: key, oldValue: oldValue,
                        newValue: value, isAutoSave: true));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SettingsService] 自动保存失败（已保留在内存中）: {ex.Message}");

                    OnConfigurationChanged(new ConfigurationChangedEventArgs(
                        isSave: false, isReset: false,
                        changedKey: key, oldValue: oldValue,
                        newValue: value, isAutoSave: true));
                }
            }
        }
    }

    /// <summary>
    /// 以类型安全的方式设置配置值并立即保存到文件（原子操作）。
    /// </summary>
    public bool SetAndSave<T>(string key, T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var stringValue = value?.ToString() ?? string.Empty;
        var oldValue = _section[key];

        lock (_syncRoot)
        {
            _section[key] = stringValue;
            _isDirty = true;

            try
            {
                WriteConfigFile();
                _isDirty = false;

                OnConfigurationChanged(new ConfigurationChangedEventArgs(
                    isSave: true, isReset: false,
                    changedKey: key, oldValue: oldValue,
                    newValue: value, isAutoSave: false));

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SettingsService] SetAndSave 保存失败（值已保留在内存中）: {key} - {ex.Message}");

                OnConfigurationChanged(new ConfigurationChangedEventArgs(
                    isSave: false, isReset: false,
                    changedKey: key, oldValue: oldValue,
                    newValue: value, isAutoSave: false));

                return false;
            }
        }
    }

    #endregion

    #region 持久化操作 (Save / Reset)

    /// <summary>
    /// 将当前内存中的配置状态保存到 XML 配置文件。
    /// </summary>
    public void Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncRoot)
        {
            WriteConfigFile();
            _isDirty = false;
        }

        OnConfigurationChanged(new ConfigurationChangedEventArgs(isSave: true));
    }

    /// <summary>
    /// 将所有配置重置为 DefaultSettings 中定义的默认值。
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncRoot)
        {
            _section = CreateDefaultAppSettings();
            _isDirty = true;
        }

        OnConfigurationChanged(new ConfigurationChangedEventArgs(isReset: true));
    }

    /// <summary>
    /// 重新从磁盘加载配置文件，丢弃所有未保存的内存修改。
    /// </summary>
    public void Reload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncRoot)
        {
            _section = LoadOrCreateConfig();
            _isDirty = false;
        }
    }

    #endregion

    #region 快捷方法

    /// <summary>
    /// 快捷方法：获取当前语言设置，若不存在则返回默认值。
    /// </summary>
    public string GetCurrentLanguage()
    {
        return Get<string>(DefaultSettings.LanguageKey) ?? DefaultSettings.DefaultLanguage;
    }

    /// <summary>
    /// 快捷方法：设置当前语言。
    /// </summary>
    public void SetCurrentLanguage(string language)
    {
        Set(DefaultSettings.LanguageKey, language);
    }

    /// <summary>
    /// 快捷方法：获取当前主题设置。
    /// </summary>
    public string GetCurrentTheme()
    {
        return Get<string>(DefaultSettings.ThemeKey) ?? DefaultSettings.DefaultTheme;
    }

    /// <summary>
    /// 快捷方法：设置当前主题。
    /// </summary>
    public void SetCurrentTheme(string theme)
    {
        Set(DefaultSettings.ThemeKey, theme);
    }

    #endregion

    #region 私有方法 - 文件操作

    private AppSettingsSection LoadOrCreateConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var doc = new XmlDocument();
                doc.Load(_configFilePath);

                var appSettingsNode = doc.SelectSingleNode($"//{DefaultSettings.SectionName}");
                if (appSettingsNode is XmlElement elem)
                {
                    return AppSettingsSection.DeserializeFromXml(elem);
                }

                return CreateDefaultAppSettings();
            }

            var defaultSection = CreateDefaultAppSettings();
            EnsureConfigDirectory();
            WriteDefaultConfigFile(defaultSection);
            return defaultSection;
        }
        catch (XmlException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SettingsService] 配置文件 XML 解析失败: {_configFilePath} - {ex.Message}");
            return CreateDefaultAppSettings();
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SettingsService] 配置文件读取 I/O 错误: {_configFilePath} - {ex.Message}");
            return CreateDefaultAppSettings();
        }
    }

    private static AppSettingsSection CreateDefaultAppSettings()
    {
        var section = new AppSettingsSection();
        section[DefaultSettings.LanguageKey] = DefaultSettings.DefaultLanguage;
        section[DefaultSettings.ThemeKey] = DefaultSettings.DefaultTheme;
        section[DefaultSettings.EditorFontSizeKey] = DefaultSettings.DefaultEditorFontSize.ToString();
        section[DefaultSettings.AutoSaveIntervalKey] = DefaultSettings.DefaultAutoSaveInterval.ToString();
        section[DefaultSettings.RecentProjectsLimitKey] = DefaultSettings.DefaultRecentProjectsLimit.ToString();
        section[DefaultSettings.PreviewWidthKey] = DefaultSettings.DefaultPreviewWidth.ToString();
        section[DefaultSettings.PreviewHeightKey] = DefaultSettings.DefaultPreviewHeight.ToString();
        section[DefaultSettings.AllowRemoteSessionKey] = DefaultSettings.DefaultAllowRemoteSession.ToString();
        section[DefaultSettings.IsFirstRunKey] = DefaultSettings.DefaultIsFirstRun.ToString();
        section[DefaultSettings.PlaceholderCompanyNameKey] = DefaultSettings.DefaultPlaceholderCompanyName;
        section[DefaultSettings.PlaceholderProductNameKey] = DefaultSettings.DefaultPlaceholderProductName;
        return section;
    }

    /// <summary>
    /// 确保占位符配置项存在于当前配置中。
    /// 用于已有配置文件的迁移：如果配置中缺少占位符项，则写入默认值并保存。
    /// </summary>
    private void EnsurePlaceholderDefaults()
    {
        try
        {
            var companyValue = _section[DefaultSettings.PlaceholderCompanyNameKey];
            var productValue = _section[DefaultSettings.PlaceholderProductNameKey];

            if (string.IsNullOrEmpty(companyValue) || string.IsNullOrEmpty(productValue))
            {
                if (string.IsNullOrEmpty(companyValue))
                    _section[DefaultSettings.PlaceholderCompanyNameKey] = DefaultSettings.DefaultPlaceholderCompanyName;
                if (string.IsNullOrEmpty(productValue))
                    _section[DefaultSettings.PlaceholderProductNameKey] = DefaultSettings.DefaultPlaceholderProductName;

                WriteConfigFile();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SettingsService] Placeholder 默认值写入失败: {ex.Message}");
        }
    }

    private static void EnsureConfigDirectory()
    {
        var dir = Path.GetDirectoryName(AppContext.BaseDirectory);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void WriteConfigFile()
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = System.Text.Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using (var writer = XmlWriter.Create(_configFilePath, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("configuration");
            writer.WriteRaw(_section.SerializeToXml());
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private void WriteDefaultConfigFile(AppSettingsSection section)
    {
        try
        {
            var original = _section;
            _section = section;
            WriteConfigFile();
            _section = original;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SettingsService] 默认配置文件写入失败: {_configFilePath} - {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 辅助

    private static T? ConvertValue<T>(string? rawValue, T? defaultValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return defaultValue;
        }

        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (underlyingType == typeof(string))
                return (T)(object)rawValue!;

            if (underlyingType == typeof(int) && int.TryParse(rawValue, out int intVal))
                return (T)(object)intVal;

            if (underlyingType == typeof(bool) && bool.TryParse(rawValue, out bool boolVal))
                return (T)(object)boolVal;

            if (underlyingType == typeof(double) && double.TryParse(rawValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                return (T)(object)dblVal;

            if (underlyingType == typeof(float) && float.TryParse(rawValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out float fltVal))
                return (T)(object)fltVal;

            if (underlyingType == typeof(long) && long.TryParse(rawValue, out long longVal))
                return (T)(object)longVal;

            return defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    private void OnConfigurationChanged(ConfigurationChangedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, e);
    }

    #endregion

    #region IDisposable 实现

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isDirty)
            {
                try
                {
                    Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SettingsService] Dispose 时自动保存失败: {ex.Message}");
                }
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// 配置变更事件参数。
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public bool IsSave { get; }
    public bool IsReset { get; }
    public DateTime Timestamp { get; } = DateTime.Now;
    public string? ChangedKey { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public bool IsAutoSave { get; }

    public ConfigurationChangedEventArgs(bool isSave = false, bool isReset = false)
        : this(isSave, isReset, null, null, null, false)
    {
    }

    public ConfigurationChangedEventArgs(
        bool isSave, bool isReset,
        string? changedKey, object? oldValue, object? newValue,
        bool isAutoSave = false)
    {
        IsSave = isSave;
        IsReset = isReset;
        ChangedKey = changedKey;
        OldValue = oldValue;
        NewValue = newValue;
        IsAutoSave = isAutoSave;
    }
}
