using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CfCardInitiatorAssignmentsPersistenceTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task CfCard_PersistsInitiatorAssignmentsJson()
    {
        using var db = TestDbContextFactory.Create(nameof(CfCard_PersistsInitiatorAssignmentsJson));
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 900, FFlowDefinitionId = 1, FFlowVersionId = 1, FStatus = "draft",
            FInitiatorId = 1, FInitiatorName = "u", FCreatedTime = DateTime.Now, FOrgId = 1,
            FInitiatorAssignmentsJson = """{"review":[{"userId":7,"userName":"甲"}]}"""
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Set<CfCard>().FindAsync(900L);
        Assert.NotNull(reloaded);
        Assert.Contains("\"userId\":7", reloaded!.FInitiatorAssignmentsJson);
    }
}
