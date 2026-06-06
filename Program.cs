using System.Net;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Visunovia.Middleware;
using Visunovia.Services;
using Visunovia.Services.Configuration;
using Visunovia.Services.Localization;
using Vite.AspNetCore;

int port = 32523;
bool noNewTab = false;
string[] argsArray = args;

for (int i = 0; i < argsArray.Length; i++)
{
    if (argsArray[i] == "--port" && i + 1 < argsArray.Length && int.TryParse(argsArray[i + 1], out int p))
    {
        port = p;
        break;
    }
    if (argsArray[i] == "--no-newtab")
    {
        noNewTab = true;
    }
}

if (port == 0 && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VISUNOVIA_PORT")))
{
    _ = int.TryParse(Environment.GetEnvironmentVariable("VISUNOVIA_PORT"), out port);
}

// 在构建 builder 之前读取配置文件，获取 AllowRemoteSession 设置
bool allowRemoteSession = ReadAllowRemoteSessionFromConfig();

// 解决 Vite 开发服务器 431 Request Header Fields Too Large 错误
// Node.js 默认 HTTP 头大小限制为 16KB，Vite .NET 代理层可能携带较大请求头
// 将限制提升至 32KB，确保代理请求不会被拒绝
SetViteNodeOptions();

var builder = WebApplication.CreateBuilder(args);

// 减少冗余的 HTTP 请求日志输出
// 生产构建后 info 级别请求日志会淹没终端，仅保留 Warning 及以上级别
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);

builder.WebHost.ConfigureKestrel(options =>
{
    if (allowRemoteSession)
    {
        options.Listen(IPAddress.Any, port);
        options.Listen(IPAddress.IPv6Any, port);
    }
    else
    {
        options.Listen(IPAddress.Loopback, port);
        options.Listen(IPAddress.IPv6Loopback, port);
    }
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<EditorSessionService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ToastService>();

// 注册 Visunovia 本地化服务（支持多语言）
builder.Services.AddVisunoviaLocalization(options =>
{
    options.BaseDirectory = Path.Combine(AppContext.BaseDirectory, "Localizations");
    options.FallbackLanguage = "en-US";
    options.PreloadLanguages = new[] { "en-US", "zh-CN", "zh-TW", "ja-JP" };
});

// 注册 Vite 开发服务器服务（仅在开发环境使用）
builder.Services.AddViteServices(options =>
{
    // 指定 package.json 所在目录（wwwroot 目录）
    options.Server.PackageDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    // 启用自动启动，确保 Vite 服务器与后端同时就绪
    options.Server.AutoRun = true;
    // 指定 Vite 开发服务器端口（与 vite.config.ts 中的 server.port 一致）
    options.Server.Port = 32423;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// 前端路由重定向：将 kebab-case 路由重定向到 PascalCase 路由
// 这样可以保持与旧路由一致，同时让 Vite/Vue Router 处理页面渲染
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    
    // 定义路由重定向映射
    var routeRedirects = new Dictionary<string, string>
    {
        { "/preferences", "/Preferences" },
        { "/about", "/About" },
        { "/project-settings", "/ProjectSettings" }
    };
    
    if (routeRedirects.TryGetValue(path, out var redirectTarget))
    {
        Console.WriteLine($"[Router] Redirecting {path} -> {redirectTarget}");
        context.Response.Redirect(redirectTarget, permanent: false);
        return;
    }
    
    await next();
});

// 服务静态文件 — 在 Vite 中间件之前注册
// 规则：
// - 构建产物（/assets/*）始终由静态文件中间件提供，绕过 Vite
// - Vite 只处理源码请求（/src/*）和 SPA 路由
// - Vite 不可用时（开发/生产）：使用预构建的静态文件提供 SPA 回退
var baseDir = AppContext.BaseDirectory;
var wwwrootPath = Path.Combine(baseDir, "wwwroot");
var wwwBuildPath = Path.Combine(baseDir, "www_build");

// 检查 www_build 目录是否存在有效的构建产物
var wwwBuildExists = Directory.Exists(Path.Combine(wwwBuildPath, "assets"));
var wwwrootAssetsExists = Directory.Exists(Path.Combine(wwwrootPath, "assets"));

// 优先使用 www_build（独立构建产物），其次是 wwwroot
string staticFilesPath;
if (wwwBuildExists)
{
    staticFilesPath = wwwBuildPath;
}
else if (wwwrootAssetsExists)
{
    staticFilesPath = wwwrootPath;
}
else
{
    staticFilesPath = wwwBuildPath;
}

Console.WriteLine($"[StaticFiles] BaseDirectory: {baseDir}");
Console.WriteLine($"[StaticFiles] Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"[StaticFiles] www_build exists: {wwwBuildExists}");
Console.WriteLine($"[StaticFiles] wwwroot assets exists: {wwwrootAssetsExists}");
Console.WriteLine($"[StaticFiles] Static files path: {staticFilesPath}");

// 构建产物静态文件服务 — 必须在 Vite 中间件之前注册
// 这样 /assets/* 请求会被直接处理，不会到达 Vite 中间件
bool hasStaticAssets = Directory.Exists(staticFilesPath) && Directory.Exists(Path.Combine(staticFilesPath, "assets"));
if (hasStaticAssets)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(staticFilesPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            var headers = ctx.Context.Response.Headers;
            headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }
    });
}

