namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 附件列表DTO
/// </summary>
public class AttachmentListDto
{
    public long Id { get; set; }
    public int RelationType { get; set; }
    public long RelationId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 上传附件请求（含关联类型+关联ID）
/// </summary>
public class UploadAttachmentRequest
{
    public int RelationType { get; set; }
    public long RelationId { get; set; }
}

/// <summary>
/// 附件下载响应
/// </summary>
public class AttachmentDownloadDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] FileData { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// 附件下载信息DTO（返回路径和文件名，由Controller处理实际IO）
/// </summary>
public class AttachmentDownloadInfoDto
{
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
}
