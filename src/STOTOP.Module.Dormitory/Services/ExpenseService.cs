using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.Dormitory.Services;

public class ExpenseService : IExpenseService
{
    private readonly IRepository<DorExpense> _expenseRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;
    private readonly IRepository<DorResidence> _residenceRepository;
    private readonly IRepository<DorBed> _bedRepository;
    private readonly IRepository<HrEmployee> _employeeRepository;

    public ExpenseService(
        IRepository<DorExpense> expenseRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository,
        IRepository<DorResidence> residenceRepository,
        IRepository<DorBed> bedRepository,
        IRepository<HrEmployee> employeeRepository)
    {
        _expenseRepository = expenseRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
        _residenceRepository = residenceRepository;
        _bedRepository = bedRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResult<ExpenseListItemDto>> GetExpensesAsync(ExpenseQueryRequest request)
    {
        var query = from e in _expenseRepository.Query()
                    join r in _roomRepository.Query() on e.FRoomId equals r.FID
                    join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                    select new { Expense = e, Room = r, Building = b };

        if (request.BuildingId.HasValue)
        {
            query = query.Where(x => x.Room.FBuildingId == request.BuildingId.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(x => x.Expense.FRoomId == request.RoomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Month))
        {
            query = query.Where(x => x.Expense.FMonth == request.Month);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpenseType))
        {
            query = query.Where(x => x.Expense.FExpenseType == request.ExpenseType);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.Expense.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ExpenseListItemDto>
        {
            Items = items.Select(x => MapToListItemDto(x.Expense, x.Room, x.Building)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ExpenseDto?> GetExpenseByIdAsync(long id)
    {
        var result = await (from e in _expenseRepository.Query()
                           join r in _roomRepository.Query() on e.FRoomId equals r.FID
                           join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                           where e.FID == id
                           select new { Expense = e, Room = r, Building = b })
                           .FirstOrDefaultAsync();

        return result == null ? null : MapToDto(result.Expense, result.Room, result.Building);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request)
    {
        // 检查房间是否存在
        var room = await _roomRepository.Query()
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.FID == request.RoomId);

        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        if (request.Amount < 0)
        {
            throw new InvalidOperationException("费用金额不能为负");
        }

        var expense = new DorExpense
        {
            FRoomId = request.RoomId,
            FExpenseType = request.ExpenseType,
            FAmount = request.Amount,
            FMonth = request.Month,
            FShareMethod = request.ShareMethod,
            FRemark = request.Remark,
            FStatus = DorStatus.Expense.Unpaid, // 待缴
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _expenseRepository.AddAsync(expense);
        return (await GetExpenseByIdAsync(expense.FID))!;
    }

    public async Task<ExpenseDto?> UpdateExpenseAsync(long id, UpdateExpenseRequest request)
    {
        var expense = await _expenseRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (expense == null) return null;

        expense.FExpenseType = request.ExpenseType;
        expense.FAmount = request.Amount;
        expense.FMonth = request.Month;
        expense.FShareMethod = request.ShareMethod;
        expense.FRemark = request.Remark;
        if (request.Status.HasValue) expense.FStatus = request.Status.Value; // 缴费状态可选更新，不传则保持
        expense.FUpdatedTime = DateTime.Now;

        await _expenseRepository.UpdateAsync(expense);
        return await GetExpenseByIdAsync(id);
    }

    public async Task<bool> DeleteExpenseAsync(long id)
    {
        var expense = await _expenseRepository.GetByIdAsync(id);
        if (expense == null) return false;

        await _expenseRepository.DeleteAsync(id);
        return true;
    }

    public async Task<List<MonthlyExpenseSummaryDto>> GetMonthlySummaryAsync(string? month)
    {
        var query = _expenseRepository.Query();

        if (!string.IsNullOrWhiteSpace(month))
        {
            query = query.Where(e => e.FMonth == month);
        }

        var summary = await query
            .GroupBy(e => new { e.FMonth, e.FExpenseType })
            .Select(g => new MonthlyExpenseSummaryDto
            {
                Month = g.Key.FMonth,
                ExpenseType = g.Key.FExpenseType,
                TotalAmount = g.Sum(e => e.FAmount),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Month)
            .ThenBy(s => s.ExpenseType)
            .ToListAsync();

        return summary;
    }

    public async Task<ExpenseAllocationDto?> GetExpenseAllocationAsync(long expenseId)
    {
        var info = await (from e in _expenseRepository.Query()
                          join r in _roomRepository.Query() on e.FRoomId equals r.FID
                          where e.FID == expenseId
                          select new { Expense = e, Room = r })
                         .FirstOrDefaultAsync();
        if (info == null) return null;

        // 当前在住人：该房间内床位上、状态=入住中、未退宿的入住记录（带员工姓名）
        var residents = await (from res in _residenceRepository.Query()
                               join b in _bedRepository.Query() on res.FBedId equals b.FID
                               where b.FRoomId == info.Room.FID && res.FStatus == 1 && res.FCheckOutDate == null
                               join emp in _employeeRepository.Query() on res.FEmployeeId equals emp.FID into eg
                               from emp in eg.DefaultIfEmpty()
                               orderby res.FID
                               select new { res.FEmployeeId, EmployeeName = emp != null ? emp.FName : null })
                              .ToListAsync();

        var dto = new ExpenseAllocationDto
        {
            ExpenseId = info.Expense.FID,
            RoomId = info.Room.FID,
            RoomNumber = info.Room.FRoomNumber,
            ShareMethod = info.Expense.FShareMethod,
            ExpenseAmount = info.Expense.FAmount,
            OccupantCount = residents.Count
        };

        if (residents.Count == 0) return dto; // 无在住人：明细空、合计 0

        var isFixed = string.Equals(info.Expense.FShareMethod, "Fixed", StringComparison.OrdinalIgnoreCase)
                      || info.Expense.FShareMethod == "固定";

        if (isFixed)
        {
            // 固定：每人按费用金额固定收取
            foreach (var p in residents)
                dto.Shares.Add(new ExpenseShareDto { EmployeeId = p.FEmployeeId, EmployeeName = p.EmployeeName, Amount = info.Expense.FAmount });
        }
        else
        {
            // 均摊：总额按人数等分，分位余数归最后一人，保证合计严格等于总额
            var n = residents.Count;
            var per = Math.Round(info.Expense.FAmount / n, 2, MidpointRounding.AwayFromZero);
            for (var i = 0; i < n; i++)
            {
                var amount = i < n - 1 ? per : info.Expense.FAmount - per * (n - 1);
                dto.Shares.Add(new ExpenseShareDto { EmployeeId = residents[i].FEmployeeId, EmployeeName = residents[i].EmployeeName, Amount = amount });
            }
        }

        dto.AllocatedTotal = dto.Shares.Sum(s => s.Amount);
        return dto;
    }

    #region Mapping

    private static ExpenseDto MapToDto(DorExpense entity, DorRoom room, DorBuilding building)
    {
        return new ExpenseDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            ExpenseType = entity.FExpenseType,
            Amount = entity.FAmount,
            Month = entity.FMonth,
            ShareMethod = entity.FShareMethod,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static ExpenseListItemDto MapToListItemDto(DorExpense entity, DorRoom room, DorBuilding building)
    {
        return new ExpenseListItemDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            ExpenseType = entity.FExpenseType,
            Amount = entity.FAmount,
            Month = entity.FMonth,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