// Vite 开发服务器中间件 — 默认禁用
// Vite 中间件会导致 API 请求被代理到 Vite 服务器，再代理回后端，形成循环
// 使用预构建产物代替 Vite 开发服务器，确保 API 请求直接由后端处理
bool viteServerAvailable = false;

// 如果需要启用 Vite 开发服务器，请取消下面的注释
// 注意：启用后 API 请求可能会超时，因为请求会经过 Vite 代理循环
/*
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("[Vite] 开发模式：尝试启用 Vite 开发服务器中间件");
    try
    {
        app.UseViteDevelopmentServer(true);
        viteServerAvailable = true;
        Console.WriteLine("[Vite] Vite 开发服务器中间件已注册");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Vite] Warning: Vite 开发服务器启动失败: {ex.Message}");
    }
}
*/

Console.WriteLine($"[StaticFiles] Vite server available: {viteServerAvailable}");

if (!hasStaticAssets)
{
    Console.WriteLine($"[StaticFiles] Warning: Static files directory not found at {staticFilesPath}");
    Console.WriteLine($"[StaticFiles] Hint: Run 'npm run build' in wwwroot/ then restart the application");
}

// 启动页面路由 — 根据 isFirstRun 配置决定显示安装向导还是主页面
// 当 isFirstRun 为 true 或不存在时，所有页面请求重定向到 SetupWizard
// 当 isFirstRun 为 false 时，正常加载页面，不进行重定向
// 注意：此中间件必须在 SPA 路由回退之前注册，以确保在 SPA 回退拦截请求之前执行
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // API 请求直接放行
    if (path.StartsWith("/api"))
    {
        await next();
        return;
    }

    // SetupWizard 页面本身直接放行（避免重定向循环）
    if (path.StartsWith("/SetupWizard", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // 静态文件请求直接放行（有扩展名的文件）
    if (path.Contains('.'))
    {
        await next();
        return;
    }

    try
    {
        var settingsService = context.RequestServices.GetRequiredService<SettingsService>();
        var isFirstRunValue = settingsService.GetRawValue(DefaultSettings.IsFirstRunKey);
        bool isFirstRun = string.IsNullOrEmpty(isFirstRunValue) ||
                          (bool.TryParse(isFirstRunValue, out bool parsed) && parsed);

        if (isFirstRun)
        {
            // 首次运行：任何页面请求都重定向到 SetupWizard
            Console.WriteLine($"[StartupPage] isFirstRun={isFirstRun}, redirecting {path} -> /SetupWizard");
            context.Response.Redirect("/SetupWizard", permanent: false);
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[StartupPage] 读取 isFirstRun 配置失败: {ex.Message}，默认显示安装向导");
        context.Response.Redirect("/SetupWizard", permanent: false);
        return;
    }

    await next();
});

// SPA 路由回退 — 所有未匹配的非 API、非静态文件请求返回 index.html
// 始终注册此中间件，作为 Vite 不可用时的回退
// 注意：此中间件必须在 StartupPage 中间件之后注册，否则会在 isFirstRun 检查之前拦截请求
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // API 直接放行
    if (path.StartsWith("/api"))
    {
        await next();
        return;
    }

    // Razor Pages 路由直接放行（如 /SetupWizard），不返回 SPA index.html
    if (path.StartsWith("/SetupWizard", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // 有扩展名的文件请求直接放行（静态文件中间件已处理）
    if (path.Contains('.'))
    {
        await next();
        return;
    }

    // Vite 开发服务器可用时，让 Vite 处理 SPA 路由
    if (viteServerAvailable && app.Environment.IsDevelopment())
    {
        await next();
        return;
    }

    // Vite 不可用时：为所有路由提供 index.html
    if (hasStaticAssets)
    {
        var indexPath = Path.Combine(staticFilesPath, "index.html");
        if (File.Exists(indexPath))
        {
            var htmlContent = await File.ReadAllTextAsync(indexPath);
            htmlContent = htmlContent.Replace("./assets/", "/assets/");
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(htmlContent);
            return;
        }
    }

    await next();
});

app.UseRouting();

// 注册全局异常处理中间件（必须在 UseAuthorization 之前）
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

// 输出本地化服务初始化信息
try
{
    var localizationService = app.Services.GetRequiredService<LocalizationService>();
    var initMsg = localizationService.GetString("Console.LocalizationInitialized");
    Console.WriteLine(string.Format(initMsg, localizationService.CurrentLanguage));
    var availMsg = localizationService.GetString("Console.AvailableLanguages");
    Console.WriteLine(string.Format(availMsg, string.Join(", ", localizationService.SupportedLanguages)));
}
catch (Exception ex)
{
    var failMsg = $"[Warning] Localization service initialization failed: {ex.Message}";
    try
    {
        var localizationService = app.Services.GetRequiredService<LocalizationService>();
        failMsg = string.Format(localizationService.GetString("Console.LocalizationInitFailed"), ex.Message);
    }
    catch { }
    Console.WriteLine(failMsg);
}

// 输出服务器监听模式信息
{
    var localizationService2 = app.Services.GetRequiredService<LocalizationService>();
    var runMsg = localizationService2.GetString("Console.ServerRunning");
    Console.WriteLine(string.Format(runMsg, port));

    var listenModeKey = allowRemoteSession ? "Console.ListenModeRemote" : "Console.ListenMode";
    var listenMsg = localizationService2.GetString(listenModeKey);
    Console.WriteLine(listenMsg);
}

// 输出已注册的 API 终结点信息
Console.WriteLine("");
Console.WriteLine("=== 已注册的 API 终结点 ===");
Console.WriteLine($"  GET  /api/project/currentProject  - 获取当前项目信息");
Console.WriteLine($"  POST /api/project/new             - 新建项目");
Console.WriteLine($"  POST /api/project/import          - 导入项目");
Console.WriteLine($"  GET  /api/project/folder-tree     - 获取项目文件夹树");
Console.WriteLine($"  GET  /api/project/scenes          - 获取场景列表");
Console.WriteLine($"  GET  /api/project/scene           - 读取剧本内容");
Console.WriteLine("============================");
Console.WriteLine("");

if (!noNewTab)
{
    // 打开浏览器到后端 API 服务器（端口 32523），由后端代理 Vite 前端请求
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = $"http://127.0.0.1:{port}",
        UseShellExecute = true
    });
}
else
{
    Console.WriteLine($"[Startup] Running in headless mode (--no-newtab), browser will not be opened automatically.");
    Console.WriteLine($"[Startup] Frontend (via backend): http://127.0.0.1:{port} | Backend API: http://127.0.0.1:{port}");
}

