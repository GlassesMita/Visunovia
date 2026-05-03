using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Visunovia.Services.Configuration;

namespace Visunovia.Services.Localization;

/// <summary>
/// 服务集合扩展方法，提供 Visunovia 本地化服务的便捷注册方式。
/// 封装复杂的 DI 注册逻辑为简洁的 fluent API 调用。
///
/// 设计原则：
/// - 遵循 ASP.NET Core 的 DI 注册约定和最佳实践
/// - 提供合理的默认配置，减少用户配置负担
/// - 支持灵活的自定义选项以满足特殊需求
/// - 确保服务生命周期与使用场景匹配
///
/// 注册的服务及其生命周期：
/// - LocalizationService：Singleton（单例）- 全局共享的翻译缓存和服务
/// - IStringLocalizer：Transient（瞬态）- 轻量级无状态包装器
/// - IStringLocalizer&lt;T&gt;：Transient（瞬态）- 泛型版本的无状态包装器
///
/// 使用示例：
/// <code>
/// // Program.cs 中的基本用法
/// var builder = WebApplication.CreateBuilder(args);
///
/// // 使用默认配置注册（Localizations 目录，默认语言从 SettingsService 读取）
/// builder.Services.AddVisunoviaLocalization();
///
/// // 或自定义配置
/// builder.Services.AddVisunoviaLocalization(options =>
/// {
///     options.BaseDirectory = "CustomLocalePath";
///     options.DefaultLanguage = "ja-JP";
///     options.FallbackLanguage = "en-US";
///     options.PreloadLanguages = ["en-US", "ja-JP", "zh-CN"];
/// });
///
/// var app = builder.Build();
/// app.Run();
/// </code>
/// </summary>
public static class ServiceCollectionExtensions
{
    #region 扩展方法

    /// <summary>
    /// 注册 Visunovia 本地化服务到依赖注入容器。
    /// 使用默认配置参数：
    /// - BaseDirectory: 应用根目录下的 "Localizations" 文件夹
    /// - DefaultLanguage: 从 SettingsService 读取或使用 "en-US"
    /// - FallbackLanguage: "en-US"
    ///
    /// 此方法是 AddVisunoviaLocalization(Action&lt;LocalizationOptions&gt;) 的简化版本，
    /// 适用于不需要自定义配置的场景。
    /// </summary>
    /// <param name="services">ASP.NET Core 服务集合</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：services 参数为 null
    /// 处理方式：调用方应确保在有效的 WebApplicationBuilder 上调用此方法
    /// </exception>
    public static IServiceCollection AddVisunoviaLocalization(this IServiceCollection services)
    {
        return AddVisunoviaLocalization(services, null);
    }

    /// <summary>
    /// 注册 Visunovia 本地化服务到依赖注入容器，支持自定义配置选项。
    /// 提供完整的配置灵活性，可自定义基础目录、默认语言、回退语言等参数。
    ///
    /// 注册流程：
    /// 1. 创建并验证 LocalizationOptions 配置对象
    /// 2. 确保 Localizations 基础目录存在
    /// 3. 注册 LocalizationService 为 Singleton
    /// 4. 注册 IStringLocalizer 和 IStringLocalizer&lt;T&gt; 为 Transient
    /// 5. 可选：预加载指定的语言文件
    /// </summary>
    /// <param name="services">ASP.NET Core 服务集合</param>
    /// <param name="configure">配置委托，用于自定义本地化选项；若为 null 则使用默认值</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：services 参数为 null
    /// 处理方式：调用方应在 WebApplication.Builder.Services 上调用
    /// </exception>
    /// <example>
    /// <code>
    /// // 完整配置示例
    /// builder.Services.AddVisunoviaLocalization(options =>
    /// {
    ///     // 自定义 PO 文件存放路径（相对于应用根目录或绝对路径）
    ///     options.BaseDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "Locales");
    ///
    ///     // 设置启动时的默认语言
    ///     options.DefaultLanguage = "zh-CN";
    ///
    ///     // 设置回退语言（当目标语言缺少翻译时使用）
    ///     options.FallbackLanguage = "en-US";
    ///
    ///     // 预加载常用语言以避免首次访问延迟
    ///     options.PreloadLanguages = new[] { "en-US", "zh-CN" };
    ///
    ///     // 是否在启动时自动扫描并加载所有可用语言
    ///     options.AutoLoadAllLanguages = false;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddVisunoviaLocalization(
        this IServiceCollection services,
        Action<LocalizationOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 创建并应用配置选项
        var options = new LocalizationOptions();
        configure?.Invoke(options);

