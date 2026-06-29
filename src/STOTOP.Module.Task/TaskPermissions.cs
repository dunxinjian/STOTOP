namespace STOTOP.Module.Task;

/// <summary>
/// 任务管理模块权限编码常量
/// </summary>
public static class TaskPermissions
{
    // 目标
    public const string GoalView = "task:goal:view";
    public const string GoalCreate = "task:goal:create";
    public const string GoalEdit = "task:goal:edit";
    public const string GoalDelete = "task:goal:delete";
    public const string GoalDecompose = "task:goal:decompose";

    // 关键成果
    public const string KrView = "task:kr:view";
    public const string KrCreate = "task:kr:create";
    public const string KrEdit = "task:kr:edit";
    public const string KrDelete = "task:kr:delete";

    // 项目
    public const string ProjectView = "task:project:view";
    public const string ProjectCreate = "task:project:create";
    public const string ProjectEdit = "task:project:edit";
    public const string ProjectDelete = "task:project:delete";
    public const string ProjectMember = "task:project:member";

    // 任务
    public const string TaskView = "task:task:view";
    public const string TaskCreate = "task:task:create";
    public const string TaskEdit = "task:task:edit";
    public const string TaskDelete = "task:task:delete";
    public const string TaskAssign = "task:task:assign";

    // 看板
    public const string KanbanView = "task:kanban:view";

    // 调度
    public const string ScheduleManage = "task:schedule:manage";

    // 提醒
    public const string ReminderManage = "task:reminder:manage";

    // 标签
    public const string TagManage = "task:tag:manage";

    // 绩效
    public const string PerformanceView = "task:performance:view";
    public const string PerformanceManage = "task:performance:manage";
    public const string PerformanceExport = "task:performance:export";

    // 通知
    public const string NotificationView = "task:notification:view";

    // 复盘
    public const string ReviewView = "task:review:view";
    public const string ReviewCreate = "task:review:create";
    public const string ReviewEdit = "task:review:edit";
    public const string ReviewDelete = "task:review:delete";

    // 知识库
    public const string KnowledgeView = "task:knowledge:view";
    public const string KnowledgeCreate = "task:knowledge:create";
    public const string KnowledgeEdit = "task:knowledge:edit";
    public const string KnowledgeDelete = "task:knowledge:delete";
    public const string KnowledgeManage = "task:knowledge:manage";
}
