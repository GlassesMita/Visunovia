using System.Text.Json.Serialization;
using Visunovia.Middleware;
using Visunovia.Services;
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

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(port);
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
    options.PreloadLanguages = new[] { "en-US", "zh-CN" };
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

{
    var localizationService2 = app.Services.GetRequiredService<LocalizationService>();
    var runMsg = localizationService2.GetString("Console.ServerRunning");
    Console.WriteLine(string.Format(runMsg, port));
}

app.Run();
