namespace Visunovia.Services.Localization;

/// <summary>
/// 表示 PO（Portable Object）文件中的单个翻译条目。
/// 每个条目包含源字符串（msgid）、翻译字符串（msgstr）以及相关的元数据信息。
/// 遵循 GNU gettext PO 文件格式规范。
///
/// 核心字段：
/// - MsgId: 原始未翻译的源字符串
/// - MsgStr: 翻译后的目标语言字符串
/// - MsgCtxt: 用于消歧义的上下文标识符
/// - MsgIdPlural: 复数形式的源字符串
///
/// 支持功能：
/// - 单数/复数形式翻译
/// - 上下文消歧义
/// - 多种注释类型（译者注释、提取注释、引用注释、标志）
/// - 条目完整性验证
/// </summary>
public class PoEntry
{
    #region 公共属性

    /// <summary>
    /// 获取或设置原始未翻译的源字符串（msgid）。
    /// 这是 PO 文件中必须存在的关键字段，用于唯一标识该翻译条目。
    /// </summary>
    public string MsgId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置翻译后的目标语言字符串（msgstr）。
    /// 对于非复数形式的条目，此属性存储唯一的翻译结果。
    /// </summary>
    public string MsgStr { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置上下文标识符（msgctxt）。
    /// 用于区分具有相同 msgid 但含义不同的条目（如 "Open" 在菜单和文件操作中的不同含义）。
    /// 空字符串表示无上下文，与不存在 msgctx 行有不同语义。
    /// </summary>
    public string? MsgCtxt { get; set; }

    /// <summary>
    /// 获取或设置复数形式的源字符串（msgid_plural）。
    /// 当条目包含复数形式时，此属性存储复数版本的原始字符串。
    /// 若为 null 则表示该条目不涉及复数形式。
    /// </summary>
    public string? MsgIdPlural { get; set; }

    /// <summary>
    /// 获取或设置复数形式的翻译数组（msgstr[0], msgstr[1], ... msgstr[N]）。
    /// 数组索引对应不同复数规则下的翻译结果：
    /// - 索引 0: 通常对应单数形式（如 1 个项目）
    /// - 索引 1: 通常对应复数形式（如 2+ 个项目）
    /// - 更高索引: 取决于目标语言的复数规则
    ///
    /// 注意：当 HasPluralForms 为 true 时应使用此数组；否则使用 MsgStr 属性。
    /// </summary>
    public List<string> PluralTranslations { get; set; } = new();

    #endregion

    #region 注释集合

    /// <summary>
    /// 获取译者注释列表（以 "# " 开头的行）。
    /// 由译者手动添加和维护的注释，用于记录翻译决策、疑问或说明。
    /// 这些注释会被保留在 PO 文件中供其他译者参考。
    /// </summary>
    public List<string> TranslatorComments { get; set; } = new();

    /// <summary>
    /// 获取提取注释列表（以 "#." 开头的行）。
    /// 由 xgettext 工具从程序源代码中自动提取的程序员注释。
    /// 通常包含对译者的提示信息，帮助理解上下文。
    /// </summary>
    public List<string> ExtractedComments { get; set; } = new();

    /// <summary>
    /// 获取引用注释列表（以 "#:" 开头的行）。
    /// 包含源代码位置信息，格式为 "文件名:行号" 或仅 "文件名"。
    /// 由提取工具自动生成，帮助定位原始代码位置。
    /// 示例：["src/main.c:42", "lib/utils.c:15"]
    /// </summary>
    public List<string> ReferenceComments { get; set; } = new();

    /// <summary>
    /// 获取标志列表（以 "#," 开头的行）。
    /// 特殊标记，影响工具的行为和诊断输出。常见标志包括：
    /// - fuzzy: 标记为模糊匹配（可能需要人工审核）
    /// - c-format / csharp-format: C/C# 风格的 printf 格式化字符串
    /// - no-c-format: 非 printf 格式化字符串
    /// - python-format / no-python-format: Python 格式化相关标志
    /// - range:min..max: 数值参数的可能范围
    /// </summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>
    /// 获取前一版本注释列表（以 "#|" 开头的行）。
    /// 由 msgmerge 工具在标记 fuzzy 时插入，显示被修改前的原始字符串。
    /// 帮助译者了解源字符串的变化内容。
    /// </summary>
    public List<string> PreviousComments { get; set; } = new();

    #endregion

    #region 计算属性

    /// <summary>
    /// 获取一个值，指示此条目是否为 PO 文件的头部条目。
    /// 头部条目的特征是 msgid 为空字符串，msgstr 包含元数据键值对。
    /// </summary>
    public bool IsHeaderEntry => string.IsNullOrEmpty(MsgId);

    /// <summary>
    /// 获取一个值，指示此条目是否包含复数形式。
    /// 当 MsgIdPlural 不为空时返回 true，表示需要处理复数翻译。
    /// </summary>
    public bool HasPluralForms => !string.IsNullOrEmpty(MsgIdPlural);

    /// <summary>
    /// 获取一个值，指示此条目是否被标记为模糊（fuzzy）。
    /// Fuzzy 标志表示翻译可能是过时的或不准确的，需要人工审核。
    /// </summary>
    public bool IsFuzzy => Flags.Contains("fuzzy", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取一个值，指示此条目是否有有效的翻译。
    /// 对于普通条目：MsgStr 不为空
    /// 对于复数条目：至少有一个 PluralTranslations 元素不为空
    /// </summary>
    public bool HasTranslation =>
        HasPluralForms
            ? PluralTranslations.Any(t => !string.IsNullOrEmpty(t))
            : !string.IsNullOrEmpty(MsgStr);

    #endregion

    #region 公共方法

    /// <summary>
    /// 验证当前条目的完整性和有效性。
    /// 检查必填字段是否存在，以及数据格式的正确性。
    /// </summary>
    /// <returns>验证结果对象，包含是否通过验证及错误/警告信息</returns>
    public PoValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 必填字段检查：msgid 不能为 null
        if (MsgId == null)
        {
            errors.Add("msgid 不能为 null");
        }

        // 复数形式一致性检查
        if (HasPluralForms && PluralTranslations.Count == 0)
        {
            warnings.Add("条目声明了复数形式但未提供任何翻译（msgstr[n] 为空）");
        }

        // fuzzy 标志 + 空翻译组合警告
        if (IsFuzzy && HasTranslation)
        {
            warnings.Add("条目被标记为 fuzzy 但已存在翻译，建议人工审核后移除 fuzzy 标志");
        }

        return new PoValidationResult(
            errors.Count == 0,
            errors,
            warnings);
    }

    /// <summary>
    /// 创建当前条目的深拷贝，用于编辑场景中的撤销/重做支持。
    /// </summary>
    /// <returns>新的 PoEntry 实例，包含相同的所有数据</returns>
    public PoEntry Clone()
    {
        return new PoEntry
        {
            MsgId = MsgId,
            MsgStr = MsgStr,
            MsgCtxt = MsgCtxt,
            MsgIdPlural = MsgIdPlural,
            PluralTranslations = new List<string>(PluralTranslations),
            TranslatorComments = new List<string>(TranslatorComments),
            ExtractedComments = new List<string>(ExtractedComments),
            ReferenceComments = new List<string>(ReferenceComments),
            Flags = new List<string>(Flags),
            PreviousComments = new List<string>(PreviousComments)
        };
    }

    /// <summary>
    /// 获取用于查找的唯一键值。
    /// 组合上下文和 msgid 生成唯一标识符，支持相同 msgid 的上下文消歧义。
    /// </summary>
    /// <returns>格式为 "[context]\x04[msgid]" 或 "[msgid]" 的键</returns>
    public string GetKey()
    {
        if (!string.IsNullOrEmpty(MsgCtxt))
        {
            return $"{MsgCtxt}\x04{MsgId}";
        }
        return MsgId;
    }

    #endregion
}

/// <summary>
/// PO 条目验证结果，包含验证状态及详细的错误/警告信息。
/// </summary>
/// <param name="isSuccess">验证是否通过（无错误）</param>
/// <param name="errors">错误列表</param>
/// <param name="warnings">警告列表</param>
public class PoValidationResult(bool isSuccess, IReadOnlyList<string>? errors = null, IReadOnlyList<string>? warnings = null)
{
    /// <summary>验证是否通过（无错误）</summary>
    public bool IsSuccess { get; } = isSuccess;

    /// <summary>验证过程中发现的错误列表（阻止使用的严重问题）</summary>
    public IReadOnlyList<string> Errors { get; } = errors ?? Array.Empty<string>();

    /// <summary>验证过程中发现的警告列表（建议修复但不阻止使用的问题）</summary>
    public IReadOnlyList<string> Warnings { get; } = warnings ?? Array.Empty<string>();
}
