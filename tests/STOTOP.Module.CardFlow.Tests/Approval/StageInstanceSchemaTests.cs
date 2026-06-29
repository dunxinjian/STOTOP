using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageInstanceSchemaTests
{
    [Fact]
    public void StageInstance_MapsInsertContextJsonColumn()
    {
        using var db = TestDbContextFactory.Create(nameof(StageInstance_MapsInsertContextJsonColumn));
        var entity = db.Model.FindEntityType(typeof(CfStageInstance));
        Assert.NotNull(entity);

        var property = entity!.FindProperty(nameof(CfStageInstance.FInsertContextJson));
        Assert.NotNull(property);

        var table = StoreObjectIdentifier.Table("CF节点执行实例", null);
        Assert.Equal("F插入上下文JSON", property!.GetColumnName(table));
    }
}
