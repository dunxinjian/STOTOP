<template>
  <div class="history-card" @click="$emit('click')">
    <div class="history-card__header">
      <span class="flow-name">{{ item.flowName }}</span>
      <span :class="['result-tag', item.result]">
        <template v-if="item.result === 'approved'">✓ 已通过</template>
        <template v-else-if="item.result === 'rejected'">✗ 已退回</template>
        <template v-else>已完成</template>
      </span>
    </div>
    <div class="history-card__body">
      <div class="title">{{ item.title }}</div>
      <div class="applicant">{{ item.applicant }}</div>
    </div>
    <div class="history-card__footer">
      <span class="time">{{ formatTime(item.completedAt) }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
defineOptions({ name: 'HistoryCard' })

export interface HistoryItem {
  id: number
  title: string
  flowName: string
  applicant: string
  result: 'approved' | 'rejected' | 'completed'
  completedAt: string
}

defineProps<{
  item: HistoryItem
}>()

defineEmits<{
  click: []
}>()

function formatTime(dateStr: string): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  const now = new Date()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const yesterday = new Date(today.getTime() - 86400000)
  const target = new Date(date.getFullYear(), date.getMonth(), date.getDate())

  if (target.getTime() === today.getTime()) {
    return '今天'
  } else if (target.getTime() === yesterday.getTime()) {
    return '昨天'
  } else if (target.getFullYear() === now.getFullYear()) {
    return `${date.getMonth() + 1}月${date.getDate()}日`
  } else {
    return `${date.getFullYear()}/${date.getMonth() + 1}/${date.getDate()}`
  }
}
</script>

<style scoped lang="scss">
.history-card {
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

    .result-tag {
      font-size: 12px;
      font-weight: 500;
      padding: 2px 8px;
      border-radius: 4px;

      &.approved {
        color: var(--color-success);
        background: rgba(7, 193, 96, 0.1);
      }

      &.rejected {
        color: #ee0a24;
        background: rgba(238, 10, 36, 0.1);
      }

      &.completed {
        color: #969799;
        background: rgba(150, 151, 153, 0.1);
      }
    }
  }

  &__body {
    .title {
      font-size: 15px;
      font-weight: 500;
      color: #323233;
      margin-bottom: 4px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .applicant {
      font-size: 13px;
      color: #646566;
    }
  }

  &__footer {
    margin-top: 8px;
    text-align: right;

    .time {
      font-size: 12px;
      color: #999;
    }
  }
}
</style>
