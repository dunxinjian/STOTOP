using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class BudgetExpenseMappingService : IBudgetExpenseMappingService
{
    private readonly IRepository<FinBudgetExpenseMapping> _mappingRepository;

    public BudgetExpenseMappingService(IRepository<FinBudgetExpenseMapping> mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    public async Task<List<BudgetExpenseMappingDto>> GetMappingsAsync(long accountSetId, long? orgId)
    {
        if (accountSetId <= 0) return new List<BudgetExpenseMappingDto>();

        var query = _mappingRepository.Query()
            .Where(m => m.FAccountSetId == accountSetId);

        if (orgId.HasValue)
        {
            query = query.Where(m => m.FOrgId == orgId.Value || m.FOrgId == null);
        }

        var mappings = await query
            .OrderBy(m => m.FOrgId.HasValue ? 0 : 1)
            .ThenBy(m => m.FExpenseType)
            .ToListAsync();

        return mappings.Select(Map).ToList();
    }

    public async Task<BudgetExpenseMappingDto> SaveMappingAsync(BudgetExpenseMappingDto dto)
    {
        NormalizeAndValidate(dto);

        var entity = await _mappingRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(m =>
                m.FAccountSetId == dto.AccountSetId &&
                m.FOrgId == dto.OrgId &&
                m.FExpenseType == dto.ExpenseType);

        var now = DateTime.Now;
        if (entity == null)
        {
            entity = new FinBudgetExpenseMapping
            {
                FAccountSetId = dto.AccountSetId,
                FOrgId = dto.OrgId,
                FExpenseType = dto.ExpenseType,
                FCreatedTime = now
            };
            Apply(entity, dto, now);
            await _mappingRepository.AddAsync(entity);
        }
        else
        {
            Apply(entity, dto, now);
            await _mappingRepository.UpdateAsync(entity);
        }

        return Map(entity);
    }

    public async Task<BudgetExpenseMappingDto?> ResolveAsync(long accountSetId, long? orgId, string expenseType)
    {
        if (accountSetId <= 0 || string.IsNullOrWhiteSpace(expenseType))
        {
            return null;
        }

        var normalizedExpenseType = expenseType.Trim();
        if (orgId.HasValue)
        {
            var orgMapping = await _mappingRepository.Query()
                .FirstOrDefaultAsync(m =>
                    m.FAccountSetId == accountSetId &&
                    m.FStatus == 1 &&
                    m.FOrgId == orgId.Value &&
                    m.FExpenseType == normalizedExpenseType);

            if (orgMapping != null)
            {
                return Map(orgMapping);
            }
        }

        var globalMapping = await _mappingRepository.Query()
            .FirstOrDefaultAsync(m =>
                m.FAccountSetId == accountSetId &&
                m.FStatus == 1 &&
                m.FOrgId == null &&
                m.FExpenseType == normalizedExpenseType);

        return globalMapping == null ? null : Map(globalMapping);
    }

    private static void NormalizeAndValidate(BudgetExpenseMappingDto dto)
    {
        if (dto.AccountSetId <= 0)
        {
            throw new InvalidOperationException("账套ID不能为空");
        }

        if (string.IsNullOrWhiteSpace(dto.ExpenseType))
        {
            throw new InvalidOperationException("费用类型不能为空");
        }

        dto.ExpenseType = dto.ExpenseType.Trim();
        dto.AccountCode = NormalizeNullable(dto.AccountCode);
        dto.CashCategory = string.IsNullOrWhiteSpace(dto.CashCategory)
            ? "expense_reimbursement"
            : dto.CashCategory.Trim();
        dto.Remark = NormalizeNullable(dto.Remark);

        var hasAccountCode = !string.IsNullOrWhiteSpace(dto.AccountCode);
        var hasPLItem = dto.PLItemId.HasValue && dto.PLItemId.Value > 0;
        if (!hasAccountCode && !hasPLItem)
        {
            throw new InvalidOperationException("费用映射需配置科目编码或损益项");
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void Apply(FinBudgetExpenseMapping entity, BudgetExpenseMappingDto dto, DateTime now)
    {
        entity.FAccountSetId = dto.AccountSetId;
        entity.FOrgId = dto.OrgId;
        entity.FExpenseType = dto.ExpenseType;
        entity.FAccountCode = dto.AccountCode;
        entity.FPLItemId = dto.PLItemId;
        entity.FCashCategory = dto.CashCategory;
        entity.FStatus = dto.Status;
        entity.FRemark = dto.Remark;
        entity.FUpdatedTime = now;
    }

    private static BudgetExpenseMappingDto Map(FinBudgetExpenseMapping entity)
    {
        return new BudgetExpenseMappingDto
        {
            Id = entity.FID,
            AccountSetId = entity.FAccountSetId,
            OrgId = entity.FOrgId,
            ExpenseType = entity.FExpenseType,
            AccountCode = entity.FAccountCode,
            PLItemId = entity.FPLItemId,
            CashCategory = entity.FCashCategory,
            Status = entity.FStatus,
            Remark = entity.FRemark
        };
    }
}
