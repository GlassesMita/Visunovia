using Microsoft.AspNetCore.Mvc;
using Visunovia.Controllers.Models;
using Visunovia.Services.Localization;

namespace Visunovia.Controllers;

/// <summary>
/// 本地化管理 API 控制器，提供语言切换、翻译查询和语言列表功能。
/// 通过 LocalizationService 实现完整的国际化（i18n）支持，
/// 包括 PO 文件翻译查找、运行时语言切换和可用语言发现。
///
/// API 端点概览：
/// - GET /api/localization/languages - 获取可用语言列表
/// - POST /api/localization/language - 切换当前语言
/// - GET /api/localization/translate - 查询单条翻译
/// - POST /api/localization/translate/batch - 批量查询翻译
/// </summary>
[ApiController]
[Route("api/localization")]
public class LocalizationController : ControllerBase
{
    private readonly LocalizationService _localizationService;
    private readonly ILogger<LocalizationController> _logger;

    /// <summary>
    /// 初始化 LocalizationController 实例
    /// </summary>
    /// <param name="localizationService">本地化服务（通过依赖注入提供）</param>
    /// <param name="logger">日志记录器</param>
    public LocalizationController(
        LocalizationService localizationService,
        ILogger<LocalizationController> logger)
    {
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取可用语言列表及当前语言信息
    /// 返回所有已安装的语言包（PO 文件）、当前活动语言和回退语言信息
    /// 前端可使用此接口构建语言选择器 UI
    /// </summary>
    /// <returns>包含语言信息的标准化 JSON 响应</returns>
    /// <response code="200">成功获取语言列表</response>
    [HttpGet("languages")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<LanguagesResponseDto>), 200)]
    public IActionResult GetAvailableLanguages()
    {
        try
        {
            var availableLanguages = _localizationService.GetAvailableLanguages();

            var response = new LanguagesResponseDto
            {
                CurrentLanguage = _localizationService.CurrentLanguage,
                AvailableLanguages = availableLanguages.ToList(),
                FallbackLanguage = _localizationService.FallbackLanguage
            };

            return Ok(ApiResponseDto<LanguagesResponseDto>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            // 异常来源：扫描本地化目录或读取语言文件时发生错误
            // 处理方式：返回 500 错误并记录详细日志用于排查目录权限或文件系统问题
            _logger.LogError(ex, "[LocalizationController] 获取语言列表失败");
            return StatusCode(500, ApiResponseDto<LanguagesResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "获取语言列表时发生内部错误"));
        }
    }

    /// <summary>
    /// 切换当前应用程序语言
    /// 执行完整的语言切换流程：验证 → 加载 PO 文件 → 更新状态 → 持久化偏好 → 触发事件
    /// 切换后所有后续的翻译请求将使用新语言
    /// </summary>
    /// <param name="request">包含目标语言代码的请求体</param>
    /// <returns>操作结果的标准化 JSON 响应，包含切换前后的语言代码</returns>
    /// <response code="200">语言切换成功</response>
    /// <response code="400">请求参数无效（语言代码格式错误或为空）</response>
    /// <response code="404">目标语言的 PO 文件不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("language")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<LanguageChangeResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<LanguageChangeResponseDto>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<LanguageChangeResponseDto>), 404)]
    [ProducesResponseType(typeof(ApiResponseDto<LanguageChangeResponseDto>), 500)]
    public IActionResult SetCurrentLanguage([FromBody] SetLanguageRequestDto request)
    {
        try
        {
            // 参数验证：检查请求体是否有效
            if (request == null || string.IsNullOrWhiteSpace(request.Language))
            {
                return BadRequest(ApiResponseDto<LanguageChangeResponseDto>.ErrorResponse(
                    "INVALID_LANGUAGE", "语言代码不能为空"));
            }

            var targetLanguage = request.Language.Trim();
            var previousLanguage = _localizationService.CurrentLanguage;

            // 验证目标语言是否可用
            if (!_localizationService.IsLanguageAvailable(targetLanguage))
            {
                var availableLanguages = string.Join(", ", _localizationService.GetAvailableLanguages());
                return NotFound(ApiResponseDto<LanguageChangeResponseDto>.ErrorResponse(
                    "LANGUAGE_NOT_FOUND",
                    $"语言 '{targetLanguage}' 不支持。可用语言: {availableLanguages}"));
            }

            try
            {
                // 执行语言切换
                _localizationService.SetCurrentLanguage(targetLanguage);

                _logger.LogInformation("[LocalizationController] 语言切换成功: {Previous} -> {New}",
                    previousLanguage, targetLanguage);
            }
            catch (InvalidOperationException ex)
            {
                // 异常来源：PO 文件加载失败或语言验证不通过（由 LocalizationService 抛出）
                // 处理方式：返回 404 错误并附带具体原因
                _logger.LogWarning(ex, "[LocalizationController] 语言切换失败: {Language}", targetLanguage);
                return NotFound(ApiResponseDto<LanguageChangeResponseDto>.ErrorResponse(
                    "LANGUAGE_LOAD_ERROR", ex.Message));
            }

            var response = new LanguageChangeResponseDto
            {
                Success = true,
                PreviousLanguage = previousLanguage,
                NewLanguage = targetLanguage
            };

            return Ok(ApiResponseDto<LanguageChangeResponseDto>.SuccessResponse(
                response, $"语言已从 '{previousLanguage}' 切换到 '{targetLanguage}'"));
        }
        catch (ArgumentException ex)
        {
            // 异常来源：语言代码格式无效（如包含非法字符）
            // 处理方式：返回 400 错误并提示正确的格式要求
            _logger.LogWarning(ex, "[LocalizationController] 语言代码格式无效");
            return BadRequest(ApiResponseDto<LanguageChangeResponseDto>.ErrorResponse(
                "INVALID_FORMAT", $"语言代码格式无效: {ex.Message}"));
        }
        catch (Exception ex)
        {
            // 异常来源：意外的异常情况（如持久化失败、事件触发异常等）
            // 处理方式：返回 500 错误并记录完整堆栈信息
            _logger.LogError(ex, "[LocalizationController] 切换语言时发生意外错误");
            return StatusCode(500, ApiResponseDto<LanguageChangeResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "切换语言时发生内部错误"));
        }
    }

    /// <summary>
    /// 查询单条文本的翻译
    /// 根据当前活动的语言返回对应 PO 文件中的翻译结果。
    /// 若找不到翻译则按回退机制处理：当前语言 → 回退语言 → 返回原始文本
    /// 此接口适用于前端动态获取少量翻译字符串的场景
    /// </summary>
    /// <param name="msgId">要翻译的原始文本（msgid）</param>
    /// <param name="context">可选的上下文标识符，用于消歧义相同 msgId 的不同含义</param>
    /// <returns>包含翻译结果的标准化 JSON 响应</returns>
    /// <response code="200">成功返回翻译结果</response>
    /// <response code="400">msgId 参数缺失或为空</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("translate")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<TranslationResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<TranslationResponseDto>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<TranslationResponseDto>), 500)]
    public IActionResult Translate([FromQuery] string msgId, [FromQuery] string? context = null)
    {
        try
        {
            // 参数验证：检查 msgId 是否有效
            if (string.IsNullOrWhiteSpace(msgId))
            {
                return BadRequest(ApiResponseDto<TranslationResponseDto>.ErrorResponse(
                    "INVALID_MSGID", "消息标识符 (msgId) 不能为空"));
            }

            // 执行翻译查询
            var translation = _localizationService.GetString(msgId, context);

            var response = new TranslationResponseDto
            {
                MsgId = msgId,
                Translation = translation,
                Language = _localizationService.CurrentLanguage
            };

            return Ok(ApiResponseDto<TranslationResponseDto>.SuccessResponse(response));
        }
        catch (ArgumentException ex)
        {
            // 异常来源：msgId 参数验证失败（通常不应到达此处，因为已在上方检查）
            // 处理方式：返回 400 错误并提示用户检查输入
            _logger.LogWarning(ex, "[LocalizationController] 翻译查询参数无效: {MsgId}", msgId);
            return BadRequest(ApiResponseDto<TranslationResponseDto>.ErrorResponse(
                "INVALID_PARAMETER", ex.Message));
        }
        catch (Exception ex)
        {
            // 异常来源：翻译查找过程中发生意外错误（如 PO 文件损坏、缓存问题等）
            // 处理方式：返回 500 错误并记录详细信息用于排查
            _logger.LogError(ex, "[LocalizationController] 翻译查询失败: {MsgId}", msgId);
            return StatusCode(500, ApiResponseDto<TranslationResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "查询翻译时发生内部错误"));
        }
    }

    /// <summary>
    /// 批量查询多条文本的翻译
    /// 允许在一次请求中同时查询多个 msgId 的翻译结果，显著减少网络往返次数。
    /// 适用于页面初始化时批量加载所有需要的翻译字符串的场景
    /// </summary>
    /// <param name="request">包含多个 msgId 的请求体</param>
    /// <returns>包含翻译结果映射的标准化 JSON 响应</returns>
    /// <response code="200">批量翻译查询成功</response>
    /// <response code="400">请求参数无效或 msgIds 列表为空</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("translate/batch")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<BatchTranslationResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<BatchTranslationResponseDto>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<BatchTranslationResponseDto>), 500)]
    public IActionResult BatchTranslate([FromBody] BatchTranslateRequestDto request)
    {
        try
        {
            // 参数验证：检查请求体是否有效
            if (request?.MsgIds == null || request.MsgIds.Count == 0)
            {
                return BadRequest(ApiResponseDto<BatchTranslationResponseDto>.ErrorResponse(
                    "INVALID_REQUEST", "请提供至少一个消息标识符 (msgId)"));
            }

            // 参数验证：限制单次请求数量以防止滥用
            const int maxBatchSize = 100;
            if (request.MsgIds.Count > maxBatchSize)
            {
                return BadRequest(ApiResponseDto<BatchTranslationResponseDto>.ErrorResponse(
                    "BATCH_TOO_LARGE",
                    $"单次请求最多支持 {maxBatchSize} 条翻译，当前数量: {request.MsgIds.Count}"));
            }

            var translations = new Dictionary<string, string>();

            // 遍历所有要翻译的消息 ID
            foreach (var msgId in request.MsgIds)
            {
                if (string.IsNullOrWhiteSpace(msgId))
                {
                    continue; // 跳过空值
                }

                try
                {
                    // 查询单条翻译并添加到结果字典
                    var translation = _localizationService.GetString(msgId);
                    translations[msgId] = translation;
                }
                catch (Exception ex)
                {
                    // 异常来源：单个 msgId 翻译失败（可能是 PO 文件中不存在该条目）
                    // 处理方式：使用原始 msgId 作为回退值，确保前端不会因缺少某条翻译而报错
                    _logger.LogDebug(ex, "[LocalizationController] 批量翻译中跳过: {MsgId}", msgId);
                    translations[msgId] = msgId; // 回退到原始文本
                }
            }

            _logger.LogDebug("[LocalizationController] 批量翻译完成: {Count}/{Total} 条",
                translations.Count, request.MsgIds.Count);

            var response = new BatchTranslationResponseDto
            {
                Translations = translations,
                Language = _localizationService.CurrentLanguage
            };

            return Ok(ApiResponseDto<BatchTranslationResponseDto>.SuccessResponse(
                response, $"成功翻译 {translations.Count}/{request.MsgIds.Count} 条"));
        }
        catch (Exception ex)
        {
            // 异常来源：批量翻译过程中发生未预期的异常
            // 处理方式：返回 500 错误并记录完整上下文信息
            _logger.LogError(ex, "[LocalizationController] 批量翻译失败");
            return StatusCode(500, ApiResponseDto<BatchTranslationResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "批量翻译时发生内部错误"));
        }
    }
}
