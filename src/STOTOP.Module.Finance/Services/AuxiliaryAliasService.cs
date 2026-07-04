using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 辅助核算别名 CRUD（裸 Dapper）。别名本身仅有 legacy Guid 组织列(与全库 long 组织模型不同源、无桥)，
/// 故一律按「所链辅助核算项目(FIN辅助核算项目)的账套(F账套ID)」做隔离——账套由控制器经 X-AccountSet-Id 头传入。
/// 缺少账套上下文(accountSetId&lt;=0)一律 fail-closed（读空、写拒）。(F52)
/// </summary>
public class AuxiliaryAliasService
{
    private readonly STOTOPDbContext _dbContext;

    public AuxiliaryAliasService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private async Task<global::System.Data.Common.DbConnection> OpenAsync()
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        return connection;
    }

    /// <summary>校验辅助核算项目属于当前账套（防按任意 itemId 把别名挂到他账套项目上）。</summary>
    private static async Task<bool> ItemBelongsToAccountSetAsync(
        global::System.Data.Common.DbConnection connection, long auxiliaryItemId, long accountSetId)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM [FIN辅助核算项目] WHERE [FID] = @ItemId AND [F账套ID] = @AccountSetId",
            new { ItemId = auxiliaryItemId, AccountSetId = accountSetId });
        return count > 0;
    }

    public async Task<List<AuxiliaryAliasDto>> GetAllAsync(string? auxType, long accountSetId)
    {
        if (accountSetId <= 0) return new List<AuxiliaryAliasDto>(); // 缺账套上下文 → fail-closed 读空

        var connection = await OpenAsync();

        // 按所链项目账套隔离：LEFT JOIN + 项目账套过滤 等效仅取本账套项目的别名，堵跨账套读。
        const string sql = @"
SELECT
    别名.[FID] AS Id,
    别名.[F辅助核算项目ID] AS AuxiliaryItemId,
    项目.[F名称] AS AuxiliaryItemName,
    项目.[F编码] AS AuxiliaryItemCode,
    别名.[F别名] AS [Alias],
    别名.[F辅助类型] AS AuxType,
    别名.[F组织ID] AS OrganizationId
FROM [FIN辅助核算别名] 别名
INNER JOIN [FIN辅助核算项目] 项目 ON 别名.[F辅助核算项目ID] = 项目.[FID]
WHERE 项目.[F账套ID] = @AccountSetId AND (@AuxType IS NULL OR 别名.[F辅助类型] = @AuxType)
ORDER BY 别名.[F辅助类型], 项目.[F名称]";

        var result = await connection.QueryAsync<AuxiliaryAliasDto>(sql, new { AuxType = auxType, AccountSetId = accountSetId });
        return result.ToList();
    }

    public async Task<AuxiliaryAliasDto?> CreateAsync(AuxiliaryAliasDto dto, long accountSetId)
    {
        if (accountSetId <= 0) throw new InvalidOperationException("缺少账套上下文，无法创建别名");

        var connection = await OpenAsync();

        if (!await ItemBelongsToAccountSetAsync(connection, dto.AuxiliaryItemId, accountSetId))
            throw new InvalidOperationException("辅助核算项目不存在或不属于当前账套");

        dto.Id = Guid.NewGuid();

        const string insertSql = @"
INSERT INTO [FIN辅助核算别名] ([FID], [F辅助核算项目ID], [F别名], [F辅助类型], [F组织ID])
VALUES (@Id, @AuxiliaryItemId, @Alias, @AuxType, @OrganizationId)";

        await connection.ExecuteAsync(insertSql, new
        {
            dto.Id,
            dto.AuxiliaryItemId,
            dto.Alias,
            dto.AuxType,
            dto.OrganizationId
        });

        return await GetByIdScopedAsync(connection, dto.Id, accountSetId);
    }

    public async Task<AuxiliaryAliasDto?> UpdateAsync(Guid id, AuxiliaryAliasDto dto, long accountSetId)
    {
        if (accountSetId <= 0) throw new InvalidOperationException("缺少账套上下文，无法更新别名");

        var connection = await OpenAsync();

        // 目标项目也须属本账套（防把别名改挂到他账套项目）
        if (!await ItemBelongsToAccountSetAsync(connection, dto.AuxiliaryItemId, accountSetId))
            throw new InvalidOperationException("辅助核算项目不存在或不属于当前账套");

        // 仅当被改别名当前所链项目属本账套时才放行，堵跨账套改
        const string updateSql = @"
UPDATE 别名 SET [F别名]=@Alias, [F辅助类型]=@AuxType, [F辅助核算项目ID]=@AuxiliaryItemId
FROM [FIN辅助核算别名] 别名
WHERE 别名.[FID]=@Id
  AND EXISTS (SELECT 1 FROM [FIN辅助核算项目] 项目
              WHERE 项目.[FID]=别名.[F辅助核算项目ID] AND 项目.[F账套ID]=@AccountSetId)";

        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = id,
            dto.Alias,
            dto.AuxType,
            dto.AuxiliaryItemId,
            AccountSetId = accountSetId
        });

        if (affected == 0) return null;

        return await GetByIdScopedAsync(connection, id, accountSetId);
    }

    public async Task<bool> DeleteAsync(Guid id, long accountSetId)
    {
        if (accountSetId <= 0) throw new InvalidOperationException("缺少账套上下文，无法删除别名");

        var connection = await OpenAsync();

        const string sql = @"
DELETE 别名 FROM [FIN辅助核算别名] 别名
WHERE 别名.[FID]=@Id
  AND EXISTS (SELECT 1 FROM [FIN辅助核算项目] 项目
              WHERE 项目.[FID]=别名.[F辅助核算项目ID] AND 项目.[F账套ID]=@AccountSetId)";
        var affected = await connection.ExecuteAsync(sql, new { Id = id, AccountSetId = accountSetId });
        return affected > 0;
    }

    private static async Task<AuxiliaryAliasDto?> GetByIdScopedAsync(
        global::System.Data.Common.DbConnection connection, Guid id, long accountSetId)
    {
        const string querySql = @"
SELECT
    别名.[FID] AS Id,
    别名.[F辅助核算项目ID] AS AuxiliaryItemId,
    项目.[F名称] AS AuxiliaryItemName,
    项目.[F编码] AS AuxiliaryItemCode,
    别名.[F别名] AS [Alias],
    别名.[F辅助类型] AS AuxType,
    别名.[F组织ID] AS OrganizationId
FROM [FIN辅助核算别名] 别名
INNER JOIN [FIN辅助核算项目] 项目 ON 别名.[F辅助核算项目ID] = 项目.[FID]
WHERE 别名.[FID] = @Id AND 项目.[F账套ID] = @AccountSetId";
        return await connection.QueryFirstOrDefaultAsync<AuxiliaryAliasDto>(querySql, new { Id = id, AccountSetId = accountSetId });
    }
}
