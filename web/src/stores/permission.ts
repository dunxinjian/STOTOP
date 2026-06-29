import { defineStore } from 'pinia'
import { ref, shallowRef, computed } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import type { MenuItem } from '@/api/auth'
import { get } from '@/api/request'
import { layoutRoute } from '@/router/routes'

const viewModules = import.meta.glob('../views/**/*.vue')

import { MODULE_CONFIG, MODULE_MENU_CODE_MAP, matchesModulePrefix } from '@/config/modules'
export { MODULE_CONFIG, MODULE_MENU_CODE_MAP }

/**
 * 模块可见性类型（从 MODULE_CONFIG 的 key 动态派生）
 */
export type ModuleVisibility = Record<keyof typeof MODULE_CONFIG, boolean>

function componentPathToModule(componentPath: string) {
  // 标准路径
  const key = `../views/${componentPath}.vue`
  if (viewModules[key]) {
    return viewModules[key]
  }

  // 尝试 index.vue
  const indexKey = `../views/${componentPath}/index.vue`
  if (viewModules[indexKey]) {
    return viewModules[indexKey]
  }

  // Fallback: 在 viewModules 中模糊搜索匹配的键（处理路径格式差异）
  const normalizedPath = componentPath.replace(/\\/g, '/').toLowerCase()
  for (const moduleKey of Object.keys(viewModules)) {
    const normalizedModuleKey = moduleKey.replace(/\\/g, '/').toLowerCase()
    if (normalizedModuleKey.endsWith(`/${normalizedPath}.vue`) || normalizedModuleKey.endsWith(`/${normalizedPath}/index.vue`)) {
      return viewModules[moduleKey]
    }
  }

  // 开发环境调试日志
  if (import.meta.env.DEV) {
    console.warn(`[Permission] Component not found for path: ${componentPath}`, 'Available keys:', Object.keys(viewModules).filter(k => k.includes(componentPath.split('/')[0])))
  }

  return null
}

/**
 * 将树形菜单扁平化（处理 orgContext 返回的已构建树结构）
 */
function flattenMenuTree(menus: MenuItem[]): MenuItem[] {
  const result: MenuItem[] = []
  for (const menu of menus) {
    // Push the menu itself (without nested children reference to avoid duplication)
    result.push(menu)
    if (menu.children && menu.children.length > 0) {
      result.push(...flattenMenuTree(menu.children))
    }
  }
  return result
}

/**
 * 将扁平菜单列表转为树形结构
 */
function buildMenuTree(flatMenus: MenuItem[]): MenuItem[] {
  const map = new Map<number, MenuItem & { children: MenuItem[] }>()
  const roots: MenuItem[] = []

  // 第一遍：所有菜单项放入map，初始化children
  for (const menu of flatMenus) {
    map.set(menu.id, { ...menu, children: [] })
  }

  // 第二遍：建立父子关系，找不到父节点的孤儿节点提升为root（避免被静默丢弃）
  for (const menu of flatMenus) {
    const node = map.get(menu.id)!
    if (menu.parentId && menu.parentId !== 0) {
      const parent = map.get(menu.parentId)
      if (parent) {
        parent.children.push(node)
      } else {
        // 防御性处理：父节点缺失时，将该节点作为根节点保留
        if (import.meta.env.DEV) {
          console.warn(`[buildMenuTree] 孤儿菜单节点: id=${menu.id}, code=${menu.code}, parentId=${menu.parentId}`)
        }
        roots.push(node)
      }
    } else {
      roots.push(node)
    }
  }

  return roots
}

