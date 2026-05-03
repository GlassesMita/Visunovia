using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 语言列表响应数据传输对象，包含当前语言和所有可用语言信息
/// </summary>
public class LanguagesResponseDto
{
    /// <summary>当前活动的语言代码</summary>
    [JsonPropertyName("currentLanguage")]
    public string CurrentLanguage { get; set; } = string.Empty;

    /// <summary>所有可用语言的代码列表</summary>
    [JsonPropertyName("availableLanguages")]
    public List<string> AvailableLanguages { get; set; } = new();

    /// <summary>回退语言代码</summary>
    [JsonPropertyName("fallbackLanguage")]
    public string FallbackLanguage { get; set; } = string.Empty;
}
