using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
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
    public async Task<ApiResult<List<AuxiliaryAliasDto>>> GetAll([FromQuery] string? auxType)
    {
        var result = await _aliasService.GetAllAsync(auxType);
        return ApiResult<List<AuxiliaryAliasDto>>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<AuxiliaryAliasDto>> Create([FromBody] AuxiliaryAliasDto dto)
    {
        try
        {
            var result = await _aliasService.CreateAsync(dto);
            return ApiResult<AuxiliaryAliasDto>.Success(result!, "创建成功");
        }
        catch (Exception ex)
        {
            return ApiResult<AuxiliaryAliasDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<AuxiliaryAliasDto>> Update(Guid id, [FromBody] AuxiliaryAliasDto dto)
    {
        try
        {
            var result = await _aliasService.UpdateAsync(id, dto);
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
    public async Task<ApiResult> Delete(Guid id)
    {
        try
        {
            var result = await _aliasService.DeleteAsync(id);
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
