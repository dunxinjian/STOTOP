using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsClaim : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long? FPolicyId { get; set; }                           // FK → INS保单
    public int FBusinessType { get; set; }                         // 冗余
    public long FRelatedObjectId { get; set; }
    public string? FRelatedObjectName { get; set; }
    public string FClaimNumber { get; set; } = string.Empty;       // 出险编号
    public DateOnly FClaimDate { get; set; }                       // 出险日期
    public string? FClaimLocation { get; set; }                    // 出险地点
    public int FAccidentType { get; set; }                         // 1=碰撞,2=侧翻,3=火灾,4=盗抢,5=自然灾害,6=其他
    public string? FAccidentDescription { get; set; }              // 事故描述
    public string? FCounterpartyInfo { get; set; }                 // 对方信息
    public decimal? FEstimatedLoss { get; set; }                   // 预估损失金额
    public decimal? FActualLoss { get; set; }                      // 实际损失金额
    public int? FLiabilityDivision { get; set; }                   // 1=全责,2=主责,3=同责,4=次责,5=无责
    public long? FPartyId { get; set; }                            // 当事人ID
    public string? FPartyName { get; set; }                        // 当事人姓名
    public string? FCaseNumber { get; set; }                       // 报案号
    public string? FClaimImages { get; set; }                      // 出险图片 JSON数组
    public int FClaimStatus { get; set; } = 1;                     // 1=已登记,2=处理中,3=已结案
    public DateTime? FClosedDate { get; set; }                     // 结案日期
    public string? FClosedRemark { get; set; }                     // 结案说明
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public InsPolicy? Policy { get; set; }
    public List<InsSettlement> Settlements { get; set; } = new();
}
