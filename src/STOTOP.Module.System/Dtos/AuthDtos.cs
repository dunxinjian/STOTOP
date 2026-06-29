namespace STOTOP.Module.System.Dtos;

public class LoginRequest
{
    public string Account { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string? SessionId { get; set; }
    public int ExpiresIn { get; set; }
    public UserInfoDto UserInfo { get; set; } = new();
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class VerifyPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public class SessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? LoginTime { get; set; }
    public DateTime? LastActiveTime { get; set; }
    public bool IsCurrent { get; set; }
}

public class UpdateConfigsRequest
{
    public Dictionary<string, string> Configs { get; set; } = new();
}

public class UserInfoDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public List<MenuDto> Menus { get; set; } = new();
}
