using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/feedback")]
public class ServiceFeedbackController : ControllerBase
{
    private readonly IServiceFeedbackService _feedbackService;

    public ServiceFeedbackController(IServiceFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.FeedbackView)]
    public async Task<ApiResult<PagedResult<ServiceFeedbackListItemDto>>> GetList([FromQuery] ServiceFeedbackQueryRequest request)
    {
        var result = await _feedbackService.GetFeedbacksAsync(request);
        return ApiResult<PagedResult<ServiceFeedbackListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.FeedbackView)]
    public async Task<ApiResult<ServiceFeedbackDto>> GetById(long id)
    {
        var result = await _feedbackService.GetFeedbackByIdAsync(id);
        if (result == null)
            return ApiResult<ServiceFeedbackDto>.Fail("反馈不存在");
        return ApiResult<ServiceFeedbackDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.FeedbackCreate)]
    public async Task<ApiResult<ServiceFeedbackDto>> Create([FromBody] CreateServiceFeedbackRequest request)
    {
        try
        {
            var result = await _feedbackService.CreateFeedbackAsync(request);
            return ApiResult<ServiceFeedbackDto>.Success(result, "创建反馈成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ServiceFeedbackDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(CrmPermissions.FeedbackCreate)]
    public async Task<ApiResult<ServiceFeedbackDto>> Update(long id, [FromBody] UpdateServiceFeedbackRequest request)
    {
        try
        {
            var result = await _feedbackService.UpdateFeedbackAsync(id, request);
            if (result == null)
                return ApiResult<ServiceFeedbackDto>.Fail("反馈不存在");
            return ApiResult<ServiceFeedbackDto>.Success(result, "更新反馈成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ServiceFeedbackDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.FeedbackCreate)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _feedbackService.DeleteFeedbackAsync(id);
        if (!result)
            return ApiResult.Fail("反馈不存在");
        return ApiResult.Ok("删除反馈成功");
    }

    [HttpPost("{id}/handle")]
    [RequirePermission(CrmPermissions.FeedbackHandle)]
    public async Task<ApiResult> Handle(long id, [FromBody] HandleFeedbackRequest request)
    {
        try
        {
            var result = await _feedbackService.HandleFeedbackAsync(id, request);
            if (!result)
                return ApiResult.Fail("反馈不存在");
            return ApiResult.Ok("处理成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
