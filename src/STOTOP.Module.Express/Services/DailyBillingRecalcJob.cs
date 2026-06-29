using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 日终计费重算任务：每日定时重算指定日期范围内的运单
/// 场景：日间维护了报价、层级关系等基础数据后，需要在日终统一重算
/// </summary>
public class DailyBillingRecalcJob
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<DailyBillingRecalcJob> _logger;

    public DailyBillingRecalcJob(
        STOTOPDbContext dbContext,
        ILogger<DailyBillingRecalcJob> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行日终重算
    /// </summary>
    /// <param name="targetDate">目标日期，默认为 T-2（2天前）</param>
    public async Task ExecuteAsync(DateTime? targetDate = null)
    {
        var date = targetDate ?? DateTime.Today.AddDays(-2);
        _logger.LogInformation("开始日终计费重算, 目标日期: {Date:yyyy-MM-dd}", date);

        try
        {
            // Step 1: 删除目标日期的计费成本明细（通过运单日期关联）
            var deletedCost = await _dbContext.Database.ExecuteSqlRawAsync(
                @"DELETE cbd FROM [EXP出港运单_计费结果_成本明细] cbd
                  INNER JOIN [EXP出港运单_计费结果] br ON cbd.[F计费结果ID] = br.[FID]
                  WHERE br.[F运单日期] = @date",
                new SqlParameter("@date", date));
            _logger.LogInformation("已删除 {Count} 条成本明细", deletedCost);

            // Step 2: 删除目标日期的计费结果
            var deletedResult = await _dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM [EXP出港运单_计费结果] WHERE [F运单日期] = @date",
                new SqlParameter("@date", date));
            _logger.LogInformation("已删除 {Count} 条计费结果", deletedResult);

            // Step 3: 将目标日期的所有运单重置为未计费
            var resetCount = await ResetBillingStatus(date);
            _logger.LogInformation("已重置 {Count} 条运单为未计费状态", resetCount);

            // Step 4: 新的 BillingEngine 需要 sourceTable + batchId + waybills 参数，
            // 日终重算场景下数据已从 STG 导入，重算需要知道原始的
            // sourceTable、batchId 和列映射配置。
            // 暂时简化处理：仅重置状态，通过 Pipeline 重新触发 PricingPlugin 完成重算。
            // TODO: 后续完善方向：
            //   1. 从导入批次记录反查该日期关联的 batchId 和 sourceTable
            //   2. 从 STG 表重新加载运单数据并调用 BillingEngine.ExecuteAsync
            //   3. 或者重置 STG 表中的 F计算状态=0，由 Pipeline 自动重新触发
            _logger.LogWarning(
                "日终重算已重置 {Count} 条运单的计费数据，需要通过 Pipeline 重新触发 PricingPlugin 完成重算，目标日期: {Date:yyyy-MM-dd}",
                resetCount, date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日终计费重算失败, 目标日期: {Date:yyyy-MM-dd}", date);
            throw;
        }
    }

    /// <summary>
    /// 将指定日期的运单 FBillingStatus 重置为 0（未计费），并清除归属网点
    /// </summary>
    private async Task<int> ResetBillingStatus(DateTime date)
    {
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE [EXP出港运单] 
              SET [F计费状态] = 0, [F归属网点ID] = NULL 
              WHERE [F运单日期] = @date AND [F计费状态] <> 0",
            new SqlParameter("@date", date));
        return affected;
    }
}
