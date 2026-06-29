using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Contract.Events;

namespace STOTOP.Module.Contract.EventHandlers;

/// <summary>
/// 合同即将到期事件处理器 - 为接收人推送 WorkHub 提醒。
/// </summary>
public class ContractExpiringEventHandler : IEventHandler<ContractExpiringEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ContractExpiringEventHandler> _logger;

    public ContractExpiringEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ContractExpiringEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ContractExpiringEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.RecipientId, new WorkItemDto
            {
                Id = @event.ContractId,
                Source = "contract",
                Category = "reminder",
                Priority = @event.DaysRemaining <= 7 ? 3 : 2,
                Title = $"合同到期提醒：{@event.ContractNo}",
                Summary = $"合同 {@event.ContractNo} 将于 {@event.ExpiryDate:yyyy-MM-dd} 到期，剩余 {@event.DaysRemaining} 天",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.RecipientId);

            _logger.LogInformation("合同到期事件处理完成: ContractId={ContractId}, ContractNo={ContractNo}, DaysRemaining={Days}",
                @event.ContractId, @event.ContractNo, @event.DaysRemaining);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理合同到期事件失败: ContractId={ContractId}", @event.ContractId);
        }
    }
}

/// <summary>
/// 合同签署事件处理器 - 向签署人推送 WorkHub 通知。
/// </summary>
public class ContractSignedEventHandler : IEventHandler<ContractSignedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ContractSignedEventHandler> _logger;

    public ContractSignedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ContractSignedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ContractSignedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.SignedByUserId <= 0)
            {
                _logger.LogDebug("合同签署事件未指定签署人，跳过通知: ContractId={ContractId}", @event.ContractId);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.SignedByUserId, new WorkItemDto
            {
                Id = @event.ContractId,
                Source = "contract",
                Category = "notification",
                Priority = 2,
                Title = $"合同已签署生效：{@event.ContractNo}",
                Summary = $"合同 [{@event.ContractNo}] 已完成签署并生效",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.SignedByUserId);

            _logger.LogInformation("合同签署事件处理完成: ContractId={ContractId}, ContractNo={ContractNo}",
                @event.ContractId, @event.ContractNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理合同签署事件失败: ContractId={ContractId}", @event.ContractId);
        }
    }
}

/// <summary>
/// 合同已过期事件处理器 - 为负责人推送 WorkHub 告警。
/// </summary>
public class ContractExpiredEventHandler : IEventHandler<ContractExpiredEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ContractExpiredEventHandler> _logger;

    public ContractExpiredEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ContractExpiredEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ContractExpiredEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.ResponsibleUserId <= 0)
            {
                _logger.LogDebug("合同过期事件未指定负责人，跳过通知: ContractId={ContractId}", @event.ContractId);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.ResponsibleUserId, new WorkItemDto
            {
                Id = @event.ContractId,
                Source = "contract",
                Category = "alert",
                Priority = 3,
                Title = $"合同已过期：{@event.ContractNo}",
                Summary = $"合同 {@event.ContractNo} 已于 {@event.ExpiryDate:yyyy-MM-dd} 过期，请及时处理。",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.ResponsibleUserId);

            _logger.LogInformation("合同过期事件处理完成: ContractId={ContractId}, ContractNo={ContractNo}",
                @event.ContractId, @event.ContractNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理合同过期事件失败: ContractId={ContractId}", @event.ContractId);
        }
    }
}
