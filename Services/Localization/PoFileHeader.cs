using System.Text;

namespace Visunovia.Services.Localization;

/// <summary>
/// 表示 PO（Portable Object）文件头部的元数据信息。
/// 头部条目是 PO 文件中的第一个条目，其 msgid 为空字符串，
/// msgstr 包含一系列以换行符分隔的键值对，用于描述翻译文件的属性。
///
/// 标准头部字段遵循 GNU gettext 规范：
/// - Project-Id-Version: 项目名称和版本号
/// - Report-Msgid-Bugs-To: 错误报告接收邮箱
/// - POT-Creation-Date: 模板文件（.pot）的创建日期
/// - PO-Revision-Date: 翻译文件的最后修订日期
/// - Last-Translator: 最后一位修改翻译的人员信息
/// - Language-Team: 负责该语言翻译的团队信息
/// - Language: ISO 639 语言代码（如 zh-CN, en, ja）
/// - MIME-Version: MIME 协议版本（通常为 1.0）
/// - Content-Type: 内容类型和字符编码（如 text/plain; charset=UTF-8）
/// - Content-Transfer-Encoding: 内容传输编码方式（通常为 8bit）
///
/// 使用示例：
/// <code>
/// var header = PoFileHeader.CreateDefault("MyApp", "1.0.0", "zh-CN");
/// header.LastTranslator = "Translator Name &lt;email@example.com&gt;";
/// </code>
/// </summary>
public class PoFileHeader
{
    #region 常量定义

    /// <summary>默认的 MIME 版本</summary>
    public const string DefaultMimeVersion = "1.0";

    /// <summary>默认的内容类型模板</summary>
    public const string DefaultContentType = "text/plain; charset=UTF-8";

    /// <summary>默认的内容传输编码</summary>
    public const string DefaultContentTransferEncoding = "8bit";

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取或设置项目标识版本（Project-Id-Version）。
    /// 格式通常为 "项目名 版本号"，如 "Visunovia 1.0.0"。
    /// 此字段用于标识翻译所属的项目及版本。
    /// </summary>
    public string ProjectIdVersion { get; set; } = "PACKAGE VERSION";

    /// <summary>
    /// 获取或设置错误报告邮箱（Report-Msgid-Bugs-To）。
    /// 当发现源字符串问题或翻译错误时，用户可通过此地址反馈。
    /// 格式为邮箱地址或 URL，如 "bugs@example.com" 或 "https://example.com/issues"。
    /// </summary>
    public string ReportMsgidBugsTo { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 POT 模板文件创建日期（POT-Creation-Date）。
    /// 表示 .pot 模板文件最初生成的日期时间。
    /// 格式遵循 RFC 2822/ISO 8601，如 "2024-01-15 10:30+0000"。
    /// </summary>
    public string? PotCreationDate { get; set; }

    /// <summary>
    /// 获取或设置 PO 翻译文件修订日期（PO-Revision-Date）。
    /// 表示翻译文件最后一次被人工或工具修改的时间。
    /// 格式同 POT-Creation-Date。若为 "YEAR-MO-DA HO:MI+ZONE" 则表示尚未修订。
    /// </summary>
    public string? PoRevisionDate { get; set; }

    /// <summary>
    /// 获取或设置最后译者信息（Last-Translator）。
    /// 格式为 "姓名 &lt;邮箱&gt;"，如 "张三 &lt;zhangsan@example.com&gt;"。
    /// 用于记录最近一次修改此翻译文件的人员。
    /// </summary>
    public string? LastTranslator { get; set; }

    /// <summary>
    /// 获取或设置语言团队信息（Language-Team）。
    /// 格式为 "语言 &lt;邮箱或URL&gt;"，如 "Chinese &lt;zh-cn@example.com&gt;"。
    /// 标识负责维护此语言翻译的团队或组织。
    /// </summary>
    public string? LanguageTeam { get; set; }

    /// <summary>
    /// 获取或设置目标语言代码（Language）。
    /// 遵循 ISO 639-1（两位字母）或 IETF BCP 47 标准。
    /// 常见值：en（英语）、zh-CN（简体中文）、ja（日语）、ko（韩语）等。
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 获取或设置 MIME 版本（MIME-Version）。
    /// 通常固定为 "1.0"，符合 RFC 2045 规范。
    /// </summary>
    public string MimeVersion { get; set; } = DefaultMimeVersion;

    /// <summary>
    /// 获取或设置内容类型（Content-Type）。
    /// 指定文本类型和字符集编码，标准值为 "text/plain; charset=UTF-8"。
    /// 字符集声明对于正确解析 PO 文件至关重要。
    /// </summary>
    public string ContentType { get; set; } = DefaultContentType;

    /// <summary>
    /// 获取或设置内容传输编码（Content-Transfer-Encoding）。
    /// 指定邮件/文本传输时的编码方式，PO 文件中通常使用 "8bit"。
    /// 可选值：7bit、8bit、base64、quoted-printable 等。
    /// </summary>
    public string ContentTransferEncoding { get; set; } = DefaultContentTransferEncoding;

    /// <summary>
    /// 获取或设置自定义扩展头部字段。
    /// 存储非标准的额外元数据，键为字段名（不含冒号），值为字段内容。
    /// 例如：X-Generator、X-Domain 等。
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    #endregion

    #region 工厂方法

    /// <summary>
    /// 创建包含默认值的头部实例。
    /// 自动设置当前时间作为修订日期，并填充常用的默认字段值。
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="version">项目版本号</param>
    /// <param name="language">目标语言代码（如 "zh-CN"）</param>
    /// <returns>初始化完成的 PoFileHeader 实例</returns>
    public static PoFileHeader CreateDefault(string projectName, string version, string language)
    {
        return new PoFileHeader
        {
            ProjectIdVersion = $"{projectName} {version}",
            Language = language,
            ContentType = DefaultContentType,
            ContentTransferEncoding = DefaultContentTransferEncoding,
            MimeVersion = DefaultMimeVersion,
            PoRevisionDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mmzzz")
        };
    }

    #endregion

    #region 解析方法

    /// <summary>
    /// 从头部条目的 msgstr 内容解析元数据字段。
    /// 将换行分隔的 "Key: Value" 格式字符串解析为各个属性。
    /// </summary>
    /// <param name="headerEntry">PO 文件的头部条目（msgid 为空的条目）</param>
    /// <returns>解析后的 PoFileHeader 实例</returns>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：传入的 headerEntry 参数为 null
    /// 处理方式：调用方应确保传入有效的条目对象
    /// </exception>
    public static PoFileHeader FromPoEntry(PoEntry headerEntry)
    {
        ArgumentNullException.ThrowIfNull(headerEntry);

        var header = new PoFileHeader();
        var content = headerEntry.MsgStr;

        if (string.IsNullOrEmpty(content))
        {
            return header;
        }

        // 按换行符分割头部内容，逐行解析键值对
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            ParseHeaderLine(line, header);
        }

        return header;
    }

