/**
 * 模块配置中心 — 所有模块的菜单前缀、路由前缀、路由module值集中定义于此。
 * permission.ts 和 router/index.ts 均从此文件导入，避免循环依赖。
 */

export interface ModuleConfigEntry {
  menuPrefixes: string[]   // 菜单code前缀（运行时用 prefix: 精确匹配）
  routePrefix: string      // 路由前缀
  routeModule: string      // 路由 meta.module 值
}

export const MODULE_CONFIG: Record<string, ModuleConfigEntry> = {
  finance:    { menuPrefixes: ['finance'],   routePrefix: '/finance',    routeModule: 'finance' },
  cardflow:   { menuPrefixes: ['cardflow'],  routePrefix: '/cardflow',   routeModule: 'cardflow' },
  hr:         { menuPrefixes: ['hr'],        routePrefix: '/hr',         routeModule: 'hr' },
  dormitory:  { menuPrefixes: ['dormitory'], routePrefix: '/dormitory',  routeModule: 'dormitory' },
  vehicle:    { menuPrefixes: ['vehicle'],   routePrefix: '/vehicle',    routeModule: 'vehicle' },
  contract:   { menuPrefixes: ['contract'],  routePrefix: '/contract',   routeModule: 'contract' },
  supplier:   { menuPrefixes: ['supplier'],  routePrefix: '/supplier',   routeModule: 'supplier' },
  points:     { menuPrefixes: ['points'],    routePrefix: '/points',     routeModule: 'points' },
  report:     { menuPrefixes: ['report'],    routePrefix: '/report',     routeModule: 'report' },
  system:     { menuPrefixes: ['sys'],       routePrefix: '/system',     routeModule: 'system' },
  task:       { menuPrefixes: ['task'],      routePrefix: '/task',       routeModule: 'task' },
  express:    { menuPrefixes: ['express'],   routePrefix: '/express',    routeModule: 'express' },
  insurance:  { menuPrefixes: ['insurance'], routePrefix: '/insurance',  routeModule: 'insurance' },
  crm:        { menuPrefixes: ['crm'],       routePrefix: '/crm',        routeModule: 'crm' },
  quality:    { menuPrefixes: ['quality'],   routePrefix: '/quality',    routeModule: 'quality' },
  conference: { menuPrefixes: ['conf'],      routePrefix: '/conference', routeModule: 'conference' },
}

/** 派生：模块代码到菜单code前缀的映射（向后兼容） */
export const MODULE_MENU_CODE_MAP: Record<string, string[]> = Object.fromEntries(
  Object.entries(MODULE_CONFIG).map(([k, v]) => [k, v.menuPrefixes])
)

/**
 * 精确前缀匹配：code 必须等于 prefix 或以 prefix: 开头
 */
export function matchesModulePrefix(code: string, prefixes: string[]): boolean {
  const codeLower = code.toLowerCase()
  return prefixes.some(prefix =>
    codeLower === prefix || codeLower.startsWith(prefix + ':')
  )
}

/**
 * 从路由路径推断模块代码
 */
export function inferModuleFromPath(path: string): string | undefined {
  const segment = path.split('/')[1]
  if (!segment) return undefined
  for (const [moduleCode, config] of Object.entries(MODULE_CONFIG)) {
    if (config.routePrefix === `/${segment}`) return moduleCode
  }
  return undefined
}
