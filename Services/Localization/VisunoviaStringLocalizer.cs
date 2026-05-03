namespace Visunovia.Services.Localization;

/// <summary>
/// Visunovia 字符串本地化器实现，基于 LocalizationService 提供翻译功能。
/// 实现 IStringLocalizer 和 IStringLocalizer&lt;T&gt; 接口，
/// 为 Razor 视图、控制器和服务代码提供便捷的翻译访问方式。
///
/// 核心特性：
/// - 通过索引器或 GetString 方法获取翻译字符串
/// - 支持字符串格式化参数（string.Format 风格）
/// - 自动处理翻译缺失情况（返回原始键名）
/// - 轻量级设计：每次请求创建新实例（瞬态/作用域生命周期）
/// - 线程安全：所有操作委托给线程安全的 LocalizationService 单例
///
/// 生命周期建议：
/// - 注册为 Transient（瞬态）：每次注入创建新实例，开销极小
/// - 或注册为 Scoped（作用域）：同一 HTTP 请求内共享实例
///
/// 性能特点：
/// - 无内部状态（除 LocalizationService 引用外），内存占用小
/// - 所有翻译查找委托给 LocalizationService 的缓存机制
/// - 字符串格式化仅在需要时执行
///
/// 使用示例：
/// <code>
/// // 在 Razor 视图中
/// @inject IStringLocalizer Localizer
/// &lt;h1&gt;@Localizer["AppTitle"]&lt;/h1&gt;
/// &lt;p&gt;@Localizer.GetString("WelcomeUser", User.Name)&lt;/p&gt;
///
/// // 在控制器中
/// public class HomeController : Controller
/// {
///     private readonly IStringLocalizer _localizer;
///
///     public HomeController(IStringLocalizer localizer)
///     {
///         _localizer = localizer;
///     }
///
///     public IActionResult Index()
///     {
///         ViewBag.Title = _localizer["HomePageTitle"];
///         return View();
///     }
/// }
/// </code>
/// </summary>
public class VisunoviaStringLocalizer : IStringLocalizer
{
    #region 私有字段

    /// <summary>核心本地化服务实例</summary>
    private readonly LocalizationService _localizationService;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化 VisunoviaStringLocalizer 实例。
    /// 通过依赖注入接收 LocalizationService 单例。
    /// </summary>
    /// <param name="localizationService">核心本地化服务实例（通过 DI 注入）</param>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：localizationService 参数为 null
    /// 处理方式：DI 容器应确保正确注册和注入 LocalizationService
    /// </exception>
    public VisunoviaStringLocalizer(LocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        _localizationService = localizationService;
    }

    #endregion

    #region IStringLocalizer 实现

    /// <summary>
    /// 索引器实现：获取指定键的翻译字符串。
    /// 直接调用 LocalizationService.GetString() 并返回结果。
    /// 适用于不需要格式化参数的简单场景。
    /// </summary>
    /// <param name="name">要翻译的键名（msgid）</param>
    /// <returns>翻译后的字符串；若找不到则返回原始键名</returns>
    /// <example>
    /// <code>
    /// var localizer = new VisunoviaStringLocalizer(localizationService);
    /// var title = localizer["PageTitle"]; // 获取 "PageTitle" 的翻译
    /// var welcome = localizer["Hello"];   // 获取 "Hello" 的翻译
    /// </code>
    /// </example>
    public string this[string name] => _localizationService.GetString(name);

