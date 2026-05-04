using System.Net;
using System.Text.Json.Serialization;
using Visunovia.Middleware;
using Visunovia.Services;
using Visunovia.Services.Configuration;
using Visunovia.Services.Localization;

int port = 28478;
string[] argsArray = args;

for (int i = 0; i < argsArray.Length; i++)
{
    if (argsArray[i] == "--port" && i + 1 < argsArray.Length && int.TryParse(argsArray[i + 1], out int p))
    {
        port = p;
        break;
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
app.UseStaticFiles();

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
