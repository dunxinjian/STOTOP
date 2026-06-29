<script setup lang="ts">
/**
 * WorkHubDetail.vue — 工作台右栏详情预览组件
 *
 * 根据中栏 select-item 事件的 type 渲染不同的详情视图：
 *   - workitem    → 工作项详情（来源/优先级/标题/摘要/元数据/操作按钮）
 *   - notification → 通知详情
 *
 * 支持浮窗模式（expanded），点击遮罩或按钮可切换。
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  CloseOutlined,
  ExpandAltOutlined,
  ShrinkOutlined,
  FileTextOutlined,
  LinkOutlined,
  ClockCircleOutlined,
  BellOutlined,
} from '@ant-design/icons-vue'
import { message as antMessage, Modal } from 'ant-design-vue'
import dayjs from 'dayjs'
import relativeTimePlugin from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'
import { executeWorkItemAction } from '@/api/workhub'
import type { WorkItem, WorkItemAction } from '@/api/workhub'
import { useWorkHub } from '@/composables/useWorkHub'
import { bizTypeStyle } from './bizType'
import CardActivityTimeline from '@/components/workhub/CardActivityTimeline.vue'

dayjs.extend(relativeTimePlugin)
dayjs.locale('zh-cn')

// ===== 类型 =====
interface SelectedItem {
  type: 'workitem' | 'notification'
  id: string
  data: any
}

// ===== Props / Emits =====
const props = defineProps<{
  selectedItem: SelectedItem
}>()

const emit = defineEmits<{
  close: []
  expand: []
}>()

// ===== Store / Router =====
const router = useRouter()

// ===== WorkHub 共享状态（用于底部上一条/下一条导航） =====
const hub = useWorkHub()
const hasSelectedItem = computed(() => !!props.selectedItem)
const navCurrentIndex = computed(() => hub.currentIndex.value)
const navTotalItems = computed(() => hub.totalItems.value)
/** 是否首条：未在列表中（-1）或已是第 0 条均视为首条（禁用上一条） */
const isFirst = computed(() => navCurrentIndex.value <= 0)
/** 是否末条：未在列表中或已是最后一条均视为末条（禁用下一条） */
const isLast = computed(() =>
  navCurrentIndex.value < 0 || navCurrentIndex.value >= navTotalItems.value - 1
)
function onPrev() {
  hub.navigatePrev()
}
function onNext() {
  hub.navigateNext()
}

// ===== 浮窗模式 =====
const expanded = ref(false)

function toggleExpand() {
  expanded.value = !expanded.value
  emit('expand')
}

function collapseExpand() {
  expanded.value = false
}

// ===== 操作按钮状态 =====
const actionLoading = ref<Record<string, boolean>>({})

// ===== 业务类型配置（由后端 bizTypeKey/bizTypeLabel 驱动） =====

// ===== 优先级配置 =====
const priorityConfig = {
  urgent: { label: '紧急', tagColor: 'error' },
  high: { label: '高', tagColor: 'warning' },
  normal: { label: '普通', tagColor: 'processing' },
  low: { label: '低', tagColor: 'default' },
} as const

// ===== 计算属性 =====
const panelTitle = computed(() => {
  if (!props.selectedItem) return '详情'
  switch (props.selectedItem.type) {
    case 'workitem': return '工作项详情'
    case 'notification': return '通知详情'
    default: return '详情'
  }
})

const workItem = computed<WorkItem | null>(() => {
  if (props.selectedItem?.type === 'workitem') return props.selectedItem.data as WorkItem
  return null
})

const cardId = computed<number | null>(() => {
  if (workItem.value?.source !== 'cardflow') return null
  const raw = (workItem.value.metadata as any)?.cardId
  const n = Number(raw)
  return Number.isFinite(n) && n > 0 ? n : null
})

const workItemBizConfig = computed(() => {
  if (!workItem.value) return null
  return {
    label: workItem.value.bizTypeLabel || '审批',
    ...bizTypeStyle(workItem.value.bizTypeKey),
  }
})

const workItemPriorityConfig = computed(() => {
  if (!workItem.value) return null
  return priorityConfig[workItem.value.priority as keyof typeof priorityConfig] ?? { label: String(workItem.value.priority), tagColor: 'default' }
})

