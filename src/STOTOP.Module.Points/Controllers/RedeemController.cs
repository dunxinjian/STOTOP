using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Points.Controllers;

[Authorize]
[ApiController]
[Route("api/points/redeem")]
public class RedeemController : ControllerBase
{
    private readonly IRedeemService _service;

    public RedeemController(IRedeemService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>商品列表</summary>
    [HttpGet("items")]
    [RequirePermission(PointsPermissions.RedeemView)]
    public async Task<ApiResult<PagedResult<RedeemItemListDto>>> GetItems([FromQuery] RedeemItemPagedRequest request)
    {
        return await _service.GetItemsPagedAsync(GetOrgId(), request);
    }

    /// <summary>创建商品</summary>
    [HttpPost("items")]
    [RequirePermission(PointsPermissions.RedeemManage)]
    public async Task<ApiResult<RedeemItemDetailDto>> CreateItem([FromBody] CreateRedeemItemRequest request)
    {
        return await _service.CreateItemAsync(GetOrgId(), request);
    }

    /// <summary>更新商品</summary>
    [HttpPut("items/{id}")]
    [RequirePermission(PointsPermissions.RedeemManage)]
    public async Task<ApiResult<RedeemItemDetailDto>> UpdateItem(long id, [FromBody] UpdateRedeemItemRequest request)
    {
        return await _service.UpdateItemAsync(id, request);
    }

    /// <summary>上下架商品</summary>
    [HttpPut("items/{id}/toggle")]
    [RequirePermission(PointsPermissions.RedeemManage)]
    public async Task<ApiResult<bool>> ToggleItem(long id)
    {
        return await _service.ToggleItemAsync(id);
    }

    /// <summary>执行兑换</summary>
    [HttpPost("exchange")]
    [RequirePermission(PointsPermissions.RedeemExchange)]
    public async Task<ApiResult<RedeemRecordListDto>> Exchange([FromBody] ExchangeRequest request)
    {
        return await _service.ExchangeAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>兑换记录</summary>
    [HttpGet("records")]
    [RequirePermission(PointsPermissions.RedeemView)]
    public async Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetRecords([FromQuery] RedeemRecordPagedRequest request)
    {
        return await _service.GetRecordsPagedAsync(GetOrgId(), request);
    }

    /// <summary>我的兑换</summary>
    [HttpGet("records/my")]
    [RequirePermission(PointsPermissions.RedeemView)]
    public async Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetMyRecords([FromQuery] RedeemRecordPagedRequest request)
    {
        return await _service.GetMyRecordsAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>确认发放</summary>
    [HttpPut("records/{id}/deliver")]
    [RequirePermission(PointsPermissions.RedeemDeliver)]
    public async Task<ApiResult<bool>> Deliver(long id, [FromBody] DeliverRequest request)
    {
        return await _service.DeliverAsync(id, GetUserId(), request);
    }

    /// <summary>取消兑换</summary>
    [HttpPut("records/{id}/cancel")]
    [RequirePermission(PointsPermissions.RedeemManage)]
    public async Task<ApiResult<bool>> Cancel(long id)
    {
        return await _service.CancelAsync(id, GetUserId());
    }
}
