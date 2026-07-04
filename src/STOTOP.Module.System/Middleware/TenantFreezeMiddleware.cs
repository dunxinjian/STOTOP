using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Middleware;

/// <summary>
/// 欠费冻结白名单中间件（多租户阶段4B·D7）。置于 <see cref="OrgContextMiddleware"/> 之后（CurrentTenantId 已就绪）。
/// 当前请求所属租户 <c>PLT租户.FStatus=欠费冻结(4)</c> 时：
/// <list type="bullet">
/// <item>放行：一切只读 GET（含财务结账视图/科目余额等结账类只读）——除【批量导出/全量拉数】路径。</item>
/// <item>拒绝：业务写（POST/PUT/DELETE/PATCH）与批量导出/下载路径 → 402 Payment Required。</item>
/// </list>
/// 登录/续费不受影响：/api/auth/*、/api/platform/*（平台超管续费/解冻）已在下方 Skip 列表。
/// <para>单客户(MDSTO)现为正式(Active) → 本中间件恒放行，是【休眠】能力（多客户欠费才生效）。</para>
/// 租户状态查 PLT租户（平台层表、无租户过滤器），带 15s TTL 内存缓存，避免每请求查库。
/// </summary>
public class TenantFreezeMiddleware
{
    private readonly RequestDelegate _next;

    // 冻结态无关的路径：登录/平台(续费·解冻)/健康检查/文档/实时。
    // 【终审修】org-context 只豁免【读/切换】具体路径,不整前缀豁免——否则 org-context 下的成员写端点
    // (POST/PUT/DELETE user-organizations) 会绕过冻结门。切换/我的组织/我的租户/当前上下文 是导航只读须放行;
    // "switch" 前缀经 StartsWith 同时覆盖 switch-tenant。
    private static readonly string[] SkipPaths =
    {
        "/api/auth/", "/api/platform/", "/setup", "/health", "/swagger", "/hubs/",
        "/api/system/org-context/my-organizations",
        "/api/system/org-context/my-tenants",
        "/api/system/org-context/switch",
        "/api/system/org-context/current",
    };

    // 冻结时即便 GET 也禁：批量导出/下载/全量拉数（按路径子串启发式识别）。
    private static readonly string[] ExportMarkers = { "export", "download", "导出", "下载", "batchget", "bulk" };

    private static readonly ConcurrentDictionary<long, (int Status, DateTime Expiry)> StatusCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TenantFreezeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        if (ShouldSkip(path)
            || context.User.Identity?.IsAuthenticated != true
            || context.Items["CurrentTenantId"] is not long tenantId)
        {
            await _next(context);
            return;
        }

        var status = await GetTenantStatusAsync(context, tenantId);
        if (status != (int)PltTenantStatus.Frozen)
        {
            await _next(context);
            return;
        }

        // 冻结态：写全禁；GET 仅禁批量导出/下载。
        var method = context.Request.Method;
        var isMutation = HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
                      || HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method);
        var isExport = ExportMarkers.Any(m => path.Contains(m));

        if (isMutation || isExport)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            context.Response.ContentType = "application/json";
            var result = ApiResult.Fail("租户已欠费冻结，请续费后使用（冻结期仅放行只读，批量导出与业务写已禁）", 402);
            await context.Response.WriteAsync(JsonSerializer.Serialize(result, CamelCase));
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(string path) => SkipPaths.Any(p => path.StartsWith(p));

    private static async Task<int> GetTenantStatusAsync(HttpContext context, long tenantId)
    {
        if (StatusCache.TryGetValue(tenantId, out var e) && e.Expiry > DateTime.UtcNow)
            return e.Status;

        int status;
        try
        {
            var db = context.RequestServices.GetRequiredService<STOTOPDbContext>();
            // PLT租户 非 ITenantScoped（无租户过滤器）→ LINQ 直查安全；provider-agnostic 可 InMemory 测。
            var row = await db.Set<PltTenant>()
                .Where(t => t.FID == tenantId)
                .Select(t => (int?)t.FStatus)
                .FirstOrDefaultAsync();
            // 查不到租户行 → 当作正式(不冻结)，绝不因缺行误锁全站。
            status = row ?? (int)PltTenantStatus.Active;
        }
        catch
        {
            // PLT租户 尚未建立(升级窗口) → fail-open(不冻结)，避免地基缺失误锁。
            status = (int)PltTenantStatus.Active;
        }

        StatusCache[tenantId] = (status, DateTime.UtcNow.Add(CacheTtl));
        return status;
    }
}
