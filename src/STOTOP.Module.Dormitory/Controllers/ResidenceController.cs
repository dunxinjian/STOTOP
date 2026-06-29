using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/residences")]
public class ResidenceController : ControllerBase
{
    private readonly IResidenceService _residenceService;

    public ResidenceController(IResidenceService residenceService)
    {
        _residenceService = residenceService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<ResidenceListItemDto>>> GetList([FromQuery] ResidenceQueryRequest request)
    {
        var result = await _residenceService.GetResidencesAsync(request);
        return ApiResult<PagedResult<ResidenceListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<ResidenceDto>> GetById(long id)
    {
        var result = await _residenceService.GetResidenceByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ResidenceDto>.Fail("入住记录不存在");
        }
        return ApiResult<ResidenceDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<ResidenceDto>> Create([FromBody] CreateResidenceRequest request)
    {
        try
        {
            var result = await _residenceService.CreateResidenceAsync(request);
            return ApiResult<ResidenceDto>.Success(result, "创建入住记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ResidenceDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<ResidenceDto>> Update(long id, [FromBody] UpdateResidenceRequest request)
    {
        var result = await _residenceService.UpdateResidenceAsync(id, request);
        if (result == null)
        {
            return ApiResult<ResidenceDto>.Fail("入住记录不存在");
        }
        return ApiResult<ResidenceDto>.Success(result, "更新入住记录成功");
    }

    [HttpPut("{id}/checkout")]
    public async Task<ApiResult<ResidenceDto>> CheckOut(long id, [FromBody] CheckOutRequest request)
    {
        var result = await _residenceService.CheckOutAsync(id, request);
        if (result == null)
        {
            return ApiResult<ResidenceDto>.Fail("入住记录不存在");
        }
        return ApiResult<ResidenceDto>.Success(result, "退宿成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _residenceService.DeleteResidenceAsync(id);
        if (!result)
        {
            return ApiResult.Fail("入住记录不存在");
        }
        return ApiResult.Ok("删除入住记录成功");
    }

    [HttpGet("bed/{bedId}/occupied")]
    public async Task<ApiResult<bool>> IsBedOccupied(long bedId)
    {
        var result = await _residenceService.IsBedOccupiedAsync(bedId);
        return ApiResult<bool>.Success(result);
    }
}
