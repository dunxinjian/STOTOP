using System.Text.Json;
using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Models;

public sealed class StartPolicy
{
    public InitiatorScope? InitiatorScope { get; set; }
    public OnBehalfPolicy? OnBehalf { get; set; }
}

public sealed class InitiatorScope
{
    public List<long> Roles { get; set; } = new();
    public List<long> Orgs { get; set; } = new();
    public List<long> Positions { get; set; } = new();
    public List<long> Users { get; set; } = new();

    [JsonIgnore]
    public bool IsEmpty => Roles.Count == 0 && Orgs.Count == 0 && Positions.Count == 0 && Users.Count == 0;
}

public sealed class OnBehalfPolicy
{
    public bool Enabled { get; set; }
    public InitiatorScope AgentScope { get; set; } = new();
}

public static class StartPolicyCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>解析发起策略；startPolicyJson 为空时从 legacy 可发起角色JSON 派生角色维（向后兼容，无数据回填）。非法 JSON 静默降级为空策略（=不限制）。</summary>
    public static StartPolicy Parse(string? startPolicyJson, string? legacyAllowedRolesJson)
    {
        if (!string.IsNullOrWhiteSpace(startPolicyJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<StartPolicy>(startPolicyJson, Options);
                if (parsed != null) return parsed;
            }
            catch (JsonException) { /* 静默降级 */ }
        }

        var policy = new StartPolicy();
        if (!string.IsNullOrWhiteSpace(legacyAllowedRolesJson))
        {
            try
            {
                var roleStrings = JsonSerializer.Deserialize<List<string>>(legacyAllowedRolesJson, Options) ?? new();
                var roleIds = roleStrings.Select(s => long.TryParse(s, out var id) ? id : 0L).Where(id => id > 0).ToList();
                if (roleIds.Count > 0) policy.InitiatorScope = new InitiatorScope { Roles = roleIds };
            }
            catch (JsonException) { /* 静默降级 */ }
        }
        return policy;
    }
}
