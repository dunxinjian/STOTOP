using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Workflow.Jobs;
using STOTOP.Module.Workflow.Services;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow;

public static class WorkflowModuleExtensions
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
    {
        // 核心服务
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<ITriggerActionService, TriggerActionService>();
        services.AddScoped<IChainService, ChainService>();
        services.AddScoped<IDispatchEngine, DispatchEngine>();
        services.AddScoped<IIssueAggregator, IssueAggregator>();
        services.AddScoped<IRevokeService, RevokeService>();
        services.AddSingleton<IWorkItemCallback, WorkItemCallback>();

        // Jobs
        services.AddScoped<WorkItemTimeoutJob>();

        return services;
    }
}
