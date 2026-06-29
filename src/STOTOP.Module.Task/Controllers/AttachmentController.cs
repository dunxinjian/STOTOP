using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/attachments")]
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _service;

    public AttachmentController(IAttachmentService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>上传附件</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<AttachmentListDto>> Upload(
        [FromForm] UploadAttachmentRequest request,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResult<AttachmentListDto>.Fail("请选择要上传的文件");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "attachments");
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return await _service.UploadAsync(
            request,
            file.FileName,
            filePath,
            file.Length,
            file.ContentType,
            GetUserId());
    }

    /// <summary>获取附件列表</summary>
    [HttpGet("{relationType}/{relationId}")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<AttachmentListDto>>> GetList(int relationType, long relationId)
    {
        return await _service.GetListAsync(relationType, relationId);
    }

    /// <summary>删除附件</summary>
    [HttpDelete("{id}")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>下载附件</summary>
    [HttpGet("{id}/download")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<IActionResult> Download(long id)
    {
        var result = await _service.GetDownloadInfoAsync(id);
        if (result.Code != 200 || result.Data == null)
            return NotFound();

        var info = result.Data;
        if (!global::System.IO.File.Exists(info.StoragePath))
            return NotFound("文件不存在");

        var fileBytes = await global::System.IO.File.ReadAllBytesAsync(info.StoragePath);
        return File(fileBytes, info.FileType ?? "application/octet-stream", info.FileName);
    }
}
