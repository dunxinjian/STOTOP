using System.Threading.Tasks;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 处理型插件基类
/// </summary>
public abstract class ProcessingPluginBase : IAutoPlugin
{
    public abstract string PluginName { get; }
    public virtual string DisplayName => PluginName;
    public PluginType PluginType => PluginType.Processing;
    public abstract Task<PluginResult> ExecuteAsync(PluginContext context);
    
    public virtual Task RollbackAsync(PluginContext context) => Task.CompletedTask;
    
    public virtual PluginMetadata GetMetadata() => new()
    {
        Code = PluginName,
        Name = DisplayName,
        PluginType = PluginType.Processing,
        Granularity = "card"
    };
}
