using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 公共费件量分摊基数：发件件量 / 派件件量 / 合计件量。
/// 由 F分摊基数(send/deliver/total) 决定取哪个分母。
/// </summary>
public sealed class VolumeBasis
{
    public long SendCount { get; init; }
    public long DeliverCount { get; init; }
    public long TotalCount => SendCount + DeliverCount;

    /// <summary>按 F分摊基数 取件量：deliver→派件、total→合计、其余(含 send/null)→发件。</summary>
    public long ByKind(string? kind) => kind switch
    {
        "deliver" => DeliverCount,
        "total" => TotalCount,
        _ => SendCount,
    };
}

/// <summary>
/// 闭合自检结果（严格闭合：同一划分维度内 Σ各scope分摊 应 = 全口径全额）。
/// 仅在「一次算全 outlet 全集」时有意义；单 scope 请求作信息性自检。
/// </summary>
public sealed class AllocationReconcileResult
{
    public long LeafId { get; init; }
    public decimal FullTotal { get; init; }
    public decimal ScopeSum { get; init; }
    public decimal Diff { get; init; }
    public bool IsBalanced { get; init; }
}

/// <summary>公共费按件量分摊引擎（纯函数，无 DB / 无副作用）。设计 spec §4。</summary>
public interface ICommonCostAllocationEngine
{
    /// <summary>
    /// 公共费按件量分摊：AllocatedAmount_i = 全口径全额_i × scope件量 / 全口径件量。
    /// 分子分母同 basis(各叶 F分摊基数) 配对；全额为0或缺失→跳过(不注入)；
    /// 全口径件量为0→该叶记0并告警(防除零)。返回 叶FID → 分摊额。
    /// </summary>
    Dictionary<long, decimal> Allocate(
        IReadOnlyList<FinAmoebaPLItem> commonCostLeaves,
        IReadOnlyDictionary<long, decimal> fullScopeTotals,
        VolumeBasis scopeVolume,
        VolumeBasis fullScopeVolume,
        out List<string> warnings);

    /// <summary>闭合自检：全额 与 各scope分摊之和 的差（严格闭合容差 0.05 元）。</summary>
    AllocationReconcileResult Reconcile(long leafId, decimal fullTotal, IEnumerable<decimal> perScopeAllocated);
}

public sealed class CommonCostAllocationEngine : ICommonCostAllocationEngine
{
    private const decimal ReconcileTolerance = 0.05m;

    public Dictionary<long, decimal> Allocate(
        IReadOnlyList<FinAmoebaPLItem> commonCostLeaves,
        IReadOnlyDictionary<long, decimal> fullScopeTotals,
        VolumeBasis scopeVolume,
        VolumeBasis fullScopeVolume,
        out List<string> warnings)
    {
        warnings = new List<string>();
        var result = new Dictionary<long, decimal>();
        foreach (var leaf in commonCostLeaves)
        {
            // 全额缺失或为0 → 不注入（保持稀疏，叶值由后续 SUM_CHILDREN/公式按0处理）
            if (!fullScopeTotals.TryGetValue(leaf.FID, out var total) || total == 0m) continue;

            var basis = leaf.F分摊基数;
            long volScope = scopeVolume.ByKind(basis);
            long volAll = fullScopeVolume.ByKind(basis);
            if (volAll == 0)
            {
                warnings.Add($"公共费[{leaf.FItemName}]全口径{basis ?? "send"}件量为0，本期分摊记0");
                result[leaf.FID] = 0m;
                continue;
            }
            result[leaf.FID] = Math.Round(total * volScope / volAll, 2);
        }
        return result;
    }

    public AllocationReconcileResult Reconcile(long leafId, decimal fullTotal, IEnumerable<decimal> perScopeAllocated)
    {
        var sum = perScopeAllocated.Sum();
        return new AllocationReconcileResult
        {
            LeafId = leafId,
            FullTotal = fullTotal,
            ScopeSum = sum,
            Diff = fullTotal - sum,
            IsBalanced = Math.Abs(fullTotal - sum) <= ReconcileTolerance,
        };
    }
}
