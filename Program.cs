using System.Net;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
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
    options.Server.PackageDirectory = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
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

// 在开发模式下启用 Vite 开发服务器中间件
// 该中间件会自动启动 Vite 并代理前端请求，实现"一个 dotnet run 命令启动前后端"
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("[Vite] 开发模式：启用 Vite 开发服务器中间件");
    Console.WriteLine("[Vite] 前端源码将被实时转换并服务到浏览器");

    // 启用 Vite 开发服务器（true 表示使用集成中间件模式）
    // 所有非 API 请求将被自动代理到 Vite 开发服务器
    app.UseViteDevelopmentServer(true);
}

// 在非 Development 模式下（非 Vite 开发服务器），服务静态文件
// 规则：
// - dotnet run（Development）：Vite 开发服务器处理，使用 www_build 目录中的预构建产物
// - dotnet publish/build release（非 Development）：使用 wwwroot 目录（发布时会将 wwwroot 内容复制到输出）
if (!app.Environment.IsDevelopment())
{
    // 优先检查 www_build 目录（dotnet run 时使用）
    var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    var wwwBuildPath = Path.Combine(builder.Environment.ContentRootPath, "www_build");
    var useWwwBuild = Directory.Exists(Path.Combine(wwwBuildPath, "assets"));
    var staticFilesPath = useWwwBuild ? wwwBuildPath : wwwrootPath;

    Console.WriteLine($"[StaticFiles] ContentRootPath: {builder.Environment.ContentRootPath}");
    Console.WriteLine($"[StaticFiles] Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"[StaticFiles] Static files path: {staticFilesPath}");
    Console.WriteLine($"[StaticFiles] Using www_build: {useWwwBuild}");
    Console.WriteLine($"[StaticFiles] Static files exists: {Directory.Exists(staticFilesPath)}");

    if (Directory.Exists(staticFilesPath))
    {
        // SPA 路由处理：所有非 API 请求返回 index.html，由 Vue Router 处理路由
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";

            // API 请求直接处理
            if (path.StartsWith("/api"))
            {
                await next();
                return;
            }

            // 静态资源直接返回
            if (path.StartsWith("/assets") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/fonts/") ||
                path.StartsWith("/lib/") ||
                path.EndsWith(".js") ||
                path.EndsWith(".css") ||
                path.EndsWith(".woff") ||
                path.EndsWith(".woff2") ||
                path.EndsWith(".ttf") ||
                path.EndsWith(".svg") ||
                path.EndsWith(".png") ||
                path.EndsWith(".jpg") ||
                path.EndsWith(".ico"))
            {
                await next();
                return;
            }

            // 对于所有其他路由（包括 /Preferences, /About, /ProjectSettings），返回 index.html
            var indexPath = Path.Combine(staticFilesPath, "index.html");
            if (File.Exists(indexPath))
            {
                var htmlContent = await File.ReadAllTextAsync(indexPath);
                // 将相对路径 ./assets/ 替换为绝对路径 /assets/
                htmlContent = htmlContent.Replace("./assets/", "/assets/");
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(htmlContent);
                return;
            }

            await next();
        });

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
    else
    {
        Console.WriteLine($"[StaticFiles] Warning: Static files directory not found at {staticFilesPath}");
        Console.WriteLine($"[StaticFiles] Hint: Run 'npm run build' in wwwroot/ then use 'dotnet publish' for full deployment");
    }
}

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