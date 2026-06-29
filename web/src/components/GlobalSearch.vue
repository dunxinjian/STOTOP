<template>
  <a-modal
    v-model:open="visible"
    :footer="null"
    :closable="false"
    :width="560"
    class="global-search-modal"
    :body-style="{ padding: 0 }"
    :mask-style="{ backgroundColor: 'rgba(0, 0, 0, 0.45)' }"
    :focus-trap="false"
    @after-open-change="handleAfterOpenChange"
  >
    <!-- 搜索输入区 -->
    <div class="search-header">
      <SearchOutlined class="search-icon" />
      <input
        ref="searchInputRef"
        v-model="keyword"
        type="text"
        placeholder="搜索功能、输入拼音首字母..."
        class="search-input"
        autofocus
        @keydown.up.prevent="moveUp"
        @keydown.down.prevent="moveDown"
        @keydown.enter.prevent="confirmSelect"
        @keydown.esc="close"
        @keydown.tab.prevent="cycleSection"
        @keydown.right="handleRight"
        @keydown.left="handleLeft"
      />
      <CloseCircleFilled
        v-if="keyword"
        class="search-clear"
        @click="keyword = ''"
      />
      <span class="search-hint">ESC 关闭</span>
    </div>

    <!-- 内容区 -->
    <div class="search-body" ref="scrollContainerRef">
      <!-- === 有搜索关键词：显示过滤结果 === -->
      <template v-if="keyword.trim()">
        <div v-if="filteredResults.length" class="result-list">
          <!-- 分组模式 -->
          <template v-if="groupedResults">
            <template v-for="group in groupedResults" :key="group.module">
              <div class="result-group-title">{{ group.module }}</div>
              <div
                v-for="item in group.items"
                :key="item.route"
                :class="['search-item', { active: getSearchIndex(item) === activeIndex }]"
                @click="goTo(item)"
                @mouseenter="activeIndex = getSearchIndex(item)"
                :data-index="getSearchIndex(item)"
              >
                <span class="item-name" v-html="highlightMatch(item.name, keyword.trim())"></span>
              </div>
            </template>
          </template>
          <!-- 普通列表模式 -->
          <template v-else>
            <div
              v-for="(item, index) in filteredResults"
              :key="item.route"
              :class="['search-item', { active: index === activeIndex }]"
              @click="goTo(item)"
              @mouseenter="activeIndex = index"
              :data-index="index"
            >
              <span class="item-name" v-html="highlightMatch(item.name, keyword.trim())"></span>
              <span class="item-module-tag" v-if="item.moduleName">{{ item.moduleName }}</span>
            </div>
          </template>
        </div>
        <div
          v-if="hiddenResultCount > 0"
          class="search-more"
          @click="showAllResults = true"
        >
          显示更多 {{ hiddenResultCount }} 条结果
        </div>
        <div class="search-empty" v-else-if="!filteredResults.length">
          <SearchOutlined class="empty-icon" />
          <div class="empty-text">未找到"<span class="empty-keyword">{{ keyword.trim() }}</span>"相关功能</div>
          <div class="empty-hint">试试换个关键词或拼音首字母</div>
        </div>
      </template>

      <!-- === 无搜索关键词：结构化菜单浏览器 === -->
      <template v-else>
        <!-- 固定（置顶页面） -->
        <div class="section" v-if="pinnedDisplay.length">
          <div class="section-title">固定</div>
          <div
            v-for="item in pinnedDisplay"
            :key="'pin-' + item.path"
            class="search-item"
            @click="goToPath(item.path)"
          >
            <span class="item-name">{{ item.title }}</span>
            <PushpinFilled class="pin-btn is-pinned" title="取消固定" @click.stop="togglePin(item.path)" />
          </div>
        </div>

        <!-- 为你推荐（仅空态且有推荐时显示） -->
        <div class="section" v-if="recommendations.length">
          <div class="section-title">为你推荐</div>
          <div
            v-for="item in recommendations"
            :key="'rec-' + item.path"
            :class="['search-item', 'recommended-item', { active: getGlobalIndex('recommend', item.path) === activeIndex }]"
            @click="goToRecommend(item)"
            @mouseenter="activeIndex = getGlobalIndex('recommend', item.path)"
            :data-index="getGlobalIndex('recommend', item.path)"
          >
            <ThunderboltOutlined class="item-icon recommended-icon" />
            <span class="item-name">{{ item.title }}</span>
            <span class="recommend-reason">{{ item.reason === 'chain' ? '关联' : '周期' }}</span>
          </div>
        </div>

        <!-- 最近使用 -->
        <div class="section" v-if="recentItems.length">
          <div class="section-title">最近使用</div>
          <div
            v-for="item in recentItems"
            :key="'recent-' + item.path"
            :class="['search-item', { active: getGlobalIndex('recent', item.path) === activeIndex }]"
            @click="goToRecent(item)"
            @mouseenter="activeIndex = getGlobalIndex('recent', item.path)"
            :data-index="getGlobalIndex('recent', item.path)"
          >
            <span class="item-name">{{ item.title }}</span>
            <span class="item-module-tag" v-if="item.moduleName">{{ item.moduleName }} ›</span>
            <PushpinOutlined
              class="pin-btn"
              :class="{ 'is-pinned': isPinned(item.path) }"
              :title="isPinned(item.path) ? '取消固定' : '固定'"
              @click.stop="togglePin(item.path)"
            />
          </div>
        </div>

        <!-- 全部模块 -->
        <div class="section">
          <div class="section-title">全部模块</div>
          <template v-for="mod in visibleModules" :key="mod.code">
            <!-- 模块头 -->
            <div
              :class="['module-header', { active: getGlobalIndex('module', mod.code) === activeIndex }]"
              @click="toggleModule(mod.code)"
              @mouseenter="activeIndex = getGlobalIndex('module', mod.code)"
              :data-index="getGlobalIndex('module', mod.code)"
            >
              <RightOutlined :class="['module-expand-icon', { expanded: expandedModules.includes(mod.code) }]" />
              <span class="module-name">{{ mod.name }}</span>
              <span class="module-count">({{ getModuleItemCount(mod.code) }})</span>
            </div>
            <!-- 模块子菜单 -->
            <template v-if="expandedModules.includes(mod.code)">
              <div
                v-for="sub in getModuleSubItems(mod.code)"
                :key="'sub-' + sub.route"
                :class="['search-item', 'sub-item', { active: getGlobalIndex('sub', sub.route!) === activeIndex }]"
                @click="goToMenuItem(sub)"
                @mouseenter="activeIndex = getGlobalIndex('sub', sub.route!)"
                :data-index="getGlobalIndex('sub', sub.route!)"
              >
                <span class="item-name">{{ sub.name }}</span>
                <PushpinOutlined
                  class="pin-btn"
                  :class="{ 'is-pinned': isPinned(sub.route!) }"
                  :title="isPinned(sub.route!) ? '取消固定' : '固定'"
                  @click.stop="togglePin(sub.route!)"
                />
              </div>
            </template>
          </template>
        </div>
      </template>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { SearchOutlined, ThunderboltOutlined, RightOutlined, CloseCircleFilled, PushpinOutlined, PushpinFilled } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { usePermissionStore } from '@/stores/permission'
