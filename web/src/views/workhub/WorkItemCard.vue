<template>
  <div
    class="work-item-card"
    :class="[
      `source-${item.source}`,
      `priority-bar-${item.priority}`,
      'clickable',
      { 'work-item-card--multi-selected': hub.isMultiSelectMode.value && hub.selectedItemIds.value.has(item.id) },
    ]"
    @click.self="handleCardClick"
  >
    <!-- 多选复选框（仅多选模式下可见） -->
    <div v-if="hub.isMultiSelectMode.value" class="multi-select-checkbox-wrap" @click.stop>
      <a-checkbox
        :checked="hub.selectedItemIds.value.has(item.id)"
        class="multi-select-checkbox"
        @change="hub.toggleSelectItem(item.id)"
      />
    </div>

    <!-- 左侧优先级色条 -->
    <div class="priority-bar" :style="{ background: priorityColor }"></div>

    <!-- 来源图标区域 -->
    <div class="source-icon-wrap" :style="{ background: `color-mix(in srgb, ${bizColor} 9%, transparent)` }">
      <component :is="bizIcon" class="source-icon" :style="{ color: bizColor }" />
    </div>

    <!-- 卡片内容 -->
    <div class="card-content" @click="handleCardClick">
      <!-- 标题行：来源标签 + 标题 + 时间 -->
      <div class="card-header">
        <span class="source-tag" :style="{ background: `color-mix(in srgb, ${bizColor} 12%, transparent)`, color: bizColor }">
          {{ bizLabel }}
        </span>
        <a-tag
          :color="priorityTagColor"
          class="priority-tag"
        >{{ priorityLabel }}</a-tag>
        <span v-if="item.source === 'oa' && item.metadata?.processNo" class="process-no">[{{ item.metadata.processNo }}]</span>
        <span class="card-title" :title="item.title">{{ isDataImport ? diTitle : item.title }}</span>
        <span class="card-time">{{ relativeTime }}</span>
      </div>

      <!-- 关联链接行 - 仅 OA/数据导入富卡渲染（默认卡的链接已并入下方合并行） -->
      <div class="related-links-row" v-if="item.relatedLinks && item.relatedLinks.length > 0 && (item.source === 'oa' || (isDataImport && diSubType))">
        <template v-for="(link, idx) in visibleLinks" :key="link.route">
          <a-popover
            v-if="link.summary"
            placement="top"
            :mouse-enter-delay="0.3"
            :mouse-leave-delay="0.2"
            :overlay-style="{ maxWidth: '320px' }"
          >
            <template #content>
              <div class="related-link-summary">{{ link.summary }}</div>
            </template>
            <span
              class="related-link"
              :class="{ disabled: !hasPermission(link) }"
              @click.stop="handleLinkClick(link)"
            >
              <component :is="getIconComponent(link.icon)" class="link-icon" />
              {{ link.label }}
            </span>
          </a-popover>
          <span
            v-else
            class="related-link"
            :class="{ disabled: !hasPermission(link) }"
            @click.stop="handleLinkClick(link)"
          >
            <component :is="getIconComponent(link.icon)" class="link-icon" />
            {{ link.label }}
          </span>
          <span v-if="idx < visibleLinks.length - 1" class="link-separator">·</span>
        </template>
        <span v-if="hiddenLinkCount > 0" class="related-link link-more">+{{ hiddenLinkCount }}</span>
      </div>

      <!-- ===== OA 审批增强渲染 ===== -->
      <template v-if="item.source === 'oa'">
        <!-- 当前审批节点 -->
        <div v-if="item.metadata?.currentNodeName" class="oa-node-row">
          <a-tag color="blue">当前节点: {{ item.metadata.currentNodeName }}</a-tag>
        </div>

        <!-- 摘要行 -->
        <div class="card-summary">{{ item.summary }}</div>

        <!-- OA 增强信息区 -->
        <div class="oa-enhanced">
          <span v-if="item.deadline" class="deadline" :class="{ overdue: isOverdue }">
            <ClockCircleOutlined class="deadline-icon" />
            {{ formattedDeadline }}
          </span>
          <a-tag v-if="item.metadata?.approvalMode" :color="approvalModeColor">
            {{ approvalModeLabel }}
          </a-tag>
        </div>
      </template>

      <!-- ===== 数据导入增强渲染 ===== -->
      <template v-else-if="isDataImport && diSubType">
        <!-- batch_completed：批次完成 -->
        <template v-if="diSubType === 'batch_completed'">
          <div class="di-stats">
            <a-progress
              :percent="diSuccessPercent"
              :stroke-color="'var(--color-success)'"
              size="small"
              :format="() => `${meta.successRows ?? 0}/${meta.totalRows ?? 0}`"
            />
            <span class="di-label">成功率</span>
          </div>
          <div v-if="meta.stagingTable" class="di-extra">
            <DatabaseOutlined class="di-extra-icon" />
            <span>暂存表：{{ meta.stagingTable }}</span>
          </div>
        </template>

        <!-- batch_failed：导入失败 -->
        <template v-else-if="diSubType === 'batch_failed'">
          <div class="di-error">
            <ExclamationCircleOutlined class="di-error-icon" />
            <span>{{ meta.errorMessage || item.summary || '导入失败' }}</span>
          </div>
          <div class="card-footer">
            <div class="card-meta"></div>
            <div class="card-actions" @click.stop>
              <a-button size="small" type="link" danger @click="router.push('/cardflow/upload-center')">
                查看详情
              </a-button>
            </div>
          </div>
        </template>

        <!-- classification_alert：分类告警 -->
        <template v-else-if="diSubType === 'classification_alert'">
          <div class="di-alert-tags">
            <a-tag
              :color="meta.severity === 'Critical' ? 'error' : meta.severity === 'Error' ? 'error' : 'warning'"
              :class="{ 'di-tag-bold': meta.severity === 'Critical' }"
            >
              {{ meta.severity || 'Warning' }}
            </a-tag>
            <a-tag v-for="cls in (meta.classificationTypes || [])" :key="cls">
              {{ cls }}
            </a-tag>
          </div>
          <div class="di-hit-rows" v-if="meta.hitRows">
            命中 <strong>{{ meta.hitRows }}</strong> 行
          </div>
          <div class="card-footer">
            <div class="card-meta"></div>
            <div class="card-actions" @click.stop>
              <a-button size="small" type="link" @click="router.push('/cardflow/upload-center')">
                查看分类结果
              </a-button>
            </div>
          </div>
        </template>

        <!-- error_dispatched：异常已派发 -->
        <template v-else-if="diSubType === 'error_dispatched'">
          <div class="di-dispatch-info">
            <div v-if="meta.batchName" class="di-dispatch-row">
              <span class="di-dispatch-label">来源批次：</span>
              <span>{{ meta.batchName }}</span>
            </div>
            <div v-if="meta.requirement" class="di-dispatch-row">
              <span class="di-dispatch-label">处理要求：</span>
              <span>{{ meta.requirement }}</span>
            </div>
          </div>
          <div class="card-footer">
            <div class="card-meta"></div>
            <div class="card-actions" @click.stop>
              <a-button
                v-for="(action, idx) in primaryActions"
                :key="action.key"
                size="small"
                :type="idx === 0 ? 'primary' : 'default'"
                @click="handleAction(action)"
              >
                {{ action.label }}
              </a-button>
              <a-dropdown v-if="secondaryActions.length" :trigger="['click']" placement="bottomRight">
                <a-button size="small" class="more-actions-btn" @click.stop>
                  <EllipsisOutlined />
                </a-button>
                <template #overlay>
                  <a-menu>
                    <a-menu-item
                      v-for="action in secondaryActions"
                      :key="action.key"
                      @click="handleAction(action)"
                    >
                      {{ action.label }}
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="__defer" @click="handleDefer">
                      <ClockCircleOutlined /> 稍后处理
                    </a-menu-item>
                  </a-menu>
                </template>
              </a-dropdown>
              <a-button v-if="!item.actions.length" size="small" type="primary" @click="handleCardClick">
                开始处理
              </a-button>
            </div>
          </div>
        </template>

        <!-- 未知 subType：降级为默认渲染 -->
        <template v-else>
          <div class="card-summary">{{ item.summary }}</div>
          <div class="card-footer">
            <div class="card-meta">
              <span v-if="item.deadline" class="deadline" :class="{ overdue: isOverdue }">
                <ClockCircleOutlined class="deadline-icon" />
                {{ formattedDeadline }}
              </span>
            </div>
            <div class="card-actions" @click.stop>
              <a-button
                v-for="(action, idx) in primaryActions"
                :key="action.key"
                size="small"
                :type="idx === 0 ? 'primary' : 'default'"
                @click="handleAction(action)"
              >
                {{ action.label }}
              </a-button>
              <a-dropdown v-if="secondaryActions.length" :trigger="['click']" placement="bottomRight">
                <a-button size="small" class="more-actions-btn" @click.stop>
                  <EllipsisOutlined />
                </a-button>
                <template #overlay>
                  <a-menu>
                    <a-menu-item
                      v-for="action in secondaryActions"
                      :key="action.key"
                      @click="handleAction(action)"
                    >
                      {{ action.label }}
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="__defer" @click="handleDefer">
                      <ClockCircleOutlined /> 稍后处理
                    </a-menu-item>
                  </a-menu>
                </template>
              </a-dropdown>
            </div>
          </div>
        </template>
      </template>

      <!-- ===== 默认渲染（非 datacenter 或无 subType）：链接·摘要 + 操作合并为一行 ===== -->
      <template v-else>
        <div class="card-line-row">
          <span class="card-line" :title="item.summary">
            <template v-for="(link, idx) in visibleLinks" :key="link.route">
              <span v-if="idx > 0" class="link-separator">·</span>
              <span
                class="related-link"
                :class="{ disabled: !hasPermission(link) }"
                @click.stop="handleLinkClick(link)"
              >
                <component :is="getIconComponent(link.icon)" class="link-icon" />{{ link.label }}
              </span>
            </template>
            <span v-if="visibleLinks.length && item.summary" class="link-separator">·</span>{{ item.summary }}
          </span>
          <span v-if="item.deadline" class="deadline" :class="{ overdue: isOverdue }">
            <ClockCircleOutlined class="deadline-icon" />
            {{ formattedDeadline }}
          </span>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  ClockCircleOutlined,
  DatabaseOutlined,
  ExclamationCircleOutlined,
  EllipsisOutlined,
  LinkOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import relativeTimePlugin from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'
