using System.Globalization;
using Jint;
using Jint.Runtime;
using Microsoft.Extensions.Logging;

namespace STOTOP.Module.CardFlow.Services.Import.TransformEngine;

public class JintTransformEngine : ITransformEngine
{
    private readonly ILogger<JintTransformEngine> _logger;

    public JintTransformEngine(ILogger<JintTransformEngine> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, object?> Execute(
        Dictionary<string, string> row,
        List<TransformRule> rules)
    {
        var result = new Dictionary<string, object?>();
        var engine = CreateEngine();

        // 注入当前行数据
        engine.SetValue("row", row);

        // 注入辅助函数
        RegisterHelpers(engine);

        foreach (var rule in rules)
        {
            try
            {
                var jsValue = engine.Evaluate(rule.Expression);
                result[rule.TargetColumn] = ConvertJsValue(jsValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "转换规则执行失败: TargetColumn={TargetColumn}, Expression={Expression}",
                    rule.TargetColumn, rule.Expression);
                result[rule.TargetColumn] = null;
            }
        }

        return result;
    }

    private static Engine CreateEngine()
    {
        return new Engine(options =>
        {
            options.LimitRecursion(10);
            options.MaxStatements(10_000);
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });
    }

    private static void RegisterHelpers(Engine engine)
    {
        // parseFloat: 安全解析浮点数
        engine.SetValue("parseFloat", new Func<string, object?>(str =>
        {
            if (string.IsNullOrWhiteSpace(str)) return null;
            return double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val)
                ? val
                : (object?)null;
        }));

        // parseInt: 安全解析整数
        engine.SetValue("parseInt", new Func<string, object?>(str =>
        {
            if (string.IsNullOrWhiteSpace(str)) return null;
            // 先尝试解析为 double 再截取整数部分，兼容 "123.45" 这类输入
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var dVal))
                return (int)dVal;
            return null;
        }));

        // trim: 去空格
        engine.SetValue("trim", new Func<string?, string>(str =>
        {
            return str?.Trim() ?? "";
        }));

        // formatDate: 日期格式化（dayjs 的简易替代）
        engine.SetValue("formatDate", new Func<string?, string?, string?>(
            (str, format) =>
            {
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (!DateTime.TryParse(str, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return null;
                return dt.ToString(format ?? "yyyy-MM-dd");
            }));

        // lookup: 查表映射（预留占位，先返回 key 本身）
        engine.SetValue("lookup", new Func<string?, string?, string?, string?>(
            (tableName, key, valueCol) =>
            {
                // TODO: 后续实现真正的查表映射逻辑
                return key;
            }));
    }

    private static object? ConvertJsValue(Jint.Native.JsValue jsValue)
    {
        if (jsValue.IsNull() || jsValue.IsUndefined())
            return null;
        if (jsValue.IsString())
            return jsValue.AsString();
        if (jsValue.IsNumber())
            return jsValue.AsNumber().ToString();
        if (jsValue.IsBoolean())
            return jsValue.AsBoolean() ? "true" : "false";
        return jsValue.ToString();
    }
}
