using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly STOTOPDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(STOTOPDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // 每个实体类型的单列主键属性名缓存（模型跨上下文一致，可静态缓存）。
    // 不硬编码 "FID"——BaseEntity 主键为 FID，但也有实体主键名为 Id（如 ExpPriceSurchargeScope），
    // 硬编码会使其 GetByIdAsync/DeleteAsync 在查询翻译期抛异常。
    private static string? _keyPropertyName;

    public async Task<T?> GetByIdAsync(long id)
    {
        // 经全局查询过滤器（组织/租户 fail-closed 硬墙）按主键取，杜绝裸 DbSet.FindAsync 绕过滤器造成的
        // 越权直查（IDOR）。主键属性名从模型元数据解析（与 FindAsync 内部同源），仅支持单列主键；
        // AsTracking 保留 FindAsync「返回受跟踪实体」的语义（全局默认 NoTrackingWithIdentityResolution），使 Update/Delete 等下游改动照旧生效。
        var keyName = _keyPropertyName ??=
            _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties is { Count: 1 } keyProps
                ? keyProps[0].Name
                : "FID";
        return await _dbSet.AsTracking().FirstOrDefaultAsync(e => EF.Property<long>(e, keyName) == id);
    }

    public async Task<List<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }
}