function buildRoutes(menus: MenuItem[], module?: string): RouteRecordRaw[] {
  const routes: RouteRecordRaw[] = []
  for (const menu of menus) {
    if (menu.type === 'button') continue

    // 根据 code 推断模块
    const inferredModule = inferModuleFromCode(menu.code)

    // module 类型：递归处理 children，并为有 path 的模块创建重定向路由
    if (menu.type === 'module') {
      if (menu.children && menu.children.length > 0) {
        const childRoutes = buildRoutes(menu.children, inferredModule || module)
        routes.push(...childRoutes)

        // 为模块自身创建重定向路由，指向第一个可路由的子菜单
        if (menu.route) {
          const firstChild = menu.children.find(c => c.type !== 'button' && c.route)
          if (firstChild && firstChild.route) {
            routes.push({
              path: menu.route,
              name: menu.code,
              redirect: firstChild.route,
              meta: {
                title: menu.name,
                icon: menu.icon,
                menuId: menu.id,
                module: inferredModule || module,
              },
            })
          }
        }
      }
      continue
    }

    // 只有 menu 类型才创建路由
    if (menu.route && menu.componentPath) {
      const component = componentPathToModule(menu.componentPath)
      if (component) {
        const route: RouteRecordRaw = {
          path: menu.route,
          name: menu.code,
          component,
          meta: {
            title: menu.name,
            icon: menu.icon,
            menuId: menu.id,
            module: inferredModule || module,
          },
        }
        routes.push(route)
      }
    } else if (menu.route && !menu.componentPath && menu.children && menu.children.length > 0) {
      // 父菜单无组件但有子菜单：自动重定向到第一个有路由的子菜单
      const firstChild = menu.children.find(c => c.type !== 'button' && c.route)
      if (firstChild && firstChild.route) {
        const redirectRoute: RouteRecordRaw = {
          path: menu.route,
          name: menu.code,
          redirect: firstChild.route,
          meta: {
            title: menu.name,
            icon: menu.icon,
            menuId: menu.id,
            module: inferredModule || module,
          },
        }
        routes.push(redirectRoute)
      }
    }
    if (menu.children && menu.children.length > 0) {
      const childRoutes = buildRoutes(menu.children, inferredModule || module)
      routes.push(...childRoutes)
    }
  }
  return routes
}

/**
 * 根据 code 推断模块归属
 */
function inferModuleFromCode(code: string): string | undefined {
  for (const [module, config] of Object.entries(MODULE_CONFIG)) {
    if (matchesModulePrefix(code, config.menuPrefixes)) {
      return module
    }
  }
  return undefined
}

/**
 * 根据模块代码过滤菜单
 * 
 * 过滤规则：
 * - 使用精确前缀匹配（code === prefix 或 code 以 prefix: 开头）
 * - isVisible=0 的菜单仅从侧边栏隐藏，不影响路由注册和权限验证
 */
function filterMenusByModule(menus: MenuItem[], moduleCode: string): MenuItem[] {
  const config = MODULE_CONFIG[moduleCode]
  if (!config) return []

  const prefixes = config.menuPrefixes
  const result: MenuItem[] = []
  const matchedRoots: string[] = []

  for (const menu of menus) {
    if (matchesModulePrefix(menu.code, prefixes)) {
      matchedRoots.push(`${menu.code}(id=${menu.id})`)
      // 过滤掉 isVisible=0 的隐藏路由，不在侧边栏菜单中展示
      const children = menu.children || []
      const filtered = children.filter(child => child.isVisible !== 0)
      if (filtered.length > 0) {
        result.push(...filtered)
      } else if (menu.type !== 'button') {
        // 防御性处理：匹配的根菜单没有可见子项时，将根菜单自身加入结果
        //（防止孤儿节点或叶子菜单在搜索中消失）
        result.push(menu)
      }
    }
  }

  if (import.meta.env.DEV && moduleCode === 'cardflow') {
    if (result.length === 0) {
      console.warn('[filterMenusByModule] 未找到 cardflow 根菜单！根菜单 code 列表=', menus.map(m => m.code))
    } else {
      console.log('[filterMenusByModule] cardflow 匹配根菜单=', matchedRoots.join(', '))
      console.log('[filterMenusByModule] 汇总 children 数=', result.length)
    }
  }
  return result
}

/**
 * 从静态路由定义生成菜单项（fallback 机制）
 */
function generateMenusFromRoutes(moduleCode: string): MenuItem[] {
  const routePrefix = MODULE_CONFIG[moduleCode]?.routePrefix
  if (!routePrefix) return []

  // 获取路由中实际使用的模块代码
  const routeModule = MODULE_CONFIG[moduleCode]?.routeModule || moduleCode

  // 查找对应模块的路由定义
  const moduleRoute = layoutRoute.children?.find(
    child => child.path === routePrefix.slice(1) // 去掉前导斜杠匹配
  )

  if (!moduleRoute || !moduleRoute.children?.length) {
    // 如果没有嵌套路由，直接从路由生成菜单
    return generateMenuItemsFromRoutes(layoutRoute.children || [], routeModule)
  }

  // 从嵌套子路由生成菜单
  return generateMenuItemsFromRoutes(moduleRoute.children, routeModule, 1000, `/${moduleRoute.path}`)
}

/**
 * 从路由数组生成菜单项
 */