const workItemRelativeTime = computed(() => {
  if (!workItem.value) return ''
  const ts = workItem.value.timestamp || (workItem.value as any).createdAt
  if (!ts) return ''
  return dayjs(ts).fromNow()
})

const workItemFormattedDeadline = computed(() => {
  if (!workItem.value?.deadline) return ''
  const d = dayjs(workItem.value.deadline)
  const now = dayjs()
  if (d.isSame(now, 'day')) return '今天截止'
  if (d.isSame(now.add(1, 'day'), 'day')) return '明天截止'
  return d.format('MM/DD 截止')
})

const workItemIsOverdue = computed(() => {
  if (!workItem.value?.deadline) return false
  return dayjs(workItem.value.deadline).isBefore(dayjs())
})

// 详情面板"详细信息"只展示对用户有意义的 metadata 键，并以中文标签呈现；
// 其余内部键（wfUid/chainId/module/bizType/各种 Id/编码值等）一律隐藏，避免泄漏框架黑话。
const METADATA_LABELS: Record<string, string> = {
  assigneeName: '处理人',
  exceptionNo: '异常编号',
  progress: '进度',
  reminderDate: '提醒日期',
  pointValue: '积分值',
}

const metadataEntries = computed(() => {
  const md = workItem.value?.metadata
  if (!md) return []
  return Object.entries(METADATA_LABELS)
    .map(([key, label]) => ({ label, value: md[key] }))
    .filter(({ value }) => value !== null && value !== undefined && value !== '')
})

const notificationData = computed(() => {
  if (props.selectedItem?.type === 'notification') return props.selectedItem.data
  return null
})

// ===== 通知图标 =====
function getNotificationIcon(type: number) {
  switch (type) {
    case 1: return '📋'
    case 2: return '✅'
    default: return '🔔'
  }
}

// ===== 时间格式化 =====
function formatTime(val?: string) {
  if (!val) return ''
  const now = new Date()
  const d = new Date(val)
  const diff = now.getTime() - d.getTime()
  if (diff < 60 * 1000) return '刚刚'
  if (diff < 60 * 60 * 1000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 24 * 60 * 60 * 1000) return `${Math.floor(diff / 3600000)}小时前`
  return d.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}

// ===== 工作项操作 =====
async function handleWorkItemAction(action: WorkItemAction) {
  if (!workItem.value) return

  // 路由跳转
  if (action.route) {
    router.push(action.route)
    return
  }

  // 撤回操作（特殊处理：需二次确认）
  if (action.key === 'withdraw') {
    Modal.confirm({
      title: '确认撤回',
      content: '撤回后流程将终止，是否继续？',
      okText: '确认撤回',
      okType: 'danger',
      cancelText: '取消',
      async onOk() {
        actionLoading.value['withdraw'] = true
        try {
          await executeWorkItemAction(workItem.value!.id, 'withdraw')
          antMessage.success('流程已撤回')
          emit('close')
        } catch {
          antMessage.error('撤回失败，请重试')
        } finally {
          actionLoading.value['withdraw'] = false
        }
      },
    })
    return
  }

  // 执行 API 操作
  const key = action.key
  actionLoading.value[key] = true
  try {
    await executeWorkItemAction(workItem.value.id, key)
    antMessage.success('操作成功')
  } catch {
    antMessage.error('操作失败，请重试')
  } finally {
    actionLoading.value[key] = false
  }
}

// ===== 稍后处理（操作收敛到右栏后，defer 由此触发）=====
function handleDefer() {
  if (!workItem.value) return
  hub.deferItem(workItem.value.id)
  emit('close')
}

// ===== 查看完整页面 =====
function goToDetailRoute() {
  if (workItem.value?.detailRoute) {
    router.push(workItem.value.detailRoute)
  }
}

</script>

