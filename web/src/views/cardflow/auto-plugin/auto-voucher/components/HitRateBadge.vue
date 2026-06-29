<template>
  <span class="hit-rate-badge">
    <a-tag :color="tagColor" size="small">{{ displayText }}</a-tag>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  rate?: number | null
}>(), {
  rate: null,
})

const displayText = computed(() => {
  if (props.rate == null) return '--'
  return `${(props.rate * 100).toFixed(1)}%`
})

const tagColor = computed(() => {
  if (props.rate == null) return 'default'
  if (props.rate >= 0.8) return 'success'
  if (props.rate >= 0.5) return 'warning'
  return 'error'
})
</script>

<style lang="scss" scoped>
.hit-rate-badge {
  display: inline-flex;
  align-items: center;
}
</style>
