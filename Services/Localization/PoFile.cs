namespace Visunovia.Services.Localization;

/// <summary>
/// 表示完整的 PO（Portable Object）翻译文件。
/// 作为 PO 文件解析结果的顶层容器，封装头部元数据和所有翻译条目。
/// 提供条目查找、翻译进度统计和文件完整性验证等功能。
///
/// 结构组成：
/// - Header: 文件头部的元数据信息（项目名、语言、编码等）
/// - Entries: 所有翻译条目的有序列表（包含头部条目作为第一个元素）
/// - FilePath: 源文件的路径（从文件加载时自动设置）
///
/// 使用示例：
/// <code>
/// // 从文件加载
/// var poFile = new PoParser().Parse("locale/zh-CN/messages.po");
///
/// // 查找翻译
/// var entry = poFile.FindByMsgId("Hello World");
/// if (entry?.HasTranslation == true)
/// {
///     Console.WriteLine(entry.MsgStr);
/// }
///
/// // 统计进度
/// var progress = poFile.GetTranslationProgress();
/// Console.WriteLine($"已翻译: {progress.TranslatedCount}/{progress.TotalCount}");
/// </code>
/// </summary>
public class PoFile
{
    #region 公共属性

    /// <summary>
    /// 获取或设置 PO 文件的头部元数据。
    /// 包含项目名称、语言代码、字符编码等描述性信息。
    /// 若 PO 文件不包含头部则为 null。
    /// </summary>
    public PoFileHeader? Header { get; set; }

    /// <summary>
    /// 获取 PO 文件中所有翻译条目的列表。
    /// 列表按文件中的出现顺序排列，第一个元素通常是头部条目（msgid 为空）。
    /// </summary>
    public List<PoEntry> Entries { get; } = new();

