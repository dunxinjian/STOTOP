using global::System.ComponentModel.DataAnnotations.Schema;
using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

[Table("FinAttachment")]
public class FinAttachment : BaseEntity, IOrgScoped
{
    [Column("FAccountSetId")]
    public long FAccountSetId { get; set; }

    public long FOrgId { get; set; }  // 组织ID

    /// <summary>业务类型：voucher / journal</summary>
    [Column("FBusinessType")]
    public string FBusinessType { get; set; } = string.Empty;

    /// <summary>关联的凭证ID或日记账ID</summary>
    [Column("FBusinessId")]
    public long FBusinessId { get; set; }

    [Column("FFileName")]
    public string FFileName { get; set; } = string.Empty;

    [Column("FOriginalName")]
    public string FOriginalName { get; set; } = string.Empty;

    [Column("FFilePath")]
    public string FFilePath { get; set; } = string.Empty;

    [Column("FFileSize")]
    public long FFileSize { get; set; }

    [Column("FContentType")]
    public string FContentType { get; set; } = string.Empty;

    [Column("FUploadTime")]
    public DateTime FUploadTime { get; set; } = DateTime.Now;

    [Column("FUploaderId")]
    public long FUploaderId { get; set; }

    [Column("FUploaderName")]
    public string FUploaderName { get; set; } = string.Empty;
}
