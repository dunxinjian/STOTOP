namespace STOTOP.Module.CardFlow;

/// <summary>
/// 数据导入模块权限编码常量
/// </summary>
public static class CardFlowPermissions
{
    // 模块级
    public const string Manage = "cardflow:manage";
    
    // 菜单级
    public const string Home = "cardflow:home";
    public const string UploadCenter = "cardflow:upload-center";
    public const string FileManager = "cardflow:file-manager";
    public const string Automation = "cardflow:automation";
    public const string Staging = "cardflow:staging";
    public const string Hangfire = "cardflow:hangfire";
    
    // 按钮/操作级 - 导入操作
    public const string ImportUpload = "cardflow:import:upload";
    public const string ImportProcess = "cardflow:import:process";
    public const string ImportValidation = "cardflow:import:validation";
    
    // 按钮/操作级 - 文件管理
    public const string FileManagerView = "cardflow:file-manager:view";
    public const string FileManagerDelete = "cardflow:file-manager:delete";
    
    // 按钮/操作级 - 自动下载
    public const string AutomationManage = "cardflow:automation:manage";
    
    // 按钮/操作级 - 异常派发
    public const string DispatchManage = "cardflow:dispatch:manage";
    
    // 菜单级 - 派发规则（统一管理）
    public const string DispatchRule = "cardflow:dispatch-rules";
    
    // 按钮/操作级 - 派发规则
    public const string DispatchRuleView = "cardflow:dispatch-rule:view";
    public const string DispatchRuleManage = "cardflow:dispatch-rule:manage";

    // 质量中心
    public const string QualityCenter = "cardflow:quality";
    public const string QualityDashboard = "cardflow:quality:dashboard";
    public const string QualityIssueTypeView = "cardflow:quality:issue-type:view";
    public const string QualityIssueTypeManage = "cardflow:quality:issue-type:manage";
}
