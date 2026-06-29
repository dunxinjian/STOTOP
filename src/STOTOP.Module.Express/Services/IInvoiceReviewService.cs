using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IInvoiceReviewService
{
    Task AutoReviewAsync(long invoiceId);
    Task ManualReviewAsync(long invoiceId, bool approved, string? remark);
    Task ReverseReviewAsync(long invoiceId, string? remark);
    Task<List<ReviewRuleDto>> GetRulesAsync();
    Task<ReviewRuleDto> CreateRuleAsync(CreateReviewRuleRequest request);
    Task<ReviewRuleDto?> UpdateRuleAsync(long id, UpdateReviewRuleRequest request);
    Task<bool> DeleteRuleAsync(long id);
}
