using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Visunovia.Services.Configuration;

namespace Visunovia.Services.Localization;

/// <summary>
/// 核心本地化服务，负责 PO 文件管理、翻译查找和语言切换。
/// 作为应用程序本地化系统的中心枢纽，提供完整的国际化（i18n）支持。
///
/// 核心功能：
/// - PO 文件的加载、缓存和热重载
/// - 翻译字符串的查找（支持上下文和复数形式）
/// - 语言回退机制（当前语言 → 回退语言 → 原始字符串）
/// - 运行时动态语言切换
/// - 可用语言列表的自动发现
///
/// 线程安全：
/// - 使用 ConcurrentDictionary 缓存已加载的语言文件
/// - 使用 lock 保护语言切换等状态变更操作
/// - 所有公共方法均可从多线程安全调用
///
/// 使用示例：
/// <code>
/// // 通过 DI 注入使用
/// public class MyController : ControllerBase
/// {
///     private readonly LocalizationService _localization;
///
///     public MyController(LocalizationService localization)
///     {
///         _localization = localization;
///     }
///
///     public string GetGreeting()
///     {
///         return _localization.GetString("Hello, World!");
///     }
/// }
/// </code>
/// </summary>
public class LocalizationService : IDisposable
{
    #region 事件定义

    /// <summary>
    /// 语言切换事件。当 CurrentLanguage 属性被修改时触发。
    /// 订阅者可在此事件中更新 UI、刷新缓存或执行其他语言相关操作。
    /// </summary>
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <summary>
    /// 翻译缓存失效事件。当缓存被清除或 PO 文件被重新加载时触发。
    /// 订阅者可根据此事件决定是否需要重建自己的缓存数据。
    /// </summary>
    public event EventHandler? TranslationCacheInvalidated;

    #endregion

    #region 私有字段

    /// <summary>日志记录器实例</summary>
    private readonly ILogger<LocalizationService> _logger;

    /// <summary>依赖注入服务提供程序，用于延迟解析 SettingsService</summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>PO 文件基础目录路径</summary>
    private readonly string _baseDirectory;

    /// <summary>PO 解析器实例</summary>
    private readonly PoParser _parser;

    /// <summary>语言文件缓存：键为语言代码，值为对应的 PoFile 对象</summary>
    private readonly ConcurrentDictionary<string, PoFile> _loadedLanguages;

    /// <summary>当前活动语言代码</summary>
    private string _currentLanguage;

    /// <summary>回退语言代码（当目标语言找不到翻译时使用）</summary>
    private string _fallbackLanguage;

    /// <summary>对象锁，用于保护语言切换等状态变更操作</summary>
    private readonly object _syncLock = new();

