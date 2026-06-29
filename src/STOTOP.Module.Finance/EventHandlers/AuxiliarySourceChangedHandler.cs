using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.EventHandlers;

/// <summary>
/// 辅助核算数据源变更事件处理器
/// 当源数据名称变更时，自动同步所有匹配的辅助核算项名称
/// </summary>
public class AuxiliarySourceChangedHandler : IEventHandler<AuxiliarySourceChangedEvent>
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<AuxiliarySourceChangedHandler> _logger;

    public AuxiliarySourceChangedHandler(
        STOTOPDbContext dbContext,
        ILogger<AuxiliarySourceChangedHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(AuxiliarySourceChangedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // 按 FSourceType + FSourceId 查找所有匹配的辅助核算项
            var items = await _dbContext.Set<FinAuxiliaryItem>()
                .Where(i => i.FSourceType == @event.SourceType && i.FSourceId == @event.SourceId)
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
            {
                _logger.LogDebug("未找到匹配的辅助核算项: SourceType={SourceType}, SourceId={SourceId}",
                    @event.SourceType, @event.SourceId);
                return;
            }

            foreach (var item in items)
            {
                item.FName = @event.NewName;
                if (@event.NewCode != null)
                    item.FCode = @event.NewCode;
                item.FUpdatedTime = DateTime.Now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "已同步 {Count} 个辅助核算项名称: SourceType={SourceType}, SourceId={SourceId}, NewName={NewName}",
                items.Count, @event.SourceType, @event.SourceId, @event.NewName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "同步辅助核算项名称失败: SourceType={SourceType}, SourceId={SourceId}",
                @event.SourceType, @event.SourceId);
        }
    }
}
