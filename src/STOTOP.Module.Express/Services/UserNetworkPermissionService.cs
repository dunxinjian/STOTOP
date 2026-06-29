using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Services;

public class UserNetworkPermissionService : IUserNetworkPermissionService
{
    private readonly IRepository<SysUserNetworkPermission> _repository;

    public UserNetworkPermissionService(IRepository<SysUserNetworkPermission> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<UserNetworkPermissionDto>> GetListAsync(UserNetworkPermissionQueryRequest request)
    {
        var query = _repository.Query();

        if (request.UserId.HasValue)
            query = query.Where(e => e.FUserId == request.UserId.Value);

        if (!string.IsNullOrWhiteSpace(request.NetworkPointCode))
            query = query.Where(e => e.FNetworkPointCode == request.NetworkPointCode);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<UserNetworkPermissionDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<UserNetworkPermissionDto?> GetByIdAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<UserNetworkPermissionDto> CreateAsync(CreateUserNetworkPermissionRequest request)
    {
        // 检查是否已存在相同的用户-网点-权限组合
        var exists = await _repository.Query().AnyAsync(e =>
            e.FUserId == request.UserId &&
            e.FNetworkPointCode == request.NetworkPointCode &&
            e.FPermissionType == request.PermissionType);
        if (exists)
            throw new InvalidOperationException("该用户已拥有此网点的相同权限");

        var entity = new SysUserNetworkPermission
        {
            FUserId = request.UserId,
            FNetworkPointCode = request.NetworkPointCode,
            FPermissionType = request.PermissionType,
            FCreatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;
        await _repository.DeleteAsync(id);
        return true;
    }

    private static UserNetworkPermissionDto MapToDto(SysUserNetworkPermission e) => new()
    {
        Id = e.FID,
        UserId = e.FUserId,
        NetworkPointCode = e.FNetworkPointCode,
        PermissionType = e.FPermissionType,
        CreatedTime = e.FCreatedTime
    };
}
