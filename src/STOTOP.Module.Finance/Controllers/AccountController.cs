using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Filters;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/accounts")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("tree")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectView, AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<List<AccountTreeDto>>> GetTree([FromQuery] string? category, [FromQuery] long accountSetId = 0)
    {
        var result = await _accountService.GetTreeAsync(category, accountSetId);
        return ApiResult<List<AccountTreeDto>>.Success(result);
    }

    /// <summary>
    /// 平铺科目列表（用于选择器）
    /// </summary>
    [HttpGet]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectView, AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<List<AccountSelectorDto>>> GetSelectorList(
        [FromQuery] long accountSetId = 0,
        [FromQuery] string? keyword = null,
        [FromQuery] bool? onlyLeaf = null)
    {
        var tree = await _accountService.GetTreeAsync(null, accountSetId);
        var flat = new List<AccountSelectorDto>();
        FlattenAccountTree(tree, flat);

        IEnumerable<AccountSelectorDto> query = flat;
        if (onlyLeaf == true)
        {
            query = query.Where(a => a.IsLeaf);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a => a.Code.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return ApiResult<List<AccountSelectorDto>>.Success(query.ToList());
    }

    private static void FlattenAccountTree(List<AccountTreeDto> nodes, List<AccountSelectorDto> output)
    {
        foreach (var node in nodes)
        {
            output.Add(new AccountSelectorDto
            {
                Id = node.Id,
                Code = node.Code,
                Name = node.Name,
                ParentId = node.ParentId,
                IsLeaf = node.IsLeaf
            });
            if (node.Children != null && node.Children.Count > 0)
            {
                FlattenAccountTree(node.Children, output);
            }
        }
    }

    [HttpGet("by-aux-type")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectView, AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<List<AccountDto>>> GetByAuxType(
        [FromQuery] string auxType,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _accountService.GetByAuxTypeAsync(auxType, accountSetId);
        return ApiResult<List<AccountDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectView, AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<AccountDto>> GetById(long id, [FromHeader(Name = "X-AccountSet-Id")] long accountSetId = 0)
    {
        var result = await _accountService.GetByIdAsync(id, accountSetId);
        if (result == null)
        {
            return ApiResult<AccountDto>.Fail("科目不存在");
        }
        return ApiResult<AccountDto>.Success(result);
    }

    [HttpPost]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<AccountDto>> Create([FromBody] CreateAccountRequest request, [FromQuery] long accountSetId = 0)
    {
        try
        {
            var result = await _accountService.CreateAsync(request, accountSetId);
            return ApiResult<AccountDto>.Success(result, "创建科目成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<AccountDto>> Update(long id, [FromBody] UpdateAccountRequest request)
    {
        var result = await _accountService.UpdateAsync(id, request);
        if (result == null)
        {
            return ApiResult<AccountDto>.Fail("科目不存在");
        }
        return ApiResult<AccountDto>.Success(result, "更新科目成功");
    }

    [HttpDelete("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _accountService.DeleteAsync(id);
            if (!result)
            {
                return ApiResult.Fail("科目不存在");
            }
            return ApiResult.Ok("删除科目成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/toggle-status")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult> ToggleStatus(long id)
    {
        try
        {
            var result = await _accountService.ToggleStatusAsync(id);
            if (!result)
            {
                return ApiResult.Fail("科目不存在");
            }
            return ApiResult.Ok("状态切换成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("initial-balances")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectView, AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult<List<InitialBalanceDto>>> GetInitialBalances([FromQuery] long accountSetId = 0)
    {
        var result = await _accountService.GetInitialBalancesAsync(accountSetId);
        return ApiResult<List<InitialBalanceDto>>.Success(result);
    }

    [HttpPost("initial-balances")]
    [RequireAccountSetPermission(AccountSetPermissions.SubjectEdit)]
    public async Task<ApiResult> SaveInitialBalances([FromBody] SaveInitialBalancesRequest request)
    {
        try
        {
            await _accountService.SaveInitialBalancesAsync(request);
            return ApiResult.Ok("保存期初余额成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 批量更新科目的辅助核算关联（编辑关联科目）
    /// 逻辑：将指定辅助类别添加到勾选科目，同时从未勾选科目移除该类别
    /// </summary>
    [HttpPost("update-auxiliary")]
    [RequireAccountSetPermission(AccountSetPermissions.AuxiliaryEdit)]
    public async Task<ApiResult> UpdateAuxiliary([FromBody] UpdateAccountAuxiliaryRequest request)
    {
        await _accountService.UpdateAccountAuxiliaryAsync(request);
        return ApiResult.Ok("关联科目保存成功");
    }
}
