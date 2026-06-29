using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 自动出账定时任务
/// </summary>
public class InvoiceGeneratorJob
{
    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IInvoiceService _invoiceService;

    public InvoiceGeneratorJob(
        IRepository<ExpQuotation> quotationRepo,
        IInvoiceService invoiceService)
    {
        _quotationRepo = quotationRepo;
        _invoiceService = invoiceService;
    }

    /// <summary>
    /// 执行自动出账
    /// 遍历所有 FStatus=1 的报价方案，根据账单周期和出账日生成账单
    /// </summary>
    public async Task ExecuteAsync()
    {
        var today = DateTime.Today;
        var dayOfWeek = (int)today.DayOfWeek; // 0=Sunday, 1=Monday...
        var dayOfMonth = today.Day;

        // 获取所有启用的报价方案（包含账单配置）
        var quotations = await _quotationRepo.Query()
            .Where(q => q.FStatus == 1 && q.FBillingDay != null)
            .ToListAsync();

        foreach (var quotation in quotations)
        {
            try
            {
                DateTime periodStart, periodEnd;

                if (quotation.FBillingCycle == 2) // 周结
                {
                    if (quotation.FBillingDay != dayOfWeek && !(dayOfWeek == 0 && quotation.FBillingDay == 7))
                        continue;

                    periodEnd = today.AddDays(-1);
                    periodStart = periodEnd.AddDays(-6);
                }
                else if (quotation.FBillingCycle == 3) // 月结
                {
                    if (quotation.FBillingDay != dayOfMonth)
                        continue;

                    periodEnd = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                    periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1);
                }
                else
                {
                    continue;
                }

                await _invoiceService.GenerateInvoiceAsync(quotation.FBusinessObjectId ?? string.Empty, quotation.FBrandCode, periodStart, periodEnd);
            }
            catch
            {
                continue;
            }
        }
    }
}
