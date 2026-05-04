using Microsoft.AspNetCore.Mvc;
using Visunovia.Controllers.Models;
using Visunovia.Services;
using Visunovia.Services.Configuration;
using Visunovia.Services.Localization;

namespace Visunovia.Controllers;

/// <summary>
/// 设置管理 API 控制器，提供应用程序配置的读取、更新、批量操作和重置功能。
/// 所有设置操作通过 SettingsService 统一管理，支持实时持久化到 XML 配置文件。
/// 保存设置后自动触发 LocalizationService 语言切换，实现即时生效。
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        SettingsService settingsService,
        LocalizationService localizationService,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有当前设置项
    /// 返回应用程序的所有配置键值对，包括语言、主题、编辑器字体大小等
    /// </summary>
    /// <returns>包含所有设置的标准化 JSON 响应</returns>
    /// <response code="200">成功获取设置列表</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<SettingsResponseDto>), 200)]
    public IActionResult GetAllSettings()
    {
        try
        {
            var settings = new Dictionary<string, object?>();

            // 遍历所有已注册的配置键并读取其值
            foreach (var key in DefaultSettings.GetAllKeys())
            {
                var value = _settingsService.GetRawValue(key);
                // 根据键名尝试转换为适当的类型
                settings[key] = GetTypedValue(key, value);
            }

            var response = new SettingsResponseDto
            {
                Settings = settings,
                IsRemoteSession = IsRemoteConnection()
            };

            return Ok(ApiResponseDto<SettingsResponseDto>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            // 异常来源：读取设置时发生意外错误（如配置文件损坏、类型转换失败等）
            // 处理方式：记录错误日志并返回 500 状态码，避免暴露内部细节
            _logger.LogError(ex, "[SettingsController] 获取设置列表失败");
            return StatusCode(500, ApiResponseDto<SettingsResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "获取设置列表时发生内部错误"));
        }
    }

    /// <summary>
    /// 批量更新所有设置项
    /// 接受完整设置对象，在一次请求中更新所有配置项并持久化
    /// </summary>
    /// <param name="request">包含多个键值对的请求体</param>
    /// <returns>操作结果的标准化 JSON 响应</returns>
    /// <response code="200">批量更新成功</response>
    /// <response code="400">请求参数无效或无有效设置项</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 500)]
    public IActionResult UpdateAllSettings([FromBody] BatchUpdateRequestDto request)
    {
        try
        {
            if (request?.Settings == null || request.Settings.Count == 0)
            {
                return BadRequest(ApiResponseDto<object>.ErrorResponse(
                    "INVALID_REQUEST", "请提供至少一个设置项"));
            }

            var allKeys = DefaultSettings.GetAllKeys();
            var updatedCount = 0;
            var invalidKeys = new List<string>();
            var oldLanguage = _settingsService.GetCurrentLanguage();
            var oldTheme = _settingsService.GetCurrentTheme();

            foreach (var kvp in request.Settings)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (string.IsNullOrWhiteSpace(key) ||
                    !allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    invalidKeys.Add(key);
                    continue;
                }

                try
                {
                    var stringValue = value?.ToString() ?? string.Empty;
                    _settingsService.Set<string>(key, stringValue);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    var skipMsg = string.Format(_localizationService.GetString("Console.InvalidSettingSkipped"), key, value);
                    _logger.LogWarning(ex, "[SettingsController] {Message}", skipMsg);
                }
            }

            if (updatedCount > 0)
            {
                _settingsService.Save();

                var newLanguage = _settingsService.GetCurrentLanguage();
                var newTheme = _settingsService.GetCurrentTheme();
                var languageChanged = !string.Equals(oldLanguage, newLanguage, StringComparison.OrdinalIgnoreCase);
                var themeChanged = !string.Equals(oldTheme, newTheme, StringComparison.OrdinalIgnoreCase);

                if (languageChanged)
                {
                    try
                    {
                        _localizationService.SetCurrentLanguage(newLanguage);
                        var switchMsg = string.Format(_localizationService.GetString("Console.LanguageSwitched"), oldLanguage, newLanguage);
                        _logger.LogInformation("[SettingsController] {Message}", switchMsg);
                    }
                    catch (Exception ex)
                    {
                        var failMsg = _localizationService.GetString("Console.LanguageSwitchFailed");
                        _logger.LogWarning(ex, "[SettingsController] {Message}", failMsg);
                    }
                }

                var completeMsg = string.Format(_localizationService.GetString("Console.BatchUpdateComplete"), updatedCount, request.Settings.Count);
                _logger.LogInformation("[SettingsController] {Message}", completeMsg);

                var result = new
                {
                    updatedCount,
                    invalidKeys,
                    languageChanged,
                    newLanguage = languageChanged ? newLanguage : null,
                    themeChanged
                };

                return Ok(ApiResponseDto<object>.SuccessResponse(result,
                    $"批量更新完成：{updatedCount} 项全部成功"));
            }

            var emptyResult = new { updatedCount = 0, invalidKeys, languageChanged = false, newLanguage = (string?)null, themeChanged = false };
            return Ok(ApiResponseDto<object>.SuccessResponse(emptyResult,
                $"批量更新完成：0 项成功，{invalidKeys.Count} 项被跳过"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsController] {Message}", _localizationService.GetString("Console.BatchUpdateFailed"));
            return StatusCode(500, ApiResponseDto<object>.ErrorResponse(
                "INTERNAL_ERROR", "批量更新设置时发生内部错误"));
        }
    }

    /// <summary>
    /// 更新单个设置项
    /// 通过指定键名修改对应的配置值，修改后自动保存到配置文件
    /// </summary>
    /// <param name="key">要更新的配置键名（如 "language"、"theme"、"editorFontSize"）</param>
    /// <param name="request">包含新值的请求体</param>
    /// <returns>操作结果的标准化 JSON 响应</returns>
    /// <response code="200">设置更新成功</response>
    /// <response code="400">请求参数无效（键名为空或值缺失）</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("{key}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<SettingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<SettingDto>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<SettingDto>), 500)]
    public IActionResult UpdateSetting(string key, [FromBody] UpdateSettingRequestDto request)
    {
        try
        {
            // 参数验证：检查键名是否有效
            if (string.IsNullOrWhiteSpace(key))
            {
                return BadRequest(ApiResponseDto<SettingDto>.ErrorResponse(
                    "INVALID_KEY", "配置键名不能为空"));
            }

            // 参数验证：检查请求体是否有效
            if (request == null || request.Value == null)
            {
                return BadRequest(ApiResponseDto<SettingDto>.ErrorResponse(
                    "INVALID_VALUE", "设置值不能为空"));
            }

            // 验证键名是否在已注册的配置列表中
            var allKeys = DefaultSettings.GetAllKeys();
            if (!allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponseDto<SettingDto>.ErrorResponse(
                    "UNKNOWN_KEY", $"未知的配置键: {key}。可用的键: {string.Join(", ", allKeys)}"));
            }

            // 使用 SetAndSave 实现原子化修改+持久化（替代 Set()+Save() 组合）
            // 设计决策：单个设置更新使用 SetAndSave 保证立即持久化，避免数据丢失风险
            var stringValue = request.Value.ToString() ?? string.Empty;
            bool saveSuccess = _settingsService.SetAndSave<string>(key, stringValue);

            // 创建统一的响应对象（在 if 之前声明一次，避免 CS0136 变量重复声明错误）
            var result = new SettingDto
            {
                Key = key,
                Value = request.Value,
                Type = request.Value.GetType().Name
            };

            if (!saveSuccess)
            {
                // 异常来源：保存失败但内存中已更新（可能存在 I/O 问题如磁盘空间不足、权限问题等）
                // 处理方式：记录警告日志，仍然返回成功给前端（因为内存中的值已更新）
                //         但在消息中提示用户可能的数据丢失风险
                _logger.LogWarning(
                    "[SettingsController] 设置 '{Key}' 已更新到内存但保存失败（可能存在 I/O 问题）",
                    key);

                return Ok(ApiResponseDto<SettingDto>.SuccessResponse(
                    result,
                    $"设置 '{key}' 已更新（注意：文件保存失败，重启后可能丢失）"));
            }

            _logger.LogInformation("[SettingsController] 设置已更新并持久化: {Key}={Value}", key, stringValue);

            return Ok(ApiResponseDto<SettingDto>.SuccessResponse(result, $"设置 '{key}' 已成功更新"));
        }
        catch (ArgumentException ex)
        {
            // 异常来源：参数验证失败（通常由 SettingsService 内部抛出）
            // 处理方式：返回 400 错误及具体原因
            _logger.LogWarning(ex, "[SettingsController] 更新设置参数无效: {Key}", key);
            return BadRequest(ApiResponseDto<SettingDto>.ErrorResponse(
                "INVALID_PARAMETER", ex.Message));
        }
        catch (Exception ex)
        {
            // 异常来源：保存过程中发生 I/O 错误或其他意外异常
            // 处理方式：返回 500 错误并记录详细日志
            _logger.LogError(ex, "[SettingsController] 更新设置失败: {Key}", key);
            return StatusCode(500, ApiResponseDto<SettingDto>.ErrorResponse(
                "INTERNAL_ERROR", $"更新设置 '{key}' 时发生内部错误"));
        }
    }

    /// <summary>
    /// 批量更新多个设置项
    /// 允许在一次请求中同时修改多个配置项，减少网络往返次数
    /// </summary>
    /// <param name="request">包含多个键值对的请求体</param>
    /// <returns>操作结果的标准化 JSON 响应，包含成功更新的数量</returns>
    /// <response code="200">批量更新成功</response>
    /// <response code="400">请求参数无效或无有效设置项</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("batch")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 500)]
    public IActionResult BatchUpdateSettings([FromBody] BatchUpdateRequestDto request)
    {
        try
        {
            // 参数验证：检查请求体是否有效
            if (request?.Settings == null || request.Settings.Count == 0)
            {
                return BadRequest(ApiResponseDto<object>.ErrorResponse(
                    "INVALID_REQUEST", "请提供至少一个设置项"));
            }

            // 设计决策说明：
            // 批量操作故意不使用 AutoSave/SetAndSave 以避免 N 次磁盘写入
            // 性能优化策略：
            // 1. 循环调用 Set()（仅内存写入，快速）
            // 2. 统一调用一次 Save()（单次磁盘 I/O）
            // 这种方式在保证数据一致性的同时最大化性能，特别适合大量设置的批量更新场景
            var allKeys = DefaultSettings.GetAllKeys();
            var updatedCount = 0;
            var invalidKeys = new List<string>();

            // 遍历所有要更新的设置项
            foreach (var kvp in request.Settings)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                // 跳过无效的键名
                if (string.IsNullOrWhiteSpace(key) ||
                    !allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    invalidKeys.Add(key);
                    continue;
                }

                try
                {
                    // 执行单个设置项的更新
                    var stringValue = value?.ToString() ?? string.Empty;
                    _settingsService.Set<string>(key, stringValue);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    // 异常来源：单个设置项更新失败（可能是类型转换问题）
                    // 处理方式：记录警告但继续处理其他项目，不中断整个批量操作
                    _logger.LogWarning(ex, "[SettingsController] 批量更新中跳过无效项: {Key}={Value}",
                        key, value);
                }
            }

            // 如果有成功的更新则保存
            if (updatedCount > 0)
            {
                _settingsService.Save();
                _logger.LogInformation("[SettingsController] 批量更新完成: 成功 {UpdatedCount}/{TotalCount} 项",
                    updatedCount, request.Settings.Count);
            }

            var result = new { updatedCount, invalidKeys };
            var message = invalidKeys.Count > 0
                ? $"批量更新完成：{updatedCount} 项成功，{invalidKeys.Count} 项被跳过"
                : $"批量更新完成：{updatedCount} 项全部成功";

            return Ok(ApiResponseDto<object>.SuccessResponse(result, message));
        }
        catch (Exception ex)
        {
            // 异常来源：保存过程中发生错误或意外的异常情况
            // 处理方式：返回 500 错误并记录完整堆栈信息用于排查
            _logger.LogError(ex, "[SettingsController] 批量更新设置失败");
            return StatusCode(500, ApiResponseDto<object>.ErrorResponse(
                "INTERNAL_ERROR", "批量更新设置时发生内部错误"));
        }
    }

    /// <summary>
    /// 重置所有设置为默认值
    /// 将所有配置项恢复为 DefaultSettings 中定义的初始值
    /// 注意：此操作不可逆，执行后立即生效并持久化
    /// </summary>
    /// <returns>操作结果的标准化 JSON 响应</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("reset")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 500)]
    public IActionResult ResetSettings()
    {
        try
        {
            // 执行重置操作
            _settingsService.Reset();
            _settingsService.Save();

            _logger.LogWarning("[SettingsController] 所有设置已重置为默认值并持久化到配置文件");

            return Ok(ApiResponseDto<object>.SuccessResponse(
                new { resetAt = DateTime.UtcNow.ToString("o") },
                "所有设置已成功重置为默认值"));
        }
        catch (Exception ex)
        {
            // 异常来源：重置或保存过程中发生错误（如权限不足、磁盘空间不足等）
            // 处理方式：返回 500 错误并提示用户检查系统状态
            _logger.LogError(ex, "[SettingsController] 重置设置失败");
            return StatusCode(500, ApiResponseDto<object>.ErrorResponse(
                "INTERNAL_ERROR", "重置设置时发生内部错误，请检查系统权限和磁盘空间"));
        }
    }

    #region 私有辅助方法

    /// <summary>
    /// 根据配置键名将字符串值转换为适当的 .NET 类型
    /// 确保前端接收到的值具有正确的数据类型而非纯字符串
    /// </summary>
    /// <param name="key">配置键名</param>
    /// <param name="value">原始字符串值</param>
    /// <returns>转换后的强类型值</returns>
    private static object? GetTypedValue(string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        // 根据已知的配置键进行类型推断
        return key switch
        {
            DefaultSettings.EditorFontSizeKey when int.TryParse(value, out int fontSize) => fontSize,
            DefaultSettings.AutoSaveIntervalKey when int.TryParse(value, out int interval) => interval,
            DefaultSettings.RecentProjectsLimitKey when int.TryParse(value, out int limit) => limit,
            DefaultSettings.PreviewWidthKey when int.TryParse(value, out int width) => width,
            DefaultSettings.PreviewHeightKey when int.TryParse(value, out int height) => height,
            DefaultSettings.AllowRemoteSessionKey when bool.TryParse(value, out bool allowRemote) => allowRemote,
            _ => value // 默认返回字符串
        };
    }

    #endregion

    /// <summary>
    /// 判断当前 HTTP 连接是否为远程会话。
    /// 通过检查客户端 IP 是否为回环地址（127.0.0.1 或 ::1）来判断，
    /// 非回环地址视为远程连接。
    /// </summary>
    /// <returns>如果客户端 IP 不是回环地址则返回 true</returns>
    private bool IsRemoteConnection()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp == null) return false;
        return !System.Net.IPAddress.IsLoopback(remoteIp);
    }
}
