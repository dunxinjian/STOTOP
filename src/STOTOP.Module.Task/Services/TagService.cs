using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Services;

public class TagService : ITagService
{
    private readonly STOTOPDbContext _db;

    public TagService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<List<TagListDto>>> GetListAsync(long orgId)
    {
        var tags = await _db.Set<TmTag>()
            .Where(t => t.FOrgId == orgId)
            .OrderBy(t => t.FSort)
            .ThenBy(t => t.FName)
            .ToListAsync();

        var tagIds = tags.Select(t => t.FID).ToList();

        // 统计每个标签关联的任务数
        var taskCounts = await _db.Set<TmTaskTag>()
            .Where(tt => tagIds.Contains(tt.FTagId))
            .GroupBy(tt => tt.FTagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToListAsync();
        var taskCountDict = taskCounts.ToDictionary(x => x.TagId, x => x.Count);

        var dtos = tags.Select(t => new TagListDto
        {
            Id = t.FID,
            Name = t.FName,
            Color = t.FColor,
            OrgId = t.FOrgId,
            Sort = t.FSort,
            TaskCount = taskCountDict.GetValueOrDefault(t.FID, 0)
        }).ToList();

        return ApiResult<List<TagListDto>>.Success(dtos);
    }

    public async Task<ApiResult<TagListDto>> CreateAsync(CreateTagRequest request, long orgId)
    {
        // 检查同组织下标签名唯一
        var exists = await _db.Set<TmTag>()
            .AnyAsync(t => t.FOrgId == orgId && t.FName == request.Name);
        if (exists)
            return ApiResult<TagListDto>.Fail("同名标签已存在");

        var tag = new TmTag
        {
            FName = request.Name,
            FColor = request.Color,
            FOrgId = orgId,
            FSort = request.Sort
        };

        _db.Set<TmTag>().Add(tag);
        await _db.SaveChangesAsync();

        return ApiResult<TagListDto>.Success(new TagListDto
        {
            Id = tag.FID,
            Name = tag.FName,
            Color = tag.FColor,
            OrgId = tag.FOrgId,
            Sort = tag.FSort,
            TaskCount = 0
        });
    }

    public async Task<ApiResult<TagListDto>> UpdateAsync(long id, UpdateTagRequest request)
    {
        var tag = await _db.Set<TmTag>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (tag == null)
            return ApiResult<TagListDto>.Fail("标签不存在");

        // 检查同组织下标签名唯一（排除自身）
        var exists = await _db.Set<TmTag>()
            .AnyAsync(t => t.FOrgId == tag.FOrgId && t.FName == request.Name && t.FID != id);
        if (exists)
            return ApiResult<TagListDto>.Fail("同名标签已存在");

        tag.FName = request.Name;
        tag.FColor = request.Color;
        tag.FSort = request.Sort;

        await _db.SaveChangesAsync();

        var taskCount = await _db.Set<TmTaskTag>().CountAsync(tt => tt.FTagId == id);

        return ApiResult<TagListDto>.Success(new TagListDto
        {
            Id = tag.FID,
            Name = tag.FName,
            Color = tag.FColor,
            OrgId = tag.FOrgId,
            Sort = tag.FSort,
            TaskCount = taskCount
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var tag = await _db.Set<TmTag>().FirstOrDefaultAsync(t => t.FID == id);
        if (tag == null)
            return ApiResult<bool>.Fail("标签不存在");

        // 删除关联的任务标签记录
        var taskTags = await _db.Set<TmTaskTag>().Where(tt => tt.FTagId == id).ToListAsync();
        _db.Set<TmTaskTag>().RemoveRange(taskTags);

        _db.Set<TmTag>().Remove(tag);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }
}
