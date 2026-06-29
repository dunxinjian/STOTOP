using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Finance.Filters;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.Interfaces;
using global::System.Security.Claims;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/vouchers")]
public class VoucherController : ControllerBase
{
    private readonly IVoucherService _voucherService;
    private readonly VoucherExcelService _voucherExcelService;

    public VoucherController(IVoucherService voucherService, VoucherExcelService voucherExcelService)
    {
        _voucherService = voucherService;
        _voucherExcelService = voucherExcelService;
    }

    /// <summary>
    /// 解析当前账套：请求头 X-AccountSet-Id 优先，其次回退 query 参数。
    /// 与权限过滤器(RequireAccountSetPermission 头优先)、服务层读回(GetCurrentAccountSetId 仅读头)保持同一口径，
    /// 避免「落库账套用 query、鉴权/读回用头」三者背离 → 已提交却读回失败(500) 或落到非预期账套。
    /// </summary>
    private long ResolveAccountSetId(long accountSetId)
    {
        var header = Request.Headers["X-AccountSet-Id"].FirstOrDefault();
        if (long.TryParse(header, out var id) && id > 0) return id;
        return accountSetId;
    }

    [HttpGet]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<VoucherPagedResult>> GetPagedList([FromQuery] VoucherQueryRequest request, [FromQuery] long accountSetId = 0)
    {
        var result = await _voucherService.GetPagedListAsync(request, accountSetId);
        return ApiResult<VoucherPagedResult>.Success(result);
    }

