namespace STOTOP.Module.System.Dtos;

// 钉钉部门（从钉钉拉取后的展示）
public class DingTalkDepartmentDto
{
    public long DeptId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public bool IsBound { get; set; }
    public long? LocalOrgId { get; set; }
}

// 钉钉用户（从钉钉拉取后的展示）
public class DingTalkUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Title { get; set; }
    public string? JobNumber { get; set; }
    public long[]? DeptIdList { get; set; }
    public bool IsBound { get; set; }
    public long? LocalUserId { get; set; }
}

// 钉钉职位
public class DingTalkPositionDto
{
    public string PositionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBound { get; set; }
    public long? LocalPositionId { get; set; }
}

// 绑定请求
public class BindOrganizationRequest
{
    public long OrgId { get; set; }
    public string DingTalkDeptId { get; set; } = string.Empty;
}

public class BindUserRequest
{
    public long UserId { get; set; }
    public string DingTalkUserId { get; set; } = string.Empty;
}

public class BindPositionRequest
{
    public long PositionId { get; set; }
    public string DingTalkPositionId { get; set; } = string.Empty;
}

// 同步结果
public class SyncResultDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int SkipCount { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>指定用户同步请求</summary>
public class SyncSpecificUsersRequest
{
    public List<string> DingTalkUserIds { get; set; } = new();
}

// 保存钉钉配置请求
public class SaveDingTalkConfigRequest
{
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
    public string? CorpId { get; set; }
    public string? AgentId { get; set; }
}

// 钉钉同步进度 DTO
public class DingTalkSyncProgressDto
{
    public string Stage { get; set; } = string.Empty;   // "departments" / "users" / "positions" / "completed" / "error"
    public string Message { get; set; } = string.Empty;  // "正在同步部门 (15/50)..."
    public int Current { get; set; }                      // 当前已处理数
    public int Total { get; set; }                        // 总数（可为0表示未知）
    public int Percent { get; set; }                      // 0-100
    public SyncResultDto? Result { get; set; }            // 仅 completed 阶段携带最终结果
}

// 更新定时同步配置请求
public class UpdateAutoSyncRequest
{
    public bool Enabled { get; set; }
    public string? CronExpression { get; set; }
}
