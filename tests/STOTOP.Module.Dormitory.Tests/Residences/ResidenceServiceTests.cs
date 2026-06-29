using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using STOTOP.Module.HR.Entities;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Residences;

public class ResidenceServiceTests
{
    private static ResidenceService CreateService(STOTOPDbContext db)
        => new ResidenceService(
            new Repository<DorResidence>(db),
            new Repository<DorBed>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db),
            new Repository<HrEmployee>(db));

    private static async Task SeedBuildingRoomBedAsync(STOTOPDbContext db, int bedStatus = 1)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        db.Set<DorBed>().Add(new DorBed { FID = 1, FRoomId = 1, FBedNumber = "1", FStatus = bedStatus });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 办理入住_联动置床位已入住()
    {
        await using var db = TestDbContextFactory.Create(nameof(办理入住_联动置床位已入住), orgId: 1);
        await SeedBuildingRoomBedAsync(db);
        var svc = CreateService(db);

        var dto = await svc.CreateResidenceAsync(new CreateResidenceRequest
        {
            BedId = 1, EmployeeId = 100, CheckInDate = new DateTime(2026, 6, 1)
        });

        Assert.Equal(1, dto.Status); // 入住中
        Assert.Equal(2, db.Set<DorBed>().Single(b => b.FID == 1).FStatus); // 床位已入住
    }

    [Fact]
    public async Task 退宿_联动恢复床位空闲()
    {
        await using var db = TestDbContextFactory.Create(nameof(退宿_联动恢复床位空闲), orgId: 1);
        await SeedBuildingRoomBedAsync(db);
        var svc = CreateService(db);
        var res = await svc.CreateResidenceAsync(new CreateResidenceRequest
        {
            BedId = 1, EmployeeId = 100, CheckInDate = new DateTime(2026, 6, 1)
        });

        await svc.CheckOutAsync(res.Id, new CheckOutRequest { CheckOutDate = new DateTime(2026, 6, 30) });

        Assert.Equal(2, db.Set<DorResidence>().Single(r => r.FID == res.Id).FStatus); // 已退宿
        Assert.Equal(1, db.Set<DorBed>().Single(b => b.FID == 1).FStatus); // 床位恢复空闲
    }

    [Fact]
    public async Task 同一床位重复入住被拒()
    {
        await using var db = TestDbContextFactory.Create(nameof(同一床位重复入住被拒), orgId: 1);
        await SeedBuildingRoomBedAsync(db);
        var svc = CreateService(db);
        await svc.CreateResidenceAsync(new CreateResidenceRequest
        {
            BedId = 1, EmployeeId = 100, CheckInDate = new DateTime(2026, 6, 1)
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateResidenceAsync(new CreateResidenceRequest
            {
                BedId = 1, EmployeeId = 101, CheckInDate = new DateTime(2026, 6, 2)
            }));
        Assert.Contains("占用", ex.Message);
    }
}
