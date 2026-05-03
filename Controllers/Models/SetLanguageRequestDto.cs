using System.ComponentModel.DataAnnotations;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 切换语言请求数据传输对象
/// </summary>
public class SetLanguageRequestDto
{
    /// <summary>目标语言代码（如 "zh-CN"、"en-US"）</summary>
    [Required(ErrorMessage = "语言代码不能为空")]
    [RegularExpression(@"^[a-zA-Z]{2,3}(-[a-zA-Z]{2,4})?$", ErrorMessage = "语言代码格式无效")]
    public string Language { get; set; } = string.Empty;
}