import { useSidebarStore, PINNED_MAX } from '@/stores/sidebar'
import { useRecommendationStore } from '@/stores/recommendation'
import { useAppStore, MODULE_TABS } from '@/stores/app'
import { useOrgContextStore } from '@/stores/orgContext'
import { keyboardManager } from '@/utils/keyboardManager'
import { useCommandPalette } from '@/composables/useCommandPalette'
import { pinyinMatch } from '@/utils/pinyin'
import { analyzeChains, analyzeTimePatterns, mergeRecommendations } from '@/utils/recommendEngine'
import type { MenuItem } from '@/api/auth'
import type { RecommendationItem } from '@/utils/recommendEngine'
import { markNavSource } from '@/stores/navChain'

// ---- 数据结构 ----

interface FlatMenuItem {
  name: string
  code: string
  route: string
  moduleName: string
  moduleCode: string
}

interface RecentItemDisplay {
  path: string
  title: string
  icon?: string
  moduleCode?: string
  moduleName?: string
}

// ---- Stores ----

const router = useRouter()
const permissionStore = usePermissionStore()
const sidebarStore = useSidebarStore()
const recommendationStore = useRecommendationStore()
const appStore = useAppStore()
const orgContextStore = useOrgContextStore()

// ---- State ----

// 命令面板可见状态来自共享 composable，确保 TopBar 点击可直接驱动其变化
const { visible } = useCommandPalette()
const keyword = ref('')
const activeIndex = ref(0)
const searchInputRef = ref<HTMLInputElement | null>(null)
const scrollContainerRef = ref<HTMLElement | null>(null)
const expandedModules = ref<string[]>([])
/** 搜索结果默认显示上限；超出后折叠并提供"显示更多" */
const SEARCH_LIMIT = 20
const showAllResults = ref(false)