app.Run();

/// <summary>
/// 从 XML 配置文件中读取 AllowRemoteSession 设置。
/// 由于此方法在 DI 容器构建之前调用，无法使用 SettingsService，
/// 因此直接解析 XML 配置文件。
/// </summary>
/// <returns>是否允许远程会话，默认为 false</returns>
static bool ReadAllowRemoteSessionFromConfig()
{
    try
    {
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "Visunovia.exe.config");
        if (!File.Exists(configPath))
            return DefaultSettings.DefaultAllowRemoteSession;

        var doc = new System.Xml.XmlDocument();
        doc.Load(configPath);

        var node = doc.SelectSingleNode($"//{DefaultSettings.SectionName}/AllowRemoteSession");
        if (node?.InnerText != null && bool.TryParse(node.InnerText.Trim(), out bool result))
            return result;

        return DefaultSettings.DefaultAllowRemoteSession;
    }
    catch
    {
        return DefaultSettings.DefaultAllowRemoteSession;
    }
}

/// <summary>
/// 设置 Node.js 的 HTTP 头大小限制，解决 Vite 开发服务器的 431 错误。
/// 该环境变量会在 Vite.AspNetCore 启动 Node.js 子进程时自动继承。
/// 默认限制为 16KB，提升至 32KB 可避免代理层请求头过大导致的拒绝。
/// </summary>
static void SetViteNodeOptions()
{
    const string maxHeaderSize = "--max-http-header-size=32768";
    var currentOptions = Environment.GetEnvironmentVariable("NODE_OPTIONS") ?? string.Empty;

    // 避免重复追加
    if (!currentOptions.Contains("--max-http-header-size"))
    {
        var newOptions = string.IsNullOrEmpty(currentOptions)
            ? maxHeaderSize
            : $"{currentOptions.TrimEnd()} {maxHeaderSize}";
        Environment.SetEnvironmentVariable("NODE_OPTIONS", newOptions);
    }
}