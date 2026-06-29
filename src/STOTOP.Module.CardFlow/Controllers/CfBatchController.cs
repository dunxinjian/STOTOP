// TODO: Task 3 步骤 4.1 - CfBatchController（增强 ImportController 迁移）
// 本文件创建 CfBatchController 占位类，使用新路由 api/cardflow/batch。
// 完整 Action 方法逻辑（约 1455 行）暂保留在 DataCenter.Controllers.ImportController。
// Task 7 清理阶段需将 ImportController 的所有 Action 迁移到此处，包括：
// - POST upload         手动上传文件并创建导入批次
// - GET  batches        分页查询批次列表
// - GET  batches/{id}   查看批次详情
// - GET  batches/{id}/errors   查看批次错误明细
// - POST batches/{id}/revoke   撤销批次
// - POST batches/{id}/rerun    重跑批次
// - POST batches/{id}/snapshot 创建批次快照
// - 等约 30 个 Action 方法
//
// 同时需要：
// - 替换 DC 实体引用为 CF 实体（DcImportBatch → CfBatch；DcImportError → CfBatchError；等）
// - 替换 DataCenterDbContext 为 STOTOPDbContext（注：DC 已经使用 STOTOPDbContext，N5 已满足）
// - 保持所有 Action 方法的功能逻辑不变

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// 卡片流程批次控制器（迁移自 DataCenter.ImportController，Task 3.1）。
/// TODO: Task 7 - 完整迁移 ImportController 的 Action 方法。
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/batch")]
public class CfBatchController : ControllerBase
{
    /// <summary>占位健康检查端点。完整 Action 待 Task 7 迁移。</summary>
    [HttpGet("ping")]
    [AllowAnonymous]
    public ApiResult<string> Ping()
    {
        return ApiResult<string>.Success("CfBatchController online (Task 3 占位实现)");
    }
}
