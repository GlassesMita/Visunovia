using Microsoft.AspNetCore.Mvc;

namespace Visunovia.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ToastService _toastService;

    public SystemController(IHostApplicationLifetime lifetime, ToastService toastService)
    {
        _lifetime = lifetime;
        _toastService = toastService;
    }

    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        Task.Run(async () =>
        {
            await Task.Delay(300);
            _lifetime.StopApplication();
        });

        return Ok(new { message = "服务正在关闭" });
    }

    /// <summary>
    /// 前端请求退出应用程序
    /// </summary>
    [HttpPost("quit")]
    public IActionResult Quit()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.StopApplication();
        });

        return Ok(new { success = true, message = "Application is quitting" });
    }

    [HttpPost("send-notification")]
    public IActionResult SendNotification([FromBody] SendNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "标题和正文不能同时为空" });
        }

        var title = request.Title ?? string.Empty;
        var message = request.Message ?? string.Empty;

        try
        {
            _toastService.ShowToast(title, message);
            return Ok(new { message = "通知已发送" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "通知发送失败", error = ex.Message });
        }
    }
}

public class SendNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
