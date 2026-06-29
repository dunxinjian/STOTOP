using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/banking-channels")]
public class BankingChannelController : ControllerBase
{
    private readonly IBankTransactionService _bankTransactionService;

    public BankingChannelController(IBankTransactionService bankTransactionService)
    {
        _bankTransactionService = bankTransactionService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<BankChannelDto>>> GetList([FromQuery] BankChannelQueryRequest request)
    {
        var result = await _bankTransactionService.GetChannelsAsync(request);
        return ApiResult<PagedResult<BankChannelDto>>.Success(result);
    }

    [HttpGet("all")]
    public async Task<ApiResult<List<BankChannelDto>>> GetAllEnabled()
    {
        var result = await _bankTransactionService.GetAllEnabledChannelsAsync();
        return ApiResult<List<BankChannelDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<BankChannelDto>> GetById(long id)
    {
        var result = await _bankTransactionService.GetChannelByIdAsync(id);
        if (result == null)
        {
            return ApiResult<BankChannelDto>.Fail("交易渠道不存在");
        }
        return ApiResult<BankChannelDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<BankChannelDto>> Create([FromBody] CreateBankChannelRequest request)
    {
        var operatorName = User.Identity?.Name;
        var result = await _bankTransactionService.CreateChannelAsync(request, operatorName);
        return ApiResult<BankChannelDto>.Success(result, "创建交易渠道成功");
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<BankChannelDto>> Update(long id, [FromBody] UpdateBankChannelRequest request)
    {
        try
        {
            var operatorName = User.Identity?.Name;
            var result = await _bankTransactionService.UpdateChannelAsync(id, request, operatorName);
            if (result == null)
            {
                return ApiResult<BankChannelDto>.Fail("交易渠道不存在");
            }
            return ApiResult<BankChannelDto>.Success(result, "更新交易渠道成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BankChannelDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _bankTransactionService.DeleteChannelAsync(id);
            if (!result)
            {
                return ApiResult.Fail("交易渠道不存在");
            }
            return ApiResult.Ok("删除交易渠道成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