        // 验证和规范化配置
        ValidateAndNormalizeOptions(options);

        // 确保基础目录存在
        EnsureBaseDirectoryExists(options.BaseDirectory);

        // 注册核心服务：LocalizationService 为单例
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LocalizationService>>();
            return new LocalizationService(
                serviceProvider: sp,
                logger: logger,
                baseDirectory: options.BaseDirectory,
                defaultLanguage: options.DefaultLanguage);
        });

        // 注册 IStringLocalizer 为瞬态（轻量级、无状态）
        services.AddTransient<IStringLocalizer, VisunoviaStringLocalizer>();

        // 注册泛型 IStringLocalizer<T> 为瞬态
        services.AddTransient(typeof(IStringLocalizer<>), typeof(VisunoviaStringLocalizer<>));

        // 可选：预加载语言（在应用启动时异步执行）
        if (options.PreloadLanguages != null && options.PreloadLanguages.Length > 0)
        {
            // 使用工厂委托在第一次请求时延迟执行预加载
            services.AddSingleton<IStartupFilter>(sp =>
            {
                var localizationService = sp.GetRequiredService<LocalizationService>();
                var logger = sp.GetRequiredService<ILogger<LocalizationOptions>>();

                return new LocalizationPreloadStartupFilter(localizationService, options.PreloadLanguages, logger);
            });
        }

        return services;
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 验证并规范化配置选项，确保所有参数合法且一致。
    /// </summary>
    private static void ValidateAndNormalizeOptions(LocalizationOptions options)
    {
        // 规范化基础目录路径
        if (string.IsNullOrWhiteSpace(options.BaseDirectory))
        {
            options.BaseDirectory = Path.Combine(AppContext.BaseDirectory, "Localizations");
        }
        else
        {
            // 将相对路径转换为基于应用根目录的绝对路径
            if (!Path.IsPathRooted(options.BaseDirectory))
            {
                options.BaseDirectory = Path.Combine(AppContext.BaseDirectory, options.BaseDirectory);
            }
        }

        // 验证默认语言
        if (string.IsNullOrWhiteSpace(options.DefaultLanguage))
        {
            options.DefaultLanguage = null;
        }

        // 验证回退语言
        if (string.IsNullOrWhiteSpace(options.FallbackLanguage))
        {
            options.FallbackLanguage = "en-US";
        }
    }

    /// <summary>
    /// 确保本地化基础目录存在。若不存在则尝试创建。
    /// </summary>
    private static void EnsureBaseDirectoryExists(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory))
        {
            try
            {
                Directory.CreateDirectory(baseDirectory);
                System.Diagnostics.Debug.WriteLine(
                    $"[AddVisunoviaLocalization] 已创建本地化目录: {baseDirectory}");
            }
            catch (UnauthorizedAccessException ex)
            {
                // 异常来源：没有目录创建权限
                // 处理方式：不抛出异常，允许应用启动（运行时会记录警告）
                System.Diagnostics.Debug.WriteLine(
                    $"[AddVisunoviaLocalization] 无法创建本地化目录: {baseDirectory} - {ex.Message}");
            }
            catch (IOException ex)
            {
                // 异常来源：磁盘空间不足或路径无效等 I/O 问题
                // 处理方式：不抛出异常，应用仍可启动但无法加载翻译
                System.Diagnostics.Debug.WriteLine(
                    $"[AddVisunoviaLocalization] 创建本地化目录失败: {baseDirectory} - {ex.Message}");
            }
        }
    }

    #endregion
}

