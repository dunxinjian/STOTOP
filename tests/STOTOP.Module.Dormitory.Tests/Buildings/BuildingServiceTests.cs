using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.HR.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Buildings;

public class BuildingServiceTests
{
    private static BuildingService CreateService(STOTOPDbContext db)
        => new BuildingService(new Repository<DorBuilding>(db), new Repository<HrEmployee>(db));

    // 坐实事实：SaveChanges 对新 IOrgScoped 实体自动注入当前组织（反驳"FOrgId 未初始化=P0漏洞"误报）
    [Fact]
    public async Task CreateBuildingAsync_自动注入当前组织()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateBuildingAsync_自动注入当前组织), orgId: 192);
        var service = CreateService(db);

        var dto = await service.CreateBuildingAsync(new CreateBuildingRequest { Code = "B1", Name = "1号楼", TotalFloors = 6 });

        Assert.True(dto.Id > 0); // FID 由数据库生成
        var entity = db.Set<DorBuilding>().IgnoreQueryFilters().Single(b => b.FID == dto.Id);
        Assert.Equal(192, entity.FOrgId);
    }

    [Fact]
    public async Task CreateBuildingAsync_编码重复抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateBuildingAsync_编码重复抛异常), orgId: 192);
        var service = CreateService(db);
        await service.CreateBuildingAsync(new CreateBuildingRequest { Code = "B1", Name = "1号楼" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateBuildingAsync(new CreateBuildingRequest { Code = "B1", Name = "重复编码" }));
    }

    // 楼栋列表实时统计房间数/床位数/已占用床位（床位状态=2 已入住）
    [Fact]
    public async Task GetBuildings_统计房间床位与占用数()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetBuildings_统计房间床位与占用数), orgId: 1);
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 2, FBuildingId = 1, FRoomNumber = "102" });
        db.Set<DorBed>().Add(new DorBed { FID = 1, FRoomId = 1, FBedNumber = "1", FStatus = 2 }); // 已入住
        db.Set<DorBed>().Add(new DorBed { FID = 2, FRoomId = 1, FBedNumber = "2", FStatus = 1 }); // 空闲
        db.Set<DorBed>().Add(new DorBed { FID = 3, FRoomId = 2, FBedNumber = "1", FStatus = 1 }); // 空闲
        await db.SaveChangesAsync();

        var page = await CreateService(db).GetBuildingsAsync(new BuildingQueryRequest { PageIndex = 1, PageSize = 10 });
        var b = Assert.Single(page.Items);
        Assert.Equal(2, b.RoomCount);
        Assert.Equal(3, b.BedCount);
        Assert.Equal(1, b.OccupiedBeds);
    }

    // 组织隔离：192 创建的楼栋，193 看不到，192 看得到
    [Fact]
    public async Task GetBuildings_按组织隔离()
    {
        var dbName = nameof(GetBuildings_按组织隔离);

        await using (var db192 = TestDbContextFactory.CreateShared(dbName, orgId: 192))
            await CreateService(db192).CreateBuildingAsync(new CreateBuildingRequest { Code = "B1", Name = "甲楼" });

        await using (var db193 = TestDbContextFactory.CreateShared(dbName, orgId: 193))
        {
            var page = await CreateService(db193).GetBuildingsAsync(new BuildingQueryRequest { PageIndex = 1, PageSize = 10 });
            Assert.Equal(0, page.Total);
        }

        await using (var db192b = TestDbContextFactory.CreateShared(dbName, orgId: 192))
        {
            var page = await CreateService(db192b).GetBuildingsAsync(new BuildingQueryRequest { PageIndex = 1, PageSize = 10 });
            Assert.Equal(1, page.Total);
        }
    }
}
