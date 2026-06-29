namespace STOTOP.Module.CardFlow.Services.Import.TransformEngine;

public interface ITransformEngine
{
    /// <summary>对一行数据执行所有转换规则，返回转换后的目标列字典</summary>
    Dictionary<string, object?> Execute(
        Dictionary<string, string> row,
        List<TransformRule> rules);
}

public class TransformRule
{
    public string TargetColumn { get; set; } = "";
    public List<string> SourceColumns { get; set; } = new();
    public string Expression { get; set; } = "";
    public string Mode { get; set; } = "visual";
    public List<TransformStep>? Transforms { get; set; }
}

public class TransformStep
{
    public string Type { get; set; } = "";
    public Dictionary<string, string> Params { get; set; } = new();
}
