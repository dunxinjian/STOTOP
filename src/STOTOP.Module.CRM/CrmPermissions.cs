namespace STOTOP.Module.CRM;

/// <summary>
/// CRM模块权限编码常量
/// </summary>
public static class CrmPermissions
{
    // 小组管理
    public const string GroupView = "crm:group:view";
    public const string GroupManage = "crm:group:manage";

    // 客户管理
    public const string CustomerView = "crm:customer:view";
    public const string CustomerCreate = "crm:customer:create";
    public const string CustomerEdit = "crm:customer:edit";
    public const string CustomerDelete = "crm:customer:delete";

    // 拜访记录
    public const string VisitView = "crm:visit:view";
    public const string VisitCreate = "crm:visit:create";
    public const string VisitEdit = "crm:visit:edit";

    // 服务工单
    public const string OrderView = "crm:order:view";
    public const string OrderCreate = "crm:order:create";
    public const string OrderEdit = "crm:order:edit";
    public const string OrderAssign = "crm:order:assign";

    // 服务反馈
    public const string FeedbackView = "crm:feedback:view";
    public const string FeedbackCreate = "crm:feedback:create";
    public const string FeedbackHandle = "crm:feedback:handle";

    // 推荐返佣
    public const string ReferralView = "crm:referral:view";
    public const string ReferralCreate = "crm:referral:create";
    public const string CommissionApply = "crm:commission:apply";
    public const string CommissionApprove = "crm:commission:approve";

    // 号段池
    public const string WaybillPoolView = "crm:waybillpool:view";
    public const string WaybillPoolManage = "crm:waybillpool:manage";

    // 预付款
    public const string PrepaymentView = "crm:prepayment:view";
    public const string PrepaymentCreate = "crm:prepayment:create";
    public const string PrepaymentAllocate = "crm:prepayment:allocate";

    // 毛利
    public const string ProfitView = "crm:profit:view";
    public const string ProfitCalc = "crm:profit:calc";

    // 奖金
    public const string BonusView = "crm:bonus:view";
    public const string BonusManage = "crm:bonus:manage";
}
