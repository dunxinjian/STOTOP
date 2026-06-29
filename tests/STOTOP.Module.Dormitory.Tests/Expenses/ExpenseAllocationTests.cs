using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services;
using STOTOP.Module.HR.Entities;
using Xunit;

namespace STOTOP.Module.Dormitory.Tests.Expenses;

public class ExpenseAllocationTests
{
    private static ExpenseService CreateService(STOTOPDbContext db)
        => new ExpenseService(
            new Repository<DorExpense>(db),
            new Repository<DorRoom>(db),
            new Repository<DorBuilding>(db),
            new Repository<DorResidence>(db),
            new Repository<DorBed>(db),
            new Repository<HrEmployee>(db));

    /// <summary>房间(1) + residentCount 张床+在住员工；可选再加一个已退宿者。费用 FID=1。</summary>
    private static async Task SeedAsync(STOTOPDbContext db, decimal amount, string shareMethod, int residentCount, bool addCheckedOut = false)
    {
        db.Set<DorBuilding>().Add(new DorBuilding { FID = 1, FCode = "B1", FName = "1号楼" });
        db.Set<DorRoom>().Add(new DorRoom { FID = 1, FBuildingId = 1, FFloor = 1, FRoomNumber = "101" });
        db.Set<DorExpense>().Add(new DorExpense
        {
            FID = 1, FRoomId = 1, FExpenseType = "Electricity",
            FAmount = amount, FMonth = "2026-06", FShareMethod = shareMethod, FStatus = 0
        });

        for (int i = 1; i <= residentCount; i++)
        {
            db.Set<DorBed>().Add(new DorBed { FID = i, FRoomId = 1, FBedNumber = i.ToString(), FStatus = 2 });
            db.Set<HrEmployee>().Add(new HrEmployee { FID = 100 + i, FName = "员工" + i });
            db.Set<DorResidence>().Add(new DorResidence
            {
                FID = i, FBedId = i, FEmployeeId = 100 + i, FStatus = 1, FCheckInDate = new DateTime(2026, 6, 1)
            });
        }

        if (addCheckedOut)
        {
            db.Set<DorBed>().Add(new DorBed { FID = 90, FRoomId = 1, FBedNumber = "90", FStatus = 1 });
            db.Set<HrEmployee>().Add(new HrEmployee { FID = 190, FName = "已退宿者" });
            db.Set<DorResidence>().Add(new DorResidence
            {
                FID = 90, FBedId = 90, FEmployeeId = 190, FStatus = 2,
                FCheckInDate = new DateTime(2026, 5, 1), FCheckOutDate = new DateTime(2026, 5, 31)
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task 均摊_整除_每人相等且合计等于总额()
    {
        await using var db = TestDbContextFactory.Create(nameof(均摊_整除_每人相等且合计等于总额));
        await SeedAsync(db, amount: 99m, shareMethod: "Equal", residentCount: 3);

        var dto = await CreateService(db).GetExpenseAllocationAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(3, dto!.OccupantCount);
        Assert.All(dto.Shares, s => Assert.Equal(33m, s.Amount));
        Assert.Equal(99m, dto.AllocatedTotal);
    }

    [Fact]
    public async Task 均摊_不整除_余数归末位且合计严格等于总额()
    {
        await using var db = TestDbContextFactory.Create(nameof(均摊_不整除_余数归末位且合计严格等于总额));
        await SeedAsync(db, amount: 100m, shareMethod: "Equal", residentCount: 3);

        var dto = await CreateService(db).GetExpenseAllocationAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, dto!.Shares.Select(s => s.Amount).ToArray());
        Assert.Equal(100m, dto.AllocatedTotal); // 无分位丢失
    }

    [Fact]
    public async Task 固定_每人按金额收取_合计为金额乘人数()
    {
        await using var db = TestDbContextFactory.Create(nameof(固定_每人按金额收取_合计为金额乘人数));
        await SeedAsync(db, amount: 50m, shareMethod: "Fixed", residentCount: 3);

        var dto = await CreateService(db).GetExpenseAllocationAsync(1);

        Assert.NotNull(dto);
        Assert.All(dto!.Shares, s => Assert.Equal(50m, s.Amount));
        Assert.Equal(150m, dto.AllocatedTotal);
    }

    [Fact]
    public async Task 无在住人_明细为空且合计为0()
    {
        await using var db = TestDbContextFactory.Create(nameof(无在住人_明细为空且合计为0));
        await SeedAsync(db, amount: 100m, shareMethod: "Equal", residentCount: 0);

        var dto = await CreateService(db).GetExpenseAllocationAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(0, dto!.OccupantCount);
        Assert.Empty(dto.Shares);
        Assert.Equal(0m, dto.AllocatedTotal);
    }

    [Fact]
    public async Task 已退宿者不计入分摊()
    {
        await using var db = TestDbContextFactory.Create(nameof(已退宿者不计入分摊));
        await SeedAsync(db, amount: 100m, shareMethod: "Equal", residentCount: 2, addCheckedOut: true);

        var dto = await CreateService(db).GetExpenseAllocationAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.OccupantCount); // 退宿者排除
        Assert.DoesNotContain(dto.Shares, s => s.EmployeeId == 190);
    }

    [Fact]
    public async Task 费用不存在返回null()
    {
        await using var db = TestDbContextFactory.Create(nameof(费用不存在返回null));
        Assert.Null(await CreateService(db).GetExpenseAllocationAsync(999));
    }
}
