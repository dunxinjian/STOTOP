<template>
  <div
    class="todo-card"
    :class="{ 'is-selected': selected, [`prio-${item.priority}`]: true }"
    @click="$emit('select')"
  >
    <span class="tc-bar" :style="{ background: barColor }" />

    <div class="tc-body">
      <!-- 首行：优先级 + 类别 + 截止 -->
      <div class="tc-row1">
        <span class="tc-prio" :style="{ background: pill.bg, color: pill.text }">{{ pill.label }}</span>
        <span class="tc-cat" :style="{ color: bizColor }">{{ bizLabel }}</span>
        <span class="tc-spacer" />
        <span v-if="dueText" class="tc-due" :class="{ 'is-danger': dueDanger }">{{ dueText }}</span>
      </div>

      <!-- 标题 -->
      <div class="tc-title" :title="item.title">{{ item.title }}</div>

      <!-- 副行：结构化信息（提交人·编号·来源，与标题去重） -->
      <div v-if="subText" class="tc-sub" :title="subText">{{ subText }}</div>

      <!-- 行内操作（仅选中时显示，贴合设计） -->
      <div v-if="selected && (primaryActions.length || canDefer)" class="tc-actions" @click.stop>
        <button
          v-for="(action, idx) in primaryActions"
          :key="action.key"
          type="button"
          class="tc-btn"
          :class="{ 'tc-btn--primary': idx === 0 }"
          @click="handleAction(action)"
        >
          {{ action.label }}
        </button>
        <button v-if="canDefer" type="button" class="tc-btn" @click="handleDefer">稍后</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import dayjs from 'dayjs'
import type { WorkItem, WorkItemAction } from '@/api/workhub'
import { useWorkHub } from '@/composables/useWorkHub'
import { bizTypeStyle } from './bizType'

const props = defineProps<{
  item: WorkItem
  selected?: boolean
}>()

defineEmits<{
  select: []
}>()

const router = useRouter()
const hub = useWorkHub()

// —— 业务类别（后端 bizTypeKey/bizTypeLabel 驱动）
const bizLabel = computed(() => props.item.bizTypeLabel || '审批')
const bizColor = computed(() => bizTypeStyle(props.item.bizTypeKey).color)

// —— 副行：结构化信息，与标题去重
// CardFlow 的 summary 形如「流程名 · 发起人 · 卡片编号」，而流程名即标题——去掉重复的标题前缀后
// 余下「发起人 · 卡片编号」即结构化信息；其余来源 summary 与标题本就不同，原样展示。
const SOURCE_LABELS: Record<WorkItem['source'], string> = {
  cardflow: '卡片流转', oa: '审批', quality: '质量', task: '任务',
  datacenter: '数据导入', contract: '合同', points: '积分',
  finance: '财务', system: '系统', workflow: '流程',
}
const subText = computed(() => {
  const title = (props.item.title || '').trim()
  let s = (props.item.summary || '').trim()
  if (s && s !== title) {
    if (title && s.startsWith(title)) {
      s = s.slice(title.length).replace(/^[\s·,，、:：\-—|/]+/, '').trim()
    }
    if (s) return s
  }
  return SOURCE_LABELS[props.item.source] || props.item.bizTypeLabel || ''
})

// —— 优先级
const priorityConfig = {
  urgent: { label: '紧急', bar: 'var(--color-danger)', bg: 'var(--color-danger-light)', text: 'var(--color-danger-text)' },
  high: { label: '高', bar: 'var(--color-warning)', bg: 'var(--color-warning-light)', text: 'var(--color-warning-text)' },
  normal: { label: '普通', bar: 'var(--border-strong)', bg: 'var(--bg-muted)', text: 'var(--text-2)' },
  low: { label: '低', bar: 'var(--border)', bg: 'var(--bg-muted)', text: 'var(--text-3)' },
} as const
type PriorityKey = keyof typeof priorityConfig
const prio = computed(() => priorityConfig[props.item.priority as PriorityKey] ?? priorityConfig.normal)
const barColor = computed(() => prio.value.bar)
const pill = computed(() => ({ label: prio.value.label, bg: prio.value.bg, text: prio.value.text }))

