using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IRedeemService
{
    Task<ApiResult<PagedResult<RedeemItemListDto>>> GetItemsPagedAsync(long orgId, RedeemItemPagedRequest request);
    Task<ApiResult<RedeemItemDetailDto>> CreateItemAsync(long orgId, CreateRedeemItemRequest request);
    Task<ApiResult<RedeemItemDetailDto>> UpdateItemAsync(long id, UpdateRedeemItemRequest request);
    Task<ApiResult<bool>> ToggleItemAsync(long id);
    Task<ApiResult<RedeemRecordListDto>> ExchangeAsync(long orgId, long userId, ExchangeRequest request);
    Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetRecordsPagedAsync(long orgId, RedeemRecordPagedRequest request);
    Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetMyRecordsAsync(long orgId, long userId, RedeemRecordPagedRequest request);
    Task<ApiResult<bool>> DeliverAsync(long id, long issuerId, DeliverRequest request);
    Task<ApiResult<bool>> CancelAsync(long id, long operatorId);
}
