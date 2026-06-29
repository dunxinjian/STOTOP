<template>
  <aside
    class="smart-sidebar"
    :class="{ 'is-collapsed': sidebarStore.collapsed }"
    :style="{
      width: sidebarStore.collapsed ? '0px' : sidebarStore.sidebarWidth + 'px',
    }"
  >
    <!-- 可滚动内容区 -->
    <div class="sidebar-content">
      <!-- 固定区 -->
      <div class="sidebar-section">
        <!-- 空态提示 -->
        <div v-if="pinnedList.length === 0" class="sidebar-empty sidebar-empty--pinned">
          <PushpinOutlined class="empty-icon" />
          <span class="empty-hint">固定常用页面到这里</span>
        </div>

        <draggable
          v-else
          v-model="pinnedList"
          item-key="path"
          group="workset"
          :animation="180"
          ghost-class="nav-item-ghost"
          chosen-class="nav-item-chosen"
          drag-class="nav-item-drag"
          handle=".nav-item"
          @end="onPinnedDragEnd"
        >
          <template #item="{ element }">
            <div
              class="nav-item nav-item--pinned"
              :class="{ active: element.path === currentPath }"
              :title="resolveLabel(element.path)"
              @click="navigateTo(element.path)"
              @click.middle.stop="handleTogglePin(element.path)"
              @contextmenu.prevent="showContextMenu($event, element.path, true)"
            >
              <PushpinFilled class="pin-marker" />
              <component :is="resolvePathIcon(element.path)" class="nav-icon" />
              <span class="nav-item-label">{{ resolveLabel(element.path) }}</span>
              <span v-if="isDirty(element.path)" class="dirty-dot" :title="`${resolveLabel(element.path)} 有未保存改动`" />
              <button
                class="item-action"
                title="取消固定"
                @click.stop="handleTogglePin(element.path)"
              >
                <PushpinOutlined />
              </button>
            </div>
          </template>
        </draggable>
      </div>

      <!-- 最近访问 -->
      <div class="sidebar-section">
        <div v-if="pinnedList.length > 0" class="section-divider" />

        <!-- 空态 -->
        <div v-if="filteredRecentPages.length === 0" class="sidebar-empty">
          <span class="empty-icon">🕐</span>
          <span class="empty-hint">暂无最近记录</span>
        </div>

        <!-- 列表 -->
        <div
          v-for="page in filteredRecentPages"
          :key="page.path"
          class="nav-item"
          :class="{ active: page.path === currentPath }"
          :title="page.label || page.path"
          @click="navigateTo(page.path)"
          @click.middle.stop="handleRemoveRecent(page.path)"
          @contextmenu.prevent="showContextMenu($event, page.path, false)"
        >
          <component :is="resolvePathIcon(page.path, page.icon)" class="nav-icon" />
          <span class="nav-item-label">{{ page.label || page.path }}</span>
          <span v-if="isDirty(page.path)" class="dirty-dot" />
          <button
            class="item-action item-action--pin"
            :class="{ disabled: pinnedFull }"
            :title="pinnedFull ? `固定区已满（最多 ${PINNED_MAX} 个）` : '固定到顶部'"
            @click.stop="handleTogglePin(page.path)"
          >
            <PushpinOutlined />
          </button>
          <button
            class="item-action item-action--close"
            title="移除"
            @click.stop="handleRemoveRecent(page.path)"
          >
            <CloseOutlined />
          </button>
        </div>
      </div>
    </div>

    <!-- 右键菜单 -->
    <div
      v-if="contextMenu.visible"
      class="context-menu"
      :style="{ top: contextMenu.y + 'px', left: contextMenu.x + 'px' }"
      @click.stop
    >
      <div class="context-menu-item" @click="handleMenuAction('close')">
        {{ contextMenu.isPinned ? '取消固定' : '关闭' }}
      </div>
      <div class="context-menu-item" @click="handleMenuAction('closeOther')">关闭其他</div>
      <div
        v-if="!contextMenu.isPinned"
        class="context-menu-item"
        @click="handleMenuAction('pin')"
      >
        固定到顶部
      </div>
    </div>

    <!-- 右边缘拖拽手柄：调节侧栏宽度 -->
    <div
      class="sidebar-resize-handle"
      title="拖拽调整侧栏宽度"
      @mousedown.prevent="onResizeStart"
    ></div>
  </aside>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { message } from 'ant-design-vue'
