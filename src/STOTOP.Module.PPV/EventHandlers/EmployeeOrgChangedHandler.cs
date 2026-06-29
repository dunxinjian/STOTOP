using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.PPV.EventHandlers;

/// <summary>
/// 订阅员工组织/岗位变更事件，提示运维同步调整 PPV 产值模板配置。
///
/// 设计取舍：
///   - PPV 产值模板通常含有岗位产值项 / 单价等人工设置的参数，
///     不能在员工调岗时简单覆盖；故本 Handler 仅"通知/告警"，不做自动改写。
///   - 后续可扩展为：写入"PPV 模板待调整"待办表 / 工作项，由运维人员在后台确认。
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
                "员工组织变更通知（PPV 产值模板待检查）：员工ID={UserId}, 变更类型={ChangeType}, 生效日期={EffectiveDate}, OrgId={OrgId}. " +
                "请检查 PPV 产值模板是否需要同步调整。",
                @event.F关联用户ID, @event.F变更类型, @event.F生效日期, @event.OrgId);

            // TODO: 后续可扩展为写入待办提醒表，由运维在后台确认调整
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理员工组织变更事件（PPV 模板通知）失败：UserId={UserId}",
                @event.F关联用户ID);
        }

        return Task.CompletedTask;
    }
}