// ---- 模块名称映射 ----

const moduleNameMap = computed(() => {
  const map: Record<string, string> = {}
  for (const mod of MODULE_TABS) {
    map[mod.code] = mod.name
  }
  return map
})

// ---- 最近使用 ----

const recentItems = computed<RecentItemDisplay[]>(() => {
  const pages = sidebarStore.recentPages
  return pages.map(p => ({
    path: p.path,
    title: p.label,
    icon: p.icon,
    moduleCode: undefined,
    moduleName: undefined,
  }))
})

// ---- 智能推荐（最多 3 项） ----

const recommendations = computed<RecommendationItem[]>(() => {
  const currentPath = router.currentRoute.value.path
  const transitions = recommendationStore.navigationTransitions
  const visitRecords = recommendationStore.visitRecords
  const recentPaths = sidebarStore.recentPages.map(p => p.path)

  const chainItems = analyzeChains(transitions, currentPath, visitRecords)
  const timeItems = analyzeTimePatterns(visitRecords, new Date())
  return mergeRecommendations(chainItems, timeItems, recentPaths, 3)
})

// ---- 可见模块列表（按权限过滤） ----

const visibleModules = computed(() => {
  const visibility = permissionStore.getModuleVisibility([])
  const result = MODULE_TABS.filter(mod => {
    if (mod.code === 'workhub') return false
    if (mod.alwaysShow) return true
    return visibility[mod.code as keyof typeof visibility] ?? false
  })
  if (import.meta.env.DEV) {
    console.log('[GlobalSearch] visibility=', visibility)
    console.log('[GlobalSearch] visibleModules codes=', result.map(m => m.code))
    console.log('[GlobalSearch] cardflow in visibleModules?', result.some(m => m.code === 'cardflow'))
    const cfGroups = permissionStore.getModuleMenuGroups('cardflow')
    console.log('[GlobalSearch] cardflow getModuleMenuGroups=', JSON.parse(JSON.stringify(cfGroups)))
  }
  return result
})

// ---- 模块子菜单缓存 ----

const moduleMenuCache = computed(() => {
  const cache: Record<string, MenuItem[]> = {}
  for (const mod of visibleModules.value) {
    if (expandedModules.value.includes(mod.code)) {
      const groups = permissionStore.getModuleMenuGroups(mod.code)
      const items: MenuItem[] = []
      for (const group of groups) {
        for (const item of group.items) {
          if (item.route) items.push(item)
        }
      }
      cache[mod.code] = items
    }
  }
  return cache
})

function getModuleItemCount(moduleCode: string): number {
  const groups = permissionStore.getModuleMenuGroups(moduleCode)
  let count = 0
  for (const group of groups) {
    count += group.items.filter(i => i.route).length
  }
  return count
}

function getModuleSubItems(moduleCode: string): MenuItem[] {
  return moduleMenuCache.value[moduleCode] || []
}

// ---- 搜索过滤（拼音模糊匹配） ----