import type { WorkItem, WorkItemAction, RelatedLink } from '@/api/workhub'
import { useWorkHub } from '@/composables/useWorkHub'
import { bizTypeStyle } from './bizType'

const hub = useWorkHub()

dayjs.extend(relativeTimePlugin)
dayjs.locale('zh-cn')

const props = defineProps<{
  item: WorkItem
}>()

defineEmits<{
  action: [itemId: string, actionKey: string]
}>()

const router = useRouter()

// ===== 业务类型标签（由后端 bizTypeKey/bizTypeLabel 驱动） =====
const bizLabel = computed(() => props.item.bizTypeLabel || '审批')
const bizStyle = computed(() => bizTypeStyle(props.item.bizTypeKey))
const bizColor = computed(() => bizStyle.value.color)
const bizIcon = computed(() => bizStyle.value.icon)

// ===== 优先级配置 =====
const priorityConfig = {
  urgent: { label: '紧急', color: 'var(--color-danger)', tagColor: 'error' },
  high: { label: '高', color: 'var(--color-warning)', tagColor: 'warning' },
  // 普通/低为中性灰，左条只为紧急(红)/高(琥珀)保留强调，避免整列铺满橙色
  normal: { label: '普通', color: 'var(--border-strong)', tagColor: 'default' },
  low: { label: '低', color: 'var(--border)', tagColor: 'default' },
} as const

