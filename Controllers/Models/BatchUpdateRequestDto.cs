using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 批量更新设置请求数据传输对象
/// </summary>
public class BatchUpdateRequestDto
{
    /// <summary>要更新的设置项键值对字典</summary>
    [Required(ErrorMessage = "设置项不能为空")]
    [JsonPropertyName("settings")]
    public Dictionary<string, object>? Settings { get; set; }
}
