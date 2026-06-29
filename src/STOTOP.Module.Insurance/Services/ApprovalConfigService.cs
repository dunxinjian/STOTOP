using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class ApprovalConfigService : IApprovalConfigService
{
    private readonly IRepository<InsApprovalConfig> _configRepo;

    public ApprovalConfigService(IRepository<InsApprovalConfig> configRepo)
    {
        _configRepo = configRepo;
    }

    public async Task<PagedResult<InsApprovalConfigDto>> GetListAsync(long orgId, int pageIndex = 1, int pageSize = 20)
    {
        var query = _configRepo.Query()
            .Where(c => c.FOrgId == orgId)
            .OrderBy(c => c.FStepOrder);

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<InsApprovalConfigDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<InsApprovalConfigDto> CreateAsync(long orgId, CreateInsApprovalConfigRequest request)
    {
        var entity = new InsApprovalConfig
        {
            FUID = Guid.NewGuid().ToString("N"),
            FOrgId = orgId,
            FStepOrder = request.StepOrder,
            FStepName = request.StepName,
            FStepCode = request.StepCode,
            FApproverType = request.ApproverType,
            FApproverId = request.ApproverId,
            FApproverName = request.ApproverName,
            FApproverRoleCode = request.ApproverRoleCode,
            FCanReject = request.CanReject,
            FRejectTargetStep = request.RejectTargetStep,
            FStatus = 1,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _configRepo.AddAsync(entity);

        return MapToDto(entity);
    }

    public async Task<InsApprovalConfigDto?> UpdateAsync(long id, UpdateInsApprovalConfigRequest request)
    {
        var entity = await _configRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return null;

        entity.FStepName = request.StepName;
        entity.FStepCode = request.StepCode;
        entity.FApproverType = request.ApproverType;
        entity.FApproverId = request.ApproverId;
        entity.FApproverName = request.ApproverName;
        entity.FApproverRoleCode = request.ApproverRoleCode;
        entity.FCanReject = request.CanReject;
        entity.FRejectTargetStep = request.RejectTargetStep;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _configRepo.UpdateAsync(entity);

        return MapToDto(entity);
    }

    /// <summary>
    /// 启用/禁用审批环节
    /// </summary>
    public async Task ToggleStatusAsync(long id)
    {
        var entity = await _configRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null)
            throw new InvalidOperationException("审批配置不存在");

        entity.FStatus = entity.FStatus == 1 ? 0 : 1;
        entity.FUpdatedTime = DateTime.Now;

        await _configRepo.UpdateAsync(entity);
    }

    /// <summary>
    /// 调整环节顺序
    /// </summary>
    public async Task ReorderAsync(List<ApprovalStepOrderItem> items)
    {
        foreach (var item in items)
        {
            var entity = await _configRepo.Query()
                .AsTracking()
                .FirstOrDefaultAsync(c => c.FID == item.Id);

            if (entity != null)
            {
                entity.FStepOrder = item.StepOrder;
                entity.FUpdatedTime = DateTime.Now;
                await _configRepo.UpdateAsync(entity);
            }
        }
    }

    #region Mapping

    private static InsApprovalConfigDto MapToDto(InsApprovalConfig entity)
    {
        return new InsApprovalConfigDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            StepOrder = entity.FStepOrder,
            StepName = entity.FStepName,
            StepCode = entity.FStepCode,
            ApproverType = entity.FApproverType,
            ApproverId = entity.FApproverId,
            ApproverName = entity.FApproverName,
            ApproverRoleCode = entity.FApproverRoleCode,
            CanReject = entity.FCanReject,
            RejectTargetStep = entity.FRejectTargetStep,
            Status = entity.FStatus,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
