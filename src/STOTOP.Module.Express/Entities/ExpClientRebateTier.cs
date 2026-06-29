using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 客户返利阶梯
/// </summary>
public class ExpClientRebateTier : BaseEntity, IOrgScoped
{
    /// <summary>返利ID</summary>
    public long FRebateId { get; set; }
    /// <summary>最低票数</summary>
    public int FMinTickets { get; set; }
    /// <summary>最高票数</summary>
    public int? FMaxTickets { get; set; }
    /// <summary>每票返利</summary>
    public decimal? FRebatePerTicket { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
}
