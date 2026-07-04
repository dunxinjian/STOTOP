import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

// ---- 数据结构 ----

export interface NavTab {
  path: string       // 路由路径
  label: string      // 页面标题
  icon?: string      // 图标名（可选）
}

// ---- 导航来源标志（模块级） ----

let _navSource: 'menu' | 'internal' = 'internal'

export function markNavSource(source: 'menu' | 'internal') {
  _navSource = source
}

export function consumeNavSource(): 'menu' | 'internal' {
  const s = _navSource
  _navSource = 'internal' // 消费后重置
  return s
}

// ---- 常量 ----

// 内存中最多保留的页签数（“软上限”，防止无限增长占内存）。
// 顶栏实际可见几个由 TopBarNavChain.vue 按容器宽度自适应，放不下的收进 “»N” 下拉。
// 超过此上限时按 LRU（最久未激活）淘汰，且绝不淘汰当前激活项。
const CHAIN_MAX = 12

// ---- Store ----

export const useNavChainStore = defineStore('navChain', () => {
  // ---------- State ----------

  const chain = ref<NavTab[]>([])
  const activeIndex = ref<number>(0)

  // 访问序列：path -> 最近一次激活的自增序号，用于 LRU 淘汰（非响应式，仅内部记账）。
  const accessOrder = new Map<string, number>()
  let accessSeq = 0
  function touch(path: string) {
    accessOrder.set(path, ++accessSeq)
  }

  // ---------- Computed ----------

  const activeTab = computed<NavTab | undefined>(() => chain.value[activeIndex.value])
  const hasMultipleTabs = computed<boolean>(() => chain.value.length > 1)

  // ---------- Actions ----------

  /**
   * 淘汰一个最久未激活（LRU）的页签，protectPath 对应项永不淘汰。
   */
  function evictLRU(protectPath: string) {
    let victimIdx = -1
    let min = Infinity
    chain.value.forEach((t, i) => {
      if (t.path === protectPath) return
      const seq = accessOrder.get(t.path) ?? 0
      if (seq < min) {
        min = seq
        victimIdx = i
      }
    })
    if (victimIdx >= 0) {
      const [removed] = chain.value.splice(victimIdx, 1)
      if (removed) accessOrder.delete(removed.path)
    }
  }

  /**
   * 追加到导航链路。
   * 若 path 已在 chain 中则仅切换 activeIndex；
   * 否则追加到末尾并激活；超过 CHAIN_MAX 时按 LRU 淘汰（保护当前项）。
   */
  function pushToChain(tab: NavTab) {
    touch(tab.path)
    const existIdx = chain.value.findIndex((t) => t.path === tab.path)
    if (existIdx !== -1) {
      activeIndex.value = existIdx
      return
    }
    chain.value.push(tab)
    if (chain.value.length > CHAIN_MAX) {
      evictLRU(tab.path)
    }
    activeIndex.value = chain.value.findIndex((t) => t.path === tab.path)
  }

  /** 清空 chain，设为单项 */
  function resetChain(tab: NavTab) {
    accessOrder.clear()
    touch(tab.path)
    chain.value = [tab]
    activeIndex.value = 0
  }

  /** 仅修改 activeIndex（不修改 chain）；激活即记为一次访问 */
  function switchTo(index: number) {
    if (index >= 0 && index < chain.value.length) {
      activeIndex.value = index
      const tab = chain.value[index]
      if (tab) touch(tab.path)
    }
  }

  /**
   * 移除指定项；如果移除的是 activeIndex，则调整到相邻Tab（优先左侧）。
   * 返回需要导航到的 path（如果 active 变了的话），否则返回 undefined。
   */
  function removeTab(index: number): string | undefined {
    if (index < 0 || index >= chain.value.length) return undefined
    const wasActive = index === activeIndex.value
    const [removed] = chain.value.splice(index, 1)
    if (removed) accessOrder.delete(removed.path)

    if (chain.value.length === 0) {
      activeIndex.value = 0
      return undefined
    }

    if (wasActive) {
      // 优先左侧
      const newIdx = index > 0 ? index - 1 : 0
      activeIndex.value = newIdx
      const tab = chain.value[newIdx]
      if (tab) touch(tab.path)
      return tab?.path
    }

    // 若移除项在 activeIndex 之前，需前移
    if (index < activeIndex.value) {
      activeIndex.value = activeIndex.value - 1
    }
    return undefined
  }

  /** 清空 chain 和 activeIndex */
  function clear() {
    accessOrder.clear()
    chain.value = []
    activeIndex.value = 0
  }

  return {
    // State
    chain,
    activeIndex,
    // Computed
    activeTab,
    hasMultipleTabs,
    // Actions
    pushToChain,
    resetChain,
    switchTo,
    removeTab,
    clear,
  }
})
