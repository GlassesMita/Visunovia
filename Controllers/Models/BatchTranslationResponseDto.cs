using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 批量翻译查询响应数据传输对象，包含多条翻译结果的映射
/// </summary>
public class BatchTranslationResponseDto
{
    /// <summary>翻译结果字典：key 为 msgId，value 为翻译文本</summary>
    [JsonPropertyName("translations")]
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>翻译所使用的目标语言</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
}
