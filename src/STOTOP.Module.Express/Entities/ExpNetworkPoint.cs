using STOTOP.Core.Models;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递网点（主键为 F编号，不继承 BaseEntity）
/// </summary>
public class ExpNetworkPoint : IOrgOwned, ITenantScoped
{
    /// <summary>编号（主键）</summary>
    public string FCode { get; set; } = string.Empty;
    /// <summary>网点简称</summary>
    public string? FShortName { get; set; }
    /// <summary>组织ID（组织扩展，指向SYS组织架构.FID）</summary>
    public long FOrgId { get; set; }
    /// <summary>所属组织ID（数据隔离用）</summary>
    public long FOwnerOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>网点级别</summary>
    public int FPointLevel { get; set; } = 1;
    /// <summary>是否一级网点 1是 0否</summary>
    public int FIsPrimaryPoint { get; set; } = 1;
    /// <summary>覆盖区域</summary>
    public string? FCoverageArea { get; set; }
    /// <summary>日处理能力</summary>
    public int? FDailyCapacity { get; set; }
    /// <summary>仓储面积</summary>
    public decimal? FStorageArea { get; set; }
    /// <summary>营业时间</summary>
    public string? FBusinessHours { get; set; }
    /// <summary>地址</summary>
    public string? FAddress { get; set; }
    /// <summary>负责人</summary>
    public string? FManager { get; set; }
    /// <summary>联系电话</summary>
    public string? FContactPhone { get; set; }
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    // ===== BU网点扩展属性 =====
    /// <summary>网点全称</summary>
    public string? FFullName { get; set; }
    /// <summary>M5：所属网点公司ID（→ SYS网点公司.FID）。现网网点均挂区域公司节点、无公司映射，故暂空——归属分配是业务任务。</summary>
    public long? FCompanyId { get; set; }
    /// <summary>M5：品牌编码（→ EXP品牌.F编码，如 ST=申通）。回填自旧 F快递品牌 死字段。</summary>
    public string? FBrandCode { get; set; }
    /// <summary>揽收员编码</summary>
    public string? FPickupEmployeeCode { get; set; }
    /// <summary>上级网点编号</summary>
    public string? FParentPointCode { get; set; }
    /// <summary>排序</summary>
    public int? FSortOrder { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    /// <summary>关联组织</summary>
    public SysOrganization? Organization { get; set; }
}
