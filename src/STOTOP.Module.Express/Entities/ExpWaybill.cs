using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 运单
/// </summary>
public class ExpWaybill : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>运单号</summary>
    public string FWaybillNo { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>店铺名称</summary>
    public string FShopName { get; set; } = string.Empty;
    /// <summary>业务对象ID（F编号）</summary>
    public string? FBusinessObjectId { get; set; }
    /// <summary>业务对象类型 KH/DL/WD/YW/CB/YZ</summary>
    public string? FClientType { get; set; }
    /// <summary>寄件省</summary>
    public string? FSenderProvince { get; set; }
    /// <summary>目的省份ID</summary>
    public int? FReceiverProvinceId { get; set; }
    /// <summary>揽收重量</summary>
    public decimal? FPickupWeight { get; set; }
    /// <summary>中转重量</summary>
    public decimal? FTransitWeight { get; set; }
    /// <summary>到件重量</summary>
    public decimal? FDeliveryWeight { get; set; }
    /// <summary>集包重量</summary>
    public decimal? FBagWeight { get; set; }
    /// <summary>计泡重量</summary>
    public decimal? FBubbleWeight { get; set; }
    /// <summary>总部重量</summary>
    public decimal? FHqWeight { get; set; }
    /// <summary>一单到底</summary>
    public bool? FIsDirectDelivery { get; set; }
    /// <summary>结算实重</summary>
    public decimal? FActualWeight { get; set; }
    /// <summary>长</summary>
    public decimal? FLength { get; set; }
    /// <summary>宽</summary>
    public decimal? FWidth { get; set; }
    /// <summary>高</summary>
    public decimal? FHeight { get; set; }
    /// <summary>抛重（数据库计算列，只读）</summary>
    public decimal? FVolumetricWeight { get; set; }
    /// <summary>计费重量</summary>
    public decimal? FBillableWeight { get; set; }
    /// <summary>声明价值</summary>
    public decimal? FDeclaredValue { get; set; }
    /// <summary>运单日期</summary>
    public DateTime FWaybillDate { get; set; }
    /// <summary>导入批次ID</summary>
    public long? FImportBatchId { get; set; }
    /// <summary>客户别名</summary>
    public string? FClientAlias { get; set; }
    /// <summary>归属网点ID（链条最底层网点，计费时回填）</summary>
    public long? FNetworkPointId { get; set; }
    /// <summary>计费状态</summary>
    public int FBillingStatus { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
