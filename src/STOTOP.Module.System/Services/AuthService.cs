using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace STOTOP.Module.System.Services;

public class AuthService : IAuthService
{
    private readonly STOTOPDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly SessionService _sessionService;
    private readonly SecurityAuditService _securityAuditService;
    private readonly SecurityConfigService _securityConfigService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(STOTOPDbContext context, IConfiguration configuration, IServiceScopeFactory serviceScopeFactory,
        SessionService sessionService, SecurityAuditService securityAuditService, SecurityConfigService securityConfigService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
        _sessionService = sessionService;
        _securityAuditService = securityAuditService;
        _securityConfigService = securityConfigService;
        _logger = logger;
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null)
    {
        var account = request.Account;

        // 检查账号是否被锁定
        var (isLocked, remainingMinutes) = await _securityAuditService.CheckAccountLocked(account);
        if (isLocked)
        {
            return ApiResult<LoginResponse>.Fail($"账号已被锁定，请{remainingMinutes}分钟后重试");
        }

        var user = await _context.Set<SysUser>()
            .FirstOrDefaultAsync(u => u.FAccount == account || (u.FPhone != null && u.FPhone == account));

        if (user == null)
        {
            await _securityAuditService.LogEvent(null, account, "Login", "Failed",
                ipAddress: ipAddress, deviceFingerprint: deviceFingerprint, deviceInfo: deviceInfo,
                failReason: "账号不存在");
            return ApiResult<LoginResponse>.Fail("账号或密码错误");
        }

        var passwordValid = VerifyPasswordHash(user.FPasswordHash, request.Password, out var needsMigration);

        if (!passwordValid)
        {
            await _securityAuditService.LogEvent(user.FID, account, "Login", "Failed",
                ipAddress: ipAddress, deviceFingerprint: deviceFingerprint, deviceInfo: deviceInfo,
                failReason: "密码错误");
            return ApiResult<LoginResponse>.Fail("账号或密码错误");
        }

        // 如果是旧的 SHA256 哈希，迁移到 bcrypt
        if (needsMigration)
        {
            user.FPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            await _context.SaveChangesAsync();
            _logger.LogInformation("用户 {UserId} 密码已从 SHA256 迁移到 bcrypt", user.FID);
        }

        if (user.FStatus != 1)
        {
            await _securityAuditService.LogEvent(user.FID, account, "Login", "Failed",
                ipAddress: ipAddress, deviceFingerprint: deviceFingerprint, deviceInfo: deviceInfo,
                failReason: "账号已禁用");
            return ApiResult<LoginResponse>.Fail("账号已被禁用");
        }

        // 创建会话
        var (sessionId, refreshToken) = await _sessionService.CreateSession(user.FID, ipAddress, deviceFingerprint, deviceInfo);

        // 检查最大设备数限制
        var maxDevices = await _securityConfigService.GetIntConfig("session.max_devices", 5);
        await _sessionService.EnforceMaxDevices(user.FID, maxDevices);

        // 使用单一作用域顺序查询，避免并行 Task.Run 的额外开销和潜在时序问题
        using var queryScope = _serviceScopeFactory.CreateScope();
        var queryContext = queryScope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var adminAuthService = queryScope.ServiceProvider.GetRequiredService<IAdminAuthorizationService>();
        var isAdmin = await adminAuthService.IsAdminByUserIdAsync(queryContext, user.FID);
        var roles = await GetUserRoleCodesAsync(queryContext, user.FID);
        var permissions = await GetUserPermissionCodesAsync(queryContext, user.FID, isAdmin);
        var menus = await GetUserMenusAsync(queryContext, user.FID, isAdmin);

        var accessTokenMinutes = await _securityConfigService.GetIntConfig("token.access_token_minutes", 30);
        var token = GenerateJwtToken(user, roles, isAdmin, sessionId, accessTokenMinutes);

        // 记录审计日志
        await _securityAuditService.LogEvent(user.FID, user.FAccount, "Login", "Success",
            ipAddress: ipAddress, deviceFingerprint: deviceFingerprint, deviceInfo: deviceInfo,
            sessionId: sessionId);

        var response = new LoginResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            SessionId = sessionId,
            ExpiresIn = accessTokenMinutes * 60,
            UserInfo = new UserInfoDto
            {
                Id = user.FID,
                Name = user.FName,
                Account = user.FAccount,
                Avatar = user.FAvatar,
                Phone = user.FPhone,
                Email = user.FEmail,
                Roles = roles,
                Permissions = permissions,
                Menus = menus
            }
        };