/// <summary>
/// 本地化服务配置选项，用于自定义 AddVisunoviaLocalization 的行为。
/// 所有属性都有合理的默认值，仅需覆盖需要自定义的部分。
/// </summary>
public class LocalizationOptions
{
    /// <summary>
    /// 获取或设置 PO 文件的基础目录路径。
    /// 支持相对于应用程序根目录的路径或绝对路径。
    /// 默认值："{AppContext.BaseDirectory}/Localizations"
    /// </summary>
    /// <example>
    /// <code>
    /// // 相对路径（自动转换为绝对路径）
    /// options.BaseDirectory = "Resources/Locales";
    ///
    /// // 绝对路径
    /// options.BaseDirectory = "D:/Translations/MyApp";
    /// </code>
    /// </example>
    public string BaseDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Localizations");

    /// <summary>
    /// 获取或设置默认语言代码。
    /// 应用启动时的初始语言，也会作为 SettingsService 的回退默认值。
    /// 默认值：从 SettingsService 读取或 "en-US"
    /// </summary>
    public string? DefaultLanguage { get; set; } = null;

    /// <summary>
    /// 获取或设置回退语言代码。
    /// 当当前语言的 PO 文件中找不到某个翻译时，会自动在此语言中查找。
    /// 通常设置为源语言（如英语），确保至少显示原始文本。
    /// 默认值："en-US"
    /// </summary>
    public string FallbackLanguage { get; set; } = "en-US";

    /// <summary>
    /// 获取或设置需要在应用启动时预加载的语言代码数组。
    /// 预加载可以消除首次访问某语言时的文件读取延迟。
    /// 设为 null 或空数组表示不进行预加载。
    /// 默认值：null（不预加载）
    /// </summary>
    /// <example>
    /// <code>
    /// // 预加载常用的三种语言
    /// options.PreloadLanguages = new[] { "en-US", "zh-CN", "ja-JP" };
    /// </code>
    /// </example>
    public string[]? PreloadLanguages { get; set; }

    /// <summary>
    /// 获取或设置是否在启动时自动加载所有可用语言。
    /// 当设为 true 时，会扫描 BaseDirectory 并加载所有 .po 文件。
    /// 注意：如果语言包数量很多，可能会延长启动时间。
    /// 默认值：false（仅按需加载）
    /// </summary>
    public bool AutoLoadAllLanguages { get; set; } = false;
}

/// <summary>
/// 启动过滤器，用于在应用启动完成后执行语言预加载操作。
/// 实现 IStartupFilter 接口，确保在所有服务注册完成后再执行预加载。
/// </summary>
internal class LocalizationPreloadStartupFilter : IStartupFilter
{
    private readonly LocalizationService _localizationService;
    private readonly string[] _languagesToPreload;
    private readonly ILogger _logger;

    /// <summary>
    /// 初始化预加载启动过滤器实例。
    /// </summary>
    public LocalizationPreloadStartupFilter(
        LocalizationService localizationService,
        string[] languagesToPreload,
        ILogger logger)
    {
        _localizationService = localizationService;
        _languagesToPreload = languagesToPreload;
        _logger = logger;
    }

    /// <summary>
    /// 配置启动管道，在应用启动后立即执行预加载逻辑。
    /// </summary>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // 在管道初始化后执行预加载
            try
            {
                var loadedCount = _localizationService.PreloadLanguages(_languagesToPreload);
                _logger.LogInformation(
                    "[LocalizationPreload] 启动预加载完成: 成功加载 {LoadedCount}/{TotalCount} 个语言",
                    loadedCount, _languagesToPreload.Length);
            }
            catch (Exception ex)
            {
                // 异常来源：预加载过程中出现意外错误
                // 处理方式：记录错误但不阻止应用启动，语言将在首次访问时按需加载
                _logger.LogError(ex, "[LocalizationPreload] 语言预加载失败，将改为按需加载模式");
            }

            // 继续执行后续的中间件管道配置
            next(app);
        };
    }
}
