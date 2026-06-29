namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 附件 DTO
/// </summary>
public class AttachmentDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public long BusinessId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadTime { get; set; }
    public long UploaderId { get; set; }
    public string UploaderName { get; set; } = string.Empty;
}
