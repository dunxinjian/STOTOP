using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>通用暂存表实体 — 用于动态注册的数据源</summary>
public class StgDynamicRecord : BaseEntity, IStagingRecord
{
    public long F批次ID { get; set; }
    public int F原始行号 { get; set; }
    public string F业务主键 { get; set; } = string.Empty;
    public string F数据源类型 { get; set; } = string.Empty;
    public string? F动态数据 { get; set; }  // JSON 格式存储动态字段
    public int F处理状态 { get; set; }
    public string? F错误信息 { get; set; }
    public long? F关联凭证ID { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public long FOrgId { get; set; }
    public string? FDataScopeId { get; set; }
    public long? FSourceWorkItemId { get; set; }
    public bool FIsRevoked { get; set; }
    public long? F账套ID { get; set; }
    public string? F归属网点编号 { get; set; }
}
