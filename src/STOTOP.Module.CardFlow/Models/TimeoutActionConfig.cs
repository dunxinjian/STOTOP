using System.Text.Json;

namespace STOTOP.Module.CardFlow.Models;

/// <summary>
/// 节点超时升级链配置（CfStageDefinition.FTimeoutActionJson）。
/// schema: { "levels": [ { "multiplier": 1, "action": "remind" }, { "multiplier": 2, "action": "autoApprove" } ] }
/// multiplier = 节点从激活起、相对 FTimeoutHours 的超时时长倍数（elapsedHours/timeoutHours）；
/// action = remind(既有提醒) / autoApprove / autoReject / escalate(升级至上级)。
/// </summary>
public sealed class TimeoutActionConfig
{
    public List<TimeoutActionLevel> Levels { get; set; } = new();

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>解析 FTimeoutActionJson；null/空/非法JSON/无有效级 → null（=向后兼容，仅走既有 flag/提醒行为）。</summary>
    public static TimeoutActionConfig? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var cfg = JsonSerializer.Deserialize<TimeoutActionConfig>(json, Opts);
            if (cfg?.Levels == null) return null;
            cfg.Levels = cfg.Levels
                .Where(l => l.Multiplier > 0 && !string.IsNullOrWhiteSpace(l.Action))
                .ToList();
            return cfg.Levels.Count > 0 ? cfg : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 按 elapsedHours/timeoutHours 的倍数比例，取"不超过当前倍数"的最高级配置；无匹配级返回 null。
    /// 例：levels=[1:remind,2:autoApprove,3:escalate]，ratio=2.5 → 命中 multiplier=2 的 autoApprove（尚未到 3x）。
    /// </summary>
    public TimeoutActionLevel? GetApplicableLevel(double elapsedHours, double timeoutHours)
    {
        if (timeoutHours <= 0) return null;
        var ratio = elapsedHours / timeoutHours;
        return Levels
            .Where(l => ratio >= l.Multiplier)
            .OrderByDescending(l => l.Multiplier)
            .FirstOrDefault();
    }
}

public sealed class TimeoutActionLevel
{
    /// <summary>超时时长倍数（如 1/2/3）</summary>
    public int Multiplier { get; set; }
    /// <summary>remind | autoApprove | autoReject | escalate</summary>
    public string Action { get; set; } = "remind";
}
