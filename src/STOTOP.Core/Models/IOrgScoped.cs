namespace STOTOP.Core.Models;

/// <summary>
/// 标记需要按组织隔离的实体。
/// 实现此接口的实体将自动被 EF Core 全局过滤器按当前组织过滤。
/// 去模板化后：FOrgId 必须 > 0，跨组织复制请通过 Service 层临时切换 IOrgContextAccessor.CurrentOrgId 实现。
/// </summary>
public interface IOrgScoped
{
    long FOrgId { get; set; }
}
