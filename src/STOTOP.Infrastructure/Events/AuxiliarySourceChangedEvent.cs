namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 辅助核算数据源变更事件
/// 当源数据（客户/供应商/部门/员工/品牌）名称变更时触发
/// </summary>
public class AuxiliarySourceChangedEvent : BusinessEvent
{
    /// <summary>来源类型：CRM客户/SUP供应商/SYS组织架构/SYS用户/EXP品牌</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>来源记录ID</summary>
    public long SourceId { get; set; }

    /// <summary>新名称</summary>
    public string NewName { get; set; } = string.Empty;

    /// <summary>新编码（可选，仅在编码也变更时设置）</summary>
    public string? NewCode { get; set; }
}
