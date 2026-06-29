using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class SettlementService : ISettlementService
{
    private readonly IRepository<InsSettlement> _settlementRepo;
    private readonly IRepository<InsApprovalConfig> _approvalConfigRepo;
    private readonly IRepository<InsApprovalRecord> _approvalRecordRepo;
    private readonly IRepository<InsClaim> _claimRepo;
    private readonly IRepository<InsCoInsuranceFund> _fundRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SettlementService(
        IRepository<InsSettlement> settlementRepo,
        IRepository<InsApprovalConfig> approvalConfigRepo,
        IRepository<InsApprovalRecord> approvalRecordRepo,
        IRepository<InsClaim> claimRepo,
        IRepository<InsCoInsuranceFund> fundRepo,
        IHttpContextAccessor httpContextAccessor)
    {
        _settlementRepo = settlementRepo;
        _approvalConfigRepo = approvalConfigRepo;
        _approvalRecordRepo = approvalRecordRepo;
        _claimRepo = claimRepo;
        _fundRepo = fundRepo;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentUserId()
    {
        var userIdObj = _httpContextAccessor.HttpContext?.Items["CurrentUserId"];
        if (userIdObj is long userId) return userId;
        return 0;
    }

    private string GetCurrentUserName()
    {
        var name = _httpContextAccessor.HttpContext?.Items["CurrentUserName"];
        return name as string ?? string.Empty;
    }

    public async Task<PagedResult<InsSettlementListItemDto>> GetListAsync(InsSettlementQueryRequest request)
    {
        var query = _settlementRepo.Query()
            .Include(s => s.Claim)
            .AsQueryable();

        if (request.ClaimId.HasValue)
            query = query.Where(s => s.FClaimId == request.ClaimId.Value);
        if (request.PolicyId.HasValue)
            query = query.Where(s => s.FPolicyId == request.PolicyId.Value);
        if (request.SettlementType.HasValue)
            query = query.Where(s => s.FSettlementType == request.SettlementType.Value);
        if (request.SettlementStatus.HasValue)
            query = query.Where(s => s.FSettlementStatus == request.SettlementStatus.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsSettlementListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InsSettlementDto?> GetByIdAsync(long id)
    {
        var entity = await _settlementRepo.Query()
            .Include(s => s.Claim)
            .Include(s => s.Policy)
            .Include(s => s.CurrentStep)
            .FirstOrDefaultAsync(s => s.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InsSettlementDto> CreateAsync(CreateInsSettlementRequest request)
    {
        var claim = await _claimRepo.GetByIdAsync(request.ClaimId);
        if (claim == null)
            throw new InvalidOperationException($"出险记录 ID {request.ClaimId} 不存在");

        var entity = new InsSettlement
        {
            FUID = Guid.NewGuid().ToString("N"),
            FOrgId = claim.FOrgId,
            FClaimId = request.ClaimId,
            FPolicyId = request.PolicyId,
            FSettlementNumber = $"STL{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}",
            FSettlementType = request.SettlementType,
            FApplyDate = request.ApplyDate,
            FApplicantId = request.ApplicantId,
            FApplicantName = request.ApplicantName,
            FAssessedAmount = request.AssessedAmount,
            FSettlementAmount = request.SettlementAmount,
            FSelfPayAmount = request.SelfPayAmount,
            FDeductible = request.Deductible,
            FSettlementStatus = 1,
            FRemark = request.Remark,
            FCreatorId = GetCurrentUserId(),
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _settlementRepo.AddAsync(entity);
        return (await GetByIdAsync(entity.FID))!;
    }

    public async Task<InsSettlementDto?> UpdateAsync(long id, UpdateInsSettlementRequest request)
    {
        var entity = await _settlementRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null) return null;

        entity.FAssessedAmount = request.AssessedAmount;
        entity.FSettlementAmount = request.SettlementAmount;
        entity.FSelfPayAmount = request.SelfPayAmount;
        entity.FDeductible = request.Deductible;
        entity.FPaymentVoucher = request.PaymentVoucher;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _settlementRepo.UpdateAsync(entity);
        return await GetByIdAsync(id);
    }

    /// <summary>
    /// 提交审批：将状态设为审批中，设当前环节ID为第一个启用的审批环节
    /// </summary>
    public async Task SubmitAsync(long id)
    {
        var entity = await _settlementRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null)
            throw new InvalidOperationException("理赔记录不存在");

        if (entity.FSettlementStatus != 1 && entity.FSettlementStatus != 99)
            throw new InvalidOperationException("只有草稿或已驳回状态的理赔才能提交审批");

        // 获取第一个启用的审批环节
        var firstStep = await _approvalConfigRepo.Query()
            .Where(c => c.FOrgId == entity.FOrgId && c.FStatus == 1)
            .OrderBy(c => c.FStepOrder)
            .FirstOrDefaultAsync();

        if (firstStep == null)
            throw new InvalidOperationException("未配置审批环节，无法提交审批");

        entity.FSettlementStatus = 10; // 审批中
        entity.FCurrentStepId = firstStep.FID;
        entity.FUpdatedTime = DateTime.Now;

        await _settlementRepo.UpdateAsync(entity);
    }

    /// <summary>
    /// 审批操作：通过/驳回/转办
    /// </summary>
    public async Task ReviewAsync(CreateInsApprovalRecordRequest request)
    {
        var entity = await _settlementRepo.Query()
            .AsTracking()
            .Include(s => s.CurrentStep)
            .FirstOrDefaultAsync(s => s.FID == request.SettlementId);

        if (entity == null)
            throw new InvalidOperationException("理赔记录不存在");

        if (entity.FSettlementStatus != 10)
            throw new InvalidOperationException("当前理赔不在审批中状态");

        if (entity.CurrentStep == null)
            throw new InvalidOperationException("当前审批环节信息不存在");

        var currentStep = entity.CurrentStep;
        var currentUserId = GetCurrentUserId();
        var currentUserName = GetCurrentUserName();

        // 写入审批记录
        var record = new InsApprovalRecord
        {
            FUID = Guid.NewGuid().ToString("N"),
            FOrgId = entity.FOrgId,
            FSettlementId = entity.FID,
            FStepConfigId = currentStep.FID,
            FStepOrder = currentStep.FStepOrder,
            FStepName = currentStep.FStepName,
            FApproverId = currentUserId,
            FApproverName = currentUserName,
            FApprovalAction = request.ApprovalAction,
            FApprovalComment = request.ApprovalComment,
            FTransferTargetId = request.TransferTargetId,
            FTransferTargetName = request.TransferTargetName,
            FApprovalTime = DateTime.Now,
            FCreatedTime = DateTime.Now
        };

        await _approvalRecordRepo.AddAsync(record);

        switch (request.ApprovalAction)
        {
            case 1: // 通过
                await HandleApproveAsync(entity, currentStep);
                break;
            case 2: // 驳回
                await HandleRejectAsync(entity, currentStep);
                break;
            case 3: // 转办 — 不改变环节，只写记录
                break;
            default:
                throw new InvalidOperationException($"不支持的审批动作: {request.ApprovalAction}");
        }
    }

    private async Task HandleApproveAsync(InsSettlement entity, InsApprovalConfig currentStep)
    {
        // 查找下一个启用的审批环节
        var nextStep = await _approvalConfigRepo.Query()
            .Where(c => c.FOrgId == entity.FOrgId && c.FStatus == 1 && c.FStepOrder > currentStep.FStepOrder)
            .OrderBy(c => c.FStepOrder)
            .FirstOrDefaultAsync();

        if (nextStep != null)
        {
            // 有下一环节，推进
            entity.FCurrentStepId = nextStep.FID;
        }
        else
        {
            // 无下一环节，审批通过
            entity.FSettlementStatus = 20; // 已通过
            entity.FCurrentStepId = null;
        }

        entity.FUpdatedTime = DateTime.Now;
        await _settlementRepo.UpdateAsync(entity);
    }

    private async Task HandleRejectAsync(InsSettlement entity, InsApprovalConfig currentStep)
    {
        if (currentStep.FRejectTargetStep.HasValue)
        {
            // 根据配置的驳回目标环节回退
            var targetStep = await _approvalConfigRepo.Query()
                .Where(c => c.FOrgId == entity.FOrgId && c.FStatus == 1 && c.FStepOrder == currentStep.FRejectTargetStep.Value)
                .FirstOrDefaultAsync();

            if (targetStep != null)
            {
                entity.FCurrentStepId = targetStep.FID;
                entity.FSettlementStatus = 10; // 仍在审批中，但回退到目标环节
            }
            else
            {
                // 目标环节不存在，回到草稿
                entity.FSettlementStatus = 99; // 已驳回
                entity.FCurrentStepId = null;
            }
        }
        else
        {
            // 无驳回目标环节配置，直接回到草稿
            entity.FSettlementStatus = 99; // 已驳回
            entity.FCurrentStepId = null;
        }

        entity.FUpdatedTime = DateTime.Now;
        await _settlementRepo.UpdateAsync(entity);
    }

    /// <summary>
    /// 获取待我审批的理赔（按当前环节的审批人ID匹配）
    /// </summary>
    public async Task<PagedResult<InsSettlementListItemDto>> GetPendingMyAsync(long approverId, int pageIndex = 1, int pageSize = 20)
    {
        // 查找审批人对应的审批配置环节（指定人员类型）
        var myStepIds = await _approvalConfigRepo.Query()
            .Where(c => c.FStatus == 1 && c.FApproverType == 1 && c.FApproverId == approverId)
            .Select(c => c.FID)
            .ToListAsync();

        var query = _settlementRepo.Query()
            .Include(s => s.Claim)
            .Where(s => s.FSettlementStatus == 10 && s.FCurrentStepId.HasValue && myStepIds.Contains(s.FCurrentStepId.Value));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.FCreatedTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<InsSettlementListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<List<InsApprovalRecordListItemDto>> GetApprovalHistoryAsync(long settlementId)
    {
        var records = await _approvalRecordRepo.Query()
            .Where(r => r.FSettlementId == settlementId)
            .OrderBy(r => r.FApprovalTime)
            .ToListAsync();

        return records.Select(r => new InsApprovalRecordListItemDto
        {
            Id = r.FID,
            StepOrder = r.FStepOrder,
            StepName = r.FStepName,
            ApproverName = r.FApproverName,
            ApprovalAction = r.FApprovalAction,
            ApprovalComment = r.FApprovalComment,
            ApprovalTime = r.FApprovalTime
        }).ToList();
    }

    /// <summary>
    /// 拨付：将状态设为已拨付(30)，更新赔付日期和凭证；如为共保基金理赔则扣减基金余额
    /// </summary>
    public async Task PayAsync(long settlementId, string? paymentVoucher)
    {
        var entity = await _settlementRepo.Query()
            .AsTracking()
            .Include(s => s.Policy)
            .FirstOrDefaultAsync(s => s.FID == settlementId);

        if (entity == null)
            throw new InvalidOperationException("理赔记录不存在");

        if (entity.FSettlementStatus != 20)
            throw new InvalidOperationException("只有审批通过状态的理赔才能拨付");

        entity.FSettlementStatus = 30; // 已拨付
        entity.FPaymentDate = DateOnly.FromDateTime(DateTime.Now);
        entity.FPaymentVoucher = paymentVoucher;
        entity.FUpdatedTime = DateTime.Now;

        // 共保基金赔付扣减（原子 UPDATE，避免并发拨付/缴费的读-改-写丢失更新）
        if (entity.FSettlementType == 2 && entity.Policy?.FCoInsuranceFundId != null)
        {
            var payoutAmount = entity.FSettlementAmount ?? 0;
            await _fundRepo.Query()
                .Where(f => f.FID == entity.Policy.FCoInsuranceFundId.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.FTotalPayouts, f => f.FTotalPayouts + payoutAmount)
                    .SetProperty(f => f.FFundBalance, f => f.FFundBalance - payoutAmount)
                    .SetProperty(f => f.FUpdatedTime, DateTime.Now));
        }

        await _settlementRepo.UpdateAsync(entity);
    }

    #region Mapping

    private static InsSettlementDto MapToDto(InsSettlement entity)
    {
        return new InsSettlementDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            ClaimId = entity.FClaimId,
            PolicyId = entity.FPolicyId,
            SettlementNumber = entity.FSettlementNumber,
            SettlementType = entity.FSettlementType,
            ApplyDate = entity.FApplyDate,
            ApplicantId = entity.FApplicantId,
            ApplicantName = entity.FApplicantName,
            AssessedAmount = entity.FAssessedAmount,
            SettlementAmount = entity.FSettlementAmount,
            SelfPayAmount = entity.FSelfPayAmount,
            Deductible = entity.FDeductible,
            SettlementStatus = entity.FSettlementStatus,
            CurrentStepId = entity.FCurrentStepId,
            CurrentStepName = entity.CurrentStep?.FStepName,
            PaymentDate = entity.FPaymentDate,
            PaymentVoucher = entity.FPaymentVoucher,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            ClaimNumber = entity.Claim?.FClaimNumber,
            PolicyNumber = entity.Policy?.FPolicyNumber
        };
    }

    private static InsSettlementListItemDto MapToListItemDto(InsSettlement entity)
    {
        return new InsSettlementListItemDto
        {
            Id = entity.FID,
            SettlementNumber = entity.FSettlementNumber,
            SettlementType = entity.FSettlementType,
            ApplyDate = entity.FApplyDate,
            ApplicantName = entity.FApplicantName,
            AssessedAmount = entity.FAssessedAmount,
            SettlementAmount = entity.FSettlementAmount,
            SettlementStatus = entity.FSettlementStatus,
            ClaimNumber = entity.Claim?.FClaimNumber
        };
    }

    #endregion
}
