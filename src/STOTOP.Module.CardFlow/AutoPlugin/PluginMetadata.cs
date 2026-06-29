using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 插件元数据（供前端展示和注册表种子生成）
/// </summary>
public class PluginMetadata
{
    [JsonPropertyName("implementationType")]
    public string Code { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string Name { get; set; } = "";

    [JsonIgnore]
    public PluginType PluginType { get; set; }

    /// <summary>
    /// 前端类型列使用的字符串形式（"Input"/"Processing"）
    /// </summary>
    [JsonPropertyName("pluginType")]
    public string AutoPluginType => PluginType.ToString();

    public string Granularity { get; set; } = "batch"; // card / batch

    public string? Description { get; set; }

    public string? DefaultConfigJson { get; set; }

    [JsonPropertyName("configSchema")]
    public List<object> ConfigSchema { get; set; } = new();
}
