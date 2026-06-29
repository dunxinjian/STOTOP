using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.Task.Services;

namespace STOTOP.Module.Task.Tests;

// 模块命名空间 STOTOP.Module 下同时有 Task 与 System 子命名空间，会与 System.Threading.Tasks.Task 撞名；
// 在文件作用域命名空间「之后」用 global:: 声明别名（其作用域内于命名空间成员），消除非泛型 Task 歧义（泛型 Task<T> 不受影响）。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Task 服务测试用的 no-op 替身：积分 / 事件 / 日志 / 服务定位器，均不产生副作用。
/// 这些是 TaskService 等的构造依赖，但测试只验证服务自身逻辑，不验证其外部副作用。
/// </summary>
public static class TaskTestFakes
{
    public static IEventDispatcher EventDispatcher() => new NoOpEventDispatcher();
    public static IPointService PointService() => new NoOpPointService();
    public static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    /// <summary>供 KeyResultService（构造依赖 IServiceProvider，懒解析 IGoalService 以打破 Goal↔KR 循环）使用。</summary>
    public static IServiceProvider ServiceProvider(STOTOPDbContext db) => new MiniProvider(db);

    private sealed class NoOpEventDispatcher : IEventDispatcher
    {
        public Task PublishAsync<T>(T @event) where T : BusinessEvent => Task.CompletedTask;
    }

    private sealed class MiniProvider(STOTOPDbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IGoalService)) return new GoalService(db, new KeyResultService(db, this));
            if (serviceType == typeof(IKeyResultService)) return new KeyResultService(db, this);
            return null;
        }
    }

    /// <summary>TaskService 仅调用 TriggerEventAsync（返回成功即可）；其余成员测试路径不触达。</summary>
    private sealed class NoOpPointService : IPointService
    {
        public Task<ApiResult<bool>> TriggerEventAsync(PointEventDto eventDto)
            => Task.FromResult(ApiResult<bool>.Success(true));

        public Task<ApiResult<PointRecordListDto>> AwardAsync(long orgId, long operatorId, ManualAwardRequest request, int accountType) => throw new NotImplementedException();
        public Task<ApiResult<PointRecordListDto>> DeductAsync(long orgId, long operatorId, ManualDeductRequest request, int accountType) => throw new NotImplementedException();
        public Task<ApiResult<PagedResult<PointRecordListDto>>> GetRecordsPagedAsync(long orgId, PointRecordPagedRequest request) => throw new NotImplementedException();
        public Task<ApiResult<PagedResult<PointRecordListDto>>> GetMyRecordsAsync(long orgId, long userId, PointRecordPagedRequest request) => throw new NotImplementedException();
        public Task<ApiResult<PointAccountDto>> GetAccountAsync(long orgId, long userId) => throw new NotImplementedException();
        public Task<ApiResult<PointAccountDto>> GetAccountByTypeAsync(long orgId, long userId, int accountType) => throw new NotImplementedException();
        public Task<ApiResult<int>> GetAccountBalanceAtDateAsync(long orgId, long userId, int accountType, DateTime atDate) => throw new NotImplementedException();
        public Task<ApiResult<PointAccountDto>> GetMyAccountAsync(long orgId, long userId) => throw new NotImplementedException();
        public Task<ApiResult<PointStatisticsDto>> GetStatisticsAsync(long orgId, long userId) => throw new NotImplementedException();
    }
}
