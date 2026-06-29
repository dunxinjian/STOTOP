using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

// 注：会计期间不实现 IOrgScoped。
// 其归属由 FAccountSetId 关联的账套（FinAccountSet 不隔离）间接管理；
// FOrgId 仅作记录字段保留，不再受全局组织过滤器作用，
// 避免在严格模式下 FOrgId=0 的种子期间被全部过滤导致下拉框为空。
public class FinAccountPeriod : BaseEntity
{
    public int FYear { get; set; }
    public int FPeriodNo { get; set; }
    public DateTime FStartDate { get; set; }
    public DateTime FEndDate { get; set; }
    public int FIsClosed { get; set; }
    public int FStatus { get; set; }
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }  // 组织ID（记录字段，不参与全局过滤）
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