function generateMenuItemsFromRoutes(
  routes: RouteRecordRaw[],
  routeModule: string,
  startId: number = 1000,
  parentPath: string = ''
): MenuItem[] {
  const menus: MenuItem[] = []
  let idCounter = startId

  for (const route of routes) {
    // 跳过隐藏的路由
    if ((route.meta as any)?.hidden) continue

    // 检查路由是否属于该模块
    const metaModule = (route.meta as any)?.module
    if (metaModule && metaModule !== routeModule) continue

    // 计算完整路径
    const fullPath = route.path.startsWith('/')
      ? route.path
      : `${parentPath}/${route.path}`.replace(/\/+/g, '/')

    const menu: MenuItem = {
      id: idCounter++,
      name: (route.meta as any)?.title || route.name?.toString() || '',
      code: route.name?.toString() || `menu_${idCounter}`,
      icon: (route.meta as any)?.icon,
      route: fullPath,
      type: 'menu',
    }

    // 处理子路由
    if (route.children && route.children.length > 0) {
      const childMenus = generateMenuItemsFromRoutes(route.children, routeModule, idCounter, fullPath)
      if (childMenus.length > 0) {
        menu.children = childMenus
        idCounter = startId + childMenus.length * 100 // 避免ID冲突
      }
    }

    menus.push(menu)
  }

  return menus
}

