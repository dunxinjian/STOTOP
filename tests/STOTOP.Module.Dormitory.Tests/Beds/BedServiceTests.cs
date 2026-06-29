using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Beds;

public class BedServiceTests
{
    private static BedService CreateService(STOTOPDbContext db)
        => new BedService(new Repository<DorBed>(db), new Repository<DorRoom>(db));

    private static async Task SeedRoomAsync(STOTOPDbContext db)
    {
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 新建床位默认空闲()
    {
        await using var db = TestDbContextFactory.Create(nameof(新建床位默认空闲), orgId: 1);
        await SeedRoomAsync(db);
        var dto = await CreateService(db).CreateBedAsync(1, new CreateBedRequest { BedNumber = "1", BedType = "lower" });
        Assert.Equal(DorStatus.Bed.Free, dto.Status);
    }

    [Fact]
    public async Task 更新床位可改状态为维修中()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新床位可改状态为维修中), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        var bed = await svc.CreateBedAsync(1, new CreateBedRequest { BedNumber = "1", BedType = "lower" });

        await svc.UpdateBedAsync(bed.Id, new UpdateBedRequest
        { BedNumber = "1", BedType = "lower", Status = DorStatus.Bed.Maintenance });

        Assert.Equal(DorStatus.Bed.Maintenance, db.Set<DorBed>().Single(b => b.FID == bed.Id).FStatus);
    }
}
