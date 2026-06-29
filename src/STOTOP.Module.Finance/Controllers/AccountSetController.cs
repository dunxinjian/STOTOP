using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/account-sets")]
public class AccountSetController : ControllerBase
{
    private readonly AccountSetService _accountSetService;
    private readonly IRepository<FinAccount> _accountRepository;

    public AccountSetController(AccountSetService accountSetService, IRepository<FinAccount> accountRepository)
    {
        _accountSetService = accountSetService;
        _accountRepository = accountRepository;
    }

    [HttpGet]
    public async Task<ApiResult<List<AccountSetDto>>> GetAll([FromQuery] long? orgId = null)
    {
        var result = await _accountSetService.GetAllAsync(orgId);
        return ApiResult<List<AccountSetDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<AccountSetDto>> GetById(long id)
    {
        var result = await _accountSetService.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<AccountSetDto>.Fail("账套不存在");
        }
        return ApiResult<AccountSetDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<AccountSetDto>> Create([FromBody] CreateAccountSetRequest request)
    {
        try
        {
            var result = await _accountSetService.CreateAsync(request);
    
            // 如果指定了模板类型，自动初始化（模板ID的应用已在Service中处理）
            if (!string.IsNullOrEmpty(request.FTemplateType))
            {
                var (success, message, accountCount, periodCount) = await _accountSetService.InitializeAccountSetAsync(
                    result.Id, false,
                    request.FTemplateType, request.FIndustryCode, request.FSourceAccountSetId);
    
                if (!success)
                {
                    // 创建成功但初始化失败，返回警告信息
                    return ApiResult<AccountSetDto>.Success(result, $"账套创建成功，但初始化失败: {message}");
                }
    
                return ApiResult<AccountSetDto>.Success(result, $"账套创建并初始化成功：{message}");
            }
    
            return ApiResult<AccountSetDto>.Success(result, "创建账套成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountSetDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<AccountSetDto>> Update(long id, [FromBody] AccountSetDto dto)
    {
        try
        {
            var result = await _accountSetService.UpdateAsync(id, dto);
            if (result == null)
            {
                return ApiResult<AccountSetDto>.Fail("账套不存在");
            }
            return ApiResult<AccountSetDto>.Success(result, "更新账套成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountSetDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _accountSetService.DeleteAsync(id);
            if (!result)
            {
                return ApiResult.Fail("账套不存在");
            }
            return ApiResult.Ok("删除账套成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/initialize")]
    public async Task<IActionResult> Initialize(long id, [FromQuery] bool force = false,
        [FromQuery] string? templateType = null,
        [FromQuery] string? industryCode = null,
        [FromQuery] long? sourceAccountSetId = null)
    {
        try
        {
            var (success, message, accountCount, periodCount) = await _accountSetService.InitializeAccountSetAsync(
                id, force, templateType, industryCode, sourceAccountSetId);
            if (!success)
            {
                return Ok(ApiResult.Fail(message));
            }
            return Ok(ApiResult<object>.Success(new { accountCount, periodCount }, message));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取可用的模板列表
    /// </summary>
    [HttpGet("templates")]
    public async Task<ApiResult<List<object>>> GetTemplates()
    {
        var templates = new List<object>
        {
            new { code = "empty", name = "空账套", description = "只创建基础结构，不预置科目", accountCount = 0 },
            new { code = "standard", name = "小企业会计准则", description = "适用于小企业的标准科目体系", accountCount = await _accountSetService.GetTemplateAccountCountAsync(0) },
            new { code = "express-delivery", name = "快递行业标准", description = "快递物流行业专用科目体系", accountCount = await _accountSetService.GetTemplateAccountCountAsync(2) }
        };
        return ApiResult<List<object>>.Success(templates);
    }

    /// <summary>
    /// 预览模板科目
    /// </summary>
    [HttpGet("templates/{code}/accounts")]
    public async Task<ApiResult<List<AccountSetTemplatePreviewDto>>> GetTemplateAccounts(string code)
    {
        try
        {
            long sourceId = code switch
            {
                "standard" => 0,
                "express-delivery" => 2,
                _ => throw new ArgumentException("无效的模板编码")
            };

            var accounts = await _accountSetService.GetTemplateAccountsAsync(sourceId);
            var dtos = accounts.Select(a => new AccountSetTemplatePreviewDto
            {
                FCode = a.FCode,
                FName = a.FName,
                FCategory = a.FCategory,
                FBalanceDirection = a.FBalanceDirection,
                FLevel = a.FLevel,
                FIsLeaf = a.FIsLeaf
            }).ToList();

            return ApiResult<List<AccountSetTemplatePreviewDto>>.Success(dtos);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<List<AccountSetTemplatePreviewDto>>.Fail(ex.Message);
        }
    }
}
