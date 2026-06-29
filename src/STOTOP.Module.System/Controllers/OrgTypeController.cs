using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/org-types")]
[ApiController]
[Authorize]
public class OrgTypeController : ControllerBase
{
    private readonly IOrgTypeService _orgTypeService;

    public OrgTypeController(IOrgTypeService orgTypeService)
    {
        _orgTypeService = orgTypeService;
    }

    /// <summary>获取所有启用的组织类型</summary>
    [HttpGet]
    public async Task<ApiResult<List<OrgTypeDto>>> GetAll()
    {
        return await _orgTypeService.GetAllAsync();
    }

    /// <summary>获取可关联账套的组织类型（用于账套绑定下拉选择）</summary>
    [HttpGet("for-account-set")]
    public async Task<ApiResult<List<OrgTypeDto>>> GetForAccountSet()
    {
        return await _orgTypeService.GetForAccountSetAsync();
    }

    /// <summary>更新组织类型的图标、排序、说明等可编辑属性</summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<bool>> Update(long id, [FromBody] OrgTypeUpdateDto dto)
    {
        return await _orgTypeService.UpdateAsync(id, dto);
    }
}
