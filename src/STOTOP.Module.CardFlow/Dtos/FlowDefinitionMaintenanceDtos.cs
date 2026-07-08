namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>流程定义在途卡片计数（按版本分组）</summary>
public class InflightSummaryDto
{
    public int Total { get; set; }
    public List<InflightVersionSummaryDto> ByVersion { get; set; } = new();
}

public class InflightVersionSummaryDto
{
    public long VersionId { get; set; }
    public int VersionNumber { get; set; }
    public int Count { get; set; }
    public List<string> StuckStageKeys { get; set; } = new();
}

/// <summary>发布请求体（可空——无 body 时按 keepOld 现状行为）</summary>
public class PublishFlowDefinitionRequest
{
    /// <summary>在途卡片策略：keepOld（默认，旧卡走旧版）/ migrate（被删节点卡片迁到新版后继节点）</summary>
    public string? InflightPolicy { get; set; }
}

/// <summary>发布结果（含在途迁移统计；keepOld 时各计数为 0）</summary>
public class PublishFlowDefinitionResultDto
{
    public string InflightPolicy { get; set; } = "keepOld";
    public int MigratedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>复制/克隆时扫描出的悬空引用</summary>
public class UnresolvedRefDto
{
    /// <summary>user / role / plugin / pluginRule</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>人名/编码/ID 等展示标识</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>引用位置描述，如「节点「财务复核」处理人」</summary>
    public string Path { get; set; } = string.Empty;
}

// ═══════════ M7-1 编辑锁（设计 E7）═══════════

/// <summary>编辑锁状态 DTO（acquire/heartbeat/takeover-* 统一返回）</summary>
public class LockStateDto
{
    /// <summary>是否有人持锁</summary>
    public bool Held { get; set; }
    /// <summary>持锁用户 ID（Held=true 时有值）</summary>
    public long? HolderId { get; set; }
    /// <summary>持锁用户姓名</summary>
    public string? HolderName { get; set; }
    /// <summary>当前调用者是否即为 holder</summary>
    public bool IsSelf { get; set; }
    /// <summary>获取锁的时间</summary>
    public DateTime? AcquiredAt { get; set; }
    /// <summary>最后心跳时间</summary>
    public DateTime? HeartbeatAt { get; set; }
    /// <summary>待响应的接管请求（null = 无）</summary>
    public LockTakeoverDto? Takeover { get; set; }
}

/// <summary>接管请求段（内嵌在 LockStateDto）</summary>
public class LockTakeoverDto
{
    public long RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}

/// <summary>接管响应请求体</summary>
public class TakeoverRespondRequest
{
    public bool Accept { get; set; }
}
