namespace STOTOP.Module.KSF.Services;

/// <summary>
/// KSF 指标取数 Agent 接口（KsfIndicator.F取数类型 = 2 时使用）。
/// 实现类通过 DI 注册为 Keyed Service（key = KsfIndicator.F取数Agent）。
/// </summary>
public interface IKsfIndicatorAgent
{
    /// <summary>
    /// 按组织 / 期间 / 员工 / 经营单元 取得指标实际值。
    /// </summary>
    /// <param name="orgId">组织ID</param>
    /// <param name="period">期间 yyyyMM</param>
    /// <param name="employeeId">员工ID（SysUser.FID）</param>
    /// <param name="businessUnitId">经营单元ID（可空）</param>
    /// <param name="paramsJson">KsfIndicator.F取数参数JSON（可空）</param>
    Task<decimal> FetchValueAsync(long orgId, string period, long employeeId, long? businessUnitId, string? paramsJson);
}
