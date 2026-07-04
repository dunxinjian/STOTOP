<template>
  <div ref="containerRef" class="nav-chain">
    <!-- 溢出收纳：容器放不下时，较早的页签收进此下拉（置于最左，代表“更早”） -->
    <a-dropdown v-if="hiddenItems.length" trigger="click" placement="bottomLeft">
      <div class="nav-overflow" :class="{ active: hiddenHasActive }" :title="`还有 ${hiddenItems.length} 个页签`">
        <span class="nav-overflow__text">»{{ hiddenItems.length }}</span>
      </div>
      <template #overlay>
        <a-menu class="nav-overflow-menu" @click="(info: { key: string | number }) => onHiddenSelect(String(info.key))">
          <a-menu-item v-for="it in hiddenItems" :key="it.tab.path">
            <div class="nav-overflow-item" :class="{ active: it.index === navChainStore.activeIndex }">
              <span class="nav-overflow-item__label">{{ it.tab.label }}</span>
              <span class="nav-overflow-item__close" @click.stop="onClose(it.index)">
                <CloseOutlined />
              </span>
            </div>
          </a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>

    <!-- 可见页签 -->
    <a-dropdown
      v-for="it in visibleItems"
      :key="it.tab.path"
      :trigger="['contextmenu']"
    >
      <div
        class="nav-tab"
        :class="{ active: it.index === navChainStore.activeIndex }"
        :data-path="it.tab.path"
        @click="onTabClick(it.index)"
        @mouseenter="hoverIndex = it.index"
        @mouseleave="hoverIndex = -1"
      >
        <span class="nav-tab__label">{{ it.tab.label }}</span>
        <span
          v-if="navChainStore.chain.length > 1 && (it.index === navChainStore.activeIndex || hoverIndex === it.index)"
          class="nav-tab__close"
          @click.stop="onClose(it.index)"
        >
          <CloseOutlined />
        </span>
      </div>
      <template #overlay>
        <a-menu @click="(info: { key: string | number }) => onContextMenu(String(info.key), it.index)">
          <a-menu-item key="close" :disabled="navChainStore.chain.length <= 1">关闭当前</a-menu-item>
          <a-menu-item key="closeOther" :disabled="navChainStore.chain.length <= 1">关闭其他</a-menu-item>
          <a-menu-item key="closeRight" :disabled="it.index >= navChainStore.chain.length - 1">关闭右侧</a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { CloseOutlined } from '@ant-design/icons-vue'
import { useNavChainStore, markNavSource } from '@/stores/navChain'

const router = useRouter()
const navChainStore = useNavChainStore()
const hoverIndex = ref<number>(-1)

// ─────────────────────────────────────────────
// 宽度自适应：按容器可用宽度决定可见几个页签，放不下的收进 “»N” 下拉。
// ─────────────────────────────────────────────
const containerRef = ref<HTMLElement | null>(null)
const visibleCount = ref<number>(navChainStore.chain.length)

// 每个页签的自然宽度（key=path）。宽度不随时间变化，测得一次即缓存，
// 便于容器变宽时用缓存值重算而无需重新渲染被折叠的页签。
const widthCache = new Map<string, number>()
const GAP = 4          // 与 .nav-chain 的 gap 保持一致
const OVERFLOW_W = 48  // “»N” 按钮预留宽度（含左侧 gap）

const total = computed(() => navChainStore.chain.length)
const visibleStart = computed(() => Math.max(0, total.value - visibleCount.value))
const visibleItems = computed(() =>
  navChainStore.chain
    .slice(visibleStart.value)
    .map((tab, i) => ({ tab, index: visibleStart.value + i })),
)
const hiddenItems = computed(() =>
  navChainStore.chain
    .slice(0, visibleStart.value)
    .map((tab, i) => ({ tab, index: i })),
)
// 当前激活页签是否落在被折叠的集合里（用于给 “»N” 加激活样式）
const hiddenHasActive = computed(() => navChainStore.activeIndex < visibleStart.value)

/** 测量当前已渲染的可见页签自然宽度并入缓存 */
function measureRendered() {
  const el = containerRef.value
  if (!el) return
  el.querySelectorAll<HTMLElement>('.nav-tab').forEach((node) => {
    const p = node.dataset.path
    if (p && node.offsetWidth > 0) widthCache.set(p, node.offsetWidth)
  })
}

/** 依据容器宽度与缓存宽度计算 visibleCount（保留最右侧/最新的页签） */
function computeVisibleCount() {
  const el = containerRef.value
  if (!el) return
  const chain = navChainStore.chain
  const n = chain.length
  if (n === 0) { visibleCount.value = 0; return }
  const avail = el.clientWidth
  if (avail <= 0) { visibleCount.value = n; return } // 尚未布局，先全显，稍后重算

  const widthOf = (path: string) => widthCache.get(path) ?? 120 // 未测得给个保守估计

  // 先看是否全部放得下（无需溢出按钮）
  let totalW = 0
  for (let i = 0; i < n; i++) {
    totalW += widthOf(chain[i].path) + (i > 0 ? GAP : 0)
  }
  if (totalW <= avail) { visibleCount.value = n; return }

  // 放不下：预留溢出按钮，从右往左尽量塞
  const budget = avail - OVERFLOW_W
  let used = 0
  let count = 0
  for (let i = n - 1; i >= 0; i--) {
    const w = widthOf(chain[i].path) + (count > 0 ? GAP : 0)
    if (used + w > budget) break
    used += w
    count++
  }
  visibleCount.value = Math.max(1, count)
}

