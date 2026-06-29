using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using SystemIO = global::System.IO;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/migration")]
public class MigrationController : ControllerBase
{
    private readonly STOTOPDbContext _ctx;
    private readonly MigrationMappingService _mappingService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MigrationController(
        STOTOPDbContext ctx,
        MigrationMappingService mappingService,
        IHttpContextAccessor httpContextAccessor)
    {
        _ctx = ctx;
        _mappingService = mappingService;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    #region 方案 CRUD

    [HttpGet("schemes")]
    public async Task<ApiResult<List<MigrationSchemeDto>>> GetSchemes([FromQuery] long? accountSetId)
    {
        var query = _ctx.Set<FinMigrationScheme>().AsNoTracking();
        if (accountSetId.HasValue)
            query = query.Where(s => s.F目标账套ID == accountSetId.Value);

        var orgId = GetCurrentOrgId();
        if (orgId > 0)
            query = query.Where(s => s.F组织ID == orgId || s.F组织ID == 0);

        var schemes = await query.OrderByDescending(s => s.F更新时间).ToListAsync();

        var result = schemes.Select(s => new MigrationSchemeDto
        {
            Id = s.FID,
            Name = s.F方案名称,
            SourceAccountSetId = s.F源账套标识,
            TargetAccountSetId = s.F目标账套ID,
            AuxMissingStrategy = s.F辅助项缺失策略,
            Description = s.F说明,
            Status = s.F状态,
            OrgId = s.F组织ID,
            CreatedTime = s.F创建时间,
            UpdatedTime = s.F更新时间,
            AccountMappingCount = s.AccountMappings.Count,
            AuxMappingCount = s.AuxMappings.Count,
            AssetMappingCount = s.AssetMappings.Count
        }).ToList();

        // 补充映射计数
        var schemeIds = schemes.Select(s => s.FID).ToList();
        var accountCounts = await _ctx.Set<FinAccountMappingDetail>()
            .Where(m => schemeIds.Contains(m.F方案ID))
            .GroupBy(m => m.F方案ID)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToListAsync();
        var auxCounts = await _ctx.Set<FinAuxMappingDetail>()
            .Where(m => schemeIds.Contains(m.F方案ID))
            .GroupBy(m => m.F方案ID)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToListAsync();
        var assetCounts = await _ctx.Set<FinAssetMappingDetail>()
            .Where(m => schemeIds.Contains(m.F方案ID))
            .GroupBy(m => m.F方案ID)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var dto in result)
        {
            dto.AccountMappingCount = accountCounts.FirstOrDefault(c => c.SchemeId == dto.Id)?.Count ?? 0;
            dto.AuxMappingCount = auxCounts.FirstOrDefault(c => c.SchemeId == dto.Id)?.Count ?? 0;
            dto.AssetMappingCount = assetCounts.FirstOrDefault(c => c.SchemeId == dto.Id)?.Count ?? 0;
        }

        return ApiResult<List<MigrationSchemeDto>>.Success(result);
    }

    [HttpGet("schemes/{id}")]
    public async Task<ApiResult<MigrationSchemeDto>> GetScheme(Guid id)
    {
        var s = await _ctx.Set<FinMigrationScheme>().AsNoTracking().FirstOrDefaultAsync(x => x.FID == id);
        if (s == null) return ApiResult<MigrationSchemeDto>.Fail("方案不存在");

        var dto = new MigrationSchemeDto
        {
            Id = s.FID,
            Name = s.F方案名称,
            SourceAccountSetId = s.F源账套标识,
            TargetAccountSetId = s.F目标账套ID,
            AuxMissingStrategy = s.F辅助项缺失策略,
            Description = s.F说明,
            Status = s.F状态,
            OrgId = s.F组织ID,
            CreatedTime = s.F创建时间,
            UpdatedTime = s.F更新时间,
            AccountMappingCount = await _ctx.Set<FinAccountMappingDetail>().CountAsync(m => m.F方案ID == id),
            AuxMappingCount = await _ctx.Set<FinAuxMappingDetail>().CountAsync(m => m.F方案ID == id),
            AssetMappingCount = await _ctx.Set<FinAssetMappingDetail>().CountAsync(m => m.F方案ID == id)
        };

        return ApiResult<MigrationSchemeDto>.Success(dto);
    }

