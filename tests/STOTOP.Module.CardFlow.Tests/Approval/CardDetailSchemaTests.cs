using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class CardDetailSchemaTests
{
    [Fact]
    public void CardDetail_MapsDetailTableKeyColumn()
    {
        using var db = TestDbContextFactory.Create(nameof(CardDetail_MapsDetailTableKeyColumn));
        var entity = db.Model.FindEntityType(typeof(CfCardDetail));
        Assert.NotNull(entity);

        var property = entity!.FindProperty(nameof(CfCardDetail.FDetailTableKey));
        Assert.NotNull(property);

        var table = StoreObjectIdentifier.Table("CF实例明细", null);
        Assert.Equal("F明细表键", property!.GetColumnName(table));
        Assert.Equal("default", property.GetDefaultValue());
    }
}
