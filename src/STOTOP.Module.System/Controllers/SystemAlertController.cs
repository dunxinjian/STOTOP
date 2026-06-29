using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/alerts")]
[ApiController]
[Authorize]
public class SystemAlertController : ControllerBase
{
    private readonly ISystemAlertService _systemAlertService;

    public SystemAlertController(ISystemAlertService systemAlertService)
    {
        _systemAlertService = systemAlertService;
    }

    /// <summary>
    /// 发布系统告警
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PublishAlert([FromBody] PublishAlertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("标题不能为空");
        if (request.TargetUserIds == null || !request.TargetUserIds.Any())
            return BadRequest("目标用户不能为空");

        await _systemAlertService.PublishAlertAsync(
            request.AlertType ?? "general",
            request.Title,
            request.Message ?? "",
            request.TargetUserIds);

        return Ok(new { message = "告警已发布" });
    }
}

public class PublishAlertRequest
{
    public string? AlertType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<long> TargetUserIds { get; set; } = new();
}
