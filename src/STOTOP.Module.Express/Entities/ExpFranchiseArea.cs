using STOTOP.Core.Models;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 承包区（主键为 F编号，不继承 BaseEntity）
/// </summary>
public class ExpFranchiseArea : IOrgOwned
{
    /// <summary>编号（主键）</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>组织ID（组织扩展，指向SYS组织架构.FID）</summary>
    public long FOrgId { get; set; }
    /// <summary>所属组织ID（数据隔离用）</summary>
    public long FOwnerOrgId { get; set; }
    /// <summary>承包人</summary>
    public string? FContractor { get; set; }
    /// <summary>承包开始日期</summary>
    public DateOnly? FContractStartDate { get; set; }
    /// <summary>承包结束日期</summary>
    public DateOnly? FContractEndDate { get; set; }
    /// <summary>覆盖片区</summary>
    public string? FCoverageDistrict { get; set; }
    /// <summary>承包费</summary>
    public decimal? FContractFee { get; set; }
    /// <summary>联系电话</summary>
    public string? FContactPhone { get; set; }
    /// <summary>地址</summary>
    public string? FAddress { get; set; }
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    // ===== 源系统迁移字段（BU承包区扩展属性） =====
    /// <summary>源UID</summary>
    public string? FSourceUid { get; set; }
    /// <summary>负责人身份证</summary>
    public string? FContractorIdCard { get; set; }
    /// <summary>紧急联系方式</summary>
    public string? FEmergencyContact { get; set; }
    /// <summary>派费标准</summary>
    public string? FDeliveryFeeRate { get; set; }
    /// <summary>结算周期原值</summary>
    public string? FSettlementCycleText { get; set; }
    /// <summary>银行账号</summary>
    public string? FBankAccount { get; set; }
    /// <summary>支付宝账号</summary>
    public string? FAlipayAccount { get; set; }
    /// <summary>三段码</summary>
    public string? FThreeSegmentCode { get; set; }
    /// <summary>排序</summary>
    public int? FSortOrder { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    /// <summary>关联组织</summary>
    public SysOrganization? Organization { get; set; }
}