/** 所有菜单项展平（带模块信息），递归到所有层级 */
// 依赖链：visibleModules(→getModuleVisibility→menus.ref) + getCurrentModuleMenus(→menus.ref)
// 两路径均同步读取 permission store 的 menus ref，后端刷新/登出/换组织时自动重算，无需手动失效。
const allFlatMenus = computed<FlatMenuItem[]>(() => {
  const result: FlatMenuItem[] = []
  const seen = new Set<string>()

  // 递归收集一棵菜单树中所有可路由的节点（包含分组节点本身）
  const collect = (items: MenuItem[], modName: string, modCode: string) => {
    for (const it of items) {
      if (it.type === 'button') continue
      if (it.route && !seen.has(it.route)) {
        seen.add(it.route)
        result.push({
          name: it.name,
          code: it.code,
          route: it.route,
          moduleName: modName,
          moduleCode: modCode,
        })
      }
      if (it.children && it.children.length > 0) {
        collect(it.children, modName, modCode)
      }
    }
  }

  for (const mod of visibleModules.value) {
    const moduleMenus = permissionStore.getCurrentModuleMenus(mod.code)
    collect(moduleMenus, mod.name, mod.code)
  }
  return result
})

// ---- 固定（置顶）项 ----

/** route → 显示名 映射（解析固定项路径为可读名称） */
const routeNameMap = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const m of allFlatMenus.value) map[m.route] = m.name
  for (const r of sidebarStore.recentPages) if (r.label) map[r.path] = r.label
  return map
})

/** 固定项（解析为可显示名） */
const pinnedDisplay = computed(() => sidebarStore.pinnedItems.map(w => ({
  path: w.path,
  title: routeNameMap.value[w.path] || w.path,
})))

function isPinned(path?: string): boolean {
  if (!path) return false
  return sidebarStore.pinnedItems.some(p => p.path === path)
}

function togglePin(path?: string) {
  if (!path) return
  const r = sidebarStore.togglePin(path)
  if (!r.ok && r.reason === 'max') message.warning(`最多固定 ${PINNED_MAX} 项`)
}

/** 全部模糊匹配结果（未截断），供计数与"显示更多"使用 */
const allMatchedResults = computed<FlatMenuItem[]>(() => {
  const kw = keyword.value.trim()
  if (!kw) return []
  const lowerKw = kw.toLowerCase()

  return allFlatMenus.value
    .filter(item =>
      pinyinMatch(item.name, kw) ||
      item.code.toLowerCase().includes(lowerKw) ||
      pinyinMatch(item.moduleName, kw)
    )
    .sort((a, b) => {
      // 优先级：完全匹配 > 前缀匹配 > 名称包含 > 模块名匹配
      const scoreA = getMatchScore(a, lowerKw)
      const scoreB = getMatchScore(b, lowerKw)
      return scoreB - scoreA
    })
})

/** 实际渲染的结果：默认截断到 SEARCH_LIMIT，点击"显示更多"后展开全部 */
const filteredResults = computed<FlatMenuItem[]>(() => {
  return showAllResults.value
    ? allMatchedResults.value
    : allMatchedResults.value.slice(0, SEARCH_LIMIT)
})

/** 被折叠隐藏的结果条数（>0 时显示"显示更多"） */
const hiddenResultCount = computed(() =>
  Math.max(0, allMatchedResults.value.length - filteredResults.value.length)
)

function getMatchScore(item: FlatMenuItem, lowerKw: string): number {
  const lowerName = item.name.toLowerCase()
  if (lowerName === lowerKw) return 100           // 完全匹配
  if (lowerName.startsWith(lowerKw)) return 80    // 前缀匹配
  if (lowerName.includes(lowerKw)) return 60      // 包含匹配
  if (pinyinMatch(item.name, lowerKw)) return 40  // 拼音匹配
  return 20                                        // 模块名匹配
}

/** 搜索结果按模块分组（仅在结果较多且跨模块时启用） */
const groupedResults = computed(() => {
  const results = filteredResults.value
  if (results.length <= 5) return null // 结果少时不分组

  const groups: { module: string; items: FlatMenuItem[] }[] = []
  const moduleMap = new Map<string, FlatMenuItem[]>()

  for (const item of results) {
    const existing = moduleMap.get(item.moduleName)
    if (existing) {
      existing.push(item)
    } else {
      const arr = [item]
      moduleMap.set(item.moduleName, arr)
      groups.push({ module: item.moduleName, items: arr })
    }
  }

  // 仅在跨2个以上模块时才分组显示
  return groups.length >= 2 ? groups : null
})

