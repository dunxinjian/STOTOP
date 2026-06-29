namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 宿舍统计数据 DTO
/// </summary>
public class DormitoryStatisticsDto
{
    /// <summary>
    /// 总床位数
    /// </summary>
    public int TotalBeds { get; set; }

    /// <summary>
    /// 已入住床位数
    /// </summary>
    public int OccupiedBeds { get; set; }

    /// <summary>
    /// 空闲床位数
    /// </summary>
    public int AvailableBeds { get; set; }

    /// <summary>
    /// 入住率
    /// </summary>
    public decimal OccupancyRate { get; set; }

    /// <summary>
    /// 空置率
    /// </summary>
    public decimal VacancyRate { get; set; }

    /// <summary>
    /// 各楼栋入住统计
    /// </summary>
    public List<BuildingOccupancyDto> BuildingOccupancies { get; set; } = new();

    /// <summary>
    /// 本月费用汇总
    /// </summary>
    public List<MonthlyExpenseSummaryDto> MonthlyExpenseSummaries { get; set; } = new();

    /// <summary>
    /// 待处理报修工单数
    /// </summary>
    public int PendingRepairOrders { get; set; }

    /// <summary>
    /// 今日访客数
    /// </summary>
    public int TodayVisitors { get; set; }

    /// <summary>
    /// 总楼栋数
    /// </summary>
    public int TotalBuildings { get; set; }

    /// <summary>
    /// 总房间数
    /// </summary>
    public int TotalRooms { get; set; }

    /// <summary>
    /// 待缴费用总额
    /// </summary>
    public decimal PendingExpenses { get; set; }
}

/// <summary>
/// 楼栋入住统计 DTO
/// </summary>
public class BuildingOccupancyDto
{
    public long BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int AvailableBeds { get; set; }
    public decimal OccupancyRate { get; set; }
}
