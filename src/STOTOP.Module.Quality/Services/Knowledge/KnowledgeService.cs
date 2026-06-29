using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Knowledge;

public class KnowledgeService : IKnowledgeService
{
    private readonly STOTOPDbContext _db;

    public KnowledgeService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<KnowledgeDto>>> GetPagedAsync(long orgId, KnowledgePagedRequest request)
    {
        var query = _db.Set<QlKnowledge>().Where(k => k.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(k => k.FCategory == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Tag))
            query = query.Where(k => k.FTags != null && k.FTags.Contains(request.Tag));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(k => k.FTitle.Contains(request.Keyword) || k.FContent.Contains(request.Keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(k => k.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(k => new KnowledgeDto
            {
                Id = k.FID,
                Title = k.FTitle,
                Content = k.FContent,
                Category = k.FCategory,
                Tags = k.FTags,
                RelatedExceptionId = k.FRelatedExceptionId,
                RelatedReviewId = k.FRelatedReviewId,
                ViewCount = k.FViewCount,
                CreateTime = k.FCreateTime,
                UpdateTime = k.FUpdateTime,
            })
            .ToListAsync();

        return ApiResult<PagedResult<KnowledgeDto>>.Success(new PagedResult<KnowledgeDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<KnowledgeDto>> GetByIdAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlKnowledge>().FirstOrDefaultAsync(k => k.FID == id && k.FOrgId == orgId);
        if (entity == null)
            return ApiResult<KnowledgeDto>.Fail("知识库文章不存在");

        // 增加浏览量
        entity.FViewCount++;
        await _db.SaveChangesAsync();

        return ApiResult<KnowledgeDto>.Success(new KnowledgeDto
        {
            Id = entity.FID,
            Title = entity.FTitle,
            Content = entity.FContent,
            Category = entity.FCategory,
            Tags = entity.FTags,
            RelatedExceptionId = entity.FRelatedExceptionId,
            RelatedReviewId = entity.FRelatedReviewId,
            ViewCount = entity.FViewCount,
            CreateTime = entity.FCreateTime,
            UpdateTime = entity.FUpdateTime,
        });
    }

    public async Task<ApiResult<KnowledgeDto>> CreateAsync(long orgId, long operatorId, CreateKnowledgeRequest request)
    {
        var entity = new QlKnowledge
        {
            FOrgId = orgId,
            FTitle = request.Title,
            FContent = request.Content,
            FCategory = request.Category,
            FTags = request.Tags,
            FRelatedExceptionId = request.RelatedExceptionId,
            FRelatedReviewId = request.RelatedReviewId,
            FCreatorId = operatorId,
        };

        _db.Set<QlKnowledge>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<KnowledgeDto>.Success(new KnowledgeDto
        {
            Id = entity.FID,
            Title = entity.FTitle,
            Content = entity.FContent,
            Category = entity.FCategory,
            Tags = entity.FTags,
            CreateTime = entity.FCreateTime,
            UpdateTime = entity.FUpdateTime,
        });
    }

    public async Task<ApiResult<KnowledgeDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateKnowledgeRequest request)
    {
        var entity = await _db.Set<QlKnowledge>().FirstOrDefaultAsync(k => k.FID == id && k.FOrgId == orgId);
        if (entity == null)
            return ApiResult<KnowledgeDto>.Fail("知识库文章不存在");

        if (request.Title != null) entity.FTitle = request.Title;
        if (request.Content != null) entity.FContent = request.Content;
        if (request.Category != null) entity.FCategory = request.Category;
        if (request.Tags != null) entity.FTags = request.Tags;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        return ApiResult<KnowledgeDto>.Success(new KnowledgeDto
        {
            Id = entity.FID,
            Title = entity.FTitle,
            Content = entity.FContent,
            Category = entity.FCategory,
            Tags = entity.FTags,
            CreateTime = entity.FCreateTime,
            UpdateTime = entity.FUpdateTime,
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlKnowledge>().FirstOrDefaultAsync(k => k.FID == id && k.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("知识库文章不存在");

        _db.Set<QlKnowledge>().Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<List<string>>> GetCategoriesAsync(long orgId)
    {
        var categories = await _db.Set<QlKnowledge>()
            .Where(k => k.FOrgId == orgId && k.FCategory != null && k.FCategory != "")
            .Select(k => k.FCategory!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return ApiResult<List<string>>.Success(categories);
    }

    public async Task<ApiResult<List<string>>> GetTagsAsync(long orgId)
    {
        var tagStrings = await _db.Set<QlKnowledge>()
            .Where(k => k.FOrgId == orgId && k.FTags != null && k.FTags != "")
            .Select(k => k.FTags!)
            .ToListAsync();

        var tags = tagStrings
            .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        return ApiResult<List<string>>.Success(tags);
    }

    public async Task<ApiResult<KnowledgeStatsDto>> GetStatsAsync(long orgId)
    {
        var knowledgeSet = _db.Set<QlKnowledge>().Where(k => k.FOrgId == orgId);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var totalArticles = await knowledgeSet.CountAsync();
        var monthNewCount = await knowledgeSet.CountAsync(k => k.FCreateTime >= monthStart);

        // 分类分布
        var categoryData = await knowledgeSet
            .Where(k => k.FCategory != null && k.FCategory != "")
            .GroupBy(k => k.FCategory!)
            .Select(g => new DistributionItem { Name = g.Key, Value = g.Count() })
            .ToListAsync();

        // 热门标签Top10
        var tagStrings = await knowledgeSet
            .Where(k => k.FTags != null && k.FTags != "")
            .Select(k => k.FTags!)
            .ToListAsync();

        var topTags = tagStrings
            .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .GroupBy(t => t)
            .Select(g => new TagCountItem { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .Take(10)
            .ToList();

        var stats = new KnowledgeStatsDto
        {
            TotalArticles = totalArticles,
            MonthNewCount = monthNewCount,
            CategoryDistribution = categoryData,
            TopTags = topTags,
        };

        return ApiResult<KnowledgeStatsDto>.Success(stats);
    }
}
