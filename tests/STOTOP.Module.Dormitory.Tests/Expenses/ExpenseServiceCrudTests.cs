using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using STOTOP.Module.HR.Entities;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Expenses;

public class ExpenseServiceCrudTests
{
    private static ExpenseService CreateService(STOTOPDbContext db)
        => new ExpenseService(
            new Repository<DorExpense>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db),
            new Repository<DorResidence>(db),
            new Repository<DorBed>(db),
            new Repository<HrEmployee>(db));

    private static async Task SeedRoomAsync(STOTOPDbContext db)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FFloor = 1, FRoomNumber = "101" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 新建费用默认待缴状态0()
    {
        await using var db = TestDbContextFactory.Create(nameof(新建费用默认待缴状态0), orgId: 1);
        await SeedRoomAsync(db);

        var dto = await CreateService(db).CreateExpenseAsync(new CreateExpenseRequest
        {
            RoomId = 1, ExpenseType = "Water", Amount = 100m, Month = "2026-06", ShareMethod = "Equal"
        });

        Assert.Equal(0, dto.Status); // 0=待缴
        Assert.Equal("Equal", dto.ShareMethod);
    }

    [Fact]
    public async Task 更新费用_传状态则更新_不传则保持()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新费用_传状态则更新_不传则保持), orgId: 1);
        await SeedRoomAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateExpenseAsync(new CreateExpenseRequest
        {
            RoomId = 1, ExpenseType = "Water", Amount = 100m, Month = "2026-06"
        });

        // 传 status=1 → 标记已缴
        await svc.UpdateExpenseAsync(created.Id, new UpdateExpenseRequest
        {
            ExpenseType = "Water", Amount = 100m, Month = "2026-06", Status = 1
        });
        Assert.Equal(1, db.Set<DorExpense>().Single(e => e.FID == created.Id).FStatus);

        // 不传 status → 保持原状态，其余字段照常更新
        await svc.UpdateExpenseAsync(created.Id, new UpdateExpenseRequest
        {
            ExpenseType = "Electricity", Amount = 200m, Month = "2026-06"
        });
        var after = db.Set<DorExpense>().Single(e => e.FID == created.Id);
        Assert.Equal(1, after.FStatus);
        Assert.Equal("Electricity", after.FExpenseType);
    }

    [Fact]
    public async Task 创建费用金额为负被拒()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建费用金额为负被拒), orgId: 1);
        await SeedRoomAsync(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db).CreateExpenseAsync(new CreateExpenseRequest
            { RoomId = 1, ExpenseType = "Water", Amount = -5m, Month = "2026-06" }));
    }
}
