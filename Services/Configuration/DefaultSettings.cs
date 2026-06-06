namespace Visunovia.Services.Configuration;

/// <summary>
/// 默认配置常量类，集中定义所有配置项的默认值和键名常量。
/// 所有配置相关的默认值应在此类中统一管理，便于维护和扩展。
/// </summary>
public static class DefaultSettings
{
    #region 配置节名称常量

    /// <summary>主配置节名称（对应 XML 中的 &lt;appSettings&gt; 节）</summary>
    public const string SectionName = "appSettings";

    #endregion

    #region 配置键名常量

    /// <summary>语言设置键名</summary>
    public const string LanguageKey = "Language";

    /// <summary>主题设置键名</summary>
    public const string ThemeKey = "Theme";

    /// <summary>编辑器字体大小键名</summary>
    public const string EditorFontSizeKey = "FontSize";

    /// <summary>自动保存间隔（秒）键名</summary>
    public const string AutoSaveIntervalKey = "AutoSaveInterval";

    /// <summary>最近打开项目数量上限键名</summary>
    public const string RecentProjectsLimitKey = "RecentProjectsLimit";

    /// <summary>预览窗口宽度键名</summary>
    public const string PreviewWidthKey = "PreviewWidth";

    /// <summary>预览窗口高度键名</summary>
    public const string PreviewHeightKey = "PreviewHeight";

    /// <summary>允许远程会话键名（控制是否监听所有网络接口）</summary>
    public const string AllowRemoteSessionKey = "AllowRemoteSession";

    /// <summary>首次运行标志键名（控制是否显示安装向导）</summary>
    public const string IsFirstRunKey = "IsFirstRun";

    #endregion

    #region 默认值常量

    /// <summary>默认语言（美式英语）</summary>
    public const string DefaultLanguage = "en-US";

    /// <summary>默认主题（亮色）</summary>
    public const string DefaultTheme = "light";

    /// <summary>默认编辑器字体大小（像素）</summary>
    public const int DefaultEditorFontSize = 14;

    /// <summary>默认自动保存间隔（秒），0 表示禁用自动保存</summary>
    public const int DefaultAutoSaveInterval = 0;

    /// <summary>默认最近打开项目数量上限</summary>
    public const int DefaultRecentProjectsLimit = 10;

    /// <summary>默认预览窗口宽度（像素）</summary>
    public const int DefaultPreviewWidth = 960;

    /// <summary>默认预览窗口高度（像素）</summary>
    public const int DefaultPreviewHeight = 540;

    /// <summary>默认不允许远程会话（仅本地连接）</summary>
    public const bool DefaultAllowRemoteSession = false;

    /// <summary>默认首次运行标志为 true（首次启动时显示安装向导）</summary>
    public const bool DefaultIsFirstRun = true;

    #endregion

    #region XML 元素名映射

    /// <summary>
    /// 将配置键名映射为 XML 元素名。
    /// 所有键名已统一为 PascalCase，与 XML 元素名一致。
    /// 唯一例外：Theme 键使用 "UI" 元素，值存储在 theme 属性中。
    /// </summary>
    public static string GetXmlElementName(string key)
    {
        return key switch
        {
            ThemeKey => "UI",
            _ => key
        };
    }

    /// <summary>
    /// 将 XML 元素名反向映射为配置键名。
    /// 用于反序列化时从 XML 元素名恢复为内部键名。
    /// </summary>
    public static string? GetKeyFromXmlElementName(string elementName)
    {
        return elementName switch
        {
            "UI" => ThemeKey,
            _ => null
        };
    }

    /// <summary>
    /// 判断指定键是否为属性式元素（使用属性而非文本内容存储值）。
    /// 目前只有 Theme 键使用属性式（&lt;UI theme="dark" /&gt;），其余均为文本式。
    /// </summary>
    public static bool IsAttributeElement(string key)
    {
        return key == ThemeKey;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取指定配置键的默认值。
    /// 当需要动态获取默认值时使用此方法，避免硬编码分散在各处。
    /// </summary>
    /// <param name="key">配置键名</param>
    /// <returns>对应的默认值，若键不存在则返回 null</returns>
    public static object? GetDefaultValue(string key)
    {
        return key switch
        {
            LanguageKey => DefaultLanguage,
            ThemeKey => DefaultTheme,
            EditorFontSizeKey => DefaultEditorFontSize,
            AutoSaveIntervalKey => DefaultAutoSaveInterval,
            RecentProjectsLimitKey => DefaultRecentProjectsLimit,
            PreviewWidthKey => DefaultPreviewWidth,
            PreviewHeightKey => DefaultPreviewHeight,
            AllowRemoteSessionKey => DefaultAllowRemoteSession,
            IsFirstRunKey => DefaultIsFirstRun,
            _ => null
        };
    }

    /// <summary>
    /// 获取所有已注册的配置键集合
    /// </summary>
    /// <returns>配置键名数组</returns>
    public static string[] GetAllKeys()
    {
        return
        [
            LanguageKey,
            ThemeKey,
            EditorFontSizeKey,
            AutoSaveIntervalKey,
            RecentProjectsLimitKey,
            PreviewWidthKey,
            PreviewHeightKey,
            AllowRemoteSessionKey,
            IsFirstRunKey
        ];
    }

    #endregion
}
