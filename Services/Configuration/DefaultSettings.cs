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
    public const string LanguageKey = "language";

    /// <summary>主题设置键名</summary>
    public const string ThemeKey = "theme";

    /// <summary>编辑器字体大小键名</summary>
    public const string EditorFontSizeKey = "editorFontSize";

    /// <summary>自动保存间隔（秒）键名</summary>
    public const string AutoSaveIntervalKey = "autoSaveInterval";

    /// <summary>最近打开项目数量上限键名</summary>
    public const string RecentProjectsLimitKey = "recentProjectsLimit";

    /// <summary>预览窗口宽度键名</summary>
    public const string PreviewWidthKey = "previewWidth";

    /// <summary>预览窗口高度键名</summary>
    public const string PreviewHeightKey = "previewHeight";

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

    #endregion

    #region XML 元素名映射

    /// <summary>
    /// 将配置键名映射为语义化 XML 元素名。
    /// Language → "Language"（文本元素）
    /// Theme → 合并到 "UI" 元素的 theme 属性
    /// EditorFontSize → "FontSize"
    /// 其余键名首字母大写作为元素名
    /// </summary>
    public static string GetXmlElementName(string key)
    {
        return key switch
        {
            LanguageKey => "Language",
            ThemeKey => "UI",
            EditorFontSizeKey => "FontSize",
            AutoSaveIntervalKey => "AutoSaveInterval",
            RecentProjectsLimitKey => "RecentProjectsLimit",
            PreviewWidthKey => "PreviewWidth",
            PreviewHeightKey => "PreviewHeight",
            _ => key
        };
    }

    /// <summary>
    /// 将语义化 XML 元素名反向映射为配置键名。
    /// 用于反序列化时从 XML 元素名恢复为内部键名。
    /// </summary>
    public static string? GetKeyFromXmlElementName(string elementName)
    {
        return elementName switch
        {
            "Language" => LanguageKey,
            "UI" => ThemeKey,
            "FontSize" => EditorFontSizeKey,
            "AutoSaveInterval" => AutoSaveIntervalKey,
            "RecentProjectsLimit" => RecentProjectsLimitKey,
            "PreviewWidth" => PreviewWidthKey,
            "PreviewHeight" => PreviewHeightKey,
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
            PreviewHeightKey
        ];
    }

    #endregion
}