    /// <summary>
    /// 获取或设置源 PO 文件的完整路径。
    /// 在通过 PoParser.Parse(string filePath) 加载时自动设置。
    /// 用于日志记录、重新保存等需要知道原始位置的场合。
    /// </summary>
    public string? FilePath { get; set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化 PoFile 实例，使用空的头部和条目列表。
    /// 适用于程序化构建新 PO 文件的场景。
    /// </summary>
    public PoFile()
    {
    }

    /// <summary>
    /// 初始化 PoFile 实例并指定头部和初始条目列表。
    /// 通常由 PoParser 在解析完成后调用此构造函数创建结果对象。
    /// </summary>
    /// <param name="header">解析得到的头部元数据（可为 null）</param>
    /// <param name="entries">所有翻译条目的列表</param>
    internal PoFile(PoFileHeader? header, List<PoEntry> entries)
    {
        Header = header;
        Entries = entries;
    }

    #endregion

    #region 查找方法

    /// <summary>
    /// 根据 msgid 查找第一个匹配的翻译条目。
    /// 执行简单的线性搜索，返回 msgid 完全匹配的第一个条目。
    ///
    /// 注意：若存在多个具有相同 msgid 但不同上下文的条目，
    /// 此方法仅返回第一个匹配项。建议使用 FindByKey 进行精确查找。
    /// </summary>
    /// <param name="msgid">要查找的源字符串</param>
    /// <returns>匹配的 PoEntry；未找到则返回 null</returns>
    public PoEntry? FindByMsgId(string msgid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(msgid);

        foreach (var entry in Entries)
        {
            if (entry.MsgId == msgid)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据上下文和 msgid 的组合键精确查找翻译条目。
    /// 使用与 PoEntry.GetKey() 相同的键生成逻辑，
    /// 可正确区分相同 msgid 不同上下文的多个条目。
    /// </summary>
    /// <param name="msgid">源字符串</param>
    /// <param name="context">上下文标识符（可为 null 表示无上下文）</param>
    /// <returns>完全匹配的 PoEntry；未找到则返回 null</returns>
    public PoEntry? FindByKey(string msgid, string? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(msgid);

        var searchKey = string.IsNullOrEmpty(context) ? msgid : $"{context}\x04{msgid}";

        foreach (var entry in Entries)
        {
            if (entry.GetKey() == searchKey)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取所有非头部条目（即实际的翻译条目集合）。
    /// 过滤掉 IsHeaderEntry 为 true 的条目，仅保留可用的翻译数据。
    /// </summary>
    /// <returns>过滤后的翻译条目枚举</returns>
    public IEnumerable<PoEntry> GetTranslationEntries()
    {
        return Entries.Where(e => !e.IsHeaderEntry);
    }

    #endregion

    #region 统计方法

    /// <summary>
    /// 计算当前 PO 文件的翻译完成进度。
    /// 返回详细的统计数据，包括总数、已翻译数、未翻译数、模糊条目数和完成百分比。
    /// </summary>
    /// <returns>翻译进度统计结果</returns>
    public TranslationProgress GetTranslationProgress()
    {
        // 过滤出实际翻译条目（排除头部）
        var translationEntries = GetTranslationEntries().ToList();
        var totalCount = translationEntries.Count;
        var translatedCount = 0;
        var fuzzyCount = 0;
        var untranslatedCount = 0;

        foreach (var entry in translationEntries)
        {
            if (entry.IsFuzzy)
            {
                fuzzyCount++;
            }
            else if (entry.HasTranslation)
            {
                translatedCount++;
            }
            else
            {
                untranslatedCount++;
            }
        }

        // 计算百分比（排除模糊条目后计算已完成比例）
        var effectiveTotal = totalCount - fuzzyCount;
        var percentage = effectiveTotal > 0
            ? Math.Round((double)translatedCount / effectiveTotal * 100, 1)
            : 0;

        return new TranslationProgress(
            totalCount,
            translatedCount,
            fuzzyCount,
            untranslatedCount,
            percentage);
    }

    #endregion

    #region 验证方法

    /// <summary>
    /// 验证整个 PO 文件的完整性。
    /// 依次验证头部信息和每个翻译条目，汇总所有问题。
    /// </summary>
    /// <returns>综合验证结果，包含所有错误和警告的聚合信息</returns>
    public PoValidationResult Validate()
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        // 验证头部
        if (Header != null)
        {
            var headerResult = Header.Validate();
            AddValidationResult(headerResult, allErrors, allWarnings, "Header");
        }
        else
        {
            allWarnings.Add("PO 文件缺少头部条目");
        }

        // 验证每个条目
        for (var i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            var result = entry.Validate();
            AddValidationResult(result, allErrors, allWarnings, $"Entry[{i}] (msgid={entry.MsgId})");
        }

        return new PoValidationResult(
            allErrors.Count == 0,
            allErrors,
            allWarnings);
    }

    /// <summary>
    /// 将单个验证结果合并到全局结果集中，添加来源前缀以便定位问题。
    /// </summary>
    private static void AddValidationResult(PoValidationResult result, List<string> errors, List<string> warnings, string source)
    {
        foreach (var error in result.Errors)
        {
            errors.Add($"[{source}] {error}");
        }
        foreach (var warning in result.Warnings)
        {
            warnings.Add($"[{source}] {warning}");
        }
    }

    #endregion
}

/// <summary>
/// 翻译进度统计结果，提供 PO 文件翻译完成情况的快照数据。
/// </summary>
/// <param name="totalCount">翻译条目总数（不含头部条目）</param>
/// <param name="translatedCount">已完成翻译的非模糊条目数量</param>
/// <param name="fuzzyCount">被标记为模糊（fuzzy）的条目数量</param>
/// <param name="untranslatedCount">完全未翻译的条目数量</param>
/// <param name="percentage">翻译完成百分比（0-100，不含模糊条目）</param>
public class TranslationProgress(int totalCount, int translatedCount, int fuzzyCount, int untranslatedCount, double percentage)
{
    /// <summary>翻译条目总数（不含头部条目）</summary>
    public int TotalCount { get; } = totalCount;

    /// <summary>已完成翻译的非模糊条目数量</summary>
    public int TranslatedCount { get; } = translatedCount;

    /// <summary>被标记为模糊（fuzzy）的条目数量</summary>
    public int FuzzyCount { get; } = fuzzyCount;

    /// <summary>完全未翻译的条目数量</summary>
    public int UntranslatedCount { get; } = untranslatedCount;

    /// <summary>翻译完成百分比（0-100，不含模糊条目）</summary>
    public double Percentage { get; } = percentage;

    /// <summary>
    /// 获取人类可读的进度摘要字符串。
    /// 格式示例："已翻译 42/100 条 (42.0%)，模糊 3 条，未翻译 55 条"
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"已翻译 {TranslatedCount}/{TotalCount} 条 ({Percentage:F1}%)");

        if (FuzzyCount > 0)
        {
            sb.Append($"，模糊 {FuzzyCount} 条");
        }

        if (UntranslatedCount > 0)
        {
            sb.Append($"，未翻译 {UntranslatedCount} 条");
        }

        return sb.ToString();
    }
}