<template>
  <!-- 浮窗遮罩 -->
  <Teleport to="body">
    <div
      v-if="expanded"
      class="detail-overlay"
      @click="collapseExpand"
    />
  </Teleport>

  <!-- 详情面板 -->
  <div
    class="detail-panel"
    :class="{ 'detail-panel--expanded': expanded }"
  >
    <!-- 空状态（防御性） -->
    <template v-if="!selectedItem">
      <div class="detail-empty">
        <div class="detail-empty__icon">
          <FileTextOutlined />
        </div>
        <div class="detail-empty__text">选择左侧列表项查看详情</div>
      </div>
    </template>

    <template v-else>
      <!-- 头部 -->
      <div class="detail-header">
        <span class="detail-header__title">{{ panelTitle }}</span>
        <div class="detail-header__actions">
          <a-tooltip :title="expanded ? '收起浮窗' : '浮窗模式'">
            <a-button
              type="text"
              size="small"
              class="header-btn"
              @click="toggleExpand"
            >
              <template #icon>
                <ShrinkOutlined v-if="expanded" />
                <ExpandAltOutlined v-else />
              </template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="关闭">
            <a-button
              type="text"
              size="small"
              class="header-btn header-btn--close"
              @click="$emit('close')"
            >
              <template #icon><CloseOutlined /></template>
            </a-button>
          </a-tooltip>
        </div>
      </div>

      <!-- 内容区 -->
      <div class="detail-body">

        <!-- ========== 工作项详情 ========== -->
        <template v-if="selectedItem.type === 'workitem' && workItem">
          <div class="workitem-detail">

            <!-- 顶部元信息行 -->
            <div class="wi-meta-row">
              <span
                class="wi-source-tag"
                :style="{
                  background: (workItemBizConfig?.color ?? 'var(--biz-approval)') + '18',
                  color: workItemBizConfig?.color ?? 'var(--biz-approval)'
                }"
              >
                <component :is="workItemBizConfig?.icon" class="wi-source-icon" />
                {{ workItemBizConfig?.label }}
              </span>
              <a-tag
                :color="workItemPriorityConfig?.tagColor ?? 'default'"
                class="wi-priority-tag"
              >
                {{ workItemPriorityConfig?.label }}
              </a-tag>
              <span class="wi-time">
                <ClockCircleOutlined class="wi-time-icon" />
                {{ workItemRelativeTime }}
              </span>
            </div>

            <!-- 标题 -->
            <div class="wi-title">{{ workItem.title }}</div>

            <!-- 摘要 -->
            <div v-if="workItem.summary" class="wi-summary">{{ workItem.summary }}</div>

            <!-- 截止日期 -->
            <div v-if="workItem.deadline" class="wi-deadline" :class="{ 'wi-deadline--overdue': workItemIsOverdue }">
              <ClockCircleOutlined class="wi-deadline-icon" />
              {{ workItemFormattedDeadline }}
              <span class="wi-deadline-full">（{{ dayjs(workItem.deadline).format('YYYY-MM-DD HH:mm') }}）</span>
            </div>

            <!-- 元数据 -->
            <div v-if="metadataEntries.length > 0 && !cardId" class="wi-metadata">
              <div class="wi-metadata__title">详细信息</div>
              <div class="wi-metadata__grid">
                <div
                  v-for="m in metadataEntries"
                  :key="m.label"
                  class="wi-metadata__item"
                >
                  <span class="wi-metadata__key">{{ m.label }}</span>
                  <span class="wi-metadata__value">{{ m.value }}</span>
                </div>
              </div>
            </div>

            <!-- CardFlow 活动时间轴（审批进度 × 操作日志合并） -->
            <CardActivityTimeline v-if="cardId" :card-id="cardId" />

            <!-- 查看完整页面链接 -->
            <div v-if="workItem.detailRoute" class="wi-link-row">
              <a-button type="link" size="small" @click="goToDetailRoute">
                <template #icon><LinkOutlined /></template>
                查看完整页面
              </a-button>
            </div>

          </div>

          <!-- 操作按钮区（sticky bottom）：稍后处理（左）+ 主操作（右） -->
          <div class="wi-actions">
            <a-button type="text" size="small" class="wi-defer-btn" @click="handleDefer">
              <template #icon><ClockCircleOutlined /></template>
              稍后处理
            </a-button>
            <a-button
              v-for="action in workItem.actions"
              :key="action.key"
              :type="action.type === 'primary' ? 'primary' : action.type === 'danger' ? 'primary' : 'default'"
              :danger="action.type === 'danger'"
              :loading="actionLoading[action.key]"
              class="wi-action-btn"
              @click="handleWorkItemAction(action)"
            >
              {{ action.label }}
            </a-button>
          </div>
        </template>

        <!-- ========== 通知详情 ========== -->
        <template v-else-if="selectedItem.type === 'notification' && notificationData">
          <div class="notification-detail">

            <!-- 来源图标 + 来源名称 -->
            <div class="nd-source-row">
              <div class="nd-icon">{{ getNotificationIcon(notificationData.eventType) }}</div>
              <span class="nd-source-name">系统通知</span>
              <div v-if="!notificationData.isRead" class="nd-unread-badge">未读</div>
            </div>

            <!-- 通知标题 -->
            <div class="nd-title">{{ notificationData.title }}</div>

            <!-- 通知内容 -->
            <div v-if="notificationData.content" class="nd-content">{{ notificationData.content }}</div>

            <!-- 时间戳 -->
            <div class="nd-time">
              <ClockCircleOutlined class="nd-time-icon" />
              {{ formatTime(notificationData.createTime) }}
            </div>

            <!-- 关联链接/路由 -->
            <div v-if="notificationData.relatedRoute || notificationData.linkUrl" class="nd-link-row">
              <a-button
                type="primary"
                @click="notificationData.relatedRoute ? router.push(notificationData.relatedRoute) : undefined"
              >
                <template #icon><LinkOutlined /></template>
                查看详情
              </a-button>
            </div>
          </div>
        </template>

      </div>

      <!-- 底部固定导航条（仅预览态可见） -->
      <div v-if="hasSelectedItem" class="detail-nav-bar">
        <a-button size="small" :disabled="isFirst" @click="onPrev">
          ← 上一条
        </a-button>
        <span class="nav-counter">
          <template v-if="navCurrentIndex >= 0">
            {{ navCurrentIndex + 1 }} / {{ navTotalItems }}
          </template>
          <template v-else>
            — / {{ navTotalItems }}
          </template>
        </span>
        <a-button size="small" :disabled="isLast" @click="onNext">
          下一条 →
        </a-button>
      </div>
      <div v-if="hasSelectedItem" class="nav-shortcut-hint">
        J/K 切换 · Enter 主操作
      </div>
    </template>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// ===== 遮罩 =====
