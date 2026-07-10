using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class ApprovalModeHandler : IApprovalModeHandler
{
    /// <summary>
    /// 判断节点是否已完成（可通过）
    /// single: 任一approved → 完成
    /// countersign: 全部approved → 完成
    /// orsign: 任一approved → 完成
    /// ratio: approved占比 >= threshold% → 完成（threshold 缺省/越界回退100%，即countersign语义）
    /// </summary>
    public bool IsStageCompleted(string approvalMode, List<AssigneeStatus> assignees, int? threshold = null)
    {
        if (assignees.Count == 0) return false;

        return approvalMode.ToLowerInvariant() switch
        {
            "single" => assignees.Any(a => a.Status == "approved"),
            "countersign" => assignees.All(a => a.Status == "approved"),
            "orsign" => assignees.Any(a => a.Status == "approved"),
            "sequential" => IsSequentialStageCompleted(assignees),
            "ratio" => IsRatioStageCompleted(assignees, threshold),
            _ => assignees.Any(a => a.Status == "approved")
        };
    }

    /// <summary>
    /// 判断节点是否需要退回
    /// single: 任一rejected → 退回
    /// countersign: 任一rejected → 退回
    /// orsign: 全部rejected → 退回
    /// ratio: rejected占比 > (100-threshold)% → 退回（补数驳回；threshold 缺省/越界回退100%，即countersign语义：任一rejected即退回）
    /// </summary>
    public bool IsStageReturned(string approvalMode, List<AssigneeStatus> assignees, int? threshold = null)
    {
        if (assignees.Count == 0) return false;

        return approvalMode.ToLowerInvariant() switch
        {
            "single" => assignees.Any(a => a.Status == "rejected"),
            "countersign" => assignees.Any(a => a.Status == "rejected"),
            "orsign" => assignees.All(a => a.Status == "rejected"),
            "sequential" => assignees
                .Where(a => !IsIgnoredSequentialStatus(a.Status))
                .Any(a => a.Status == "rejected"),
            "ratio" => IsRatioStageReturned(assignees, threshold),
            _ => assignees.Any(a => a.Status == "rejected")
        };
    }

    private static bool IsRatioStageCompleted(List<AssigneeStatus> assignees, int? threshold)
    {
        var effectiveThreshold = NormalizeThreshold(threshold);
        var approvedCount = assignees.Count(a => a.Status == "approved");
        return (double)approvedCount / assignees.Count >= effectiveThreshold / 100.0;
    }

    private static bool IsRatioStageReturned(List<AssigneeStatus> assignees, int? threshold)
    {
        var effectiveThreshold = NormalizeThreshold(threshold);
        var rejectedCount = assignees.Count(a => a.Status == "rejected");
        return (double)rejectedCount / assignees.Count > (100 - effectiveThreshold) / 100.0;
    }

    /// <summary>threshold 缺省或越界（须 1-99）时回退 100%，即等价 countersign 语义。</summary>
    private static int NormalizeThreshold(int? threshold)
    {
        return threshold is >= 1 and <= 99 ? threshold.Value : 100;
    }

    private static bool IsSequentialStageCompleted(List<AssigneeStatus> assignees)
    {
        var activeAssignees = assignees
            .Where(a => !IsIgnoredSequentialStatus(a.Status))
            .ToList();

        return activeAssignees.Any(a => a.Status == "approved")
            && activeAssignees.All(a => a.Status == "approved");
    }

    private static bool IsIgnoredSequentialStatus(string status)
    {
        return status is "cancelled" or "transferred";
    }
}
