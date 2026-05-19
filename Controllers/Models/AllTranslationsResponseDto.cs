using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 全部翻译键值对响应数据传输对象
/// 用于 GET /api/localization/translations 端点，返回指定语言的所有翻译条目
/// </summary>
public class AllTranslationsResponseDto
{
    /// <summary>翻译数据所属的语言代码</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>翻译键值对字典（key 为 msgId，value 为翻译后的文本）</summary>
    [JsonPropertyName("translations")]
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>翻译条目总数</summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount => Translations.Count;
}
