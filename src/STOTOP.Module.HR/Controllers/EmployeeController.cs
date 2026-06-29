using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.HR.Dtos;
using STOTOP.Module.HR.Entities;
using STOTOP.Module.HR.Services;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.HR.Controllers;

[Authorize]
[ApiController]
[Route("api/hr/employees")]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _service;
    private readonly STOTOPDbContext _db;

    public EmployeeController(IEmployeeService service, STOTOPDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>分页查询员工列表</summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<EmployeeDto>>> GetPagedList([FromQuery] EmployeePagedRequest request)
    {
        return await _service.GetPagedListAsync(request);
    }

    /// <summary>获取员工详情</summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<EmployeeDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    /// <summary>创建员工</summary>
    [HttpPost]
    public async Task<ApiResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest request)
    {
        return await _service.CreateAsync(request);
    }

    /// <summary>更新员工</summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<EmployeeDto>> Update(long id, [FromBody] UpdateEmployeeRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>删除员工</summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>根据用户ID查询员工</summary>
    [HttpGet("by-user/{userId}")]
    public async Task<ApiResult<EmployeeDto?>> GetByUserId(long userId)
    {
        return await _service.GetByUserIdAsync(userId);
    }

    /// <summary>搜索可关联的用户</summary>
    [HttpGet("search-users")]
    public async Task<ApiResult<List<UserSimpleDto>>> SearchAvailableUsers([FromQuery] string? keyword)
    {
        return await _service.SearchAvailableUsersAsync(keyword);
    }

    /// <summary>获取全部在职员工（扁平列表，供辅助核算关联使用）</summary>
    [HttpGet("all-enabled")]
    public async Task<ApiResult<List<object>>> GetAllEnabled()
    {
        var employees = await _db.Set<HrEmployee>()
            .Where(e => e.FEmployeeStatus == 1)
            .OrderBy(e => e.FName)
            .ToListAsync();

        var userIds = employees.Select(e => e.FUserId).Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FAccount })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID);

        var items = employees.Select(e =>
        {
            userDict.TryGetValue(e.FUserId, out var user);
            return (object)new
            {
                id = e.FID,
                code = user?.FAccount ?? e.FUserId.ToString(),
                name = e.FName,
                phone = e.FPhone,
                status = e.FEmployeeStatus
            };
        }).ToList();

        return ApiResult<List<object>>.Success(items);
    }
}
