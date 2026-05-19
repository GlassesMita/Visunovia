using System.Net;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
using Visunovia.Middleware;
using Visunovia.Services;
using Visunovia.Services.Configuration;
using Visunovia.Services.Localization;

int port = 28478;
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

var builder = WebApplication.CreateBuilder(args);

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

// 在生产模式下，服务 wwwroot 目录中的 Vue 构建产物
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var useVueBuild = Directory.Exists(wwwrootPath) && 
                  (Directory.GetFiles(wwwrootPath, "*.html").Any() || 
                   Directory.GetFiles(Path.Combine(wwwrootPath, "assets"), "*", SearchOption.AllDirectories).Any());

Console.WriteLine($"[StaticFiles] ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"[StaticFiles] Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"[StaticFiles] wwwroot path: {wwwrootPath}");
Console.WriteLine($"[StaticFiles] wwwroot exists: {Directory.Exists(wwwrootPath)}");
Console.WriteLine($"[StaticFiles] Using Vue build: {useVueBuild}");

if (useVueBuild)
{
    Console.WriteLine($"[StaticFiles] Using wwwroot directory for Vue build");

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";

        if (path == "/" || path == "/Preferences" || path == "/FileBrowser" || path == "/About" || path == "/Shortcuts" ||
            path.StartsWith("/assets") || path == "/index.html" || path == "/preferences.html" ||
            path == "/FileBrowser.html" || path == "/About.html" || path == "/Shortcuts.html")
        {
            var htmlMap = new Dictionary<string, string>
            {
                ["/"] = "index.html",
                ["/Preferences"] = "preferences.html",
                ["/FileBrowser"] = "FileBrowser.html",
                ["/About"] = "About.html",
                ["/Shortcuts"] = "Shortcuts.html",
            };

            if (htmlMap.ContainsKey(path))
            {
                var indexPath = Path.Combine(wwwrootPath, htmlMap[path]);
                if (File.Exists(indexPath))
                {
                    var htmlContent = await File.ReadAllTextAsync(indexPath);
                    // 将相对路径 ./assets/ 替换为绝对路径 /assets/，解决从子路径访问时资源加载失败的问题
                    htmlContent = htmlContent.Replace("./assets/", "/assets/");
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(htmlContent);
                    return;
                }
            }

            var filePath = Path.Combine(wwwrootPath, path.TrimStart('/'));
            if (File.Exists(filePath))
            {
                await next();
                return;
            }
        }

        if (path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/fonts/") ||
            path.StartsWith("/lib/") || path.StartsWith("/favicon") || path == "/index.html.bak")
        {
            var fallbackPath = Path.Combine(wwwrootPath, path.TrimStart('/'));
            if (File.Exists(fallbackPath))
            {
                var ext = Path.GetExtension(fallbackPath);
                var contentType = ext switch
                {
                    ".css" => "text/css",
                    ".js" => "application/javascript",
                    ".ttf" => "font/ttf",
                    ".woff" => "font/woff",
                    ".woff2" => "font/woff2",
                    ".ico" => "image/x-icon",
                    _ => "application/octet-stream"
                };
                context.Response.ContentType = contentType;
                await context.Response.SendFileAsync(fallbackPath);
                return;
            }
        }

        await next();
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath),
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
    Console.WriteLine($"[StaticFiles] Using default wwwroot for static files");

    // 当 wwwroot 目录不存在时（如纯编译输出未包含前端资源），跳过静态文件配置
    if (!Directory.Exists(wwwrootPath))
    {
        Console.WriteLine($"[StaticFiles] Warning: wwwroot directory not found at {wwwrootPath}, skipping static file serving");
        Console.WriteLine($"[StaticFiles] Hint: Run 'npm run build' in wwwroot/ then use 'dotnet publish' for full deployment");
    }
    else
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            // 开发模式下，将 /src/* 和 /@vite/* 请求代理到 Vite 开发服务器
            if (path.StartsWith("/src/") || path.StartsWith("/@vite/") || path.StartsWith("/node_modules/"))
            {
                var vitePort = 5173;
                var vitePath = path;
                if (path == "/@vite/client")
                {
                    vitePath = "/@vite/client";
                }

                try
                {
                    var viteUrl = $"http://127.0.0.1:{vitePort}{vitePath}{context.Request.QueryString}";
                    var request = (HttpWebRequest)WebRequest.Create(viteUrl);
                    request.Method = context.Request.Method;
                    request.Headers["Accept"] = context.Request.Headers["Accept"].ToString();

                    using var response = (HttpWebResponse)request.GetResponse();
                    using var responseStream = response.GetResponseStream();
                    using var memoryStream = new MemoryStream();
                    responseStream?.CopyTo(memoryStream);

                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.ContentType;
                    await context.Response.Body.WriteAsync(memoryStream.ToArray());
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ViteProxy] Failed to proxy {path}: {ex.Message}");
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync($"Vite dev server not available. Please run 'npm run dev' in wwwroot directory.\nError: {ex.Message}");
                    return;
                }
            }

            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath),
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
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = $"http://127.0.0.1:{port}",
        UseShellExecute = true
    });
}
else
{
    Console.WriteLine($"[Startup] Running in headless mode (--no-newtab), browser will not be opened automatically.");
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