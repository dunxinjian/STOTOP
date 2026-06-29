using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgShentongDeliveryDaily : BaseEntity, IStagingRecord
{
    // ===== 暂存系统列 (IStagingRecord + ExcelInput 依赖) =====
    public long F批次ID { get; set; }
    public int? F原始行号 { get; set; }            // ExcelInput BulkCopy 无条件写 -> 必须有
    public int F处理状态 { get; set; }
    public string? F错误信息 { get; set; }
    public long? F关联凭证ID { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public string? FDataScopeId { get; set; }      // ExcelInput 纯导入恒 NULL，仅满足模型
    public long? FSourceWorkItemId { get; set; }   // 同上恒 NULL
    public bool FIsRevoked { get; set; }
    public long FOrgId { get; set; }
    public long? F账套ID { get; set; }              // 仅入库，不参与去重/取数（按组织隔离）
    public string? F归属网点编号 { get; set; }     // 取数过滤列（OutletResolver 规范化）
    public string? F其他列数据 { get; set; }        // ExcelInput BulkCopy 无条件写 -> 必须有
    public string? F业务主键 { get; set; }          // keyFields 哈希，ExcelInput 无条件写 -> 必须有
    public string? F流水号 { get; set; }            // string? 可空！派件 rule 不配 serialNumberRule -> 恒 NULL

    // ===== 业务列 (Excel 23 列；件量 int / 金额 decimal(18,4)) =====
    public DateTime? F结算日期 { get; set; }        // date 列
    public string? F网点编号 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F承包区编号 { get; set; }
    public string? F承包区名称 { get; set; }
    public string? F业务员编码 { get; set; }
    public string? F业务员名称 { get; set; }
    public int? F基础派费收费件量 { get; set; }     // = 派件量，报表用
    public decimal? F基础派费收费金额 { get; set; }
    public decimal? F基础派费收费调整金额 { get; set; }
    public decimal? F正常派件退费收金额 { get; set; }
    public decimal? F周期性派费收金额 { get; set; }
    public decimal? F大货计重收费金额 { get; set; }
    public int? F违规重量罚款收件量 { get; set; }
    public decimal? F违规重量罚款收金额 { get; set; }
    public int? F基础派费和时效拦截弃件付费件量合计 { get; set; }
    public decimal? F基础派费和时效拦截弃件付费金额合计 { get; set; }
    public int? F综合KPI奖励派费件量 { get; set; }
    public decimal? F综合KPI奖励派费金额 { get; set; }
    public decimal? F考核奖励派费金额 { get; set; }
    public decimal? F补贴派费付费金额 { get; set; }
    public decimal? F周期性派费付费金额 { get; set; }
    public decimal? F大货计重付费金额 { get; set; }
}
