using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 语言信息数据传输对象，包含语言代码和显示名称
/// </summary>
public class LanguageInfoDto
{
    /// <summary>语言代码（PO 文件名去 .po 后缀，如 zh-CN、en-US）</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>语言显示名称（从 PO 文件的 Common.LanguageDisplayName 读取）</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
