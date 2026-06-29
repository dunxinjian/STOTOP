using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// CF自动插件管理 API
/// 提供插件注册列表 + 插件规则列表查询，供流程节点配置使用。
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/auto-plugin")]
public class CfAutoPluginController : ControllerBase
{
    private readonly STOTOPDbContext _dbContext;

    public CfAutoPluginController(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>获取全局已启用的插件注册列表</summary>
    /// <param name="granularity">可选：按处理粒度过滤（card / batch）</param>
    [HttpGet("registry")]
    public async Task<ApiResult<object>> GetRegistry([FromQuery] string? granularity = null)
    {
        var query = _dbContext.Set<CfAutoPluginRegistry>()
            .Where(r => r.F状态 == 1);

        if (!string.IsNullOrEmpty(granularity))
            query = query.Where(r => r.F处理粒度 == granularity);

        var list = await query
            .OrderBy(r => r.F插件类型)
            .ThenBy(r => r.F插件编码)
            .Select(r => new
            {
                id = r.FID,
                pluginCode = r.F插件编码,
                pluginName = r.F插件名称,
                pluginType = r.F插件类型,
                granularity = r.F处理粒度,
                description = r.F说明
            })
            .ToListAsync();

        return ApiResult<object>.Success(list);
    }

    /// <summary>根据插件编码获取匹配的规则列表（仅启用规则）</summary>
    /// <param name="pluginCode">插件编码（如 AutoVoucher、ExcelInput），对应 CfPluginRule.F类型编码</param>
    [HttpGet("rules")]
    public async Task<ApiResult<object>> GetRules([FromQuery] string pluginCode)
    {
        if (string.IsNullOrEmpty(pluginCode))
            return ApiResult<object>.Fail("pluginCode is required");

        var list = await _dbContext.Set<CfPluginRule>()
            .Where(r => r.F类型编码 == pluginCode && r.F状态 == 1)
            .OrderBy(r => r.F规则名称)
            .Select(r => new
            {
                id = r.FID,
                ruleName = r.F规则名称,
                pluginCode = r.F类型编码,
                description = r.F说明
            })
            .ToListAsync();

        return ApiResult<object>.Success(list);
    }
}
