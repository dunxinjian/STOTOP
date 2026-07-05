using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 卡片附件本地存储：存到 ContentRoot/secure-uploads/cardflow-attachments/{orgId}/{yyyy-MM}/{guid}{ext}。
/// 刻意避开 Program.cs 的 /uploads 静态服务目录——卡片附件（可能含报销票据等敏感件）只能经授权下载端点读取，
/// 不走静态直链。storageKey 形如 "{orgId}/{yyyy-MM}/{guid}{ext}"，下载时按当前组织校验归属并防目录穿越。
/// 纯 IO 无状态，做成静态类，无需 DI（避免改动并发中的 CardFlowModuleExtensions）。
/// </summary>
public static class CardAttachmentStorage
{
    private const string RootFolder = "secure-uploads";
    private const string AttachmentsFolder = "cardflow-attachments";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar", ".ofd"
    };

    public sealed record SavedAttachment(string StorageKey, string OriginalName, long Size, string ContentType);

    public static async Task<SavedAttachment> SaveAsync(IWebHostEnvironment env, IFormFile file, long orgId)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException("附件为空");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException($"不支持的附件类型：{ext}");
        }

        var month = DateTime.Now.ToString("yyyy-MM");
        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absoluteDir = Path.Combine(env.ContentRootPath, RootFolder, AttachmentsFolder, orgId.ToString(), month);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(absoluteDir, fileName);
        await using (var fs = File.Create(absolutePath))
        {
            await file.CopyToAsync(fs);
        }

        // storageKey 统一用正斜杠，跨平台稳定
        var storageKey = $"{orgId}/{month}/{fileName}";
        return new SavedAttachment(storageKey, file.FileName, file.Length, file.ContentType ?? "application/octet-stream");
    }

    /// <summary>
    /// 按当前组织解析 storageKey 为物理路径：校验首段归属组织（防跨组织读取）+ 防目录穿越。非法/越权返回 false。
    /// </summary>
    public static bool TryResolvePath(IWebHostEnvironment env, long orgId, string? storageKey, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return false;
        }

        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var segments = normalizedKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // 首段必须等于当前组织，且至少 {orgId}/{month}/{file} 三段
        if (segments.Length < 3 || segments[0] != orgId.ToString())
        {
            return false;
        }

        var baseDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, RootFolder, AttachmentsFolder, orgId.ToString()));
        var candidate = Path.GetFullPath(Path.Combine(
            env.ContentRootPath, RootFolder, AttachmentsFolder,
            normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

        // 防目录穿越：解析后仍须落在本组织目录内
        if (!candidate.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(candidate))
        {
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    public static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain; charset=utf-8",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            _ => "application/octet-stream"
        };
    }
}