    /// <summary>
    /// 获取并格式化翻译字符串的实现。
    /// 先从 LocalizationService 获取翻译模板，然后使用 string.Format 插入参数。
    ///
    /// 格式化规则：
    /// - 使用标准 .NET 复合格式化语法（{0}, {1}, ...）
    /// - 若翻译字符串包含占位符但参数数量不匹配，string.Format 会抛出 FormatException
    /// - 若找不到翻译，使用原始键名作为格式化模板
    ///
    /// 性能优化：
    /// - 仅在提供了非空参数时才执行格式化操作
    /// - 零参数时直接返回翻译字符串，避免不必要的格式化开销
    /// </summary>
    /// <param name="name">要翻译的键名</param>
    /// <param name="arguments">要插入到翻译字符串中的格式化参数</param>
    /// <returns>格式化后的完整翻译字符串</returns>
    /// <exception cref="FormatException">
    /// 异常来源：翻译字符串中的占位符索引与提供的参数数量不匹配
    /// 处理方式：调用方应确保 PO 文件中的翻译模板与代码中的参数一致
    /// </exception>
    /// <example>
    /// <code>
    /// var localizer = new VisunoviaStringLocalizer(localizationService);
    ///
    /// // 假设 PO 文件中: "Greeting" = "你好, {0}! 今天是 {1}."
    /// var message = localizer.GetString("Greeting", "张三", "周一");
    /// // 结果: "你好, 张三! 今天是 周一."
    ///
    /// // 无参数的情况
    /// var title = localizer.GetString("Title"); // 直接返回翻译，不执行格式化
    /// </code>
    /// </example>
    public string GetString(string name, params object[] arguments)
    {
        // 从 LocalizationService 获取翻译模板
        var template = _localizationService.GetString(name);

        // 如果没有提供参数或参数为空数组，直接返回模板
        if (arguments == null || arguments.Length == 0)
        {
            return template;
        }

        // 执行字符串格式化
        try
        {
            return string.Format(template, arguments);
        }
        catch (FormatException ex)
        {
            // 异常来源：翻译模板中的占位符（如 {0}, {1}）与传入的参数数量或类型不匹配
            // 处理方式：记录错误并返回未格式化的原始模板，避免应用崩溃
            System.Diagnostics.Debug.WriteLine(
                $"[VisunoviaStringLocalizer] 字符串格式化失败: Key='{name}', Template='{template}', " +
                $"ParameterCount={arguments.Length} - {ex.Message}");

            // 返回原始模板以保证应用继续运行
            return template;
        }
    }

    #endregion
}

/// <summary>
/// 泛型版本的 Visunovia 字符串本地化器实现。
/// 继承自 VisunoviaStringLocalizer 并实现 IStringLocalizer&lt;T&gt; 接口。
///
/// 类型参数 T 可用于未来的资源分组或上下文前缀功能。
/// 当前版本中，泛型参数主要用于 DI 注册时的类型区分，
/// 允许为不同的组件或模块注入不同配置的本地化器实例。
///
/// 扩展性设计：
/// - 可在未来版本中添加基于类型名称的资源文件查找逻辑
/// - 可支持按命名空间自动加载特定区域的翻译资源
/// - 可与资源管理器集成，实现更细粒度的翻译控制
///
/// 使用示例：
/// <code>
/// // 按控制器类型分组
/// public class ProductController : Controller
/// {
///     private readonly IStringLocalizer<ProductController> _localizer;
///
///     public ProductController(IStringLocalizer<ProductController> localizer)
///     {
///         _localizer = localizer;
///     }
///     // ...
/// }
///
/// // 按服务类型分组
/// public class ReportService
/// {
///     private readonly IStringLocalizer<ReportService> _localizer;
///     // ...
/// }
/// </code>
/// </summary>
/// <typeparam name="T">关联的类型，用于标识此本地化器的上下文</typeparam>
public class VisunoviaStringLocalizer<T> : VisunoviaStringLocalizer, IStringLocalizer<T>
{
    /// <summary>
    /// 初始化泛型版本的 VisunoviaStringLocalizer 实例。
    /// 接收 LocalizationService 并传递给基类构造函数。
    /// </summary>
    /// <param name="localizationService">核心本地化服务实例</param>
    /// <exception cref="ArgumentNullException">
    /// 异常来源：localizationService 参数为 null
    /// 处理方式：DI 容器应确保正确注册和注入 LocalizationService
    /// </exception>
    public VisunoviaStringLocalizer(LocalizationService localizationService)
        : base(localizationService)
    {
    }
}