.detail-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.2);
  z-index: 999;
}

// ===== 面板容器 =====
.detail-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-card);
  border-left: 1px solid $border-color-lighter;
  overflow: hidden;
  transition: box-shadow 0.25s ease;

  // 浮窗模式
  &--expanded {
    position: fixed;
    right: 0;
    top: 42px;
    bottom: 0;
    width: 720px;
    z-index: 1000;
    box-shadow: -4px 0 24px rgba(0, 0, 0, 0.15);
    border-left: none;
  }
}

// ===== 空状态 =====
.detail-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: $text-placeholder;
  height: 100%;
}

.detail-empty__icon {
  font-size: 48px;
  opacity: 0.3;
}

.detail-empty__text {
  font-size: 14px;
}

// ===== 头部 =====
.detail-header {
  display: flex;
  align-items: center;
  height: 48px;
  padding: 0 12px 0 16px;
  border-bottom: 1px solid $border-color-lighter;
  flex-shrink: 0;
  background: var(--bg-card);
  gap: 8px;
}

.detail-header__title {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: $text-primary;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.detail-header__actions {
  display: flex;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
}

.header-btn {
  color: $text-secondary;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;

  &:hover {
    color: $text-primary;
    background: $border-color-lighter;
    border-radius: 4px;
  }

  &--close:hover {
    color: $color-danger;
    background: rgba(255, 77, 79, 0.08);
  }
}

// ===== 内容区 =====
.detail-body {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;

  &::-webkit-scrollbar {
    width: 4px;
  }

  &::-webkit-scrollbar-thumb {
    background: rgba(0, 0, 0, 0.12);
    border-radius: 2px;
  }
}

// ===== 工作项详情 =====
.workitem-detail {
  flex: 1;
  padding: 20px 20px 12px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

// 元信息行
.wi-meta-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.wi-source-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.3px;
}

.wi-source-icon {
  font-size: 12px;
}

.wi-priority-tag {
  font-size: 11px;
  margin: 0;
}

.wi-time {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 12px;
  color: $text-secondary;
  margin-left: auto;
}

.wi-time-icon {
  font-size: 11px;
}

// 标题
.wi-title {
  font-size: 18px;
  font-weight: 700;
  color: $text-primary;
  line-height: 1.4;
}

// 摘要
.wi-summary {
  font-size: 14px;
  color: $text-regular;
  line-height: 1.7;
  background: var(--bg-muted);
  border-radius: 8px;
  padding: 12px 14px;
  border-left: 3px solid $border-color;
}

// 截止日期
.wi-deadline {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 13px;
  color: $text-secondary;
  background: var(--bg-muted);
  padding: 5px 12px;
  border-radius: 8px;
  width: fit-content;

  &--overdue {
    color: $color-danger;
    background: rgba(255, 77, 79, 0.08);
    font-weight: 500;
  }
}

.wi-deadline-icon {
  font-size: 12px;
}

.wi-deadline-full {
  font-size: 12px;
  opacity: 0.7;
}

// 元数据
.wi-metadata {
  background: var(--bg-muted);
  border-radius: 8px;
  padding: 12px 14px;
  border: 1px solid $border-color-lighter;
}

.wi-metadata__title {
  font-size: 12px;
  font-weight: 600;
  color: $text-secondary;
  margin-bottom: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.wi-metadata__grid {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.wi-metadata__item {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 13px;
}

.wi-metadata__key {
  color: $text-secondary;
  flex-shrink: 0;
  min-width: 72px;
  font-size: 12px;

  &::after {
    content: '：';
  }
}

.wi-metadata__value {
  color: $text-primary;
  word-break: break-all;
}

// 链接行
.wi-link-row {
  display: flex;
  align-items: center;
}

// 操作按钮区（sticky bottom）
.wi-actions {
  position: sticky;
  bottom: 0;
  background: var(--bg-card);
  border-top: 1px solid $border-color-lighter;
  padding: 12px 20px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  flex-shrink: 0;
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.04);
}

.wi-action-btn {
  flex: 1;
  min-width: 80px;
}

.wi-defer-btn {
  flex-shrink: 0;
  color: $text-secondary;
}

// ===== 对话详情（ConversationThread 占满） =====
:deep(.conversation-thread) {
  height: 100%;
}

// ===== 通知详情 =====
.notification-detail {
  flex: 1;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

// 来源行
.nd-source-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.nd-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: var(--bg-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.nd-source-name {
  font-size: 13px;
  color: $text-secondary;
  font-weight: 500;
}

.nd-unread-badge {
  margin-left: auto;
  background: $color-danger;
  color: var(--text-on-accent);
  font-size: 11px;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 600;
}

// 标题
.nd-title {
  font-size: 18px;
  font-weight: 700;
  color: $text-primary;
  line-height: 1.4;
}

// 内容
.nd-content {
  font-size: 14px;
  color: $text-regular;
  line-height: 1.7;
  background: var(--bg-muted);
  border-radius: 8px;
  padding: 14px;
  border-left: 3px solid $border-color;
}

// 时间
.nd-time {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  color: $text-placeholder;
}

.nd-time-icon {
  font-size: 11px;
}

// 链接
.nd-link-row {
  margin-top: 8px;
}

// OA 流程编号
.oa-process-no {
  padding: 8px 16px 0;
  font-size: 12px;
  color: var(--text-3);
  flex-shrink: 0;
  background: var(--bg-page);
  letter-spacing: 0.3px;
}

// ===== OA 审批预览容器 =====
.oa-preview-wrapper {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: stretch;
  overflow: hidden;
  background: var(--bg-page);
}

// ===== 底部导航条 =====
.detail-nav-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  border-top: 1px solid var(--border);
  background: var(--bg-muted);
  flex-shrink: 0;
}

.nav-counter {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  user-select: none;
}

.nav-shortcut-hint {
  text-align: center;
  font-size: 11px;
  color: rgba(0, 0, 0, 0.3);
  padding: 2px 0 4px;
  background: var(--bg-muted);
  flex-shrink: 0;
  user-select: none;
}
</style>
