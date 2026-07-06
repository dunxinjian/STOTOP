using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>
/// 新租户自动开通编排（多租户阶段4·R5）。把 design/23 §10 的开通链落成一个事务：
/// 建组织根 → 物化闭包 → 建 PLT租户（FID=根组织FID，保不变量）→ 建初始管理员用户（随机密码）
/// → 建租户私有 admin 角色 → 建成员(已接受)+主任职 → 重算 R8 派生授权。
/// 仅平台超管在平台作用域下调用（经 <see cref="Filters.PlatformOnlyAttribute"/>）。
/// </summary>
public interface IProvisionTenantService
{
    Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest request);
}
