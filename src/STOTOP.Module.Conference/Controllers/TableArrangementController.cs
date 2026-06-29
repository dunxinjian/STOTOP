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
public class TableArrangementController : ControllerBase
{
    private readonly ITableArrangementService _tableService;

    public TableArrangementController(ITableArrangementService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet("meals/{mealId}/tables")]
    [RequirePermission(ConferencePermissions.TableView)]
    public async Task<ApiResult<List<TableDto>>> GetTables(int mealId)
    {
        var result = await _tableService.GetTablesAsync(mealId);
        return ApiResult<List<TableDto>>.Success(result);
    }

    [HttpPost("meals/{mealId}/tables")]
    [RequirePermission(ConferencePermissions.TableEdit)]
    public async Task<ApiResult<TableDto>> CreateTable(int mealId, [FromBody] CreateTableRequest request)
    {
        try
        {
            var result = await _tableService.CreateTableAsync(mealId, request);
            return ApiResult<TableDto>.Success(result, "创建桌次成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<TableDto>.Fail(ex.Message);
        }
    }

    [HttpPut("tables/{id}")]
    [RequirePermission(ConferencePermissions.TableEdit)]
    public async Task<ApiResult<TableDto>> UpdateTable(int id, [FromBody] UpdateTableRequest request)
    {
        try
        {
            var result = await _tableService.UpdateTableAsync(id, request);
            if (result == null)
                return ApiResult<TableDto>.Fail("桌次不存在");
            return ApiResult<TableDto>.Success(result, "更新桌次成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<TableDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("tables/{id}")]
    [RequirePermission(ConferencePermissions.TableEdit)]
    public async Task<ApiResult> DeleteTable(int id)
    {
        try
        {
            var result = await _tableService.DeleteTableAsync(id);
            if (!result)
                return ApiResult.Fail("桌次不存在");
            return ApiResult.Ok("删除桌次成功");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"删除桌次失败：{ex.Message}");
        }
    }

    [HttpPut("tables/{id}/seats")]
    [RequirePermission(ConferencePermissions.TableEdit)]
    public async Task<ApiResult<TableDto>> SetTableSeats(int id, [FromBody] TableSeatRequest request)
    {
        var result = await _tableService.SetTableSeatsAsync(id, request);
        if (result == null)
            return ApiResult<TableDto>.Fail("桌次不存在");
        return ApiResult<TableDto>.Success(result, "设置座位成功");
    }

    [HttpPost("meals/{mealId}/tables/auto-arrange")]
    [RequirePermission(ConferencePermissions.TableEdit)]
    public async Task<ApiResult<AutoArrangePreviewDto>> AutoArrange(int mealId, [FromBody] AutoArrangeConfigRequest request)
    {
        var result = await _tableService.AutoArrangeAsync(mealId, request);
        return ApiResult<AutoArrangePreviewDto>.Success(result);
    }

    [HttpGet("meals/{mealId}/tables/export-image")]
    [RequirePermission(ConferencePermissions.TableView)]
    public async Task<IActionResult> ExportImage(int mealId)
    {
        var bytes = await _tableService.ExportImageAsync(mealId);
        return File(bytes, "image/png", "桌次编排.png");
    }

    [HttpGet("meals/{mealId}/tables/export-pdf")]
    [RequirePermission(ConferencePermissions.TableView)]
    public async Task<IActionResult> ExportPdf(int mealId)
    {
        var bytes = await _tableService.ExportPdfAsync(mealId);
        return File(bytes, "application/pdf", "桌次编排.pdf");
    }

    [HttpGet("meals/{mealId}/tables/export-excel")]
    [RequirePermission(ConferencePermissions.TableView)]
    public async Task<IActionResult> ExportExcel(int mealId)
    {
        var bytes = await _tableService.ExportExcelAsync(mealId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "桌次编排.xlsx");
    }
}
