namespace Visunovia.Services.Localization;

/// <summary>
/// 字符串本地化器接口，提供类型安全的翻译字符串访问能力。
/// 作为 LocalizationService 的轻量级抽象层，方便在 Razor 视图、控制器和服务代码中使用。
///
/// 设计目标：
/// - 简化翻译字符串的获取操作
/// - 支持字符串格式化参数
/// - 与 ASP.NET Core 的依赖注入模式无缝集成
/// - 提供索引器语法以获得更简洁的调用方式
///
/// 使用示例：
/// <code>
/// // 在 Razor 视图中使用
/// @inject IStringLocalizer Localizer
/// &lt;h1&gt;@Localizer["WelcomeMessage"]&lt;/h1&gt;
/// &lt;p&gt;@Localizer.GetString("ItemCount", itemCount)&lt;/p&gt;
///
/// // 在服务代码中使用
/// public class MyService
/// {
///     private readonly IStringLocalizer _localizer;
///
///     public MyService(IStringLocalizer localizer)
///     {
///         _localizer = localizer;
///     }
///
///     public string GetMessage()
///     {
///         return _localizer["Greeting"];
///     }
/// }
/// </code>
/// </summary>
public interface IStringLocalizer
{
    /// <summary>
    /// 索引器：获取指定键的翻译字符串。
    /// 提供类似字典的简洁访问方式，适用于不需要格式化参数的场景。
    /// </summary>
    /// <param name="name">要翻译的键名（对应 PO 文件中的 msgid）</param>
    /// <returns>翻译后的字符串；若找不到翻译则返回原始键名</returns>
    /// <example>
    /// <code>
    /// var title = localizer["PageTitle"];
    /// var welcome = localizer["Hello, {0}!"]; // 仅获取，不进行格式化
    /// </code>
    /// </example>
    string this[string name] { get; }

    /// <summary>
    /// 获取并格式化翻译字符串。
    /// 使用 string.Format 将提供的参数插入到翻译字符串中的占位符。
    /// 适用于包含动态内容的翻译场景（如 "找到 {0} 个结果"）。
    /// </summary>
    /// <param name="name">要翻译的键名（msgid）</param>
    /// <param name="arguments">要插入到翻译字符串中的格式化参数</param>
    /// <returns>格式化后的翻译字符串；若找不到翻译则返回用原始键名格式化的结果</returns>
    /// <example>
    /// <code>
    /// // 假设 PO 文件中 "ItemCount" = "找到 {0} 个项目"
    /// var message = localizer.GetString("ItemCount", 42); // 返回 "找到 42 个项目"
    ///
    /// // 多个参数
    /// // "Welcome" = "欢迎, {0}! 您有 {1} 条新消息"
    /// var msg = localizer.GetString("Welcome", "张三", 5);
    /// </code>
    /// </example>
    string GetString(string name, params object[] arguments);
}

/// <summary>
/// 泛型字符串本地化器接口，支持按类型或区域分组管理翻译资源。
/// 继承自 IStringLocalizer，添加类型关联的上下文信息。
///
/// 典型用途：
/// - 按功能模块分组（如 IStringLocalizer&lt;HomeController&gt;）
/// - 按业务领域分组（如 IStringLocalizer&lt;OrderManagement&gt;）
/// - 为不同视图或组件提供独立的翻译命名空间
///
/// 使用示例：
/// <code>
/// // 控制器中按控制器类型分组
/// public class HomeController : Controller
/// {
///     private readonly IStringLocalizer<HomeController> _localizer;
///
///     public HomeController(IStringLocalizer<HomeController> localizer)
///     {
///         _localizer = localizer;
///     }
/// }
///
/// // 服务中按功能模块分组
/// public class OrderService
/// {
///     private readonly IStringLocalizer<OrderService> _localizer;
///     // ...
/// }
/// </code>
/// </summary>
/// <typeparam name="T">关联的资源类型，用于确定翻译资源的上下文或前缀</typeparam>
public interface IStringLocalizer<T> : IStringLocalizer
{
}
