using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.System.Controllers;

[ApiController]
[Route("api/system/db-connections")]
[Authorize]
public class DbConnectionController : ControllerBase
{
    private readonly IDbConnectionService _dbConnectionService;

    public DbConnectionController(IDbConnectionService dbConnectionService)
    {
        _dbConnectionService = dbConnectionService;
    }

    /// <summary>
    /// 检查系统数据库连接是否已配置（免认证）
    /// </summary>
    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<ApiResult<object>> GetStatus()
    {
        var hasSystemConnection = await _dbConnectionService.HasSystemConnectionAsync();
        return ApiResult<object>.Success(new { hasSystemConnection });
    }

    /// <summary>
    /// 获取数据库连接列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<List<DbConnectionDto>>> GetList()
    {
        var list = await _dbConnectionService.GetListAsync();
        return ApiResult<List<DbConnectionDto>>.Success(list);
    }

    /// <summary>
    /// 获取数据库连接详情
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResult<DbConnectionDto>> GetById(long id)
    {
        try
        {
            var dto = await _dbConnectionService.GetByIdAsync(id);
            return ApiResult<DbConnectionDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return ApiResult<DbConnectionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<DbConnectionDto>> Create([FromBody] DbConnectionCreateDto dto)
    {
        try
        {
            var result = await _dbConnectionService.CreateAsync(dto);
            return ApiResult<DbConnectionDto>.Success(result);
        }
        catch (Exception ex)
        {
            return ApiResult<DbConnectionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新数据库连接
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResult<DbConnectionDto>> Update(long id, [FromBody] DbConnectionUpdateDto dto)
    {
        try
        {
            var result = await _dbConnectionService.UpdateAsync(id, dto);
            return ApiResult<DbConnectionDto>.Success(result);
        }
        catch (Exception ex)
        {
            return ApiResult<DbConnectionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除数据库连接
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _dbConnectionService.DeleteAsync(id);
            return ApiResult.Ok("删除成功");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 测试数据库连接（不保存）
    /// </summary>
    [HttpPost("test")]
    public async Task<ApiResult<bool>> TestConnection([FromBody] DbConnectionTestDto dto)
    {
        try
        {
            var result = await _dbConnectionService.TestConnectionAsync(dto);
            return ApiResult<bool>.Success(result, "连接成功");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail($"连接失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取数据库连接下拉选项
    /// </summary>
    [HttpGet("options")]
    public async Task<ApiResult<List<DbConnectionOptionDto>>> GetOptions()
    {
        var options = await _dbConnectionService.GetOptionsAsync();
        return ApiResult<List<DbConnectionOptionDto>>.Success(options);
    }

    /// <summary>
    /// 获取指定连接的表列表
    /// </summary>
    [HttpGet("{id:long}/tables")]
    public async Task<IActionResult> GetTables(long id)
    {
        try
        {
            var tables = await _dbConnectionService.GetTablesAsync(id);
            return Ok(ApiResult<List<DbTableDto>>.Success(tables));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<List<DbTableDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取指定表的字段列表
    /// </summary>
    [HttpGet("{id:long}/tables/{tableName}/columns")]
    public async Task<IActionResult> GetColumns(long id, string tableName)
    {
        try
        {
            var columns = await _dbConnectionService.GetColumnsAsync(id, tableName);
            return Ok(ApiResult<List<DbColumnDto>>.Success(columns));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<List<DbColumnDto>>.Fail(ex.Message));
        }
    }
}
