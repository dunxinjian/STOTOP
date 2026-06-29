using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 凭证落库纯规则：期间解析、结账校验、归属鉴权。
/// 不依赖 DbContext，便于单元测试；VoucherService 负责取数后委派到这里。
/// </summary>
public static class VoucherPostingRules
{
    /// <summary>
    /// 按 日期 + 账套 在候选期间中定位唯一期间；查不到抛错（缺账期一律拒绝落库）。
    /// 假设同账套的期间互不重叠（建账时保证）；OrderByDescending(FStartDate) 仅作防御性兜底，
    /// 万一存在重叠取开始日期最晚者，勿因"反正不重叠"而删除此排序。
    /// </summary>
    public static FinAccountPeriod ResolvePeriod(
        IEnumerable<FinAccountPeriod> candidates, DateTime date, long accountSetId)
    {
        var period = candidates
            .Where(p => p.FAccountSetId == accountSetId
                && p.FStartDate <= date
                && p.FEndDate >= date)
            .OrderByDescending(p => p.FStartDate)
            .FirstOrDefault();
        if (period == null)
            throw new InvalidOperationException(
                $"未找到账套 {accountSetId} 在 {date:yyyy-MM-dd} 对应的账期，请先建立该期间");
        return period;
    }

    /// <summary>
    /// 校验期间未结账，已结账则拒绝落库（FIsClosed: 1=已结账）。
    /// </summary>
    public static void EnsureOpenForPosting(FinAccountPeriod period)
    {
        if (period.FIsClosed == 1)
            throw new InvalidOperationException(
                $"{period.FYear}年第{period.FPeriodNo}期已结账，不能在该期间登记/修改凭证");
    }

    /// <summary>
    /// 归属鉴权谓词：凭证是否对当前上下文可见/可操作。
    /// currentOrgId==0（无 HttpContext，后台任务）→ 不约束；
    /// currentAccountSetId==0（无 X-AccountSet-Id 请求头）→ 仅校验组织。
    /// </summary>
    public static bool IsAccessible(FinVoucher voucher, long currentOrgId, long currentAccountSetId)
        => (currentOrgId == 0 || voucher.FOrgId == currentOrgId)
        && (currentAccountSetId == 0 || voucher.FAccountSetId == currentAccountSetId);
}
