using Microsoft.AspNetCore.Mvc;

namespace Visunovia.Controllers;

/// <summary>
/// 系统控制 API，提供应用退出等系统级操作
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IHostApplicationLifetime _lifetime;

    public SystemController(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    /// <summary>
    /// 请求停止后端服务（用于前端退出应用时调用）
    /// </summary>
    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        // 异步延迟停止，确保响应能先返回给前端
        Task.Run(async () =>
        {
            await Task.Delay(300); // 等待 300ms 让 HTTP 响应先发送
            _lifetime.StopApplication();
        });

        return Ok(new { message = "服务正在关闭" });
    }
}
