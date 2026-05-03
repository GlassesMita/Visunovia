using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 批量翻译查询请求数据传输对象
/// 用于一次性请求多个文本字符串的翻译结果
/// </summary>
public class BatchTranslateRequestDto
{
    /// <summary>要翻译的消息标识符列表（msgid 集合）</summary>
    [Required(ErrorMessage = "消息标识符列表不能为空")]
    [MinLength(1, ErrorMessage = "请提供至少一个消息标识符")]
    [JsonPropertyName("msgIds")]
    public List<string>? MsgIds { get; set; }
}
