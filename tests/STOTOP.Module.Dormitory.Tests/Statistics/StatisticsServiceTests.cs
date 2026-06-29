using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Statistics;

public class StatisticsServiceTests
{
    private static StatisticsService CreateService(STOTOPDbContext db)
        => new StatisticsService(
            new Repository<DorBed>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db),
            new Repository<DorResidence>(db),
            new Repository<DorExpense>(db),
            new Repository<DorRepairOrder>(db),
            new Repository<DorVisitor>(db));

    [Fact]
    public async Task 统计_占用按床位状态_待缴按费用状态0()
    {
        await using var db = TestDbContextFactory.Create(nameof(统计_占用按床位状态_待缴按费用状态0), orgId: 1);
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼", FStatus = 1 });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FRoomNumber = "101" });
        db.Set<DorBed>().Add(new DorBed { FID = 1, FRoomId = 1, FBedNumber = "1", FStatus = 2 }); // 已入住
        db.Set<DorBed>().Add(new DorBed { FID = 2, FRoomId = 1, FBedNumber = "2", FStatus = 1 }); // 空闲
        db.Set<DorExpense>().Add(new DorExpense { FID = 1, FRoomId = 1, FExpenseType = "Water", FAmount = 100m, FMonth = "2026-06", FStatus = 0 }); // 待缴
        db.Set<DorExpense>().Add(new DorExpense { FID = 2, FRoomId = 1, FExpenseType = "Electricity", FAmount = 50m, FMonth = "2026-06", FStatus = 1 }); // 已缴
        await db.SaveChangesAsync();

        var stats = await CreateService(db).GetStatisticsAsync();

        Assert.Equal(2, stats.TotalBeds);
        Assert.Equal(1, stats.OccupiedBeds);          // 仅床位状态=2
        Assert.Equal(100m, stats.PendingExpenses);    // 仅费用状态=0(待缴)，不含已缴的50
        var occ = Assert.Single(stats.BuildingOccupancies);
        Assert.Equal(2, occ.TotalBeds);
        Assert.Equal(1, occ.OccupiedBeds);
    }
}
