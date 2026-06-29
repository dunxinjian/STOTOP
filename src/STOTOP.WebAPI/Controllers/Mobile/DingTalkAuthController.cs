using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace STOTOP.WebAPI.Controllers.Mobile;

/// <summary>
/// 钉钉移动端鉴权控制器
/// </summary>
[ApiController]
[Route("api/dingtalk")]
public class DingTalkAuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DingTalkAuthController> _logger;
    private readonly STOTOPDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;

    public DingTalkAuthController(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<DingTalkAuthController> logger,
        STOTOPDbContext context,
        IMemoryCache cache,
        IWebHostEnvironment env)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _context = context;
        _cache = cache;
        _env = env;
    }

    /// <summary>
    /// 钉钉免登 — 用 authCode 换取系统 JWT
    /// </summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] DingTalkLoginRequest request)
    {
        try
        {
            // 开发环境 mock 支持：Chrome 直接打开 mobile.html 时 bridge.requestAuthCode() 返回 dev-mock-auth-code
            // 此时取数据库第一个启用用户直接颁发 Token，便于本地调试
            if (request.AuthCode == "dev-mock-auth-code" && _env.IsDevelopment())
            {
                var devUser = await _context.Set<SysUser>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.FStatus == 1);

                if (devUser != null)
                {
                    var devSystemUser = await BuildSystemUserDto(devUser);
                    var devToken = GenerateJwtToken(devSystemUser);
                    var devRefreshToken = GenerateRefreshToken();
                    await SaveRefreshToken(devSystemUser.Id, devRefreshToken);

                    _logger.LogInformation("[DEV-MOCK] 开发环境钉钉免登 mock 登录: UserId={UserId}, Name={Name}", devSystemUser.Id, devSystemUser.Name);

                    return Ok(new
                    {
                        code = 200,
                        data = new
                        {
                            token = devToken,
                            refreshToken = devRefreshToken,
                            user = new
                            {
                                id = devSystemUser.Id,
                                name = devSystemUser.Name,
                                avatar = devSystemUser.Avatar,
                                roles = devSystemUser.Roles,
                                organizations = devSystemUser.Organizations,
                                defaultOrgId = devSystemUser.DefaultOrgId
                            }
                        },
                        message = "开发环境登录成功"
                    });
                }

                _logger.LogWarning("[DEV-MOCK] 开发环境 mock 登录失败：未找到任何启用用户");
            }

            // 1. 用 authCode 换取钉钉用户信息
            var accessToken = await GetDingTalkAccessToken();
            var dingUser = await GetDingTalkUserByCode(request.AuthCode, accessToken);

            if (dingUser == null)
            {
                return Ok(new { code = 401, message = "钉钉免登失败：无法获取用户信息" });
            }

            // 2. 根据钉钉 userId 匹配系统用户
            // TODO: 从数据库中根据 dingUser.UserId 或 unionId 查找系统用户
            // 此处为模板代码，需接入实际用户查询逻辑
            var systemUser = await MatchSystemUser(dingUser.UserId, dingUser.Name);
            if (systemUser == null)
            {
                return Ok(new { code = 403, message = "该钉钉用户未绑定系统账号" });
            }

            // 3. 生成 JWT + RefreshToken
            var token = GenerateJwtToken(systemUser);
            var refreshToken = GenerateRefreshToken();

            // 4. 存储 refreshToken（关联到用户）
            // TODO: 保存 refreshToken 到数据库或缓存（含过期时间 7 天）
            await SaveRefreshToken(systemUser.Id, refreshToken);

            return Ok(new
            {
                code = 200,
                data = new
                {
                    token,
                    refreshToken,
                    user = new
                    {
                        id = systemUser.Id,
                        name = systemUser.Name,
                        avatar = systemUser.Avatar,
                        roles = systemUser.Roles,
                        organizations = systemUser.Organizations,
                        defaultOrgId = systemUser.DefaultOrgId
                    }
                },
                message = "登录成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉免登失败");
            return Ok(new { code = 500, message = "登录异常，请稍后重试" });
        }
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // 验证 refreshToken 有效性
            var userId = await ValidateRefreshToken(request.RefreshToken);
            if (userId <= 0)
            {
                return Ok(new { code = 401, message = "refreshToken 已过期" });
            }

            // 获取用户信息生成新 Token
            var systemUser = await GetSystemUserById(userId);
            if (systemUser == null)
            {
                return Ok(new { code = 401, message = "用户不存在" });
            }

            var newToken = GenerateJwtToken(systemUser);
            var newRefreshToken = GenerateRefreshToken();

            // 替换旧 refreshToken
            await SaveRefreshToken(userId, newRefreshToken);

            return Ok(new
            {
                code = 200,
                data = new { token = newToken, refreshToken = newRefreshToken },
                message = "刷新成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token 刷新失败");
            return Ok(new { code = 500, message = "刷新异常" });
        }
    }

    /// <summary>
    /// 获取 JSAPI 签名
    /// </summary>
    [HttpGet("jsapi-signature")]
    [Authorize]
    public async Task<IActionResult> GetJsapiSignature([FromQuery] string url)
    {
        try
        {
            var corpId = _config["DingTalk:CorpId"] ?? "";
            var agentId = _config["DingTalk:AgentId"] ?? "";

            // 获取 jsapi_ticket
            var accessToken = await GetDingTalkAccessToken();
            var ticket = await GetJsapiTicket(accessToken);

            // 生成签名
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonceStr = Guid.NewGuid().ToString("N").Substring(0, 16);

            var signStr = $"jsapi_ticket={ticket}&noncestr={nonceStr}&timestamp={timestamp}&url={url}";
            var signature = ComputeSha1(signStr);

            return Ok(new
            {
                code = 200,
                data = new
                {
                    agentId,
                    corpId,
                    timeStamp = timestamp,
                    nonceStr,
                    signature
                },
                message = "ok"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 JSAPI 签名失败");
            return Ok(new { code = 500, message = "获取签名异常" });
        }
    }

    #region Private Methods

    private string GenerateJwtToken(SystemUserDto user)
    {
        var secret = _config["Jwt:Secret"]!;
        var issuer = _config["Jwt:Issuer"] ?? "STOTOP";
        var audience = _config["Jwt:Audience"] ?? "STOTOP";
        // 移动端 Token 有效期 2 小时（比 PC 端长）
        var expireHours = 2;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new("source", "dingtalk-mobile"), // 标识来源为钉钉移动端
        };

        // 添加角色 claims
        foreach (var role in user.Roles ?? Array.Empty<string>())
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string ComputeSha1(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 获取钉钉企业内部应用 AccessToken（带内存缓存）
    /// </summary>
    private async Task<string> GetDingTalkAccessToken()
    {
        const string cacheKey = "dingtalk_access_token";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        var appKey = _config["DingTalk:AppKey"] ?? "";
        var appSecret = _config["DingTalk:AppSecret"] ?? "";

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "https://oapi.dingtalk.com/gettoken",
            new { appkey = appKey, appsecret = appSecret }
        );

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("access_token").GetString() ?? "";

        // access_token 钉钉端有效期 2 小时，提前 30 分钟过期，避免临界点失败
        _cache.Set(cacheKey, token, TimeSpan.FromHours(1.5));
        return token;
    }

    /// <summary>
    /// 通过 authCode 获取钉钉用户信息
    /// </summary>
    private async Task<DingTalkUserInfo?> GetDingTalkUserByCode(string authCode, string accessToken)
    {
        var client = _httpClientFactory.CreateClient();

        // 新版接口：通过免登码获取用户信息
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://oapi.dingtalk.com/topapi/v2/user/getuserinfo?access_token={accessToken}");
        request.Content = JsonContent.Create(new { code = authCode });

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var errcode = json.GetProperty("errcode").GetInt32();
        if (errcode != 0) return null;

        var result = json.GetProperty("result");
        return new DingTalkUserInfo
        {
            UserId = result.GetProperty("userid").GetString() ?? "",
            Name = result.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            UnionId = result.TryGetProperty("unionid", out var uid) ? uid.GetString() ?? "" : ""
        };
    }

    /// <summary>
    /// 获取 JSAPI Ticket（带内存缓存）
    /// </summary>
    private async Task<string> GetJsapiTicket(string accessToken)
    {
        const string cacheKey = "dingtalk_jsapi_ticket";

        if (_cache.TryGetValue(cacheKey, out string? cachedTicket) && !string.IsNullOrEmpty(cachedTicket))
        {
            return cachedTicket;
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetFromJsonAsync<JsonElement>(
            $"https://oapi.dingtalk.com/get_jsapi_ticket?access_token={accessToken}");
        var ticket = response.GetProperty("ticket").GetString() ?? "";

        // 钉钉 jsapi_ticket 有效期 2 小时
        _cache.Set(cacheKey, ticket, TimeSpan.FromHours(2));
        return ticket;
    }

    /// <summary>
    /// 匹配系统用户：根据钉钉 UserId 查找已绑定的系统用户
    /// </summary>
    private async Task<SystemUserDto?> MatchSystemUser(string dingUserId, string name)
    {
        _logger.LogInformation("匹配系统用户: DingTalk UserId={DingUserId}, Name={Name}", dingUserId, name);

        var user = await _context.Set<SysUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.FDingTalkUserId == dingUserId && u.FStatus == 1);

        if (user == null)
        {
            _logger.LogWarning("钉钉用户 {DingUserId} ({Name}) 未匹配到系统用户", dingUserId, name);
            return null;
        }

        return await BuildSystemUserDto(user);
    }

    /// <summary>
    /// 根据系统用户 ID 获取用户信息
    /// </summary>
    private async Task<SystemUserDto?> GetSystemUserById(int userId)
    {
        var user = await _context.Set<SysUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.FID == userId && u.FStatus == 1);

        if (user == null) return null;
        return await BuildSystemUserDto(user);
    }

    /// <summary>
    /// 组装 SystemUserDto，附带组织列表与角色编码
    /// </summary>
    private async Task<SystemUserDto> BuildSystemUserDto(SysUser user)
    {
        // 用户组织列表（通过 SysUserOrganization Join SysOrganization）
        var orgs = await _context.Set<SysUserOrganization>()
            .AsNoTracking()
            .Where(uo => uo.FUserId == user.FID && uo.FStatus == 1)
            .Join(_context.Set<SysOrganization>(),
                uo => uo.FOrgId,
                org => org.FID,
                (uo, org) => new { uo.FOrgId, org.FName, org.FCode, uo.FIsPrimaryOrg })
            .ToListAsync();

        var defaultOrgId = orgs.FirstOrDefault(o => o.FIsPrimaryOrg == 1)?.FOrgId
            ?? orgs.FirstOrDefault()?.FOrgId ?? 0;

        // 用户角色编码列表
        var roles = await _context.Set<SysUserRole>()
            .AsNoTracking()
            .Where(ur => ur.FUserId == user.FID)
            .Join(_context.Set<SysRole>(),
                ur => ur.FRoleId,
                r => r.FID,
                (ur, r) => r.FCode)
            .ToListAsync();

        return new SystemUserDto
        {
            Id = (int)user.FID,
            Name = user.FName,
            Avatar = user.FAvatar,
            Roles = roles.ToArray(),
            Organizations = orgs.Select(o => (object)new
            {
                id = (int)o.FOrgId,
                name = o.FName,
                code = o.FCode ?? ""
            }).ToArray(),
            DefaultOrgId = (int)defaultOrgId
        };
    }

    /// <summary>
    /// 保存或更新移动端 RefreshToken（7 天有效期）
    /// </summary>
    private async Task SaveRefreshToken(int userId, string refreshToken)
    {
        _logger.LogInformation("保存 RefreshToken: UserId={UserId}", userId);

        var expiry = DateTime.Now.AddDays(7);

        var session = await _context.Set<SysUserSession>()
            .FirstOrDefaultAsync(s => s.FUserId == userId && s.FSessionId.StartsWith("mobile_"));

        if (session != null)
        {
            session.FRefreshToken = refreshToken;
            session.FRefreshTokenExpiry = expiry;
            session.FLastActiveTime = DateTime.Now;
            session.FStatus = 1;
        }
        else
        {
            _context.Set<SysUserSession>().Add(new SysUserSession
            {
                FUserId = userId,
                FSessionId = $"mobile_{userId}_{Guid.NewGuid():N}",
                FRefreshToken = refreshToken,
                FRefreshTokenExpiry = expiry,
                FLoginTime = DateTime.Now,
                FLastActiveTime = DateTime.Now,
                FStatus = 1
            });
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 校验 RefreshToken 有效性，返回关联的 userId（0 表示无效）
    /// </summary>
    private async Task<int> ValidateRefreshToken(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return 0;

        var session = await _context.Set<SysUserSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FRefreshToken == refreshToken
                && s.FStatus == 1
                && s.FRefreshTokenExpiry > DateTime.Now);

        return session != null ? (int)session.FUserId : 0;
    }

    #endregion

    #region DTOs

    public class DingTalkLoginRequest
    {
        public string AuthCode { get; set; } = "";
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = "";
    }

    private class DingTalkUserInfo
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string UnionId { get; set; } = "";
    }

    private class SystemUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Avatar { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
        public object[] Organizations { get; set; } = Array.Empty<object>();
        public int DefaultOrgId { get; set; }
    }

    #endregion
}
