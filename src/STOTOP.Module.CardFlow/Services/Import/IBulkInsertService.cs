using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Import;

public interface IBulkInsertService
{
    Task BulkInsertErrorsAsync(List<CfBatchError> records, CancellationToken ct = default);

    /// <summary>
    /// 将已映射的行数据批量写入目标业务表（目标库由调用方传入连接字符串）。
    /// </summary>
    /// <param name="connectionString">目标数据库连接字符串</param>
    /// <param name="targetTableName">目标表名（支持中文表名，内部会加方括号）</param>
    /// <param name="rows">每行为 key=列名、value=值 的字典，key 即目标表列名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实际写入行数</returns>
    Task<int> BulkInsertTargetAsync(
        string connectionString,
        string targetTableName,
        List<Dictionary<string, object?>> rows,
        CancellationToken ct = default);
}
