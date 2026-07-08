using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IVoucherPreviewService
{
    /// <summary>M5-3 凭证试算：草稿节点 + 样例卡片数据 → 借贷分录预览。诚实降级：真试算需运行时上下文时 success=false。</summary>
    Task<VoucherPreviewDto> PreviewVoucherAsync(
        long definitionId,
        VoucherPreviewRequest request,
        CancellationToken cancellationToken = default);
}