function getSearchIndex(item: FlatMenuItem): number {
  return filteredResults.value.indexOf(item)
}

/** 搜索匹配高亮 */
function highlightMatch(text: string, query: string): string {
  if (!query) return text
  // 先尝试精确子串高亮
  const lowerText = text.toLowerCase()
  const lowerQuery = query.toLowerCase()
  const idx = lowerText.indexOf(lowerQuery)
  if (idx >= 0) {
    return text.slice(0, idx) + '<mark class="match-hl">' + text.slice(idx, idx + query.length) + '</mark>' + text.slice(idx + query.length)
  }
  return text
}

// ---- 全局索引计算（空态浏览器） ----

/** 计算浏览模式下所有可导航项目的平坦列表 */
const browseItemsList = computed<{ type: 'recommend' | 'recent' | 'module' | 'sub'; id: string }[]>(() => {
  const list: { type: 'recommend' | 'recent' | 'module' | 'sub'; id: string }[] = []
  // 推荐项
  for (const item of recommendations.value) {
    list.push({ type: 'recommend', id: item.path })
  }
  // 最近使用项
  for (const item of recentItems.value) {
    list.push({ type: 'recent', id: item.path })
  }
  // 模块项
  for (const mod of visibleModules.value) {
    list.push({ type: 'module', id: mod.code })
    if (expandedModules.value.includes(mod.code)) {
      const subItems = getModuleSubItems(mod.code)
      for (const sub of subItems) {
        if (sub.route) list.push({ type: 'sub', id: sub.route })
      }
    }
  }
  return list
})

function getGlobalIndex(type: 'recommend' | 'recent' | 'module' | 'sub', id: string): number {
  return browseItemsList.value.findIndex(item => item.type === type && item.id === id)
}

/** 当前可导航列表的总数 */
const totalNavigableItems = computed(() => {
  if (keyword.value.trim()) return filteredResults.value.length
  return browseItemsList.value.length
})

// ---- 键盘导航 ----

// 列表长度变化（清空搜索 / 展开折叠模块）后，夹紧 activeIndex 避免越界丢失高亮
watch(totalNavigableItems, (total) => {
  if (activeIndex.value > total - 1) {
    activeIndex.value = Math.max(0, total - 1)
  }
})

function moveUp() {
  if (activeIndex.value > 0) activeIndex.value--
  scrollIntoView()
}

function moveDown() {
  if (activeIndex.value < totalNavigableItems.value - 1) activeIndex.value++
  scrollIntoView()
}

function confirmSelect() {
  if (keyword.value.trim()) {
    // 搜索模式
    const item = filteredResults.value[activeIndex.value]
    if (item) goTo(item)
  } else {
    // 浏览模式
    const entry = browseItemsList.value[activeIndex.value]
    if (!entry) return
    if (entry.type === 'recommend') {
      const rec = recommendations.value.find(r => r.path === entry.id)
      if (rec) goToRecommend(rec)
    } else if (entry.type === 'recent') {
      const page = recentItems.value.find(p => p.path === entry.id)
      if (page) goToRecent(page)
    } else if (entry.type === 'module') {
      toggleModule(entry.id)
    } else if (entry.type === 'sub') {
      // 查找对应的菜单项
      for (const mod of visibleModules.value) {
        const subs = getModuleSubItems(mod.code)
        const found = subs.find(s => s.route === entry.id)
        if (found) { goToMenuItem(found); break }
      }
    }
  }
}

function handleRight() {
  // 仅浏览模式下生效
  if (keyword.value.trim()) return
  const entry = browseItemsList.value[activeIndex.value]
  if (!entry || entry.type !== 'module') return
  // 展开未展开的模块
  if (!expandedModules.value.includes(entry.id)) {
    expandedModules.value.push(entry.id)
  }
}

