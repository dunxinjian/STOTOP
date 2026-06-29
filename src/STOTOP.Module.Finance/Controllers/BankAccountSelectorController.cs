using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Controllers;

/// <summary>
/// 银行账户选择器（CardFlow 等场景使用）
/// 数据源：FinPaymentChannel（公司自有交易渠道）
/// </summary>
[Authorize]
[ApiController]
[Route("api/finance/bank-accounts")]
public class BankAccountSelectorController : ControllerBase
{
    private readonly IRepository<FinPaymentChannel> _channelRepository;

    public BankAccountSelectorController(IRepository<FinPaymentChannel> channelRepository)
    {
        _channelRepository = channelRepository;
    }

    /// <summary>
    /// 按组织查询可用银行账户列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<List<BankAccountSelectorDto>>> GetList(
        [FromQuery] long orgId = 0,
        [FromQuery] string? keyword = null)
    {
        var query = _channelRepository.Query()
            .Where(c => c.FStatus == 1);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c =>
                c.FName.Contains(kw)
                || (c.FAccountNo != null && c.FAccountNo.Contains(kw))
                || (c.FBankName != null && c.FBankName.Contains(kw)));
        }

        var list = await query
            .OrderBy(c => c.FName)
            .Select(c => new BankAccountSelectorDto
            {
                Id = c.FID,
                AccountNo = c.FAccountNo ?? string.Empty,
                AccountName = c.FName,
                BankName = c.FBankName,
                AccountId = null,
                AccountCode = null
            })
            .ToListAsync();

        // 注：FinPaymentChannel 当前未关联组织，orgId 参数保留作前端契约一致；
        // 若后续实体新增 FOrgId 字段，可在此处追加过滤条件。
        _ = orgId;

        return ApiResult<List<BankAccountSelectorDto>>.Success(list);
    }
}
