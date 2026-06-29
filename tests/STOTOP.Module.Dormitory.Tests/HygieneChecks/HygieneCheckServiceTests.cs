using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.HygieneChecks;

public class HygieneCheckServiceTests
{
    private static HygieneCheckService CreateService(STOTOPDbContext db)
        => new HygieneCheckService(
            new Repository<DorHygieneCheck>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db));

    private static async Task SeedRoomAsync(STOTOPDbContext db)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 创建评分超范围被拒()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建评分超范围被拒), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateHygieneCheckAsync(new CreateHygieneCheckRequest
            { RoomId = 1, InspectorId = 5, CheckDate = new DateTime(2026, 6, 1), Score = 101, Result = "优秀" }));
    }

    [Fact]
    public async Task 更新卫生检查改分数与结果()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新卫生检查改分数与结果), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateHygieneCheckAsync(new CreateHygieneCheckRequest
        { RoomId = 1, InspectorId = 5, CheckDate = new DateTime(2026, 6, 1), Score = 80, Result = "良好" });

        var updated = await svc.UpdateHygieneCheckAsync(created.Id, new UpdateHygieneCheckRequest
        { InspectorId = 5, CheckDate = new DateTime(2026, 6, 2), Score = 95, Result = "优秀", Remark = "整改完成" });

        Assert.NotNull(updated);
        Assert.Equal(95, updated!.Score);
        Assert.Equal("优秀", updated.Result);
    }
}