    /// <summary>
    /// 解析单行头部内容，提取键值对并设置对应属性。
    /// 支持 "Key: Value" 和 "Key:Value" 两种格式（冒号后可选空格）。
    /// </summary>
    private static void ParseHeaderLine(string line, PoFileHeader header)
    {
        // 跳过空行
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // 定位第一个冒号作为键值分隔符
        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
        {
            return;
        }

        var key = line[..colonIndex].Trim();
        var value = line[(colonIndex + 1)..].Trim();

        // 根据键名路由到对应的属性
        switch (key)
        {
            case "Project-Id-Version":
                header.ProjectIdVersion = value;
                break;
            case "Report-Msgid-Bugs-To":
                header.ReportMsgidBugsTo = value;
                break;
            case "POT-Creation-Date":
                header.PotCreationDate = value;
                break;
            case "PO-Revision-Date":
                header.PoRevisionDate = value;
                break;
            case "Last-Translator":
                header.LastTranslator = value;
                break;
            case "Language-Team":
                header.LanguageTeam = value;
                break;
            case "Language":
                header.Language = value;
                break;
            case "MIME-Version":
                header.MimeVersion = value;
                break;
            case "Content-Type":
                header.ContentType = value;
                break;
            case "Content-Transfer-Encoding":
                header.ContentTransferEncoding = value;
                break;
            default:
                // 未识别的字段存入自定义头部字典
                header.CustomHeaders[key] = value;
                break;
        }
    }

    #endregion

    #region 序列化方法

    /// <summary>
    /// 将头部元数据序列化为符合 GNU PO 规范的 msgstr 字符串格式。
    /// 生成的字符串可直接用作头部条目的 MsgStr 属性值。
    /// </summary>
    /// <returns>格式化的头部内容字符串，各字段以换行符分隔</returns>
    public override string ToString()
    {
        var sb = new StringBuilder(512);

        AppendField(sb, "Project-Id-Version", ProjectIdVersion);
        AppendField(sb, "Report-Msgid-BugsTo", ReportMsgidBugsTo);
        AppendField(sb, "POT-Creation-Date", PotCreationDate);
        AppendField(sb, "PO-Revision-Date", PoRevisionDate);
        AppendField(sb, "Last-Translator", LastTranslator);
        AppendField(sb, "Language-Team", LanguageTeam);
        AppendField(sb, "Language", Language);
        AppendField(sb, "MIME-Version", MimeVersion);
        AppendField(sb, "Content-Type", ContentType);
        AppendField(sb, "Content-Transfer-Encoding", ContentTransferEncoding);

        // 追加自定义头部字段（按字母排序以保证输出一致性）
        foreach (var kvp in CustomHeaders.OrderBy(x => x.Key))
        {
            AppendField(sb, kvp.Key, kvp.Value);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 向 StringBuilder 追加单个头部字段。
    /// 仅在值非空时追加，自动处理换行符。
    /// </summary>
    private static void AppendField(StringBuilder sb, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }
            sb.Append(key).Append(": ").Append(value);
        }
    }

    #endregion

    #region 验证方法

    /// <summary>
    /// 验证头部字段的完整性和有效性。
    /// 检查必需字段是否存在以及关键字段值的合法性。
    /// </summary>
    /// <returns>验证结果对象，包含是否通过验证及错误/警告信息</returns>
    public PoValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 必需字段检查：Language 是现代 gettext 工具链所期望的
        if (string.IsNullOrEmpty(Language))
        {
            warnings.Add("缺少 Language 字段，建议添加 ISO 639 语言代码");
        }

        // Content-Type 应包含 charset 声明
        if (string.IsNullOrEmpty(ContentType) || !ContentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Content-Type 缺少 charset 声明，可能导致字符编码问题");
        }

        // Project-Id-Version 使用默认值的警告
        if (ProjectIdVersion == "PACKAGE VERSION")
        {
            warnings.Add("Project-Id-Version 仍为默认值，建议更新为实际项目名称和版本");
        }

        return new PoValidationResult(
            errors.Count == 0,
            errors,
            warnings);
    }

    #endregion
}
