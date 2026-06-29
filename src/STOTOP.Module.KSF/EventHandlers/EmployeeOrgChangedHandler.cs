using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.KSF.EventHandlers;

/// <summary>
/// 订阅员工组织/岗位变更事件，提示运维同步调整 KSF 员工经营单元映射。
///
/// 设计取舍：
///   - KSF 员工经营单元映射（PmKsfEmployeeMapping 等）通常含有人工设置的分摊比例 / 绩效参数，
///     不能在员工调岗时简单覆盖；故本 Handler 仅"通知/告警"，不做自动改写。
///   - 后续可扩展为：写入"KSF 映射待调整"待办表 / 工作项，由运维人员在后台确认。
/// </summary>
public class EmployeeOrgChangedHandler : IEventHandler<EmployeeOrgChangedEvent>
{
    private readonly ILogger<EmployeeOrgChangedHandler> _logger;

    public EmployeeOrgChangedHandler(ILogger<EmployeeOrgChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(EmployeeOrgChangedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning(
                "员工组织变更通知（KSF 映射待调整）：员工ID={UserId}, 变更类型={ChangeType}, 生效日期={EffectiveDate}, OrgId={OrgId}. " +
                "请检查 KSF 员工经营单元映射 是否需要同步调整。",
                @event.F关联用户ID, @event.F变更类型, @event.F生效日期, @event.OrgId);

            // TODO: 后续可扩展为写入待办提醒表（如 PmKsfMappingPendingAdjust），由运维在后台确认调整
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理员工组织变更事件（KSF 映射通知）失败：UserId={UserId}",
                @event.F关联用户ID);
        }

        return Task.CompletedTask;
    }
}