type PriorityKey = keyof typeof priorityConfig

const priorityColor = computed(() => priorityConfig[props.item.priority as PriorityKey]?.color ?? 'var(--text-3)')
const priorityLabel = computed(() => priorityConfig[props.item.priority as PriorityKey]?.label ?? String(props.item.priority))
const priorityTagColor = computed(() => priorityConfig[props.item.priority as PriorityKey]?.tagColor ?? 'default')

// ===== 时间处理 =====
const relativeTime = computed(() => {
  const ts = props.item.timestamp || (props.item as any).createdAt
  if (!ts) return ''
  return dayjs(ts).fromNow()
})

const formattedDeadline = computed(() => {
  if (!props.item.deadline) return ''
  const d = dayjs(props.item.deadline)
  const now = dayjs()
  const today = now.startOf('day')
  const tomorrow = now.add(1, 'day').startOf('day')
  if (d.isSame(today, 'day')) return '今天截止'
  if (d.isSame(tomorrow, 'day')) return '明天截止'
  return d.format('MM/DD 截止')
})

const isOverdue = computed(() => {
  if (!props.item.deadline) return false
  return dayjs(props.item.deadline).isBefore(dayjs())
})

// ===== 数据导入增强 =====
const isDataImport = computed(() => props.item.source === 'datacenter')
const meta = computed(() => props.item.metadata || {})
const diSubType = computed(() => meta.value.subType as string | undefined)

