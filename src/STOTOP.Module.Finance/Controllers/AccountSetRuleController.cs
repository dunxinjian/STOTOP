using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/account-set-rules")]
public class AccountSetRuleController : ControllerBase
{
    private readonly IAccountSetRuleService _ruleService;

    public AccountSetRuleController(IAccountSetRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    /// <summary>头优先解析账套（照 VoucherController 口径：X-AccountSet-Id 头 > query）</summary>
    private long ResolveAccountSetId(long accountSetId)
    {
        var header = Request.Headers["X-AccountSet-Id"].FirstOrDefault();
        if (long.TryParse(header, out var id) && id > 0) return id;
        return accountSetId;
    }

    [HttpGet]
    [RequirePermission(FinancePermissions.AccountSetRuleView)]
    public async Task<ApiResult<AccountSetRuleDto>> Get([FromQuery] long accountSetId = 0)
    {
        var id = ResolveAccountSetId(accountSetId);
        if (id <= 0) return ApiResult<AccountSetRuleDto>.Fail("请先选择账套");
        var dto = await _ruleService.GetDtoAsync(id);
        return ApiResult<AccountSetRuleDto>.Success(dto);
    }

    [HttpPut]
    [RequirePermission(FinancePermissions.AccountSetRuleEdit)]
    public async Task<ApiResult<AccountSetRuleDto>> Update(
        [FromBody] UpdateAccountSetRuleRequest request, [FromQuery] long accountSetId = 0)
    {
        var id = ResolveAccountSetId(accountSetId);
        if (id <= 0) return ApiResult<AccountSetRuleDto>.Fail("请先选择账套");
        try
        {
            var operatorName = User.Identity?.Name ?? "未知";
            var dto = await _ruleService.UpsertAsync(id, request, operatorName);
            return ApiResult<AccountSetRuleDto>.Success(dto, "账套规则已保存");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountSetRuleDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 当前账套启用的凭证字（凭证录入页下拉用）。
    /// 仅 [Authorize] 不挂规则查看权限——凭证录入员无 account-set-rule:view 也需读到启用集合。
    /// </summary>
    [HttpGet("enabled-voucher-words")]
    public async Task<ApiResult<string[]>> GetEnabledVoucherWords([FromQuery] long accountSetId = 0)
    {
        var id = ResolveAccountSetId(accountSetId);
        if (id <= 0) return ApiResult<string[]>.Fail("请先选择账套");
        var words = await _ruleService.GetEnabledVoucherWordsAsync(id);
        return ApiResult<string[]>.Success(words);
    }
}