// —— 截止
const isOverdue = computed(() => !!props.item.deadline && dayjs(props.item.deadline).isBefore(dayjs()))
const dueText = computed(() => {
  if (!props.item.deadline) return ''
  const d = dayjs(props.item.deadline)
  const now = dayjs()
  if (isOverdue.value) return '已逾期'
  if (d.isSame(now, 'day')) return '今天'
  if (d.isSame(now.add(1, 'day'), 'day')) return '明天'
  return d.format('MM/DD')
})
const dueDanger = computed(() => isOverdue.value || props.item.priority === 'urgent')

// —— 操作分层（与列表卡一致：oa 前 2 个主操作，其余 1 个）
function getDefaultPrimaryCount(source: WorkItem['source']): number {
  return source === 'oa' ? 2 : 1
}
const primaryActions = computed<WorkItemAction[]>(() => {
  const actions = props.item.actions || []
  if (actions.length === 0) return []
  const hasExplicit = actions.some(a => a.type === 'primary' || a.type === 'secondary')
  if (hasExplicit) return actions.filter(a => a.type === 'primary').slice(0, 2)
  return actions.slice(0, getDefaultPrimaryCount(props.item.source))
})
const canDefer = computed(() => (props.item.actions?.length ?? 0) > 0)

function handleAction(action: WorkItemAction) {
  if (action.route) {
    router.push(action.route)
  } else {
    hub.executeAction(props.item, action)
  }
}

function handleDefer() {
  hub.deferItem(props.item.id)
}
</script>

<style scoped lang="scss">
.todo-card {
  position: relative;
  display: flex;
  flex-shrink: 0;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  cursor: pointer;
  overflow: hidden;
  transition: border-color 0.18s ease, box-shadow 0.18s ease, transform 0.15s ease, background-color 0.15s ease;

  &:hover {
    border-color: var(--color-primary-border);
    box-shadow: var(--shadow-sm);
  }

  &.is-selected {
    background: var(--bg-card);
    border-color: var(--color-primary-border);
    box-shadow: var(--shadow-md);
  }

  // 紧急项选中：浅红底 + 红描边（贴合设计）
  &.prio-urgent.is-selected {
    background: var(--color-danger-light);
    border-color: var(--color-danger-border);
  }
}

.tc-bar {
  width: 3px;
  flex-shrink: 0;
}

.tc-body {
  flex: 1;
  min-width: 0;
  padding: 9px 12px 9px 13px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.tc-row1 {
  display: flex;
  align-items: center;
  gap: 7px;
  min-width: 0;
}

.tc-prio {
  flex-shrink: 0;
  font-size: 10px;
  line-height: 16px;
  padding: 0 7px;
  border-radius: var(--radius-pill);
  font-weight: 500;
}

.tc-cat {
  flex-shrink: 0;
  font-size: 11px;
}

.tc-spacer {
  flex: 1;
}

.tc-due {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--text-3);

  &.is-danger {
    color: var(--color-danger-text);
  }
}

.tc-title {
  font-size: 13px;
  font-weight: 500;
  line-height: 1.4;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.tc-sub {
  font-size: 11.5px;
  line-height: 1.4;
  color: var(--text-2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.tc-actions {
  display: flex;
  gap: 7px;
  margin-top: 4px;
}

.tc-btn {
  font-size: 12px;
  line-height: 1;
  padding: 5px 14px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-strong);
  background: var(--bg-card);
  color: var(--text-2);
  cursor: pointer;
  transition: background 0.15s, color 0.15s, border-color 0.15s;

  &:hover {
    color: var(--text-1);
    border-color: var(--text-3);
  }

  &--primary {
    background: var(--color-primary);
    border-color: var(--color-primary);
    color: var(--text-on-accent);

    &:hover {
      background: var(--color-primary-hover);
      border-color: var(--color-primary-hover);
      color: var(--text-on-accent);
    }
  }
}
</style>
