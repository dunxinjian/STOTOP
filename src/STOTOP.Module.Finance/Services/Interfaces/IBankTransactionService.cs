using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IBankTransactionService
{
    // 交易渠道 CRUD
    Task<PagedResult<BankChannelDto>> GetChannelsAsync(BankChannelQueryRequest request);
    Task<List<BankChannelDto>> GetAllEnabledChannelsAsync();
    Task<BankChannelDto?> GetChannelByIdAsync(long id);
    Task<BankChannelDto> CreateChannelAsync(CreateBankChannelRequest request, string? operatorName);
    Task<BankChannelDto?> UpdateChannelAsync(long id, UpdateBankChannelRequest request, string? operatorName);
    Task<bool> DeleteChannelAsync(long id);

    // 流水导入
    Task<BankTransactionImportResult> ImportTransactionsAsync(BankTransactionImportRequest request, string? operatorName);

    // 流水查询
    Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(BankTransactionQueryRequest request);
    Task<BankTransactionDto?> GetTransactionByIdAsync(long id);

    // 智能匹配（简化版：按对方户名+金额匹配）
    Task<AutoMatchResult> AutoMatchAsync();

    // 人工匹配
    Task<bool> ManualMatchAsync(BankTransactionManualMatchRequest request, string? operatorName);

    // 无需匹配标记
    Task<int> SkipMatchAsync(BankTransactionSkipMatchRequest request, string? operatorName);
}
