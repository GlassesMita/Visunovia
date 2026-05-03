using System.ComponentModel.DataAnnotations;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 更新单个设置项请求数据传输对象
/// </summary>
public class UpdateSettingRequestDto
{
    /// <summary>要设置的新值</summary>
    [Required(ErrorMessage = "值不能为空")]
    public object? Value { get; set; }
}
