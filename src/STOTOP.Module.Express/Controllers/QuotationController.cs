using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 报价方案管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/quotations")]
public class QuotationController : ControllerBase
{
    private readonly IQuotationService _quotationService;
    private readonly IPricePlanImportService _importService;
    private readonly IShopService _shopService;
    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IRepository<ExpQuotationCommission> _commissionRepo;
    private readonly IRepository<ExpQuotationChangeLog> _changeLogRepo;
    private readonly IRepository<ExpQuotationAlias> _aliasRepo;

    public QuotationController(IQuotationService quotationService, IPricePlanImportService importService, IShopService shopService, IRepository<ExpQuotation> quotationRepo, IRepository<ExpQuotationCommission> commissionRepo, IRepository<ExpQuotationChangeLog> changeLogRepo, IRepository<ExpQuotationAlias> aliasRepo)
    {
        _quotationService = quotationService;
        _importService = importService;
        _shopService = shopService;
        _quotationRepo = quotationRepo;
        _commissionRepo = commissionRepo;
        _changeLogRepo = changeLogRepo;
        _aliasRepo = aliasRepo;
    }

    /// <summary>
    /// 校验报价方案是否归属当前组织（_quotationRepo.Query 受全局组织过滤器约束，
    /// 子表均非组织隔离实体，必须经此校验防止跨组织越权读写）。
    /// </summary>
    private async Task<bool> QuotationBelongsToOrgAsync(long quotationId)
        => await _quotationRepo.Query().AnyAsync(q => q.FID == quotationId);

