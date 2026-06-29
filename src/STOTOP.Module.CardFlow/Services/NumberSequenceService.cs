using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class NumberSequenceService : INumberSequenceService
{
    private readonly STOTOPDbContext _dbContext;

    public NumberSequenceService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNumberAsync(long flowDefinitionId, long orgId, string template)
    {
        var now = DateTime.Now;
        var year = now.Year;

        // 使用行锁获取并递增序号（并发安全）
        var sequence = await GetAndIncrementSequenceAsync(flowDefinitionId, orgId, year);

        // 解析模板替换
        var result = template
            .Replace("{yyyy}", now.ToString("yyyy"))
            .Replace("{MM}", now.ToString("MM"))
            .Replace("{dd}", now.ToString("dd"));

        // 替换 {seq3} ~ {seq6}
        result = Regex.Replace(result, @"\{seq(\d)\}", match =>
        {
            int digits = int.Parse(match.Groups[1].Value);
            return sequence.ToString().PadLeft(digits, '0');
        });

        // 替换不带位数的 {seq}（默认 4 位）
        result = result.Replace("{seq}", sequence.ToString().PadLeft(4, '0'));

        return result;
    }

    private async Task<int> GetAndIncrementSequenceAsync(long flowDefinitionId, long orgId, int year)
    {
        // 从实体模型动态获取表名和列名，避免与中文列名/表名不匹配
        var entityType = _dbContext.Model.FindEntityType(typeof(CfNumberSequence))
            ?? throw new InvalidOperationException("未找到 CfNumberSequence 实体映射");
        var tableName = entityType.GetTableName() ?? "CF编号序号";

        string ColName(string propertyName)
        {
            var prop = entityType.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"未找到属性 {propertyName}");
            var storeId = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            return prop.GetColumnName(storeId) ?? prop.Name;
        }
        var colFlow = ColName(nameof(CfNumberSequence.FFlowDefinitionId));
        var colOrg = ColName(nameof(CfNumberSequence.FOrgId));
        var colYear = ColName(nameof(CfNumberSequence.FYear));
        var colSeq = ColName(nameof(CfNumberSequence.FCurrentSequence));
        var colUpd = ColName(nameof(CfNumberSequence.FUpdatedTime));

        var sql = $@"
            DECLARE @seq INT;
            
            MERGE [{tableName}] WITH (HOLDLOCK) AS target
            USING (SELECT @p0 AS V_Flow, @p1 AS V_Org, @p2 AS V_Year) AS source
            ON target.[{colFlow}] = source.V_Flow
               AND target.[{colOrg}] = source.V_Org 
               AND target.[{colYear}] = source.V_Year
            WHEN MATCHED THEN
                UPDATE SET [{colSeq}] = [{colSeq}] + 1, [{colUpd}] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([{colFlow}], [{colOrg}], [{colYear}], [{colSeq}], [{colUpd}])
                VALUES (source.V_Flow, source.V_Org, source.V_Year, 1, GETDATE());
            
            SELECT @seq = [{colSeq}] FROM [{tableName}] WITH (UPDLOCK, ROWLOCK)
            WHERE [{colFlow}] = @p0 AND [{colOrg}] = @p1 AND [{colYear}] = @p2;
            
            SELECT @seq;";

        var connection = _dbContext.Database.GetDbConnection();
        await _dbContext.Database.OpenConnectionAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var p0 = command.CreateParameter(); p0.ParameterName = "@p0"; p0.Value = flowDefinitionId;
            var p1 = command.CreateParameter(); p1.ParameterName = "@p1"; p1.Value = orgId;
            var p2 = command.CreateParameter(); p2.ParameterName = "@p2"; p2.Value = year;
            command.Parameters.Add(p0);
            command.Parameters.Add(p1);
            command.Parameters.Add(p2);

            if (_dbContext.Database.CurrentTransaction != null)
                command.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            // Connection lifecycle managed by EF
        }
    }
}