function handleLeft() {
  // 仅浏览模式下生效
  if (keyword.value.trim()) return
  const entry = browseItemsList.value[activeIndex.value]
  if (!entry) return
  if (entry.type === 'module') {
    // 折叠已展开的模块
    const idx = expandedModules.value.indexOf(entry.id)
    if (idx >= 0) {
      expandedModules.value.splice(idx, 1)
    }
  } else if (entry.type === 'sub') {
    // 从子项跳回模块头
    for (let i = activeIndex.value - 1; i >= 0; i--) {
      if (browseItemsList.value[i].type === 'module') {
        activeIndex.value = i
        scrollIntoView()
        break
      }
    }
  }
}

function cycleSection() {
  // Tab 切换到下一个区域：推荐 → 最近使用 → 模块区 → 推荐
  if (keyword.value.trim()) return
  const recommendCount = recommendations.value.length
  const recentCount = recentItems.value.length
  const moduleStart = recommendCount + recentCount
  if (activeIndex.value < recommendCount && recentCount > 0) {
    activeIndex.value = recommendCount
  } else if (activeIndex.value < moduleStart && visibleModules.value.length) {
    activeIndex.value = moduleStart
  } else {
    activeIndex.value = 0
  }
  scrollIntoView()
}

function scrollIntoView() {
  nextTick(() => {
    const container = scrollContainerRef.value
    if (!container) return
    const el = container.querySelector(`[data-index="${activeIndex.value}"]`) as HTMLElement
    if (el) el.scrollIntoView({ block: 'nearest' })
  })
}

// ---- 模块展开/折叠 ----

function toggleModule(code: string) {
  const idx = expandedModules.value.indexOf(code)
  if (idx >= 0) {
    expandedModules.value.splice(idx, 1)
  } else {
    expandedModules.value.push(code)
  }
}

// ---- 导航 ----

function goTo(item: FlatMenuItem) {
  visible.value = false
  if (router.currentRoute.value.path !== item.route) {
    markNavSource('menu')
    router.push(item.route)
  }
}

function goToRecommend(item: RecommendationItem) {
  visible.value = false
  if (router.currentRoute.value.path !== item.path) {
    markNavSource('menu')
    router.push(item.path)
  }
}

function goToRecent(item: RecentItemDisplay) {
  visible.value = false
  if (router.currentRoute.value.path !== item.path) {
    markNavSource('menu')
    router.push(item.path)
  }
}

function goToPath(path: string) {
  visible.value = false
  if (router.currentRoute.value.path !== path) {
    markNavSource('menu')
    router.push(path)
  }
}

function goToMenuItem(item: MenuItem) {
  if (!item.route) return
  visible.value = false
  if (router.currentRoute.value.path !== item.route) {
    markNavSource('menu')
    router.push(item.route)
  }
}

function close() {
  visible.value = false
}

function open() {
  visible.value = true
}

// ---- 生命周期 ----

function handleAfterOpenChange(isOpen: boolean) {
  if (isOpen) {
    keyword.value = ''
    activeIndex.value = 0
    expandedModules.value = []
    // 动画完全结束后聚焦，确保 focus-trap 不会抢走焦点
    nextTick(() => {
      searchInputRef.value?.focus({ preventScroll: true })
    })
  }
}

// ---- 组织切换强制刷新 ----
// 当组织切换时（orgSwitchVersion 递增），清空展开状态等本地缓存，
// 确保 computed 重新求值时使用最新的 permissionStore.menus 数据
watch(() => orgContextStore.orgSwitchVersion, () => {
  expandedModules.value = []
  keyword.value = ''
  activeIndex.value = 0
})

// keyword 变化时重置 activeIndex 与展开开关，并把焦点保持在输入框，
// 保证搜索/浏览两套索引切换后键盘导航连续
watch(keyword, () => {
  activeIndex.value = 0
  showAllResults.value = false
  nextTick(() => {
    searchInputRef.value?.focus({ preventScroll: true })
  })
})

// 监听 visible 变化，弹窗打开时尝试提前聚焦（afterOpenChange 会做最终保障）
watch(visible, (newVal) => {
  if (newVal) {
    nextTick(() => {
      searchInputRef.value?.focus({ preventScroll: true })
    })
  }
})

