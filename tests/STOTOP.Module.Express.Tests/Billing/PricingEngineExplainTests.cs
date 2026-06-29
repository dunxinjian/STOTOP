using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

public class PricingEngineExplainTests
{
    [Fact]
    public void PricingEngine_exposes_single_waybill_explain_contract()
    {
        var method = typeof(PricingEngine).GetMethod(nameof(PricingEngine.ExplainAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PricingEngineExplainResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(typeof(BillingWaybillData), parameters[0].ParameterType);
        Assert.Contains(typeof(PricingClientTypeExplainResult).GetProperties(), p => p.Name == nameof(PricingClientTypeExplainResult.Formula));
        Assert.Contains(typeof(PricingEngineExplainResult).GetProperties(), p => p.Name == nameof(PricingEngineExplainResult.ConfigurationIssues));
    }
}
