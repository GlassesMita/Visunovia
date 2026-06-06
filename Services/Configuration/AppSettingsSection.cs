using System.Xml;

namespace Visunovia.Services.Configuration;

/// <summary>
/// 自定义配置节数据结构，使用语义化 XML 元素存储配置项。
/// 所有配置项统一存储在内部字典中，序列化为独立的 XML 元素。
///
/// XML 配置文件格式示例：
/// <code>
/// &lt;appSettings&gt;
///   &lt;Language&gt;zh-CN&lt;/Language&gt;
///   &lt;UI theme="dark" /&gt;
///   &lt;FontSize&gt;14&lt;/FontSize&gt;
///   &lt;AutoSaveInterval&gt;0&lt;/AutoSaveInterval&gt;
/// &lt;/appSettings&gt;
/// </code>
/// </summary>
public class AppSettingsSection
{
    #region 内部存储

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取所有配置键值对（只读快照）。
    /// </summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    #endregion

    #region 索引器

    /// <summary>
    /// 获取或设置指定键的配置值。
    /// </summary>
    /// <param name="key">配置键名</param>
    /// <returns>对应的配置值，不存在时返回 null</returns>
    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value != null)
                _values[key] = value;
            else
                _values.Remove(key);
        }
    }

    #endregion

    #region 序列化

    /// <summary>
    /// 将当前配置节序列化为语义化 XML 字符串。
    /// 每个配置项输出为独立的 XML 元素：
    /// - Language → &lt;Language&gt;zh-CN&lt;/Language&gt;
    /// - Theme → &lt;UI theme="dark" /&gt;
    /// - FontSize → &lt;FontSize&gt;14&lt;/FontSize&gt;
    /// 其余键使用 GetXmlElementName 映射后的元素名
    /// </summary>
    /// <returns>XML 格式的配置字符串</returns>
    public string SerializeToXml()
    {
        var sb = new System.Text.StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = true,
            Encoding = System.Text.Encoding.UTF8
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartElement(DefaultSettings.SectionName);

            foreach (var key in DefaultSettings.GetAllKeys())
            {
                var value = _values.TryGetValue(key, out var v) ? v : DefaultSettings.GetDefaultValue(key)?.ToString();
                if (value == null) continue;

                var elementName = DefaultSettings.GetXmlElementName(key);

                if (DefaultSettings.IsAttributeElement(key))
                {
                    writer.WriteStartElement(elementName);
                    writer.WriteAttributeString("theme", value);
                    writer.WriteEndElement();
                }
                else
                {
                    writer.WriteElementString(elementName, value);
                }
            }

            writer.WriteEndElement();
        }

        return sb.ToString();
    }

    #endregion

    #region 反序列化

    /// <summary>
    /// 从 XML 元素反序列化为 AppSettingsSection 实例。
    /// 支持两种格式：
    /// - 新格式语义化元素：&lt;Language&gt;zh-CN&lt;/Language&gt;、&lt;UI theme="dark" /&gt;
    /// - 旧格式兼容：&lt;add key="language" value="zh-CN" /&gt;、&lt;localized language="en-US" theme="dark" /&gt;
    /// </summary>
    /// <param name="element">包含配置数据的 XML 元素</param>
    /// <returns>填充后的 AppSettingsSection 实例</returns>
    public static AppSettingsSection DeserializeFromXml(XmlElement element)
    {
        var section = new AppSettingsSection();

        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is not XmlElement childElem) continue;

            switch (childElem.Name)
            {
                case "add":
                    var addKey = childElem.GetAttribute("key");
                    var addValue = childElem.GetAttribute("value");
                    if (!string.IsNullOrEmpty(addKey))
                    {
                        section._values[addKey] = addValue ?? string.Empty;
                    }
                    break;

                case "localized":
                    var lang = childElem.GetAttribute("language");
                    if (!string.IsNullOrEmpty(lang))
                        section._values[DefaultSettings.LanguageKey] = lang;
                    var theme = childElem.GetAttribute("theme");
                    if (!string.IsNullOrEmpty(theme))
                        section._values[DefaultSettings.ThemeKey] = theme;
                    break;

                default:
                    // 所有键名已统一为 PascalCase，XML 元素名即键名
                    // 唯一例外：UI 元素存储 Theme 值，通过 theme 属性读取
                    if (childElem.Name == "UI")
                    {
                        var themeAttr = childElem.GetAttribute("theme");
                        if (!string.IsNullOrEmpty(themeAttr))
                            section._values[DefaultSettings.ThemeKey] = themeAttr;
                    }
                    else
                    {
                        // 元素名直接作为键名（PascalCase）
                        var textValue = childElem.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(textValue))
                            section._values[childElem.Name] = textValue;
                    }
                    break;
            }
        }

        // 补全缺失的默认键，确保反序列化后所有配置项都存在
        foreach (var key in DefaultSettings.GetAllKeys())
        {
            if (!section._values.ContainsKey(key))
            {
                var defaultValue = DefaultSettings.GetDefaultValue(key);
                if (defaultValue != null)
                    section._values[key] = defaultValue.ToString()!;
            }
        }

        return section;
    }

    #endregion
}
