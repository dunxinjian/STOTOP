<template>
  <div class="national-price-row">
    <div class="price-field">
      <span class="price-label">全国单价</span>
      <a-input-number
        :value="price"
        :min="0"
        :precision="2"
        :controls="false"
        style="width: 160px"
        @change="onPriceChange"
      />
      <span class="price-unit">元</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ValueType } from 'ant-design-vue/es/input-number/src/utils/MiniDecimal'

defineProps<{
  price: number
}>()

const emit = defineEmits<{
  'update:price': [value: number]
}>()

function onPriceChange(value: ValueType) {
  emit('update:price', value == null ? 0 : Number(value))
}
</script>

<style scoped lang="scss">
.national-price-row {
  background: #fff;
  border-radius: 6px;
  padding: 16px;
  border: 1px solid #f0f0f0;
}

.price-field {
  display: flex;
  align-items: center;
  gap: 12px;
}

.price-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
  white-space: nowrap;
}

.price-unit {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
}
</style>
