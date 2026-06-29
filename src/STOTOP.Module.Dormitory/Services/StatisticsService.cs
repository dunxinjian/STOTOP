using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IRepository<DorBed> _bedRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;
    private readonly IRepository<DorResidence> _residenceRepository;
    private readonly IRepository<DorExpense> _expenseRepository;
    private readonly IRepository<DorRepairOrder> _repairOrderRepository;
    private readonly IRepository<DorVisitor> _visitorRepository;

    public StatisticsService(
        IRepository<DorBed> bedRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository,
        IRepository<DorResidence> residenceRepository,
        IRepository<DorExpense> expenseRepository,
        IRepository<DorRepairOrder> repairOrderRepository,
        IRepository<DorVisitor> visitorRepository)
    {
        _bedRepository = bedRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
        _residenceRepository = residenceRepository;
        _expenseRepository = expenseRepository;
        _repairOrderRepository = repairOrderRepository;
        _visitorRepository = visitorRepository;
    }

    public async Task<DormitoryStatisticsDto> GetStatisticsAsync()
    {
        // 获取总床位数
        var totalBeds = await _bedRepository.Query().CountAsync();

        // 已入住床位数（床位状态 2=已入住，入住/退宿时联动维护）
        var occupiedBeds = await _bedRepository.Query()
            .CountAsync(b => b.FStatus == DorStatus.Bed.Occupied);

        var availableBeds = totalBeds - occupiedBeds;

        // 计算入住率和空置率
        var occupancyRate = totalBeds > 0 ? Math.Round((decimal)occupiedBeds / totalBeds * 100, 2) : 0;
        var vacancyRate = totalBeds > 0 ? Math.Round((decimal)availableBeds / totalBeds * 100, 2) : 0;

        // 各楼栋入住统计
        var buildingOccupancies = await GetBuildingOccupanciesAsync();

        // 本月费用汇总
        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        var monthlyExpenseSummaries = await GetMonthlyExpenseSummariesAsync(currentMonth);

        // 待处理报修工单数
        var pendingRepairOrders = await _repairOrderRepository.Query()
            .CountAsync(ro => ro.FStatus == DorStatus.Repair.Pending);

        // 今日访客数
        var today = DateTime.Today;
        var todayVisitors = await _visitorRepository.Query()
            .CountAsync(v => v.FArrivalTime.Date == today);

        // 总楼栋数
        var totalBuildings = await _buildingRepository.Query()
            .CountAsync(b => b.FStatus == DorStatus.Enable.Enabled);

        // 总房间数
        var totalRooms = await _roomRepository.Query().CountAsync();

        // 待缴费用总额（费用状态 0=待缴）
        var pendingExpenses = await _expenseRepository.Query()
            .Where(e => e.FStatus == DorStatus.Expense.Unpaid)
            .SumAsync(e => e.FAmount);

        return new DormitoryStatisticsDto
        {
            TotalBeds = totalBeds,
            OccupiedBeds = occupiedBeds,
            AvailableBeds = availableBeds,
            OccupancyRate = occupancyRate,
            VacancyRate = vacancyRate,
            BuildingOccupancies = buildingOccupancies,
            MonthlyExpenseSummaries = monthlyExpenseSummaries,
            PendingRepairOrders = pendingRepairOrders,
            TodayVisitors = todayVisitors,
            TotalBuildings = totalBuildings,
            TotalRooms = totalRooms,
            PendingExpenses = pendingExpenses
        };
    }

    private async Task<List<BuildingOccupancyDto>> GetBuildingOccupanciesAsync()
    {
        // 一次性取楼栋/房间/床位，内存聚合，避免 N+1
        var buildings = await _buildingRepository.Query()
            .Where(b => b.FStatus == DorStatus.Enable.Enabled)
            .Select(b => new { b.FID, b.FName })
            .ToListAsync();
        if (buildings.Count == 0) return new List<BuildingOccupancyDto>();

        var buildingIds = buildings.Select(b => b.FID).ToList();
        var rooms = await _roomRepository.Query()
            .Where(r => buildingIds.Contains(r.FBuildingId))
            .Select(r => new { r.FID, r.FBuildingId })
            .ToListAsync();
        var roomToBuilding = rooms.ToDictionary(r => r.FID, r => r.FBuildingId);
        var roomIds = rooms.Select(r => r.FID).ToList();

        var beds = await _bedRepository.Query()
            .Where(b => roomIds.Contains(b.FRoomId))
            .Select(b => new { b.FRoomId, b.FStatus })
            .ToListAsync();

        var totalByBuilding = new Dictionary<long, int>();
        var occupiedByBuilding = new Dictionary<long, int>();
        foreach (var bed in beds)
        {
            if (!roomToBuilding.TryGetValue(bed.FRoomId, out var bId)) continue;
            totalByBuilding[bId] = totalByBuilding.GetValueOrDefault(bId) + 1;
            if (bed.FStatus == DorStatus.Bed.Occupied)
                occupiedByBuilding[bId] = occupiedByBuilding.GetValueOrDefault(bId) + 1;
        }

        return buildings.Select(b =>
        {
            var total = totalByBuilding.GetValueOrDefault(b.FID);
            var occupied = occupiedByBuilding.GetValueOrDefault(b.FID);
            return new BuildingOccupancyDto
            {
                BuildingId = b.FID,
                BuildingName = b.FName,
                TotalBeds = total,
                OccupiedBeds = occupied,
                AvailableBeds = total - occupied,
                OccupancyRate = total > 0 ? Math.Round((decimal)occupied / total * 100, 2) : 0
            };
        }).ToList();
    }

    private async Task<List<MonthlyExpenseSummaryDto>> GetMonthlyExpenseSummariesAsync(string month)
    {
        var summary = await _expenseRepository.Query()
            .Where(e => e.FMonth == month)
            .GroupBy(e => e.FExpenseType)
            .Select(g => new MonthlyExpenseSummaryDto
            {
                Month = month,
                ExpenseType = g.Key,
                TotalAmount = g.Sum(e => e.FAmount),
                Count = g.Count()
            })
            .ToListAsync();

        return summary;
    }
}
