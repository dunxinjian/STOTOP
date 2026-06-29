using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 自动插件工厂（按编码注册和创建插件实例）
/// </summary>
public class AutoPluginFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);

    public AutoPluginFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Register<T>(string code) where T : IAutoPlugin
    {
        _registry[code] = typeof(T);
    }

    /// <summary>
    /// 创建插件实例。本工厂注册为 Singleton、其 <see cref="_serviceProvider"/> 是【根容器】，
    /// 不能用它解析 Scoped 插件（会落在根作用域 → captive：应用级近单例、跨并发共享、永不释放）。
    /// 因此创建插件【必须】由调用方传入其自身的 scoped provider（请求作用域或 IServiceScopeFactory.CreateScope()）。
    /// 注：早先存在的捕获根容器的单参重载 Create(code) 已移除，以杜绝 captive 反模式被再次引入。
    /// </summary>
    public IAutoPlugin Create(string code, IServiceProvider scopedProvider)
    {
        if (!_registry.TryGetValue(code, out var type))
            throw new InvalidOperationException($"未注册的插件编码: {code}");

        return (IAutoPlugin)scopedProvider.GetRequiredService(type);
    }

    public bool IsRegistered(string code) => _registry.ContainsKey(code);

    public IReadOnlyDictionary<string, Type> GetAllRegistrations() => _registry;

    public List<PluginMetadata> GetAllMetadata()
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        return _registry.Select(kv =>
        {
            var plugin = (IAutoPlugin)sp.GetRequiredService(kv.Value);
            return plugin.GetMetadata();
        }).ToList();
    }
}
