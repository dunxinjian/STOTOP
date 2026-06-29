using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案（按品牌管理）
/// </summary>
public class ExpCostPlan : BaseEntity, IOrgScoped
{
    /// <summary>品牌编码</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>方案名称</summary>
    public string FPlanName { get; set; } = string.Empty;
    /// <summary>状态 0=草稿 1=启用 2=停用</summary>
    public int FStatus { get; set; } = 0;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 导航属性
    /// <summary>成本项列表</summary>
    public List<ExpCostPlanItem> Items { get; set; } = new();
    /// <summary>互斥配置列表</summary>
    public List<ExpCostPlanExclusion> Exclusions { get; set; } = new();
}
