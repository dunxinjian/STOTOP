using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Task.EventHandlers;
using STOTOP.Module.Task.Events;
using STOTOP.Module.Task.Services;
using STOTOP.Module.Task.Services.DingTalk;

namespace STOTOP.Module.Task;

public static class TaskModuleExtensions
{
    /// <summary>
    /// 添加任务管理模块服务
    /// </summary>
    public static IServiceCollection AddTaskModule(this IServiceCollection services)
    {
        // 目标与关键成果
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IKeyResultService, KeyResultService>();

        // 项目
        services.AddScoped<IProjectService, ProjectService>();

        // 任务
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITaskCommentService, TaskCommentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IProgressReportService, ProgressReportService>();

        // 调度与提醒
        services.AddScoped<ITaskScheduleService, TaskScheduleService>();
        services.AddScoped<ITaskReminderService, TaskReminderService>();

        // 绩效
        services.AddScoped<IPerformanceService, PerformanceService>();

        // 通知
        services.AddScoped<INotificationService, NotificationService>();

        // 复盘与知识库
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();

        // 钉钉集成
        services.AddScoped<DingTalkApiClient>();
        services.AddScoped<IDingTalkTodoService, DingTalkTodoService>();
        services.AddScoped<IDingTalkMessageService, DingTalkMessageService>();

        // Event Handlers
        services.AddScoped<IEventHandler<TaskAssignedEvent>, TaskAssignedEventHandler>();
        services.AddScoped<IEventHandler<TaskStatusChangedEvent>, TaskStatusChangedEventHandler>();
        services.AddScoped<IEventHandler<TaskReminderDueEvent>, TaskReminderDueEventHandler>();

        return services;
    }
}
