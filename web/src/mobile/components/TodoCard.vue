<template>
  <div class="todo-card" @click="$emit('click')">
    <div class="todo-card__header">
      <span class="flow-name">{{ item.flowName }}</span>
      <span class="time">{{ formatTime(item.createdAt) }}</span>
    </div>
    <div class="todo-card__body">
      <div class="applicant">{{ item.applicant }}</div>
      <div class="title">{{ item.title }}</div>
    </div>
    <div class="todo-card__footer" v-if="item.amount">
      <span class="amount">¥{{ formatAmount(item.amount) }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { TodoCard } from '@shared/types'

defineOptions({ name: 'TodoCard' })

defineProps<{
  item: TodoCard
}>()

defineEmits<{
  click: []
}>()

/** 相对时间格式化 */
function formatTime(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const yesterday = new Date(today.getTime() - 86400000)
  const target = new Date(date.getFullYear(), date.getMonth(), date.getDate())

  const timeStr = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`

  if (target.getTime() === today.getTime()) {
    return `今天 ${timeStr}`
  } else if (target.getTime() === yesterday.getTime()) {
    return `昨天 ${timeStr}`
  } else {
    return `${date.getMonth() + 1}/${date.getDate()} ${timeStr}`
  }
}

/** 金额千分位格式化 */
function formatAmount(amount: number): string {
  return amount.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
</script>

<style scoped lang="scss">
.todo-card {
  background: #fff;
  border-radius: 10px;
  padding: 14px 16px;
  margin-bottom: 10px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
  transition: background 0.15s;

  &:active {
    background: #f7f8fa;
  }

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;

    .flow-name {
      font-size: 13px;
      color: var(--text-2);
      font-weight: 500;
      background: var(--bg-muted);
      padding: 2px 8px;
      border-radius: 4px;
    }

    .time {
      font-size: 12px;
      color: #999;
    }
  }

  &__body {
    .applicant {
      font-size: 15px;
      font-weight: 500;
      color: #323233;
      margin-bottom: 4px;
    }

    .title {
      font-size: 13px;
      color: #646566;
      line-height: 1.4;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  &__footer {
    margin-top: 10px;
    text-align: right;

    .amount {
      font-size: 16px;
      font-weight: 600;
      color: #ee0a24;
    }
  }
}
</style>
