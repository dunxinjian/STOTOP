<template>
  <a-tag :color="config.color" class="priority-tag">
    <component :is="config.icon" />
    {{ config.label }}
  </a-tag>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  ThunderboltOutlined,
  ArrowUpOutlined,
  MinusOutlined,
  ArrowDownOutlined,
} from '@ant-design/icons-vue'

const props = defineProps<{
  priority: number | string
}>()

const priorityMap: Record<string | number, { label: string; color: string; icon: any }> = {
  urgent: { label: '紧急', color: 'red', icon: ThunderboltOutlined },
  high: { label: '高', color: 'orange', icon: ArrowUpOutlined },
  medium: { label: '中', color: 'blue', icon: MinusOutlined },
  low: { label: '低', color: 'default', icon: ArrowDownOutlined },
  // numeric mapping: 0=low, 1=medium, 2=high, 3=urgent
  0: { label: '低', color: 'default', icon: ArrowDownOutlined },
  1: { label: '中', color: 'blue', icon: MinusOutlined },
  2: { label: '高', color: 'orange', icon: ArrowUpOutlined },
  3: { label: '紧急', color: 'red', icon: ThunderboltOutlined },
}

const config = computed(() => priorityMap[props.priority] ?? priorityMap['medium'])
</script>

<style scoped lang="scss">
.priority-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
}
</style>
