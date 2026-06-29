using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/periods")]
public class AccountPeriodController : ControllerBase
{
    private readonly IAccountPeriodService _periodService;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IRepository<FinAccount> _accountRepository;

    public AccountPeriodController(
        IAccountPeriodService periodService,
        IRepository<FinAccountPeriod> periodRepository,
        IRepository<FinAccount> accountRepository)
    {
        _periodService = periodService;
        _periodRepository = periodRepository;
        _accountRepository = accountRepository;
    }

    [HttpGet]
    public async Task<ApiResult<List<AccountPeriodDto>>> GetAll([FromQuery] long accountSetId = 0)
    {
        var result = await _periodService.GetAllAsync(accountSetId);
        return ApiResult<List<AccountPeriodDto>>.Success(result);
    }

    [HttpGet("current")]
    public async Task<ApiResult<AccountPeriodDto>> GetCurrent([FromQuery] long accountSetId = 0)
    {
        var result = await _periodService.GetCurrentAsync(accountSetId);
        if (result == null)
        {
            return ApiResult<AccountPeriodDto>.Fail("未找到当前期间");
        }
        return ApiResult<AccountPeriodDto>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<AccountPeriodDto>> GetById(long id)
    {
        var result = await _periodService.GetByIdAsync(id);
        if (result == null)
        {
            return ApiResult<AccountPeriodDto>.Fail("期间不存在");
        }
        return ApiResult<AccountPeriodDto>.Success(result);
    }

    [HttpGet("year/{year}")]
    public async Task<ApiResult<List<AccountPeriodDto>>> GetByYear(int year, [FromQuery] long accountSetId = 0)
    {
        var result = await _periodService.GetByYearAsync(year, accountSetId);
        return ApiResult<List<AccountPeriodDto>>.Success(result);
    }

    [HttpPost("create/{year}")]
    public async Task<ApiResult<List<AccountPeriodDto>>> CreateYearPeriods(int year, [FromQuery] long accountSetId = 0)
    {
        try
        {
            var result = await _periodService.CreateYearPeriodsAsync(year, accountSetId);
            return ApiResult<List<AccountPeriodDto>>.Success(result, $"成功创建{year}年的12个会计期间");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<AccountPeriodDto>>.Fail(ex.Message);
        }
    }

    [HttpGet("closing-info")]
    public async Task<ApiResult<object>> GetClosingInfo([FromQuery] long accountSetId = 0)
    {
        // 查找最早未结账期间（当前需要结账的期间）
        var currentPeriod = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId && p.FIsClosed == 0)
            .OrderBy(p => p.FYear).ThenBy(p => p.FPeriodNo)
            .FirstOrDefaultAsync();

        // 查找最近已结账期间（用于反结账）
        var lastClosedPeriod = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId && p.FIsClosed == 1)
            .OrderByDescending(p => p.FYear).ThenByDescending(p => p.FPeriodNo)
            .FirstOrDefaultAsync();

        // 查找关键科目
        var profitAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == "3103" && a.FAccountSetId == accountSetId);
        var retainedAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == "310405" && a.FAccountSetId == accountSetId);

        // 已结账期间列表
        var closedPeriods = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId && p.FIsClosed == 1)
            .OrderBy(p => p.FYear).ThenBy(p => p.FPeriodNo)
            .Select(p => new { id = p.FID, year = p.FYear, month = p.FPeriodNo, isClosed = true })
            .ToListAsync();

        bool canClose = currentPeriod != null;
        bool canReopen = lastClosedPeriod != null;

        string message = profitAccount == null
            ? "警告：未找到3103科目"
            : (retainedAccount == null ? "警告：未找到310405科目" : "");

        return ApiResult<object>.Success(new
        {
            currentPeriod = currentPeriod != null
                ? new { id = currentPeriod.FID, year = currentPeriod.FYear, month = currentPeriod.FPeriodNo, isClosed = false }
                : (object?)null,
            lastClosedPeriod = lastClosedPeriod != null
                ? new { id = lastClosedPeriod.FID, year = lastClosedPeriod.FYear, month = lastClosedPeriod.FPeriodNo, isClosed = true }
                : (object?)null,
            profitAccount = profitAccount != null
                ? new { code = profitAccount.FCode, name = profitAccount.FName }
                : (object?)null,
            retainedEarningsAccount = retainedAccount != null
                ? new { code = retainedAccount.FCode, name = retainedAccount.FName }
                : (object?)null,
            closedPeriods,
            canClose,
            canReopen,
            message
        });
    }

    [HttpGet("pre-close-check")]
    public async Task<ApiResult<object>> PreCloseCheck([FromQuery] long accountSetId = 0, [FromQuery] int year = 0, [FromQuery] int periodNo = 0)
    {
        if (year == 0 || periodNo == 0)
            return ApiResult<object>.Fail("请指定年份和期间号");

        var result = await _periodService.PreCloseCheckAsync(accountSetId, year, periodNo);
        return ApiResult<object>.Success(result);
    }

    [HttpPost("{id}/close")]
    [RequirePermission(FinancePermissions.PeriodClose)]
    public async Task<ApiResult> Close(long id, [FromQuery] long accountSetId = 0)
    {
        var (success, message) = await _periodService.CloseAsync(id, accountSetId);
        return success ? ApiResult.Ok(message) : ApiResult.Fail(message);
    }

    [HttpPost("{id}/reopen")]
    [RequirePermission(FinancePermissions.PeriodReopen)]
    public async Task<ApiResult> Reopen(long id, [FromQuery] long accountSetId = 0)
    {
        var (success, message) = await _periodService.ReopenAsync(id, accountSetId);
        return success ? ApiResult.Ok(message) : ApiResult.Fail(message);
    }
}