    [HttpGet("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<VoucherDto>> GetById(long id)
    {
        var result = await _voucherService.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VoucherDto>.Fail("凭证不存在");
        }
        return ApiResult<VoucherDto>.Success(result);
    }

    [HttpPost]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult<VoucherDto>> Create([FromBody] CreateVoucherRequest request, [FromQuery] long accountSetId = 0)
    {
        try
        {
            var creator = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var effectiveAccountSetId = ResolveAccountSetId(accountSetId);
            // 手动录入凭证：强校验科目辅助核算契约(声明的维度必须带齐, E2)
            var result = await _voucherService.CreateAsync(request, creator, effectiveAccountSetId, enforceAuxContract: true);
            return ApiResult<VoucherDto>.Success(result, "创建凭证成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VoucherDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherEdit)]
    public async Task<ApiResult<VoucherDto>> Update(long id, [FromBody] CreateVoucherRequest request)
    {
        try
        {
            var modifier = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            // 手动编辑凭证：强校验科目辅助核算契约(E2)
            var result = await _voucherService.UpdateAsync(id, request, modifier, enforceAuxContract: true);
            if (result == null)
            {
                return ApiResult<VoucherDto>.Fail("凭证不存在");
            }
            return ApiResult<VoucherDto>.Success(result, "更新凭证成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VoucherDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherDelete)]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _voucherService.DeleteAsync(id);
            if (!result)
            {
                return ApiResult.Fail("凭证不存在");
            }
            return ApiResult.Ok("删除凭证成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/audit")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherAudit)]
    public async Task<ApiResult> Audit(long id)
    {
        var auditor = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        var result = await _voucherService.AuditAsync(id, auditor);
        if (!result)
        {
            return ApiResult.Fail("凭证不存在");
        }
        return ApiResult.Ok("审核成功");
    }

    [HttpPost("{id}/unaudit")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherUnaudit)]
    public async Task<ApiResult> UnAudit(long id)
    {
        var result = await _voucherService.UnAuditAsync(id);
        if (!result)
        {
            return ApiResult.Fail("凭证不存在");
        }
        return ApiResult.Ok("反审核成功");
    }

    [HttpPost("draft")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult<VoucherDto>> SaveDraft([FromBody] CreateVoucherRequest request, [FromQuery] long accountSetId = 0)
    {
        var creator = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        var result = await _voucherService.SaveDraftAsync(request, creator, ResolveAccountSetId(accountSetId));
        return ApiResult<VoucherDto>.Success(result, "保存草稿成功");
    }

    [HttpGet("drafts")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<List<VoucherListDto>>> GetDrafts([FromQuery] long accountSetId = 0)
    {
        var result = await _voucherService.GetDraftsAsync(accountSetId);
        return ApiResult<List<VoucherListDto>>.Success(result);
    }

    [HttpPost("reorder/{periodId}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherEdit)]
    public async Task<ApiResult> ReorderNumbers(long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _voucherService.ReorderNumbersAsync(periodId, accountSetId);
        return ApiResult.Ok("整理凭证号成功");
    }

    [HttpGet("next-number")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<int>> GetNextNumber([FromQuery] string word, [FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _voucherService.GetNextNumberAsync(word, periodId, accountSetId);
        return ApiResult<int>.Success(result);
    }

    [HttpGet("pending-count")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<int>> GetPendingAuditCount([FromQuery] long accountSetId = 0)
    {
        var result = await _voucherService.GetPendingAuditCountAsync(accountSetId);
        return ApiResult<int>.Success(result);
    }

    [HttpPost("copy/{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult<object>> Copy(long id)
    {
        return await _voucherService.CopyAsync(id);
    }

    [HttpPost("reverse/{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult<object>> Reverse(long id)
    {
        return await _voucherService.ReverseAsync(id);
    }

    [HttpPost("batch-audit")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherAudit)]
    public async Task<ApiResult<object>> BatchAudit([FromBody] BatchAuditRequest request)
    {
        var auditorName = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        var auditorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
        long.TryParse(auditorIdStr, out var auditorId);
        return await _voucherService.BatchAuditAsync(request.Ids, auditorId, auditorName);
    }

    [HttpGet("check-gap")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<ApiResult<object>> CheckGap([FromQuery] long accountSetId, [FromQuery] int year, [FromQuery] int periodNo)
    {
        return await _voucherService.CheckGapAsync(accountSetId, year, periodNo);
    }

    [HttpGet("export")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public async Task<IActionResult> Export([FromQuery] string ids, [FromQuery] long accountSetId)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest(ApiResult.Fail("请指定要导出的凭证"));

        var voucherIds = ids.Split(',').Select(long.Parse).ToList();
        var bytes = await _voucherExcelService.ExportToExcel(voucherIds, accountSetId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "凭证导出.xlsx");
    }

    [HttpPost("import")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult<VoucherImportResult>> Import(IFormFile? file, [FromForm] long accountSetId)
    {
        if (file == null || file.Length == 0)
            return ApiResult<VoucherImportResult>.Fail("请上传文件");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return ApiResult<VoucherImportResult>.Fail("仅支持 .xlsx 或 .xls 格式");

        if (file.Length > 10 * 1024 * 1024)
            return ApiResult<VoucherImportResult>.Fail("文件过大，请拆分后再导入");

        var currentUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        using var stream = file.OpenReadStream();
        var result = await _voucherExcelService.ImportFromExcel(stream, file.FileName, accountSetId, currentUser);
        return ApiResult<VoucherImportResult>.Success(result);
    }

    [HttpGet("export-template")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherView)]
    public IActionResult ExportTemplate()
    {
        var bytes = _voucherExcelService.ExportTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "凭证导入模板.xlsx");
    }

    /// <summary>
    /// 完成凭证补录：将草稿凭证提交为待审核状态
    /// </summary>
    [HttpPost("{id}/complete-record")]
    [RequireAccountSetPermission(AccountSetPermissions.VoucherCreate)]
    public async Task<ApiResult> CompleteRecord(long id)
    {
        return await _voucherService.CompleteRecordAsync(id);
    }
}

public class BatchAuditRequest
{
    public List<long> Ids { get; set; } = new();
    public long AuditorId { get; set; }
    public string AuditorName { get; set; } = string.Empty;
}
