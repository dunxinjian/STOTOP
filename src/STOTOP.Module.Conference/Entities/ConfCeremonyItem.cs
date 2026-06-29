using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>仪式流程环节</summary>
public class ConfCeremonyItem : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public string FName { get; set; } = string.Empty;  // 环节名称
    public string FStartTime { get; set; } = string.Empty;  // HH:mm
    public int FDuration { get; set; } = 5;  // 时长（分钟）
    public string? FResponsible { get; set; }  // 负责人
    public string? FMusic { get; set; }  // 背景音乐
    public string? FLighting { get; set; }  // 灯光方案
    public string? FProps { get; set; }  // 道具物品
    public string? FRemark { get; set; }
    public int FSort { get; set; }  // 排序
    public string FPhase { get; set; } = "仪式";  // 阶段：迎宾/仪式/宴席/送客
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 导航属性
    public ConfEvent Event { get; set; } = null!;
}
