using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ApprovalModeNormalizationTests
{
    [Fact]
    public void NormalizeApprovalMode_PreservesSequential()
    {
        var method = typeof(FlowDefinitionService).GetMethod(
            "NormalizeApprovalMode",
            global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal("sequential", method!.Invoke(null, new object?[] { "sequential" }));
        Assert.Equal("countersign", method.Invoke(null, new object?[] { "allapprove" }));
        Assert.Equal("single", method.Invoke(null, new object?[] { "anyapprove" }));
    }
}
