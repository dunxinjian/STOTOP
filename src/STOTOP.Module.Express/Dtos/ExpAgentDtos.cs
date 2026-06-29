using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 业务代理详情
/// </summary>
public class AgentDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int AgentLevel { get; set; }
    public string? AgentRegion { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>业务对象ID（来自EXP业务对象表）</summary>
    public long? ServiceObjectId { get; set; }
}

/// <summary>
/// 创建业务代理请求
/// </summary>
public class CreateAgentRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int AgentLevel { get; set; } = 1;
    public string? AgentRegion { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新业务代理请求
/// </summary>
public class UpdateAgentRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int AgentLevel { get; set; }
    public string? AgentRegion { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public DateOnly? CooperationStartDate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 业务代理查询请求
/// </summary>
public class AgentQueryRequest : PagedRequest
{
    public int? Status { get; set; }
    public int? AgentLevel { get; set; }
}