    /// <summary>
    /// 分页查询报价方案列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<QuotationListItemDto>>> GetList([FromQuery] QuotationQueryRequest request)
    {
        var result = await _quotationService.GetListAsync(request);
        return ApiResult<PagedResult<QuotationListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取报价方案详情（含重量段和矩阵明细）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<QuotationDto>> GetById(long id)
    {
        var result = await _quotationService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<QuotationDto>.Fail("报价方案不存在");
        return ApiResult<QuotationDto>.Success(result);
    }

    /// <summary>
    /// 创建报价方案
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<QuotationDto>> Create([FromBody] CreateQuotationRequest request)
    {
        try
        {
            var result = await _quotationService.CreateAsync(request);
            return ApiResult<QuotationDto>.Success(result, "创建报价方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<QuotationDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新报价方案
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<QuotationDto>> Update(long id, [FromBody] UpdateQuotationRequest request)
    {
        try
        {
            var result = await _quotationService.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<QuotationDto>.Fail("报价方案不存在");
            return ApiResult<QuotationDto>.Success(result, "更新报价方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<QuotationDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除报价方案
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _quotationService.DeleteAsync(id);
        if (!result)
            return ApiResult.Fail("报价方案不存在");
        return ApiResult.Ok("删除报价方案成功");
    }

    /// <summary>
    /// 复制报价方案
    /// </summary>
    [HttpPost("{id}/copy")]
    public async Task<ApiResult<QuotationDto>> Copy(long id)
    {
        try
        {
            var result = await _quotationService.CopyPlanAsync(id);
            return ApiResult<QuotationDto>.Success(result, "复制报价方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<QuotationDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 下载报价方案Excel模板
    /// </summary>
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate()
    {
        try
        {
            var bytes = await _importService.GenerateTemplateAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "报价方案模板.xlsx");
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 从 Excel导入报价方案
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ApiResult<QuotationDto>> ImportFromExcel(
        [FromForm] string brandCode,
        [FromForm] string planName,
        IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return ApiResult<QuotationDto>.Fail("请上传Excel文件");

            var result = await _importService.ImportFromExcelAsync(brandCode, planName, file);
            return ApiResult<QuotationDto>.Success(result, "导入报价方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<QuotationDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult<QuotationDto>.Fail($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 业务对象统一查询聚合（含报价数量统计）
    /// </summary>
    [HttpGet("/api/express/client-quotation-summary")]
    public async Task<ApiResult<PagedResult<ClientQuotationSummaryDto>>> GetClientQuotationSummary([FromQuery] ClientQuotationSummaryQuery query)
    {
        var result = await _quotationService.GetClientQuotationSummaryAsync(query);
        return ApiResult<PagedResult<ClientQuotationSummaryDto>>.Success(result);
    }

    /// <summary>
    /// 按店铺名称查询关联的报价方案（按业务对象分组）
    /// </summary>
    [HttpGet("by-shop")]
    public async Task<ApiResult<List<QuotationByShopGroupDto>>> GetQuotationsByShop([FromQuery] string shopName)
    {
        if (string.IsNullOrWhiteSpace(shopName))
            return ApiResult<List<QuotationByShopGroupDto>>.Fail("店铺名称不能为空");
        var result = await _quotationService.GetQuotationsByShopAsync(shopName.Trim());
        return ApiResult<List<QuotationByShopGroupDto>>.Success(result);
    }

    // ==================== 关联店铺 ====================

    /// <summary>
    /// 获取报价方案关联的店铺列表
    /// </summary>
    [HttpGet("{id}/shops")]
    public async Task<ApiResult<List<QuotationShopDto>>> GetShops(long id)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<List<QuotationShopDto>>.Fail("报价方案不存在");
        try
        {
            var result = await _shopService.GetShopsByQuotationIdAsync(id);
            return ApiResult<List<QuotationShopDto>>.Success(result);
        }
        catch (Exception ex)
        {
            return ApiResult<List<QuotationShopDto>>.Fail($"获取关联店铺失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 为报价方案添加关联店铺
    /// </summary>
    [HttpPost("{id}/shops")]
    public async Task<ApiResult> AddShops(long id, [FromBody] AddQuotationShopsRequest request)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult.Fail("报价方案不存在");
        try
        {
            var count = await _shopService.AddShopsToQuotationAsync(id, request.ShopNames);
            return ApiResult.Ok($"成功添加 {count} 个店铺");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 移除报价方案关联的店铺
    /// </summary>
    [HttpDelete("{id}/shops/{shopId}")]
    public async Task<ApiResult> RemoveShop(long id, long shopId)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult.Fail("店铺关联不存在");
        var result = await _shopService.RemoveShopFromQuotationAsync(id, shopId);
        if (!result)
            return ApiResult.Fail("店铺关联不存在");
        return ApiResult.Ok("移除成功");
    }

    /// <summary>
    /// 检查店铺关联冲突
    /// </summary>
    [HttpPost("{id}/shops/check-conflicts")]
    public async Task<ApiResult<List<ShopConflictDto>>> CheckShopConflicts(long id, [FromBody] AddQuotationShopsRequest request)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<List<ShopConflictDto>>.Fail("报价方案不存在");
        var conflicts = await _shopService.CheckShopConflictsAsync(id, request.ShopNames);
        return ApiResult<List<ShopConflictDto>>.Success(conflicts);
    }

    // ==================== 佣金配置 ====================

    private static readonly Dictionary<string, int> CalcMethodToInt = new()
    {
        ["fixed"] = 1,
        ["percent"] = 2,
        ["weight"] = 3,
    };

    private static readonly Dictionary<int, string> CalcMethodToString = new()
    {
        [1] = "fixed",
        [2] = "percent",
        [3] = "weight",
    };

    /// <summary>
    /// 获取报价方案的佣金配置列表
    /// </summary>
    [HttpGet("{id}/commissions")]
    public async Task<ApiResult<List<QuotationCommissionDto>>> GetCommissions(long id)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<List<QuotationCommissionDto>>.Fail("报价方案不存在");
        var entities = await _commissionRepo.Query()
            .Where(c => c.FQuotationId == id)
            .OrderByDescending(c => c.FID)
            .ToListAsync();

        var list = entities.Select(c => new QuotationCommissionDto
        {
            FId = c.FID,
            FQuotationId = c.FQuotationId,
            FEnabled = c.FEnabled,
            FCalcMethod = CalcMethodToString.GetValueOrDefault(c.FCalcMethod, "fixed"),
            FRate = c.FRate,
            FFixedAmount = c.FFixedAmount,
            FWeightAmount = c.FWeightAmount,
            FTargetClientType = c.FTargetClientType,
            FTargetClientId = c.FTargetClientId,
            FCreatedTime = c.FCreatedTime,
        }).ToList();

        return ApiResult<List<QuotationCommissionDto>>.Success(list);
    }

    /// <summary>
    /// 保存佣金配置（新增或更新）
    /// </summary>
    [HttpPost("{id}/commissions")]
    public async Task<ApiResult> SaveCommission(long id, [FromBody] SaveQuotationCommissionRequest request)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult.Fail("报价方案不存在");
        try
        {
            if (request.FId.HasValue && request.FId.Value > 0)
            {
                // 更新
                var entity = await _commissionRepo.GetByIdAsync(request.FId.Value);
                if (entity == null || entity.FQuotationId != id)
                    return ApiResult.Fail("佣金配置不存在");

                entity.FEnabled = request.FEnabled;
                entity.FCalcMethod = CalcMethodToInt.TryGetValue(request.FCalcMethod, out var m) ? m : 1;
                entity.FRate = request.FRate;
                entity.FFixedAmount = request.FFixedAmount;
                entity.FWeightAmount = request.FWeightAmount;
                entity.FTargetClientType = request.FTargetClientType;
                entity.FTargetClientId = request.FTargetClientId;
                await _commissionRepo.UpdateAsync(entity);
            }
            else
            {
                // 新增
                var entity = new ExpQuotationCommission
                {
                    FQuotationId = id,
                    FEnabled = request.FEnabled,
                    FCalcMethod = CalcMethodToInt.TryGetValue(request.FCalcMethod, out var m) ? m : 1,
                    FRate = request.FRate,
                    FFixedAmount = request.FFixedAmount,
                    FWeightAmount = request.FWeightAmount,
                    FTargetClientType = request.FTargetClientType,
                    FTargetClientId = request.FTargetClientId,
                    FCreatedTime = DateTime.Now,
                };
                await _commissionRepo.AddAsync(entity);
            }

            return ApiResult.Ok("保存成功");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"保存佣金配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除佣金配置
    /// </summary>
    [HttpDelete("commissions/{commissionId}")]
    public async Task<ApiResult> DeleteCommission(long commissionId)
    {
        var entity = await _commissionRepo.GetByIdAsync(commissionId);
        if (entity == null)
            return ApiResult.Fail("佣金配置不存在");

        // 越权防护：佣金非组织隔离实体，需校验其所属报价归属当前组织
        if (!await QuotationBelongsToOrgAsync(entity.FQuotationId))
            return ApiResult.Fail("佣金配置不存在");

        await _commissionRepo.DeleteAsync(commissionId);
        return ApiResult.Ok("删除成功");
    }

    // ==================== 变更日志 ====================

    private static readonly Dictionary<int, string> ChangeTypeToFieldName = new()
    {
        [1] = "价格修改",
        [2] = "店铺调整",
        [3] = "条款变更",
        [4] = "状态变更",
    };

    /// <summary>
    /// 获取报价方案的变更日志
    /// </summary>
    [HttpGet("{id}/change-logs")]
    public async Task<ApiResult<List<QuotationChangeLogDto>>> GetChangeLogs(long id)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<List<QuotationChangeLogDto>>.Fail("报价方案不存在");
        var entities = await _changeLogRepo.Query()
            .Where(c => c.FQuotationId == id)
            .OrderByDescending(c => c.FChangeTime)
            .Take(200)
            .ToListAsync();

        var list = entities.Select(c => new QuotationChangeLogDto
        {
            FId = c.FID,
            FQuotationId = c.FQuotationId,
            FFieldName = ChangeTypeToFieldName.GetValueOrDefault(c.FChangeType, "其他变更"),
            FOldValue = c.FBeforeContent,
            FNewValue = c.FAfterContent,
            FChangedBy = c.FChangedBy,
            FChangedTime = c.FChangeTime,
        }).ToList();

        return ApiResult<List<QuotationChangeLogDto>>.Success(list);
    }

    // ==================== 别名管理 ====================

    /// <summary>
    /// 获取报价方案别名列表
    /// </summary>
    [HttpGet("{id}/aliases")]
    public async Task<ApiResult<List<QuotationAliasDto>>> GetAliases(long id)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<List<QuotationAliasDto>>.Fail("报价方案不存在");
        var entities = await _aliasRepo.Query()
            .Where(a => a.FQuotationId == id)
            .OrderByDescending(a => a.FCreatedTime)
            .ToListAsync();

        var list = entities.Select(a => new QuotationAliasDto
        {
            Id = a.FID,
            QuotationId = a.FQuotationId,
            Alias = a.FAlias,
            CreatedTime = a.FCreatedTime,
        }).ToList();

        return ApiResult<List<QuotationAliasDto>>.Success(list);
    }

    /// <summary>
    /// 为报价方案添加别名
    /// </summary>
    [HttpPost("{id}/aliases")]
    public async Task<ApiResult<QuotationAliasDto>> AddAlias(long id, [FromBody] AddQuotationAliasRequest request)
    {
        if (!await QuotationBelongsToOrgAsync(id))
            return ApiResult<QuotationAliasDto>.Fail("报价方案不存在");
        try
        {
            if (string.IsNullOrWhiteSpace(request.Alias))
                return ApiResult<QuotationAliasDto>.Fail("别名不能为空");

            var trimmedAlias = request.Alias.Trim();
            var exists = await _aliasRepo.Query()
                .AnyAsync(a => a.FQuotationId == id && a.FAlias == trimmedAlias);
            if (exists)
                return ApiResult<QuotationAliasDto>.Fail("该别名已存在");

            var entity = new ExpQuotationAlias
            {
                FQuotationId = id,
                FAlias = trimmedAlias,
                FCreatedTime = DateTime.Now,
            };
            await _aliasRepo.AddAsync(entity);

            var dto = new QuotationAliasDto
            {
                Id = entity.FID,
                QuotationId = entity.FQuotationId,
                Alias = entity.FAlias,
                CreatedTime = entity.FCreatedTime,
            };

            return ApiResult<QuotationAliasDto>.Success(dto, "添加别名成功");
        }
        catch (Exception ex)
        {
            return ApiResult<QuotationAliasDto>.Fail($"添加别名失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除报价方案别名
    /// </summary>
    [HttpDelete("aliases/{aliasId}")]
    public async Task<ApiResult> RemoveAlias(long aliasId)
    {
        var entity = await _aliasRepo.GetByIdAsync(aliasId);
        if (entity == null)
            return ApiResult.Fail("别名不存在");

        // 越权防护：别名非组织隔离实体，需校验其所属报价归属当前组织
        if (!await QuotationBelongsToOrgAsync(entity.FQuotationId))
            return ApiResult.Fail("别名不存在");

        await _aliasRepo.DeleteAsync(aliasId);
        return ApiResult.Ok("删除成功");
    }
}
