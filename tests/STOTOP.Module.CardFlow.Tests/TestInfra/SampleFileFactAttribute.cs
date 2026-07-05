using Xunit;

namespace STOTOP.Module.CardFlow.Tests.TestInfra;

/// <summary>
/// 依赖本机真实样本文件的 Fact：样本根目录先取环境变量 STOTOP_SAMPLE_DIR，
/// 未设则回退 <see cref="DefaultSampleDir"/>（有 E 盘样本的机器零配置不变）；
/// 任一样本缺失时整例 Skip 而非红。
/// </summary>
public sealed class SampleFileFactAttribute : FactAttribute
{
    private const string DefaultSampleDir = @"E:\STOTOP_Fable\Taicang";

    public SampleFileFactAttribute(params string[] sampleRelativePaths)
    {
        var root = ResolveSampleDir();
        var missing = sampleRelativePaths
            .Select(rel => Path.Combine(root, rel))
            .Where(full => !File.Exists(full))
            .ToList();
        if (missing.Count > 0)
        {
            Skip = $"样本文件不存在（设 STOTOP_SAMPLE_DIR 或恢复 {root}）：{string.Join("；", missing)}";
        }
    }

    /// <summary>测试体内拼样本绝对路径统一走这里，别再硬编码根目录。</summary>
    public static string ResolveSampleDir() =>
        Environment.GetEnvironmentVariable("STOTOP_SAMPLE_DIR") is { Length: > 0 } dir ? dir : DefaultSampleDir;
}
