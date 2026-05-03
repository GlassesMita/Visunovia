using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 语言信息数据传输对象，描述单个语言的详细信息
/// </summary>
public class LanguageInfoDto
{
    /// <summary>语言代码（如 "zh-CN"、"en-US"）</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>语言显示名称（可选）</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>该语言是否可用（PO 文件是否存在）</summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }
}