    [HttpPost("schemes")]
    public async Task<ApiResult<MigrationSchemeDto>> CreateScheme([FromBody] CreateMigrationSchemeRequest request)
    {
        var now = DateTime.Now;
        var scheme = new FinMigrationScheme
        {
            FID = Guid.NewGuid(),
            F方案名称 = request.Name,
            F源账套标识 = request.SourceAccountSetId,
            F目标账套ID = request.TargetAccountSetId,
            F辅助项缺失策略 = request.AuxMissingStrategy,
            F说明 = request.Description,
            F状态 = 1,
            F组织ID = GetCurrentOrgId(),
            F创建时间 = now,
            F更新时间 = now
        };

        _ctx.Set<FinMigrationScheme>().Add(scheme);
        await _ctx.SaveChangesAsync();

        return ApiResult<MigrationSchemeDto>.Success(new MigrationSchemeDto
        {
            Id = scheme.FID,
            Name = scheme.F方案名称,
            SourceAccountSetId = scheme.F源账套标识,
            TargetAccountSetId = scheme.F目标账套ID,
            AuxMissingStrategy = scheme.F辅助项缺失策略,
            Description = scheme.F说明,
            Status = scheme.F状态,
            OrgId = scheme.F组织ID,
            CreatedTime = scheme.F创建时间,
            UpdatedTime = scheme.F更新时间
        }, "创建方案成功");
    }

    [HttpPut("schemes/{id}")]
    public async Task<ApiResult<MigrationSchemeDto>> UpdateScheme(Guid id, [FromBody] UpdateMigrationSchemeRequest request)
    {
        var scheme = await _ctx.Set<FinMigrationScheme>().FirstOrDefaultAsync(x => x.FID == id);
        if (scheme == null) return ApiResult<MigrationSchemeDto>.Fail("方案不存在");

        if (request.Name != null) scheme.F方案名称 = request.Name;
        if (request.SourceAccountSetId != null) scheme.F源账套标识 = request.SourceAccountSetId;
        if (request.TargetAccountSetId.HasValue) scheme.F目标账套ID = request.TargetAccountSetId.Value;
        if (request.AuxMissingStrategy != null) scheme.F辅助项缺失策略 = request.AuxMissingStrategy;
        if (request.Description != null) scheme.F说明 = request.Description;
        if (request.Status.HasValue) scheme.F状态 = request.Status.Value;
        scheme.F更新时间 = DateTime.Now;

        await _ctx.SaveChangesAsync();

        return ApiResult<MigrationSchemeDto>.Success(new MigrationSchemeDto
        {
            Id = scheme.FID,
            Name = scheme.F方案名称,
            SourceAccountSetId = scheme.F源账套标识,
            TargetAccountSetId = scheme.F目标账套ID,
            AuxMissingStrategy = scheme.F辅助项缺失策略,
            Description = scheme.F说明,
            Status = scheme.F状态,
            OrgId = scheme.F组织ID,
            CreatedTime = scheme.F创建时间,
            UpdatedTime = scheme.F更新时间
        }, "更新方案成功");
    }

    [HttpDelete("schemes/{id}")]
    public async Task<ApiResult> DeleteScheme(Guid id)
    {
        var scheme = await _ctx.Set<FinMigrationScheme>().FirstOrDefaultAsync(x => x.FID == id);
        if (scheme == null) return ApiResult.Fail("方案不存在");

        _ctx.Set<FinMigrationScheme>().Remove(scheme);
        await _ctx.SaveChangesAsync();
        return ApiResult.Ok("删除方案成功");
    }

    #endregion

    #region 科目映射 CRUD

