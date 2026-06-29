using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;
using STOTOP.Infrastructure.Events;
using System.Security.Claims;

namespace STOTOP.Module.System.Services;

public class UserService : IUserService
{
    private readonly STOTOPDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChangeLogService _changeLogService;
    private readonly IEventDispatcher _eventDispatcher;

    public UserService(STOTOPDbContext context, IHttpContextAccessor httpContextAccessor, IChangeLogService changeLogService, IEventDispatcher eventDispatcher)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _changeLogService = changeLogService;
        _eventDispatcher = eventDispatcher;
    }

    private (long? UserId, string? UserName) GetCurrentUser()
    {
        var claims = _httpContextAccessor.HttpContext?.User;
        var userIdStr = claims?.FindFirst("userId")?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = claims?.FindFirst("userName")?.Value ?? claims?.FindFirst(ClaimTypes.Name)?.Value;
        long? userId = long.TryParse(userIdStr, out var id) ? id : null;
        return (userId, userName);
    }

    public async Task<ApiResult<PagedResult<UserDto>>> GetPagedListAsync(UserPagedRequest request)
    {
        var query = _context.Set<SysUser>()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(u => u.FName.Contains(keyword)
                || u.FAccount.Contains(keyword)
                || (u.FPhone != null && u.FPhone.Contains(keyword))
                || u.UserOrganizations.Any(uo => uo.Organization.FName.Contains(keyword)));
        }

        // 按角色过滤：只返回该角色关联的用户
        if (request.RoleId.HasValue)
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.FRoleId == request.RoleId.Value
                && (!request.OrgId.HasValue || ur.FOrgId == request.OrgId.Value || ur.FOrgId == null)));
        }
        else if (request.OrgId.HasValue)
        {
            // 无角色过滤但有组织过滤
            query = query.Where(u => u.UserRoles.Any(ur => ur.FOrgId == request.OrgId.Value));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var userIds = items.Select(u => u.FID).ToList();

        // 批量获取用户的组织信息（用于 OrgName，优先取主任职组织）
        var userOrgs = await _context.Set<SysUserOrganization>()
            .Where(uo => userIds.Contains(uo.FUserId))
            .Join(_context.Set<SysOrganization>(),
                uo => uo.FOrgId,
                org => org.FID,
                (uo, org) => new { uo.FUserId, OrgName = org.FName, uo.FIsPrimaryOrg })
            .ToListAsync();

        var dtos = items.Select(u => {
            var org = userOrgs.FirstOrDefault(o => o.FUserId == u.FID && o.FIsPrimaryOrg == 1)
                   ?? userOrgs.FirstOrDefault(o => o.FUserId == u.FID);
            return new UserDto
            {
                Id = u.FID,
                FUID = u.FUID,
                Name = u.FName,
                Account = u.FAccount,
                Phone = u.FPhone,
                Email = u.FEmail,
                Avatar = u.FAvatar,
                OrgName = org?.OrgName,
                Status = u.FStatus,
                DingTalkBindStatus = u.FDingTalkBindStatus,
                DingTalkUserId = u.FDingTalkUserId,
                DingTalkUserName = u.FDingTalkUserName,
                Remark = u.FRemark,
                CreateTime = u.FCreateTime,
                Roles = u.UserRoles.Select(ur => new RoleSimpleDto
                {
                    Id = ur.Role.FID,
                    Name = ur.Role.FName,
                    Code = ur.Role.FCode
                }).ToList()
            };
        }).ToList();

        var result = new PagedResult<UserDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };

        return ApiResult<PagedResult<UserDto>>.Success(result);
    }

    public async Task<ApiResult<UserDto>> GetByIdAsync(long id)
    {
        var user = await _context.Set<SysUser>()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.FID == id);

        if (user == null)
        {
            return ApiResult<UserDto>.Fail("用户不存在");
        }

        var dto = new UserDto
        {
            Id = user.FID,
            FUID = user.FUID,
            Name = user.FName,
            Account = user.FAccount,
            Phone = user.FPhone,
            Email = user.FEmail,
            Avatar = user.FAvatar,
            Status = user.FStatus,
            DingTalkBindStatus = user.FDingTalkBindStatus,
            DingTalkUserId = user.FDingTalkUserId,
            DingTalkUserName = user.FDingTalkUserName,
            Remark = user.FRemark,
            CreateTime = user.FCreateTime,
            Roles = user.UserRoles.Select(ur => new RoleSimpleDto
            {
                Id = ur.Role.FID,
                Name = ur.Role.FName,
                Code = ur.Role.FCode
            }).ToList()
        };

        // 查询用户组织列表
        dto.Organizations = await GetUserOrganizationsAsync(id);

        // 查询用户岗位列表
        dto.Positions = await GetUserPositionsAsync(id);

        return ApiResult<UserDto>.Success(dto);
    }

    public async Task<ApiResult<UserDto>> CreateAsync(CreateUserRequest request)
    {
        if (await _context.Set<SysUser>().AnyAsync(u => u.FAccount == request.Account))
        {
            return ApiResult<UserDto>.Fail("账号已存在");
        }

        var user = new SysUser
        {
            FUID = Guid.NewGuid().ToString("N"),
            FName = request.Name,
            FAccount = request.Account,
            FPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FPhone = request.Phone,
            FEmail = request.Email,
            FAvatar = request.Avatar,
            FStatus = request.Status
        };

        await _context.Set<SysUser>().AddAsync(user);
        await _context.SaveChangesAsync();

        // 添加角色关联
        if (request.RoleIds.Any())
        {
            var userRoles = request.RoleIds.Select(roleId => new SysUserRole
            {
                FUserId = user.FID,
                FRoleId = roleId
            });
            await _context.Set<SysUserRole>().AddRangeAsync(userRoles);
            await _context.SaveChangesAsync();
        }

        // 记录变更日志
        var (operatorId, operatorName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("用户", user.FID, user.FName,
            "创建", $"创建用户：{user.FName}（{user.FAccount}）", operatorId, operatorName);

        return await GetByIdAsync(user.FID);
    }

    public async Task<ApiResult<UserDto>> UpdateAsync(long id, UpdateUserRequest request)
    {
        var user = await _context.Set<SysUser>()
            .Include(u => u.UserRoles)
            .AsTracking()
            .FirstOrDefaultAsync(u => u.FID == id);

        if (user == null)
        {
            return ApiResult<UserDto>.Fail("用户不存在");
        }

        // 记录旧值用于对比
        var oldName = user.FName;
        var oldPhone = user.FPhone;
        var oldEmail = user.FEmail;
        var oldStatus = user.FStatus;

        user.FName = request.Name;
        user.FPhone = request.Phone;
        user.FEmail = request.Email;
        user.FAvatar = request.Avatar;
        user.FStatus = request.Status;
        user.FUpdateTime = DateTime.Now;

        // 更新角色关联
        _context.Set<SysUserRole>().RemoveRange(user.UserRoles);
        if (request.RoleIds.Any())
        {
            var userRoles = request.RoleIds.Select(roleId => new SysUserRole
            {
                FUserId = user.FID,
                FRoleId = roleId
            });
            await _context.Set<SysUserRole>().AddRangeAsync(userRoles);
        }

        await _context.SaveChangesAsync();

        // 姓名变更时发布辅助核算同步事件
        if (oldName != request.Name)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "SYS用户",
                SourceId = id,
                NewName = request.Name
            });
        }

        // 记录变更日志
        var changes = new Dictionary<string, object>();
        if (oldName != request.Name) changes["姓名"] = new { 旧值 = oldName, 新值 = request.Name };
        if (oldPhone != request.Phone) changes["手机"] = new { 旧值 = oldPhone ?? "", 新值 = request.Phone ?? "" };
        if (oldEmail != request.Email) changes["邮箱"] = new { 旧值 = oldEmail ?? "", 新值 = request.Email ?? "" };
        if (oldStatus != request.Status) changes["状态"] = new { 旧值 = oldStatus.ToString(), 新值 = request.Status.ToString() };

        if (changes.Count > 0)
        {
            var changeJson = global::System.Text.Json.JsonSerializer.Serialize(changes,
                new global::System.Text.Json.JsonSerializerOptions { Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var (operatorId, operatorName) = GetCurrentUser();
            await _changeLogService.LogChangeAsync("用户", id, user.FName, "修改", changeJson, operatorId, operatorName);
        }

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var user = await _context.Set<SysUser>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.FID == id);
        if (user == null)
        {
            return ApiResult<bool>.Fail("用户不存在");
        }

        // 检查是否被凭证引用（通过 FIN辅助核算项目）
        var isReferenced = await CheckFinReferenceAsync("SYS用户", id);
        if (isReferenced)
        {
            return ApiResult<bool>.Fail("该用户已被财务凭证引用，无法删除");
        }

        var userName = user.FName;
        var userAccount = user.FAccount;

        _context.Set<SysUser>().Remove(user);
        await _context.SaveChangesAsync();

        // 记录变更日志
        var (operatorId, operatorName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("用户", id, userName, "删除",
            $"删除用户：{userName}（{userAccount}）", operatorId, operatorName);

        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> ResetPasswordAsync(long id, string newPassword)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var user = await _context.Set<SysUser>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.FID == id);
        if (user == null)
        {
            return ApiResult<bool>.Fail("用户不存在");
        }

        user.FPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "密码重置成功");
    }

    public async Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId)
    {
        return await _context.Set<SysUserOrganization>()
            .Where(uo => uo.FUserId == userId)
            .Join(_context.Set<SysOrganization>(),
                uo => uo.FOrgId,
                org => org.FID,
                (uo, org) => new UserOrganizationDto
                {
                    Id = uo.FID,
                    UserId = uo.FUserId,
                    OrgId = uo.FOrgId,
                    OrgName = org.FName,
                    OrgType = org.FTypeId.ToString(),
                    IsPrimaryOrg = uo.FIsPrimaryOrg,
                    Position = uo.FPosition,
                    JobNumber = uo.FJobNumber,
                    EntryDate = uo.FEntryDate,
                    Status = uo.FStatus
                })
            .ToListAsync();
    }

    public async Task<List<PositionDto>> GetUserPositionsAsync(long userId)
    {
        return await _context.Set<SysUserPosition>()
            .Where(up => up.FUserId == userId)
            .Join(_context.Set<SysPosition>(),
                up => up.FPositionId,
                p => p.FID,
                (up, p) => new PositionDto
                {
                    Id = p.FID,
                    Uid = p.FUID,
                    Name = p.FName,
                    Code = p.FCode,
                    Description = p.FDescription,
                    Status = p.FStatus,
                    Sort = p.FSort,
                    CreateTime = p.FCreateTime,
                    UpdateTime = p.FUpdateTime
                })
            .ToListAsync();
    }

    /// <summary>
    /// 检查是否被财务凭证引用
    /// </summary>
    private async Task<bool> CheckFinReferenceAsync(string sourceType, long sourceId)
    {
        try
        {
            var result = await _context.Database.SqlQueryRaw<int>(
                @"SELECT COUNT(*) AS [Value] FROM [FIN辅助核算项目] 
                  WHERE FAuxType = {0} AND CAST(FCode AS BIGINT) = {1}", sourceType, sourceId)
                .FirstOrDefaultAsync();

            return result > 0;
        }
        catch
        {
            return false;
        }
    }

}
