using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.Dormitory.Services;

public class ResidenceService : IResidenceService
{
    private readonly IRepository<DorResidence> _residenceRepository;
    private readonly IRepository<DorBed> _bedRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;
    private readonly IRepository<HrEmployee> _employeeRepository;

    public ResidenceService(
        IRepository<DorResidence> residenceRepository,
        IRepository<DorBed> bedRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository,
        IRepository<HrEmployee> employeeRepository)
    {
        _residenceRepository = residenceRepository;
        _bedRepository = bedRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResult<ResidenceListItemDto>> GetResidencesAsync(ResidenceQueryRequest request)
    {
        var query = from r in _residenceRepository.Query()
                    join b in _bedRepository.Query() on r.FBedId equals b.FID
                    join rm in _roomRepository.Query() on b.FRoomId equals rm.FID
                    join bd in _buildingRepository.Query() on rm.FBuildingId equals bd.FID
                    join emp in _employeeRepository.Query() on r.FEmployeeId equals emp.FID into empGroup
                    from emp in empGroup.DefaultIfEmpty()
                    select new { Residence = r, Bed = b, Room = rm, Building = bd, Employee = emp };

        if (request.BuildingId.HasValue)
        {
            query = query.Where(x => x.Room.FBuildingId == request.BuildingId.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(x => x.Room.FID == request.RoomId.Value);
        }

        if (request.BedId.HasValue)
        {
            query = query.Where(x => x.Residence.FBedId == request.BedId.Value);
        }

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(x => x.Residence.FEmployeeId == request.EmployeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            if (long.TryParse(keyword, out var empId))
            {
                query = query.Where(x => x.Residence.FEmployeeId == empId ||
                    (x.Employee != null && x.Employee.FName.Contains(keyword)));
            }
            else
            {
                query = query.Where(x => x.Employee != null && x.Employee.FName.Contains(keyword));
            }
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Residence.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.Residence.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ResidenceListItemDto>
        {
            Items = items.Select(x => MapToListItemDto(x.Residence, x.Bed, x.Room, x.Building, x.Employee)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ResidenceDto?> GetResidenceByIdAsync(long id)
    {
        var result = await (from r in _residenceRepository.Query()
                           join b in _bedRepository.Query() on r.FBedId equals b.FID
                           join rm in _roomRepository.Query() on b.FRoomId equals rm.FID
                           join bd in _buildingRepository.Query() on rm.FBuildingId equals bd.FID
                           where r.FID == id
                           select new { Residence = r, Bed = b, Room = rm, Building = bd })
                           .FirstOrDefaultAsync();

        return result == null ? null : MapToDto(result.Residence, result.Bed, result.Room, result.Building);
    }

    public async Task<ResidenceDto> CreateResidenceAsync(CreateResidenceRequest request)
    {
        // 检查床位是否存在
        var bed = await _bedRepository.Query()
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.FID == request.BedId);

        if (bed == null)
        {
            throw new InvalidOperationException("床位不存在");
        }

        // 检查床位是否已被占用
        var isOccupied = await IsBedOccupiedAsync(request.BedId);
        if (isOccupied)
        {
            throw new InvalidOperationException("该床位已被占用");
        }

        var residence = new DorResidence
        {
            FBedId = request.BedId,
            FEmployeeId = request.EmployeeId,
            FCheckInDate = request.CheckInDate,
            FRemark = request.Remark,
            FStatus = DorStatus.Residence.CheckedIn,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _residenceRepository.AddAsync(residence);

        // 更新床位状态为已入住
        var bedToUpdate = await _bedRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == request.BedId);
        if (bedToUpdate != null)
        {
            bedToUpdate.FStatus = DorStatus.Bed.Occupied;
            bedToUpdate.FUpdatedTime = DateTime.Now;
            await _bedRepository.UpdateAsync(bedToUpdate);
        }

        return (await GetResidenceByIdAsync(residence.FID))!;
    }

    public async Task<ResidenceDto?> UpdateResidenceAsync(long id, UpdateResidenceRequest request)
    {
        var residence = await _residenceRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (residence == null) return null;

        residence.FRemark = request.Remark;
        residence.FUpdatedTime = DateTime.Now;

        await _residenceRepository.UpdateAsync(residence);
        return await GetResidenceByIdAsync(id);
    }

    public async Task<ResidenceDto?> CheckOutAsync(long id, CheckOutRequest request)
    {
        var residence = await _residenceRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (residence == null) return null;

        residence.FCheckOutDate = request.CheckOutDate;
        residence.FRemark = request.Remark;
        residence.FStatus = DorStatus.Residence.CheckedOut;
        residence.FUpdatedTime = DateTime.Now;

        await _residenceRepository.UpdateAsync(residence);

        // 更新床位状态为空闲
        var bedToUpdate = await _bedRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == residence.FBedId);
        if (bedToUpdate != null)
        {
            bedToUpdate.FStatus = DorStatus.Bed.Free;
            bedToUpdate.FUpdatedTime = DateTime.Now;
            await _bedRepository.UpdateAsync(bedToUpdate);
        }

        return await GetResidenceByIdAsync(id);
    }

    public async Task<bool> DeleteResidenceAsync(long id)
    {
        var residence = await _residenceRepository.GetByIdAsync(id);
        if (residence == null) return false;

        await _residenceRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> IsBedOccupiedAsync(long bedId)
    {
        return await _residenceRepository.Query()
            .AnyAsync(r => r.FBedId == bedId && r.FStatus == DorStatus.Residence.CheckedIn && r.FCheckOutDate == null);
    }

    #region Mapping

    private static ResidenceDto MapToDto(DorResidence entity, DorBed bed, DorRoom room, DorBuilding building)
    {
        return new ResidenceDto
        {
            Id = entity.FID,
            BedId = entity.FBedId,
            BedNumber = bed.FBedNumber,
            RoomId = room.FID,
            RoomNumber = room.FRoomNumber,
            BuildingId = building.FID,
            BuildingName = building.FName,
            EmployeeId = entity.FEmployeeId,
            CheckInDate = entity.FCheckInDate,
            CheckOutDate = entity.FCheckOutDate,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static ResidenceListItemDto MapToListItemDto(DorResidence entity, DorBed bed, DorRoom room, DorBuilding building, HrEmployee? employee = null)
    {
        return new ResidenceListItemDto
        {
            Id = entity.FID,
            BedId = entity.FBedId,
            BedNumber = bed.FBedNumber,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            EmployeeId = entity.FEmployeeId,
            EmployeeName = employee?.FName,
            CheckInDate = entity.FCheckInDate,
            CheckOutDate = entity.FCheckOutDate,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
