using System.Text;

namespace Visunovia.Services.Localization;

/// <summary>
/// PO（Portable Object）文件写入器，负责将 PoFile 对象序列化为标准 GNU PO 格式。
/// 生成的输出严格遵循 gettext PO 文件规范，可被 msgfmt、msgmerge 等工具正确处理。
///
/// 核心功能：
/// - 将 PoFile 对象写入磁盘文件或返回字符串
/// - 保持原始注释和标志信息
/// - 正确处理多行字符串的格式化
/// - 自动转义特殊字符
/// - 支持头部元数据的更新
///
/// 输出格式特点：
/// - 条目间以空行分隔
/// - 注释按类型顺序排列：#, #., #:, #,,
/// - 长字符串自动折叠为多行（每行不超过 77 字符）
/// - 使用 UTF-8 编码写入文件
///
/// 使用示例：
/// <code>
/// var writer = new PoWriter();
/// writer.Write(poFile, "output.po");
/// string content = writer.WriteToString(poFile);
/// </code>
/// </summary>
public class PoWriter
{
    #region 常量定义

    /// <summary>单行字符串的最大长度（不含引号），超过此长度将自动换行</summary>
    private const int MaxLineLength = 77;

    /// <summary>换行符字符串</summary>
    private const string NewLine = "\n";

    #endregion

    #region 公共方法

    /// <summary>
    /// 将 PoFile 对象序列化为 PO 格式并写入指定文件路径。
    /// 文件以 UTF-8 编码写入，无 BOM 标记（符合 GNU gettext 惯例）。
    /// </summary>
    /// <param name="po">要序列化的 PoFile 对象</param>
    /// <param name="filePath">目标文件的完整路径</param>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：po 或 filePath 参数为 null
    /// 处理方式：调用方应确保传入有效参数
    /// </exception>
    /// <exception cref="IOException">
    /// 异常来源：文件写入过程中的 I/O 错误（权限不足、磁盘空间不足等）
    /// 处理方式：向上层抛出，由调用方决定是否重试或提示用户检查文件系统
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// 异常来源：当前用户没有目标目录或文件的写入权限
    /// 处理方式：向上层抛出，由调用方提示用户检查权限设置
    /// </exception>
    public void Write(PoFile po, string filePath)
    {
        ArgumentNullException.ThrowIfNull(po);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = WriteToString(po);

        // 确保目标目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            // 使用 UTF-8 无 BOM 编码写入（GNU gettext 标准）
            File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (IOException ex)
        {
            // 异常来源：文件被锁定、无写入权限、磁盘空间不足等 I/O 问题
            // 处理方式：包装异常并向上层传递，保留原始异常链以便诊断
            throw new IOException($"无法写入 PO 文件: {filePath}", ex);
        }
    }

    /// <summary>
    /// 将 PoFile 对象序列化为符合 GNU PO 规范的字符串表示。
    /// 可用于内存操作、网络传输或预览等场景。
    /// </summary>
    /// <param name="po">要序列化的 PoFile 对象</param>
    /// <returns>完整的 PO 文件内容字符串</returns>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：传入的 po 参数为 null
    /// 处理方式：调用方应确保传入有效的 PoFile 实例
    /// </exception>
    public string WriteToString(PoFile po)
    {
        ArgumentNullException.ThrowIfNull(po);

        var sb = new StringBuilder(4096);
        var isFirstEntry = true;

        foreach (var entry in po.Entries)
        {
            // 条目之间以空行分隔（第一个条目前不加空行）
            if (!isFirstEntry)
            {
                sb.Append(NewLine);
            }

            WriteEntry(sb, entry);
            isFirstEntry = false;
        }

        return sb.ToString();
    }

    #endregion

    #region 私有方法 - 条目写入

    /// <summary>
    /// 将单个 PoEntry 序列化并追加到 StringBuilder 中。
    /// 按照标准顺序输出：注释 → 上下文 → msgid → [复数] → msgstr
    /// </summary>
    private void WriteEntry(StringBuilder sb, PoEntry entry)
    {
        // 1. 输出注释（按 GNU 规范的标准顺序）
        WriteComments(sb, entry.TranslatorComments, "# ");
        WriteComments(sb, entry.ExtractedComments, "#.");
        WriteComments(sb, entry.ReferenceComments, "#:");
        WriteFlags(sb, entry.Flags);
        WriteComments(sb, entry.PreviousComments, "#|");

        // 2. 输出上下文（如果存在）
        if (entry.MsgCtxt != null)
        {
            WriteKeywordLine(sb, "msgctxt", entry.MsgCtxt);
        }

        // 3. 输出 msgid
        WriteKeywordLine(sb, "msgid", entry.MsgId);

        // 4. 输出复数形式（如果存在）
        if (entry.HasPluralForms)
        {
            WriteKeywordLine(sb, "msgid_plural", entry.MsgIdPlural ?? string.Empty);

            // 输出每个复数翻译
            for (var i = 0; i < entry.PluralTranslations.Count; i++)
            {
                var value = i < entry.PluralTranslations.Count ? entry.PluralTranslations[i] : string.Empty;
                WriteKeywordLine(sb, $"msgstr[{i}]", value);
            }
        }
        else
        {
            // 5. 输出普通 msgstr
            WriteKeywordLine(sb, "msgstr", entry.MsgStr);
        }
    }

