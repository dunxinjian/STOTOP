using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _materialService;

    public MaterialController(IMaterialService materialService)
    {
        _materialService = materialService;
    }

    [HttpGet("events/{eventId}/materials")]
    [RequirePermission(ConferencePermissions.MaterialView)]
    public async Task<ApiResult<PagedResult<MaterialListItemDto>>> GetMaterials(int eventId, [FromQuery] MaterialQueryRequest request)
    {
        var result = await _materialService.GetMaterialsAsync(eventId, request);
        return ApiResult<PagedResult<MaterialListItemDto>>.Success(result);
    }

    [HttpGet("materials/{id}")]
    [RequirePermission(ConferencePermissions.MaterialView)]
    public async Task<ApiResult<MaterialDto>> GetById(int id)
    {
        var result = await _materialService.GetMaterialByIdAsync(id);
        if (result == null)
            return ApiResult<MaterialDto>.Fail("物品不存在");
        return ApiResult<MaterialDto>.Success(result);
    }

    [HttpPost("events/{eventId}/materials")]
    [RequirePermission(ConferencePermissions.MaterialCreate)]
    public async Task<ApiResult<MaterialDto>> Create(int eventId, [FromBody] CreateMaterialRequest request)
    {
        try
        {
            var result = await _materialService.CreateMaterialAsync(eventId, request);
            return ApiResult<MaterialDto>.Success(result, "添加物品成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<MaterialDto>.Fail(ex.Message);
        }
    }

    [HttpPut("materials/{id}")]
    [RequirePermission(ConferencePermissions.MaterialEdit)]
    public async Task<ApiResult<MaterialDto>> Update(int id, [FromBody] UpdateMaterialRequest request)
    {
        try
        {
            var result = await _materialService.UpdateMaterialAsync(id, request);
            if (result == null)
                return ApiResult<MaterialDto>.Fail("物品不存在");
            return ApiResult<MaterialDto>.Success(result, "更新物品成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<MaterialDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("materials/{id}")]
    [RequirePermission(ConferencePermissions.MaterialDelete)]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _materialService.DeleteMaterialAsync(id);
        if (!result)
            return ApiResult.Fail("物品不存在");
        return ApiResult.Ok("删除物品成功");
    }

    [HttpPut("materials/{id}/receive")]
    [RequirePermission(ConferencePermissions.MaterialEdit)]
    public async Task<ApiResult<MaterialDto>> Receive(int id, [FromBody] MaterialReceiveRequest request)
    {
        var result = await _materialService.ReceiveMaterialAsync(id, request);
        if (result == null)
            return ApiResult<MaterialDto>.Fail("物品不存在");
        return ApiResult<MaterialDto>.Success(result, "领用成功");
    }

    [HttpPut("materials/{id}/return")]
    [RequirePermission(ConferencePermissions.MaterialEdit)]
    public async Task<ApiResult<MaterialDto>> Return(int id, [FromBody] MaterialReturnRequest request)
    {
        var result = await _materialService.ReturnMaterialAsync(id, request);
        if (result == null)
            return ApiResult<MaterialDto>.Fail("物品不存在");
        return ApiResult<MaterialDto>.Success(result, "归还成功");
    }

    [HttpGet("events/{eventId}/materials/summary")]
    [RequirePermission(ConferencePermissions.MaterialView)]
    public async Task<ApiResult<MaterialSummaryDto>> GetSummary(int eventId)
    {
        var result = await _materialService.GetSummaryAsync(eventId);
        return ApiResult<MaterialSummaryDto>.Success(result);
    }

    [HttpGet("events/{eventId}/materials/export")]
    [RequirePermission(ConferencePermissions.MaterialView)]
    public async Task<IActionResult> Export(int eventId)
    {
        var bytes = await _materialService.ExportMaterialsAsync(eventId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "物品清单.xlsx");
    }

    [HttpGet("events/{eventId}/materials/checklist")]
    [RequirePermission(ConferencePermissions.MaterialView)]
    public async Task<ApiResult<List<MaterialListItemDto>>> GetChecklist(int eventId)
    {
        var result = await _materialService.GetChecklistAsync(eventId);
        return ApiResult<List<MaterialListItemDto>>.Success(result);
    }
}
