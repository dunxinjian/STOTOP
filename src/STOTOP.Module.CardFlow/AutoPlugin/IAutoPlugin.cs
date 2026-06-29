using System.Threading.Tasks;

namespace STOTOP.Module.CardFlow.AutoPlugin;

public enum PluginType { Input, Processing }

/// <summary>
/// 自动插件接口（统一替代原 IAgent 和 IAutoNodeHandler/IBatchAutoNodeHandler）
/// </summary>
public interface IAutoPlugin
{
    string PluginName { get; }
    string DisplayName { get; }
    PluginType PluginType { get; }
    Task<PluginResult> ExecuteAsync(PluginContext context);
    Task RollbackAsync(PluginContext context);
    PluginMetadata GetMetadata();
}
