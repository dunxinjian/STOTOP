using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/code-rules")]
[ApiController]
[Authorize]
public class CodeRuleController : ControllerBase
{
    private readonly ICodeRuleService _codeRuleService;

    public CodeRuleController(ICodeRuleService codeRuleService)
    {
        _codeRuleService = codeRuleService;
    }

    [HttpGet]
    public async Task<ApiResult<List<CodeRuleDto>>> GetAll()
    {
        return await _codeRuleService.GetAllRulesAsync();
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<CodeRuleDto>> GetById(long id)
    {
        return await _codeRuleService.GetRuleByIdAsync(id);
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<CodeRuleDto>> Update(long id, [FromBody] CodeRuleUpdateDto dto)
    {
        return await _codeRuleService.UpdateRuleAsync(id, dto);
    }

    [HttpGet("{id}/preview")]
    public async Task<ApiResult<string>> Preview(long id)
    {
        return await _codeRuleService.PreviewCodeAsync(id);
    }

    [HttpPost]
    public async Task<ApiResult<CodeRuleDto>> Create([FromBody] CodeRuleCreateDto dto)
    {
        return await _codeRuleService.CreateRuleAsync(dto);
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        return await _codeRuleService.DeleteRuleAsync(id);
    }
}
