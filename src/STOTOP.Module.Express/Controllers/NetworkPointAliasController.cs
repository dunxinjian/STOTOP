using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 网点名称映射管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/network-point-aliases")]
public class NetworkPointAliasController : ControllerBase
{
    private readonly IRepository<ExpNetworkPointAlias> _aliasRepo;
    private readonly IRepository<ExpNetworkPoint> _networkPointRepo;

    public NetworkPointAliasController(
        IRepository<ExpNetworkPointAlias> aliasRepo,
        IRepository<ExpNetworkPoint> networkPointRepo)
    {
        _aliasRepo = aliasRepo;
        _networkPointRepo = networkPointRepo;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>分页查询映射列表</summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<NetworkPointAliasDto>>> GetList([FromQuery] NetworkPointAliasQueryRequest request)
    {
        var query = _aliasRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(a => a.FName.Contains(kw) || a.FNetworkPointCode.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(request.NetworkPointCode))
        {
            query = query.Where(a => a.FNetworkPointCode == request.NetworkPointCode);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new NetworkPointAliasDto
            {
                Id = a.FID,
                Name = a.FName,
                NetworkPointCode = a.FNetworkPointCode,
                OrgId = a.FOrgId
            })
            .ToListAsync();

        // JOIN 获取网点名称
        var codes = items.Select(i => i.NetworkPointCode).Distinct().ToList();
        var nameMap = await _networkPointRepo.Query()
            .Where(np => codes.Contains(np.FCode))
            .ToDictionaryAsync(np => np.FCode, np => np.FFullName ?? np.FCode);

        foreach (var item in items)
        {
            item.NetworkPointName = nameMap.GetValueOrDefault(item.NetworkPointCode);
        }

        return ApiResult<PagedResult<NetworkPointAliasDto>>.Success(new PagedResult<NetworkPointAliasDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    /// <summary>新增单条映射</summary>
    [HttpPost]
    public async Task<ApiResult<NetworkPointAliasDto>> Create([FromBody] CreateNetworkPointAliasRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<NetworkPointAliasDto>.Fail("名称不能为空");

        var networkPoint = await _networkPointRepo.Query()
            .FirstOrDefaultAsync(np => np.FCode == request.NetworkPointCode);
        if (networkPoint == null)
            return ApiResult<NetworkPointAliasDto>.Fail("网点编号不存在");

        var orgId = GetOrgId();

        // 检查唯一约束
        var exists = await _aliasRepo.Query()
            .AnyAsync(a => a.FName == request.Name && a.FOrgId == orgId);
        if (exists)
            return ApiResult<NetworkPointAliasDto>.Fail("该名称映射已存在");

        var entity = new ExpNetworkPointAlias
        {
            FName = request.Name.Trim(),
            FNetworkPointCode = request.NetworkPointCode,
            FOrgId = orgId
        };

        var created = await _aliasRepo.AddAsync(entity);

        return ApiResult<NetworkPointAliasDto>.Success(new NetworkPointAliasDto
        {
            Id = created.FID,
            Name = created.FName,
            NetworkPointCode = created.FNetworkPointCode,
            NetworkPointName = networkPoint.FFullName ?? networkPoint.FCode,
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
    public async Task<ApiResult<BatchCreateNetworkPointAliasResultDto>> BatchCreate([FromBody] BatchCreateNetworkPointAliasRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return ApiResult<BatchCreateNetworkPointAliasResultDto>.Fail("items不能为空");

        var orgId = GetOrgId();
        var successCount = 0;
        var skippedCount = 0;

        // 获取所有涉及的网点编号，批量验证
        var codes = request.Items.Select(i => i.NetworkPointCode).Distinct().ToList();
        var validCodes = await _networkPointRepo.Query()
            .Where(np => codes.Contains(np.FCode))
            .Select(np => np.FCode)
            .ToListAsync();
        var validCodeSet = new HashSet<string>(validCodes);

        // 获取已存在的名称映射
        var names = request.Items.Select(i => i.Name).Distinct().ToList();
        var existingNames = await _aliasRepo.Query()
            .Where(a => names.Contains(a.FName) && a.FOrgId == orgId)
            .Select(a => a.FName)
            .ToListAsync();
        var existingNameSet = new HashSet<string>(existingNames);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || !validCodeSet.Contains(item.NetworkPointCode))
            {
                skippedCount++;
                continue;
            }

            if (existingNameSet.Contains(item.Name))
            {
                skippedCount++;
                continue;
            }

            var entity = new ExpNetworkPointAlias
            {
                FName = item.Name.Trim(),
                FNetworkPointCode = item.NetworkPointCode,
                FOrgId = orgId
            };

            await _aliasRepo.AddAsync(entity);
            existingNameSet.Add(item.Name); // 防止同批次重复
            successCount++;
        }

        return ApiResult<BatchCreateNetworkPointAliasResultDto>.Success(new BatchCreateNetworkPointAliasResultDto
        {
            SuccessCount = successCount,
            SkippedCount = skippedCount
        });
    }
}
