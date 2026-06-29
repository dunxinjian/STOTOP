using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IRepository<ExpInvoice> _invoiceRepo;
    private readonly IRepository<ExpInvoiceReviewLog> _reviewLogRepo;
    private readonly IRepository<ExpBillingResult> _billingResultRepo;
    private readonly IRepository<ExpClientWeightCap> _weightCapRepo;
    private readonly IRepository<ExpClientProvinceQuota> _provinceQuotaRepo;
    private readonly IRepository<ExpQuotation> _clientRepo;
    private readonly IRepository<ExpBrand> _brandRepo;
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly IInvoiceReviewService _reviewService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        IRepository<ExpInvoice> invoiceRepo,
        IRepository<ExpInvoiceReviewLog> reviewLogRepo,
        IRepository<ExpBillingResult> billingResultRepo,
        IRepository<ExpClientWeightCap> weightCapRepo,
        IRepository<ExpClientProvinceQuota> provinceQuotaRepo,
        IRepository<ExpQuotation> clientRepo,
        IRepository<ExpBrand> brandRepo,
        IRepository<ExpWaybill> waybillRepo,
        IRepository<ExpProvince> provinceRepo,
        IInvoiceReviewService reviewService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InvoiceService> logger)
    {
        _invoiceRepo = invoiceRepo;
        _reviewLogRepo = reviewLogRepo;
        _billingResultRepo = billingResultRepo;
        _weightCapRepo = weightCapRepo;
        _provinceQuotaRepo = provinceQuotaRepo;
        _clientRepo = clientRepo;
        _brandRepo = brandRepo;
        _waybillRepo = waybillRepo;
        _provinceRepo = provinceRepo;
        _reviewService = reviewService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<PagedResult<InvoiceDto>> GetPagedListAsync(InvoiceQueryRequest request)
    {
        var query = _invoiceRepo.Query();

        // 多网点视角过滤已由全局查询过滤器（IOrgScoped.FOrgId）自动处理

        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(e => e.FBusinessObjectId == request.ClientId);
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.ReviewStatus.HasValue)
            query = query.Where(e => e.FReviewStatus == request.ReviewStatus.Value);
        if (request.PeriodStart.HasValue)
            query = query.Where(e => e.FPeriodEnd >= request.PeriodStart.Value);
        if (request.PeriodEnd.HasValue)
            query = query.Where(e => e.FPeriodStart <= request.PeriodEnd.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(e => e.FInvoiceNo != null && e.FInvoiceNo.Contains(kw));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InvoiceDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InvoiceDetailDto?> GetDetailAsync(long id)
    {
        var entity = await _invoiceRepo.GetByIdAsync(id);
        if (entity == null) return null;

        var logs = await _reviewLogRepo.Query()
            .Where(l => l.FInvoiceId == id)
            .OrderByDescending(l => l.FCreatedTime)
            .ToListAsync();

        var dto = new InvoiceDetailDto
        {
            Id = entity.FID,
            InvoiceNo = entity.FInvoiceNo,
            ClientId = entity.FBusinessObjectId,
            BrandCode = entity.FBrandCode,
            PeriodStart = entity.FPeriodStart,
            PeriodEnd = entity.FPeriodEnd,
            TotalWaybills = entity.FTotalWaybills,
            TotalWeight = entity.FTotalWeight,
            AvgWeight = entity.FAvgWeight,
            WeightCap = entity.FWeightCap,
            ExcessWeight = entity.FExcessWeight,
            WeightCapSurcharge = entity.FWeightCapSurcharge,
            QuotaSurcharge = entity.FQuotaSurcharge,
            TotalCharge = entity.FTotalCharge,
            TotalChargeWithSurcharge = entity.FTotalChargeWithSurcharge,
            TotalCost = entity.FTotalCost,
            TotalProfit = entity.FTotalProfit,
            PrepayDeduction = entity.FPrepayDeduction,
            PayableAmount = entity.FPayableAmount,
            ReviewStatus = entity.FReviewStatus,
            Reviewer = entity.FReviewer,
            ReviewTime = entity.FReviewTime,
            ReviewRemark = entity.FReviewRemark,
            Status = entity.FStatus,
            Archived = entity.FArchived,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            ReviewLogs = logs.Select(l => new InvoiceReviewLogDto
            {
                Id = l.FID,
                Action = l.FAction,
                RuleId = l.FRuleId,
                RuleResult = l.FRuleResult,
                OperatorId = l.FOperatorId,
                Remark = l.FRemark,
                CreatedTime = l.FCreatedTime
            }).ToList()
        };

        return dto;
    }

    public async Task<InvoiceDto> GenerateInvoiceAsync(string clientId, string brandCode, DateTime periodStart, DateTime periodEnd)
    {
        // 1. 汇总账期内所有 BillingResult（FCalcStatus=1 且 FInvoiceId 为空, FPartyRole=1 应收）
        var billingResults = await _billingResultRepo.Query()
            .Where(b => b.FPartyClientId == clientId
                && b.FBrandCode == brandCode
                && b.FCalcStatus == 1
                && b.FInvoiceId == null
                && b.FPartyRole == 1
                && b.FWaybillDate >= periodStart
                && b.FWaybillDate <= periodEnd)
            .ToListAsync();

        var totalWaybills = billingResults.Count;
        var totalWeight = billingResults.Sum(b => b.FBillableWeight ?? 0m);
        var totalCharge = billingResults.Sum(b => b.FChargeAmount ?? 0m);
        var avgWeight = totalWaybills > 0 ? totalWeight / totalWaybills : 0m;

        // 2. 月度均重超标检查
        decimal weightCapSurcharge = 0m;
        decimal? weightCapValue = null;
        decimal? excessWeight = null;

        var weightCap = await _weightCapRepo.Query()
            .Where(w => w.FBusinessObjectId == clientId
                && (w.FBrandCode == null || w.FBrandCode == brandCode)
                && w.FEnabled
                && (w.FEffectiveDate == null || w.FEffectiveDate <= periodEnd)
                && (w.FExpiryDate == null || w.FExpiryDate >= periodStart))
            .FirstOrDefaultAsync();

        if (weightCap != null && totalWaybills > 0 && avgWeight > weightCap.FMaxAvgWeight)
        {
            weightCapValue = weightCap.FMaxAvgWeight;
            excessWeight = avgWeight - weightCap.FMaxAvgWeight;
            var excessUnit = weightCap.FExcessUnit ?? 1m;
            var excessUnitPrice = weightCap.FExcessUnitPrice ?? 0m;
            weightCapSurcharge = Math.Ceiling(excessWeight.Value / excessUnit) * excessUnitPrice * totalWaybills;
        }

        // 3. 目的地占比超标检查
        decimal quotaSurcharge = 0m;
        if (totalWaybills > 0)
        {
            var quotas = await _provinceQuotaRepo.Query()
                .Where(q => q.FBusinessObjectId == clientId
                    && (q.FBrandCode == null || q.FBrandCode == brandCode)
                    && q.FEnabled
                    && (q.FEffectiveDate == null || q.FEffectiveDate <= periodEnd)
                    && (q.FExpiryDate == null || q.FExpiryDate >= periodStart))
                .ToListAsync();

            if (quotas.Count > 0)
            {
                // 占比追补尚未实现（需运单目的省份数据 + 追补费率规则）。此处不再静默加 0，
                // 而是显式告警：配置了省份占比配额的客户本次账单未计算占比追补，可能少计费。
                _logger.LogWarning(
                    "客户 {ClientId}/{BrandCode} 配置了 {Count} 条省份占比配额，但占比追补功能尚未实现，本次账单未计算占比追补，可能少计费",
                    clientId, brandCode, quotas.Count);

                // 获取每个省份的运单数量 - 通过关联运单获取省份信息
                var provinceGroups = billingResults
                    .GroupBy(b => b.FWaybillNo)
                    .Select(g => g.First())
                    .ToList();

                // 需要从BillingResult关联运单获取省份，这里简化为直接从billingResults获取
                // 实际需要join运单表获取目的省份
                foreach (var quota in quotas)
                {
                    // 简化实现：通过SQL统计每个省份的运单数
                    // 在完整实现中应关联运单获取FDestProvinceId
                    var maxRatio = quota.FMaxRatio / 100m; // 百分比转小数
                    // 占比超标附加费在此预留，需要运单省份数据支持
                    // quota surcharge calculation would go here with actual province data
                }
            }
        }

        // 4. 计算总额
        var totalChargeWithSurcharge = totalCharge + weightCapSurcharge + quotaSurcharge;

        // 成本/利润/应付：数据已存在于计费结果（FTotalCost），此前从未汇总，导致发票这些字段恒为 NULL，
        // 发票级利润报表与应付金额无法使用。此处补齐：
        //   总成本 = 各运单成本合计；总利润 = 含追补应收 − 总成本；
        //   预付抵扣在收款环节处理，生成账单阶段不自动扣减客户预付余额（避免有副作用的金额操作）。
        var totalCost = billingResults.Sum(b => b.FTotalCost);
        var totalProfit = totalChargeWithSurcharge - totalCost;
        decimal prepayDeduction = 0m;
        var payableAmount = totalChargeWithSurcharge - prepayDeduction;

        // 5. 生成账单号 INV + yyyyMMdd + 4位序号
        var today = DateTime.Today;
        var dateStr = today.ToString("yyyyMMdd");
        var existingCount = await _invoiceRepo.Query()
            .CountAsync(i => i.FInvoiceNo != null && i.FInvoiceNo.StartsWith("INV" + dateStr));
        var invoiceNo = $"INV{dateStr}{(existingCount + 1):D4}";

        // 6. 创建账单
        var invoice = new ExpInvoice
        {
            FInvoiceNo = invoiceNo,
            FBusinessObjectId = clientId,
            FBrandCode = brandCode,
            FPeriodStart = periodStart,
            FPeriodEnd = periodEnd,
            FTotalWaybills = totalWaybills,
            FTotalWeight = totalWeight,
            FAvgWeight = avgWeight,
            FWeightCap = weightCapValue,
            FExcessWeight = excessWeight,
            FWeightCapSurcharge = weightCapSurcharge,
            FQuotaSurcharge = quotaSurcharge,
            FTotalCharge = totalCharge,
            FTotalChargeWithSurcharge = totalChargeWithSurcharge,
            FTotalCost = totalCost,
            FTotalProfit = totalProfit,
            FPrepayDeduction = prepayDeduction,
            FPayableAmount = payableAmount,
            FReviewStatus = 0,
            FStatus = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _invoiceRepo.AddAsync(invoice);

        // 7. 回填 BillingResult.FInvoiceId
        foreach (var br in billingResults)
        {
            br.FInvoiceId = result.FID;
            await _billingResultRepo.UpdateAsync(br);
        }

        // 8. 自动审核
        await _reviewService.AutoReviewAsync(result.FID);

        // 重新读取（审核可能更新了状态）
        var updated = await _invoiceRepo.GetByIdAsync(result.FID);
        return MapToDto(updated ?? result);
    }

    public async Task<InvoiceDto?> ConfirmAsync(long id)
    {
        var entity = await _invoiceRepo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FStatus = 1; // 已确认
        entity.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<InvoiceDto?> SendAsync(long id)
    {
        var entity = await _invoiceRepo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FStatus = 2; // 已发送
        entity.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<InvoiceDto?> ReceivePaymentAsync(long id, decimal amount)
    {
        var entity = await _invoiceRepo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FStatus = 3; // 已收款
        entity.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(entity);
        return MapToDto(entity);
    }

    private static InvoiceDto MapToDto(ExpInvoice e) => new()
    {
        Id = e.FID,
        InvoiceNo = e.FInvoiceNo,
        ClientId = e.FBusinessObjectId,
        BrandCode = e.FBrandCode,
        PeriodStart = e.FPeriodStart,
        PeriodEnd = e.FPeriodEnd,
        TotalWaybills = e.FTotalWaybills,
        TotalWeight = e.FTotalWeight,
        AvgWeight = e.FAvgWeight,
        WeightCap = e.FWeightCap,
        ExcessWeight = e.FExcessWeight,
        WeightCapSurcharge = e.FWeightCapSurcharge,
        QuotaSurcharge = e.FQuotaSurcharge,
        TotalCharge = e.FTotalCharge,
        TotalChargeWithSurcharge = e.FTotalChargeWithSurcharge,
        TotalCost = e.FTotalCost,
        TotalProfit = e.FTotalProfit,
        PrepayDeduction = e.FPrepayDeduction,
        PayableAmount = e.FPayableAmount,
        ReviewStatus = e.FReviewStatus,
        Reviewer = e.FReviewer,
        ReviewTime = e.FReviewTime,
        ReviewRemark = e.FReviewRemark,
        Status = e.FStatus,
        Archived = e.FArchived,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };

    #region 对账功能

    public async Task<ReconciliationDetailDto> GetReconciliationDetailAsync(long invoiceId)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException("账单不存在");

        // 获取报价方案名称（作为客户名称）
        var client = await _clientRepo.Query().FirstOrDefaultAsync(q => q.FBusinessObjectId == invoice.FBusinessObjectId);
        var clientName = client?.FPlanName ?? "";

        // 获取省份字典
        var provinces = await _provinceRepo.Query().ToListAsync();
        var provinceDict = provinces.ToDictionary(p => p.FID, p => p.FName);

        // 查询计费结果明细（FPartyRole=1 应收，FCalcStatus=1 正常）
        var billingResults = await _billingResultRepo.Query()
            .Where(b => b.FInvoiceId == invoiceId && b.FPartyRole == 1 && b.FCalcStatus == 1)
            .ToListAsync();

        // 获取关联运单信息
        var waybillNos = billingResults.Select(b => b.FWaybillNo).Where(n => n != null).Distinct().ToList();
        var waybills = await _waybillRepo.Query()
            .Where(w => waybillNos.Contains(w.FWaybillNo))
            .ToListAsync();
        var waybillDict = waybills.ToDictionary(w => w.FWaybillNo);

        // 组装明细行
        var lines = billingResults.Select(b =>
        {
            waybillDict.TryGetValue(b.FWaybillNo ?? "", out var wb);
            var provinceName = "";
            if (wb?.FReceiverProvinceId != null)
                provinceDict.TryGetValue(wb.FReceiverProvinceId.Value, out provinceName!);

            return new ReconciliationLineDto
            {
                WaybillId = wb?.FID ?? 0,
                WaybillNo = wb?.FWaybillNo ?? b.FWaybillNo ?? "",
                WaybillDate = wb?.FWaybillDate ?? DateTime.MinValue,
                ProvinceName = provinceName ?? "",
                BillableWeight = b.FBillableWeight ?? 0,
                FreightCharge = b.FFreightCharge ?? 0,
                SurchargeAmount = b.FSurchargeAmount ?? 0,
                ChargeAmount = b.FChargeAmount ?? 0
            };
        }).OrderBy(l => l.WaybillDate).ThenBy(l => l.WaybillNo).ToList();

        return new ReconciliationDetailDto
        {
            InvoiceId = invoice.FID,
            InvoiceNo = invoice.FInvoiceNo ?? "",
            ClientName = clientName,
            PeriodStart = invoice.FPeriodStart,
            PeriodEnd = invoice.FPeriodEnd,
            ReconciliationStatus = invoice.FReconciliationStatus,
            Remarks = invoice.FReconciliationRemarks,
            DisputeReason = invoice.FDisputeReason,
            DisputeResolution = invoice.FDisputeResolution,
            TotalCharge = invoice.FTotalCharge ?? 0,
            TotalWaybills = invoice.FTotalWaybills ?? 0,
            Lines = lines
        };
    }

    public async Task<bool> ConfirmReconciliationAsync(long invoiceId, ReconciliationConfirmRequest request)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) return false;

        if (invoice.FStatus < 2)
            throw new InvalidOperationException("账单尚未发送，无法对账");
        if (invoice.FReconciliationStatus != 0)
            throw new InvalidOperationException("账单已对账或存在异议，无法重复确认");

        var userId = GetCurrentUserId();
        invoice.FReconciliationStatus = 1;
        invoice.FReconciliationRemarks = request.Remarks;
        invoice.FReconciliationBy = userId;
        invoice.FReconciliationTime = DateTime.Now;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);
        return true;
    }

    public async Task<bool> RaiseDisputeAsync(long invoiceId, ReconciliationDisputeRequest request)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) return false;

        if (invoice.FStatus < 2)
            throw new InvalidOperationException("账单尚未发送，无法提起异议");
        if (invoice.FReconciliationStatus != 0 && invoice.FReconciliationStatus != 1)
            throw new InvalidOperationException("当前对账状态无法提起异议");

        invoice.FReconciliationStatus = 2;
        invoice.FDisputeReason = request.Reason;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);
        return true;
    }

    public async Task<bool> ResolveDisputeAsync(long invoiceId, ReconciliationResolveRequest request)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
        if (invoice == null) return false;

        if (invoice.FReconciliationStatus != 2)
            throw new InvalidOperationException("账单当前无异议，无法处理");

        var userId = GetCurrentUserId();
        invoice.FReconciliationStatus = 3;
        invoice.FDisputeResolvedBy = userId;
        invoice.FDisputeResolvedTime = DateTime.Now;
        invoice.FDisputeResolution = request.Resolution;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepo.UpdateAsync(invoice);
        return true;
    }

    public async Task<byte[]> ExportReconciliationAsync(long invoiceId)
    {
        var detail = await GetReconciliationDetailAsync(invoiceId);

        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("对账明细");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        var headers = new[] { "运单号", "运单日期", "目的地", "计费重量", "基础运费", "附加费", "应收金额" };

        // 写表头
        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }
        sheet.CreateFreezePane(0, 1);

        // 写数据行
        int rowIndex = 1;
        foreach (var line in detail.Lines)
        {
            var row = sheet.CreateRow(rowIndex++);
            row.CreateCell(0).SetCellValue(line.WaybillNo);
            row.CreateCell(1).SetCellValue(line.WaybillDate.ToString("yyyy-MM-dd"));
            row.CreateCell(2).SetCellValue(line.ProvinceName);
            row.CreateCell(3).SetCellValue((double)line.BillableWeight);
            row.CreateCell(4).SetCellValue((double)line.FreightCharge);
            row.CreateCell(5).SetCellValue((double)line.SurchargeAmount);
            row.CreateCell(6).SetCellValue((double)line.ChargeAmount);
        }

        // 汇总行
        var sumRow = sheet.CreateRow(rowIndex);
        sumRow.CreateCell(0).SetCellValue("合计");
        sumRow.CreateCell(3).SetCellValue((double)detail.Lines.Sum(l => l.BillableWeight));
        sumRow.CreateCell(4).SetCellValue((double)detail.Lines.Sum(l => l.FreightCharge));
        sumRow.CreateCell(5).SetCellValue((double)detail.Lines.Sum(l => l.SurchargeAmount));
        sumRow.CreateCell(6).SetCellValue((double)detail.Lines.Sum(l => l.ChargeAmount));

        // 汇总行加粗
        var sumStyle = workbook.CreateCellStyle();
        var sumFont = workbook.CreateFont();
        sumFont.IsBold = true;
        sumStyle.SetFont(sumFont);
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sumRow.GetCell(i);
            if (cell != null) cell.CellStyle = sumStyle;
        }

        // 自动列宽
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.AutoSizeColumn(i);
            if (sheet.GetColumnWidth(i) < 3000)
                sheet.SetColumnWidth(i, 3000);
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, true);
        return ms.ToArray();
    }

    private long GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("nameid");
        return claim != null && long.TryParse(claim.Value, out var id) ? id : 0;
    }

    #endregion
}
