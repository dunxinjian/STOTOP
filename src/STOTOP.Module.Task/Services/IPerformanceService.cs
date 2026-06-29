using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IPerformanceService
{
    // ===== 考核周期管理 =====

    /// <summary>
    /// 考核周期分页列表
    /// </summary>
    Task<ApiResult<PagedResult<PerformancePeriodListDto>>> GetPeriodsPagedAsync(PerformancePeriodPagedRequest request, long orgId);

    /// <summary>
    /// 创建考核周期
    /// </summary>
    Task<ApiResult<PerformancePeriodListDto>> CreatePeriodAsync(CreatePerformancePeriodRequest request, long orgId, long operatorId);

    /// <summary>
    /// 更新考核周期
    /// </summary>
    Task<ApiResult<PerformancePeriodListDto>> UpdatePeriodAsync(long id, UpdatePerformancePeriodRequest request);

    /// <summary>
    /// 触发该周期绩效自动计算
    /// </summary>
    Task<ApiResult<bool>> CalculateAsync(long periodId);

    // ===== 考核记录 =====

    /// <summary>
    /// 获取周期内所有考核记录
    /// </summary>
    Task<ApiResult<List<PerformanceRecordListDto>>> GetRecordsByPeriodAsync(long periodId);

    /// <summary>
    /// 个人考核详情（含任务明细+各维度评分）
    /// </summary>
    Task<ApiResult<PerformanceRecordDetailDto>> GetRecordDetailAsync(long id);

    /// <summary>
    /// 提交自评（含各维度评分+自评文字）
    /// </summary>
    Task<ApiResult<bool>> SelfEvaluateAsync(long id, SelfEvaluateRequest request, long operatorId);

    /// <summary>
    /// 上级评分（含各维度评分+评语+考核等级S/A/B/C/D）
    /// </summary>
    Task<ApiResult<bool>> ReviewAsync(long id, SuperiorReviewRequest request, long operatorId);

    /// <summary>
    /// 我的绩效历史
    /// </summary>
    Task<ApiResult<List<PerformanceRecordListDto>>> GetMyPerformanceAsync(long userId, long orgId);

    /// <summary>
    /// 绩效看板（部门/团队统计）
    /// </summary>
    Task<ApiResult<PerformanceDashboardDto>> GetDashboardAsync(long orgId, long? periodId);

    // ===== 维度配置 =====

    /// <summary>
    /// 获取评价维度配置列表
    /// </summary>
    Task<ApiResult<List<PerformanceDimensionListDto>>> GetDimensionsAsync(long orgId);

    /// <summary>
    /// 创建评价维度
    /// </summary>
    Task<ApiResult<PerformanceDimensionListDto>> CreateDimensionAsync(CreatePerformanceDimensionRequest request, long orgId);

    /// <summary>
    /// 更新评价维度
    /// </summary>
    Task<ApiResult<PerformanceDimensionListDto>> UpdateDimensionAsync(long id, UpdatePerformanceDimensionRequest request);

    /// <summary>
    /// 删除评价维度
    /// </summary>
    Task<ApiResult<bool>> DeleteDimensionAsync(long id);
}