    [HttpGet("account-mappings")]
    public async Task<ApiResult<List<AccountMappingDto>>> GetAccountMappings([FromQuery] Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAccountMappingDetail>()
            .Where(m => m.F方案ID == schemeId)
            .OrderBy(m => m.F源科目编码).ThenBy(m => m.F优先级)
            .AsNoTracking()
            .ToListAsync();

        var result = mappings.Select(m => new AccountMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            SourceCode = m.F源科目编码,
            SourceName = m.F源科目名称,
            TargetAccountId = m.F目标科目ID,
            TargetCode = m.F目标科目编码,
            TargetName = m.F目标科目名称,
            MappingType = m.F映射类型,
            ConditionJson = m.F条件JSON,
            Priority = m.F优先级,
            Description = m.F说明,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AccountMappingDto>>.Success(result);
    }

    [HttpPost("account-mappings")]
    public async Task<ApiResult<List<AccountMappingDto>>> CreateAccountMappings([FromBody] BatchCreateAccountMappingRequest request)
    {
        var now = DateTime.Now;
        var entities = request.Items.Select(item => new FinAccountMappingDetail
        {
            FID = Guid.NewGuid(),
            F方案ID = item.SchemeId,
            F源科目编码 = item.SourceCode,
            F源科目名称 = item.SourceName,
            F目标科目ID = item.TargetAccountId,
            F目标科目编码 = item.TargetCode,
            F目标科目名称 = item.TargetName,
            F映射类型 = item.MappingType,
            F条件JSON = item.ConditionJson,
            F优先级 = item.Priority,
            F说明 = item.Description,
            F状态 = 1,
            F创建时间 = now
        }).ToList();

        _ctx.Set<FinAccountMappingDetail>().AddRange(entities);
        await _ctx.SaveChangesAsync();

        var result = entities.Select(m => new AccountMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            SourceCode = m.F源科目编码,
            SourceName = m.F源科目名称,
            TargetAccountId = m.F目标科目ID,
            TargetCode = m.F目标科目编码,
            TargetName = m.F目标科目名称,
            MappingType = m.F映射类型,
            ConditionJson = m.F条件JSON,
            Priority = m.F优先级,
            Description = m.F说明,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AccountMappingDto>>.Success(result, "创建科目映射成功");
    }

    [HttpPut("account-mappings/{id}")]
    public async Task<ApiResult<AccountMappingDto>> UpdateAccountMapping(Guid id, [FromBody] UpdateAccountMappingRequest request)
    {
        var mapping = await _ctx.Set<FinAccountMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult<AccountMappingDto>.Fail("映射记录不存在");

        if (request.SourceCode != null) mapping.F源科目编码 = request.SourceCode;
        if (request.SourceName != null) mapping.F源科目名称 = request.SourceName;
        if (request.TargetAccountId.HasValue) mapping.F目标科目ID = request.TargetAccountId;
        if (request.TargetCode != null) mapping.F目标科目编码 = request.TargetCode;
        if (request.TargetName != null) mapping.F目标科目名称 = request.TargetName;
        if (request.MappingType.HasValue) mapping.F映射类型 = request.MappingType.Value;
        if (request.ConditionJson != null) mapping.F条件JSON = request.ConditionJson;
        if (request.Priority.HasValue) mapping.F优先级 = request.Priority.Value;
        if (request.Description != null) mapping.F说明 = request.Description;
        if (request.Status.HasValue) mapping.F状态 = request.Status.Value;

        await _ctx.SaveChangesAsync();

        return ApiResult<AccountMappingDto>.Success(new AccountMappingDto
        {
            Id = mapping.FID,
            SchemeId = mapping.F方案ID,
            SourceCode = mapping.F源科目编码,
            SourceName = mapping.F源科目名称,
            TargetAccountId = mapping.F目标科目ID,
            TargetCode = mapping.F目标科目编码,
            TargetName = mapping.F目标科目名称,
            MappingType = mapping.F映射类型,
            ConditionJson = mapping.F条件JSON,
            Priority = mapping.F优先级,
            Description = mapping.F说明,
            Status = mapping.F状态,
            CreatedTime = mapping.F创建时间
        }, "更新科目映射成功");
    }

    [HttpDelete("account-mappings/{id}")]
    public async Task<ApiResult> DeleteAccountMapping(Guid id)
    {
        var mapping = await _ctx.Set<FinAccountMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult.Fail("映射记录不存在");

        _ctx.Set<FinAccountMappingDetail>().Remove(mapping);
        await _ctx.SaveChangesAsync();
        return ApiResult.Ok("删除科目映射成功");
    }

    [HttpPost("account-mappings/import")]
    public async Task<ApiResult<List<AccountMappingDto>>> ImportAccountMappings(
        [FromQuery] Guid schemeId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResult<List<AccountMappingDto>>.Fail("请上传文件");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(ms) : new XSSFWorkbook(ms);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null || sheet.LastRowNum < 1)
            return ApiResult<List<AccountMappingDto>>.Fail("文件为空或无数据行");

        // 读取表头
        var headerRow = sheet.GetRow(0);
        var colMap = new Dictionary<string, int>();
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var val = headerRow.GetCell(c)?.StringCellValue?.Trim();
            if (!string.IsNullOrEmpty(val)) colMap[val] = c;
        }

        var now = DateTime.Now;
        var entities = new List<FinAccountMappingDetail>();

        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            var sourceCode = GetCellString(row, colMap, "源科目编码");
            if (string.IsNullOrEmpty(sourceCode)) continue;

            entities.Add(new FinAccountMappingDetail
            {
                FID = Guid.NewGuid(),
                F方案ID = schemeId,
                F源科目编码 = sourceCode,
                F源科目名称 = GetCellString(row, colMap, "源科目名称"),
                F目标科目编码 = GetCellString(row, colMap, "目标科目编码") ?? "",
                F目标科目名称 = GetCellString(row, colMap, "目标科目名称") ?? "",
                F映射类型 = 1,
                F优先级 = 10,
                F说明 = GetCellString(row, colMap, "说明"),
                F状态 = 1,
                F创建时间 = now
            });
        }

