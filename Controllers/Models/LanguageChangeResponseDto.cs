using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 语言切换结果响应数据传输对象
/// </summary>
public class LanguageChangeResponseDto
{
    /// <summary>操作是否成功</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>切换前的语言代码</summary>
    [JsonPropertyName("previousLanguage")]
    public string PreviousLanguage { get; set; } = string.Empty;

    /// <summary>切换后的新语言代码</summary>
    [JsonPropertyName("newLanguage")]
    public string NewLanguage { get; set; } = string.Empty;
}