    /// <summary>对象是否已被释放</summary>
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化 LocalizationService 实例。
    /// 自动从 SettingsService 读取默认语言设置，并初始化内部缓存和解析器。
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供程序，用于获取 SettingsService</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="baseDirectory">PO 文件基础目录，默认为应用根目录下的 "Localizations" 文件夹</param>
    /// <param name="defaultLanguage">默认语言代码，若未指定则从 SettingsService 读取或使用 "en-US"</param>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：serviceProvider 或 logger 参数为 null
    /// 处理方式：调用方应确保通过 DI 框架正确注入这些依赖项
    /// </exception>
    public LocalizationService(
        IServiceProvider serviceProvider,
        ILogger<LocalizationService> logger,
        string? baseDirectory = null,
        string? defaultLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _logger = logger;
        _baseDirectory = baseDirectory ?? Path.Combine(AppContext.BaseDirectory, "Localizations");
        _parser = new PoParser();
        _loadedLanguages = new ConcurrentDictionary<string, PoFile>(StringComparer.OrdinalIgnoreCase);

        // 从 SettingsService 读取默认语言或使用参数指定值/硬编码默认值
        _fallbackLanguage = "en-US";
        _currentLanguage = ResolveDefaultLanguage(defaultLanguage);

        _logger.LogInformation(
            "[LocalizationService] 初始化完成: 基础目录={BaseDirectory}, 当前语言={CurrentLanguage}, 回退语言={FallbackLanguage}",
            _baseDirectory, _currentLanguage, _fallbackLanguage);
    }

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取或设置当前活动的语言代码。
    /// 设置此属性会触发语言切换流程（验证、加载、事件通知）。
    /// 推荐使用 SetCurrentLanguage() 方法以获得更完善的错误处理和日志记录。
    /// </summary>
    /// <exception cref="ArgumentException">
    /// 异常来源：设置为空白的语言代码
    /// 处理方式：调用方应确保传入有效的语言标识符
    /// </exception>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetCurrentLanguage(value);
    }

    /// <summary>
    /// 获取或设置回退语言代码。
    /// 当在当前语言中找不到翻译时，会自动尝试在回退语言中查找。
    /// 通常设置为源语言（如 "en-US"），以确保至少能显示原始文本。
    /// </summary>
    public string FallbackLanguage
    {
        get => _fallbackLanguage;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _fallbackLanguage = value;
            _logger.LogDebug("[LocalizationService] 回退语言已更新: {FallbackLanguage}", value);
        }
    }

    /// <summary>
    /// 获取 PO 文件的基础目录路径。
    /// 所有语言的 .po 文件都应存放在此目录下。
    /// </summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>
    /// 获取当前已加载并缓存的语言代码集合。
    /// 仅包含成功加载过的语言，不代表目录中所有可用语言。
    /// </summary>
    public IReadOnlyCollection<string> LoadedLanguages => _loadedLanguages.Keys.ToList().AsReadOnly();

    /// <summary>
    /// 获取所有可用（存在 PO 文件）的语言代码列表。
    /// 通过扫描 BaseDirectory 目录下的 .po 文件自动发现。
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages => GetAvailableLanguages();

    #endregion

    #region PO 文件加载

    /// <summary>
    /// 加载指定语言的 PO 文件到内存缓存中。
    /// 若该语言已经缓存且 forceReload 为 false，则直接返回缓存的版本。
    /// 支持热重载：当 forceReload 为 true 时会强制重新读取文件。
    /// </summary>
    /// <param name="languageCode">要加载的语言代码（如 "zh-CN"）</param>
    /// <param name="forceReload">是否强制重新加载（忽略已有缓存）</param>
    /// <returns>加载完成的 PoFile 对象；若加载失败则返回 null</returns>
    /// <exception cref="ArgumentException">
    /// 异常来源：languageCode 参数为空或空白
    /// 处理方式：调用方应确保传入有效的 ISO 语言代码
    /// </exception>
    public PoFile? LoadLanguage(string languageCode, bool forceReload = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var normalizedCode = NormalizeLanguageCode(languageCode);

        // 如果不需要强制重载且已缓存，直接返回
        if (!forceReload && _loadedLanguages.TryGetValue(normalizedCode, out var cached))
        {
            _logger.LogDebug("[LocalizationService] 从缓存返回语言: {Language}", normalizedCode);
            return cached;
        }

        return LoadLanguageFromFile(normalizedCode);
    }

    /// <summary>
    /// 从磁盘文件加载指定语言的 PO 文件。
    /// 内部方法，处理实际的文件读取和解析逻辑。
    /// </summary>
    private PoFile? LoadLanguageFromFile(string languageCode)
    {
        var filePath = GetPoFilePath(languageCode);

        try
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                _logger.LogWarning(
                    "[LocalizationService] PO 文件不存在: {FilePath}，将跳过语言 {Language}",
                    filePath, languageCode);
                return null;
            }

            // 执行解析
            var poFile = _parser.Parse(filePath);

            // 更新缓存
            _loadedLanguages.AddOrUpdate(languageCode, poFile, (_, _) => poFile);

            // 记录解析警告
            if (_parser.Warnings.Count > 0)
            {
                foreach (var warning in _parser.Warnings)
                {
                    _logger.LogWarning("[LocalizationService] PO 解析警告 ({Language}): {Warning}",
                        languageCode, warning);
                }
            }

            _logger.LogInformation(
                "[LocalizationService] 成功加载语言 {Language}: {EntryCount} 个条目 (来自 {FilePath})",
                languageCode, poFile.Entries.Count, filePath);

            return poFile;
        }
        catch (FileNotFoundException ex)
        {
            // 异常来源：文件在检查后被删除（竞态条件）
            // 处理方式：记录警告并返回 null，不中断应用运行
            _logger.LogWarning(ex, "[LocalizationService] PO 文件加载时不存在: {FilePath}", filePath);
            return null;
        }
        catch (IOException ex)
        {
            // 异常来源：文件读取 I/O 错误（权限不足、文件被锁定等）
            // 处理方式：记录错误并返回 null，允许应用以降级模式运行
            _logger.LogError(ex, "[LocalizationService] 无法读取 PO 文件: {FilePath}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            // 异常来源：PO 文件格式错误或其他意外异常
            // 处理方式：记录错误但不抛出，允许应用使用部分功能
            _logger.LogError(ex, "[LocalizationService] PO 文件解析失败: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 预加载指定的多个语言文件到缓存中。
    /// 在应用启动时调用可避免首次访问时的延迟。
    /// </summary>
    /// <param name="languageCodes">要预加载的语言代码数组</param>
    /// <returns>成功加载的语言数量</returns>
    public int PreloadLanguages(params string[] languageCodes)
    {
        var successCount = 0;
        foreach (var code in languageCodes)
        {
            if (LoadLanguage(code) != null)
            {
                successCount++;
            }
        }

        _logger.LogInformation("[LocalizationService] 预加载完成: {SuccessCount}/{TotalCount} 个语言",
            successCount, languageCodes.Length);
        return successCount;
    }

    /// <summary>
    /// 清除指定语言的缓存条目。
    /// 下次访问该语言时会重新从磁盘加载。
    /// </summary>
    /// <param name="languageCode">要清除缓存的语言代码</param>
    /// <returns>是否成功移除（false 表示该语言未被缓存）</returns>
    public bool UnloadLanguage(string languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        var removed = _loadedLanguages.TryRemove(normalizedCode, out _);

        if (removed)
        {
            _logger.LogDebug("[LocalizationService] 已卸载语言缓存: {Language}", normalizedCode);
        }

        return removed;
    }

    /// <summary>
    /// 清除所有已加载的语言缓存。
    /// 触发 TranslationCacheInvalidated 事件通知订阅者。
    /// </summary>
    public void ClearCache()
    {
        _loadedLanguages.Clear();
        OnTranslationCacheInvalidated();
        _logger.LogInformation("[LocalizationService] 已清除所有语言缓存");
    }

    #endregion

    #region 翻译查找

    /// <summary>
    /// 获取指定 msgId 的翻译字符串。
    /// 查找顺序：当前语言 → 回退语言 → 返回原始 msgId。
    /// 支持可选的上下文参数用于消歧义。
    /// </summary>
    /// <param name="msgId">要翻译的原始字符串（msgid）</param>
    /// <param name="context">可选的上下文标识符，用于区分相同 msgId 的不同含义</param>
    /// <returns>翻译后的字符串；若找不到翻译则返回原始 msgId</returns>
    /// <exception cref="ArgumentException">
    /// 异常来源：msgId 参数为空或空白
    /// 处理方式：调用方应确保传入有效的非空字符串
    /// </exception>
    public string GetString(string msgId, string? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(msgId);

        // 尝试从当前语言获取翻译
        var translation = TryGetTranslation(_currentLanguage, msgId, context);
        if (translation != null)
        {
            return translation;
        }

        // 尝试从回退语言获取翻译
        if (!string.Equals(_currentLanguage, _fallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            translation = TryGetTranslation(_fallbackLanguage, msgId, context);
            if (translation != null)
            {
                _logger.LogTrace(
                    "[LocalizationService] 翻译回退: '{MsgId}' 在 {CurrentLanguage} 中未找到，使用 {FallbackLanguage} 的翻译",
                    msgId, _currentLanguage, _fallbackLanguage);
                return translation;
            }
        }

        // 未找到任何翻译，返回原始字符串
        _logger.LogTrace(
            "[LocalizationService] 翻译未找到: '{MsgId}' (上下文: {Context})，返回原始字符串",
            msgId, context ?? "(无)");
        return msgId;
    }

    /// <summary>
    /// 获取复数形式的翻译字符串。
    /// 根据数量参数自动选择正确的复数形式索引。
    /// 查找顺序与 GetString 相同：当前语言 → 回退语言 → 返回 msgId 或 msgIdPlural。
    /// </summary>
    /// <param name="msgId">单数形式的原始字符串</param>
    /// <param name="msgIdPlural">复数形式的原始字符串</param>
    /// <param name="count">数量值，用于选择正确的复数形式</param>
    /// <returns>适合当前数量的翻译字符串；若找不到则根据 count 返回 msgId 或 msgIdPlural</returns>
    /// <exception cref="ArgumentException">
    /// 异常来源：msgId 或 msgIdPlural 参数为空白
    /// 处理方式：调用方应确保传入有效的非空字符串
    /// </exception>
    public string GetPluralString(string msgId, string msgIdPlural, long count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(msgId);
        ArgumentException.ThrowIfNullOrWhiteSpace(msgIdPlural);

        // 尝试从当前语言获取复数翻译
        var translation = TryGetPluralTranslation(_currentLanguage, msgId, count);
        if (translation != null)
        {
            return translation;
        }

        // 尝试从回退语言获取复数翻译
        if (!string.Equals(_currentLanguage, _fallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            translation = TryGetPluralTranslation(_fallbackLanguage, msgId, count);
            if (translation != null)
            {
                _logger.LogTrace(
                    "[LocalizationService] 复数翻译回退: '{MsgId}' 在 {CurrentLanguage} 中未找到，使用 {FallbackLanguage}",
                    msgId, _currentLanguage, _fallbackLanguage);
                return translation;
            }
        }

        // 未找到任何翻译，根据数量返回单数或复数形式
        return count == 1 ? msgId : msgIdPlural;
    }

    /// <summary>
    /// 尝试从指定语言获取翻译字符串。
    /// 内部辅助方法，封装单次语言查找逻辑。
    /// </summary>
    private string? TryGetTranslation(string languageCode, string msgId, string? context)
    {
        var poFile = EnsureLanguageLoaded(languageCode);
        if (poFile == null)
        {
            return null;
        }

        var entry = poFile.FindByKey(msgId, context);

        // 检查是否有有效翻译（排除 fuzzy 条目）
        if (entry?.HasTranslation == true && !entry.IsFuzzy)
        {
            return entry.MsgStr;
        }

        return null;
    }

    /// <summary>
    /// 尝试从指定语言获取复数形式翻译。
    /// 内部辅助方法，处理复数形式的选择逻辑。
    /// </summary>
    private string? TryGetPluralTranslation(string languageCode, string msgId, long count)
    {
        var poFile = EnsureLanguageLoaded(languageCode);
        if (poFile == null)
        {
            return null;
        }

        var entry = poFile.FindByMsgId(msgId);
        if (entry?.HasPluralForms != true || !entry.HasTranslation || entry.IsFuzzy)
        {
            return null;
        }

        // 简化的复数规则：英语式（1 为单数，其他为复数）
        // 完整实现应根据目标语言的 Plural-Forms 规则计算索引
        var pluralIndex = count == 1 ? 0 : 1;

        // 确保索引在有效范围内
        if (pluralIndex < entry.PluralTranslations.Count)
        {
            var translation = entry.PluralTranslations[pluralIndex];
            if (!string.IsNullOrEmpty(translation))
            {
                return translation;
            }
        }

        return null;
    }

    /// <summary>
    /// 确保指定语言的 PO 文件已加载到缓存中。
    /// 若尚未加载则自动触发加载流程。
    /// </summary>
    private PoFile? EnsureLanguageLoaded(string languageCode)
    {
        if (_loadedLanguages.TryGetValue(languageCode, out var poFile))
        {
            return poFile;
        }

        return LoadLanguage(languageCode);
    }

    #endregion

    #region 语言管理

    /// <summary>
    /// 切换当前活动语言。
    /// 执行完整的语言切换流程：验证 → 加载 → 持久化 → 事件通知。
    /// 此方法是推荐的切换语言的方式，相比直接设置 CurrentLanguage 属性有更好的错误处理。
    /// </summary>
    /// <param name="languageCode">目标语言代码（如 "zh-CN"、"en-US"、"ja-JP"）</param>
    /// <exception cref="ArgumentException">
    /// 异常来源：languageCode 为空或空白
    /// 处理方式：调用方应传入有效的 ISO 639 语言代码
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 异常来源：目标语言的 PO 文件不存在且无法加载
    /// 处理方式：调用方应先检查 SupportedLanguages 或提前准备好 PO 文件
    /// </exception>
    public void SetCurrentLanguage(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var normalizedCode = NormalizeLanguageCode(languageCode);
        string previousLanguage;

        lock (_syncLock)
        {
            // 避免重复设置相同语言
            if (string.Equals(_currentLanguage, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[LocalizationService] 语言未变化: {Language}", normalizedCode);
                return;
            }

            previousLanguage = _currentLanguage;

            // 验证并预加载目标语言
            var poFile = LoadLanguage(normalizedCode);
            if (poFile == null)
            {
                // 检查 PO 文件是否存在以给出更精确的错误信息
                var filePath = GetPoFilePath(normalizedCode);
                if (!File.Exists(filePath))
                {
                    throw new InvalidOperationException(
                        $"无法切换到语言 '{normalizedCode}'：PO 文件不存在 ({filePath})。" +
                        $"请确保在 {_baseDirectory} 目录下提供了 {normalizedCode}.po 文件。");
                }

                // 文件存在但加载失败（可能是格式错误）
                _logger.LogWarning(
                    "[LocalizationService] 语言 {Language} 的 PO 文件加载失败，但仍切换语言（将以降级模式运行）",
                    normalizedCode);
            }

            // 更新当前语言
            _currentLanguage = normalizedCode;

            // 尝试持久化用户偏好到 SettingsService
            PersistLanguagePreference(normalizedCode);

            _logger.LogInformation(
                "[LocalizationService] 语言已切换: {PreviousLanguage} → {CurrentLanguage}",
                previousLanguage, _currentLanguage);
        }

        // 在锁外触发事件，避免死锁
        OnLanguageChanged(previousLanguage, normalizedCode);
    }

    /// <summary>
    /// 扫描 Localizations 目录，返回所有可用语言的代码列表。
    /// 通过查找 .po 文件自动发现已安装的语言包。
    /// </summary>
    /// <returns>按字母排序的语言代码列表（如 ["en-US", "ja-JP", "zh-CN"]）</returns>
    public IReadOnlyList<string> GetAvailableLanguages()
    {
        var languages = new List<string>();

        if (!Directory.Exists(_baseDirectory))
        {
            _logger.LogWarning("[LocalizationService] 本地化目录不存在: {BaseDirectory}", _baseDirectory);
            return languages;
        }

        try
        {
            // 搜索所有 .po 文件并提取语言代码
            var poFiles = Directory.GetFiles(_baseDirectory, "*.po", SearchOption.TopDirectoryOnly);
            foreach (var file in poFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(fileName))
                {
                    languages.Add(fileName);
                }
            }

            // 按字母排序以保证一致的顺序
            languages.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 异常来源：没有目录读取权限
            // 处理方式：返回已收集的语言列表，不抛出异常
            _logger.LogError(ex, "[LocalizationService] 无权读取本地化目录: {BaseDirectory}", _baseDirectory);
        }
        catch (IOException ex)
        {
            // 异常来源：目录扫描过程中的 I/O 错误
            // 处理方式：返回空列表，允许应用继续运行
            _logger.LogError(ex, "[LocalizationService] 扫描本地化目录失败: {BaseDirectory}", _baseDirectory);
        }

        return languages;
    }

    /// <summary>
    /// 获取指定语言的显示名称。
    /// 从该语言的 PO 文件中查找 Common.LanguageDisplayName 条目的 msgstr，
    /// 如果找不到则回退使用语言代码本身作为显示名称。
    /// </summary>
    /// <param name="languageCode">语言代码（PO 文件名去 .po 后缀）</param>
    /// <returns>语言的显示名称</returns>
    public string GetLanguageDisplayName(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        try
        {
            var filePath = GetPoFilePath(languageCode);
            if (!File.Exists(filePath))
                return languageCode;

            var poFile = _parser.Parse(filePath);
            var entry = poFile.FindByMsgId("Common.LanguageDisplayName");
            if (entry != null && !string.IsNullOrWhiteSpace(entry.MsgStr))
                return entry.MsgStr;
        }
        catch (Exception ex)
        {
            // 异常来源：PO 文件解析失败或文件读取错误
            // 处理方式：回退使用语言代码，不抛出异常
            _logger.LogDebug(ex, "[LocalizationService] 读取语言显示名称失败: {LanguageCode}", languageCode);
        }

        return languageCode;
    }

    /// <summary>
    /// 检查指定语言的 PO 文件是否存在且可用。
    /// </summary>
    /// <param name="languageCode">要检查的语言代码</param>
    /// <returns>若该语言的 PO 文件存在则返回 true</returns>
    public bool IsLanguageAvailable(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var filePath = GetPoFilePath(languageCode);
        return File.Exists(filePath);
    }

    /// <summary>
    /// 获取指定语言的翻译进度统计信息。
    /// 用于显示翻译完成度、管理界面等场景。
    /// </summary>
    /// <param name="languageCode">要查询的语言代码</param>
    /// <returns>翻译进度统计结果；若语言未加载则返回 null</returns>
    public TranslationProgress? GetTranslationProgress(string languageCode)
    {
        var poFile = EnsureLanguageLoaded(languageCode);
        return poFile?.GetTranslationProgress();
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 规范化语言代码格式。
    /// 统一转换为小写并标准化分隔符，确保缓存键的一致性。
    /// </summary>
    private static string NormalizeLanguageCode(string languageCode)
    {
        // 保持原始大小写但去除首尾空白
        return languageCode.Trim();
    }

    /// <summary>
    /// 构建指定语言的 PO 文件完整路径。
    /// </summary>
    private string GetPoFilePath(string languageCode)
    {
        return Path.Combine(_baseDirectory, $"{languageCode}.po");
    }

    /// <summary>
    /// 解析默认语言：优先使用参数值，其次从 SettingsService 读取，最后使用硬编码默认值。
    /// </summary>
    private string ResolveDefaultLanguage(string? explicitDefault)
    {
        // 1. 优先使用显式指定的默认语言
        if (!string.IsNullOrWhiteSpace(explicitDefault))
        {
            return explicitDefault.Trim();
        }

        // 2. 尝试从 SettingsService 读取用户上次选择的语言
        try
        {
            var settingsService = _serviceProvider.GetService(typeof(SettingsService)) as SettingsService;
            if (settingsService != null)
            {
                var savedLanguage = settingsService.GetCurrentLanguage();
                if (!string.IsNullOrWhiteSpace(savedLanguage))
                {
                    _logger.LogDebug("[LocalizationService] 从 SettingsService 恢复语言偏好: {Language}", savedLanguage);
                    return savedLanguage;
                }
            }
        }
        catch (Exception ex)
        {
            // 异常来源：SettingsService 获取或读取过程中出现异常
            // 处理方式：静默忽略，使用硬编码默认值
            _logger.LogWarning(ex, "[LocalizationService] 从 SettingsService 读取语言设置失败，使用默认值");
        }

        // 3. 使用硬编码的默认语言
        return DefaultSettings.DefaultLanguage;
    }

    /// <summary>
    /// 将用户的语言偏好持久化到 SettingsService。
    /// 在语言切换成功后调用，以便下次启动时恢复用户选择。
    /// </summary>
    private void PersistLanguagePreference(string languageCode)
    {
        try
        {
            var settingsService = _serviceProvider.GetService(typeof(SettingsService)) as SettingsService;
            if (settingsService != null)
            {
                settingsService.SetCurrentLanguage(languageCode);
                settingsService.Save();
                _logger.LogDebug("[LocalizationService] 已持久化语言偏好: {Language}", languageCode);
            }
        }
        catch (Exception ex)
        {
            // 异常来源：SettingsService 保存过程中出现异常（I/O 错误、配置损坏等）
            // 处理方式：仅记录警告，不影响语言切换操作本身
            _logger.LogWarning(ex, "[LocalizationService] 持久化语言偏好失败（不影响当前语言设置）");
        }
    }

    /// <summary>
    /// 触发 LanguageChanged 事件。
    /// </summary>
    private void OnLanguageChanged(string previousLanguage, string newLanguage)
    {
        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(previousLanguage, newLanguage));
    }

    /// <summary>
    /// 触发 TranslationCacheInvalidated 事件。
    /// </summary>
    private void OnTranslationCacheInvalidated()
    {
        TranslationCacheInvalidated?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region IDisposable 实现

    /// <summary>
    /// 释放 LocalizationService 占用的资源。
    /// 清除所有缓存的 PO 文件数据并注销事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _loadedLanguages.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    #endregion
}

/// <summary>
/// 语言切换事件参数，携带语言变更的详细信息。
/// </summary>
public class LanguageChangedEventArgs : EventArgs
{
    /// <summary>切换前的语言代码</summary>
    public string PreviousLanguage { get; }

    /// <summary>切换后的新语言代码</summary>
    public string NewLanguage { get; }

    /// <summary>变更发生的时间戳</summary>
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>
    /// 初始化 LanguageChangedEventArgs 实例。
    /// </summary>
    /// <param name="previousLanguage">切换前的语言代码</param>
    /// <param name="newLanguage">切换后的新语言代码</param>
    public LanguageChangedEventArgs(string previousLanguage, string newLanguage)
    {
        PreviousLanguage = previousLanguage;
        NewLanguage = newLanguage;
    }
}
