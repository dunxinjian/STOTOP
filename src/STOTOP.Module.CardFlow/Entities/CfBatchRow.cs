using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 批次明细：批次内逐行暂存数据
/// 状态机：0=待质检, 1=质检通过, 2=质检失败, 3=已创建卡片, 4=已忽略, 5=已撤销
/// </summary>
public class CfBatchRow : BaseEntity
{
    public long FBatchId { get; set; }
    public int FRowNo { get; set; }
    /// <summary>原始行数据 JSON（按 schema 字段命名映射后）</summary>
    public string FDataJson { get; set; } = string.Empty;
    /// <summary>0=待质检, 1=质检通过, 2=质检失败, 3=已创建卡片, 4=已忽略, 5=已撤销</summary>
    public int FStatus { get; set; }
    public string? FErrorMessage { get; set; }
    /// <summary>关联生成的卡片ID（创建卡片成功后回填）</summary>
    public long? FCardId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