// 通过 keyboardManager 注册全局快捷键（键盘 Ctrl+K 路径仍然可用）
let unregisterShortcut: (() => void) | null = null
onMounted(() => {
  unregisterShortcut = keyboardManager.register({
    key: 'ctrl+k',
    label: '命令面板',
    description: '打开命令面板',
    scope: 'global',
    handler: () => {
      visible.value = true
    },
  })
})

onBeforeUnmount(() => {
  unregisterShortcut?.()
  unregisterShortcut = null
})

defineExpose({ open })
</script>

<style lang="scss">
.global-search-modal {
  .ant-modal {
    top: 100px;
  }

  .ant-modal-content {
    border-radius: var(--radius-modal);
    overflow: hidden;
    padding: 0;
    box-shadow: var(--shadow-lg);
  }

  .ant-modal-body {
    padding: 0 !important;
  }

  // 自定义进入/退出动画
  &.ant-modal-wrap {
    .ant-modal {
      animation-duration: 180ms;
      animation-timing-function: cubic-bezier(0.22, 1, 0.36, 1);
    }
  }
}

// 覆盖 Ant 默认的 zoom 动画
.ant-zoom-enter,
.ant-zoom-appear {
  .global-search-modal & {
    animation-name: searchModalIn;
  }
}
.ant-zoom-leave {
  .global-search-modal & {
    animation-name: searchModalOut;
    animation-duration: 120ms;
  }
}

@keyframes searchModalIn {
  from {
    opacity: 0;
    transform: scale(0.98);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes searchModalOut {
  from {
    opacity: 1;
    transform: scale(1);
  }
  to {
    opacity: 0;
    transform: scale(0.98);
  }
}
</style>

<style scoped lang="scss">
.search-header {
  display: flex;
  align-items: center;
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);

  &:focus-within {
    border-bottom-color: var(--color-primary);
  }

  &:focus-within .search-icon {
    color: var(--color-primary);
  }

  .search-icon {
    font-size: var(--font-lg);
    color: rgba(0, 0, 0, 0.25);
    flex-shrink: 0;
    transition: color 0.2s;
  }

  .search-input {
    flex: 1;
    margin: 0 12px;
    border: none;
    outline: none;
    font-size: var(--font-base);
    color: var(--text-1);
    background: transparent;

    &::placeholder {
      color: var(--text-disabled);
    }
  }

  .search-clear {
    font-size: var(--font-base);
    color: rgba(0, 0, 0, 0.25);
    cursor: pointer;
    margin-right: 8px;
    transition: color 0.2s;
    flex-shrink: 0;

    &:hover {
      color: rgba(0, 0, 0, 0.45);
    }
  }

  .search-hint {
    font-size: var(--font-xs);
    color: var(--text-3);
    flex-shrink: 0;
    padding: 2px 6px;
    border: 1px solid var(--border);
    border-radius: var(--radius-sm);
    font-family: 'SF Mono', 'Monaco', 'Menlo', monospace;
    letter-spacing: 0.5px;
  }
}

.search-body {
  height: min(480px, 65vh);
  overflow-y: auto;
  padding: 6px 0;

  &::-webkit-scrollbar {
    width: 4px;
  }
  &::-webkit-scrollbar-thumb {
    background: var(--border-strong);
    border-radius: 2px;
  }
}

.section {
  &:not(:first-child) {
    margin-top: 4px;
    border-top: 1px solid var(--border);
    padding-top: 4px;
  }
}

.section-title {
  padding: 8px 18px 4px;
  font-size: var(--font-xs);
  font-weight: 600;
  color: var(--text-3);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  gap: 6px;

  &::before {
    content: '';
    display: inline-block;
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--text-3);
    opacity: 0.6;
  }
}

