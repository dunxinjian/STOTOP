using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.Points.Events;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Points.Services;

public class PointApplicationService : IPointApplicationService
{
    private readonly STOTOPDbContext _db;
    private readonly IPointService _pointService;
    private readonly IEventDispatcher _eventDispatcher;

    public PointApplicationService(STOTOPDbContext db, IPointService pointService, IEventDispatcher eventDispatcher)
    {
        _db = db;
        _pointService = pointService;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ApiResult<PointApplicationDetailDto>> SubmitAsync(long orgId, long applicantId, SubmitPointApplicationRequest request)
    {
        // 验证规则存在且启用
        var rule = await _db.Set<PmPointRule>()
            .FirstOrDefaultAsync(r => r.FID == request.RuleId && r.FOrgId == orgId && r.FIsEnabled);

        if (rule == null)
            return ApiResult<PointApplicationDetailDto>.Fail("积分规则不存在或已禁用");

        var entity = new PmPointApplication
        {
            FOrgId = orgId,
            FApplicantId = applicantId,
            FRuleId = request.RuleId,
            FApplicationNote = request.ApplicationNote,
            FAttachment = request.Attachment,
            FStatus = 0, // 0=待审批
            FCreateTime = DateTime.Now
        };

        _db.Set<PmPointApplication>().Add(entity);
        await _db.SaveChangesAsync();

        var dto = await MapToDetailDto(entity);

        // 发布积分申请提交事件
        try
        {
            await _eventDispatcher.PublishAsync(new PointApplicationSubmittedEvent
            {
                ApplicationId = entity.FID,
                ApplicantId = applicantId,
                RequestedPoints = rule.FPointValue,
                Reason = request.ApplicationNote ?? "",
                OrgId = orgId,
                TriggeredByUserId = applicantId,
                ModuleCode = "points"
            });
        }
        catch { /* 事件发布失败不影响主业务 */ }

        return ApiResult<PointApplicationDetailDto>.Success(dto);
    }

    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPagedListAsync(long orgId, ApplicationPagedRequest request)
    {
        var query = _db.Set<PmPointApplication>()
            .Where(a => a.FOrgId == orgId)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(a => a.FStatus == request.Status.Value);
        if (request.ApplicantId.HasValue)
            query = query.Where(a => a.FApplicantId == request.ApplicantId.Value);
        if (request.RuleId.HasValue)
            query = query.Where(a => a.FRuleId == request.RuleId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(a => a.FApplicationNote.Contains(kw));
        }

        var total = await query.CountAsync();

        var applications = await query
            .OrderByDescending(a => a.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = await MapToListDtos(applications);

        return ApiResult<PagedResult<PointApplicationListDto>>.Success(new PagedResult<PointApplicationListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetMyApplicationsAsync(long orgId, long userId, MyApplicationPagedRequest request)
    {
        var query = _db.Set<PmPointApplication>()
            .Where(a => a.FOrgId == orgId && a.FApplicantId == userId)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(a => a.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var applications = await query
            .OrderByDescending(a => a.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = await MapToListDtos(applications);

        return ApiResult<PagedResult<PointApplicationListDto>>.Success(new PagedResult<PointApplicationListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPendingAsync(long orgId, PendingApplicationPagedRequest request)
    {
        var query = _db.Set<PmPointApplication>()
            .Where(a => a.FOrgId == orgId && a.FStatus == 0) // 0=待审批
            .AsQueryable();

        if (request.ApplicantId.HasValue)
            query = query.Where(a => a.FApplicantId == request.ApplicantId.Value);

        var total = await query.CountAsync();

        var applications = await query
            .OrderBy(a => a.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = await MapToListDtos(applications);

        return ApiResult<PagedResult<PointApplicationListDto>>.Success(new PagedResult<PointApplicationListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<bool>> ApproveAsync(long id, long approverId, ApprovePointApplicationRequest request)
    {
        var entity = await _db.Set<PmPointApplication>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("申请记录不存在");

        if (entity.FStatus != 0)
            return ApiResult<bool>.Fail("该申请已处理");

        // 获取规则信息
        var rule = await _db.Set<PmPointRule>().FirstOrDefaultAsync(r => r.FID == entity.FRuleId);
        if (rule == null)
            return ApiResult<bool>.Fail("关联规则不存在");

        entity.FStatus = 1; // 1=已通过
        entity.FApproverId = approverId;
        entity.FApprovalComment = request.ApprovalComment;
        entity.FApprovalTime = DateTime.Now;
        await _db.SaveChangesAsync();

        // 审批通过后触发奖分
        await _pointService.AwardAsync(entity.FOrgId, approverId, new ManualAwardRequest
        {
            UserId = entity.FApplicantId,
            SourceId = rule.FSourceId,
            PointValue = rule.FPointValue,
            Remark = $"申请审批通过：{entity.FApplicationNote}"
        });

        // 发布积分申请审批通过事件
        try
        {
            await _eventDispatcher.PublishAsync(new PointApplicationApprovedEvent
            {
                ApplicationId = id,
                ApplicantId = entity.FApplicantId,
                ApprovedPoints = rule.FPointValue,
                ApproverId = approverId,
                TriggeredByUserId = approverId,
                ModuleCode = "points"
            });
        }
        catch { /* 事件发布失败不影响主业务 */ }

        return ApiResult<bool>.Success(true, "审批通过");
    }

    public async Task<ApiResult<bool>> RejectAsync(long id, long approverId, string reason)
    {
        var entity = await _db.Set<PmPointApplication>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("申请记录不存在");

        if (entity.FStatus != 0)
            return ApiResult<bool>.Fail("该申请已处理");

        entity.FStatus = 2; // 2=已拒绝
        entity.FApproverId = approverId;
        entity.FApprovalComment = reason;
        entity.FApprovalTime = DateTime.Now;
        await _db.SaveChangesAsync();

        // 发布积分申请驳回事件
        try
        {
            await _eventDispatcher.PublishAsync(new PointApplicationRejectedEvent
            {
                ApplicationId = id,
                ApplicantId = entity.FApplicantId,
                Reason = reason ?? "",
                ApproverId = approverId,
                TriggeredByUserId = approverId,
                ModuleCode = "points"
            });
        }
        catch { /* 事件发布失败不影响主业务 */ }

        return ApiResult<bool>.Success(true, "已拒绝");
    }

    private async Task<List<PointApplicationListDto>> MapToListDtos(List<PmPointApplication> applications)
    {
        if (applications.Count == 0) return new List<PointApplicationListDto>();

        // 批量获取用户名
        var userIds = applications.Select(a => a.FApplicantId)
            .Union(applications.Where(a => a.FApproverId.HasValue).Select(a => a.FApproverId!.Value))
            .Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID, u => u.FName);

        // 批量获取规则名和积分值
        var ruleIds = applications.Select(a => a.FRuleId).Distinct().ToList();
        var rules = await _db.Set<PmPointRule>()
            .Where(r => ruleIds.Contains(r.FID))
            .Select(r => new { r.FID, r.FRuleName, r.FPointValue })
            .ToListAsync();
        var ruleDict = rules.ToDictionary(r => r.FID);

        return applications.Select(a =>
        {
            var ruleInfo = ruleDict.GetValueOrDefault(a.FRuleId);
            return new PointApplicationListDto
            {
                Id = a.FID,
                OrgId = a.FOrgId,
                ApplicantId = a.FApplicantId,
                ApplicantName = userDict.GetValueOrDefault(a.FApplicantId),
                RuleId = a.FRuleId,
                RuleName = ruleInfo?.FRuleName,
                PointValue = ruleInfo?.FPointValue ?? 0,
                ApplicationNote = a.FApplicationNote,
                Status = a.FStatus,
                ApproverId = a.FApproverId,
                ApproverName = a.FApproverId.HasValue ? userDict.GetValueOrDefault(a.FApproverId.Value) : null,
                ApprovalTime = a.FApprovalTime,
                CreateTime = a.FCreateTime
            };
        }).ToList();
    }

    private async Task<PointApplicationDetailDto> MapToDetailDto(PmPointApplication a)
    {
        var dto = new PointApplicationDetailDto
        {
            Id = a.FID,
            OrgId = a.FOrgId,
            ApplicantId = a.FApplicantId,
            RuleId = a.FRuleId,
            ApplicationNote = a.FApplicationNote,
            Attachment = a.FAttachment,
            Status = a.FStatus,
            ApproverId = a.FApproverId,
            ApprovalComment = a.FApprovalComment,
            ApprovalTime = a.FApprovalTime,
            CreateTime = a.FCreateTime
        };

        // 获取申请人名
        var applicant = await _db.Set<SysUser>()
            .Where(u => u.FID == a.FApplicantId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();
        dto.ApplicantName = applicant?.FName;

        // 获取规则和来源名
        var rule = await _db.Set<PmPointRule>()
            .FirstOrDefaultAsync(r => r.FID == a.FRuleId);
        if (rule != null)
        {
            dto.RuleName = rule.FRuleName;
            dto.PointValue = rule.FPointValue;

            var source = await _db.Set<PmPointSource>()
                .Where(s => s.FID == rule.FSourceId)
                .Select(s => new { s.FSourceName })
                .FirstOrDefaultAsync();
            dto.SourceName = source?.FSourceName;
        }

        // 获取审批人名
        if (a.FApproverId.HasValue)
        {
            var approver = await _db.Set<SysUser>()
                .Where(u => u.FID == a.FApproverId.Value)
                .Select(u => new { u.FName })
                .FirstOrDefaultAsync();
            dto.ApproverName = approver?.FName;
        }

        return dto;
    }
}
