using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow")]
public class CfHomeController : ControllerBase
{
    private readonly STOTOPDbContext _context;

    public CfHomeController(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>公司/经营单元列表（来自 IMP网点公司 表）</summary>
    [HttpGet("companies")]
    [RequirePermission(CardFlowPermissions.Home)]
    public async Task<ApiResult<List<ImportCompanyDto>>> GetCompanies()
    {
        try
        {
            var list = await _context.Database
                .SqlQueryRaw<ImportCompanyDto>("SELECT FID AS Fid, F公司名 AS FName, F是否经营单元 AS FIsBusinessUnit, F排序 AS FSortOrder FROM [IMP网点公司] ORDER BY F排序")
                .ToListAsync();
            return ApiResult<List<ImportCompanyDto>>.Success(list);
        }
        catch
        {
            // 表可能不存在，返回空列表
            return ApiResult<List<ImportCompanyDto>>.Success(new List<ImportCompanyDto>());
        }
    }

    /// <summary>首页统计数据</summary>
    [HttpGet("home/stats")]
    [RequirePermission(CardFlowPermissions.Home)]
    public async Task<ApiResult<CardFlowHomeDto>> GetStats()
    {
        var today = DateTime.Today;
        var weekAgo = today.AddDays(-7);

        var dto = new CardFlowHomeDto
        {
            Download = new DownloadSummary
            {
                TotalTasks = await _context.Set<CfDownloadTask>().CountAsync(x => x.FStatus == 1),
                TodayExecutions = await _context.Set<CfDownloadLog>().CountAsync(x => x.FStartTime.Date == today),
                RecentFailures = await _context.Set<CfDownloadLog>().CountAsync(x => x.FStatus == 2 && x.FStartTime >= weekAgo),
            },
            Import = new ImportSummary
            {
                FileTypeCount = 0, // DC文件类型已废除
                TodayBatches = await _context.Set<CfBatch>().CountAsync(x => x.FCreatedTime.Date == today),
                PendingRows = 0, // 旧 ImpExpressTransaction 已移除，后续由 STG 暂存表统计
            },
        };

        return ApiResult<CardFlowHomeDto>.Success(dto);
    }
}