.search-item {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 7px 18px;
  cursor: pointer;
  transition: background 0.12s;

  &:hover {
    background: var(--bg-muted);
  }

  &.active {
    background: var(--bg-muted);
  }

  &.active::before {
    content: '';
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: var(--color-primary);
  }

  .item-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: var(--font-sm2);
    color: var(--text-1);
    font-weight: 450;
  }

  .item-module-tag {
    font-size: var(--font-xs);
    color: var(--text-2);
    background: var(--bg-muted);
    padding: 1px 7px;
    border-radius: var(--radius-sm);
    flex-shrink: 0;
    margin-left: 12px;
  }

  &.sub-item {
    padding-left: 40px;

    .item-name {
      font-weight: 400;
      font-size: var(--font-sm2);
    }
  }
}

.module-header {
  position: relative;
  display: flex;
  align-items: center;
  padding: 7px 18px;
  cursor: pointer;
  transition: background 0.12s;

  &:hover {
    background: var(--bg-muted);
  }

  &.active {
    background: var(--bg-muted);
  }

  &.active::before {
    content: '';
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: var(--color-primary);
  }

  .module-expand-icon {
    font-size: var(--font-xs);
    color: var(--text-3);
    width: 14px;
    flex-shrink: 0;
    transition: transform 0.2s ease;

    &.expanded {
      transform: rotate(90deg);
    }
  }

  .module-name {
    font-size: var(--font-sm2);
    color: var(--text-1);
    font-weight: 500;
    margin-left: 4px;
  }

  .module-count {
    font-size: var(--font-xs);
    color: var(--text-3);
    margin-left: 6px;
    background: var(--bg-muted);
    padding: 0 6px;
    border-radius: var(--radius-lg);
    line-height: 16px;
  }
}

.search-empty {
  padding: 36px 18px;
  text-align: center;
  color: var(--text-3);
  font-size: var(--font-sm2);

  .empty-icon {
    font-size: 32px;
    color: var(--text-disabled);
    margin-bottom: 12px;
  }

  .empty-text {
    font-size: var(--font-sm2);
    color: var(--text-2);
    margin-bottom: 4px;
  }

  .empty-keyword {
    color: var(--color-primary);
    font-weight: 500;
  }

  .empty-hint {
    font-size: var(--font-sm);
    color: var(--text-3);
  }
}

.result-list {
  padding: 2px 0;
}

.result-group-title {
  padding: 6px 18px 2px;
  font-size: var(--font-xs);
  font-weight: 500;
  color: var(--text-3);

  &:not(:first-child) {
    margin-top: 4px;
    border-top: 1px solid var(--border);
    padding-top: 8px;
  }
}

.recommended-item {
  position: relative;
  background: var(--bg-muted);
  box-shadow: inset 2px 0 0 var(--color-primary);
}
.recommended-item:hover {
  background: var(--bg-muted);
}

:deep(.match-hl) {
  background: var(--color-primary-light);
  color: var(--color-primary);
  padding: 0 1px;
  border-radius: var(--radius-sm);
  font-weight: 500;
}

.item-icon {
  font-size: var(--font-sm2);
  margin-right: 8px;
  flex-shrink: 0;
}

// 固定/取消固定按钮：行 hover 时浮现；已固定项常亮橙色
.pin-btn {
  font-size: var(--font-sm2);
  color: var(--text-3);
  margin-left: 8px;
  flex-shrink: 0;
  opacity: 0;
  cursor: pointer;
  transition: opacity 0.12s, color 0.12s;
}

.search-item:hover .pin-btn {
  opacity: 1;
}

.pin-btn:hover {
  color: var(--color-primary);
}

.pin-btn.is-pinned {
  opacity: 1;
  color: var(--color-primary);
}

.recommended-icon {
  color: var(--color-primary);
}

.recommend-reason {
  background: var(--color-primary-light);
  color: var(--color-primary);
  font-size: var(--font-xs);
  padding: 1px 4px;
  border-radius: var(--radius-sm);
  margin-left: auto;
  flex-shrink: 0;
}

.search-more {
  padding: var(--space-sm8) var(--space-lg16);
  text-align: center;
  font-size: var(--font-sm);
  color: var(--text-1);
  cursor: pointer;
  border-top: 1px solid var(--border);

  &:hover {
    color: var(--color-primary);
    background: var(--bg-muted);
  }
}
</style>
