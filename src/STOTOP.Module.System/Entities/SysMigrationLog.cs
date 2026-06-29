namespace STOTOP.Module.System.Entities;

public class SysMigrationLog
{
    public long FID { get; set; }
    public string F模块 { get; set; } = string.Empty;
    public int F版本号 { get; set; }
    public string? F描述 { get; set; }
    public string F状态 { get; set; } = string.Empty;
    public string? F错误消息 { get; set; }
    public DateTime F执行时间 { get; set; }
    public long? F耗时ms { get; set; }
    public string? F实例标识 { get; set; }
}
