namespace STOTOP.Core.Interfaces;

/// <summary>
/// 数据作用域撤销处理器接口。
/// 各业务模块（暂存表、凭证等）实现此接口，由 RevokeService 统一调度。
/// </summary>
public interface IDataScopeRevokeHandler
{
    /// <summary>Handler 名称（用于日志标识）</summary>
    string HandlerName { get; }

    /// <summary>
    /// 按 DataScopeId 撤销业务数据
    /// </summary>
    /// <param name="dataScopeId">数据作用域ID</param>
    /// <param name="operatorId">操作人ID</param>
    /// <returns>影响的记录数</returns>
    Task<int> RevokeByDataScopeAsync(string dataScopeId, long operatorId);
}
