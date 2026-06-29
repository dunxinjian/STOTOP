<template>
  <div class="timeline">
    <div v-for="(step, idx) in steps" :key="idx" class="timeline-item" :class="{ last: idx === steps.length - 1 }">
      <span class="timeline-dot" :class="step.status">
        <CheckOutlined v-if="step.status === 'done'" />
        <LoadingOutlined v-else-if="step.status === 'current'" spin />
        <ClockCircleOutlined v-else />
      </span>
      <div class="timeline-body">
        <span class="timeline-text">{{ step.text }}</span>
        <span v-if="step.duration" class="timeline-duration">{{ step.duration }}</span>
        <div v-if="step.operator || step.time" class="timeline-meta">
          <span v-if="step.operator" class="timeline-operator">{{ step.operator }}</span>
          <span v-if="step.time" class="timeline-time">{{ step.time }}</span>
        </div>
      </div>
    </div>
    <a-empty v-if="!steps || steps.length === 0" description="暂无链路信息" :image="null" />
  </div>
</template>

<script setup lang="ts">
import { CheckOutlined, LoadingOutlined, ClockCircleOutlined } from '@ant-design/icons-vue'
import type { ChainEvent } from '../composables/useUploadCenter'

defineProps<{
  steps: ChainEvent[]
}>()
</script>

<style scoped lang="scss">
.timeline {
  padding: 8px 0;
}

.timeline-item {
  display: flex;
  gap: 12px;
  padding-bottom: 16px;
  position: relative;

  &:not(.last)::before {
    content: '';
    position: absolute;
    left: 7px;
    top: 22px;
    bottom: 0;
    width: 1px;
    background: var(--border);
  }

  &.last {
    padding-bottom: 0;
  }
}

.timeline-dot {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 10px;
  z-index: 1;
  margin-top: 2px;

  &.done {
    background: var(--color-success-light);
    border: 1px solid var(--color-success);
    color: var(--color-success-text);
  }

  &.current {
    background: var(--color-info-light);
    border: 1px solid var(--color-info);
    color: var(--color-info);
  }

  &.waiting {
    background: var(--bg-muted);
    border: 1px solid var(--border);
    color: var(--text-3);
  }
}

.timeline-body {
  flex: 1;
  min-width: 0;
}

.timeline-text {
  font-size: 13px;
  color: var(--text-1);
  line-height: 16px;
}

.timeline-duration {
  font-size: 12px;
  color: var(--text-3);
  margin-left: 8px;
}

.timeline-meta {
  margin-top: 4px;
  font-size: 12px;
  color: var(--text-3);
  display: flex;
  gap: 8px;
}

.timeline-operator {
  color: var(--color-info);
}

.timeline-time {
  color: var(--text-3);
}
</style>
