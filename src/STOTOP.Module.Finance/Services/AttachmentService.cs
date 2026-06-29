using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 附件上传服务
/// </summary>
public class AttachmentService
{
    private readonly IRepository<FinAttachment> _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // 允许的文件类型
    private static readonly string[] AllowedExtensions = {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar"
    };

    public AttachmentService(IRepository<FinAttachment> repository, IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    /// <summary>
    /// 上传文件并保存记录
    /// </summary>
    public async Task<FinAttachment> UploadAsync(
        IFormFile file,
        long accountSetId,
        string businessType,
        long businessId,
        string uploaderName = "",
        long uploaderId = 0,
        string webRootPath = "")
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("文件为空");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"不支持的文件类型: {ext}");

        // 构造存储路径：uploads/attachments/{yyyy-MM}/{guid}.ext
        var folder = Path.Combine("uploads", "attachments", DateTime.Now.ToString("yyyy-MM"));
        var absoluteFolder = string.IsNullOrEmpty(webRootPath)
            ? folder
            : Path.Combine(webRootPath, folder);

        Directory.CreateDirectory(absoluteFolder);

        var guid = Guid.NewGuid().ToString("N");
        var fileName = guid + ext;
        var absolutePath = Path.Combine(absoluteFolder, fileName);
        var relativePath = Path.Combine(folder, fileName).Replace("\\", "/");

        using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new FinAttachment
        {
            FAccountSetId = accountSetId,
            FBusinessType = businessType,
            FBusinessId = businessId,
            FFileName = fileName,
            FOriginalName = file.FileName,
            FFilePath = relativePath,
            FFileSize = file.Length,
            FContentType = file.ContentType,
            FUploadTime = DateTime.Now,
            FUploaderId = uploaderId,
            FUploaderName = uploaderName
        };

        await _repository.AddAsync(attachment);
        return attachment;
    }

    /// <summary>
    /// 获取附件列表
    /// </summary>
    public async Task<List<AttachmentDto>> GetListAsync(string businessType, long businessId)
    {
        var list = await _repository.Query()
            .Where(a => a.FBusinessType == businessType && a.FBusinessId == businessId)
            .OrderBy(a => a.FUploadTime)
            .ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 获取附件详情（用于下载）
    /// </summary>
    public async Task<AttachmentDto?> GetFileAsync(long id)
    {
        var entity = await _repository.Query()
            .FirstOrDefaultAsync(a => a.FID == id);
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// 删除附件（文件+记录）
    /// </summary>
    public async Task DeleteAsync(long id, string webRootPath = "")
    {
        var attachment = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (attachment == null) return;

        // 删除物理文件
        var absolutePath = string.IsNullOrEmpty(webRootPath)
            ? attachment.FFilePath
            : Path.Combine(webRootPath, attachment.FFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        await _repository.DeleteAsync(id);
    }

    private static AttachmentDto MapToDto(FinAttachment entity) => new()
    {
        Id = entity.FID,
        AccountSetId = entity.FAccountSetId,
        BusinessType = entity.FBusinessType,
        BusinessId = entity.FBusinessId,
        FileName = entity.FFileName,
        OriginalName = entity.FOriginalName,
        FilePath = entity.FFilePath,
        FileSize = entity.FFileSize,
        ContentType = entity.FContentType,
        UploadTime = entity.FUploadTime,
        UploaderId = entity.FUploaderId,
        UploaderName = entity.FUploaderName
    };
}
