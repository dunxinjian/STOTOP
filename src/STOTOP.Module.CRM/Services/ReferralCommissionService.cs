using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class ReferralCommissionService : IReferralCommissionService
{
    private readonly IRepository<CrmExternalContact> _contactRepo;
    private readonly IRepository<CrmReferral> _referralRepo;
    private readonly IRepository<CrmCommission> _commissionRepo;
    private readonly IRepository<CrmCustomer> _customerRepo;
    private readonly IRepository<CrmCustomerProfit> _profitRepo;
    private readonly STOTOPDbContext _db;

    public ReferralCommissionService(
        IRepository<CrmExternalContact> contactRepo,
        IRepository<CrmReferral> referralRepo,
        IRepository<CrmCommission> commissionRepo,
        IRepository<CrmCustomer> customerRepo,
        IRepository<CrmCustomerProfit> profitRepo,
        STOTOPDbContext db)
    {
        _contactRepo = contactRepo;
        _referralRepo = referralRepo;
        _commissionRepo = commissionRepo;
        _customerRepo = customerRepo;
        _profitRepo = profitRepo;
        _db = db;
    }

    #region External Contacts

    public async Task<PagedResult<ExternalContactDto>> GetExternalContactsAsync(ExternalContactQueryRequest request)
    {
        var query = _contactRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(c => c.FName.Contains(kw) || (c.FPhone != null && c.FPhone.Contains(kw)) || (c.FCompany != null && c.FCompany.Contains(kw)));
        }

        if (request.Status.HasValue)
            query = query.Where(c => c.FStatus == request.Status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ExternalContactDto>
        {
            Items = items.Select(MapExternalContactToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ExternalContactDto?> GetExternalContactByIdAsync(long id)
    {
        var entity = await _contactRepo.Query().FirstOrDefaultAsync(c => c.FID == id);
        return entity == null ? null : MapExternalContactToDto(entity);
    }

    public async Task<ExternalContactDto> CreateExternalContactAsync(CreateExternalContactRequest request)
    {
        var entity = new CrmExternalContact
        {
            FName = request.Name,
            FPhone = request.Phone,
            FCompany = request.Company,
            FBankAccount = request.BankAccount,
            FBankName = request.BankName,
            FRemark = request.Remark,
            FStatus = 0,
            FCreatedTime = DateTime.Now
        };

        await _contactRepo.AddAsync(entity);
        return MapExternalContactToDto(entity);
    }

    public async Task<ExternalContactDto?> UpdateExternalContactAsync(long id, UpdateExternalContactRequest request)
    {
        var entity = await _contactRepo.Query().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (entity == null) return null;

        entity.FName = request.Name;
        entity.FPhone = request.Phone;
        entity.FCompany = request.Company;
        entity.FBankAccount = request.BankAccount;
        entity.FBankName = request.BankName;
        entity.FRemark = request.Remark;
        entity.FStatus = request.Status;
        entity.FUpdatedTime = DateTime.Now;

        await _contactRepo.UpdateAsync(entity);
        return MapExternalContactToDto(entity);
    }

    public async Task<bool> DeleteExternalContactAsync(long id)
    {
        var entity = await _contactRepo.GetByIdAsync(id);
        if (entity == null) return false;
        await _contactRepo.DeleteAsync(id);
        return true;
    }

    #endregion

    #region Referrals

    public async Task<PagedResult<ReferralDto>> GetReferralsAsync(ReferralQueryRequest request)
    {
        var query = _referralRepo.Query()
            .Include(r => r.Customer)
            .Include(r => r.ExternalContact)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(r => r.FCustomerId == request.CustomerId);
        if (request.OrgId.HasValue)
            query = query.Where(r => r.FOrgId == request.OrgId.Value);
        if (request.ReferrerType.HasValue)
            query = query.Where(r => r.FReferrerType == request.ReferrerType.Value);
        if (request.EmployeeId.HasValue)
            query = query.Where(r => r.FEmployeeId == request.EmployeeId.Value);
        if (request.ExternalContactId.HasValue)
            query = query.Where(r => r.FExternalContactId == request.ExternalContactId.Value);
        if (request.StartDate.HasValue)
            query = query.Where(r => r.FReferralDate >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(r => r.FReferralDate <= request.EndDate.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ReferralDto>
        {
            Items = items.Select(MapReferralToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ReferralDto?> GetReferralByIdAsync(long id)
    {
        var entity = await _referralRepo.Query()
            .Include(r => r.Customer)
            .Include(r => r.ExternalContact)
            .FirstOrDefaultAsync(r => r.FID == id);

        return entity == null ? null : MapReferralToDto(entity);
    }

    public async Task<ReferralDto> CreateReferralAsync(CreateReferralRequest request)
    {
        var entity = new CrmReferral
        {
            FCustomerId = request.CustomerId,
            FOrgId = request.OrgId ?? 0,
            FReferrerType = request.ReferrerType,
            FEmployeeId = request.EmployeeId,
            FExternalContactId = request.ExternalContactId,
            FReferralDate = request.ReferralDate,
            FDescription = request.Description,
            FCommissionRate = request.CommissionRate,
            FCreatedTime = DateTime.Now
        };

        await _referralRepo.AddAsync(entity);
        return (await GetReferralByIdAsync(entity.FID))!;
    }

    public async Task<bool> DeleteReferralAsync(long id)
    {
        var entity = await _referralRepo.GetByIdAsync(id);
        if (entity == null) return false;
        await _referralRepo.DeleteAsync(id);
        return true;
    }

    #endregion

    #region Commissions

    public async Task<PagedResult<CommissionDto>> GetCommissionsAsync(CommissionQueryRequest request)
    {
        var query = _commissionRepo.Query()
            .Include(c => c.Customer)
            .Include(c => c.Referral)
            .AsQueryable();

        if (request.ReferralId.HasValue)
            query = query.Where(c => c.FReferralId == request.ReferralId.Value);
        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(c => c.FCustomerId == request.CustomerId);
        if (request.Status.HasValue)
            query = query.Where(c => c.FStatus == request.Status.Value);
        if (request.OrgId.HasValue)
            query = query.Where(c => c.Referral.FOrgId == request.OrgId.Value);
        if (request.StartDate.HasValue)
            query = query.Where(c => c.FCreatedTime >= request.StartDate.Value.ToDateTime(TimeOnly.MinValue));
        if (request.EndDate.HasValue)
            query = query.Where(c => c.FCreatedTime <= request.EndDate.Value.ToDateTime(TimeOnly.MaxValue));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<CommissionDto>
        {
            Items = items.Select(MapCommissionToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CommissionDto?> GetCommissionByIdAsync(long id)
    {
        var entity = await _commissionRepo.Query()
            .Include(c => c.Customer)
            .Include(c => c.Referral)
            .FirstOrDefaultAsync(c => c.FID == id);

        return entity == null ? null : MapCommissionToDto(entity);
    }

    public async Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request)
    {
        var entity = new CrmCommission
        {
            FReferralId = request.ReferralId,
            FCustomerId = request.CustomerId,
            FContractId = request.ContractId,
            FCommissionAmount = request.CommissionAmount,
            FCalcBasis = request.CalcBasis,
            FApplicantId = request.ApplicantId,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now
        };

        await _commissionRepo.AddAsync(entity);
        return (await GetCommissionByIdAsync(entity.FID))!;
    }

    public async Task<bool> UpdateCommissionStatusAsync(long id, int status)
    {
        var entity = await _commissionRepo.Query().AsTracking().FirstOrDefaultAsync(c => c.FID == id);
        if (entity == null) return false;

        entity.FStatus = status;
        entity.FUpdatedTime = DateTime.Now;
        await _commissionRepo.UpdateAsync(entity);
        return true;
    }

    /// <summary>
    /// 根据客户毛利数据和推荐记录的返佣比例计算返佣金额
    /// </summary>
    public async Task<CalcCommissionResultDto> CalcCommissionAsync(CalcCommissionRequest request)
    {
        var referral = await _referralRepo.Query()
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.FID == request.ReferralId)
            ?? throw new InvalidOperationException("推荐记录不存在");

        var commissionRate = referral.FCommissionRate
            ?? throw new InvalidOperationException("推荐记录未设置返佣比例");

        // 查询指定期间范围的客户毛利数据，以收入作为计算基础
        var profitQuery = _profitRepo.Query()
            .Where(p => p.FCustomerId == request.CustomerId);

        if (!string.IsNullOrWhiteSpace(request.StartPeriod))
            profitQuery = profitQuery.Where(p => string.Compare(p.FPeriod, request.StartPeriod) >= 0);
        if (!string.IsNullOrWhiteSpace(request.EndPeriod))
            profitQuery = profitQuery.Where(p => string.Compare(p.FPeriod, request.EndPeriod) <= 0);

        var totalRevenue = await profitQuery.SumAsync(p => p.FRevenue);
        var calcAmount = Math.Round(totalRevenue * commissionRate / 100m, 2);

        var calcBasis = $"客户[{referral.Customer?.FShortName}] 期间{request.StartPeriod}~{request.EndPeriod} 总收入{totalRevenue:N2}元 × 返佣比例{commissionRate}% = {calcAmount:N2}元";

        return new CalcCommissionResultDto
        {
            ReferralId = request.ReferralId,
            CustomerId = request.CustomerId,
            CustomerName = referral.Customer?.FShortName,
            CommissionRate = commissionRate,
            TotalRevenue = totalRevenue,
            CalcAmount = calcAmount,
            CalcBasis = calcBasis
        };
    }

    /// <summary>
    /// 提交返佣申请审批
    /// </summary>
    public async Task<CommissionDto> SubmitForApprovalAsync(SubmitCommissionRequest request, long userId)
    {
        var commission = await _commissionRepo.Query().AsTracking()
            .FirstOrDefaultAsync(c => c.FID == request.CommissionId)
            ?? throw new InvalidOperationException("返佣申请不存在");

        if (commission.FStatus != 0)
            throw new InvalidOperationException("只有草稿状态的返佣申请可以提交审批");

        throw new NotSupportedException("BPM流程已废除，请通过 CardFlow 发起返佣审批。");
    }

    /// <summary>
    /// 审批回调：更新返佣申请状态
    /// </summary>
    public async Task HandleApprovalCallbackAsync(ApprovalCallbackRequest request)
    {
        var commission = await _commissionRepo.Query().AsTracking()
            .FirstOrDefaultAsync(c => c.FID == request.CommissionId)
            ?? throw new InvalidOperationException("返佣申请不存在");

        if (commission.FStatus != 1)
            throw new InvalidOperationException("只有审批中的返佣申请可以回调");

        commission.FStatus = request.Approved ? 2 : 4; // 2=已批准 4=已驳回
        commission.FUpdatedTime = DateTime.Now;
        await _commissionRepo.UpdateAsync(commission);

    }

    #endregion

    #region Statistics

    public async Task<List<ReferralStatisticsDto>> GetStatisticsByReferrerAsync(long? orgId, DateOnly? startDate, DateOnly? endDate)
    {
        var referralQuery = _referralRepo.Query()
            .Include(r => r.ExternalContact)
            .Include(r => r.Commissions)
            .AsQueryable();

        if (orgId.HasValue)
            referralQuery = referralQuery.Where(r => r.FOrgId == orgId.Value);
        if (startDate.HasValue)
            referralQuery = referralQuery.Where(r => r.FReferralDate >= startDate.Value);
        if (endDate.HasValue)
            referralQuery = referralQuery.Where(r => r.FReferralDate <= endDate.Value);

        var referrals = await referralQuery.ToListAsync();

        // Group by referrer (external contact or employee)
        var grouped = referrals.GroupBy(r => new { r.FReferrerType, r.FEmployeeId, r.FExternalContactId });

        return grouped.Select(g =>
        {
            var first = g.First();
            var allCommissions = g.SelectMany(r => r.Commissions).ToList();

            return new ReferralStatisticsDto
            {
                ReferrerId = first.FReferrerType == 1 ? first.FEmployeeId : first.FExternalContactId,
                ReferrerName = first.FReferrerType == 2 ? first.ExternalContact?.FName : null,
                ReferralCount = g.Count(),
                TotalCommission = allCommissions.Sum(c => c.FCommissionAmount),
                PaidCommission = allCommissions.Where(c => c.FStatus == 3).Sum(c => c.FCommissionAmount) // 3=已付款
            };
        }).OrderByDescending(s => s.TotalCommission).ToList();
    }

    #endregion

    #region Mapping

    private static ExternalContactDto MapExternalContactToDto(CrmExternalContact entity) => new()
    {
        Id = entity.FID,
        Name = entity.FName,
        Phone = entity.FPhone,
        Company = entity.FCompany,
        BankAccount = entity.FBankAccount,
        BankName = entity.FBankName,
        Remark = entity.FRemark,
        Status = entity.FStatus,
        CreatorName = entity.FCreatorName,
        CreatedTime = entity.FCreatedTime,
        UpdaterName = entity.FUpdaterName,
        UpdatedTime = entity.FUpdatedTime
    };

    private static ReferralDto MapReferralToDto(CrmReferral entity) => new()
    {
        Id = entity.FID,
        CustomerId = entity.FCustomerId,
        CustomerName = entity.Customer?.FShortName,
        OrgId = entity.FOrgId,
        ReferrerType = entity.FReferrerType,
        EmployeeId = entity.FEmployeeId,
        ExternalContactId = entity.FExternalContactId,
        ExternalContactName = entity.ExternalContact?.FName,
        ReferralDate = entity.FReferralDate,
        Description = entity.FDescription,
        CommissionRate = entity.FCommissionRate,
        CreatorName = entity.FCreatorName,
        CreatedTime = entity.FCreatedTime
    };

    private static CommissionDto MapCommissionToDto(CrmCommission entity) => new()
    {
        Id = entity.FID,
        ReferralId = entity.FReferralId,
        CustomerId = entity.FCustomerId,
        CustomerName = entity.Customer?.FShortName,
        ContractId = entity.FContractId,
        CommissionAmount = entity.FCommissionAmount,
        CalcBasis = entity.FCalcBasis,
        ApplicantId = entity.FApplicantId,
        Status = entity.FStatus,
        OaProcessInstanceId = entity.FOaProcessInstanceId,
        PaymentOrderId = entity.FPaymentOrderId,
        CreatorName = entity.FCreatorName,
        CreatedTime = entity.FCreatedTime
    };

    #endregion
}
