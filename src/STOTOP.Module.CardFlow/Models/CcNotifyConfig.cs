using System.Text.Json;
using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Models;

public sealed class CcNotifyConfig
{
    public List<CcUser> Users { get; set; } = new();
    public string Timing { get; set; } = "onEnter";
    public List<string> Channels { get; set; } = new() { "system" };

    [JsonIgnore]
    public bool HasRecipients => Users.Count > 0;

    public bool ShouldFire(string currentTiming)
        => HasRecipients && (string.Equals(Timing, "always", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Timing, currentTiming, StringComparison.OrdinalIgnoreCase));

    public bool HasChannel(string channel)
        => Channels.Any(c => string.Equals(c, channel, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>解析 FCcConfigJson；null/空/非法JSON→null(=不触发)。向后兼容缺字段。</summary>
    public static CcNotifyConfig? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var cfg = JsonSerializer.Deserialize<CcNotifyConfig>(json, Opts);
            return cfg is { HasRecipients: true } ? cfg : null;
        }
        catch (JsonException) { return null; }
    }
}

public sealed class CcUser
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
}
