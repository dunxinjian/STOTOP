using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<UserController> _logger;

    public UserController(STOTOPDbContext dbContext, ILogger<UserController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>
    /// 获取当前用户的布局偏好
    /// </summary>
    [HttpGet("layout-preference")]
    public async Task<IActionResult> GetLayoutPreference()
    {
        var userId = GetUserId();
        if (userId == 0)
            return Unauthorized(ApiResult.Fail("未认证", 401));

        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var json = await conn.ExecuteScalarAsync<string?>(
            "SELECT [F布局偏好] FROM [SYS用户] WHERE [FID] = @userId",
            new { userId });

        if (string.IsNullOrEmpty(json))
            return NoContent();

        return Ok(ApiResult<object>.Success(
            System.Text.Json.JsonSerializer.Deserialize<object>(json)!));
    }

    /// <summary>
    /// 更新当前用户的布局偏好
    /// </summary>
    [HttpPut("layout-preference")]
    public async Task<IActionResult> PutLayoutPreference([FromBody] System.Text.Json.JsonElement body)
    {
        var userId = GetUserId();
        if (userId == 0)
            return Unauthorized(ApiResult.Fail("未认证", 401));

        var json = body.GetRawText();

        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await conn.ExecuteAsync(
            "UPDATE [SYS用户] SET [F布局偏好] = @json WHERE [FID] = @userId",
            new { json, userId });

        return Ok(ApiResult.Ok("保存成功"));
    }

    /// <summary>
    /// 获取当前用户的待办数量聚合
    /// </summary>
    [HttpGet("todo-count")]
    public async Task<IActionResult> GetTodoCount()
    {
        var userId = GetUserId();
        if (userId == 0)
            return Unauthorized(ApiResult.Fail("未认证", 401));

        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var dto = new TodoCountSummaryDto();

        // 处理中批次数
        try
        {
            dto.UploadProcessing = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [CF批次] WHERE [F已撤销]=0 AND [F状态]=4");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询处理中批次数失败（可能表不存在）");
        }

        // 异常批次数（有待处理错误）
        try
        {
            dto.UploadException = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(DISTINCT [F批次ID]) FROM [CF批次错误] WHERE [F派发状态]='Pending'");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询异常批次数失败（可能表不存在）");
        }

        // 停滞预警（处理中超4小时）
        try
        {
            dto.UploadStalled = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [CF批次] WHERE [F已撤销]=0 AND [F状态]=4 AND DATEDIFF(HOUR,[F导入开始时间],GETDATE())>=4");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询停滞预警数失败（可能表不存在）");
        }

        // 停滞预警是处理中的子集，不重复计入 total
        dto.Total = dto.UploadProcessing + dto.UploadException;

        return Ok(ApiResult<TodoCountSummaryDto>.Success(dto));
    }
}

public class TodoCountSummaryDto
{
    public int UploadProcessing { get; set; }
    public int UploadException { get; set; }
    public int UploadStalled { get; set; }
    public int OaTodoCount { get; set; }
    public int Total { get; set; }
}