export const usePermissionStore = defineStore('permission', () => {
  const menus = ref<MenuItem[]>([])
  const routes = shallowRef<RouteRecordRaw[]>([])
  const oaTodoCount = ref<number>(0)
  const taskCount = ref<number>(0)
  const voucherPendingCount = ref<number>(0)

  function generateRoutes(menuData: MenuItem[]): RouteRecordRaw[] {
    // Handle both flat and tree input: flatten first to ensure buildMenuTree works correctly
    // (orgContext may pass already-tree-structured menus)
    const flatMenus = flattenMenuTree(menuData)
    menus.value = flatMenus

    const treeMenus = buildMenuTree(flatMenus)
    const dynamicRoutes = buildRoutes(treeMenus)
    routes.value = dynamicRoutes

    if (import.meta.env.DEV) {
      const cardflowMenus = flatMenus.filter(m => m.code?.toLowerCase().includes('cardflow') || m.code?.toLowerCase().includes('flow-definition') || m.code?.toLowerCase().includes('orchestration'))
      console.log('[Permission] generateRoutes: cardflow 相关菜单数=', cardflowMenus.length, cardflowMenus.map(m => ({ id: m.id, code: m.code, name: m.name, type: m.type, parentId: m.parentId, route: m.route })))
    }

    return dynamicRoutes
  }

  /**
   * 获取当前模块的菜单（需要传入 currentModule）
   * 优先使用后端返回的菜单，如果为空则从静态路由生成（fallback）
   */
  function getCurrentModuleMenus(currentModule: string): MenuItem[] {
    if (currentModule === 'workhub') return []
    
    // menus.value 存储的是后端返回的扁平列表，需要先构建树再过滤
    const treeMenus = buildMenuTree(menus.value)
    const backendMenus = filterMenusByModule(treeMenus, currentModule)

    if (import.meta.env.DEV && backendMenus.length === 0 && menus.value.length > 0) {
      const moduleCodes = treeMenus.map(m => m.code)
      console.warn(`[Permission] getCurrentModuleMenus('${currentModule}'): 后端菜单为空，尝试 fallback。menus.value 总数=${menus.value.length}，树根数=${treeMenus.length}，所有根菜单code=[${moduleCodes.join(', ')}]`)
    }

    if (backendMenus.length > 0) {
      return backendMenus
    }
    
    // Fallback: 从静态路由生成菜单
    return generateMenusFromRoutes(currentModule)
  }

  /**
   * 计算模块可见性
   * 所有模块均基于用户实际菜单权限判断，不硬编码始终可见
   */
  function getModuleVisibility(_permissions: string[]): ModuleVisibility {
    const hasModuleMenu = (moduleCode: string): boolean => {
      const config = MODULE_CONFIG[moduleCode]
      if (!config) return false
      return menus.value.some(menu => matchesModulePrefix(menu.code, config.menuPrefixes))
    }

    const result = {} as ModuleVisibility
    for (const moduleCode of Object.keys(MODULE_CONFIG)) {
      (result as any)[moduleCode] = hasModuleMenu(moduleCode)
    }
    return result
  }

  /**
   * 判断用户是否有某个模块的访问权限
   */
  function hasModuleAccess(moduleCode: string, permissions: string[]): boolean {
    const visibility = getModuleVisibility(permissions)
    return visibility[moduleCode as keyof ModuleVisibility] ?? false
  }

  /**
   * 获取模块菜单分组数据，供 Mega Menu 使用
   * @param moduleCode 模块 code（如 'finance', 'hr'）
   * @returns 分组后的菜单数据 { groupName, items }[]
   */
  function getModuleMenuGroups(moduleCode: string): { groupName: string; groupIcon?: string; items: MenuItem[] }[] {
    const moduleMenus = getCurrentModuleMenus(moduleCode)
    if (!moduleMenus.length) return []

    // 复合模块（含多个前缀）：每个顶级菜单项作为一组
    const isCompoundModule = (MODULE_CONFIG[moduleCode]?.menuPrefixes || []).length > 1

    if (isCompoundModule) {
      // 复合模块：每个顶级菜单项（子模块）作为一组
      return moduleMenus
        .filter(item => item.type !== 'button')
        .map(item => {
          const filteredChildren = item.children?.filter(c => c.type !== 'button')
          return {
            groupName: item.name,
            groupIcon: item.icon || 'AppstoreOutlined',
            // Fix: [] is truthy in JS, must check .length to fallback to [item]
            items: filteredChildren?.length ? filteredChildren : [item],
          }
        })
    } else {
      // 标准两级模块：有子菜单的项作为分组标题，子菜单项作为 items
      const groups: { groupName: string; groupIcon?: string; items: MenuItem[] }[] = []
      for (const item of moduleMenus) {
        if (item.type === 'button') continue
        if (item.children && item.children.filter(c => c.type !== 'button').length > 0) {
          groups.push({
            groupName: item.name,
            groupIcon: item.icon || 'AppstoreOutlined',
            items: item.children.filter(c => c.type !== 'button'),
          })
        } else {
          // 无子菜单的顶级项：单独成一组（groupName 与 item.name 相同）
          groups.push({
            groupName: item.name,
            groupIcon: item.icon || 'AppstoreOutlined',
            items: [item],
          })
        }
      }
      return groups
    }
  }

  /**
   * 获取当前路由同级兄弟页面列表，供面包屑增强使用
   * @param routePath 当前路由路径
   * @returns 同级页面列表 { name, route, isCurrent }[]
   */
  function getBreadcrumbSiblings(routePath: string): { name: string; route: string; isCurrent: boolean }[] {
    if (!menus.value.length) return []

    // 在菜单树中查找目标路由及其父节点
    function findParentChildren(items: MenuItem[], target: string): MenuItem[] | null {
      for (const item of items) {
        if (item.children && item.children.length > 0) {
          // 检查子菜单中是否有目标路由
          const hasTarget = item.children.some(
            c => c.route === target && c.type !== 'button'
          )
          if (hasTarget) {
            return item.children.filter(c => c.type !== 'button' && !!c.route)
          }
          // 递归查找
          const found = findParentChildren(item.children, target)
          if (found) return found
        }
      }
      return null
    }

    const treeMenus = buildMenuTree(menus.value)
    const siblings = findParentChildren(treeMenus, routePath)
    if (!siblings) return []

    return siblings.map(s => ({
      name: s.name,
      route: s.route!,
      isCurrent: s.route === routePath,
    }))
  }

  async function fetchBadgeCounts() {
    try {
      const [taskRes, voucherRes] = await Promise.allSettled([
        get<number>('/task/my/count', {}, { silent: true } as any),
        get<number>('/finance/vouchers/pending-count', {}, { silent: true } as any),
      ])
      oaTodoCount.value = 0
      if (taskRes.status === 'fulfilled') {
        taskCount.value = Number(taskRes.value) || 0
      }
      if (voucherRes.status === 'fulfilled') {
        voucherPendingCount.value = Number(voucherRes.value) || 0
      }
    } catch {
      // 忽略错误
    }
  }

  // 判断用户是否有管理后台访问权限
  const hasAdminAccess = computed(() => {
    return getCurrentModuleMenus('system').length > 0
  })

  function reset() {
    menus.value = []
    routes.value = []
    oaTodoCount.value = 0
    taskCount.value = 0
    voucherPendingCount.value = 0
  }

  return {
    menus,
    routes,
    oaTodoCount,
    taskCount,
    voucherPendingCount,
    hasAdminAccess,
    generateRoutes,
    getCurrentModuleMenus,
    getModuleMenuGroups,
    getBreadcrumbSiblings,
    getModuleVisibility,
    hasModuleAccess,
    fetchBadgeCounts,
    reset,
  }
})
