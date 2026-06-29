using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STOTOP.WebAPI.Controllers.Mobile;

/// <summary>
/// 移动端通用接口
/// </summary>
[ApiController]
[Route("api/mobile")]
public class MobileController : ControllerBase
{
    private readonly ILogger<MobileController> _logger;

    public MobileController(ILogger<MobileController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 版本检�?
    /// </summary>
    [HttpGet("version")]
    [AllowAnonymous]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            code = 200,
            data = new
            {
                version = "1.0.0",
                forceUpdate = false,
                releaseNotes = "初始版本"
            },
            message = "ok"
        });
    }

    /// <summary>
    /// 前端错误上报
    /// </summary>
    [HttpPost("error-report")]
    [AllowAnonymous]
    public IActionResult ReportError([FromBody] ErrorReportRequest request)
    {
        _logger.LogWarning(
            "[Mobile Error] Route={Route}, Message={Message}, UA={UserAgent}",
            request.Route, request.Message, request.UserAgent);

        // TODO: 可以持久化到数据库或发送到告警系统
        return Ok(new { code = 200, message = "ok" });
    }

    public class ErrorReportRequest
    {
        public string Message { get; set; } = "";
        public string? Stack { get; set; }
        public string? Route { get; set; }
        public string? UserAgent { get; set; }
    }
}
