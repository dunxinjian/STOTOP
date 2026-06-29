using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/files")]
public class FileController : ControllerBase
{
    private readonly AttachmentService _attachmentService;
    private readonly IWebHostEnvironment _env;

    public FileController(AttachmentService attachmentService, IWebHostEnvironment env)
    {
        _attachmentService = attachmentService;
        _env = env;
    }

    /// <summary>
    /// 上传文件
    /// POST /api/finance/files/upload
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<ApiResult<object>> Upload(
        IFormFile file,
        [FromForm] long accountSetId = 0,
        [FromForm] string businessType = "",
        [FromForm] long businessId = 0)
    {
        try
        {
            var uploaderName = User.Identity?.Name ?? "";
            var uploaderIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            long.TryParse(uploaderIdClaim, out var uploaderId);

            var attachment = await _attachmentService.UploadAsync(
                file,
                accountSetId,
                businessType,
                businessId,
                uploaderName,
                uploaderId,
                _env.ContentRootPath);

            return ApiResult<object>.Success(new
            {
                fileId = attachment.FID,
                fileName = attachment.FFileName,
                originalName = attachment.FOriginalName,
                filePath = attachment.FFilePath,
                fileSize = attachment.FFileSize,
                contentType = attachment.FContentType,
                uploadTime = attachment.FUploadTime
            });
        }
        catch (ArgumentException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult<object>.Fail($"上传失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 下载文件
    /// GET /api/finance/files/{id}
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(long id)
    {
        var attachment = await _attachmentService.GetFileAsync(id);
        if (attachment == null)
            return NotFound("文件不存在");

        var absolutePath = Path.Combine(_env.ContentRootPath, attachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!global::System.IO.File.Exists(absolutePath))
            return NotFound("文件已被删除");

        return PhysicalFile(absolutePath, attachment.ContentType, attachment.OriginalName);
    }

    /// <summary>
    /// 删除文件
    /// DELETE /api/finance/files/{id}
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        await _attachmentService.DeleteAsync(id, _env.ContentRootPath);
        return ApiResult<bool>.Success(true);
    }

    /// <summary>
    /// 获取附件列表
    /// GET /api/finance/files/list?businessType=voucher&businessId=123
    /// </summary>
    [HttpGet("list")]
    public async Task<ApiResult<List<object>>> GetList(
        [FromQuery] string businessType,
        [FromQuery] long businessId)
    {
        var list = await _attachmentService.GetListAsync(businessType, businessId);
        var result = list.Select(a => (object)new
        {
            id = a.Id,
            fileName = a.FileName,
            originalName = a.OriginalName,
            filePath = a.FilePath,
            fileSize = a.FileSize,
            contentType = a.ContentType,
            uploadTime = a.UploadTime,
            uploaderName = a.UploaderName
        }).ToList();

        return ApiResult<List<object>>.Success(result);
    }
}
