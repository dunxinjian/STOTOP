namespace STOTOP.Module.System.Entities;

/// <summary>
/// 组织节点类别（M4，多租户阶段2）。SysOrganization.FKind 的单一真源，驱动树合法性 / 范围根 / 闭包逻辑。
/// 与 design/23 §4.3 一致；从 SYS组织类型.FKind 派生回填（typeCode→FKind 映射见 BasicDataSeeder.EnsureOrgTypes）。
/// </summary>
public enum OrgKind
{
    /// <summary>集团（客户/租户根，如 MDSTO）</summary>
    Group = 0,
    /// <summary>区域公司（如 石家庄申通/太仓美申，R8 范围层）</summary>
    Region = 1,
    /// <summary>网点公司（如 城区/南郊/沙溪/浏河公司，经营单元 1:1 派生源）</summary>
    Company = 2,
    /// <summary>中心（运营中心分组网点公司 / 管理中心分组部门）</summary>
    Center = 3,
    /// <summary>部门（管理层，允许深部门链嵌套）</summary>
    Dept = 4,
    /// <summary>班组（叶层）</summary>
    Team = 5,
}

/// <summary>
/// R8 数据范围根类型（4 级，多租户阶段2 · 用户裁定）。design/23 原文 3 级(TENANT/CENTER/COMPANY)为 v1"区域公司=租户"术语；
/// v2 集团=租户下辖多区域公司，故需"区域公司"作为独立范围层：集团(全租户汇总) ⊃ 区域公司 ⊃ 中心 ⊃ 网点公司。
/// </summary>
public enum OrgScopeType
{
    /// <summary>集团级：全租户可视（集团总部汇总）</summary>
    Group = 1,
    /// <summary>区域公司级：本区域公司闭包子树</summary>
    Region = 2,
    /// <summary>中心级：某运营中心闭包下所有网点公司</summary>
    Center = 3,
    /// <summary>网点公司级：单个网点公司</summary>
    Company = 4,
}
