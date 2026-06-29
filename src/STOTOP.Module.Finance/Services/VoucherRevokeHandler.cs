using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;
// 消歧义：本文件 IVoucherService 指 Finance 内部接口
using IVoucherService = STOTOP.Module.Finance.Services.Interfaces.IVoucherService;

namespace STOTOP.Module.Finance.Services;

/// <summary>凭证撤销处理器：支持按 DataScopeId 批量撤销/红冲凭证</summary>
public class VoucherRevokeHandler : IDataScopeRevokeHandler
{
    private readonly STOTOPDbContext _context;
    private readonly IVoucherService _voucherService;
    private readonly ILogger<VoucherRevokeHandler> _logger;

    public VoucherRevokeHandler(
        STOTOPDbContext context,
        IVoucherService voucherService,
        ILogger<VoucherRevokeHandler> logger)
    {
        _context = context;
        _voucherService = voucherService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string HandlerName => "VoucherRevokeHandler";

    /// <summary>IDataScopeRevokeHandler 接口实现，返回影响行数</summary>
    async Task<int> IDataScopeRevokeHandler.RevokeByDataScopeAsync(string dataScopeId, long operatorId)
    {
        var result = await RevokeByDataScopeAsync(dataScopeId, operatorId);
        return result.TotalCount;
    }

    /// <summary>按 DataScopeId 撤销凭证：未过账→标记作废，已过账→生成红冲凭证</summary>
    /// <param name="dataScopeId">数据作用域ID（通常为批次ID）</param>
    /// <param name="operatorId">操作人ID</param>
    /// <returns>撤销的凭证数量</returns>
    public async Task<VoucherRevokeResult> RevokeByDataScopeAsync(string dataScopeId, long operatorId)
    {
        var vouchers = await _context.Set<FinVoucher>()
            .Include(v => v.Entries)
            .Where(v => v.FDataScopeId == dataScopeId && !v.FIsRevoked)
            .ToListAsync();

        if (vouchers.Count == 0)
        {
            _logger.LogInformation("VoucherRevokeHandler: DataScopeId={DataScopeId} 下没有可撤销的凭证", dataScopeId);
            return new VoucherRevokeResult { TotalCount = 0 };
        }

        int revokedCount = 0;
        int redVoucherCount = 0;
        var errors = new List<string>();

        foreach (var voucher in vouchers)
        {
            try
            {
                if (voucher.FStatus == 2) // 已审核/已过账 → 生成红冲凭证
                {
                    await GenerateRedVoucherAsync(voucher, operatorId);
                    voucher.FIsRevoked = true;
                    redVoucherCount++;
                }
                else // 未审核（草稿/待审核）→ 直接标记作废
                {
                    voucher.FIsRevoked = true;
                    voucher.FStatus = -1; // 作废状态
                }
                revokedCount++;
            }
            catch (Exception ex)
            {
                var msg = $"撤销凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}(ID:{voucher.FID}) 失败: {ex.Message}";
                _logger.LogWarning(ex, "VoucherRevokeHandler: {Message}", msg);
                errors.Add(msg);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "VoucherRevokeHandler: DataScopeId={DataScopeId} 撤销完成，共{Total}张，作废{Revoked}张，红冲{Red}张，失败{Errors}张",
            dataScopeId, vouchers.Count, revokedCount - redVoucherCount, redVoucherCount, errors.Count);

        return new VoucherRevokeResult
        {
            TotalCount = vouchers.Count,
            RevokedCount = revokedCount,
            RedVoucherCount = redVoucherCount,
            Errors = errors
        };
    }

    /// <summary>生成红冲凭证（金额取反）</summary>
    private async Task GenerateRedVoucherAsync(FinVoucher original, long operatorId)
    {
        // 获取下一个凭证号
        var maxNo = await _context.Set<FinVoucher>()
            .Where(v => v.FVoucherWord == original.FVoucherWord
                     && v.FPeriodId == original.FPeriodId
                     && v.FAccountSetId == original.FAccountSetId)
            .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;

        var redVoucher = new FinVoucher
        {
            FVoucherWord = original.FVoucherWord,
            FVoucherNo = maxNo + 1,
            FDate = DateTime.Today,
            FPeriodId = original.FPeriodId,
            FAttachmentCount = 0,
            FCreator = original.FCreator,
            FStatus = 1, // 待审核
            FSource = $"red-voucher:{original.FID}",
            FRemark = $"红冲凭证（原凭证: {original.FVoucherWord}{original.FVoucherNo}）",
            FDataScopeId = original.FDataScopeId,
            FAccountSetId = original.FAccountSetId,
            FOrgId = original.FOrgId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _context.Set<FinVoucher>().Add(redVoucher);
        await _context.SaveChangesAsync(); // 获取 FID

        foreach (var entry in original.Entries.OrderBy(e => e.FLineNo))
        {
            var redEntry = new FinVoucherEntry
            {
                FVoucherId = redVoucher.FID,
                FLineNo = entry.FLineNo,
                FSummary = $"红冲: {entry.FSummary}",
                FAccountId = entry.FAccountId,
                FAccountCode = entry.FAccountCode,
                FAccountName = entry.FAccountName,
                FAuxiliaryJson = entry.FAuxiliaryJson,
                FDebitAmount = -entry.FDebitAmount,
                FCreditAmount = -entry.FCreditAmount,
                FDataScopeId = entry.FDataScopeId,
                FOrgId = entry.FOrgId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            _context.Set<FinVoucherEntry>().Add(redEntry);
        }

        _logger.LogInformation(
            "VoucherRevokeHandler: 已生成红冲凭证 {RedVoucherId} (原凭证 {OriginalId}: {Word}{No})",
            redVoucher.FID, original.FID, original.FVoucherWord, original.FVoucherNo);
    }
}

/// <summary>凭证撤销结果</summary>
public class VoucherRevokeResult
{
    public int TotalCount { get; set; }
    public int RevokedCount { get; set; }
    public int RedVoucherCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
