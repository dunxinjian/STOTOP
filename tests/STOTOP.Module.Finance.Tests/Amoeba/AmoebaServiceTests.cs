using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

public class AmoebaServiceTests
{
    [Fact]
    public async Task GetTemplateById_keeps_decimal_places_in_item_tree()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetTemplateById_keeps_decimal_places_in_item_tree));
        var template = new FinAmoebaPLTemplate
        {
            FName = "经营报表",
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
        db.Set<FinAmoebaPLTemplate>().Add(template);
        await db.SaveChangesAsync();

        db.Set<FinAmoebaPLItem>().Add(new FinAmoebaPLItem
        {
            FTemplateId = template.FID,
            FItemName = "重量",
            FNodeRole = "data",
            FSort = 10,
            F小数位数 = 3,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetTemplateByIdAsync(template.FID);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Items.Single().DecimalPlaces);
    }

    [Fact]
    public async Task DeleteItem_removes_descendants_with_deleted_parent()
    {
        await using var db = TestDbContextFactory.Create(nameof(DeleteItem_removes_descendants_with_deleted_parent));
        var template = new FinAmoebaPLTemplate
        {
            FName = "经营报表",
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
        db.Set<FinAmoebaPLTemplate>().Add(template);
        await db.SaveChangesAsync();

        var parent = new FinAmoebaPLItem { FTemplateId = template.FID, FItemName = "成本", FNodeRole = "group", FSort = 10, FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow };
        db.Set<FinAmoebaPLItem>().Add(parent);
        await db.SaveChangesAsync();
        var child = new FinAmoebaPLItem { FTemplateId = template.FID, FParentId = parent.FID, FItemName = "运输成本", FNodeRole = "data", FSort = 10, FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow };
        db.Set<FinAmoebaPLItem>().Add(child);
        await db.SaveChangesAsync();
        var grandchild = new FinAmoebaPLItem { FTemplateId = template.FID, FParentId = child.FID, FItemName = "干线成本", FNodeRole = "data", FSort = 10, FCreatedTime = DateTime.UtcNow, FUpdatedTime = DateTime.UtcNow };
        db.Set<FinAmoebaPLItem>().Add(grandchild);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var deleted = await service.DeleteItemAsync(template.FID, parent.FID);

        Assert.True(deleted);
        Assert.Empty(await db.Set<FinAmoebaPLItem>().Where(i => i.FTemplateId == template.FID).ToListAsync());
    }

    [Fact]
    public async Task AddItem_persists_decimal_places()
    {
        await using var db = TestDbContextFactory.Create(nameof(AddItem_persists_decimal_places));
        var template = await SeedTemplateAsync(db);
        var service = CreateService(db);

        var created = await service.AddItemAsync(template.FID, new CreateAmoebaPLItemRequest
        {
            ItemName = "计费重量",
            NodeRole = "data",
            Sort = 10,
            DecimalPlaces = 3,
        });

        Assert.Equal(3, created.DecimalPlaces);
        var stored = await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == created.Id);
        Assert.Equal(3, stored.F小数位数);
    }

    [Fact]
    public async Task AddItem_rejects_nonexistent_parent()
    {
        await using var db = TestDbContextFactory.Create(nameof(AddItem_rejects_nonexistent_parent));
        var template = await SeedTemplateAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddItemAsync(template.FID, new CreateAmoebaPLItemRequest
        {
            ItemName = "孤儿项",
            NodeRole = "data",
            Sort = 10,
            ParentId = 99999,
        }));
        Assert.Empty(await db.Set<FinAmoebaPLItem>().Where(i => i.FTemplateId == template.FID).ToListAsync());
    }

    [Fact]
    public async Task UpdateItem_rejects_moving_under_own_descendant()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateItem_rejects_moving_under_own_descendant));
        var template = await SeedTemplateAsync(db);
        var parent = await SeedItemAsync(db, template.FID, "成本", "group");
        var child = await SeedItemAsync(db, template.FID, "运输成本", "group", parent.FID);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(template.FID, parent.FID, new UpdateAmoebaPLItemRequest
        {
            ItemName = "成本",
            Sort = 10,
            ParentId = child.FID,
        }));
        Assert.Equal(0, (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == parent.FID)).FParentId);
    }

    [Fact]
    public async Task UpdateItem_rejects_self_parent()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateItem_rejects_self_parent));
        var template = await SeedTemplateAsync(db);
        var item = await SeedItemAsync(db, template.FID, "成本", "group");
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(template.FID, item.FID, new UpdateAmoebaPLItemRequest
        {
            ItemName = "成本",
            Sort = 10,
            ParentId = item.FID,
        }));
    }

    [Fact]
    public async Task ReorderItems_rejects_parent_cycle()
    {
        await using var db = TestDbContextFactory.Create(nameof(ReorderItems_rejects_parent_cycle));
        var template = await SeedTemplateAsync(db);
        var a = await SeedItemAsync(db, template.FID, "甲", "group");
        var b = await SeedItemAsync(db, template.FID, "乙", "group", a.FID);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReorderItemsAsync(template.FID, new List<ReorderAmoebaPLItemRequest>
        {
            new() { ItemId = a.FID, Sort = 10, ParentId = b.FID },
            new() { ItemId = b.FID, Sort = 10, ParentId = a.FID },
        }));
        // 整批拒绝，原结构不变
        Assert.Equal(0, (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == a.FID)).FParentId);
        Assert.Equal(a.FID, (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == b.FID)).FParentId);
    }

    [Fact]
    public async Task ReorderItems_rejects_non_indicator_under_indicator_section()
    {
        await using var db = TestDbContextFactory.Create(nameof(ReorderItems_rejects_non_indicator_under_indicator_section));
        var template = await SeedTemplateAsync(db);
        var section = await SeedItemAsync(db, template.FID, "运营指标", "group", configure: i => i.F是否指标分区 = true);
        var cost = await SeedItemAsync(db, template.FID, "运输成本", "data", configure: i => i.F项目类别 = "cost");
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReorderItemsAsync(template.FID, new List<ReorderAmoebaPLItemRequest>
        {
            new() { ItemId = cost.FID, Sort = 10, ParentId = section.FID },
        }));
    }

    [Fact]
    public async Task ReorderItems_tolerates_unrelated_preexisting_cycle()
    {
        await using var db = TestDbContextFactory.Create(nameof(ReorderItems_tolerates_unrelated_preexisting_cycle));
        var template = await SeedTemplateAsync(db);
        // 存量脏环：甲↔乙（与本次请求无关）
        var a = await SeedItemAsync(db, template.FID, "甲", "group");
        var b = await SeedItemAsync(db, template.FID, "乙", "group", a.FID);
        a.FParentId = b.FID;
        await db.SaveChangesAsync();
        var normal = await SeedItemAsync(db, template.FID, "正常项", "data");
        var service = CreateService(db);

        // 仅调整正常项排序，不应被存量环阻断
        var ok = await service.ReorderItemsAsync(template.FID, new List<ReorderAmoebaPLItemRequest>
        {
            new() { ItemId = normal.FID, Sort = 99, ParentId = 0 },
        });

        Assert.True(ok);
        Assert.Equal(99, (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == normal.FID)).FSort);
    }

    [Fact]
    public async Task UpdateItem_rejects_inplace_category_change_under_indicator_section()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateItem_rejects_inplace_category_change_under_indicator_section));
        var template = await SeedTemplateAsync(db);
        var section = await SeedItemAsync(db, template.FID, "运营指标", "group", configure: i => i.F是否指标分区 = true);
        var indicator = await SeedItemAsync(db, template.FID, "发件票量", "indicator", section.FID, i => i.F项目类别 = "indicator");
        var service = CreateService(db);

        // 不移动、仅原地把类别改成 cost，同样必须被拒
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(template.FID, indicator.FID, new UpdateAmoebaPLItemRequest
        {
            ItemName = "发件票量",
            Sort = 10,
            ItemCategory = "cost",
        }));
        Assert.Equal("indicator", (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == indicator.FID)).F项目类别);
    }

    [Fact]
    public async Task DeleteItem_survives_preexisting_parent_cycle()
    {
        await using var db = TestDbContextFactory.Create(nameof(DeleteItem_survives_preexisting_parent_cycle));
        var template = await SeedTemplateAsync(db);
        var a = await SeedItemAsync(db, template.FID, "甲", "group");
        var b = await SeedItemAsync(db, template.FID, "乙", "group", a.FID);
        // 直接在库里制造历史脏数据环：甲↔乙
        a.FParentId = b.FID;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var deleted = await service.DeleteItemAsync(template.FID, a.FID);

        Assert.True(deleted);
        Assert.Empty(await db.Set<FinAmoebaPLItem>().Where(i => i.FTemplateId == template.FID).ToListAsync());
    }

    private static async Task<FinAmoebaPLTemplate> SeedTemplateAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var template = new FinAmoebaPLTemplate
        {
            FName = "经营报表",
            FAccountSetId = 1,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
        db.Set<FinAmoebaPLTemplate>().Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    private static async Task<FinAmoebaPLItem> SeedItemAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long templateId,
        string name,
        string nodeRole,
        long parentId = 0,
        Action<FinAmoebaPLItem>? configure = null)
    {
        var item = new FinAmoebaPLItem
        {
            FTemplateId = templateId,
            FItemName = name,
            FNodeRole = nodeRole,
            FParentId = parentId,
            FSort = 10,
            FCreatedTime = DateTime.UtcNow,
            FUpdatedTime = DateTime.UtcNow,
        };
        configure?.Invoke(item);
        db.Set<FinAmoebaPLItem>().Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task UpdateItem_rename_rewrites_formula_references()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateItem_rename_rewrites_formula_references));
        var template = await SeedTemplateAsync(db);
        var revenue = await SeedItemAsync(db, template.FID, "出港收入", "data");
        var profit = await SeedItemAsync(db, template.FID, "毛利", "formula", configure: i => i.FFormula = "${出港收入}-${运输成本}");
        var service = CreateService(db);

        await service.UpdateItemAsync(template.FID, revenue.FID, new UpdateAmoebaPLItemRequest
        {
            ItemName = "出港总收入",
            Sort = 10,
        });

        var updatedFormula = (await db.Set<FinAmoebaPLItem>().SingleAsync(i => i.FID == profit.FID)).FFormula;
        Assert.Equal("${出港总收入}-${运输成本}", updatedFormula);
    }

    [Fact]
    public async Task DeleteItem_blocked_when_referenced_by_formula()
    {
        await using var db = TestDbContextFactory.Create(nameof(DeleteItem_blocked_when_referenced_by_formula));
        var template = await SeedTemplateAsync(db);
        var revenue = await SeedItemAsync(db, template.FID, "出港收入", "data");
        await SeedItemAsync(db, template.FID, "毛利", "formula", configure: i => i.FFormula = "${出港收入}*0.3");
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteItemAsync(template.FID, revenue.FID));

        Assert.Contains("毛利", ex.Message);
        Assert.NotNull(await db.Set<FinAmoebaPLItem>().FirstOrDefaultAsync(i => i.FID == revenue.FID));
    }

    [Fact]
    public async Task AddItem_trims_item_name()
    {
        await using var db = TestDbContextFactory.Create(nameof(AddItem_trims_item_name));
        var template = await SeedTemplateAsync(db);
        var service = CreateService(db);

        var created = await service.AddItemAsync(template.FID, new CreateAmoebaPLItemRequest
        {
            ItemName = "  运输成本  ",
            NodeRole = "data",
            Sort = 10,
        });

        Assert.Equal("运输成本", created.ItemName);
    }

    private static AmoebaService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        return new AmoebaService(
            new Repository<FinAmoebaPLTemplate>(db),
            new Repository<FinAmoebaPLItem>(db),
            db,
            new HttpContextAccessor());
    }
}
