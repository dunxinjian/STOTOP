<script setup lang="ts">
/**
 * 尾部省略文本：溢出时才挂 tooltip（设计 D3——截断必配 tooltip）。
 */
import { ref, watchEffect } from 'vue'

const props = withDefaults(defineProps<{
  text: string
  maxWidth?: string
  lines?: 1 | 2
}>(), {
  maxWidth: '100%',
  lines: 1,
})

const el = ref<HTMLElement>()
const overflowing = ref(false)

watchEffect(() => {
  void props.text
  const node = el.value
  if (!node) return
  overflowing.value = props.lines === 1
    ? node.scrollWidth > node.clientWidth
    : node.scrollHeight > node.clientHeight
})
</script>

<template>
  <a-tooltip v-if="overflowing" :title="text">
    <span ref="el" class="ellipsis-text" :class="{ 'is-multiline': lines === 2 }" :style="{ maxWidth }">{{ text }}</span>
  </a-tooltip>
  <span v-else ref="el" class="ellipsis-text" :class="{ 'is-multiline': lines === 2 }" :style="{ maxWidth }">{{ text }}</span>
</template>

<style scoped lang="scss">
.ellipsis-text {
  display: inline-block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;

  &.is-multiline {
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    white-space: normal;
  }
}
</style>
