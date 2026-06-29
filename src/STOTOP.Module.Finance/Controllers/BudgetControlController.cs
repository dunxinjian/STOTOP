using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/budget-control")]
public class BudgetControlController : ControllerBase
{
    private readonly IBudgetOccupationService _budgetOccupationService;

    public BudgetControlController(IBudgetOccupationService budgetOccupationService)
    {
        _budgetOccupationService = budgetOccupationService;
    }

    [HttpPost("preview")]
    public async Task<ApiResult<BudgetPreviewResult>> Preview([FromBody] BudgetPreviewRequest request)
    {
        try
        {
            var result = await _budgetOccupationService.PreviewAsync(request);
            return ApiResult<BudgetPreviewResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BudgetPreviewResult>.Fail(ex.Message);
        }
    }
}
