using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;

namespace STOTOP.WebAPI.Controllers.Mobile;

/// <summary>
/// 移动端文件上传控制器
/// </summary>
[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<UploadController> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 移动端文件直传
    /// </summary>
    [HttpPost("mobile")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> UploadMobile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Ok(new { code = 400, message = "文件不能为空" });
        }

        try
        {
            // TODO: 实际文件存储逻辑（本地/OSS）
            var uploadPath = _config["DataImport:UploadPath"] ?? "uploads";
            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadPath, "mobile", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/files/mobile/{fileName}";
            return Ok(new { code = 200, data = new { url, fileName = file.FileName, size = file.Length }, message = "上传成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移动端文件上传失败");
            return Ok(new { code = 500, message = "上传失败" });
        }
    }

    /// <summary>
    /// 钉钉 mediaId 转存
    /// </summary>
    [HttpPost("media-transfer")]
    public async Task<IActionResult> MediaTransfer([FromBody] MediaTransferRequest request)
    {
        if (string.IsNullOrEmpty(request.MediaId))
        {
            return Ok(new { code = 400, message = "mediaId 不能为空" });
        }

        try
        {
            // 1. 获取钉钉 AccessToken
            var appKey = _config["DingTalk:AppKey"] ?? "";
            var appSecret = _config["DingTalk:AppSecret"] ?? "";
            var client = _httpClientFactory.CreateClient();

            // 获取 access_token
            var tokenResp = await client.PostAsJsonAsync(
                "https://oapi.dingtalk.com/gettoken",
                new { appkey = appKey, appsecret = appSecret });
            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var accessToken = tokenJson.GetProperty("access_token").GetString() ?? "";

            // 2. 通过 mediaId 下载文件
            var downloadUrl = $"https://oapi.dingtalk.com/media/download?access_token={accessToken}&media_id={request.MediaId}";
            var fileBytes = await client.GetByteArrayAsync(downloadUrl);

            // 3. 存储文件
            var uploadPath = _config["DataImport:UploadPath"] ?? "uploads";
            var fileName = $"{Guid.NewGuid():N}.jpg"; // 默认图片格式
            var filePath = Path.Combine(uploadPath, "mobile", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            var url = $"/files/mobile/{fileName}";
            return Ok(new { code = 200, data = new { url }, message = "转存成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MediaId 转存失败: {MediaId}", request.MediaId);
            return Ok(new { code = 500, message = "转存失败" });
        }
    }

    public class MediaTransferRequest
    {
        public string MediaId { get; set; } = "";
    }
}
