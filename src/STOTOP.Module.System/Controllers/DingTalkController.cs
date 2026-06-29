using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/dingtalk")]
[ApiController]
[Authorize]
public class DingTalkController : ControllerBase
{
    private readonly IDingTalkService _dingTalkService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DingTalkController(IDingTalkService dingTalkService, IServiceScopeFactory serviceScopeFactory)
    {
        _dingTalkService = dingTalkService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>获取钉钉全局配置</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var record = DingTalkConfigHelper.GetGlobalConfig();
        if (record == null)
            return Ok(ApiResult<object>.Success(null!));

        return Ok(ApiResult<object>.Success(new
        {
            id = record.Id,
            appKey = record.AppKey,
            appSecret = string.IsNullOrEmpty(record.AppSecret) ? "" : "********", // 不返回明文密钥
            corpId = record.CorpId,
            agentId = record.AgentId ?? "",
            autoSync = record.AutoSync,
            syncCron = record.SyncCron ?? "0 0 2 * * ?",
            lastSyncTime = record.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        }));
    }

    /// <summary>保存钉钉全局配置</summary>
    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("config")]
    public IActionResult SaveConfig([FromBody] SaveDingTalkConfigRequest request)
    {
        // 读取现有配置（如果有）
        var existing = DingTalkConfigHelper.GetGlobalConfig();

        var record = existing ?? new DingTalkConfigRecord();
        record.OrgId = null; // 全局配置
        record.ConfigName = "全局配置";
        record.AppKey = request.AppKey ?? "";
        record.CorpId = request.CorpId ?? "";
        record.AgentId = request.AgentId;

        // AppSecret：如果前端传的是 "********"，说明没改，保留原值
        if (!string.IsNullOrEmpty(request.AppSecret) && request.AppSecret != "********")
        {
            record.AppSecret = DingTalkConfigHelper.EncryptSecret(request.AppSecret);
        }
        // 如果是新建且没传 secret，置空
        else if (existing == null)
        {
            record.AppSecret = "";
        }
        // else: 保留 existing.AppSecret 不变

        DingTalkConfigHelper.SaveConfig(record);
        return Ok(ApiResult.Ok("配置保存成功"));
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("pull/departments")]
    public async Task<ApiResult<List<DingTalkDepartmentDto>>> PullDepartments()
    {
        var result = await _dingTalkService.PullDepartmentsAsync();
        return ApiResult<List<DingTalkDepartmentDto>>.Success(result);
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("pull/users")]
    public async Task<ApiResult<List<DingTalkUserDto>>> PullUsers()
    {
        var result = await _dingTalkService.PullUsersAsync();
        return ApiResult<List<DingTalkUserDto>>.Success(result);
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("pull/positions")]
    public async Task<ApiResult<List<DingTalkPositionDto>>> PullPositions()
    {
        var result = await _dingTalkService.PullPositionsAsync();
        return ApiResult<List<DingTalkPositionDto>>.Success(result);
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("sync/full")]
    public IActionResult FullSync()
    {
        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDingTalkService>();
            try
            {
                await service.FullSyncFromDingTalkAsync();
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DingTalkController>>();
                logger.LogError(ex, "后台钉钉全量同步失败");
                // 错误已通过 SignalR 通知前端，此处仅记录日志
            }
        });
        return Ok(ApiResult<string>.Success("同步已开始，请关注实时进度"));
    }

    /// <summary>同步指定钉钉用户</summary>
    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("sync/specific-users")]
    public async Task<IActionResult> SyncSpecificUsers([FromBody] SyncSpecificUsersRequest request)
    {
        if (request?.DingTalkUserIds == null || request.DingTalkUserIds.Count == 0)
            return BadRequest(ApiResult<string>.Fail("请指定至少一个钉钉用户ID"));

        if (request.DingTalkUserIds.Count > 50)
            return BadRequest(ApiResult<string>.Fail("单次最多同步50个用户"));

        var result = await _dingTalkService.SyncSpecificUsersAsync(request.DingTalkUserIds);
        return Ok(ApiResult<SyncResultDto>.Success(result));
    }

    /// <summary>获取当前同步状态</summary>
    [HttpGet("sync-status")]
    public IActionResult GetSyncStatus()
    {
        var status = DingTalkService.GetSyncStatus();
        if (status == null || !status.IsSyncing)
        {
            return Ok(ApiResult<object>.Success(new { isSyncing = false }));
        }
        return Ok(ApiResult<object>.Success(new
        {
            isSyncing = true,
            stage = status.Stage,
            message = status.Message,
            current = status.Current,
            total = status.Total,
            percent = status.Percent,
            startTime = status.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        }));
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var token = await _dingTalkService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
                return Ok(ApiResult<string>.Success("连接成功"));
            return BadRequest(ApiResult.Fail("获取AccessToken失败，请检查配置"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResult.Fail($"连接失败: {ex.Message}"));
        }
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("bind/organization")]
    public async Task<ApiResult> BindOrganization([FromBody] BindOrganizationRequest request)
    {
        await _dingTalkService.BindOrganizationAsync(request);
        return ApiResult.Ok("绑定成功");
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("unbind/organization/{id}")]
    public async Task<ApiResult> UnbindOrganization(long id)
    {
        await _dingTalkService.UnbindOrganizationAsync(id);
        return ApiResult.Ok("解绑成功");
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("bind/user")]
    public async Task<ApiResult> BindUser([FromBody] BindUserRequest request)
    {
        await _dingTalkService.BindUserAsync(request);
        return ApiResult.Ok("绑定成功");
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("unbind/user/{id}")]
    public async Task<ApiResult> UnbindUser(long id)
    {
        await _dingTalkService.UnbindUserAsync(id);
        return ApiResult.Ok("解绑成功");
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("bind/position")]
    public async Task<ApiResult> BindPosition([FromBody] BindPositionRequest request)
    {
        await _dingTalkService.BindPositionAsync(request);
        return ApiResult.Ok("绑定成功");
    }

    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("unbind/position/{id}")]
    public async Task<ApiResult> UnbindPosition(long id)
    {
        await _dingTalkService.UnbindPositionAsync(id);
        return ApiResult.Ok("解绑成功");
    }

    /// <summary>
    /// 获取定时同步配置
    /// </summary>
    [HttpGet("auto-sync")]
    public IActionResult GetAutoSync()
    {
        var config = DingTalkConfigHelper.GetGlobalConfig();
        return Ok(ApiResult<object>.Success(new
        {
            enabled = config?.AutoSync == 1,
            cronExpression = config?.SyncCron ?? "0 0 2 * * ?"
        }));
    }

    /// <summary>
    /// 更新定时同步配置（即时生效）
    /// </summary>
    [RequirePermission(SystemPermissions.DingTalkManage)]
    [HttpPost("auto-sync")]
    public IActionResult UpdateAutoSync([FromBody] UpdateAutoSyncRequest request)
    {
        var config = DingTalkConfigHelper.GetGlobalConfig();
        if (config == null)
            return Ok(ApiResult.Fail("请先保存钉钉应用配置"));

        // 验证Cron表达式
        if (request.Enabled && string.IsNullOrWhiteSpace(request.CronExpression))
            return Ok(ApiResult.Fail("请设置同步频率"));

        // 更新配置
        DingTalkConfigHelper.UpdateAutoSync(
            config.Id,
            request.Enabled ? 1 : 0,
            request.CronExpression ?? "0 0 2 * * ?"
        );

        // 动态注册/移除Hangfire任务
        if (request.Enabled)
        {
            RecurringJob.AddOrUpdate<IDingTalkService>(
                "dingtalk-auto-sync",
                service => service.FullSyncFromDingTalkAsync(),
                request.CronExpression ?? "0 0 2 * * ?");
        }
        else
        {
            RecurringJob.RemoveIfExists("dingtalk-auto-sync");
        }

        return Ok(ApiResult.Ok(request.Enabled ? "定时同步已开启" : "定时同步已关闭"));
    }
}