        return ApiResult<LoginResponse>.Success(response, "登录成功");
    }

    public async Task<ApiResult<UserInfoDto>> GetUserInfoAsync(long userId)
    {
        // 并行执行4个独立查询，每个查询使用独立的 DbContext 作用域
        var userTask = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            return await context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == userId);
        });

        var rolesTask = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            return await GetUserRoleCodesAsync(context, userId);
        });

        var permissionsTask = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            return await GetUserPermissionCodesAsync(context, userId);
        });

        var menusTask = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            return await GetUserMenusAsync(context, userId);
        });

        await Task.WhenAll(userTask, rolesTask, permissionsTask, menusTask);

        var user = await userTask;
        if (user == null)
        {
            return ApiResult<UserInfoDto>.Fail("用户不存在");
        }

        var roles = await rolesTask;
        var permissions = await permissionsTask;
        var menus = await menusTask;

        var userInfo = new UserInfoDto
        {
            Id = user.FID,
            Name = user.FName,
            Account = user.FAccount,
            Avatar = user.FAvatar,
            Phone = user.FPhone,
            Email = user.FEmail,
            Roles = roles,
            Permissions = permissions,
            Menus = menus
        };

        return ApiResult<UserInfoDto>.Success(userInfo);
    }

    public async Task<ApiResult<List<string>>> GetUserPermissionsAsync(long userId)
    {
        var permissions = await GetUserPermissionCodesAsync(userId);
        return ApiResult<List<string>>.Success(permissions);
    }

    private async Task<bool> CheckIsAdminAsync(long userId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        var adminAuthService = scope.ServiceProvider.GetRequiredService<IAdminAuthorizationService>();
        return await adminAuthService.IsAdminByUserIdAsync(context, userId);
    }

    private string GenerateJwtToken(SysUser user, List<string> roles, bool isAdmin, string? sessionId = null, int? expireMinutes = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Secret"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;
        var minutes = expireMinutes ?? int.Parse(jwtSettings["ExpireMinutes"] ?? "480");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.FID.ToString()),
            new Claim(ClaimTypes.Name, user.FAccount),
            new Claim("userName", user.FName),
            new Claim("userId", user.FID.ToString())
        };

        // 添加sessionId claim
        if (!string.IsNullOrEmpty(sessionId))
        {
            claims.Add(new Claim("sessionId", sessionId));
        }

        // 添加用户角色到 Token
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 为 admin 用户添加特殊管理员角色标识
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AdminAuthorizationService.AdminRoleClaim));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(minutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ===== 使用共享 _context 的版本（供 LoginAsync 等串行场景使用）=====
    private async Task<List<string>> GetUserRoleCodesAsync(long userId)
    {
        return await GetUserRoleCodesAsync(_context, userId);
    }

    private async Task<List<string>> GetUserPermissionCodesAsync(long userId)
    {
        return await GetUserPermissionCodesAsync(_context, userId);
    }

    private async Task<List<MenuDto>> GetUserMenusAsync(long userId)
    {
        return await GetUserMenusAsync(_context, userId);
    }

    // ===== 接受外部 DbContext 的版本（供并行查询使用）=====
    private static async Task<List<string>> GetUserRoleCodesAsync(STOTOPDbContext context, long userId)
    {
        return await context.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId)
            .Join(context.Set<SysRole>(),
                ur => ur.FRoleId,
                r => r.FID,
                (ur, r) => r.FCode)
            .ToListAsync();
    }

    private static async Task<List<string>> GetUserPermissionCodesAsync(STOTOPDbContext context, long userId)
    {
        // admin 用户直接返回所有权限编码
        var isAdmin = await context.Set<SysUserRole>()
            .AnyAsync(ur => ur.FUserId == userId && ur.FRoleId == AdminAuthorizationService.AdminRoleId);
        return await GetUserPermissionCodesAsync(context, userId, isAdmin);
    }

    private static async Task<List<string>> GetUserPermissionCodesAsync(STOTOPDbContext context, long userId, bool isAdmin)
    {
        if (isAdmin)
        {
            return await context.Set<SysPermission>()
                .Select(p => p.FCode)
                .Distinct()
                .ToListAsync();
        }

        var roleIds = await context.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId)
            .Select(ur => ur.FRoleId)
            .ToListAsync();

        return await context.Set<SysRolePermission>()
            .Where(rp => roleIds.Contains(rp.FRoleId))
            .Join(context.Set<SysPermission>(),
                rp => rp.FPermissionId,
                p => p.FID,
                (rp, p) => p.FCode)
            .Distinct()
            .ToListAsync();
    }

    private static async Task<List<MenuDto>> GetUserMenusAsync(STOTOPDbContext context, long userId)
    {
        // admin 用户直接返回所有模块和菜单
        var isAdmin = await context.Set<SysUserRole>()
            .AnyAsync(ur => ur.FUserId == userId && ur.FRoleId == AdminAuthorizationService.AdminRoleId);
        return await GetUserMenusAsync(context, userId, isAdmin);
    }

    private static async Task<List<MenuDto>> GetUserMenusAsync(STOTOPDbContext context, long userId, bool isAdmin)
    {
        if (isAdmin)
        {
            var allPermissions = await context.Set<SysPermission>()
                .Where(p => p.FType == "模块" || p.FType == "菜单")
                .OrderBy(p => p.FParentId)
                .ThenBy(p => p.FSort)
                .ToListAsync();

            var allMenuDtos = allPermissions.Select(p => new MenuDto
            {
                Id = p.FID,
                Name = p.FName,
                Code = p.FCode,
                Icon = p.FIcon,
                Route = p.FRoute,
                ComponentPath = p.FComponentPath,
                Type = p.FType == "模块" ? "module" : "menu",
                Sort = p.FSort,
                ParentId = p.FParentId,
                IsVisible = p.FIsVisible
            }).ToList();

            return allMenuDtos;
        }

        // 获取用户角色ID列表
        var roleIds = await context.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId)
            .Select(ur => ur.FRoleId)
            .ToListAsync();

        // 获取角色关联的权限（只包含模块和菜单类型，不包含按钮）
        var permissionIds = await context.Set<SysRolePermission>()
            .Where(rp => roleIds.Contains(rp.FRoleId))
            .Select(rp => rp.FPermissionId)
            .Distinct()
            .ToListAsync();

        // 查询权限详情，只获取模块和菜单类型
        var permissions = await context.Set<SysPermission>()
            .Where(p => permissionIds.Contains(p.FID) && (p.FType == "模块" || p.FType == "菜单"))
            .OrderBy(p => p.FParentId)
            .ThenBy(p => p.FSort)
            .ToListAsync();

        // 转换为 MenuDto（返回扁平列表，前端自行构建树形结构）
        var menuDtos = permissions.Select(p => new MenuDto
        {
            Id = p.FID,
            Name = p.FName,
            Code = p.FCode,
            Icon = p.FIcon,
            Route = p.FRoute,
            ComponentPath = p.FComponentPath,
            Type = p.FType == "模块" ? "module" : "menu",
            Sort = p.FSort,
            ParentId = p.FParentId,
            IsVisible = p.FIsVisible
        }).ToList();

        return menuDtos;
    }

    private List<MenuDto> BuildMenuTree(List<MenuDto> menus)
    {
        var menuLookup = menus.ToLookup(m => m.ParentId);
        var rootMenus = new List<MenuDto>();

        foreach (var menu in menus.Where(m => m.ParentId == 0))
        {
            rootMenus.Add(BuildMenuNode(menu, menuLookup));
        }

        return rootMenus.OrderBy(m => m.Sort).ToList();
    }

    private MenuDto BuildMenuNode(MenuDto menu, ILookup<long, MenuDto> menuLookup)
    {
        var children = menuLookup[menu.Id]
            .OrderBy(m => m.Sort)
            .Select(m => BuildMenuNode(m, menuLookup))
            .ToList();

        menu.Children = children;
        return menu;
    }

    public async Task<ApiResult<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress = null, string? deviceFingerprint = null)
    {
        var session = await _sessionService.ValidateRefreshToken(refreshToken);
        if (session == null)
        {
            return ApiResult<RefreshTokenResponse>.Fail("RefreshToken无效或已过期", 401);
        }

        // 查询关联用户信息
        var user = await _context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == session.FUserId);
        if (user == null || user.FStatus != 1)
        {
            return ApiResult<RefreshTokenResponse>.Fail("用户不存在或已禁用", 401);
        }

        // 旋转RefreshToken
        var newRefreshToken = await _sessionService.RotateRefreshToken(session.FID);

        // 签发新的AccessToken
        var roles = await GetUserRoleCodesAsync(user.FID);
        var accessTokenMinutes = await _securityConfigService.GetIntConfig("token.access_token_minutes", 30);
        var isAdmin = await CheckIsAdminAsync(user.FID);
        var token = GenerateJwtToken(user, roles, isAdmin, session.FSessionId, accessTokenMinutes);

        // 记录审计日志
        await _securityAuditService.LogEvent(user.FID, user.FAccount, "TokenRefresh", "Success",
            ipAddress: ipAddress, deviceFingerprint: deviceFingerprint, sessionId: session.FSessionId);

        return ApiResult<RefreshTokenResponse>.Success(new RefreshTokenResponse
        {
            Token = token,
            RefreshToken = newRefreshToken,
            ExpiresIn = accessTokenMinutes * 60
        });
    }

    public async Task<(string token, int expiresIn)> IssueAccessTokenAsync(long userId, string? sessionId)
    {
        var user = await _context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == userId);
        if (user == null || user.FStatus != 1)
            return (string.Empty, 0);

        var roles = await GetUserRoleCodesAsync(userId);
        var accessTokenMinutes = await _securityConfigService.GetIntConfig("token.access_token_minutes", 30);
        var isAdmin = await CheckIsAdminAsync(user.FID);
        var token = GenerateJwtToken(user, roles, isAdmin, sessionId, accessTokenMinutes);

        return (token, accessTokenMinutes * 60);
    }

    public async Task LogoutAsync(long userId, string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            await _sessionService.TerminateSession(sessionId, "用户主动退出");
        }

        var user = await _context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == userId);
        await _securityAuditService.LogEvent(userId, user?.FAccount, "Logout", "Success", sessionId: sessionId);
    }

    /// <summary>
    /// 验证密码哈希。支持 bcrypt（新）和 SHA256（旧，登录时自动迁移）。
    /// </summary>
    /// <param name="storedHash">数据库中存储的哈希值</param>
    /// <param name="password">用户输入的明文密码</param>
    /// <param name="needsMigration">输出参数，表示是否需要从 SHA256 迁移到 bcrypt</param>
    private static bool VerifyPasswordHash(string storedHash, string password, out bool needsMigration)
    {
        needsMigration = false;

        // 判断是否为旧的 SHA256 哈希（64 位十六进制字符）
        if (storedHash.Length == 64 && storedHash.All(c => char.IsAsciiHexDigit(c)))
        {
            needsMigration = true;
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString() == storedHash;
        }

        // 使用 bcrypt 验证
        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }

    public async Task<bool> VerifyPassword(long userId, string password)
    {
        var user = await _context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == userId);
        if (user == null) return false;

        return VerifyPasswordHash(user.FPasswordHash, password, out _);
    }
}