const diTitle = computed(() => {
  if (!isDataImport.value) return props.item.title
  const sub = diSubType.value
  if (sub === 'batch_completed' || sub === 'batch_failed') {
    return meta.value.fileName || props.item.title
  }
  if (sub === 'classification_alert') {
    return (meta.value.fileName || props.item.title) + ' 数据异常'
  }
  if (sub === 'error_dispatched') {
    return meta.value.errorDescription || props.item.title
  }
  return props.item.title
})

const diSuccessPercent = computed(() => {
  const total = meta.value.totalRows
  const success = meta.value.successRows
  if (!total || total === 0) return 0
  return Math.round((success / total) * 100)
})

// ===== 事件处理 =====
function handleCardClick() {
  // 多选模式：点击卡片等于勾选/取消勾选
  if (hub.isMultiSelectMode.value) {
    hub.toggleSelectItem(props.item.id)
    return
  }
  // 普通模式：选中项由父组件（WorkHubCenter 的 @click.capture）派发到右栏 WorkHubDetail 预览，
  // 跳转完整页面改由右栏「查看完整页面」按钮触发，卡片点击不再直接路由跳转。
}

function handleAction(action: WorkItemAction) {
  if (action.route) {
    router.push(action.route)
  } else {
    // 通过可逆性分级统一入口处理：
    // - needsConfirm=true → 二次确认（不可逆）
    // - 否则 → 乐观更新 + 5 秒 Undo
    hub.executeAction(props.item, action)
  }
}

// 稍后处理：将本卡片移入「已延后」区
function handleDefer() {
  hub.deferItem(props.item.id)
}

// ===== 操作按钮分层 =====
// primary：始终可见（最多 2 个）；secondary：收纳到“...”下拉菜单。
// 若 action 显式声明了 type='primary'/'secondary'，按声明分组；
// 否则按 source 默认分类规则降级（OA 前 2 个为 primary，其余 source 第 1 个为 primary）。
function getDefaultPrimaryCount(source: WorkItem['source']): number {
  return source === 'oa' ? 2 : 1
}

