using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 业务员名称映射管理（源脏名 → 员工工号）。镜像 NetworkPointAliasController。
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/salesman-aliases")]
public class SalesmanAliasController : ControllerBase
{
    private readonly IRepository<ExpSalesmanAlias> _aliasRepo;
    private readonly IRepository<ExpSalesman> _salesmanRepo;

    public SalesmanAliasController(
        IRepository<ExpSalesmanAlias> aliasRepo,
        IRepository<ExpSalesman> salesmanRepo)
    {
        _aliasRepo = aliasRepo;
        _salesmanRepo = salesmanRepo;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>分页查询映射列表</summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<SalesmanAliasDto>>> GetList([FromQuery] SalesmanAliasQueryRequest request)
    {
        var query = _aliasRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(a => a.FName.Contains(kw) || a.FEmployeeNo.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(request.EmployeeNo))
        {
            query = query.Where(a => a.FEmployeeNo == request.EmployeeNo);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new SalesmanAliasDto
            {
                Id = a.FID,
                Name = a.FName,
                EmployeeNo = a.FEmployeeNo,
                OrgId = a.FOrgId
            })
            .ToListAsync();

        // JOIN 获取员工姓名
        var nos = items.Select(i => i.EmployeeNo).Distinct().ToList();
        var nameMap = await _salesmanRepo.Query()
            .Where(s => nos.Contains(s.FEmployeeNo))
            .ToDictionaryAsync(s => s.FEmployeeNo, s => s.FName);

        foreach (var item in items)
        {
            item.EmployeeName = nameMap.GetValueOrDefault(item.EmployeeNo);
        }

        return ApiResult<PagedResult<SalesmanAliasDto>>.Success(new PagedResult<SalesmanAliasDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    /// <summary>新增单条映射</summary>
    [HttpPost]
    public async Task<ApiResult<SalesmanAliasDto>> Create([FromBody] CreateSalesmanAliasRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<SalesmanAliasDto>.Fail("名称不能为空");

        var salesman = await _salesmanRepo.Query()
            .FirstOrDefaultAsync(s => s.FEmployeeNo == request.EmployeeNo);
        if (salesman == null)
            return ApiResult<SalesmanAliasDto>.Fail("员工工号不存在");

        var orgId = GetOrgId();

        // 检查唯一约束
        var exists = await _aliasRepo.Query()
            .AnyAsync(a => a.FName == request.Name && a.FOrgId == orgId);
        if (exists)
            return ApiResult<SalesmanAliasDto>.Fail("该名称映射已存在");

        var entity = new ExpSalesmanAlias
        {
            FName = request.Name.Trim(),
            FEmployeeNo = request.EmployeeNo,
            FOrgId = orgId
        };

        var created = await _aliasRepo.AddAsync(entity);

        return ApiResult<SalesmanAliasDto>.Success(new SalesmanAliasDto
        {
            Id = created.FID,
            Name = created.FName,
            EmployeeNo = created.FEmployeeNo,
            EmployeeName = salesman.FName,
            OrgId = created.FOrgId
        }, "创建成功");
    }

    /// <summary>删除单条映射</summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var entity = await _aliasRepo.GetByIdAsync(id);
        if (entity == null)
            return ApiResult.Fail("映射记录不存在");

        await _aliasRepo.DeleteAsync(id);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>批量新增映射</summary>
    [HttpPost("batch")]
    public async Task<ApiResult<BatchCreateSalesmanAliasResultDto>> BatchCreate([FromBody] BatchCreateSalesmanAliasRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return ApiResult<BatchCreateSalesmanAliasResultDto>.Fail("items不能为空");

        var orgId = GetOrgId();
        var successCount = 0;
        var skippedCount = 0;

        // 获取所有涉及的员工工号，批量验证
        var nos = request.Items.Select(i => i.EmployeeNo).Distinct().ToList();
        var validNos = await _salesmanRepo.Query()
            .Where(s => nos.Contains(s.FEmployeeNo))
            .Select(s => s.FEmployeeNo)
            .ToListAsync();
        var validNoSet = new HashSet<string>(validNos);

        // 获取已存在的名称映射
        var names = request.Items.Select(i => i.Name).Distinct().ToList();
        var existingNames = await _aliasRepo.Query()
            .Where(a => names.Contains(a.FName) && a.FOrgId == orgId)
            .Select(a => a.FName)
            .ToListAsync();
        var existingNameSet = new HashSet<string>(existingNames);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || !validNoSet.Contains(item.EmployeeNo))
            {
                skippedCount++;
                continue;
            }

            if (existingNameSet.Contains(item.Name))
            {
                skippedCount++;
                continue;
            }

            var entity = new ExpSalesmanAlias
            {
                FName = item.Name.Trim(),
                FEmployeeNo = item.EmployeeNo,
                FOrgId = orgId
            };

            await _aliasRepo.AddAsync(entity);
            existingNameSet.Add(item.Name); // 防止同批次重复
            successCount++;
        }

        return ApiResult<BatchCreateSalesmanAliasResultDto>.Success(new BatchCreateSalesmanAliasResultDto
        {
            SuccessCount = successCount,
            SkippedCount = skippedCount
        });
    }
}
