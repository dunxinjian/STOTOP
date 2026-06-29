using STOTOP.Core.Models;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IPointService
{
    /// <summary>手动奖分（管理员操作）</summary>
    Task<ApiResult<PointRecordListDto>> AwardAsync(long orgId, long operatorId, ManualAwardRequest request, int accountType = PointAccountTypes.B);
    /// <summary>手动扣分</summary>
    Task<ApiResult<PointRecordListDto>> DeductAsync(long orgId, long operatorId, ManualDeductRequest request, int accountType = PointAccountTypes.B);
    /// <summary>核心方法：接收事件 → 匹配规则 → 检查上限 → 计算积分 → 创建记录或申请</summary>
    Task<ApiResult<bool>> TriggerEventAsync(PointEventDto eventDto);
    /// <summary>积分流水分页</summary>
    Task<ApiResult<PagedResult<PointRecordListDto>>> GetRecordsPagedAsync(long orgId, PointRecordPagedRequest request);
    /// <summary>我的积分明细</summary>
    Task<ApiResult<PagedResult<PointRecordListDto>>> GetMyRecordsAsync(long orgId, long userId, PointRecordPagedRequest request);
    /// <summary>查询账户（聚合 A+B）</summary>
    Task<ApiResult<PointAccountDto>> GetAccountAsync(long orgId, long userId);
    /// <summary>按账户类型查询单一账户行</summary>
    Task<ApiResult<PointAccountDto>> GetAccountByTypeAsync(long orgId, long userId, int accountType);
    /// <summary>按指定日期计算账户余额（半开区间，清算 Job 使用）</summary>
    Task<ApiResult<int>> GetAccountBalanceAtDateAsync(long orgId, long userId, int accountType, DateTime atDate);
    /// <summary>我的账户</summary>
    Task<ApiResult<PointAccountDto>> GetMyAccountAsync(long orgId, long userId);
    /// <summary>统计看板</summary>
    Task<ApiResult<PointStatisticsDto>> GetStatisticsAsync(long orgId, long userId);
}
