using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class StageAssigneeSchemaTests
{
    [Fact]
    public void StageAssignee_MapsSortOrderColumn()
    {
        using var db = TestDbContextFactory.Create(nameof(StageAssignee_MapsSortOrderColumn));
        var entity = db.Model.FindEntityType(typeof(CfStageAssignee));
        Assert.NotNull(entity);

        var property = entity!.FindProperty(nameof(CfStageAssignee.FSortOrder));
        Assert.NotNull(property);

        var table = StoreObjectIdentifier.Table("CF节点处理人", null);
        Assert.Equal("F排序", property!.GetColumnName(table));
        Assert.Equal(0, property.GetDefaultValue());
    }
}
