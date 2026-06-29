namespace STOTOP.Module.CardFlow.Services.Handlers;

public class ClassificationHandlerFactory
{
    private readonly Dictionary<string, Type> _handlerMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册Handler类型映射</summary>
    public void Register<T>(string handlerType) where T : IClassificationHandler
    {
        _handlerMap[handlerType] = typeof(T);
    }

    /// <summary>获取所有已注册的Handler类型列表</summary>
    public List<string> GetRegisteredTypes()
    {
        return _handlerMap.Keys.ToList();
    }
}
