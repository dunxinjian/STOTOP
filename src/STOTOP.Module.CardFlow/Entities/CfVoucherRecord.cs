using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 凭证生成记录（迁移自 DcVoucherGenerationRecord）
/// </summary>
public class CfVoucherRecord : BaseEntity
{
    /// <summary>关联批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>暂存表名</summary>
    public string FTargetTable { get; set; } = string.Empty;
    public int FTotalRows { get; set; }
    public int FMatchedRows { get; set; }
    public int FUnmatchedRows { get; set; }
    /// <summary>未匹配明细 JSON</summary>
    public string? FUnmatchedDetailsJson { get; set; }
    public int FGeneratedVoucherCount { get; set; }
    /// <summary>凭证ID列表 JSON</summary>
    public string? FVoucherIdsJson { get; set; }
    /// <summary>状态：0=进行中 1=全部成功 2=部分成功 3=全部失败</summary>
    public int FStatus { get; set; }
    public string? FErrorMessage { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime? FUpdatedTime { get; set; }
}
