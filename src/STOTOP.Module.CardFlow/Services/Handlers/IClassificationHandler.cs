using STOTOP.Module.CardFlow.Models;

namespace STOTOP.Module.CardFlow.Services.Handlers;

public interface IClassificationHandler
{
    string HandlerType { get; }
    Task<HandlerResult> HandleAsync(HandlerContext context);
}

public class HandlerContext
{
    public long BatchId { get; set; }
    public string TargetTable { get; set; } = string.Empty;
    public ClassificationItem Classification { get; set; } = default!;
    public string? HandlerConfig { get; set; }  // JSON配置
    public long OrgId { get; set; }              // 所属组织
    public long CreatorId { get; set; }          // 创建者用户ID（系统自动处理时使用系统用户ID）
    public long? PeriodId { get; set; }          // 财务会计期间ID（可选，凭证处理器需要）
    public long? AccountSetId { get; set; }      // 账套ID（可选，凭证处理器需要）
    public DateTime? BusinessDate { get; set; }  // 暂存表中的业务日期
}

public class HandlerResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object> Output { get; set; } = new();

    public static HandlerResult Ok(string message = "处理成功") => new() { Success = true, Message = message };
    public static HandlerResult Fail(string message) => new() { Success = false, Message = message };
}
