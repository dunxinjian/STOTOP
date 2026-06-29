/**
 * 菜单元数据解析（轻量版）
 *
 * 当前仅基于路径片段推断 label。后续可扩展为：
 *  1) 从 MODULE_TABS 注册表查找模块前缀映射
 *  2) 从权限菜单（permissionStore.menus）按 route 字段查找标准 name
 */
export interface MenuMeta {
  icon?: string
  label?: string
}

/**
 * 根据路由路径解析菜单元数据。
 * 暂时返回路径最后一段作为 label，icon 留空。
 */
export function resolveMenuMeta(path: string): MenuMeta {
  if (!path) return { label: '' }
  const segments = path.split('/').filter(Boolean)
  return { label: segments[segments.length - 1] || path }
}