const primaryActions = computed<WorkItemAction[]>(() => {
  const actions = props.item.actions || []
  if (actions.length === 0) return []
  const hasExplicit = actions.some(a => a.type === 'primary' || a.type === 'secondary')
  if (hasExplicit) {
    return actions.filter(a => a.type === 'primary').slice(0, 2)
  }
  return actions.slice(0, getDefaultPrimaryCount(props.item.source))
})

const secondaryActions = computed<WorkItemAction[]>(() => {
  const actions = props.item.actions || []
  if (actions.length === 0) return []
  const hasExplicit = actions.some(a => a.type === 'primary' || a.type === 'secondary')
  if (hasExplicit) {
    return actions.filter(a => a.type === 'secondary')
  }
  return actions.slice(getDefaultPrimaryCount(props.item.source))
})

// ===== OA 审批增强 =====
const approvalModeLabel = computed(() => {
  const mode = props.item.metadata?.approvalMode
  const map: Record<string, string> = { countersign: '会签', or_sign: '或签', sequential: '依次' }
  return map[mode] || mode || ''
})

const approvalModeColor = computed(() => {
  const mode = props.item.metadata?.approvalMode
  const map: Record<string, string> = { countersign: 'purple', or_sign: 'cyan', sequential: 'geekblue' }
  return map[mode] || 'default'
})

// ===== 关联链接 =====
function hasPermission(link: RelatedLink): boolean {
  if (!link.permission) return true
  // TODO: 对接实际权限系统，当前默认放行
  return true
}

function handleLinkClick(link: RelatedLink) {
  if (!hasPermission(link)) return
  router.push(link.route)
}

// 简化实现：所有关联链接统一使用 LinkOutlined 图标
function getIconComponent(_iconName?: string) {
  return LinkOutlined
}

// 关联链接最多展示 3 条，超出折叠为「+N」
const MAX_VISIBLE_LINKS = 3
const visibleLinks = computed(() => (props.item.relatedLinks || []).slice(0, MAX_VISIBLE_LINKS))
const hiddenLinkCount = computed(() => Math.max(0, (props.item.relatedLinks?.length || 0) - MAX_VISIBLE_LINKS))
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.work-item-card {
  background: $bg-card;
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  display: flex;
  overflow: hidden;
  transition: box-shadow 0.18s ease, transform 0.15s ease, background-color 0.15s ease, border-color 0.18s ease;
  border: 1px solid var(--border);

  &:hover {
    border-color: var(--color-primary-border);
    box-shadow: var(--shadow-md);
    transform: translateY(-1px);

    .more-actions-btn {
      opacity: 1;
    }
  }

  &.clickable {
    cursor: pointer;
  }

  &.work-item-card--multi-selected {
    background-color: var(--bg-muted);
    border-color: var(--border-strong);
  }
}

// ===== 多选复选框 =====
.multi-select-checkbox-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 8px 0 12px;
  flex-shrink: 0;
}

// ===== 左侧优先级色条 =====
.priority-bar {
  width: 3px;
  flex-shrink: 0;
}

// ===== 来源图标 =====
.source-icon-wrap {
  width: 40px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 4px;
  border-right: 1px solid rgba(18, 31, 53, 0.04);
}

.source-icon {
  font-size: 17px;
}

