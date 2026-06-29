using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace STOTOP.Module.CardFlow.Services.Handlers;

public class InfoRecordHandler : IClassificationHandler
{
    private readonly ILogger<InfoRecordHandler> _logger;

    public string HandlerType => "InfoRecord";

    public InfoRecordHandler(ILogger<InfoRecordHandler> logger)
    {
        _logger = logger;
    }

    public Task<HandlerResult> HandleAsync(HandlerContext context)
    {
        // 1. 反序列化配置
        InfoRecordConfig? config = null;
        if (!string.IsNullOrWhiteSpace(context.HandlerConfig))
        {
            try
            {
                config = JsonSerializer.Deserialize<InfoRecordConfig>(context.HandlerConfig,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "InfoRecordHandler: HandlerConfig 反序列化失败");
            }
        }

        config ??= new InfoRecordConfig();

        // 2. 做变量替换
        var fields = new Dictionary<string, string>();
        foreach (var (key, template) in config.Fields)
        {
            fields[key] = ReplaceTemplateVariables(template, context);
        }

        // 3. 以指定 logLevel 记录结构化日志
        var logMessage = $"[信息记录] 分类处理记录 - {string.Join(", ", fields.Select(f => $"{f.Key}={f.Value}"))}";

        var logLevel = config.LogLevel.ToLower() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, logMessage);

        return Task.FromResult(HandlerResult.Ok("信息记录完成"));
    }

    private static string ReplaceTemplateVariables(string template, HandlerContext context)
    {
        var contextJson = context.Classification.Context != null
            ? JsonSerializer.Serialize(context.Classification.Context)
            : "";

        return template
            .Replace("{BatchId}", context.BatchId.ToString())
            .Replace("{TargetTable}", context.TargetTable)
            .Replace("{ClassificationType}", context.Classification.Type)
            .Replace("{AffectedRowCount}", context.Classification.AffectedRowCount.ToString())
            .Replace("{Severity}", context.Classification.Severity)
            .Replace("{Context}", contextJson);
    }

    private class InfoRecordConfig
    {
        public string LogLevel { get; set; } = "Information";
        public Dictionary<string, string> Fields { get; set; } = new()
        {
            ["F类型"] = "{ClassificationType}",
            ["F批次"] = "{BatchId}",
            ["F详情"] = "{Context}"
        };
    }
}
