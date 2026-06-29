using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.PPV.Events;

namespace STOTOP.Module.Points.EventHandlers;

/// <summary>
/// 订阅 PPV 月度汇总完成事件，联动积分模块自动奖扣分。
/// 业务规则：
///   - BScoreChange > 0 → Award B 分（周期清算）
///   - BScoreChange &lt; 0 → Deduct B 分
///   - AScoreChange > 0 → Award A 分（终身资本化）
/// 幂等键：(OrgId, RelatedEventType="PpvMonthlyAggregated", RelatedEventId="{EmployeeId}_{Period}", AccountType)
/// 由 PointService.AwardAsync / DeductAsync 内置的事件幂等机制保证重复事件不重复奖扣。
/// </summary>
public class PpvMonthlyAggregatedEventHandler : IEventHandler<PpvMonthlyAggregatedEvent>
{
    private const string EventTypeKey = "PpvMonthlyAggregated";

    private readonly IPointService _pointService;
    private readonly ILogger<PpvMonthlyAggregatedEventHandler> _logger;

    public PpvMonthlyAggregatedEventHandler(IPointService pointService, ILogger<PpvMonthlyAggregatedEventHandler> logger)
    {
        _pointService = pointService;
        _logger = logger;
    }

    public async Task HandleAsync(PpvMonthlyAggregatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.OrgId <= 0 || @event.EmployeeId <= 0 || string.IsNullOrEmpty(@event.Period))
            {
                _logger.LogWarning("PPV 月度汇总事件字段缺失：OrgId={OrgId}, EmployeeId={EmployeeId}, Period={Period}",
                    @event.OrgId, @event.EmployeeId, @event.Period);
                return;
            }

            // 事件幂等键：同一员工 + 期间只入账一次
            var eventId = $"{@event.EmployeeId}_{@event.Period}";

            // 1) B 分变动
            if (@event.BScoreChange > 0)
            {
                var awardResult = await _pointService.AwardAsync(
                    @event.OrgId,
                    @event.EmployeeId,
                    new ManualAwardRequest
                    {
                        UserId = @event.EmployeeId,
                        PointValue = @event.BScoreChange,
                        Remark = $"PPV 月度产值奖励 B 分 期间:{@event.Period}",
                        RelatedEventType = EventTypeKey,
                        RelatedEventId = eventId,
                        RelatedModule = "PPV",
                        RelatedEntityType = "PpvMonthlyResult",
                        RelatedEntityId = @event.MonthlyResultId > 0 ? @event.MonthlyResultId : null
                    },
                    PointAccountTypes.B);

                if (awardResult.Code != 200)
                {
                    _logger.LogWarning("PPV B 分奖励失败：员工={EmployeeId}, 期间={Period}, Msg={Msg}",
                        @event.EmployeeId, @event.Period, awardResult.Message);
                }
            }
            else if (@event.BScoreChange < 0)
            {
                var deductResult = await _pointService.DeductAsync(
                    @event.OrgId,
                    @event.EmployeeId,
                    new ManualDeductRequest
                    {
                        UserId = @event.EmployeeId,
                        PointValue = Math.Abs(@event.BScoreChange),
                        Remark = $"PPV 月度产值扣减 B 分 期间:{@event.Period}",
                        RelatedEventType = EventTypeKey,
                        RelatedEventId = eventId,
                        RelatedModule = "PPV",
                        RelatedEntityType = "PpvMonthlyResult",
                        RelatedEntityId = @event.MonthlyResultId > 0 ? @event.MonthlyResultId : null
                    },
                    PointAccountTypes.B);

                if (deductResult.Code != 200)
                {
                    _logger.LogWarning("PPV B 分扣减失败：员工={EmployeeId}, 期间={Period}, Msg={Msg}",
                        @event.EmployeeId, @event.Period, deductResult.Message);
                }
            }

            // 2) A 分变动（仅奖励，A 分不可扣减）
            if (@event.AScoreChange > 0)
            {
                var awardResult = await _pointService.AwardAsync(
                    @event.OrgId,
                    @event.EmployeeId,
                    new ManualAwardRequest
                    {
                        UserId = @event.EmployeeId,
                        PointValue = @event.AScoreChange,
                        Remark = $"PPV 月度产值奖励 A 分 期间:{@event.Period}",
                        RelatedEventType = EventTypeKey,
                        RelatedEventId = eventId,
                        RelatedModule = "PPV",
                        RelatedEntityType = "PpvMonthlyResult",
                        RelatedEntityId = @event.MonthlyResultId > 0 ? @event.MonthlyResultId : null
                    },
                    PointAccountTypes.A);

                if (awardResult.Code != 200)
                {
                    _logger.LogWarning("PPV A 分奖励失败：员工={EmployeeId}, 期间={Period}, Msg={Msg}",
                        @event.EmployeeId, @event.Period, awardResult.Message);
                }
            }

            _logger.LogInformation(
                "PPV 积分联动完成：员工={EmployeeId}, 期间={Period}, BScoreChange={BChange}, AScoreChange={AChange}",
                @event.EmployeeId, @event.Period, @event.BScoreChange, @event.AScoreChange);
        }
        catch (Exception ex)
        {
            // 单条异常隔离：记录日志，不抛出（避免影响 PpvCalcJob 主流程及其他订阅方）
            _logger.LogError(ex, "PPV 积分联动失败：员工={EmployeeId}, 期间={Period}",
                @event.EmployeeId, @event.Period);
        }
    }
}
