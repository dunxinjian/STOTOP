using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.KSF.Events;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;

namespace STOTOP.Module.Points.EventHandlers;

/// <summary>
/// 订阅 KSF 月度核算结果事件，联动积分模块自动奖扣分。
/// 业务规则：
///   - KSF 超额完成（FloatingAmount &gt; 0）→ Award A 分（终身资本化）
///   - KSF 红线扣分（Deduction &gt; 0）    → Deduct B 分（周期清算）
/// 仅处理 ResultStatus == 2（正式）的结果，试算结果不联动。
/// 幂等键：(OrgId, RelatedEventType="KsfMonthlyResult", RelatedEventId="{EmployeeId}_{Period}", AccountType)
/// 由 PointService.AwardAsync / DeductAsync 内置的事件幂等机制（PmPointRecord.F关联事件类型 + F关联事件ID）保证重复事件不重复奖扣。
/// </summary>
public class KsfMonthlyResultEventHandler : IEventHandler<KsfMonthlyResultEvent>
{
    private const string EventTypeKey = "KsfMonthlyResult";

    private readonly IPointService _pointService;
    private readonly ILogger<KsfMonthlyResultEventHandler> _logger;

    public KsfMonthlyResultEventHandler(IPointService pointService, ILogger<KsfMonthlyResultEventHandler> logger)
    {
        _pointService = pointService;
        _logger = logger;
    }

    public async Task HandleAsync(KsfMonthlyResultEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // 仅处理正式状态的结果（试算/异常结果不联动积分）
            if (@event.ResultStatus != 2)
            {
                _logger.LogDebug("KSF 结果非正式状态，跳过积分联动：员工={EmployeeId}, 期间={Period}, 状态={Status}",
                    @event.EmployeeId, @event.Period, @event.ResultStatus);
                return;
            }

            if (@event.OrgId <= 0 || @event.EmployeeId <= 0 || string.IsNullOrEmpty(@event.Period))
            {
                _logger.LogWarning("KSF 月度结果事件字段缺失：OrgId={OrgId}, EmployeeId={EmployeeId}, Period={Period}",
                    @event.OrgId, @event.EmployeeId, @event.Period);
                return;
            }

            // 事件幂等键：同一员工 + 期间只入账一次
            var eventId = $"{@event.EmployeeId}_{@event.Period}";

            // 1) 超额奖励 → A 分（按 1元=1积分 默认换算，后续可改由 PmPointRule 配置驱动）
            if (@event.FloatingAmount > 0)
            {
                var awardPoints = (int)Math.Round(@event.FloatingAmount);
                if (awardPoints > 0)
                {
                    var awardResult = await _pointService.AwardAsync(
                        @event.OrgId,
                        @event.EmployeeId, // operatorId 用员工自身 ID 标记系统自动入账
                        new ManualAwardRequest
                        {
                            UserId = @event.EmployeeId,
                            PointValue = awardPoints,
                            Remark = $"KSF 超额奖励 期间:{@event.Period}",
                            RelatedEventType = EventTypeKey,
                            RelatedEventId = eventId,
                            RelatedModule = "ksf",
                            RelatedEntityType = "KsfResult",
                            RelatedEntityId = @event.ResultId > 0 ? @event.ResultId : null
                        },
                        PointAccountTypes.A);

                    if (awardResult.Code != 200)
                    {
                        _logger.LogWarning("KSF 超额奖励 A 分失败：员工={EmployeeId}, 期间={Period}, Msg={Msg}",
                            @event.EmployeeId, @event.Period, awardResult.Message);
                    }
                }
            }

            // 2) 红线扣分 → B 分
            if (@event.Deduction > 0)
            {
                var deductPoints = (int)Math.Round(@event.Deduction);
                if (deductPoints > 0)
                {
                    var deductResult = await _pointService.DeductAsync(
                        @event.OrgId,
                        @event.EmployeeId,
                        new ManualDeductRequest
                        {
                            UserId = @event.EmployeeId,
                            PointValue = deductPoints,
                            Remark = $"KSF 红线扣分 期间:{@event.Period}",
                            RelatedEventType = EventTypeKey,
                            RelatedEventId = eventId,
                            RelatedModule = "ksf",
                            RelatedEntityType = "KsfResult",
                            RelatedEntityId = @event.ResultId > 0 ? @event.ResultId : null
                        },
                        PointAccountTypes.B);

                    if (deductResult.Code != 200)
                    {
                        _logger.LogWarning("KSF 红线扣分 B 分失败：员工={EmployeeId}, 期间={Period}, Msg={Msg}",
                            @event.EmployeeId, @event.Period, deductResult.Message);
                    }
                }
            }

            _logger.LogInformation(
                "KSF 积分联动完成：员工={EmployeeId}, 期间={Period}, 浮动={Float}, 扣减={Deduct}",
                @event.EmployeeId, @event.Period, @event.FloatingAmount, @event.Deduction);
        }
        catch (Exception ex)
        {
            // 单条异常隔离：记录日志，不抛出（避免影响 KsfCalcJob 主流程及其他订阅方）
            _logger.LogError(ex, "KSF 积分联动失败：员工={EmployeeId}, 期间={Period}",
                @event.EmployeeId, @event.Period);
        }
    }
}
