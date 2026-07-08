using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IFlowVersionMigrationService
{
    /// <summary>发布后在途迁移（InflightPolicy=migrate）：把旧版本进行中卡片里「当前节点在新版本已不存在」的卡片
    /// 迁到新版本「原排序其后的第一个人工节点」；无法安全迁移的逐张记日志跳过，不整体回滚。</summary>
    Task<PublishFlowDefinitionResultDto> MigrateInflightCardsAsync(long definitionId, long operatorId);
}
