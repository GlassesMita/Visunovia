using System.Text;

namespace Visunovia.Services.Localization;

/// <summary>
/// PO（Portable Object）文件解析器，实现 GNU gettext PO 格式的完整语法解析。
/// 支持从文件路径或文本内容加载并解析 PO 文件，生成结构化的 PoFile 对象。
///
/// 解析能力：
/// - 标准 PO 条目（msgid + msgstr）
/// - 复数形式条目（msgid + msgid_plural + msgstr[n]）
/// - 上下文条目（msgctxt 消歧义）
/// - 多行字符串（连续双引号行自动拼接）
/// - 转义字符处理（\n, \t, \", \\, \r 等）
/// - 五种注释类型（#, #., #:, #,, #|）
/// - 头部元数据自动提取
///
/// 容错机制：
/// - 单个条目解析失败不会中断整个文件的解析
/// - 无效行会被跳过并记录警告
/// - 缺失字段使用合理的默认值
///
/// 使用示例：
/// <code>
/// var parser = new PoParser();
/// var poFile = parser.Parse("path/to/file.po");
/// foreach (var entry in poFile.Entries)
/// {
///     Console.WriteLine($"{entry.MsgId} -> {entry.MsgStr}");
/// }
/// </code>
/// </summary>
public class PoParser
{
    #region 私有字段

    /// <summary>解析过程中收集的警告信息</summary>
    private readonly List<string> _warnings = new();

    /// <summary>解析过程中收集的错误信息</summary>
    private readonly List<string> _errors = new();

    /// <summary>当前正在解析的行号（用于错误定位）</summary>
    private int _currentLineNum;

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取解析过程中产生的所有警告信息。
    /// 包含格式问题、非致命性错误等不影响解析结果的提示信息。
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// 获取解析过程中产生的所有错误信息。
    /// 包含导致某些条目被跳过的严重错误详情。
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    #endregion

    #region 公共方法 - 入口点

