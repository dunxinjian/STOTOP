<template>
  <a-dropdown
    :trigger="contextMenuTrigger"
    placement="bottomLeft"
    @openChange="handleContextMenuChange"
  >
    <a-tooltip
      :title="collapsed ? tab.label : ''"
      placement="right"
      :mouse-enter-delay="0.3"
    >
      <div
        class="sidebar-tab-item"
        :class="{
          'sidebar-tab-item--active': active,
          'sidebar-tab-item--collapsed': collapsed,
          'sidebar-tab-item--dragging': isDragging,
          'sidebar-tab-item--drag-over-top': dragOverPosition === 'top',
          'sidebar-tab-item--drag-over-bottom': dragOverPosition === 'bottom',
          'sidebar-tab-item--workhub': tab.id === 'workhub',
          'sidebar-tab-item--pinned': !closable && tab.id !== 'workhub',
        }"
        :draggable="draggable"
        @click="$emit('click')"
        @dragstart="handleDragStart"
        @dragend="handleDragEnd"
        @dragover="handleDragOver"
        @dragleave="handleDragLeave"
        @drop="handleDrop"
      >
        <span class="sidebar-tab-item__icon">
          <component :is="iconComponent" v-if="iconComponent" />
          <AppstoreOutlined v-else />
        </span>
        <span v-if="!collapsed" class="sidebar-tab-item__label">{{ tab.label }}</span>
        <span
          v-if="!collapsed && closable"
          class="sidebar-tab-item__close"
          @click.stop="$emit('close')"
        >
          <CloseOutlined />
        </span>
      </div>
    </a-tooltip>

    <template #overlay>
      <a-menu @click="handleContextAction">
        <!-- 动态Tab右键菜单 -->
        <template v-if="closable">
          <a-menu-item key="close">关闭</a-menu-item>
          <a-menu-item key="closeOther">关闭其他</a-menu-item>
          <a-menu-item key="closeRight">关闭右侧所有</a-menu-item>
          <a-menu-divider />
          <a-menu-item key="pin">固定到侧栏</a-menu-item>
        </template>
        <!-- 固定入口右键菜单（非工作台） -->
        <template v-else-if="tab.id !== 'workhub'">
          <a-menu-item key="unpin">取消固定</a-menu-item>
          <a-menu-item key="moveUp">上移</a-menu-item>
          <a-menu-item key="moveDown">下移</a-menu-item>
        </template>
        <!-- 工作台 -->
        <template v-else>
          <a-menu-item key="home" disabled>这是您的主页</a-menu-item>
        </template>
      </a-menu>
    </template>
  </a-dropdown>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { CloseOutlined, AppstoreOutlined } from '@ant-design/icons-vue'
import * as AntIcons from '@ant-design/icons-vue'

/** 侧栏标签项的最小形状（固定入口/动态标签共用） */
interface SidebarTab {
  id: string
  label: string
  icon?: string
}

const props = defineProps<{
  tab: SidebarTab
  active: boolean
  closable: boolean
  collapsed: boolean
  draggable?: boolean
  index?: number
}>()

const emit = defineEmits<{
  click: []
  close: []
  contextmenu: []
  contextAction: [action: string]
  dragReorder: [fromIndex: number, toIndex: number]
}>()

// 图标动态渲染
const iconComponent = computed(() => {
  const name = props.tab.icon
  if (!name) return null
  return (AntIcons as Record<string, any>)[name] || null
})

// 右键菜单触发方式
const contextMenuTrigger = ['contextmenu'] as any

function handleContextMenuChange(open: boolean) {
  if (open) emit('contextmenu')
}

function handleContextAction({ key }: { key: string | number }) {
  emit('contextAction', String(key))
}

// ===== 拖拽排序 =====
const isDragging = ref(false)
const dragOverPosition = ref<'top' | 'bottom' | null>(null)

function handleDragStart(e: DragEvent) {
  if (!props.draggable || props.index === undefined) return
  isDragging.value = true
  e.dataTransfer!.effectAllowed = 'move'
  e.dataTransfer!.setData('text/plain', String(props.index))
}

function handleDragEnd() {
  isDragging.value = false
}

function handleDragOver(e: DragEvent) {
  if (!props.draggable) return
  e.preventDefault()
  e.dataTransfer!.dropEffect = 'move'
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
  const midY = rect.top + rect.height / 2
  dragOverPosition.value = e.clientY < midY ? 'top' : 'bottom'
}

function handleDragLeave() {
  dragOverPosition.value = null
}

function handleDrop(e: DragEvent) {
  e.preventDefault()
  dragOverPosition.value = null
  if (!props.draggable || props.index === undefined) return
  const fromIndex = parseInt(e.dataTransfer!.getData('text/plain'), 10)
  if (isNaN(fromIndex) || fromIndex === props.index) return
  emit('dragReorder', fromIndex, props.index)
}
</script>

<style scoped lang="scss">
@use '@/styles/sidebar.scss';
</style>
