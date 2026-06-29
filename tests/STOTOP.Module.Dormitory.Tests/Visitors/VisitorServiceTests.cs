using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Visitors;

public class VisitorServiceTests
{
    private static VisitorService CreateService(STOTOPDbContext db)
        => new VisitorService(
            new Repository<DorVisitor>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db));

    private static async Task SeedBuildingRoomVisitorAsync(STOTOPDbContext db, int status = 1)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FFloor = 1, FRoomNumber = "101" });
        db.Set<DorVisitor>().Add(new DorVisitor
        {
            FID = 1,
            FRoomId = 1,
            FVisitorName = "张三",
            FArrivalTime = new DateTime(2026, 6, 19, 9, 0, 0),
            FStatus = status
        });
        await db.SaveChangesAsync();
    }

    // 回归 R3：前端 visitorLeave(id) 不带 body，服务端登记离开应取当前时间并置状态=2
    [Fact]
    public async Task DepartureAsync_无body时服务端取当前时间并置已离开()
    {
        await using var db = TestDbContextFactory.Create(nameof(DepartureAsync_无body时服务端取当前时间并置已离开));
        await SeedBuildingRoomVisitorAsync(db);
        var service = CreateService(db);

        var before = DateTime.Now;
        var result = await service.DepartureAsync(1); // 无 body
        var after = DateTime.Now;

        Assert.NotNull(result);
        var entity = db.Set<DorVisitor>().Single(v => v.FID == 1);
        Assert.Equal(2, entity.FStatus); // 2 = 已离开
        Assert.NotNull(entity.FDepartureTime);
        Assert.InRange(entity.FDepartureTime!.Value, before.AddSeconds(-2), after.AddSeconds(2));
    }

    [Fact]
    public async Task DepartureAsync_可显式指定离开时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(DepartureAsync_可显式指定离开时间));
        await SeedBuildingRoomVisitorAsync(db);
        var service = CreateService(db);

        var t = new DateTime(2026, 6, 19, 18, 30, 0);
        await service.DepartureAsync(1, t);

        Assert.Equal(t, db.Set<DorVisitor>().Single(v => v.FID == 1).FDepartureTime);
    }

    [Fact]
    public async Task DepartureAsync_记录不存在返回null()
    {
        await using var db = TestDbContextFactory.Create(nameof(DepartureAsync_记录不存在返回null));
        Assert.Null(await CreateService(db).DepartureAsync(999));
    }

    [Fact]
    public async Task CreateVisitorAsync_置来访中并记录到访时间被访人()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateVisitorAsync_置来访中并记录到访时间被访人));
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        await db.SaveChangesAsync();

        var t = new DateTime(2026, 6, 19, 10, 0, 0);
        var dto = await CreateService(db).CreateVisitorAsync(new CreateVisitorRequest
        {
            RoomId = 1, VisitorName = "李四", VisitReason = "探亲", VisitedPersonId = 5, ArrivalTime = t
        });

        var e = db.Set<DorVisitor>().Single(v => v.FID == dto.Id);
        Assert.Equal(1, e.FStatus); // 来访中
        Assert.Equal(t, e.FArrivalTime);
        Assert.Equal(5, e.FVisitedPersonId);
    }

    [Fact]
    public async Task CreateVisitorAsync_房间不存在抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateVisitorAsync_房间不存在抛异常));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db).CreateVisitorAsync(new CreateVisitorRequest
            { RoomId = 999, VisitorName = "无", ArrivalTime = new DateTime(2026, 6, 19) }));
    }
}
