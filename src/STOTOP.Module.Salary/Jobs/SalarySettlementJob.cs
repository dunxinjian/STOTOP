using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.KSF.Entities;
using STOTOP.Module.PPV.Entities;
using STOTOP.Module.Salary.Entities;
using STOTOP.Module.Salary.Services;

namespace STOTOP.Module.Salary.Jobs;

/// <summary>
/// 月度工资结算 Job。每月 6 日 05:00 执行（RecurringJobId: sal.monthly-settlement）。
///
/// 业务流程（以"上月"为核算期间，period = yyyyMM）：
/// 1. 查询有薪酬档案的员工。
/// 2. 对每个员工聚合 KSF 浮动薪酬 + PPV 产值奖金 + B 分兑换 + 基本工资。
/// 3. 计算社保、公积金、个税后生成月度工资单 + 明细行。
/// 4. 幂等写入（只覆盖草稿/待审状态的旧工资单）。
/// 5. 单员工异常 try-catch 隔离，不中断整体。
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
[AutomaticRetry(Attempts = 0)]
public class SalarySettlementJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<SalarySettlementJob> _logger;
    private readonly ISalaryConfigService _configService;

    public SalarySettlementJob(
        STOTOPDbContext db,
        ILogger<SalarySettlementJob> logger,
        ISalaryConfigService configService)
    {
        _db = db;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// 月度结算入口。
    /// </summary>
    /// <param name="period">期间 yyyyMM，默认上月</param>
    /// <param name="specificEmployeeId">仅结算指定员工（手动重跑场景），不传则全量</param>
    public async Task Execute(string? period = null, long? specificEmployeeId = null)
    {
        period ??= DateTime.Now.AddMonths(-1).ToString("yyyyMM");

        _logger.LogInformation("SalarySettlementJob 启动 | period={Period} | specificEmployee={Employee}",
            period, specificEmployeeId?.ToString() ?? "<all>");

        // 1. 查询有薪酬档案的员工
        var archives = await _db.Set<SalaryArchive>()
            .Where(a => specificEmployeeId == null || a.F员工ID == specificEmployeeId)
            .ToListAsync();

        int totalEmployees = 0, totalSucceeded = 0, totalFailed = 0;

        // 2. 对每个员工结算
        foreach (var archive in archives)
        {
            totalEmployees++;
            try
            {
                await SettleForEmployee(archive, period);
                totalSucceeded++;
                _logger.LogInformation(
                    "SalarySettlementJob 员工结算完成 | employeeId={EmployeeId} | orgId={OrgId} | period={Period}",
                    archive.F员工ID, archive.FOrgId, period);
            }
            catch (Exception ex)
            {
                totalFailed++;
                _logger.LogError(ex,
                    "SalarySettlementJob 单员工结算失败 | employeeId={EmployeeId} | orgId={OrgId} | period={Period}",
                    archive.F员工ID, archive.FOrgId, period);
            }
        }

        _logger.LogInformation(
            "SalarySettlementJob 完成 | period={Period} | employees={EmpCount} | ok={Ok} | fail={Fail}",
            period, totalEmployees, totalSucceeded, totalFailed);
    }

    private async Task SettleForEmployee(SalaryArchive archive, string period)
    {
        var employeeId = archive.F员工ID;
        var orgId = archive.FOrgId;

        // a. 基本工资 & 岗位津贴
        var baseSalary = archive.F基本工资;
        var positionAllowance = archive.F岗位津贴;

        // b. 查询 KSF 浮动薪酬（F状态=2 正式）
        var ksfResult = await _db.Set<KsfResult>()
            .Where(r => r.F员工ID == employeeId && r.F期间 == period && r.F状态 == 2)
            .FirstOrDefaultAsync();
        decimal ksfAmount = ksfResult?.F浮动部分 ?? 0;

        // c. 查询 PPV 月度汇总（F状态=1 正常）
        var ppvResult = await _db.Set<PpvMonthlyResult>()
            .Where(r => r.F员工ID == employeeId && r.F期间 == period && r.F状态 == 1)
            .FirstOrDefaultAsync();
        decimal ppvAmount = ppvResult?.F总产值 ?? 0;

        // d. 查询 B 分兑换（兑换类型=2 工资）
        var bConversion = await _db.Set<SalaryBScoreConversion>()
            .Where(c => c.F员工ID == employeeId && c.F期间 == period)
            .FirstOrDefaultAsync();
        decimal bScoreAmount = bConversion?.F兑换金额 ?? 0;

        // e. 考勤扣减（预留接口，默认 0）
        decimal attendanceDeduction = 0; // TODO: 对接考勤模块

        // f. 社保个人 = F社保基数 × 社保比例（默认10.5%：养老8% + 医疗2% + 失业0.5%）
        decimal socialInsurance = Math.Round(archive.F社保基数 * _configService.GetSocialInsuranceRate(orgId), 2);

        // g. 公积金个人 = F公积金基数 × 公积金比例（默认12%）
        decimal housingFund = Math.Round(archive.F公积金基数 * _configService.GetHousingFundRate(orgId), 2);

        // h. 应发合计 = 基本工资 + 岗位津贴 + KSF + PPV + B分兑换 - 考勤扣减
        decimal grossPay = baseSalary + positionAllowance + ksfAmount + ppvAmount + bScoreAmount - attendanceDeduction;

        // i. 个税：起征额优先取员工档案中的F个税起征额，为0时fallback到系统默认值
        decimal exemption = archive.F个税起征额 > 0
            ? archive.F个税起征额
            : _configService.GetTaxExemptionAmount(orgId);
        decimal taxableIncome = grossPay - socialInsurance - housingFund - exemption;
        decimal tax = _configService.CalcPersonalIncomeTax(taxableIncome);

        // j. 实发合计 = 应发合计 - 社保 - 公积金 - 个税
        decimal netPay = grossPay - socialInsurance - housingFund - tax;

        // 4. 幂等写入（事务内）
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 只删除草稿(0)或待审(1)状态的旧工资单，已审/已发放的不覆盖
            var oldPayrolls = await _db.Set<SalaryPayroll>()
                .Where(p => p.F员工ID == employeeId && p.F期间 == period && p.FOrgId == orgId
                    && (p.F状态 == 0 || p.F状态 == 1))
                .ToListAsync();

            if (oldPayrolls.Count > 0)
            {
                var oldPayrollIds = oldPayrolls.Select(p => p.FID).ToList();

                // 先删明细
                var oldDetails = await _db.Set<SalaryPayrollDetail>()
                    .Where(d => oldPayrollIds.Contains(d.F工资单ID))
                    .ToListAsync();
                if (oldDetails.Count > 0)
                {
                    _db.RemoveRange(oldDetails);
                }

                _db.RemoveRange(oldPayrolls);
                await _db.SaveChangesAsync();
            }

            // 如果已存在已审/已发放的工资单，跳过该员工
            var hasApproved = await _db.Set<SalaryPayroll>()
                .AnyAsync(p => p.F员工ID == employeeId && p.F期间 == period && p.FOrgId == orgId
                    && (p.F状态 == 2 || p.F状态 == 3));
            if (hasApproved)
            {
                await tx.RollbackAsync();
                _logger.LogInformation(
                    "SalarySettlementJob 员工已有已审/已发放工资单，跳过 | employeeId={EmployeeId} | period={Period}",
                    employeeId, period);
                return;
            }

            // INSERT 新工资单
            var payroll = new SalaryPayroll
            {
                FOrgId = orgId,
                F员工ID = employeeId,
                F期间 = period,
                F基本工资 = baseSalary,
                FKSF浮动 = ksfAmount,
                FPPV奖金 = ppvAmount,
                FB分兑换 = bScoreAmount,
                F考勤扣减 = attendanceDeduction,
                F社保个人 = socialInsurance,
                F公积金个人 = housingFund,
                F个税 = tax,
                F应发合计 = grossPay,
                F实发合计 = netPay,
                F状态 = 1, // 待审
                F创建时间 = DateTime.Now,
            };
            _db.Add(payroll);
            await _db.SaveChangesAsync();

            // INSERT 明细行
            var details = new List<SalaryPayrollDetail>
            {
                CreateDetail(orgId, payroll.FID, 1, "基本工资", baseSalary),
                CreateDetail(orgId, payroll.FID, 2, "岗位津贴", positionAllowance),
                CreateDetail(orgId, payroll.FID, 3, "KSF浮动", ksfAmount, ksfResult?.FID, "KsfResult"),
                CreateDetail(orgId, payroll.FID, 4, "PPV奖金", ppvAmount, ppvResult?.FID, "PpvMonthlyResult"),
                CreateDetail(orgId, payroll.FID, 5, "B分兑换", bScoreAmount, bConversion?.FID, "SalaryBScoreConversion"),
                CreateDetail(orgId, payroll.FID, 6, "考勤扣减", -attendanceDeduction),
                CreateDetail(orgId, payroll.FID, 7, "社保个人", -socialInsurance),
                CreateDetail(orgId, payroll.FID, 8, "公积金个人", -housingFund),
                CreateDetail(orgId, payroll.FID, 9, "个税", -tax),
            };
            _db.AddRange(details);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static SalaryPayrollDetail CreateDetail(
        long orgId, long payrollId, int itemType, string itemName, decimal amount,
        long? sourceId = null, string? sourceType = null)
    {
        return new SalaryPayrollDetail
        {
            FOrgId = orgId,
            F工资单ID = payrollId,
            F项目类型 = itemType,
            F项目名称 = itemName,
            F金额 = amount,
            F来源ID = sourceId,
            F来源类型 = sourceType,
        };
    }
}
