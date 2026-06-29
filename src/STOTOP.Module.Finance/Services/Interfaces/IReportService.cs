using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IReportService
{
    // 科目余额表
    Task<List<AccountBalanceDto>> GetAccountBalanceAsync(long periodId, long? accountId = null, long accountSetId = 0);
    
    // 从凭证计算科目余额表（按年月）
    Task<List<AccountBalanceDto>> GetAccountBalanceByYearMonthAsync(int year, int month, long? accountId = null, long accountSetId = 0);
    
    // 辅助余额表
    Task<List<AuxiliaryBalanceDto>> GetAuxiliaryBalanceAsync(long periodId, string? auxiliaryType = null, long accountSetId = 0);
    
    // 资产余额表
    Task<List<AssetBalanceDto>> GetAssetBalanceAsync(long? periodId = null, long accountSetId = 0);
    
    // 利润表
    Task<List<ProfitStatementDto>> GetProfitStatementAsync(long startPeriodId, long endPeriodId, string format = "small", long accountSetId = 0);
    
    // 小企业利润表（按年月查询）
    Task<List<SmallEnterpriseProfitStatementDto>> GetSmallEnterpriseProfitStatementAsync(int year, int month, long accountSetId = 0);
    
    // 资产负债表
    Task<List<BalanceSheetDto>> GetBalanceSheetAsync(long periodId, long accountSetId = 0);
    
    // 资金流量表
    Task<List<CashFlowDto>> GetCashFlowAsync(long startPeriodId, long endPeriodId, long accountSetId = 0);
    
    // 应交税费表
    Task<List<TaxPayableDto>> GetTaxPayableAsync(long periodId, long accountSetId = 0);
    
    // 阿米巴损益表
    Task<AmoebaPLReportDto> GetAmoebaPLAsync(long startPeriodId, long endPeriodId, long? departmentId = null, long? amoebaId = null, long accountSetId = 0);
    
    // 重新计算科目余额
    Task<bool> RecalculateBalanceAsync(long periodId, long accountSetId = 0);
    
    // 科目明细账
    Task<AccountDetailResultDto> GetAccountDetailAsync(long accountId, int year, int periodNo, long accountSetId = 0);
    
    // 钻取明细
    Task<List<DrillDownItemDto>> GetDrillDownAsync(string reportType, int rowIndex, int year, int month, long accountSetId, string? accountCode = null);
    
    // 利润趋势
    Task<List<ProfitTrendDto>> GetProfitTrendAsync(int year, long accountSetId);
    
    // 收入构成
    Task<List<CompositionItemDto>> GetRevenueCompositionAsync(int year, int month, long accountSetId);
    
    // 费用构成
    Task<List<CompositionItemDto>> GetExpenseCompositionAsync(int year, int month, long accountSetId);
    
    // 同比对比
    Task<List<ComparisonDto>> GetYoYComparisonAsync(int year, int month, long accountSetId);
    
    // 环比对比
    Task<List<ComparisonDto>> GetMoMComparisonAsync(int year, int month, long accountSetId);
}
