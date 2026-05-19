using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 语言信息数据传输对象，包含语言代码、显示名称和当前激活状态。
/// 用于构建语言选择器 UI，提供完整的语言标识和状态信息。
/// </summary>
public class LanguageInfoDto
{
    /// <summary>语言代码（ISO 格式，如 zh-CN、en-US、ja-JP）</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>语言的显示名称（从翻译文件读取 "Common.LanguageDisplayName"）</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>是否为当前激活的语言</summary>
    [JsonPropertyName("isCurrent")]
    public bool IsCurrent { get; set; }
}
