using STOTOP.Core.Models;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace STOTOP.Module.Finance.Entities;

[Table("FinOperationLog")]
public class FinOperationLog : BaseEntity, IOrgScoped
{
    [Column("FAccountSetId")]
    public long FAccountSetId { get; set; }

    public long FOrgId { get; set; }  // 组织ID

    [Column("FModule")]
    public string FModule { get; set; } = string.Empty;  // 模块：凭证/结账/日记账

    [Column("FOperationType")]
    public string FOperationType { get; set; } = string.Empty;  // 操作类型：新增/审核/删除/结账/反结账

    [Column("FDescription")]
    public string FDescription { get; set; } = string.Empty;  // 操作描述

    [Column("FTargetId")]
    public long? FTargetId { get; set; }  // 目标记录ID（如凭证ID）

    [Column("FTargetCode")]
    public string? FTargetCode { get; set; }  // 目标编号（如凭证号）

    [Column("FOperatorId")]
    public long FOperatorId { get; set; }  // 操作人ID

    [Column("FOperatorName")]
    public string FOperatorName { get; set; } = string.Empty;  // 操作人名称

    [Column("FOperationTime")]
    public DateTime FOperationTime { get; set; } = DateTime.Now;

    [Column("FIpAddress")]
    public string? FIpAddress { get; set; }

    [Column("FExtraData")]
    public string? FExtraData { get; set; }  // JSON格式额外信息

    [Column("FCreatedTime")]
    public DateTime FCreatedTime { get; set; }

    [Column("FUpdatedTime")]
    public DateTime FUpdatedTime { get; set; }
}
