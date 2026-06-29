namespace STOTOP.Module.Salary.Services;

/// <summary>
/// 薪酬配置服务 — 后续可从数据库/系统参数读取配置
/// 当前实现返回 SalaryConstants 中的默认值
/// </summary>
public interface ISalaryConfigService
{
    decimal GetSocialInsuranceRate(long orgId);
    decimal GetHousingFundRate(long orgId);
    decimal GetTaxExemptionAmount(long orgId);
    decimal GetBScoreToYuanRate(long orgId);
    decimal CalcPersonalIncomeTax(decimal taxableIncome);
}
