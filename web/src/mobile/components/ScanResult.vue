<template>
  <div class="scan-result" v-if="result">
    <div class="scan-result__type">
      <van-tag :type="tagType" size="medium">{{ typeLabel }}</van-tag>
    </div>
    <div class="scan-result__content" v-text="result.text"></div>
    <div class="scan-result__actions">
      <van-button
        v-for="act in actions"
        :key="act.action"
        size="small"
        type="primary"
        plain
        @click="$emit('action', act.action)"
      >
        {{ act.label }}
      </van-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Tag as VanTag, Button as VanButton } from 'vant'

defineOptions({ name: 'ScanResult' })

const props = defineProps<{
  result: { type: string; text: string }
  actions: Array<{ label: string; action: string }>
}>()

defineEmits<{
  action: [actionName: string]
}>()

const typeLabel = computed(() => {
  const map: Record<string, string> = {
    qrCode: 'QR码',
    barCode: '条形码',
  }
  return map[props.result.type] || '文本'
})

const tagType = computed(() => {
  const map: Record<string, 'primary' | 'success' | 'warning'> = {
    qrCode: 'primary',
    barCode: 'success',
  }
  return map[props.result.type] || 'warning'
})
</script>

<style scoped lang="scss">
.scan-result {
  background: #fff;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 12px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);

  &__type {
    margin-bottom: 8px;
  }

  &__content {
    font-size: 14px;
    color: #333;
    word-break: break-all;
    padding: 8px;
    background: #f7f8fa;
    border-radius: 4px;
    margin-bottom: 12px;
    line-height: 1.5;
  }

  &__actions {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }
}
</style>
