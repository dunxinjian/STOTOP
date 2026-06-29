using System.Threading.Tasks;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 输入型插件基类
/// </summary>
public abstract class InputPluginBase : IAutoPlugin
{
    public abstract string PluginName { get; }
    public virtual string DisplayName => PluginName;
    public PluginType PluginType => PluginType.Input;
    public abstract Task<PluginResult> ExecuteAsync(PluginContext context);
    
    public virtual Task RollbackAsync(PluginContext context) => Task.CompletedTask;
    
    public virtual PluginMetadata GetMetadata() => new()
    {
        Code = PluginName,
        Name = DisplayName,
        PluginType = PluginType.Input,
        Granularity = "batch"
    };
}
