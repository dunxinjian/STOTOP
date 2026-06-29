using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Repairs;

public class RepairOrderServiceTests
{
    private static RepairOrderService CreateService(STOTOPDbContext db)
        => new RepairOrderService(
            new Repository<DorRepairOrder>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db));

    private static async Task SeedRoomAsync(STOTOPDbContext db)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 创建工单默认待处理()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建工单默认待处理), orgId: 1);
        await SeedRoomAsync(db);
        var dto = await CreateService(db).CreateRepairOrderAsync(new CreateRepairOrderRequest
        { RoomId = 1, ReporterId = 7, Description = "灯坏了", Priority = 2 });
        Assert.Equal(DorStatus.Repair.Pending, dto.Status);
        Assert.Equal("灯坏了", dto.Description);
    }

    [Fact]
    public async Task 处理工单置状态与处理人结果()
    {
        await using var db = TestDbContextFactory.Create(nameof(处理工单置状态与处理人结果), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateRepairOrderAsync(new CreateRepairOrderRequest
        { RoomId = 1, ReporterId = 7, Description = "灯坏了", Priority = 1 });

        var handled = await svc.HandleRepairOrderAsync(created.Id, new HandleRepairOrderRequest
        { HandlerId = 9, Result = "已更换灯管", Status = DorStatus.Repair.Done });

        Assert.NotNull(handled);
        Assert.Equal(DorStatus.Repair.Done, handled!.Status);
        Assert.Equal(9, handled.HandlerId);
        Assert.Equal("已更换灯管", handled.Result);
        Assert.NotNull(handled.HandledTime);
    }

    [Fact]
    public async Task 更新工单改描述与紧急度()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新工单改描述与紧急度), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateRepairOrderAsync(new CreateRepairOrderRequest
        { RoomId = 1, ReporterId = 7, Description = "灯坏了", Priority = 1 });

        var updated = await svc.UpdateRepairOrderAsync(created.Id, new UpdateRepairOrderRequest
        { Description = "灯和插座都坏了", Priority = 3 });

        Assert.NotNull(updated);
        Assert.Equal("灯和插座都坏了", updated!.Description);
        Assert.Equal(3, updated.Priority);
    }
}
