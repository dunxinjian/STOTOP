using STOTOP.Module.Salary.Constants;

namespace STOTOP.Module.Salary.Services;

public class SalaryConfigService : ISalaryConfigService
{
    public decimal GetSocialInsuranceRate(long orgId) => SalaryConstants.SocialInsuranceRate;
    public decimal GetHousingFundRate(long orgId) => SalaryConstants.HousingFundRate;
    public decimal GetTaxExemptionAmount(long orgId) => SalaryConstants.TaxExemptionAmount;
    public decimal GetBScoreToYuanRate(long orgId) => SalaryConstants.BScoreToYuanRate;

    public decimal CalcPersonalIncomeTax(decimal taxableIncome)
    {
        if (taxableIncome <= 0) return 0;
        foreach (var (upperLimit, rate, quickDeduction) in SalaryConstants.TaxBrackets)
        {
            if (taxableIncome <= upperLimit)
                return taxableIncome * rate - quickDeduction;
        }
        return 0; // 不应到达此处
    }
}