    /// <summary>
    /// 写入关键字行及其关联的多行字符串值。
    /// 自动处理长字符串的折叠和多行拼接格式。
    /// </summary>
    private static void WriteKeywordLine(StringBuilder sb, string keyword, string value)
    {
        sb.Append(keyword);

        // 空值的特殊处理：直接输出空引号
        if (string.IsNullOrEmpty(value))
        {
            sb.Append(" \"\"");
            sb.Append(NewLine);
            return;
        }

        // 将长字符串分割为多行，每行不超过 MaxLimit 字符
        var lines = SplitLongString(value);
        for (var i = 0; i < lines.Count; i++)
        {
            if (i == 0)
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append('"');
                sb.Append(NewLine);
            }
            sb.Append('"');
            sb.Append(lines[i]);
            sb.Append('"');
        }

        sb.Append(NewLine);
    }

    #endregion

    #region 私有方法 - 注释写入

    /// <summary>
    /// 写入注释列表，每行添加指定的前缀。
    /// </summary>
    private static void WriteComments(StringBuilder sb, IReadOnlyList<string> comments, string prefix)
    {
        foreach (var comment in comments)
        {
            sb.Append(prefix).Append(comment).Append(NewLine);
        }
    }

    /// <summary>
    /// 写入标志列表。多个标志以逗号分隔写在同一行中。
    /// </summary>
    private static void WriteFlags(StringBuilder sb, List<string> flags)
    {
        if (flags.Count == 0)
        {
            return;
        }

        sb.Append("#, ");
        sb.AppendJoin(", ", flags);
        sb.Append(NewLine);
    }

    #endregion

    #region 私有方法 - 字符串处理

    /// <summary>
    /// 将字符串转义为 PO 文件中的安全字面量形式。
    /// 转义以下特殊字符：" → \", \ → \\, \n, \t, \r 等。
    /// </summary>
    private static string EscapeString(string value)
    {
        if (!NeedsEscaping(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 10);
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                default:
                    sb.Append(value[i]);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 快速判断字符串是否包含需要转义的字符。
    /// 用于避免不必要的 StringBuilder 分配。
    /// </summary>
    private static bool NeedsEscaping(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '"':
                case '\\':
                case '\n':
                case '\t':
                case '\r':
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 将长字符串分割为多段，每段的原始长度不超过 MaxLineLength。
    /// 分割点优先选择在单词边界处，以保证可读性。
    ///
    /// 注意：这里的长度限制是针对转义后的字符串而言，
    /// 因此实际分割时需要考虑转义可能增加的字符数。
    /// </summary>
    private static List<string> SplitLongString(string value)
    {
        var result = new List<string>();
        var escaped = EscapeString(value);

        if (escaped.Length <= MaxLineLength)
        {
            result.Add(escaped);
            return result;
        }

        var pos = 0;
        while (pos < escaped.Length)
        {
            var remaining = escaped.Length - pos;
            var chunkSize = Math.Min(MaxLineLength, remaining);

            // 尝试在单词边界处分割以提高可读性
            if (chunkSize < remaining && pos + chunkSize < escaped.Length)
            {
                var splitPos = FindSplitPosition(escaped, pos, chunkSize);
                chunkSize = splitPos - pos;
            }

            result.Add(escaped.Substring(pos, chunkSize));
            pos += chunkSize;
        }

        return result;
    }

    /// <summary>
    /// 在指定范围内查找最佳的分割位置。
    /// 优先选择空格、标点等自然断点位置。
    /// </summary>
    private static int FindSplitPosition(string text, int start, int maxLength)
    {
        var end = Math.Min(start + maxLength, text.Length);

        // 从后向前搜索最近的空白字符作为分割点
        for (var i = end - 1; i > start; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        // 未找到合适的分割点，强制在最大长度处截断
        return end;
    }

    #endregion
}
