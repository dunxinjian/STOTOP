namespace STOTOP.Infrastructure.Data;

/// <summary>
/// 数据库初始化接口，统一执行新库初始化、启动修补和基线校验
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// 启动时执行幂等修补和基础数据补齐。
    /// </summary>
    void MigrateAll(STOTOPDbContext context);

    /// <summary>
    /// 面向空库/全新库的初始化流程：结构校验、基础数据写入、最终基线校验。
    /// </summary>
    DatabaseInitializationReport InitializeNewDatabase(STOTOPDbContext context);

    /// <summary>
    /// 校验当前数据库是否满足新库 baseline 要求。
    /// </summary>
    DatabaseInitializationReport ValidateDatabase(STOTOPDbContext context);
}

public class DatabaseInitializationReport
{
    public bool Success => Issues.Count == 0;
    public List<string> Steps { get; } = new();
    public List<string> Issues { get; } = new();
}
