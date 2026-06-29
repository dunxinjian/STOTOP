using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAmoebaPLItem : BaseEntity
{
    public long FTemplateId { get; set; }
    public string FItemName { get; set; } = string.Empty;
    public string FNodeRole { get; set; } = "data"; // 节点角色，值域: group/data/formula/indicator
    public string? FFormula { get; set; }
    public int FSort { get; set; }
    public long FParentId { get; set; }
    public string? FRelatedAccountsJson { get; set; }
    public string? FDataSource { get; set; }          // F数据源（值域：voucher/manual/formula/estimate；voucher_brand/voucher_other 已废弃）
    public string? FSummaryKeywordsJson { get; set; } // F摘要关键词JSON
    public string? FBillingFilterJson { get; set; }    // F计费过滤Json（计费数据源筛选条件）
    public string? FUnit { get; set; }                  // 单位：元/票/KG/%/人/件·人·日/KM/米·方/平米
    public string? FDataSourceRemark { get; set; }      // 数据来源说明（如"网点管家-有偿流量流向报表"）
    public string? FCalculationLogic { get; set; }      // 计算逻辑说明（如"基础派费+补贴派费+..."）
    public string? FPerUnitMode { get; set; }           // 单票&均计算方式：auto/manual/none
    public int? F小数位数 { get; set; }            // 数值显示小数位数：null=按单位自动判断(默认2位), 1~4
    public bool FIsManualEntry { get; set; }            // 是否手工填报项

    // 分摊配置（阿米巴方案B 终态，替代已删的固定比例分摊表）
    public string? F分摊方式 { get; set; }              // null/"direct"=直接归段; "volume"=按件量分摊
    public string? F分摊基数 { get; set; }              // "send"|"deliver"|"total"（仅 F分摊方式="volume" 时有效）
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }

    // 正交二维字段（新增）
    public string? F项目类别 { get; set; }       // indicator / revenue / cost / profit / section
    public bool F是否指标分区 { get; set; }       // 标识全局唯一 indicator section
    public string? F值来源 { get; set; }          // system / formula / manual
    public string? F系统数据源 { get; set; }      // voucher / billing / estimate / depreciation
}
