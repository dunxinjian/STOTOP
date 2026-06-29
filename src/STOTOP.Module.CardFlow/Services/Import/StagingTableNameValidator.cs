using System.Text.RegularExpressions;

namespace STOTOP.Module.CardFlow.Services.Import;

/// <summary>
/// 暂存表名安全校验工具类，防止动态表名拼接 SQL 注入
/// </summary>
public static class StagingTableNameValidator
{
    /// <summary>
    /// 合法表名正则：仅允许中文、字母、数字、下划线
    /// </summary>
    private static readonly Regex SafeTableNameRegex = new(@"^[\u4e00-\u9fff\w]+$", RegexOptions.Compiled);

    /// <summary>
    /// 校验暂存表名是否合法，不合法则抛出异常
    /// </summary>
    public static void EnsureSafe(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || !SafeTableNameRegex.IsMatch(tableName))
            throw new InvalidOperationException($"暂存表名不合法：{tableName}");
    }
}
