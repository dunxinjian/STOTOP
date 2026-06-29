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

const CHAIN_MAX = 3

// ---- Store ----

export const useNavChainStore = defineStore('navChain', () => {
  // ---------- State ----------

  const chain = ref<NavTab[]>([])
  const activeIndex = ref<number>(0)

  // ---------- Computed ----------

  const activeTab = computed<NavTab | undefined>(() => chain.value[activeIndex.value])
  const hasMultipleTabs = computed<boolean>(() => chain.value.length > 1)

  // ---------- Actions ----------

  /**
   * 追加到导航链路。
   * 若 path 已在 chain 中则仅切换 activeIndex；
   * 否则追加到末尾，超过3个则 shift 移除最早项，activeIndex 设为最后一项。
   */
  function pushToChain(tab: NavTab) {
    const existIdx = chain.value.findIndex((t) => t.path === tab.path)
    if (existIdx !== -1) {
      activeIndex.value = existIdx
      return
    }
    chain.value.push(tab)
    if (chain.value.length > CHAIN_MAX) {
      chain.value.shift()
    }
    activeIndex.value = chain.value.length - 1
  }

  /** 清空 chain，设为单项 */
  function resetChain(tab: NavTab) {
    chain.value = [tab]
    activeIndex.value = 0
  }

  /** 仅修改 activeIndex（不修改 chain） */
  function switchTo(index: number) {
    if (index >= 0 && index < chain.value.length) {
      activeIndex.value = index
    }
  }

  /**
   * 移除指定项；如果移除的是 activeIndex，则调整到相邻Tab（优先左侧）。
   * 返回需要导航到的 path（如果 active 变了的话），否则返回 undefined。
   */
  function removeTab(index: number): string | undefined {
    if (index < 0 || index >= chain.value.length) return undefined
    const wasActive = index === activeIndex.value
    chain.value.splice(index, 1)

    if (chain.value.length === 0) {
      activeIndex.value = 0
      return undefined
    }

    if (wasActive) {
      // 优先左侧
      const newIdx = index > 0 ? index - 1 : 0
      activeIndex.value = newIdx
      return chain.value[newIdx]?.path
    }

    // 若移除项在 activeIndex 之前，需前移
    if (index < activeIndex.value) {
      activeIndex.value = activeIndex.value - 1
    }
    return undefined
  }

  /** 清空 chain 和 activeIndex */
  function clear() {
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
