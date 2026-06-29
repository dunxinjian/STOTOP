<template>
  <!-- 标题不再显示，由面包屑导航承担 -->

  <!-- 返回按钮：当 backTo 有值时渲染到工具栏左侧 -->
  <Teleport v-if="isMounted && isActive && isCurrent && backTo && hasTarget('page-toolbar-left')" to="#page-toolbar-left">
    <div class="toolbar-back-btn" @click="handleBack">
      <LeftOutlined />
      <span>{{ backLabel || '返回' }}</span>
    </div>
  </Teleport>

  <!-- 将 #left slot 内容传送到面包屑工具栏左侧 -->
  <Teleport v-if="isMounted && isActive && isCurrent && hasTarget('page-toolbar-left')" to="#page-toolbar-left">
    <slot name="left"></slot>
    <slot name="title-extra"></slot>
  </Teleport>

  <!-- 将 #center slot 内容传送到面包屑工具栏中间 -->
  <Teleport v-if="isMounted && isActive && isCurrent && hasTarget('page-toolbar-center')" to="#page-toolbar-center">
    <div class="page-toolbar-center-content">
      <slot name="center"></slot>
    </div>
  </Teleport>

  <!-- 将 #right/#actions slot 内容传送到面包屑工具栏右侧 -->
  <Teleport v-if="isMounted && isActive && isCurrent && hasTarget('page-toolbar-actions')" to="#page-toolbar-actions">
    <slot name="right"></slot>
    <slot name="actions"></slot>
  </Teleport>

  <!-- 将 #toolbar slot 内容传送到面包屑工具栏第二行 -->
  <Teleport v-if="isMounted && isActive && isCurrent && hasTarget('page-toolbar-row2')" to="#page-toolbar-row2">
    <slot name="toolbar"></slot>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed, useSlots, onMounted, onActivated, onDeactivated, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LeftOutlined } from '@ant-design/icons-vue'
import { useToolbarStore } from '@/stores/toolbar'

const props = defineProps<{
  /** 页面标题（保留向后兼容，不再显示） */
  title?: string
  /** 页面描述（保留向后兼容，不再显示） */
  description?: string
  /** 返回路由路径，传入则显示返回按钮 */
  backTo?: string
  /** 返回按钮文字，默认 "返回" */
  backLabel?: string
}>()

const route = useRoute()
const router = useRouter()
const slots = useSlots()
const toolbarStore = useToolbarStore()

const isMounted = ref(false)
const isActive = ref(true)

// 页面唯一标识，在 onMounted 时快照固定，不随路由变化而改变。
// 若使用响应式 computed，keep-alive 缓存中的旧页面在被销毁时，
// route 已切换到新页面，导致 pageId 与新页面相同，引发工具栏内容残留。
let fixedPageId = ''
// 实例级 token：每次 register 时由 toolbarStore 返回，用于区分同一路由名的多个 keep-alive 实例
let instanceToken = 0

function calcPageId() {
  if (route.name) {
    const idPart = route.params.id ? `:${route.params.id}` : ''
    return `route:${String(route.name)}${idPart}`
  }
  return `route:${route.fullPath}`
}

// 仅当 store 中 activeToken 与本实例的 instanceToken 匹配时才允许 Teleport 渲染。
// 这确保即使多个同路由名的实例共存时（keep-alive 犯围），也只有「最后一次 register 」的实例能对外渲染工具栏内容。
const isCurrent = computed(() => !!fixedPageId && instanceToken > 0 && toolbarStore.activeToken === instanceToken)

function hasTarget(id: string): boolean {
  return !!document.getElementById(id)
}

function handleBack() {
  router.push(props.backTo!)
}

function registerToolbar() {
  fixedPageId = calcPageId()
  instanceToken = toolbarStore.register(fixedPageId, { row2: !!slots.toolbar })
}

function unregisterToolbar() {
  toolbarStore.unregister(fixedPageId, instanceToken)
}

onMounted(async () => {
  // 在 onMounted 时计算并注册，获取实例级 token
  registerToolbar()
  await nextTick()
  isMounted.value = true
})

onActivated(() => {
  isActive.value = true
  // keep-alive 复活时重新注册，获取新 token，确保最新实例接管工具栏
  registerToolbar()
})

onDeactivated(() => {
  isActive.value = false
  unregisterToolbar()
})

onUnmounted(() => {
  unregisterToolbar()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.toolbar-back-btn {
  display: flex;
  align-items: center;
  gap: var(--space-xs4);
  font-size: var(--font-base);
  color: var(--text-1);
  cursor: pointer;
  padding: var(--space-xs4) var(--space-sm8);
  border-radius: var(--radius-sm);
  transition: background 0.2s;
  &:hover {
    color: var(--color-primary);
    background: var(--color-primary-light);
  }
}

.page-toolbar-center-content {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--font-lg);
  line-height: 24px;
  font-weight: 600;
  color: var(--text-1);
}

.page-toolbar-center-content:empty {
  display: none;
}

.page-toolbar-center-content :deep(*) {
  min-width: 0;
  max-width: 100%;
}
</style>
