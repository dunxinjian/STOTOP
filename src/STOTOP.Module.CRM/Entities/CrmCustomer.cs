using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

/// <summary>
/// CRM客户（主键为 F编号，不继承 BaseEntity）
/// </summary>
public class CrmCustomer : IOrgScoped
{
    /// <summary>编号（主键）</summary>
    public string FCode { get; set; } = string.Empty;
    public string? FServiceObjectId { get; set; }
    /// <summary>客户简称</summary>
    public string FShortName { get; set; } = string.Empty;
    /// <summary>客户全称</summary>
    public string? FFullName { get; set; }
    public string? FContact { get; set; }
    public string? FPhone { get; set; }
    public string? FIndustry { get; set; }
    public string? FScale { get; set; }
    public int FStatus { get; set; } = 1;
    public long FOrgId { get; set; }
    public long? FBdEmployeeId { get; set; }
    public long? FMaintenanceEmployeeId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // ===== 客户扩展属性（对应 EXP业务对象 F类型=1，原位于 EXP业务对象，2026-04 回归到此） =====
    public string? FSenderAddress { get; set; }
    public string? FOfficeAddress { get; set; }
    public string? FCargoType { get; set; }
    public decimal? FPrepayPerTicket { get; set; }
    public string? FAttachmentPath { get; set; }
    public string? FSourceClientType { get; set; }
    public string? FSettlementModeText { get; set; }
    public string? FWarehouseCategory { get; set; }
    public string? FCutoffTime { get; set; }
    public string? FRequiredArea { get; set; }
    public string? FDailyOrderVolume { get; set; }
    public string? FSkuStructure { get; set; }
    public string? FCombinedProducts { get; set; }
    public string? FPlatform { get; set; }
    public string? FExpressPriority { get; set; }
    public string? FRemoteDelivery { get; set; }
    public string? FReturnRestock { get; set; }
    public string? FCustomerSoftware { get; set; }
    public string? FTempClientId { get; set; }

    // Navigation
    public List<CrmCustomerContact> Contacts { get; set; } = new();
    public List<CrmCustomerTransfer> Transfers { get; set; } = new();
    public List<CrmVisitRecord> VisitRecords { get; set; } = new();
    public List<CrmServiceOrder> ServiceOrders { get; set; } = new();
    public List<CrmCustomerProfit> Profits { get; set; } = new();
    public List<CrmCustomerAccount> Accounts { get; set; } = new();
}
