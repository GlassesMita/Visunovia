using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 翻译查询响应数据传输对象，包含单条翻译结果
/// </summary>
public class TranslationResponseDto
{
    /// <summary>原始消息标识符（msgid）</summary>
    [JsonPropertyName("msgId")]
    public string MsgId { get; set; } = string.Empty;

    /// <summary>翻译后的文本</summary>
    [JsonPropertyName("translation")]
    public string Translation { get; set; } = string.Empty;

    /// <summary>翻译所使用的目标语言</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
}
