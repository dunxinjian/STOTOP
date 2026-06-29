using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IRepairOrderService
{
    Task<PagedResult<RepairOrderListItemDto>> GetRepairOrdersAsync(RepairOrderQueryRequest request);
    Task<RepairOrderDto?> GetRepairOrderByIdAsync(long id);
    Task<RepairOrderDto> CreateRepairOrderAsync(CreateRepairOrderRequest request);
    Task<RepairOrderDto?> UpdateRepairOrderAsync(long id, UpdateRepairOrderRequest request);
    Task<RepairOrderDto?> HandleRepairOrderAsync(long id, HandleRepairOrderRequest request);
    Task<bool> DeleteRepairOrderAsync(long id);
}
