using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Visunovia.Controllers.Models;
using Visunovia.Services;
using Visunovia.Services.Configuration;
using Visunovia.Services.Localization;

namespace Visunovia.Controllers;

/// <summary>
/// 安装向导 API 控制器，提供系统语言检测、首次运行状态管理等功能。
/// </summary>
[ApiController]
[Route("api/setup")]
public class SetupWizardController : ControllerBase
{
    private readonly LocalizationService _localizationService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<SetupWizardController> _logger;

    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en-US", "zh-CN", "zh-TW", "ja-JP"
    };

    public SetupWizardController(
        LocalizationService localizationService,
        SettingsService settingsService,
        ILogger<SetupWizardController> logger)
    {
        _localizationService = localizationService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// 检查是否需要显示安装向导。
    /// 当 isFirstRun 为 true 或不存在时返回 true。
    /// </summary>
    [HttpGet("isFirstRun")]
    [Produces("application/json")]
    public IActionResult IsFirstRun()
    {
        try
        {
            var value = _settingsService.GetRawValue(DefaultSettings.IsFirstRunKey);
            bool isFirstRun = string.IsNullOrEmpty(value) || bool.TryParse(value, out bool parsed) && parsed;

            return Ok(ApiResponseDto<bool>.SuccessResponse(isFirstRun));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SetupWizard] 检查首次运行状态失败");
            // 出错时默认显示向导
            return Ok(ApiResponseDto<bool>.SuccessResponse(true));
        }
    }

    /// <summary>
    /// 检测系统语言并返回最佳匹配的可用语言。
    /// 优先匹配系统语言，回退到 en-US，无语言文件时返回 null。
    /// </summary>
    [HttpGet("detectLanguage")]
    [Produces("application/json")]
    public IActionResult DetectLanguage()
    {
        try
        {
            var availableLanguages = _localizationService.GetAvailableLanguages().ToList();

            if (availableLanguages.Count == 0)
            {
                _logger.LogWarning("[SetupWizard] 未找到任何 PO 语言文件");
                return Ok(ApiResponseDto<SetupLanguageDetectionResultDto>.SuccessResponse(
                    new SetupLanguageDetectionResultDto
                    {
                        DetectedLanguage = null,
                        SystemLanguage = null,
                        FallbackUsed = true,
                        AvailableLanguages = new List<string>(),
                        NoFiles = true
                    }));
            }

            // 获取系统语言
            var systemCulture = CultureInfo.CurrentUICulture;
            var systemLangCode = systemCulture.Name; // e.g. "zh-CN", "en-US"

            // 尝试精确匹配
            var match = availableLanguages.FirstOrDefault(l =>
                string.Equals(l, systemLangCode, StringComparison.OrdinalIgnoreCase));

            // 尝试两字母语言代码匹配 (e.g. "zh" matches "zh-CN")
            if (match == null)
            {
                var twoLetter = systemCulture.TwoLetterISOLanguageName; // e.g. "zh", "en", "ja"
                match = availableLanguages.FirstOrDefault(l =>
                    l.StartsWith(twoLetter, StringComparison.OrdinalIgnoreCase));
            }

            bool fallbackUsed = false;
            if (match == null)
            {
                // 回退到 en-US
                match = availableLanguages.FirstOrDefault(l =>
                    string.Equals(l, "en-US", StringComparison.OrdinalIgnoreCase));
                fallbackUsed = true;
            }

            return Ok(ApiResponseDto<SetupLanguageDetectionResultDto>.SuccessResponse(
                new SetupLanguageDetectionResultDto
                {
                    DetectedLanguage = match ?? "en-US",
                    SystemLanguage = systemLangCode,
                    FallbackUsed = fallbackUsed,
                    AvailableLanguages = availableLanguages,
                    NoFiles = false
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SetupWizard] 语言检测失败");
            return StatusCode(500, ApiResponseDto<SetupLanguageDetectionResultDto>.ErrorResponse(
                "INTERNAL_ERROR", "语言检测时发生内部错误"));
        }
    }

    /// <summary>
    /// 完成安装向导，保存用户选择并标记 isFirstRun 为 false。
    /// </summary>
    [HttpPost("complete")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public IActionResult Complete([FromBody] SetupCompleteRequestDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponseDto<object>.ErrorResponse(
                    "INVALID_REQUEST", "请求体不能为空"));
            }

            // 保存语言设置
            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                _settingsService.SetAndSave(DefaultSettings.LanguageKey, request.Language);
                _localizationService.SetCurrentLanguage(request.Language);
            }

            // 保存主题设置
            if (!string.IsNullOrWhiteSpace(request.Theme))
            {
                _settingsService.SetAndSave(DefaultSettings.ThemeKey, request.Theme);
            }

            // 标记已完成首次运行向导
            _settingsService.SetAndSave(DefaultSettings.IsFirstRunKey, "false");

            _logger.LogInformation("[SetupWizard] 安装向导已完成，语言={Language}，主题={Theme}",
                request.Language, request.Theme);

            return Ok(ApiResponseDto<object>.SuccessResponse(new { success = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SetupWizard] 完成安装向导失败");
            return StatusCode(500, ApiResponseDto<object>.ErrorResponse(
                "INTERNAL_ERROR", "保存设置时发生内部错误"));
        }
    }
}

/// <summary>
/// 语言检测结果 DTO
/// </summary>
public class SetupLanguageDetectionResultDto
{
    public string? DetectedLanguage { get; set; }
    public string? SystemLanguage { get; set; }
    public bool FallbackUsed { get; set; }
    public List<string> AvailableLanguages { get; set; } = new();
    public bool NoFiles { get; set; }
}

/// <summary>
/// 安装向导完成请求 DTO
/// </summary>
public class SetupCompleteRequestDto
{
    public string? Language { get; set; }
    public string? Theme { get; set; }
}