        _ctx.Set<FinAccountMappingDetail>().AddRange(entities);
        await _ctx.SaveChangesAsync();

        var result = entities.Select(m => new AccountMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            SourceCode = m.F源科目编码,
            SourceName = m.F源科目名称,
            TargetCode = m.F目标科目编码,
            TargetName = m.F目标科目名称,
            MappingType = m.F映射类型,
            Priority = m.F优先级,
            Description = m.F说明,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AccountMappingDto>>.Success(result, $"导入成功，共{result.Count}条");
    }

    [HttpGet("account-mappings/export")]
    public async Task<IActionResult> ExportAccountMappings([FromQuery] Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAccountMappingDetail>()
            .Where(m => m.F方案ID == schemeId)
            .OrderBy(m => m.F源科目编码)
            .AsNoTracking()
            .ToListAsync();

        IWorkbook workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("科目映射");

        var headers = new[] { "源科目编码", "源科目名称", "目标科目编码", "目标科目名称", "映射类型", "优先级", "说明" };
        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
            headerRow.CreateCell(i).SetCellValue(headers[i]);

        for (int i = 0; i < mappings.Count; i++)
        {
            var row = sheet.CreateRow(i + 1);
            var m = mappings[i];
            row.CreateCell(0).SetCellValue(m.F源科目编码);
            row.CreateCell(1).SetCellValue(m.F源科目名称 ?? "");
            row.CreateCell(2).SetCellValue(m.F目标科目编码);
            row.CreateCell(3).SetCellValue(m.F目标科目名称);
            row.CreateCell(4).SetCellValue(m.F映射类型 == 1 ? "直接" : "条件");
            row.CreateCell(5).SetCellValue(m.F优先级);
            row.CreateCell(6).SetCellValue(m.F说明 ?? "");
        }

        using var stream = new MemoryStream();
        workbook.Write(stream, true);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "科目映射.xlsx");
    }

    #endregion

    #region 辅助映射 CRUD

    [HttpGet("aux-mappings")]
    public async Task<ApiResult<List<AuxMappingDto>>> GetAuxMappings([FromQuery] Guid schemeId, [FromQuery] string? auxType = null)
    {
        var query = _ctx.Set<FinAuxMappingDetail>()
            .Where(m => m.F方案ID == schemeId);
        if (!string.IsNullOrEmpty(auxType))
            query = query.Where(m => m.F辅助类型 == auxType);

        var mappings = await query.OrderBy(m => m.F辅助类型).ThenBy(m => m.F源编码)
            .AsNoTracking().ToListAsync();

        var result = mappings.Select(m => new AuxMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            AuxType = m.F辅助类型,
            SourceCode = m.F源编码,
            SourceName = m.F源名称,
            TargetAuxItemId = m.F目标辅助项目ID,
            TargetCode = m.F目标编码,
            TargetName = m.F目标名称,
            Strategy = m.F处理策略,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AuxMappingDto>>.Success(result);
    }

    [HttpPost("aux-mappings")]
    public async Task<ApiResult<List<AuxMappingDto>>> CreateAuxMappings([FromBody] BatchCreateAuxMappingRequest request)
    {
        var now = DateTime.Now;
        var entities = request.Items.Select(item => new FinAuxMappingDetail
        {
            FID = Guid.NewGuid(),
            F方案ID = item.SchemeId,
            F辅助类型 = item.AuxType,
            F源编码 = item.SourceCode,
            F源名称 = item.SourceName,
            F目标辅助项目ID = item.TargetAuxItemId,
            F目标编码 = item.TargetCode,
            F目标名称 = item.TargetName,
            F处理策略 = item.Strategy,
            F状态 = 1,
            F创建时间 = now
        }).ToList();

        _ctx.Set<FinAuxMappingDetail>().AddRange(entities);
        await _ctx.SaveChangesAsync();

        var result = entities.Select(m => new AuxMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            AuxType = m.F辅助类型,
            SourceCode = m.F源编码,
            SourceName = m.F源名称,
            TargetAuxItemId = m.F目标辅助项目ID,
            TargetCode = m.F目标编码,
            TargetName = m.F目标名称,
            Strategy = m.F处理策略,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AuxMappingDto>>.Success(result, "创建辅助映射成功");
    }

    [HttpPut("aux-mappings/{id}")]
    public async Task<ApiResult<AuxMappingDto>> UpdateAuxMapping(Guid id, [FromBody] UpdateAuxMappingRequest request)
    {
        var mapping = await _ctx.Set<FinAuxMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult<AuxMappingDto>.Fail("映射记录不存在");

        if (request.AuxType != null) mapping.F辅助类型 = request.AuxType;
        if (request.SourceCode != null) mapping.F源编码 = request.SourceCode;
        if (request.SourceName != null) mapping.F源名称 = request.SourceName;
        if (request.TargetAuxItemId.HasValue) mapping.F目标辅助项目ID = request.TargetAuxItemId;
        if (request.TargetCode != null) mapping.F目标编码 = request.TargetCode;
        if (request.TargetName != null) mapping.F目标名称 = request.TargetName;
        if (request.Strategy != null) mapping.F处理策略 = request.Strategy;
        if (request.Status.HasValue) mapping.F状态 = request.Status.Value;

        await _ctx.SaveChangesAsync();

        return ApiResult<AuxMappingDto>.Success(new AuxMappingDto
        {
            Id = mapping.FID,
            SchemeId = mapping.F方案ID,
            AuxType = mapping.F辅助类型,
            SourceCode = mapping.F源编码,
            SourceName = mapping.F源名称,
            TargetAuxItemId = mapping.F目标辅助项目ID,
            TargetCode = mapping.F目标编码,
            TargetName = mapping.F目标名称,
            Strategy = mapping.F处理策略,
            Status = mapping.F状态,
            CreatedTime = mapping.F创建时间
        }, "更新辅助映射成功");
    }

    [HttpDelete("aux-mappings/{id}")]
    public async Task<ApiResult> DeleteAuxMapping(Guid id)
    {
        var mapping = await _ctx.Set<FinAuxMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult.Fail("映射记录不存在");

        _ctx.Set<FinAuxMappingDetail>().Remove(mapping);
        await _ctx.SaveChangesAsync();
        return ApiResult.Ok("删除辅助映射成功");
    }

    #endregion

    #region 资产映射 CRUD

    [HttpGet("asset-mappings")]
    public async Task<ApiResult<List<AssetMappingDto>>> GetAssetMappings([FromQuery] Guid schemeId)
    {
        var mappings = await _ctx.Set<FinAssetMappingDetail>()
            .Where(m => m.F方案ID == schemeId)
            .OrderBy(m => m.F源资产编号)
            .AsNoTracking()
            .ToListAsync();

        var result = mappings.Select(m => new AssetMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            SourceAssetCode = m.F源资产编号,
            TargetAssetCardId = m.F目标资产卡片ID,
            TargetAssetCode = m.F目标资产编号,
            TargetAssetName = m.F目标资产名称,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AssetMappingDto>>.Success(result);
    }

    [HttpPost("asset-mappings")]
    public async Task<ApiResult<List<AssetMappingDto>>> CreateAssetMappings([FromBody] BatchCreateAssetMappingRequest request)
    {
        var now = DateTime.Now;
        var entities = request.Items.Select(item => new FinAssetMappingDetail
        {
            FID = Guid.NewGuid(),
            F方案ID = item.SchemeId,
            F源资产编号 = item.SourceAssetCode,
            F目标资产卡片ID = item.TargetAssetCardId,
            F目标资产编号 = item.TargetAssetCode,
            F目标资产名称 = item.TargetAssetName,
            F状态 = 1,
            F创建时间 = now
        }).ToList();

        _ctx.Set<FinAssetMappingDetail>().AddRange(entities);
        await _ctx.SaveChangesAsync();

        var result = entities.Select(m => new AssetMappingDto
        {
            Id = m.FID,
            SchemeId = m.F方案ID,
            SourceAssetCode = m.F源资产编号,
            TargetAssetCardId = m.F目标资产卡片ID,
            TargetAssetCode = m.F目标资产编号,
            TargetAssetName = m.F目标资产名称,
            Status = m.F状态,
            CreatedTime = m.F创建时间
        }).ToList();

        return ApiResult<List<AssetMappingDto>>.Success(result, "创建资产映射成功");
    }

    [HttpPut("asset-mappings/{id}")]
    public async Task<ApiResult<AssetMappingDto>> UpdateAssetMapping(Guid id, [FromBody] UpdateAssetMappingRequest request)
    {
        var mapping = await _ctx.Set<FinAssetMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult<AssetMappingDto>.Fail("映射记录不存在");

        if (request.SourceAssetCode != null) mapping.F源资产编号 = request.SourceAssetCode;
        if (request.TargetAssetCardId.HasValue) mapping.F目标资产卡片ID = request.TargetAssetCardId;
        if (request.TargetAssetCode != null) mapping.F目标资产编号 = request.TargetAssetCode;
        if (request.TargetAssetName != null) mapping.F目标资产名称 = request.TargetAssetName;
        if (request.Status.HasValue) mapping.F状态 = request.Status.Value;

        await _ctx.SaveChangesAsync();

        return ApiResult<AssetMappingDto>.Success(new AssetMappingDto
        {
            Id = mapping.FID,
            SchemeId = mapping.F方案ID,
            SourceAssetCode = mapping.F源资产编号,
            TargetAssetCardId = mapping.F目标资产卡片ID,
            TargetAssetCode = mapping.F目标资产编号,
            TargetAssetName = mapping.F目标资产名称,
            Status = mapping.F状态,
            CreatedTime = mapping.F创建时间
        }, "更新资产映射成功");
    }

    [HttpDelete("asset-mappings/{id}")]
    public async Task<ApiResult> DeleteAssetMapping(Guid id)
    {
        var mapping = await _ctx.Set<FinAssetMappingDetail>().FirstOrDefaultAsync(m => m.FID == id);
        if (mapping == null) return ApiResult.Fail("映射记录不存在");

        _ctx.Set<FinAssetMappingDetail>().Remove(mapping);
        await _ctx.SaveChangesAsync();
        return ApiResult.Ok("删除资产映射成功");
    }

    #endregion

    #region 向导接口

    /// <summary>向导上传文件临时目录（parse-columns 保存，后续步骤通过 fileId 复用）</summary>
    private static readonly string WizardTempDir = Path.Combine(Path.GetTempPath(), "stotop-voucher-migration");

    /// <summary>根据 fileId 或 filePath 解析实际文件路径，找不到返回 null</summary>
    private static string? ResolveWizardFilePath(string? fileId, string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && SystemIO.File.Exists(filePath)) return filePath;
        if (!string.IsNullOrEmpty(fileId))
        {
            // fileId 仅允许 GUID+扩展名，防止路径穿越
            var safe = Path.GetFileName(fileId);
            var p = Path.Combine(WizardTempDir, safe);
            if (SystemIO.File.Exists(p)) return p;
        }
        return null;
    }

    /// <summary>根据列名关键词推断角色：凭证日期/凭证号/科目编码/借方金额 等</summary>
    private static string InferColumnRole(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        // 科目相关优先
        if (name.Contains("科目"))
        {
            if (name.Contains("编码") || name.Contains("代码") || name.Contains("编号")) return "accountCode";
            if (name.Contains("名称")) return "accountName";
        }

        // 资产
        if (name.Contains("资产") && (name.Contains("编号") || name.Contains("编码") || name.Contains("代码") || name.Contains("卡片")))
            return "assetCode";

        // 辅助核算
        if (name.Contains("辅助")) return "auxiliary";

        // 借贷金额
        if (name.Contains("借方") || (name.Contains("借") && name.Contains("金额"))) return "debitAmount";
        if (name.Contains("贷方") || (name.Contains("贷") && name.Contains("金额"))) return "creditAmount";

        // 凭证字段组合
        if (name.Contains("凭证"))
        {
            if (name.Contains("日期")) return "voucherDate";
            if (name.Contains("字")) return "voucherWord";
            if (name.Contains("号")) return "voucherNo";
        }

        if (name.Contains("日期")) return "voucherDate";
        if (name.Contains("摘要") || name.Contains("说明")) return "summary";

        return string.Empty;
    }

    [HttpPost("wizard/parse-columns")]
    public async Task<ApiResult<object>> ParseColumns(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResult<object>.Fail("请上传文件");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xls" && ext != ".xlsx")
            return ApiResult<object>.Fail("仅支持 .xls / .xlsx 文件");

        // 保存到临时目录，后续步骤通过 fileId 复用
        SystemIO.Directory.CreateDirectory(WizardTempDir);
        var fileId = Guid.NewGuid().ToString("N") + ext;
        var filePath = Path.Combine(WizardTempDir, fileId);
        using (var write = new SystemIO.FileStream(filePath, SystemIO.FileMode.Create, SystemIO.FileAccess.Write))
        {
            await file.CopyToAsync(write);
        }

        using var fs = new SystemIO.FileStream(filePath, SystemIO.FileMode.Open, SystemIO.FileAccess.Read, SystemIO.FileShare.Read);
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(fs) : new XSSFWorkbook(fs);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null) return ApiResult<object>.Fail("文件为空");

        var headerRow = sheet.GetRow(0);
        if (headerRow == null) return ApiResult<object>.Fail("无法读取表头行");

        var columns = new List<object>();
        var maxCol = (int)headerRow.LastCellNum;
        var sampleRowLimit = Math.Min(5, sheet.LastRowNum);
        for (int c = 0; c < maxCol; c++)
        {
            var name = headerRow.GetCell(c)?.ToString()?.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var samples = new List<string>();
            for (int r = 1; r <= sampleRowLimit; r++)
            {
                var v = sheet.GetRow(r)?.GetCell(c)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(v)) samples.Add(v);
            }

            columns.Add(new
            {
                name,
                sampleValues = samples,
                suggestedRole = InferColumnRole(name)
            });
        }

        return ApiResult<object>.Success(new { fileId, columns });
    }

    [HttpPost("wizard/extract-subjects")]
    public async Task<ApiResult<ExtractSubjectsResponse>> ExtractSubjects([FromBody] ExtractSubjectsRequest request)
    {
        var filePath = ResolveWizardFilePath(request.FileId, request.FilePath);
        if (string.IsNullOrEmpty(filePath))
            return ApiResult<ExtractSubjectsResponse>.Fail("源文件不存在或已过期，请返回第一步重新上传");

        if (string.IsNullOrEmpty(request.SubjectCodeColumn))
            return ApiResult<ExtractSubjectsResponse>.Fail("未指定科目编码列");

        using var fs = new SystemIO.FileStream(filePath, SystemIO.FileMode.Open, SystemIO.FileAccess.Read, SystemIO.FileShare.Read);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(fs) : new XSSFWorkbook(fs);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null)
            return ApiResult<ExtractSubjectsResponse>.Fail("文件为空");

        // 读表头获取列索引
        var headerRow = sheet.GetRow(0);
        var colMap = new Dictionary<string, int>();
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var val = headerRow.GetCell(c)?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val)) colMap[val] = c;
        }

        if (!colMap.ContainsKey(request.SubjectCodeColumn))
            return ApiResult<ExtractSubjectsResponse>.Fail($"列 '{request.SubjectCodeColumn}' 不存在");

        var codeCol = colMap[request.SubjectCodeColumn];
        var nameCol = !string.IsNullOrEmpty(request.SubjectNameColumn) && colMap.ContainsKey(request.SubjectNameColumn)
            ? colMap[request.SubjectNameColumn] : (int?)null;

        var subjectCounts = new Dictionary<string, (string? name, int count)>();
        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            var code = row.GetCell(codeCol)?.ToString()?.Trim();
            if (string.IsNullOrEmpty(code)) continue;

            var name = nameCol.HasValue ? row.GetCell(nameCol.Value)?.ToString()?.Trim() : null;

            if (subjectCounts.TryGetValue(code, out var existing))
            {
                subjectCounts[code] = (existing.name ?? name, existing.count + 1);
            }
            else
            {
                subjectCounts[code] = (name, 1);
            }
        }

        var subjects = subjectCounts.Select(kv => new SourceSubjectInfo
        {
            Code = kv.Key,
            Name = kv.Value.name,
            Count = kv.Value.count
        }).OrderBy(s => s.Code).ToList();

        return ApiResult<ExtractSubjectsResponse>.Success(new ExtractSubjectsResponse { Subjects = subjects });
    }

    [HttpPost("wizard/auto-match")]
    public async Task<ApiResult<MigrationAutoMatchResponse>> AutoMatch([FromBody] AutoMatchRequest request)
    {
        var result = await _mappingService.AutoMatchSubjectsAsync(
            request.SchemeId, request.TargetAccountSetId, request.Subjects);
        return ApiResult<MigrationAutoMatchResponse>.Success(result);
    }

    [HttpPost("wizard/preview")]
    public async Task<ApiResult<WizardPreviewResponse>> Preview([FromBody] WizardPreviewRequest request)
    {
        var filePath = ResolveWizardFilePath(request.FileId, request.FilePath);
        if (string.IsNullOrEmpty(filePath))
            return ApiResult<WizardPreviewResponse>.Fail("源文件不存在或已过期，请返回第一步重新上传");

        // 加载映射
        var accountMappings = await _mappingService.LoadAccountMappingsAsync(request.SchemeId);

        using var fs = new SystemIO.FileStream(filePath, SystemIO.FileMode.Open, SystemIO.FileAccess.Read, SystemIO.FileShare.Read);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(fs) : new XSSFWorkbook(fs);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null)
            return ApiResult<WizardPreviewResponse>.Fail("文件为空");

        var headerRow = sheet.GetRow(0);
        var colMap = new Dictionary<string, int>();
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var val = headerRow.GetCell(c)?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val)) colMap[val] = c;
        }

        var previews = new List<WizardPreviewItem>();
        var roles = request.ColumnRoles;
        var maxRows = Math.Min(request.Rows, sheet.LastRowNum);

        for (int r = 1; r <= maxRows; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            var originalRow = new Dictionary<string, string?>();
            foreach (var kv in colMap)
            {
                originalRow[kv.Key] = row.GetCell(kv.Value)?.ToString()?.Trim();
            }

            try
            {
                var sourceCode = GetValueByRole(row, colMap, roles.SubjectCodeColumn);
                var resolved = _mappingService.ResolveAccount(
                    sourceCode ?? "",
                    accountMappings,
                    GetValueByRole(row, colMap, roles.SummaryColumn));

                previews.Add(new WizardPreviewItem
                {
                    OriginalRow = originalRow,
                    Converted = new WizardConvertedVoucher
                    {
                        VoucherDate = GetValueByRole(row, colMap, roles.VoucherDateColumn),
                        VoucherNo = GetValueByRole(row, colMap, roles.VoucherNoColumn),
                        VoucherWord = GetValueByRole(row, colMap, roles.VoucherWordColumn),
                        Summary = GetValueByRole(row, colMap, roles.SummaryColumn),
                        TargetAccountCode = resolved?.targetCode,
                        TargetAccountName = resolved?.targetName,
                        DebitAmount = ParseDecimal(GetValueByRole(row, colMap, roles.DebitColumn)),
                        CreditAmount = ParseDecimal(GetValueByRole(row, colMap, roles.CreditColumn))
                    }
                });
            }
            catch (Exception ex)
            {
                previews.Add(new WizardPreviewItem
                {
                    OriginalRow = originalRow,
                    Error = ex.Message
                });
            }
        }

        return ApiResult<WizardPreviewResponse>.Success(new WizardPreviewResponse { Previews = previews });
    }

    [HttpPost("wizard/commit")]
    public async Task<ApiResult<WizardCommitResponse>> Commit([FromBody] WizardCommitRequest request)
    {
        try
        {
            await _mappingService.CommitWizardAsync(request);
            return ApiResult<WizardCommitResponse>.Success(new WizardCommitResponse { Success = true }, "配置保存成功");
        }
        catch (Exception ex)
        {
            return ApiResult<WizardCommitResponse>.Fail(ex.Message);
        }
    }

    #endregion

    #region 私有辅助方法

    private static string? GetCellString(IRow row, Dictionary<string, int> colMap, string colName)
    {
        if (!colMap.TryGetValue(colName, out var idx)) return null;
        return row.GetCell(idx)?.ToString()?.Trim();
    }

    private static string? GetValueByRole(IRow row, Dictionary<string, int> colMap, string? columnName)
    {
        if (string.IsNullOrEmpty(columnName)) return null;
        if (!colMap.TryGetValue(columnName, out var idx)) return null;
        return row.GetCell(idx)?.ToString()?.Trim();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return decimal.TryParse(value, out var result) ? result : null;
    }

    #endregion
}
