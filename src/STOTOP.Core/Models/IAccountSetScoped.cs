namespace STOTOP.Core.Models;

/// <summary>
/// 标记"账套级共享"实体：按 FAccountSetId 限定，不随组织隔离。
/// 与 IOrgScoped 互斥——实现本接口的实体【不】被组织全局查询过滤器约束。
/// 账套本身已按组织授权（FinAccountSetAuthorization），账套即访问边界。
/// </summary>
public interface IAccountSetScoped
{
    long FAccountSetId { get; set; }
}
