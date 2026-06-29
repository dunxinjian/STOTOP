using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class BudgetService : IBudgetService
{
    private readonly IRepository<FinBudgetVersion> _versionRepository;
    private readonly IRepository<FinBudgetLine> _lineRepository;

    public BudgetService(
        IRepository<FinBudgetVersion> versionRepository,
        IRepository<FinBudgetLine> lineRepository)
    {
        _versionRepository = versionRepository;
        _lineRepository = lineRepository;
    }

    public async Task<List<BudgetVersionDto>> GetVersionsAsync(long accountSetId, int? year)
    {
        if (accountSetId <= 0) return new List<BudgetVersionDto>();

        var query = _versionRepository.Query()
            .Where(v => v.FAccountSetId == accountSetId);

        if (year.HasValue)
        {
            query = query.Where(v => v.FYear == year.Value);
        }

        var versions = await query
            .OrderByDescending(v => v.FYear)
            .ThenByDescending(v => v.FCreatedTime)
            .ToListAsync();

        return versions.Select(MapVersion).ToList();
    }

    public async Task<BudgetVersionDto> CreateVersionAsync(CreateBudgetVersionRequest request, string? operatorName)
    {
        ValidateCreateVersion(request);

        var name = request.Name.Trim();
        var scenarioType = NormalizeScenarioType(request.ScenarioType);
        var exists = await _versionRepository.Query()
            .AnyAsync(v =>
                v.FAccountSetId == request.AccountSetId &&
                v.FYear == request.Year &&
                v.FScenarioType == scenarioType &&
                v.FName == name);

        if (exists)
        {
            throw new InvalidOperationException("同一账套、年度、场景下已存在同名预算版本");
        }

        var now = DateTime.Now;
        var entity = new FinBudgetVersion
        {
            FAccountSetId = request.AccountSetId,
            FName = name,
            FScenarioType = scenarioType,
            FYear = request.Year,
            FStatus = "draft",
            FOwnerOrgId = request.OwnerOrgId,
            FOrgId = request.OwnerOrgId,
            FCreatedBy = operatorName,
            FCreatedTime = now,
            FUpdatedTime = now
        };

        await _versionRepository.AddAsync(entity);
        return MapVersion(entity);
    }

    public async Task SubmitVersionAsync(long id)
    {
        var version = await _versionRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (version == null)
        {
            throw new InvalidOperationException("预算版本不存在");
        }

        if (version.FStatus == "approved")
        {
            throw new InvalidOperationException("已审批预算版本不能重新提交");
        }

        if (version.FStatus == "submitted")
        {
            return;
        }

        version.FStatus = "submitted";
        version.FUpdatedTime = DateTime.Now;
        await _versionRepository.UpdateAsync(version);
    }

    public async Task ApproveVersionAsync(long id, string? operatorName)
    {
        var version = await _versionRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (version == null)
        {
            throw new InvalidOperationException("预算版本不存在");
        }

        if (version.FStatus == "approved")
        {
            return;
        }

        if (version.FStatus != "submitted")
        {
            throw new InvalidOperationException("预算版本需提交后才能审批");
        }

        var now = DateTime.Now;
        version.FStatus = "approved";
        version.FApprovedBy = operatorName;
        version.FApprovedTime = now;
        version.FUpdatedTime = now;
        await _versionRepository.UpdateAsync(version);
    }

    public async Task<List<BudgetLineDto>> GetLinesAsync(long budgetVersionId, string? period, long? orgId)
    {
        var versionExists = await _versionRepository.Query()
            .AnyAsync(v => v.FID == budgetVersionId);

        if (!versionExists)
        {
            throw new InvalidOperationException("预算版本不存在");
        }

        var query = _lineRepository.Query()
            .Where(l => l.FBudgetVersionId == budgetVersionId);

        if (!string.IsNullOrWhiteSpace(period))
        {
            var normalizedPeriod = NormalizePeriod(period);
            query = query.Where(l => l.FPeriod == normalizedPeriod);
        }

        if (orgId.HasValue)
        {
            query = query.Where(l => l.FOrgId == orgId.Value);
        }

        var lines = await query
            .OrderBy(l => l.FPeriod)
            .ThenBy(l => l.FOrgId)
            .ThenBy(l => l.FPLItemId)
            .ThenBy(l => l.FAccountCode)
            .ToListAsync();

        return lines.Select(MapLine).ToList();
    }

    public async Task BatchUpsertLinesAsync(long budgetVersionId, BatchUpsertBudgetLinesRequest request)
    {
        if (request.Lines.Count == 0) return;

        var version = await _versionRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == budgetVersionId);

        if (version == null)
        {
            throw new InvalidOperationException("预算版本不存在");
        }

        if (version.FStatus == "approved")
        {
            throw new InvalidOperationException("已审批预算版本不能修改明细");
        }

        var now = DateTime.Now;
        foreach (var line in request.Lines)
        {
            NormalizeAndValidateLine(budgetVersionId, line);

            FinBudgetLine? entity = null;
            if (line.Id > 0)
            {
                entity = await _lineRepository.Query()
                    .AsTracking()
                    .FirstOrDefaultAsync(l => l.FID == line.Id && l.FBudgetVersionId == budgetVersionId);
            }

            if (entity == null)
            {
                var accountCode = NormalizeNullable(line.AccountCode);
                var dimensionJson = NormalizeNullable(line.DimensionJson);
                entity = await _lineRepository.Query()
                    .AsTracking()
                    .FirstOrDefaultAsync(l =>
                        l.FBudgetVersionId == budgetVersionId &&
                        l.FPeriod == line.Period &&
                        l.FOrgId == line.OrgId &&
                        l.FAmoebaUnitId == line.AmoebaUnitId &&
                        l.FAccountId == line.AccountId &&
                        l.FAccountCode == accountCode &&
                        l.FPLItemId == line.PLItemId &&
                        l.FDimensionJson == dimensionJson);
            }

            if (entity == null)
            {
                entity = new FinBudgetLine
                {
                    FBudgetVersionId = budgetVersionId,
                    FCreatedTime = now
                };
                ApplyLine(entity, line, now);
                await _lineRepository.AddAsync(entity);
            }
            else
            {
                ApplyLine(entity, line, now);
                await _lineRepository.UpdateAsync(entity);
            }
        }

        version.FUpdatedTime = now;
        await _versionRepository.UpdateAsync(version);
    }

    private static void ValidateCreateVersion(CreateBudgetVersionRequest request)
    {
        if (request.AccountSetId <= 0)
        {
            throw new InvalidOperationException("账套ID不能为空");
        }

        if (request.Year <= 0)
        {
            throw new InvalidOperationException("预算年度不能为空");
        }

        if (request.OwnerOrgId <= 0)
        {
            throw new InvalidOperationException("归属组织不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("预算版本名称不能为空");
        }
    }

    private static void NormalizeAndValidateLine(long budgetVersionId, BudgetLineDto line)
    {
        line.BudgetVersionId = budgetVersionId;
        line.Period = NormalizePeriod(line.Period);
        line.AccountCode = NormalizeNullable(line.AccountCode);
        line.DimensionJson = NormalizeNullable(line.DimensionJson);
        line.Remark = NormalizeNullable(line.Remark);

        if (line.OrgId <= 0)
        {
            throw new InvalidOperationException("预算明细组织不能为空");
        }

        var hasAccountId = line.AccountId.HasValue && line.AccountId.Value > 0;
        var hasAccountCode = !string.IsNullOrWhiteSpace(line.AccountCode);
        var hasPLItem = line.PLItemId.HasValue && line.PLItemId.Value > 0;

        if (!hasAccountId && !hasAccountCode && !hasPLItem)
        {
            throw new InvalidOperationException("预算明细需关联科目或损益项");
        }
    }

    private static string NormalizePeriod(string period)
    {
        var value = period.Trim();
        if (value.Length != 6 || !value.All(char.IsDigit))
        {
            throw new InvalidOperationException("预算期间格式应为YYYYMM");
        }

        var month = int.Parse(value.Substring(4, 2));
        if (month < 1 || month > 12)
        {
            throw new InvalidOperationException("预算期间月份必须在01到12之间");
        }

        return value;
    }

    private static string NormalizeScenarioType(string scenarioType)
    {
        return string.IsNullOrWhiteSpace(scenarioType)
            ? "annual_budget"
            : scenarioType.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ApplyLine(FinBudgetLine entity, BudgetLineDto dto, DateTime now)
    {
        entity.FBudgetVersionId = dto.BudgetVersionId;
        entity.FPeriod = dto.Period;
        entity.FOrgId = dto.OrgId;
        entity.FAmoebaUnitId = dto.AmoebaUnitId;
        entity.FAccountId = dto.AccountId;
        entity.FAccountCode = dto.AccountCode;
        entity.FPLItemId = dto.PLItemId;
        entity.FDimensionJson = dto.DimensionJson;
        entity.FAmount = dto.Amount;
        entity.FQuantity = dto.Quantity;
        entity.FUnitPrice = dto.UnitPrice;
        entity.FRemark = dto.Remark;
        entity.FUpdatedTime = now;
    }

    private static BudgetVersionDto MapVersion(FinBudgetVersion entity)
    {
        return new BudgetVersionDto
        {
            Id = entity.FID,
            AccountSetId = entity.FAccountSetId,
            Name = entity.FName,
            ScenarioType = entity.FScenarioType,
            Year = entity.FYear,
            Status = entity.FStatus,
            OwnerOrgId = entity.FOwnerOrgId,
            CreatedBy = entity.FCreatedBy,
            CreatedTime = entity.FCreatedTime,
            ApprovedBy = entity.FApprovedBy,
            ApprovedTime = entity.FApprovedTime
        };
    }

    private static BudgetLineDto MapLine(FinBudgetLine entity)
    {
        return new BudgetLineDto
        {
            Id = entity.FID,
            BudgetVersionId = entity.FBudgetVersionId,
            Period = entity.FPeriod,
            OrgId = entity.FOrgId,
            AmoebaUnitId = entity.FAmoebaUnitId,
            AccountId = entity.FAccountId,
            AccountCode = entity.FAccountCode,
            PLItemId = entity.FPLItemId,
            DimensionJson = entity.FDimensionJson,
            Amount = entity.FAmount,
            Quantity = entity.FQuantity,
            UnitPrice = entity.FUnitPrice,
            Remark = entity.FRemark
        };
    }
}
