<template>
  <span class="completeness-badge" :style="{ '--ring-color': ringColor }">
    <svg viewBox="0 0 36 36" class="ring-svg">
      <path
        class="ring-bg"
        d="M18 2.0845
           a 15.9155 15.9155 0 0 1 0 31.831
           a 15.9155 15.9155 0 0 1 0 -31.831"
      />
      <path
        class="ring-fg"
        :stroke-dasharray="`${displayScore}, 100`"
        d="M18 2.0845
           a 15.9155 15.9155 0 0 1 0 31.831
           a 15.9155 15.9155 0 0 1 0 -31.831"
      />
    </svg>
    <span class="ring-label">{{ displayText }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  score?: number | null
}>(), {
  score: null,
})

const displayScore = computed(() => props.score ?? 0)
const displayText = computed(() => props.score != null ? `${props.score}%` : '--')

const ringColor = computed(() => {
  if (props.score == null) return 'var(--border)'
  if (props.score >= 80) return 'var(--color-success)'
  if (props.score >= 50) return 'var(--color-warning)'
  return 'var(--color-danger)'
})
</script>

<style lang="scss" scoped>
.completeness-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.ring-svg {
  width: 24px;
  height: 24px;
  transform: rotate(-90deg);
}

.ring-bg {
  fill: none;
  stroke: var(--border);
  stroke-width: 3;
}

.ring-fg {
  fill: none;
  stroke: var(--ring-color, var(--border));
  stroke-width: 3;
  stroke-linecap: round;
  transition: stroke-dasharray 0.3s ease;
}

.ring-label {
  font-size: 12px;
  color: var(--ring-color, var(--text-3));
  font-weight: 500;
  min-width: 28px;
}
</style>