    /// <summary>
    /// 从指定文件路径加载并解析 PO 文件。
    /// 自动检测 UTF-8 编码（含或不含 BOM）并读取全部内容后进行解析。
    /// </summary>
    /// <param name="filePath">PO 文件的完整路径</param>
    /// <returns>解析完成的 PoFile 对象，包含头部信息和所有翻译条目</returns>
    /// <exception cref="FileNotFoundException">
    /// 异常来源：指定的文件路径不存在
    /// 处理方式：调用方应在调用前检查文件存在性，或捕获此异常提示用户
    /// </exception>
    /// <exception cref="IOException">
    /// 异常来源：文件读取过程中的 I/O 错误（权限不足、磁盘故障等）
    /// 处理方式：向上层抛出，由调用方决定是否重试或提示用户检查文件系统
    /// </exception>
    public PoFile Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PO 文件不存在: {filePath}", filePath);
        }

        string content;
        try
        {
            // 使用 UTF-8 编码读取，自动处理 BOM
            content = File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch (IOException ex)
        {
            // 异常来源：文件被锁定、无读取权限、磁盘空间不足等 I/O 问题
            // 处理方式：包装异常并向上层传递，保留原始异常链
            throw new IOException($"无法读取 PO 文件: {filePath}", ex);
        }

        var result = ParseText(content);
        result.FilePath = filePath;
        return result;
    }

    /// <summary>
    /// 从文本内容直接解析 PO 文件数据。
    /// 适用于已加载到内存的字符串内容，或需要预处理内容的场景。
    /// </summary>
    /// <param name="content">PO 文件的完整文本内容</param>
    /// <returns>解析完成的 PoFile 对象</returns>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：传入的内容为 null
    /// 处理方式：调用方应确保传入有效的字符串内容
    /// </exception>
    public PoFile ParseText(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // 重置状态
        _warnings.Clear();
        _errors.Clear();
        _currentLineNum = 0;

        // 将内容按行分割，保留空行以维持结构信息
        var lines = SplitIntoLines(content);
        var entries = new List<PoEntry>();
        PoFileHeader? header = null;

        // 逐行扫描，按条目边界切分
        var entryLines = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            _currentLineNum = i + 1;
            var line = lines[i];

            // 空行表示条目边界
            if (IsBlankLine(line))
            {
                if (entryLines.Count > 0)
                {
                    var entry = ParseEntryBlock(entryLines);
                    if (entry != null)
                    {
                        entries.Add(entry);
                        // 第一个条目（msgid 为空）作为头部
                        if (header == null && entry.IsHeaderEntry)
                        {
                            header = PoFileHeader.FromPoEntry(entry);
                        }
                    }
                    entryLines.Clear();
                }
                continue;
            }

            entryLines.Add(line);
        }

        // 处理文件末尾没有空行的情况（最后一个条目）
        if (entryLines.Count > 0)
        {
            var entry = ParseEntryBlock(entryLines);
            if (entry != null)
            {
                entries.Add(entry);
                if (header == null && entry.IsHeaderEntry)
                {
                    header = PoFileHeader.FromPoEntry(entry);
                }
            }
        }

        return new PoFile(header, entries);
    }

    #endregion

    #region 私有方法 - 行级处理

    /// <summary>
    /// 将文本内容分割为行列表。
    /// 同时支持 \n、\r\n 和 \r 作为换行符，确保跨平台兼容性。
    /// </summary>
    private static List<string> SplitIntoLines(string content)
    {
        var lines = new List<string>();
        var sb = new StringBuilder();
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '\r')
            {
                // 处理 \r\n 或单独的 \r
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++; // 跳过 \n
                }
                lines.Add(sb.ToString());
                sb.Clear();
            }
            else if (ch == '\n')
            {
                lines.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        // 处理末尾没有换行符的情况
        if (sb.Length > 0 || content.EndsWith('\n') || content.EndsWith('\r'))
        {
            lines.Add(sb.ToString());
        }

        return lines;
    }

    /// <summary>
    /// 判断一行是否为空白行（仅包含空白字符或为空）。
    /// 空白行在 PO 文件中用作条目之间的分隔符。
    /// </summary>
    private static bool IsBlankLine(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (!char.IsWhiteSpace(line[i]))
            {
                return false;
            }
        }
        return true;
    }

    #endregion

    #region 私有方法 - 条目块解析

    /// <summary>
    /// 解析单个条目的所有行，构建 PoEntry 对象。
    /// 这是解析的核心方法，负责识别注释、上下文、msgid、复数和 msgstr 等各部分。
    /// </summary>
    /// <param name="lines">构成该条目的原始行列表</param>
    /// <returns>解析成功的 PoEntry；若严重失败则返回 null 并记录错误</returns>
    private PoEntry? ParseEntryBlock(List<string> lines)
    {
        var entry = new PoEntry();
        StringSegment? currentTarget = null;
        var pluralIndex = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // 空行或纯空白行在条目内部应被忽略
            if (trimmed.Length == 0)
            {
                continue;
            }

            // 注释行识别和处理
            if (trimmed[0] == '#')
            {
                ParseCommentLine(trimmed, entry);
                continue;
            }

            // 字符串行（以双引号开头）
            if (trimmed[0] == '"')
            {
                AppendToStringSegment(entry, currentTarget, ref pluralIndex, trimmed);
                continue;
            }

            // 关键字行（msgid, msgstr, msgctxt, msgid_plural）
            if (TryParseKeywordLine(trimmed, entry, out var newTarget, out var newIndex))
            {
                currentTarget = newTarget;
                if (newIndex.HasValue)
                {
                    pluralIndex = newIndex.Value;
                }
                continue;
            }

            // 无法识别的非空行，记录警告但继续解析
            AddWarning($"第 {_currentLineNum} 行: 无法识别的内容 '{trimmed}'");
        }

        return entry;
    }

    /// <summary>
    /// 解析注释行并根据前缀分类存储到对应的集合中。
    /// 支持五种注释类型：译者注释(#)、提取注释(#.)、引用注释(#:)、标志(#,)、前一版本(#)。
    /// </summary>
    private void ParseCommentLine(string line, PoEntry entry)
    {
        // 注释行长度至少为 2 个字符（# 加上至少一个后续字符）
        if (line.Length < 2)
        {
            entry.TranslatorComments.Add(line);
            return;
        }

        var commentChar = line[1];

        switch (commentChar)
        {
            case ' ':
                // 译者注释 (# )：# 后跟空格，去除 "# " 前缀
                if (line.Length > 2)
                {
                    entry.TranslatorComments.Add(line[2..]);
                }
                break;

            case '.':
                // 提取注释 (#.)：程序源码中的注释，去除 "#." 前缀
                if (line.Length > 2)
                {
                    entry.ExtractedComments.Add(line[2..].TrimStart());
                }
                break;

            case ':':
                // 引用注释 (#:)：源代码位置引用，去除 "#:" 前缀
                if (line.Length > 2)
                {
                    entry.ReferenceComments.Add(line[2..].TrimStart());
                }
                break;

            case ',':
                // 标志 (#,)：逗号分隔的标志列表，去除 "#," 前缀
                if (line.Length > 2)
                {
                    var flagsStr = line[2..].TrimStart();
                    // 单个标志行可能包含多个逗号分隔的标志
                    foreach (var flag in flagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        entry.Flags.Add(flag);
                    }
                }
                break;

            case '|':
                // 前一版本注释 (#|)：msgmerge 生成的变更对比
                if (line.Length > 2)
                {
                    entry.PreviousComments.Add(line[2..].TrimStart());
                }
                break;

            default:
                // 其他以 # 开头的行归入译者注释
                entry.TranslatorComments.Add(line);
                break;
        }
    }

    /// <summary>
    /// 尝试解析关键字行（msgid, msgstr, msgctxt, msgid_plural, msgstr[N]）。
    /// 识别关键字并设置当前的目标字符串段。
    /// </summary>
    /// <returns>是否成功识别为关键字行</returns>
    private bool TryParseKeywordLine(string line, PoEntry entry, out StringSegment? target, out int? pluralIdx)
    {
        target = null;
        pluralIdx = null;

        if (line.StartsWith("msgid"))
        {
            target = StringSegment.MsgId;
            // 提取 msgid 行中的初始值（如果有内联字符串）
            ExtractInlineString(line[5..], entry, (v) => entry.MsgId = v);
            return true;
        }

        if (line.StartsWith("msgctxt"))
        {
            target = StringSegment.MsgCtxt;
            ExtractInlineString(line[7..], entry, (v) => entry.MsgCtxt = v);
            return true;
        }

        if (line.StartsWith("msgid_plural"))
        {
            target = StringSegment.MsgIdPlural;
            ExtractInlineString(line[12..], entry, (v) => entry.MsgIdPlural = v);
            return true;
        }

        if (line.StartsWith("msgstr"))
        {
            var rest = line[6..];
            // 检查是否为复数形式 msgstr[N]
            if (rest.StartsWith('['))
            {
                var closeBracket = rest.IndexOf(']');
                if (closeBracket > 1)
                {
                    var indexStr = rest[1..closeBracket];
                    if (int.TryParse(indexStr.Trim(), out var idx))
                    {
                        target = StringSegment.PluralTranslation;
                        pluralIdx = idx;
                        // 确保 PluralTranslations 数组足够大
                        while (entry.PluralTranslations.Count <= idx)
                        {
                            entry.PluralTranslations.Add(string.Empty);
                        }
                        ExtractInlineString((closeBracket + 1 < rest.Length ? rest[(closeBracket + 1)..] : string.Empty),
                            entry, (v) => entry.PluralTranslations[idx] = v);
                        return true;
                    }
                }
            }

            target = StringSegment.MsgStr;
            ExtractInlineString(rest, entry, (v) => entry.MsgStr = v);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从关键字行的剩余部分提取内联的初始字符串值。
    /// 例如：msgid "hello" 中的 "hello" 部分。
    /// </summary>
    private static void ExtractInlineString(string rest, PoEntry entry, Action<string> setter)
    {
        var trimmed = rest.TrimStart();
        if (trimmed.Length >= 2 && trimmed[0] == '"')
        {
            var value = ParseQuotedString(trimmed);
            if (value != null)
            {
                setter(value);
            }
        }
    }

    /// <summary>
    /// 将当前行的字符串内容追加到对应的目标段中。
    /// 处理多行字符串的拼接逻辑。
    /// </summary>
    private static void AppendToStringSegment(PoEntry entry, StringSegment? target, ref int pluralIndex, string line)
    {
        var value = ParseQuotedString(line);
        if (value == null)
        {
            return;
        }

        switch (target)
        {
            case StringSegment.MsgId:
                entry.MsgId += value;
                break;
            case StringSegment.MsgStr:
                entry.MsgStr += value;
                break;
            case StringSegment.MsgCtxt:
                entry.MsgCtxt = (entry.MsgCtxt ?? string.Empty) + value;
                break;
            case StringSegment.MsgIdPlural:
                entry.MsgIdPlural = (entry.MsgIdPlural ?? string.Empty) + value;
                break;
            case StringSegment.PluralTranslation:
                // 确保数组索引有效
                while (entry.PluralTranslations.Count <= pluralIndex)
                {
                    entry.PluralTranslations.Add(string.Empty);
                }
                entry.PluralTranslations[pluralIndex] += value;
                break;
        }
    }

    #endregion

    #region 私有方法 - 字符串解析

    /// <summary>
    /// 解析带引号的 PO 字符串字面量。
    /// 提取双引号之间的内容并处理转义字符序列。
    ///
    /// 支持的转义序列：
    /// - \n: 换行符 (LF)
    /// - \t: 制表符 (TAB)
    /// - \r: 回车符 (CR)
    /// - \": 双引号
    /// - \\: 反斜杠本身
    /// - \': 单引号（非标准但常见）
    /// - \a: 响铃 (BEL)
    /// - \b: 退格 (BS)
    /// - \f: 换页 (FF)
    /// - \v: 垂直制表 (VT)
    /// - \0oo: 八进制转义（最多3位八进制数字）
    /// - \xhh: 十六进制转义（可变长十六进制数字）
    /// </summary>
    /// <param name="line">包含引号字符串的原始行</param>
    /// <returns>解码后的字符串；若格式无效则返回 null</returns>
    private static string? ParseQuotedString(string line)
    {
        var trimmed = line.TrimStart();

        // 必须以双引号开头
        if (trimmed.Length < 2 || trimmed[0] != '"')
        {
            return null;
        }

        // 找到匹配的闭合引号
        var endQuote = FindClosingQuote(trimmed, 1);
        if (endQuote < 0)
        {
            return null;
        }

        // 提取引号间的内容并处理转义序列
        var rawContent = trimmed.Substring(1, endQuote - 1);
        return UnescapeString(rawContent);
    }

    /// <summary>
    /// 从指定位置开始查找匹配的闭合双引号。
    /// 正确处理嵌套的转义引号（\" 不算作闭合）。
    /// </summary>
    /// <param name="text">要搜索的字符串</param>
    /// <param name="startIndex">搜索起始位置（起始引号之后的位置）</param>
    /// <returns>闭合引号的索引；未找到则返回 -1</returns>
    private static int FindClosingQuote(string text, int startIndex)
    {
        var i = startIndex;
        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                // 跳过转义字符及其后的字符
                i += 2;
                continue;
            }

            if (text[i] == '"')
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    /// <summary>
    /// 解析 PO 字符串中的转义字符序列。
    /// 使用高性能的单次遍历方式处理所有标准转义序列。
    /// </summary>
    /// <param name="raw">原始的（含转义标记的）字符串内容</param>
    /// <returns>完成转义处理的最终字符串</returns>
    private static string UnescapeString(string raw)
    {
        if (!raw.Contains('\\'))
        {
            return raw;
        }

        var sb = new StringBuilder(raw.Length);
        var i = 0;

        while (i < raw.Length)
        {
            if (raw[i] != '\\')
            {
                sb.Append(raw[i]);
                i++;
                continue;
            }

            // 遇到反斜杠，检查后续字符
            if (i + 1 >= raw.Length)
            {
                // 结尾处的孤立反斜杠，原样保留
                sb.Append('\\');
                i++;
                continue;
            }

            var next = raw[i + 1];
            switch (next)
            {
                case 'n':
                    sb.Append('\n');
                    i += 2;
                    break;
                case 't':
                    sb.Append('\t');
                    i += 2;
                    break;
                case 'r':
                    sb.Append('\r');
                    i += 2;
                    break;
                case '"':
                    sb.Append('"');
                    i += 2;
                    break;
                case '\\':
                    sb.Append('\\');
                    i += 2;
                    break;
                case '\'':
                    sb.Append('\'');
                    i += 2;
                    break;
                case 'a':
                    sb.Append('\a');
                    i += 2;
                    break;
                case 'b':
                    sb.Append('\b');
                    i += 2;
                    break;
                case 'f':
                    sb.Append('\f');
                    i += 2;
                    break;
                case 'v':
                    sb.Append('\v');
                    i += 2;
                    break;
                case '0' when i + 2 < raw.Length && IsOctalDigit(raw[i + 2]):
                    // 八进制转义：\0oo 形式（1-3位八进制数字）
                    var octalEnd = i + 2;
                    var octalCount = 1;
                    while (octalCount < 3 && octalEnd < raw.Length && IsOctalDigit(raw[octalEnd]))
                    {
                        octalEnd++;
                        octalCount++;
                    }
                    var octalStr = raw.Substring(i + 2, octalEnd - (i + 2));
                    sb.Append((char)Convert.ToInt32(octalStr, 8));
                    i = octalEnd;
                    break;
                case 'x' when i + 2 < raw.Length && IsHexDigit(raw[i + 2]):
                    // 十六进制转义：\xhh 形式（可变长十六进制数字）
                    var hexEnd = i + 2;
                    while (hexEnd < raw.Length && hexEnd - (i + 2) < 4 && IsHexDigit(raw[hexEnd]))
                    {
                        hexEnd++;
                    }
                    var hexStr = raw.Substring(i + 2, hexEnd - (i + 2));
                    sb.Append((char)Convert.ToInt32(hexStr, 16));
                    i = hexEnd;
                    break;
                default:
                    // 未知的转义序列，原样保留反斜杠和后续字符
                    sb.Append('\\');
                    sb.Append(next);
                    i += 2;
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>判断字符是否为八进制数字（0-7）</summary>
    private static bool IsOctalDigit(char c) => c is >= '0' and <= '7';

    /// <summary>判断字符是否为十六进制数字（0-9, a-f, A-F）</summary>
    private static bool IsHexDigit(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    #endregion

    #region 私有方法 - 诊断信息

    /// <summary>
    /// 添加一条警告信息。不会中断解析流程。
    /// </summary>
    private void AddWarning(string message)
    {
        _warnings.Add(message);
    }

    /// <summary>
    /// 添加一条错误信息。用于记录解析过程中的异常情况。
    /// </summary>
    private void AddError(string message)
    {
        _errors.Add(message);
    }

    #endregion

    #region 内部枚举

    /// <summary>
    /// 当前正在解析的字符串目标段标识符。
    /// 用于跟踪多行字符串拼接时的目标位置。
    /// </summary>
    private enum StringSegment
    {
        MsgId,
        MsgStr,
        MsgCtxt,
        MsgIdPlural,
        PluralTranslation
    }

    #endregion
}