import {
  PushpinFilled,
  PushpinOutlined,
  CloseOutlined,
  HomeOutlined, NodeIndexOutlined, AccountBookOutlined,
  DatabaseOutlined, UserOutlined, TeamOutlined,
  CarOutlined, FileTextOutlined, ShopOutlined,
  TrophyOutlined, CheckSquareOutlined, SendOutlined,
  SafetyOutlined, SafetyCertificateOutlined, ScheduleOutlined,
  ApartmentOutlined, BarChartOutlined, SettingOutlined,
  AppstoreOutlined, FileOutlined,
} from '@ant-design/icons-vue'
import draggable from 'vuedraggable'
import { useSidebarStore, PINNED_MAX, type WorksetItem } from '@/stores/sidebar'
import { resolveMenuMeta } from '@/utils/menu-registry'
import { MODULE_TABS } from '@/stores/app'
import { markNavSource } from '@/stores/navChain'
import type { Component } from 'vue'

const ICON_MAP: Record<string, Component> = {
  HomeOutlined, NodeIndexOutlined, AccountBookOutlined,
  DatabaseOutlined, UserOutlined, TeamOutlined,
  CarOutlined, FileTextOutlined, ShopOutlined,
  TrophyOutlined, CheckSquareOutlined, SendOutlined,
  SafetyOutlined, SafetyCertificateOutlined, ScheduleOutlined,
  ApartmentOutlined, BarChartOutlined, SettingOutlined,
  AppstoreOutlined, FileOutlined,
}

function resolvePathIcon(path: string, fallbackIcon?: string): Component {
  // 优先使用传入的 icon 字符串（来自 recentPages.icon）
  if (fallbackIcon && ICON_MAP[fallbackIcon]) return ICON_MAP[fallbackIcon]
  // 从路径前缀匹配模块图标
  for (const mod of MODULE_TABS) {
    const prefix = mod.route.split('/').slice(0, 2).join('/')
    if (path.startsWith(prefix + '/') || path === prefix || path.startsWith(mod.route)) {
      return (mod.icon && ICON_MAP[mod.icon as keyof typeof ICON_MAP]) || FileOutlined
    }
  }
  return FileOutlined
}

const route = useRoute()
const router = useRouter()
const sidebarStore = useSidebarStore()
const { recentPages, pinnedItems, pinnedFull } = storeToRefs(sidebarStore)

const currentPath = computed(() => route.path)

// ---------- 工作集本地副本（vuedraggable 直接绑定，仅固定区） ----------
const pinnedList = ref<WorksetItem[]>([])

/** 最近访问：排除已在固定区或工作集中的项目（兼容旧数据） */
const filteredRecentPages = computed(() => {
  const worksetPaths = new Set(sidebarStore.workset.map((w) => w.path))
  return recentPages.value.filter((p) => !worksetPaths.has(p.path))
})

watch(
  pinnedItems,
  (v) => { pinnedList.value = v.map((i) => ({ ...i })) },
  { immediate: true, deep: true },
)

function onPinnedDragEnd() {
  if (pinnedList.value.length > PINNED_MAX) {
    pinnedList.value.splice(PINNED_MAX)
    message.warning(`固定区最多 ${PINNED_MAX} 个`)
  }
  const combined: WorksetItem[] = pinnedList.value.map((i) => ({ path: i.path, pinned: true }))
  sidebarStore.reorderWorkset(combined)
}

function handleTogglePin(path: string) {
  const result = sidebarStore.togglePin(path)
  if (!result.ok && result.reason === 'max') {
    message.warning(`固定区最多 ${PINNED_MAX} 个，请先取消其他项的固定`)
  }
}

// ---------- 移除最近访问 ----------
function handleRemoveRecent(path: string) {
  sidebarStore.removeRecentPage(path)
}

// ---------- 右键菜单 ----------
const contextMenu = ref({
  visible: false,
  x: 0,
  y: 0,
  path: '',
  isPinned: false,
})

