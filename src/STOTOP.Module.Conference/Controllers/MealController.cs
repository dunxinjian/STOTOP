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
public class MealController : ControllerBase
{
    private readonly IMealService _mealService;

    public MealController(IMealService mealService)
    {
        _mealService = mealService;
    }

    [HttpGet("events/{eventId}/meals")]
    [RequirePermission(ConferencePermissions.MealView)]
    public async Task<ApiResult<List<MealPlanListItemDto>>> GetMealPlans(int eventId)
    {
        var result = await _mealService.GetMealPlansAsync(eventId);
        return ApiResult<List<MealPlanListItemDto>>.Success(result);
    }

    [HttpPost("events/{eventId}/meals")]
    [RequirePermission(ConferencePermissions.MealCreate)]
    public async Task<ApiResult<MealPlanDto>> CreateMealPlan(int eventId, [FromBody] CreateMealPlanRequest request)
    {
        try
        {
            var result = await _mealService.CreateMealPlanAsync(eventId, request);
            return ApiResult<MealPlanDto>.Success(result, "创建餐食计划成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<MealPlanDto>.Fail(ex.Message);
        }
    }

    [HttpPut("meals/{id}")]
    [RequirePermission(ConferencePermissions.MealEdit)]
    public async Task<ApiResult<MealPlanDto>> UpdateMealPlan(int id, [FromBody] UpdateMealPlanRequest request)
    {
        try
        {
            var result = await _mealService.UpdateMealPlanAsync(id, request);
            if (result == null)
                return ApiResult<MealPlanDto>.Fail("餐食计划不存在");
            return ApiResult<MealPlanDto>.Success(result, "更新餐食计划成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<MealPlanDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("meals/{id}")]
    [RequirePermission(ConferencePermissions.MealDelete)]
    public async Task<ApiResult> DeleteMealPlan(int id)
    {
        var result = await _mealService.DeleteMealPlanAsync(id);
        if (!result)
            return ApiResult.Fail("餐食计划不存在");
        return ApiResult.Ok("删除餐食计划成功");
    }

    [HttpPut("meals/{id}/attendees")]
    [RequirePermission(ConferencePermissions.MealEdit)]
    public async Task<ApiResult<MealPlanDto>> SetMealAttendees(int id, [FromBody] MealAttendeeRequest request)
    {
        var result = await _mealService.SetMealAttendeesAsync(id, request);
        if (result == null)
            return ApiResult<MealPlanDto>.Fail("餐食计划不存在");
        return ApiResult<MealPlanDto>.Success(result, "设置用餐人员成功");
    }

    [HttpPost("events/{eventId}/meals/auto-generate")]
    [RequirePermission(ConferencePermissions.MealEdit)]
    public async Task<ApiResult<List<MealPlanListItemDto>>> AutoGenerateMealPlans(int eventId)
    {
        var result = await _mealService.AutoGenerateMealPlansAsync(eventId);
        return ApiResult<List<MealPlanListItemDto>>.Success(result, "自动生成餐食计划成功");
    }
}
