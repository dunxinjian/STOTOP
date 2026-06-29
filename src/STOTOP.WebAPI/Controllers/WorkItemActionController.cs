using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;

namespace STOTOP.WebAPI.Controllers;

/// <summary>WorkItem 操作控制器：重跑、跳过、查看关联异常记录</summary>
/// <remarks>IPartialRerunService 已随 Pipeline 体系删除</remarks>
[ApiController]
[Route("api/workitem")]
[Authorize]
public class WorkItemActionController : ControllerBase
{
    /// <summary>修正后重跑：按 WorkItem 关联的 STG 记录从指定 Agent 阶段重跑</summary>
    [HttpPost("{id}/rerun")]
    public IActionResult Rerun(long id, [FromBody] RerunRequest? request)
    {
        return StatusCode(503, ApiResult.Fail("重跑功能正在适配新架构，暂不可用"));
    }

    /// <summary>按批次重跑所有异常记录</summary>
    [HttpPost("batch/{batchId}/rerun")]
    public IActionResult RerunBatch(long batchId)
    {
        return StatusCode(503, ApiResult.Fail("重跑功能正在适配新架构，暂不可用"));
    }

    /// <summary>标记跳过：关联 STG 记录设为跳过状态，关闭 WorkItem</summary>
    [HttpPost("{id}/skip")]
    public IActionResult Skip(long id, [FromBody] SkipRequest? request)
    {
        return StatusCode(503, ApiResult.Fail("跳过功能正在适配新架构，暂不可用"));
    }

    /// <summary>查看 WorkItem 关联的异常记录详情（分页）</summary>
    [HttpGet("{id}/affected-rows")]
    public IActionResult GetAffectedRows(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // 工作项行级详情查询属于高复杂层功能，待新架构适配后实现
        return Ok(ApiResult<object>.Success(new AffectedRowsResult(new List<object>(), 0)));
    }
}

#region Request DTOs

public record RerunRequest(string? StartAgentCode);
public record SkipRequest(string? Remark);
public record AffectedRowsResult(List<object> Rows, int Total);

#endregion
