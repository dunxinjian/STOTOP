using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Review;

public class ReviewService : IReviewService
{
    private readonly STOTOPDbContext _db;

    public ReviewService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<ReviewDto>>> GetPagedAsync(long orgId, ReviewPagedRequest request)
    {
        var query = _db.Set<QlReview>().Where(r => r.FOrgId == orgId);

        if (request.ExceptionId.HasValue)
            query = query.Where(r => r.FExceptionId == request.ExceptionId.Value);
        if (request.StartDate.HasValue)
            query = query.Where(r => r.FReviewDate >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(r => r.FReviewDate <= request.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(r => r.FTitle.Contains(request.Keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ReviewDto
            {
                Id = r.FID,
                ExceptionId = r.FExceptionId,
                Title = r.FTitle,
                RootCause = r.FRootCause,
                ImpactAnalysis = r.FImpactAnalysis,
                Conclusion = r.FConclusion,
                ReviewDate = r.FReviewDate,
                CreateTime = r.FCreateTime,
            })
            .ToListAsync();

        return ApiResult<PagedResult<ReviewDto>>.Success(new PagedResult<ReviewDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<ReviewDto>> GetByIdAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlReview>().FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<ReviewDto>.Fail("复盘记录不存在");

        var improvements = await _db.Set<QlReviewImprovement>()
            .Where(i => i.FReviewId == id)
            .OrderBy(i => i.FSortOrder)
            .Select(i => new ReviewImprovementDto
            {
                Id = i.FID,
                Content = i.FContent,
                AssigneeId = i.FAssigneeId,
                Deadline = i.FDeadline,
                Completed = i.FCompleted,
                CompletedTime = i.FCompletedTime,
                SortOrder = i.FSortOrder,
            })
            .ToListAsync();

        return ApiResult<ReviewDto>.Success(new ReviewDto
        {
            Id = entity.FID,
            ExceptionId = entity.FExceptionId,
            Title = entity.FTitle,
            RootCause = entity.FRootCause,
            ImpactAnalysis = entity.FImpactAnalysis,
            Conclusion = entity.FConclusion,
            ReviewDate = entity.FReviewDate,
            CreateTime = entity.FCreateTime,
            Improvements = improvements,
        });
    }

    public async Task<ApiResult<ReviewDto>> CreateAsync(long orgId, long operatorId, CreateReviewRequest request)
    {
        var entity = new QlReview
        {
            FOrgId = orgId,
            FExceptionId = request.ExceptionId,
            FTitle = request.Title,
            FRootCause = request.RootCause,
            FImpactAnalysis = request.ImpactAnalysis,
            FConclusion = request.Conclusion,
            FReviewDate = request.ReviewDate,
            FCreatorId = operatorId,
        };

        _db.Set<QlReview>().Add(entity);
        await _db.SaveChangesAsync();

        if (request.Improvements?.Any() == true)
        {
            foreach (var imp in request.Improvements)
            {
                _db.Set<QlReviewImprovement>().Add(new QlReviewImprovement
                {
                    FReviewId = entity.FID,
                    FContent = imp.Content,
                    FAssigneeId = imp.AssigneeId,
                    FDeadline = imp.Deadline,
                    FSortOrder = imp.SortOrder,
                });
            }
            await _db.SaveChangesAsync();
        }

        return ApiResult<ReviewDto>.Success(new ReviewDto
        {
            Id = entity.FID,
            ExceptionId = entity.FExceptionId,
            Title = entity.FTitle,
            ReviewDate = entity.FReviewDate,
            CreateTime = entity.FCreateTime,
        });
    }

    public async Task<ApiResult<ReviewDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateReviewRequest request)
    {
        var entity = await _db.Set<QlReview>().FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<ReviewDto>.Fail("复盘记录不存在");

        if (request.Title != null) entity.FTitle = request.Title;
        if (request.RootCause != null) entity.FRootCause = request.RootCause;
        if (request.ImpactAnalysis != null) entity.FImpactAnalysis = request.ImpactAnalysis;
        if (request.Conclusion != null) entity.FConclusion = request.Conclusion;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        return ApiResult<ReviewDto>.Success(new ReviewDto
        {
            Id = entity.FID,
            ExceptionId = entity.FExceptionId,
            Title = entity.FTitle,
            ReviewDate = entity.FReviewDate,
            CreateTime = entity.FCreateTime,
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlReview>().FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("复盘记录不存在");

        var improvements = await _db.Set<QlReviewImprovement>().Where(i => i.FReviewId == id).ToListAsync();
        _db.Set<QlReviewImprovement>().RemoveRange(improvements);
        _db.Set<QlReview>().Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<ReviewStatsDto>> GetStatsAsync(long orgId)
    {
        var reviews = _db.Set<QlReview>().Where(r => r.FOrgId == orgId);
        var improvements = _db.Set<QlReviewImprovement>();

        var totalReviews = await reviews.CountAsync();

        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var monthNewCount = await reviews.CountAsync(r => r.FCreateTime >= monthStart);

        // 获取当前组织所有复盘关联的改进措施
        var reviewIds = reviews.Select(r => r.FID);
        var orgImprovements = improvements.Where(i => reviewIds.Contains(i.FReviewId));

        var totalImprovements = await orgImprovements.CountAsync();
        var completedImprovements = await orgImprovements.CountAsync(i => i.FCompleted);
        var pendingImprovements = totalImprovements - completedImprovements;
        var completionRate = totalImprovements > 0
            ? Math.Round((double)completedImprovements / totalImprovements * 100, 1)
            : 0;

        return ApiResult<ReviewStatsDto>.Success(new ReviewStatsDto
        {
            TotalReviews = totalReviews,
            MonthNewCount = monthNewCount,
            PendingImprovements = pendingImprovements,
            CompletionRate = completionRate,
        });
    }

    public async Task<ApiResult<PagedResult<ImprovementListDto>>> GetImprovementsAsync(long orgId, ImprovementPagedRequest request)
    {
        var reviewIds = _db.Set<QlReview>().Where(r => r.FOrgId == orgId).Select(r => r.FID);

        var query = from i in _db.Set<QlReviewImprovement>()
                    join r in _db.Set<QlReview>() on i.FReviewId equals r.FID
                    where reviewIds.Contains(i.FReviewId)
                    select new { Improvement = i, ReviewTitle = r.FTitle };

        if (request.Completed.HasValue)
            query = query.Where(x => x.Improvement.FCompleted == request.Completed.Value);
        if (request.ReviewId.HasValue)
            query = query.Where(x => x.Improvement.FReviewId == request.ReviewId.Value);
        if (request.AssigneeId.HasValue)
            query = query.Where(x => x.Improvement.FAssigneeId == request.AssigneeId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(x => x.Improvement.FContent.Contains(request.Keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.Improvement.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ImprovementListDto
            {
                Id = x.Improvement.FID,
                ReviewId = x.Improvement.FReviewId,
                ReviewTitle = x.ReviewTitle,
                Content = x.Improvement.FContent,
                AssigneeId = x.Improvement.FAssigneeId,
                Deadline = x.Improvement.FDeadline,
                Completed = x.Improvement.FCompleted,
                CompletedTime = x.Improvement.FCompletedTime,
                SortOrder = x.Improvement.FSortOrder,
            })
            .ToListAsync();

        return ApiResult<PagedResult<ImprovementListDto>>.Success(new PagedResult<ImprovementListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<bool>> UpdateImprovementAsync(long orgId, long id, UpdateImprovementRequest request)
    {
        var entity = await (from i in _db.Set<QlReviewImprovement>().AsTracking()
                            join r in _db.Set<QlReview>() on i.FReviewId equals r.FID
                            where i.FID == id && r.FOrgId == orgId
                            select i).FirstOrDefaultAsync();

        if (entity == null)
            return ApiResult<bool>.Fail("改进措施不存在");

        if (request.Content != null) entity.FContent = request.Content;
        if (request.AssigneeId.HasValue) entity.FAssigneeId = request.AssigneeId;
        if (request.Deadline.HasValue) entity.FDeadline = request.Deadline;

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> CompleteImprovementAsync(long orgId, long id, CompleteImprovementRequest request)
    {
        var entity = await (from i in _db.Set<QlReviewImprovement>().AsTracking()
                            join r in _db.Set<QlReview>() on i.FReviewId equals r.FID
                            where i.FID == id && r.FOrgId == orgId
                            select i).FirstOrDefaultAsync();

        if (entity == null)
            return ApiResult<bool>.Fail("改进措施不存在");

        entity.FCompleted = true;
        entity.FCompletedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }
}
