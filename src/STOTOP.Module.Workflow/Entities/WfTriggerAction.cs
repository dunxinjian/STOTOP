using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF触发动作 - 注册所有可发起的业务动作</summary>
public class WfTriggerAction : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FKey { get; set; } = string.Empty;           // 唯一标识：datacenter.upload
    public string FLabel { get; set; } = string.Empty;         // 显示名称：上传数据
    public string? FIcon { get; set; }                         // 图标名（Ant Design Vue icon）
    public string FModule { get; set; } = string.Empty;        // 所属模块
    public string FRoute { get; set; } = string.Empty;         // 点击跳转路由
    public string FCategory { get; set; } = "create";          // 发起类别：upload/create/apply
    public string? FRequiredPermission { get; set; }           // 所需权限码（为空表示只需模块访问权限）
    public int FOrder { get; set; } = 0;                       // 排序
    public bool FIsEnabled { get; set; } = true;               // 是否启用
    public string? FDescription { get; set; }                  // 动作描述/提示文字
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
