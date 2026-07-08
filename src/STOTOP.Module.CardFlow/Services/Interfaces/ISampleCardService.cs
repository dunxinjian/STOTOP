using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ISampleCardService
{
    /// <summary>取该 definition 近 30 天卡片采样（上限 20 张），供干跑代入。dataJson 敏感字段脱敏，路由引用字段保留原值。</summary>
    Task<List<SampleCardDto>> GetSampleCardsAsync(
        long definitionId,
        string? keyword,
        CancellationToken cancellationToken = default);
}
