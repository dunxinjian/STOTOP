using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Filters;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/auxiliary-aliases")]
public class AuxiliaryAliasController : ControllerBase
{
    private readonly AuxiliaryAliasService _aliasService;

    public AuxiliaryAliasController(AuxiliaryAliasService aliasService)
    {
        _aliasService = aliasService;
    }

    [HttpGet]
    [RequireAccountSetPermission(AccountSetPermissions.AuxiliaryView)]
    public async Task<ApiResult<List<AuxiliaryAliasDto>>> GetAll([FromQuery] string? auxType,
        [FromHeader(Name = "X-AccountSet-Id")] long accountSetId = 0)
    {
        var result = await _aliasService.GetAllAsync(auxType, accountSetId);
        return ApiResult<List<AuxiliaryAliasDto>>.Success(result);
    }

    [HttpPost]
    [RequireAccountSetPermission(AccountSetPermissions.AuxiliaryEdit)]
    public async Task<ApiResult<AuxiliaryAliasDto>> Create([FromBody] AuxiliaryAliasDto dto,
        [FromHeader(Name = "X-AccountSet-Id")] long accountSetId = 0)
    {
        try
        {
            var result = await _aliasService.CreateAsync(dto, accountSetId);
            return ApiResult<AuxiliaryAliasDto>.Success(result!, "创建成功");
        }
        catch (Exception ex)
        {
            return ApiResult<AuxiliaryAliasDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.AuxiliaryEdit)]
    public async Task<ApiResult<AuxiliaryAliasDto>> Update(Guid id, [FromBody] AuxiliaryAliasDto dto,
        [FromHeader(Name = "X-AccountSet-Id")] long accountSetId = 0)
    {
        try
        {
            var result = await _aliasService.UpdateAsync(id, dto, accountSetId);
            if (result == null)
            {
                return ApiResult<AuxiliaryAliasDto>.Fail("记录不存在");
            }
            return ApiResult<AuxiliaryAliasDto>.Success(result, "更新成功");
        }
        catch (Exception ex)
        {
            return ApiResult<AuxiliaryAliasDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequireAccountSetPermission(AccountSetPermissions.AuxiliaryEdit)]
    public async Task<ApiResult> Delete(Guid id,
        [FromHeader(Name = "X-AccountSet-Id")] long accountSetId = 0)
    {
        try
        {
            var result = await _aliasService.DeleteAsync(id, accountSetId);
            if (!result)
            {
                return ApiResult.Fail("记录不存在");
            }
            return ApiResult.Ok("删除成功");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
