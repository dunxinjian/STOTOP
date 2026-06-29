using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Finance.Events;

namespace STOTOP.Module.Finance.EventHandlers;

/// <summary>
/// 凭证待审核事件处理器 - 向审核人推送 WorkHub 待办。
/// </summary>
public class VoucherPendingAuditEventHandler : IEventHandler<VoucherPendingAuditEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<VoucherPendingAuditEventHandler> _logger;

    public VoucherPendingAuditEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<VoucherPendingAuditEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(VoucherPendingAuditEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.AuditorId <= 0)
            {
                _logger.LogDebug("凭证待审核事件未指定审核人，跳过通知: VoucherId={VoucherId}", @event.VoucherId);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.AuditorId, new WorkItemDto
            {
                Id = @event.VoucherId,
                Source = "finance",
                Category = "todo",
                Priority = 2,
                Title = $"凭证待审核：{@event.VoucherNo}",
                Summary = $"凭证 {@event.VoucherNo} 已提交，金额 {@event.Amount:N2}，请审核。",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.AuditorId);

            _logger.LogInformation("已向审核人推送凭证待审核通知: VoucherId={VoucherId}, AuditorId={AuditorId}",
                @event.VoucherId, @event.AuditorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理凭证待审核事件失败: VoucherId={VoucherId}", @event.VoucherId);
        }
    }
}

/// <summary>
/// 账期关闭事件处理器 - 向操作人推送 WorkHub 通知。
/// </summary>
public class AccountPeriodClosedEventHandler : IEventHandler<AccountPeriodClosedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<AccountPeriodClosedEventHandler> _logger;

    public AccountPeriodClosedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<AccountPeriodClosedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(AccountPeriodClosedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.TriggeredByUserId <= 0)
            {
                _logger.LogDebug("账期关闭事件未指定操作人，跳过通知: PeriodId={PeriodId}", @event.PeriodId);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.TriggeredByUserId, new WorkItemDto
            {
                Id = @event.PeriodId,
                Source = "finance",
                Category = "notification",
                Priority = 2,
                Title = $"账期结账完成：{@event.PeriodName}",
                Summary = $"账期 [{@event.PeriodName}] 已成功结账",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.TriggeredByUserId);

            _logger.LogInformation("账期关闭事件处理完成: PeriodId={PeriodId}, PeriodName={PeriodName}",
                @event.PeriodId, @event.PeriodName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理账期关闭事件失败: PeriodId={PeriodId}", @event.PeriodId);
        }
    }
}
