using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/cardflow/callback")]
public class NotificationCallbackController : ControllerBase
{
    private readonly IEnumerable<INotificationChannel> _channels;

    public NotificationCallbackController(IEnumerable<INotificationChannel> channels)
    {
        _channels = channels;
    }

    [HttpPost("{channel}")]
    public async Task<ApiResult> HandleCallback(string channel)
    {
        var handler = _channels.FirstOrDefault(c => c.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
        if (handler == null)
            return ApiResult.Fail($"未知的回调渠道: {channel}", 404);

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString());

        var context = new CallbackContext(channel, Request.Query["event"].ToString(), rawBody, headers);

        var valid = await handler.ValidateCallbackAsync(context);
        if (!valid)
            return ApiResult.Fail("签名验证失败", 403);

        await handler.HandleCallbackAsync(context);
        return ApiResult.Ok("回调处理成功");
    }
}
