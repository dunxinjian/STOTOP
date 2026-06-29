using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class ApprovalModeHandler : IApprovalModeHandler
{
    /// <summary>
    /// 判断节点是否已完成（可通过）
    /// single: 任一approved → 完成
    /// countersign: 全部approved → 完成
    /// orsign: 任一approved → 完成
    /// </summary>
    public bool IsStageCompleted(string approvalMode, List<AssigneeStatus> assignees)
    {
        if (assignees.Count == 0) return false;

        return approvalMode.ToLowerInvariant() switch
        {
            "single" => assignees.Any(a => a.Status == "approved"),
            "countersign" => assignees.All(a => a.Status == "approved"),
            "orsign" => assignees.Any(a => a.Status == "approved"),
            "sequential" => IsSequentialStageCompleted(assignees),
            _ => assignees.Any(a => a.Status == "approved")
        };
    }

    /// <summary>
    /// 判断节点是否需要退回
    /// single: 任一rejected → 退回
    /// countersign: 任一rejected → 退回
    /// orsign: 全部rejected → 退回
    /// </summary>
    public bool IsStageReturned(string approvalMode, List<AssigneeStatus> assignees)
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
            _ => assignees.Any(a => a.Status == "rejected")
        };
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
