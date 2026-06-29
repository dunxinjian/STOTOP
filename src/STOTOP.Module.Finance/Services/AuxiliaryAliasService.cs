using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services;

public class AuxiliaryAliasService
{
    private readonly STOTOPDbContext _dbContext;

    public AuxiliaryAliasService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AuxiliaryAliasDto>> GetAllAsync(string? auxType)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

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
LEFT JOIN [FIN辅助核算项目] 项目 ON 别名.[F辅助核算项目ID] = 项目.[FID]
WHERE (@AuxType IS NULL OR 别名.[F辅助类型] = @AuxType)
ORDER BY 别名.[F辅助类型], 项目.[F名称]";

        var result = await connection.QueryAsync<AuxiliaryAliasDto>(sql, new { AuxType = auxType });
        return result.ToList();
    }

    public async Task<AuxiliaryAliasDto?> CreateAsync(AuxiliaryAliasDto dto)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

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

        // 查询完整记录返回
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
LEFT JOIN [FIN辅助核算项目] 项目 ON 别名.[F辅助核算项目ID] = 项目.[FID]
WHERE 别名.[FID] = @Id";

        return await connection.QueryFirstOrDefaultAsync<AuxiliaryAliasDto>(querySql, new { dto.Id });
    }

    public async Task<AuxiliaryAliasDto?> UpdateAsync(Guid id, AuxiliaryAliasDto dto)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        const string updateSql = @"
UPDATE [FIN辅助核算别名] SET [F别名]=@Alias, [F辅助类型]=@AuxType, [F辅助核算项目ID]=@AuxiliaryItemId WHERE [FID]=@Id";

        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = id,
            dto.Alias,
            dto.AuxType,
            dto.AuxiliaryItemId
        });

        if (affected == 0) return null;

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
LEFT JOIN [FIN辅助核算项目] 项目 ON 别名.[F辅助核算项目ID] = 项目.[FID]
WHERE 别名.[FID] = @Id";

        return await connection.QueryFirstOrDefaultAsync<AuxiliaryAliasDto>(querySql, new { Id = id });
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        const string sql = "DELETE FROM [FIN辅助核算别名] WHERE [FID]=@Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
