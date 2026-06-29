using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/treasury-plans")]
public class TreasuryPlanController : ControllerBase
{
    private readonly ITreasuryPlanService _treasuryPlanService;

    public TreasuryPlanController(ITreasuryPlanService treasuryPlanService)
    {
        _treasuryPlanService = treasuryPlanService;
    }

    [HttpGet("account-bindings")]
    public async Task<ApiResult<List<TreasuryAccountBindingDto>>> GetAccountBindings(
        [FromQuery] long accountSetId,
        [FromQuery] long? orgId)
    {
        var result = await _treasuryPlanService.GetBindingsAsync(accountSetId, orgId);
        return ApiResult<List<TreasuryAccountBindingDto>>.Success(result);
    }

    [HttpPost("account-bindings")]
    public async Task<ApiResult<TreasuryAccountBindingDto>> SaveAccountBinding([FromBody] TreasuryAccountBindingDto request)
    {
        try
        {
            var result = await _treasuryPlanService.SaveBindingAsync(request);
            return ApiResult<TreasuryAccountBindingDto>.Success(result, "保存资金账户绑定成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<TreasuryAccountBindingDto>.Fail(ex.Message);
        }
    }

    [HttpGet("lines")]
    public async Task<ApiResult<List<TreasuryPlanLineDto>>> GetPlanLines(
        [FromQuery] long accountSetId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] long? orgId)
    {
        try
        {
            var start = (startDate ?? DateTime.Today).Date;
            var end = (endDate ?? start.AddDays(90)).Date;
            var result = await _treasuryPlanService.GetPlanLinesAsync(accountSetId, start, end, orgId);
            return ApiResult<List<TreasuryPlanLineDto>>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<TreasuryPlanLineDto>>.Fail(ex.Message);
        }
    }

    [HttpPost("lines")]
    public async Task<ApiResult<TreasuryPlanLineDto>> SavePlanLine([FromBody] TreasuryPlanLineDto request)
    {
        try
        {
            var result = await _treasuryPlanService.SavePlanLineAsync(request);
            return ApiResult<TreasuryPlanLineDto>.Success(result, "保存资金计划明细成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<TreasuryPlanLineDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("lines/{id}")]
    public async Task<ApiResult> DeletePlanLine(long id)
    {
        try
        {
            await _treasuryPlanService.DeletePlanLineAsync(id);
            return ApiResult.Ok("删除资金计划明细成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("rolling-13-weeks")]
    public async Task<ApiResult<Rolling13WeekTreasuryDto>> GetRolling13Weeks(
        [FromQuery] long accountSetId,
        [FromQuery] DateTime? startDate,
        [FromQuery] long? orgId,
        [FromQuery] decimal safetyCash = 0m)
    {
        try
        {
            var result = await _treasuryPlanService.GetRolling13WeeksAsync(
                accountSetId,
                (startDate ?? DateTime.Today).Date,
                orgId,
                safetyCash);
            return ApiResult<Rolling13WeekTreasuryDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<Rolling13WeekTreasuryDto>.Fail(ex.Message);
        }
    }
}
