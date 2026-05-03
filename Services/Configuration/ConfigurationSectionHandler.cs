using System.Configuration;
using System.Xml;

namespace Visunovia.Services.Configuration;

/// <summary>
/// XML 配置节处理器，实现 IConfigurationSectionHandler 接口。
/// 负责解析配置文件中的自定义配置节，将其转换为强类型的 AppSettingsSection 对象。
///
/// 此处理器在 .NET 配置系统读取 &lt;configSections&gt; 中注册的自定义节时被自动调用。
///
/// 注册方式（在 app.config / exe.config 中）：
/// <code>
/// &lt;configSections&gt;
///   &lt;section name="appSettings"
///            type="Visunovia.Services.Configuration.ConfigurationSectionHandler, Visunovia"
///            allowLocation="true"
///            allowDefinition="Everywhere" /&gt;
/// &lt;/configSections&gt;
/// </code>
/// </summary>
public class ConfigurationSectionHandler : IConfigurationSectionHandler
{
    /// <summary>
    /// 创建配置节对象。由 .NET 配置系统在加载配置时自动调用。
    /// 将原始 XML 节点转换为类型安全的 AppSettingsSection 实例。
    /// </summary>
    /// <param name="parent">父配置对象（通常为 null，除非存在嵌套的配置组）</param>
    /// <param name="configContext">配置上下文（通常为 null，用于 HttpConfiguration 场景）</param>
    /// <param name="section">待解析的 XML 节点（对应 &lt;appSettings&gt; 元素）</param>
    /// <returns>填充完成的 AppSettingsSection 实例；当节点无效时返回含默认值的实例</returns>
    public object Create(object? parent, object? configContext, XmlNode section)
    {
        // 异常场景：传入节点为空时返回默认配置实例
        // 来源：配置文件中未定义该节，或 configSections 注册缺失
        // 处理方式：返回带有全部默认值的 AppSettingsSection，确保系统可正常启动
        if (section == null)
        {
            return CreateDefaultSection();
        }

        try
        {
            return ParseSectionNode(section);
        }
        catch (XmlException ex)
        {
            // 异常来源：XML 格式错误（如标签未闭合、非法字符、编码问题）
            // 处理方式：记录异常并回退到默认配置，避免因配置文件损坏导致应用崩溃
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigurationSectionHandler] XML 解析异常: {ex.Message}");

            return CreateDefaultSection();
        }
        catch (Exception ex)
        {
            // 异常来源：意外的运行时错误（如类型转换失败、内存不足等）
            // 处理方式：捕获所有未预期异常，返回默认配置保证可用性
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigurationSectionHandler] 未知异常: {ex.GetType().Name} - {ex.Message}");

            return CreateDefaultSection();
        }
    }

    #region 解析逻辑

    /// <summary>
    /// 解析 XML 节点为 AppSettingsSection 实例的核心方法。
    /// 遍历子元素和属性，将 XML 数据映射到强类型对象。
    /// </summary>
    private static AppSettingsSection ParseSectionNode(XmlNode section)
    {
        if (section is not XmlElement sectionElement)
        {
            return CreateDefaultSection();
        }

        return AppSettingsSection.DeserializeFromXml(sectionElement);
    }

    /// <summary>
    /// 创建包含所有默认值的 AppSettingsSection 实例。
    /// 作为配置文件不存在或解析失败时的回退方案。
    /// </summary>
    private static AppSettingsSection CreateDefaultSection()
    {
        var section = new AppSettingsSection();
        section[DefaultSettings.LanguageKey] = DefaultSettings.DefaultLanguage;
        section[DefaultSettings.ThemeKey] = DefaultSettings.DefaultTheme;
        section[DefaultSettings.EditorFontSizeKey] = DefaultSettings.DefaultEditorFontSize.ToString();
        section[DefaultSettings.AutoSaveIntervalKey] = DefaultSettings.DefaultAutoSaveInterval.ToString();
        section[DefaultSettings.RecentProjectsLimitKey] = DefaultSettings.DefaultRecentProjectsLimit.ToString();
        section[DefaultSettings.PreviewWidthKey] = DefaultSettings.DefaultPreviewWidth.ToString();
        section[DefaultSettings.PreviewHeightKey] = DefaultSettings.DefaultPreviewHeight.ToString();
        return section;
    }

    #endregion
}
