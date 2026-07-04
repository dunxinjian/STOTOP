using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary><see cref="ITodoDispatchLogService"/> 默认实现。仅经 <see cref="STOTOPDbContext"/> 读写 CF待办分发日志。</summary>
public class TodoDispatchLogService : ITodoDispatchLogService
{
    private readonly STOTOPDbContext _db;

    public TodoDispatchLogService(STOTOPDbContext db) => _db = db;

    public async Task RecordDispatchAsync(long todoItemId, long tenantId, string channel, string? externalTaskId, string? corpId)
    {
        // 按 (待办, 渠道) 幂等 upsert；重推更新 taskId。分发在待办所属租户上下文内，显式落 FTenantId=该待办租户。
        var log = await _db.Set<CfTodoDispatchLog>()
            .FirstOrDefaultAsync(l => l.FTodoItemId == todoItemId && l.FChannel == channel);
        if (log == null)
        {
            _db.Set<CfTodoDispatchLog>().Add(new CfTodoDispatchLog
            {
                FTenantId = tenantId, FTodoItemId = todoItemId, FChannel = channel,
                FExternalTaskId = externalTaskId, FCorpId = corpId, FDispatchStatus = "dispatched",
            });
        }
        else
        {
            log.FExternalTaskId = externalTaskId;
            if (corpId != null) log.FCorpId = corpId;
            log.FDispatchStatus = "dispatched";
            log.FLastCallbackEvent = null;   // 重推 → 清幂等标记，新 taskId 的回调可再处理
            log.FLastCallbackAt = null;
            log.FUpdateTime = DateTime.Now;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<(long? TodoItemId, bool AlreadyProcessed)> TryBeginCallbackAsync(string externalTaskId, string eventType)
    {
        if (string.IsNullOrEmpty(externalTaskId)) return (null, false);

        // 回调匿名、无租户上下文 → IgnoreQueryFilters 读；据权威 taskId 绑定定位待办与租户。
        var log = await _db.Set<CfTodoDispatchLog>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.FExternalTaskId == externalTaskId);
        if (log == null) return (null, false);                       // 无分发记录 → 调用方 legacy 匹配 + 告警
        if (log.FLastCallbackEvent == eventType) return (log.FTodoItemId, true); // 同事件重放 → 幂等跳过

        log.FLastCallbackEvent = eventType;
        log.FLastCallbackAt = DateTime.Now;
        log.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
        return (log.FTodoItemId, false);
    }
}
