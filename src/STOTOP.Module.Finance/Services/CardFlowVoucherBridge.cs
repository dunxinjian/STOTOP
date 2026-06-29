using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// CardFlow IVoucherService 桥接实现：将 CardFlow 模块的凭证创建/红冲请求委派到
/// Finance 模块现有的 VoucherService（IVoucherService - Finance 命名空间）。
/// 通过命名空间限定避免与 Finance.Services.Interfaces.IVoucherService 名称冲突。
/// </summary>
public class CardFlowVoucherBridge : STOTOP.Core.Interfaces.IVoucherService
{
    private readonly STOTOPDbContext _db;
    private readonly STOTOP.Module.Finance.Services.Interfaces.IVoucherService _financeVoucher;
    private readonly IRepository<FinAccount> _accountRepo;
    private readonly IRepository<FinAccountPeriod> _periodRepo;
    private readonly IRepository<FinAccountSet> _accountSetRepo;

    public CardFlowVoucherBridge(
        STOTOPDbContext db,
        STOTOP.Module.Finance.Services.Interfaces.IVoucherService financeVoucher,
        IRepository<FinAccount> accountRepo,
        IRepository<FinAccountPeriod> periodRepo,
        IRepository<FinAccountSet> accountSetRepo)
    {
        _db = db;
        _financeVoucher = financeVoucher;
        _accountRepo = accountRepo;
        _periodRepo = periodRepo;
        _accountSetRepo = accountSetRepo;
    }

    public async Task<long> CreateAsync(VoucherCreateDto voucher)
    {
        if (voucher.Entries == null || voucher.Entries.Count < 2)
            throw new InvalidOperationException("凭证至少需要 2 条分录");

        // 解析 PeriodId（FPeriodId=0 时按 FDate + FAccountSetId 自动定位）
        long periodId = voucher.FPeriodId;
        if (periodId == 0)
        {
            var period = await _periodRepo.Query()
                .Where(p => p.FAccountSetId == voucher.FAccountSetId
                    && p.FStartDate <= voucher.FDate
                    && p.FEndDate >= voucher.FDate)
                .OrderByDescending(p => p.FStartDate)
                .FirstOrDefaultAsync();
            if (period == null)
                throw new InvalidOperationException(
                    $"未找到账套 {voucher.FAccountSetId} 在 {voucher.FDate:yyyy-MM-dd} 对应的账期");
            periodId = period.FID;
        }

        // 转换分录：FAccountId=0 + FAccountCode 非空 → 按 编码+账套 解析科目
        var entries = new List<CreateVoucherEntryRequest>();
        foreach (var e in voucher.Entries)
        {
            long accountId = e.FAccountId;
            if (accountId == 0 && !string.IsNullOrWhiteSpace(e.FAccountCode))
            {
                var account = await _accountRepo.Query()
                    .FirstOrDefaultAsync(a => a.FAccountSetId == voucher.FAccountSetId
                        && a.FCode == e.FAccountCode);
                if (account == null)
                    throw new InvalidOperationException(
                        $"科目编码 {e.FAccountCode} 在账套 {voucher.FAccountSetId} 下不存在");
                accountId = account.FID;
            }
            if (accountId == 0)
                throw new InvalidOperationException($"分录第 {e.FLineNo} 行缺少 AccountId / AccountCode");

            entries.Add(new CreateVoucherEntryRequest
            {
                LineNo = e.FLineNo,
                Summary = e.FSummary,
                AccountId = accountId,
                AuxiliaryJson = e.FAuxiliaryJson,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount,
            });
        }

        var request = new CreateVoucherRequest
        {
            VoucherWord = voucher.FVoucherWord,
            Date = voucher.FDate,
            PeriodId = periodId,
            AttachmentCount = voucher.FAttachmentCount,
            Source = voucher.FSource,
            Remark = voucher.FRemark,
            DataScopeId = voucher.FDataScopeId,
            Entries = entries
        };

        var dto = await _financeVoucher.CreateAsync(request, voucher.FCreator, voucher.FAccountSetId);
        return dto.Id;
    }

    public async Task CreateReversalAsync(long voucherId)
    {
        var result = await _financeVoucher.ReverseAsync(voucherId);
        if (result.Code != 200)
            throw new InvalidOperationException(result.Message ?? "凭证红冲失败");

        // 标记原凭证已撤销（红冲）
        var source = await _db.Set<FinVoucher>().FirstOrDefaultAsync(v => v.FID == voucherId);
        if (source != null && !source.FIsRevoked)
        {
            source.FIsRevoked = true;
            source.FUpdatedTime = DateTime.Now;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<long?> GetDefaultAccountSetIdAsync(long orgId)
    {
        // 1. 优先取该组织下 FIsDefault=true 的账套
        var defaultSet = await _accountSetRepo.Query()
            .Where(a => a.FOrgId == orgId && a.FIsDefault && a.FStatus == 1)
            .Select(a => (long?)a.FID)
            .FirstOrDefaultAsync();
        if (defaultSet.HasValue) return defaultSet;

        // 2. 回退：取该组织下任意启用账套
        return await _accountSetRepo.Query()
            .Where(a => a.FOrgId == orgId && a.FStatus == 1)
            .OrderBy(a => a.FSortOrder)
            .ThenBy(a => a.FID)
            .Select(a => (long?)a.FID)
            .FirstOrDefaultAsync();
    }
}
