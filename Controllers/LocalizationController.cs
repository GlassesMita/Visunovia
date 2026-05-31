using Microsoft.AspNetCore.Mvc;
using Visunovia.Controllers.Models;
using Visunovia.Services.Localization;

namespace Visunovia.Controllers;

/// <summary>
/// 本地化管理 API 控制器，提供语言列表查询、翻译获取和语言切换功能。
/// 通过 LocalizationService 实现完整的国际化（i18n）支持，
/// 包括 PO 文件翻译查找、运行时语言切换和可用语言发现。
///
/// API 端点概览：
/// - GET /api/localization/languages - 获取可用语言列表（含 code、name、nativeName）
/// - GET /api/localization/translations?lang=zh-CN - 获取指定语言的全部翻译键值对
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
    /// 获取可用语言列表
    /// 返回所有已安装的语言包（PO 文件）及其元数据信息。
    /// 每个语言条目包含代码、通用名称和本地化名称，
    /// 前端可使用此接口构建语言选择器 UI
    /// </summary>
    /// <returns>包含语言列表的标准化 JSON 响应，格式为 { success: true, data: [{ code, name, nativeName }] }</returns>
    /// <response code="200">成功获取语言列表</response>
    [HttpGet("languages")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<List<LanguageInfoDto>>), 200)]
    public IActionResult GetAvailableLanguages()
    {
        try
        {
            var availableLanguages = _localizationService.GetAvailableLanguages();
            var languageInfos = availableLanguages
                .Select(code => BuildLanguageInfo(code))
                .ToList();

            return Ok(ApiResponseDto<List<LanguageInfoDto>>.SuccessResponse(languageInfos));
        }
        catch (Exception ex)
        {
            // 异常来源：扫描本地化目录或读取语言文件时发生错误
            // 处理方式：返回 500 错误并记录详细日志用于排查目录权限或文件系统问题
            _logger.LogError(ex, "[LocalizationController] 获取语言列表失败");
            return StatusCode(500, ApiResponseDto<List<LanguageInfoDto>>.ErrorResponse(
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
    /// 获取指定语言的全部翻译键值对
    /// 从 PO 文件中读取该语言的所有翻译条目，以字典形式返回。
    /// 适用于前端初始化时批量加载翻译数据，或翻译管理工具查看所有条目。
    /// </summary>
    /// <param name="lang">目标语言代码（如 zh-CN、en-US），若为空则使用当前语言</param>
    /// <returns>包含全部翻译键值对的标准化 JSON 响应</returns>
    /// <response code="200">成功获取翻译数据</response>
    /// <response code="400">语言代码参数无效</response>
    /// <response code="404">目标语言的 PO 文件不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("translations")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<AllTranslationsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<AllTranslationsResponseDto>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<AllTranslationsResponseDto>), 404)]
    [ProducesResponseType(typeof(ApiResponseDto<AllTranslationsResponseDto>), 500)]
    public IActionResult GetTranslations([FromQuery] string? lang = null)
    {
        try
        {
            // 确定目标语言：优先使用参数值，否则回退到当前活动语言
            var targetLanguage = !string.IsNullOrWhiteSpace(lang)
                ? lang.Trim()
                : _localizationService.CurrentLanguage;

            // 参数验证：检查语言代码是否有效
            if (string.IsNullOrWhiteSpace(targetLanguage))
            {
                return BadRequest(ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                    "INVALID_LANGUAGE", "语言代码不能为空"));
            }

            // 验证目标语言是否可用
            if (!_localizationService.IsLanguageAvailable(targetLanguage))
            {
                var availableLanguages = string.Join(", ", _localizationService.GetAvailableLanguages());
                return NotFound(ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                    "LANGUAGE_NOT_FOUND",
                    $"语言 '{targetLanguage}' 不支持。可用语言: {availableLanguages}"));
            }

            try
            {
                // 加载指定语言的 PO 文件
                var poFile = _localizationService.LoadLanguage(targetLanguage);
                if (poFile == null)
                {
                    // 异常来源：PO 文件存在但加载失败（可能是格式错误或 I/O 问题）
                    // 处理方式：返回 404 错误并提示用户检查文件
                    _logger.LogWarning("[LocalizationController] 无法加载语言 {Language} 的 PO 文件", targetLanguage);
                    return NotFound(ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                        "LANGUAGE_LOAD_FAILED",
                        $"无法加载语言 '{targetLanguage}' 的翻译文件"));
                }

                // 提取所有翻译条目（跳过 msgId 为空的头部条目）
                // 键转换为小写以匹配前端使用的 key 格式（如 "app.title" 对应 PO 中的 "App.Title"）
                var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in poFile.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.MsgId) && entry.HasTranslation)
                    {
                        translations[entry.MsgId.ToLowerInvariant()] = entry.MsgStr;
                    }
                }

                var response = new AllTranslationsResponseDto
                {
                    Language = targetLanguage,
                    Translations = translations
                };

                _logger.LogInformation(
                    "[LocalizationController] 获取翻译成功: {Language}, {Count} 条",
                    targetLanguage, translations.Count);

                return Ok(ApiResponseDto<AllTranslationsResponseDto>.SuccessResponse(
                    response, $"成功获取 {translations.Count} 条翻译"));
            }
            catch (InvalidOperationException ex)
            {
                // 异常来源：PO 文件加载失败（由 LocalizationService 抛出）
                // 处理方式：返回 404 错误并附带具体原因
                _logger.LogWarning(ex, "[LocalizationController] 加载翻译失败: {Language}", targetLanguage);
                return NotFound(ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                    "LANGUAGE_LOAD_ERROR", ex.Message));
            }
        }
        catch (ArgumentException ex)
        {
            // 异常来源：语言代码格式无效（如包含非法字符）
            // 处理方式：返回 400 错误并提示正确的格式要求
            _logger.LogWarning(ex, "[LocalizationController] 语言代码格式无效");
            return BadRequest(ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                "INVALID_FORMAT", $"语言代码格式无效: {ex.Message}"));
        }
        catch (Exception ex)
        {
            // 异常来源：意外的异常情况（如内存不足、并发问题等）
            // 处理方式：返回 500 错误并记录完整堆栈信息用于排查
            _logger.LogError(ex, "[LocalizationController] 获取翻译时发生意外错误: {Language}", lang);
            return StatusCode(500, ApiResponseDto<AllTranslationsResponseDto>.ErrorResponse(
                "INTERNAL_ERROR", "获取翻译时发生内部错误"));
        }
    }

    /// <summary>
    /// 获取当前语言的单条翻译（简化版，直接返回翻译字符串）
    /// 前端每次需要翻译时调用此接口，后端从 PO 文件中查找并返回 msgstr。
    /// 若找不到翻译则返回原始 msgId 作为回退。
    /// 此接口设计为轻量级、高频调用的前端翻译入口，
    /// 前端应自行缓存翻译结果以减少请求次数。
    /// </summary>
    /// <param name="msgId">要翻译的键（如 "app.title"、"common.ok"）</param>
    /// <returns>纯文本翻译结果，Content-Type: text/plain</returns>
    /// <response code="200">成功返回翻译结果（纯文本）</response>
    /// <response code="400">msgId 参数缺失或为空</response>
    [HttpGet("currentLang")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    public IActionResult GetCurrentLangTranslation([FromQuery] string msgId)
    {
        if (string.IsNullOrWhiteSpace(msgId))
        {
            return BadRequest("msgId is required");
        }

        try
        {
            var translation = _localizationService.GetString(msgId.Trim());
            return Content(translation, "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalizationController] 翻译查询失败: {MsgId}", msgId);
            // 回退：返回原始 msgId
            return Content(msgId, "text/plain; charset=utf-8");
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

    /// <summary>
    /// 获取指定语言的嵌套格式翻译消息（vue-i18n 兼容格式）
    /// 将 PO 文件中的扁平键（如 "App.Title"、"Common.Ok"）转换为嵌套对象格式
    /// { app: { title: "..." }, common: { ok: "..." } }，供前端 vue-i18n 直接使用。
    /// 前端在初始化时调用此接口，将返回的消息合并到 vue-i18n 的 messages 中，
    /// 使得所有通过 useI18n() 获取的 t() 函数都能使用后端 PO 文件的翻译。
    /// </summary>
    /// <param name="lang">目标语言代码（如 zh-CN、en-US），若为空则使用当前语言</param>
    /// <returns>嵌套格式的翻译消息对象</returns>
    /// <response code="200">成功获取嵌套翻译消息</response>
    /// <response code="404">目标语言的 PO 文件不存在</response>
    [HttpGet("messages")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<Dictionary<string, object>>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<Dictionary<string, object>>), 404)]
    public IActionResult GetMessages([FromQuery] string? lang = null)
    {
        try
        {
            var targetLanguage = !string.IsNullOrWhiteSpace(lang)
                ? lang.Trim()
                : _localizationService.CurrentLanguage;

            if (string.IsNullOrWhiteSpace(targetLanguage))
            {
                return BadRequest(ApiResponseDto<Dictionary<string, object>>.ErrorResponse(
                    "INVALID_LANGUAGE", "语言代码不能为空"));
            }

            if (!_localizationService.IsLanguageAvailable(targetLanguage))
            {
                return NotFound(ApiResponseDto<Dictionary<string, object>>.ErrorResponse(
                    "LANGUAGE_NOT_FOUND", $"语言 '{targetLanguage}' 不支持"));
            }

            var poFile = _localizationService.LoadLanguage(targetLanguage);
            if (poFile == null)
            {
                return NotFound(ApiResponseDto<Dictionary<string, object>>.ErrorResponse(
                    "LANGUAGE_LOAD_FAILED", $"无法加载语言 '{targetLanguage}' 的翻译文件"));
            }

            // 将扁平键转换为嵌套对象
            // 例如 "App.Title" -> { app: { title: "..." } }
            // 例如 "Common.Ok" -> { common: { ok: "..." } }
            var messages = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in poFile.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.MsgId) || !entry.HasTranslation)
                    continue;

                var key = entry.MsgId;
                var value = entry.MsgStr;

                // 跳过头部条目
                if (key == "")
                    continue;

                // 将 "App.Title" 转换为嵌套路径 ["App", "Title"]
                var parts = key.Split('.');
                if (parts.Length == 1)
                {
                    // 无嵌套，直接作为顶级键（转为小写）
                    messages[key.ToLowerInvariant()] = value;
                }
                else
                {
                    // 构建嵌套对象
                    var current = (IDictionary<string, object>)messages;
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        var partKey = ToCamelCase(parts[i]);
                        if (!current.ContainsKey(partKey))
                        {
                            current[partKey] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        }
                        current = (IDictionary<string, object>)current[partKey];
                    }
                    var lastKey = ToCamelCase(parts[parts.Length - 1]);
                    current[lastKey] = value;
                }
            }

            _logger.LogInformation(
                "[LocalizationController] 获取嵌套翻译消息: {Language}, {Count} 条",
                targetLanguage, poFile.Entries.Count(e => !string.IsNullOrWhiteSpace(e.MsgId) && e.HasTranslation));

            return Ok(ApiResponseDto<Dictionary<string, object>>.SuccessResponse(messages));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalizationController] 获取嵌套翻译消息失败: {Language}", lang);
            return StatusCode(500, ApiResponseDto<Dictionary<string, object>>.ErrorResponse(
                "INTERNAL_ERROR", "获取翻译消息时发生内部错误"));
        }
    }

    /// <summary>
    /// 将 PascalCase 键名转换为 camelCase（vue-i18n 约定）
    /// 例如 "App" -> "app", "Title" -> "title", "OK" -> "ok"
    /// </summary>
    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;
        if (key.Length == 1)
            return key.ToLowerInvariant();
        return char.ToLowerInvariant(key[0]) + key.Substring(1);
    }

    #region 私有辅助方法

    /// <summary>
    /// 根据语言代码构建语言信息 DTO 对象。
    /// 从预定义映射表或 LocalizationService 获取显示名称，
    /// 并标记是否为当前激活的语言。
    /// </summary>
    /// <param name="languageCode">语言代码（如 zh-CN、en-US）</param>
    /// <returns>包含完整信息的 LanguageInfoDto 实例</returns>
    private LanguageInfoDto BuildLanguageInfo(string languageCode)
    {
        // 从 PO 文件的 Common.LanguageDisplayName 条目读取语言名称
        // 每个 PO 文件第 19-21 行内置了该语言的本地化名称
        var displayName = _localizationService.GetLanguageDisplayName(languageCode);

        return new LanguageInfoDto
        {
            Code = languageCode,
            DisplayName = displayName,
            IsCurrent = string.Equals(languageCode, _localizationService.CurrentLanguage, StringComparison.OrdinalIgnoreCase)
        };
    }

    #endregion
}
