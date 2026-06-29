<template>
  <div class="timeline-view">
    <a-timeline>
      <a-timeline-item
        v-for="(item, index) in items"
        :key="`${item.time}-${item.title}`"
        :color="item.color || typeColorMap[item.type || ''] || '#1677ff'"
      >
        <div
          class="timeline-view__item"
          :class="{ 'timeline-view__item--today': highlightToday && isToday(item.time) }"
        >
          <div class="timeline-view__time">{{ item.time }}</div>
          <div class="timeline-view__title">
            {{ item.title }}
            <a-badge
              v-if="item.count"
              :count="item.count"
              :number-style="{ backgroundColor: 'var(--color-info)', fontSize: '11px' }"
              :overflow-count="999"
              class="timeline-view__badge"
            />
          </div>
          <div v-if="item.location" class="timeline-view__location">
            <EnvironmentOutlined style="margin-right: 4px;" />{{ item.location }}
          </div>
          <a-tag v-if="item.type" :color="typeColorMap[item.type] || 'default'" size="small" class="timeline-view__type">
            {{ item.type }}
          </a-tag>
        </div>
      </a-timeline-item>
    </a-timeline>
    <EmptyState v-if="!items.length" size="small" title="暂无日程" />
  </div>
</template>

<script setup lang="ts">
import { EnvironmentOutlined } from '@ant-design/icons-vue'

export interface TimelineItem {
  /** 时间文本 */
  time: string
  /** 标题 */
  title: string
  /** 地点 */
  location?: string
  /** 类型 */
  type?: string
  /** 参与人数 */
  count?: number
  /** 时间线颜色 */
  color?: string
}

interface Props {
  /** 时间轴项 */
  items: TimelineItem[]
  /** 是否高亮今日项 */
  highlightToday?: boolean
}

withDefaults(defineProps<Props>(), {
  highlightToday: false,
})

const typeColorMap: Record<string, string> = {
  '会议': '#1677ff',
  '培训': '#722ed1',
  '参观': '#13c2c2',
  '用餐': '#fa8c16',
  '休息': '#8c8c8c',
}

function isToday(timeStr: string): boolean {
  const today = new Date().toISOString().slice(0, 10)
  return timeStr.includes(today)
}
</script>

<style scoped lang="scss">
.timeline-view {
  padding: 8px 0;

  &__item {
    padding: 4px 0;

    &--today {
      background: var(--color-primary-light);
      border-radius: 6px;
      padding: 8px 12px;
      margin: -4px -12px;
    }
  }

  &__time {
    font-weight: 600;
    font-size: 14px;
    color: #262626;
    margin-bottom: 4px;
  }

  &__title {
    font-size: 14px;
    color: #434343;
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__badge {
    flex-shrink: 0;
  }

  &__location {
    color: #8c8c8c;
    font-size: 12px;
    margin-top: 2px;
    display: flex;
    align-items: center;
  }

  &__type {
    margin-top: 4px;
  }
}
</style>
