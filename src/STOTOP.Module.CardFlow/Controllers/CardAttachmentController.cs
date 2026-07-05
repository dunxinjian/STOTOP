using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Services;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// 卡片附件上传/下载。file 字段的真实落盘链路：上传返回 CardFileValue（卡片 dataJson 只存元数据），
/// 下载走授权端点（登录 + 组织隔离 + 防目录穿越），不暴露静态直链。
/// 注：当前授权粒度为“登录 + 同组织”，尚未做“能否访问该卡片”的细粒度校验（需附件↔卡片持久化关联），留后续增强。
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/cards/attachments")]
public class CardAttachmentController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public CardAttachmentController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    public sealed record CardFileValueDto(string Name, string Url, long Size, string MimeType);

    /// <summary>上传单个附件，返回可写入卡片 dataJson 的 CardFileValue。</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ApiResult<CardFileValueDto>> Upload(IFormFile file)
    {
        var orgId = GetOrgId();
        if (orgId == 0)
        {
            return ApiResult<CardFileValueDto>.Fail("当前无组织上下文", 401);
        }

        var saved = await CardAttachmentStorage.SaveAsync(_env, file, orgId);
        var url = $"/api/cardflow/cards/attachments/download?key={Uri.EscapeDataString(saved.StorageKey)}";
        return ApiResult<CardFileValueDto>.Success(new CardFileValueDto(saved.OriginalName, url, saved.Size, saved.ContentType));
    }

    /// <summary>授权下载：按当前组织解析 storageKey，防跨组织与目录穿越。</summary>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string key)
    {
        var orgId = GetOrgId();
        if (orgId == 0)
        {
            return Unauthorized();
        }

        if (!CardAttachmentStorage.TryResolvePath(_env, orgId, key, out var absolutePath))
        {
            return NotFound();
        }

        var contentType = CardAttachmentStorage.GuessContentType(absolutePath);
        var stream = global::System.IO.File.OpenRead(absolutePath);
        return File(stream, contentType);
    }
}