// ===== 卡片内容 =====
.card-content {
  flex: 1;
  padding: var(--space-sm8) var(--space-md12) var(--space-sm8) var(--space-sm8);
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

// ===== 标题行 =====
.card-header {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  min-width: 0;
}

.source-tag {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  letter-spacing: 0;
}

.priority-tag {
  flex-shrink: 0;
  font-size: 11px;
  margin: 0;
  line-height: 18px;
  padding: 0 6px;
  border-radius: 999px;
}

.card-title {
  flex: 1;
  font-size: var(--font-base);
  font-weight: 500;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-time {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--text-3);
  white-space: nowrap;
}

// ===== 摘要 =====
.card-summary {
  font-size: var(--font-sm2);
  color: var(--text-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.4;
}

// ===== 默认卡合并行（链接·摘要 + 操作） =====
.card-line-row {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  min-width: 0;
}

.card-line {
  flex: 1;
  min-width: 0;
  font-size: var(--font-sm2);
  color: var(--text-2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

// ===== 底部行 =====
.card-footer {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  margin-top: var(--space-2xs2);
}

.card-meta {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  flex: 1;
  min-width: 0;
}

.deadline {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 12px;
  color: var(--text-3);
  white-space: nowrap;

  &.overdue {
    color: var(--color-danger);
    font-weight: 500;
  }
}

.deadline-icon {
  font-size: 11px;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;

  :deep(.ant-btn-sm) {
    height: 26px;
    font-size: 12px;
    padding: 0 12px;
    border-radius: var(--radius-md);
  }

  :deep(.ant-btn-primary) {
    background: var(--color-primary);
    border-color: var(--color-primary);

    &:hover,
    &:focus {
      background: var(--color-primary-hover);
      border-color: var(--color-primary-hover);
    }
  }

  .more-actions-btn {
    padding: 0 6px;
    color: var(--text-3);
    opacity: 0;
    transition: opacity 0.15s ease, color 0.15s ease;

    &:hover {
      color: var(--text-1);
    }

    &:focus-visible {
      opacity: 1;
    }
  }
}

// ===== 流程编号 =====
.process-no {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--text-3);
  white-space: nowrap;
}

// ===== OA 审批增强样式 =====
.oa-node-row {
  display: flex;
  align-items: center;
  gap: 6px;

  :deep(.ant-tag) {
    margin: 0;
    font-size: 12px;
  }
}

.oa-enhanced {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;

  :deep(.ant-tag) {
    margin: 0;
  }
}

// ===== 数据导入增强样式 =====
.di-stats {
  display: flex;
  align-items: center;
  gap: 8px;

  :deep(.ant-progress) {
    flex: 1;
    margin-bottom: 0;
  }
}

.di-label {
  font-size: 12px;
  color: $text-secondary;
  white-space: nowrap;
}

.di-extra {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: $text-secondary;
}

.di-extra-icon {
  font-size: 12px;
  color: var(--color-success);
}

.di-error {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  font-size: 13px;
  color: var(--color-danger);
  line-height: 1.5;
}

.di-error-icon {
  flex-shrink: 0;
  margin-top: 2px;
}

.di-alert-tags {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;

  :deep(.ant-tag) {
    margin: 0;
  }
}

.di-tag-bold {
  font-weight: 700;
}

.di-hit-rows {
  font-size: 12px;
  color: $text-regular;
}

.di-dispatch-info {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.di-dispatch-row {
  font-size: 12px;
  color: $text-regular;
  line-height: 1.5;
}

.di-dispatch-label {
  color: $text-secondary;
  font-weight: 500;
}

// ===== 关联链接行 =====
.related-links-row {
  display: flex;
  align-items: center;
  padding: 2px 0;
  margin-bottom: 2px;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.related-link {
  font-size: var(--font-sm2);
  color: var(--text-3);
  cursor: pointer;
  transition: color 0.2s;

  &:hover {
    color: var(--color-primary);
  }

  &.disabled {
    color: var(--text-disabled);
    cursor: not-allowed;
  }
}

.link-icon {
  font-size: 12px;
  margin-right: 3px;
}

.link-separator {
  margin: 0 6px;
  color: var(--text-disabled);
}

.link-more {
  color: var(--text-3);
  font-weight: 500;
}

.related-link-summary {
  white-space: pre-wrap;
  font-size: 13px;
  color: var(--text-2);
  max-width: 280px;
  line-height: 1.5;
}
</style>