function showContextMenu(e: MouseEvent, path: string, isPinned: boolean) {
  e.preventDefault()
  contextMenu.value = {
    visible: true,
    x: e.clientX,
    y: e.clientY,
    path,
    isPinned,
  }
}

function hideContextMenu() {
  contextMenu.value.visible = false
}

function handleMenuAction(action: 'close' | 'closeOther' | 'pin') {
  const path = contextMenu.value.path
  const isPinned = contextMenu.value.isPinned
  hideContextMenu()

  switch (action) {
    case 'close':
      if (isPinned) {
        handleTogglePin(path)
      } else {
        handleRemoveRecent(path)
      }
      break
    case 'closeOther':
      sidebarStore.removeAllRecentExcept(path)
      break
    case 'pin':
      if (!isPinned) {
        handleTogglePin(path)
      }
      break
  }
}

onMounted(() => {
  document.addEventListener('click', hideContextMenu)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', hideContextMenu)
  document.removeEventListener('mousemove', onResizeMove)
  document.removeEventListener('mouseup', onResizeEnd)
  // 防止组件卸载时遗留 body 样式
  document.body.style.userSelect = ''
  document.body.style.cursor = ''
})
function isDirty(path: string) {
  return sidebarStore.isDirty(path)
}

// ---------- 标签解析 ----------
function resolveLabel(path: string): string {
  // 1. 尝试从路由配置的 meta 中获取
  try {
    const resolved = router.resolve(path)
    const meta = resolved?.meta as Record<string, any> | undefined
    if (meta) {
      if (typeof meta.label === 'string' && meta.label) return meta.label
      if (typeof meta.title === 'string' && meta.title) return meta.title
    }
  } catch {
    // 路径无法解析时忽略
  }

  // 2. 尝试从菜单注册表获取
  const menuLabel = resolveMenuMeta(path).label
  if (menuLabel) return menuLabel

  // 3. 尝试从 recentPages 中查找该 path 的已知 label
  const recent = recentPages.value.find((p) => p.path === path)
  if (recent?.label) return recent.label

  // 4. 降级：取路径最后一段
  const segments = path.split('/').filter(Boolean)
  return segments[segments.length - 1] || path
}

// ---------- 导航 ----------
function navigateTo(path: string) {
  if (!path) return
  if (route.path !== path) {
    markNavSource('menu')
    router.push(path)
  }
}

// ---------- 侧栏宽度拖拽 ----------
let resizeStartX = 0
let resizeStartWidth = 0

function onResizeStart(e: MouseEvent) {
  resizeStartX = e.clientX
  resizeStartWidth = sidebarStore.sidebarWidth
  document.body.style.userSelect = 'none'
  document.body.style.cursor = 'col-resize'
  document.addEventListener('mousemove', onResizeMove)
  document.addEventListener('mouseup', onResizeEnd)
}

function onResizeMove(e: MouseEvent) {
  const delta = e.clientX - resizeStartX
  sidebarStore.setSidebarWidth(resizeStartWidth + delta)
}

function onResizeEnd() {
  document.body.style.userSelect = ''
  document.body.style.cursor = ''
  document.removeEventListener('mousemove', onResizeMove)
  document.removeEventListener('mouseup', onResizeEnd)
}

</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// 组件特有样式（基础布局已在 layout.scss 中定义）

// 飞书式视觉重构：去阴影 + 细边框分隔
.smart-sidebar {
  box-shadow: none;
  border-right: 1px solid rgba(0, 0, 0, 0.07);
}

.sidebar-section {
  & + .sidebar-section {
    margin-top: 4px;
  }
}

.section-title {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 10px;
  color: rgba(0, 0, 0, 0.28);
  font-weight: 400;
  padding: 10px 14px 2px;
  letter-spacing: 0.3px;
  line-height: 1;
  border-bottom: none;
  user-select: none;

  .section-icon {
    font-size: 11px;
    color: var(--text-3);
  }
}

.sidebar-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 10px 12px;

  .empty-icon {
    font-size: 13px;
    line-height: 1;
    opacity: 0.5;
  }

  .empty-hint {
    font-size: 12px;
    color: #aaa;
    line-height: 1;
  }
}

