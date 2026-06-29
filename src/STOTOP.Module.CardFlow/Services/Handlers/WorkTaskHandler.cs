extern alias TaskModule;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskModule::STOTOP.Module.Task.Dtos;
using TaskModule::STOTOP.Module.Task.Services;

namespace STOTOP.Module.CardFlow.Services.Handlers;

public class WorkTaskHandler : IClassificationHandler
{
    private readonly ITaskService _taskService;
    private readonly ILogger<WorkTaskHandler> _logger;

    public string HandlerType => "WorkTask";

    public WorkTaskHandler(ITaskService taskService, ILogger<WorkTaskHandler> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    public async Task<HandlerResult> HandleAsync(HandlerContext context)
    {
        // 1. 反序列化配置
        WorkTaskConfig? config = null;
        if (!string.IsNullOrWhiteSpace(context.HandlerConfig))
        {
            try
            {
                config = JsonSerializer.Deserialize<WorkTaskConfig>(context.HandlerConfig,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WorkTaskHandler: HandlerConfig 反序列化失败");
            }
        }

        config ??= new WorkTaskConfig();

        // 2. 模板变量替换
        var title = ReplaceTemplateVariables(config.TitleTemplate, context);
        var description = ReplaceTemplateVariables(config.DescriptionTemplate, context);

        // 3. 构建 CreateTaskRequest
        var request = new CreateTaskRequest
        {
            Title = title,
            Description = description,
            Priority = config.Priority,
            Type = config.TaskType,
            Visibility = config.Visibility,
            PlanStart = DateTime.Now,
            PlanEnd = DateTime.Now.AddDays(config.DaysUntilDue),
            AssigneeId = config.AssigneeId
        };

        // 4. 调用 TaskService 创建任务
        try
        {
            var result = await _taskService.CreateAsync(request, context.OrgId, context.CreatorId);

            if (result.Code == 200 && result.Data != null)
            {
                _logger.LogInformation("WorkTaskHandler: 成功创建任务 {TaskId}, 标题: {Title}",
                    result.Data.Id, title);
                var handlerResult = HandlerResult.Ok($"已创建工作任务: {result.Data.Id}");
                handlerResult.Output["TaskId"] = result.Data.Id;
                return handlerResult;
            }
            else
            {
                _logger.LogWarning("WorkTaskHandler: 创建任务失败 - {Message}", result.Message);
                return HandlerResult.Fail($"创建任务失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WorkTaskHandler: 调用 TaskService.CreateAsync 异常");
            return HandlerResult.Fail($"创建任务异常: {ex.Message}");
        }
    }

    private static string ReplaceTemplateVariables(string template, HandlerContext context)
    {
        return template
            .Replace("{BatchId}", context.BatchId.ToString())
            .Replace("{TargetTable}", context.TargetTable)
            .Replace("{ClassificationType}", context.Classification.Type)
            .Replace("{AffectedRowCount}", context.Classification.AffectedRowCount.ToString())
            .Replace("{Severity}", context.Classification.Severity);
    }

    private class WorkTaskConfig
    {
        public string TitleTemplate { get; set; } = "[数据异常] {TargetTable} - {ClassificationType} ({AffectedRowCount}条)";
        public string DescriptionTemplate { get; set; } = "导入批次 {BatchId} 检测到 {ClassificationType}，共 {AffectedRowCount} 条，严重级别：{Severity}";
        public int Priority { get; set; } = 2;
        public int DaysUntilDue { get; set; } = 3;
        public long? AssigneeId { get; set; }
        public int TaskType { get; set; } = 0;
        public int Visibility { get; set; } = 0;
    }
}
