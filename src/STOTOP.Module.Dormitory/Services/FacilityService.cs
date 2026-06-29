using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class FacilityService : IFacilityService
{
    private readonly IRepository<DorFacility> _facilityRepository;
    private readonly IRepository<DorRoom> _roomRepository;

    public FacilityService(
        IRepository<DorFacility> facilityRepository,
        IRepository<DorRoom> roomRepository)
    {
        _facilityRepository = facilityRepository;
        _roomRepository = roomRepository;
    }

    public async Task<List<FacilityDto>> GetFacilitiesByRoomIdAsync(long roomId)
    {
        var facilities = await _facilityRepository.Query()
            .Where(f => f.FRoomId == roomId)
            .OrderByDescending(f => f.FCreatedTime)
            .ToListAsync();

        return facilities.Select(MapToDto).ToList();
    }

    public async Task<FacilityDto?> GetFacilityByIdAsync(long id)
    {
        var facility = await _facilityRepository.GetByIdAsync(id);
        return facility == null ? null : MapToDto(facility);
    }

    public async Task<FacilityDto> CreateFacilityAsync(long roomId, CreateFacilityRequest request)
    {
        // 检查房间是否存在
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        var facility = new DorFacility
        {
            FRoomId = roomId,
            FFacilityName = request.FacilityName,
            FQuantity = request.Quantity,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _facilityRepository.AddAsync(facility);
        return MapToDto(facility);
    }

    public async Task<FacilityDto?> UpdateFacilityAsync(long id, UpdateFacilityRequest request)
    {
        var facility = await _facilityRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(f => f.FID == id);

        if (facility == null) return null;

        facility.FFacilityName = request.FacilityName;
        facility.FQuantity = request.Quantity;
        facility.FRemark = request.Remark;
        facility.FStatus = request.Status;
        facility.FUpdatedTime = DateTime.Now;

        await _facilityRepository.UpdateAsync(facility);
        return MapToDto(facility);
    }

    public async Task<bool> DeleteFacilityAsync(long id)
    {
        var facility = await _facilityRepository.GetByIdAsync(id);
        if (facility == null) return false;

        await _facilityRepository.DeleteAsync(id);
        return true;
    }

    #region Mapping

    private static FacilityDto MapToDto(DorFacility entity)
    {
        return new FacilityDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            FacilityName = entity.FFacilityName,
            Quantity = entity.FQuantity,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
