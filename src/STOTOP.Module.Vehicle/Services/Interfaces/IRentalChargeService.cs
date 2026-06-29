using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;

namespace STOTOP.Module.Vehicle.Services.Interfaces;

public interface IRentalChargeService
{
    Task<PagedResult<RentalChargeListItemDto>> GetChargesAsync(RentalChargeQueryRequest request);
    Task<RentalChargeDto?> GetChargeByIdAsync(long id);
    Task<int> GenerateChargesAsync(GenerateChargesRequest request);  // 返回生成的账单数量
    Task<RentalChargeDto?> ConfirmChargeAsync(long id, ConfirmChargeRequest request);
    Task<RentalChargeDto?> WaiveChargeAsync(long id, WaiveChargeRequest request);
}