.sidebar-empty--pinned {
  .empty-icon {
    font-size: 12px;
    color: #bbb;
    opacity: 1;
  }
}

// 飞书式导航项：紧凑圆角悬浮 + 整行底色高亮（无左侧竖条）
.nav-item {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 36px;
  padding: 0 8px;
  // 预留右侧操作位
  padding-right: 28px;
  margin: 1px 6px;
  border-radius: 6px;
  font-size: 13px;
  color: $sidebar-text;
  cursor: pointer;
  transition: all 0.15s ease;
  position: relative;

  // 显式移除可能继承自全局的左侧竖条装饰
  &::before {
    content: none !important;
    display: none !important;
  }

  &.active::before {
    content: '' !important;
    display: block !important;
    position: absolute;
    left: 0;
    top: 10px;
    bottom: 10px;
    width: 3px;
    border-radius: 0 2px 2px 0;
    background: var(--sidebar-active-indicator);
  }

  &.nav-item--pinned {
    padding-left: 22px;
  }

  .pin-marker {
    position: absolute;
    left: 8px;
    top: 50%;
    transform: translateY(-50%);
    font-size: 10px;
    color: var(--color-primary);
    opacity: 0;
    transition: opacity 0.15s ease;
  }

  &:hover {
    background: var(--sidebar-item-hover);
    color: var(--text-1);
  }

  &.active {
    background: var(--sidebar-item-active-bg);
    color: var(--sidebar-item-active-text);
    font-weight: 600;
  }

  &.active .pin-marker,
  &:hover .pin-marker {
    opacity: 0.85;
  }
}

.nav-item-label {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nav-icon {
  font-size: 14px;
  flex-shrink: 0;
  color: rgba(0, 0, 0, 0.45);
}

.nav-item.active .nav-icon {
  color: var(--sidebar-item-active-text);
}

// 右侧操作按钮（pin / 移除）—— hover 显示
.item-action {
  position: absolute;
  right: 6px;
  top: 50%;
  transform: translateY(-50%);
  width: 18px;
  height: 18px;
  padding: 0;
  border: none;
  background: transparent;
  color: #999;
  border-radius: 3px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  opacity: 0;
  transition: opacity 0.15s ease, background 0.15s ease, color 0.15s ease;

  &:hover {
    background: rgba(0, 0, 0, 0.06);
    color: var(--color-primary);
  }

  &.disabled {
    cursor: not-allowed;
    color: #ccc;
    &:hover { background: transparent; color: #ccc; }
  }
}

.nav-item:hover .item-action {
  opacity: 1;
}

// 两个操作按钮位置（pin 在左，close 在右）
.item-action--pin { right: 26px; }
.item-action--close { right: 6px; }

// 固定项：pin 图标常显作为状态标识
.nav-item--pinned .item-action {
  opacity: 0;
  &:hover { opacity: 1; }
}
.nav-item--pinned:hover .item-action {
  opacity: 1;
}

// dirty 状态：5px 圆形品牌橙圆点（飞书式收敛）
.dirty-dot {
  position: absolute;
  right: 30px;
  top: 50%;
  transform: translateY(-50%);
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary-border);
  pointer-events: none;
}

// 拖拽手柄：橙色 hover 提示
.sidebar-resize-handle {
  &:hover,
  &.resizing,
  &.dragging {
    background: var(--color-primary-border);
  }
}

// 拖拽视觉反馈
.nav-item-ghost {
  opacity: 0.35;
  background: var(--color-primary-light);
  border-left: 2px solid var(--color-primary);
}

.nav-item-chosen {
  cursor: grabbing;
}

.nav-item-drag {
  background: #fff;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12);
  border-radius: 4px;
}

// 右键菜单
.context-menu {
  position: fixed;
  z-index: 1000;
  background: #fff;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  padding: 4px 0;
  min-width: 120px;
  font-size: 13px;
  user-select: none;
}

.context-menu-item {
  padding: 7px 14px;
  cursor: pointer;
  color: $text-primary;
  transition: background 0.15s ease;

  &:hover {
    background: rgba(0, 0, 0, 0.04);
  }
}

</style>