async function recompute() {
  await nextTick()
  measureRendered()
  computeVisibleCount()
}

let ro: ResizeObserver | null = null
onMounted(() => {
  recompute()
  if (containerRef.value && typeof ResizeObserver !== 'undefined') {
    // 容器宽度不变时页签宽度不变，缓存已足够，无需重新测量
    ro = new ResizeObserver(() => computeVisibleCount())
    ro.observe(containerRef.value)
  }
})
onBeforeUnmount(() => {
  ro?.disconnect()
  ro = null
})

// chain 增删或标题变化后重新测量+计算
watch(
  () => navChainStore.chain.map((t) => `${t.path}#${t.label}`).join('|'),
  () => recompute(),
)

// ─────────────────────────────────────────────
// 交互
// ─────────────────────────────────────────────
function onTabClick(index: number) {
  if (index === navChainStore.activeIndex) return
  markNavSource('internal')
  navChainStore.switchTo(index)
  const tab = navChainStore.chain[index]
  if (tab) {
    router.push(tab.path)
  }
}

function onHiddenSelect(path: string) {
  const idx = navChainStore.chain.findIndex((t) => t.path === path)
  if (idx >= 0) onTabClick(idx)
}

function onClose(index: number) {
  if (navChainStore.chain.length <= 1) return
  const redirectPath = navChainStore.removeTab(index)
  if (redirectPath) {
    router.push(redirectPath)
  }
}

function onContextMenu(key: string, index: number) {
  switch (key) {
    case 'close':
      onClose(index)
      break
    case 'closeOther': {
      const keep = navChainStore.chain[index]
      if (!keep) return
      navChainStore.resetChain(keep)
      if (navChainStore.activeIndex !== 0) {
        navChainStore.switchTo(0)
      }
      router.push(keep.path)
      break
    }
    case 'closeRight': {
      const removeCount = navChainStore.chain.length - 1 - index
      for (let i = 0; i < removeCount; i++) {
        navChainStore.chain.splice(index + 1, 1)
      }
      if (navChainStore.activeIndex > index) {
        navChainStore.switchTo(index)
        const tab = navChainStore.chain[index]
        if (tab) router.push(tab.path)
      }
      break
    }
  }
}
</script>

<style scoped lang="scss">
.nav-chain {
  display: flex;
  align-items: stretch;
  gap: 4px;
  min-width: 0;
  overflow: hidden;
  height: 100%;
}

// —— 溢出收纳按钮 “»N”
.nav-overflow {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 0 8px;
  min-width: 34px;
  cursor: pointer;
  color: var(--text-2);
  border-bottom: 2px solid transparent;
  transition: background 0.2s, color 0.2s, border-color 0.2s;
  flex-shrink: 0;
  user-select: none;

  &:hover {
    background: var(--bg-muted);
    color: var(--text-1);
  }

  &.active {
    color: var(--color-primary);
    border-bottom-color: var(--color-primary);
  }

  &__text {
    font-size: 12.5px;
    font-weight: 500;
    line-height: 1;
    white-space: nowrap;
  }
}

.nav-tab {
  display: flex;
  align-items: center;
  height: 100%;
  border-radius: 0;
  padding: 0 12px;
  max-width: 220px;
  cursor: pointer;
  background: transparent;
  border-bottom: 2px solid transparent;
  transition: background 0.2s, color 0.2s, border-color 0.2s;
  user-select: none;
  flex-shrink: 0;

  &:hover {
    background: var(--bg-muted);

    .nav-tab__label {
      color: var(--text-1);
    }
  }

  &.active {
    background: transparent;
    border-bottom-color: var(--color-primary);

    .nav-tab__label {
      color: var(--text-1);
      font-weight: 500;
    }

    .nav-tab__close {
      color: var(--text-2);
    }
  }

  &__label {
    font-size: 13px;
    color: var(--text-2);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1;
  }

  &__close {
    display: flex;
    align-items: center;
    justify-content: center;
    margin-left: 6px;
    font-size: 10px;
    color: var(--text-3);
    flex-shrink: 0;
    width: 15px;
    height: 15px;
    border-radius: var(--radius-sm);
    transition: background 0.15s, color 0.15s;

    &:hover {
      background: var(--bg-muted);
      color: var(--text-1);
    }
  }
}

// —— 溢出下拉项
.nav-overflow-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  min-width: 160px;

  &.active .nav-overflow-item__label {
    color: var(--color-primary);
    font-weight: 500;
  }

  &__label {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: var(--text-1);
  }

  &__close {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 16px;
    height: 16px;
    font-size: 10px;
    color: var(--text-3);
    border-radius: var(--radius-sm);
    flex-shrink: 0;
    transition: background 0.15s, color 0.15s;

    &:hover {
      background: var(--bg-muted);
      color: var(--text-1);
    }
  }
}
</style>
