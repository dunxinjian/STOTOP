using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class AttachmentService : IAttachmentService
{
    private readonly STOTOPDbContext _db;

    public AttachmentService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<List<AttachmentListDto>>> GetListAsync(int relationType, long relationId)
    {
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == relationType && a.FRelationId == relationId)
            .OrderByDescending(a => a.FCreateTime)
            .ToListAsync();

        var userIds = attachments.Select(a => a.FUserId).Distinct().ToList();
        var userDict = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToDictionaryAsync(u => u.FID, u => u.FName);

        var items = attachments.Select(a => new AttachmentListDto
        {
            Id = a.FID,
            RelationType = a.FRelationType,
            RelationId = a.FRelationId,
            UserId = a.FUserId,
            UserName = userDict.GetValueOrDefault(a.FUserId),
            OriginalFileName = a.FOriginalFileName,
            StoragePath = a.FStoragePath,
            FileSize = a.FFileSize,
            FileType = a.FFileType,
            CreateTime = a.FCreateTime
        }).ToList();

        return ApiResult<List<AttachmentListDto>>.Success(items);
    }

    public async Task<ApiResult<AttachmentListDto>> UploadAsync(
        UploadAttachmentRequest request,
        string originalFileName,
        string storagePath,
        long fileSize,
        string fileType,
        long operatorId)
    {
        var attachment = new TmAttachment
        {
            FRelationType = request.RelationType,
            FRelationId = request.RelationId,
            FUserId = operatorId,
            FOriginalFileName = originalFileName,
            FStoragePath = storagePath,
            FFileSize = fileSize,
            FFileType = fileType,
            FCreateTime = DateTime.Now
        };

        _db.Set<TmAttachment>().Add(attachment);
        await _db.SaveChangesAsync();

        var userName = await _db.Set<SysUser>()
            .Where(u => u.FID == operatorId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new AttachmentListDto
        {
            Id = attachment.FID,
            RelationType = attachment.FRelationType,
            RelationId = attachment.FRelationId,
            UserId = attachment.FUserId,
            UserName = userName,
            OriginalFileName = attachment.FOriginalFileName,
            StoragePath = attachment.FStoragePath,
            FileSize = attachment.FFileSize,
            FileType = attachment.FFileType,
            CreateTime = attachment.FCreateTime
        };

        return ApiResult<AttachmentListDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var attachment = await _db.Set<TmAttachment>()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (attachment == null)
            return ApiResult<bool>.Fail("附件不存在");

        _db.Set<TmAttachment>().Remove(attachment);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<AttachmentDownloadInfoDto>> GetDownloadInfoAsync(long id)
    {
        var attachment = await _db.Set<TmAttachment>()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (attachment == null)
            return ApiResult<AttachmentDownloadInfoDto>.Fail("附件不存在");

        var dto = new AttachmentDownloadInfoDto
        {
            FileName = attachment.FOriginalFileName,
            StoragePath = attachment.FStoragePath,
            FileType = attachment.FFileType
        };

        return ApiResult<AttachmentDownloadInfoDto>.Success(dto);
    }
}
