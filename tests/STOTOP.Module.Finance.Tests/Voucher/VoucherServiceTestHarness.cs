using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Tests.Voucher;

/// <summary>
/// 构造一个全量接线的 VoucherService 供集成测试使用。
/// 通过 DefaultHttpContext 注入 CurrentOrgId / X-AccountSet-Id 头 / 用户身份。
/// </summary>
public static class VoucherServiceTestHarness
{
    public sealed class NoOpEventDispatcher : IEventDispatcher
    {
        public Task PublishAsync<T>(T @event) where T : BusinessEvent => Task.CompletedTask;
    }

    public static IHttpContextAccessor HttpContext(long orgId, long accountSetId, long userId = 1, string userName = "tester")
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["CurrentOrgId"] = orgId;
        if (accountSetId > 0)
            ctx.Request.Headers["X-AccountSet-Id"] = accountSetId.ToString();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userName),
        }, "Test"));
        return new HttpContextAccessor { HttpContext = ctx };
    }

    public static VoucherService Build(STOTOPDbContext db, IHttpContextAccessor http)
    {
        var opLog = new OperationLogService(
            new Repository<FinOperationLog>(db), http, NullLogger<OperationLogService>.Instance);
        var changeTracking = new ChangeTrackingService(
            new Repository<FinChangeHistory>(db), http);
        return new VoucherService(
            new Repository<FinVoucher>(db),
            new Repository<FinVoucherEntry>(db),
            new Repository<FinAccount>(db),
            new Repository<FinAccountPeriod>(db),
            opLog,
            changeTracking,
            http,
            new NoOpEventDispatcher(),
            db);
    }

    // 注：FinAccount 实现 IAccountSetScoped（无 FOrgId 字段，组织归属由账套间接管理），
    // 故 orgId 参数仅为保持各测试调用点签名一致而保留，不写入实体。
    public static FinAccount Account(long id, string code, string name, long accountSetId, long orgId)
        => new() { FID = id, FCode = code, FName = name, FAccountSetId = accountSetId, FEnableStatus = 1 };

    public static FinAccountPeriod Period(long id, int year, int periodNo, long accountSetId, int isClosed = 0)
        => new()
        {
            FID = id, FYear = year, FPeriodNo = periodNo, FAccountSetId = accountSetId, FIsClosed = isClosed,
            FStartDate = new DateTime(year, periodNo, 1),
            FEndDate = new DateTime(year, periodNo, DateTime.DaysInMonth(year, periodNo)),
        };
}
