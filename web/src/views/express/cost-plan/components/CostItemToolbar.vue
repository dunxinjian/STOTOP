<template>
  <div class="cost-item-toolbar">
    <a-form layout="inline" class="toolbar-form">
      <a-form-item class="toolbar-form-item">
        <a-input
          :value="itemName"
          placeholder="成本项名称"
          allow-clear
          :style="{ width: '200px' }"
          @update:value="emit('update:itemName', $event)"
        />
      </a-form-item>
      <a-form-item class="toolbar-form-item">
        <a-select
          :value="itemType"
          :style="{ width: '130px' }"
          :options="typeOptions"
          @change="(val: SelectValue) => emit('update:itemType', Number(val))"
        />
      </a-form-item>
      <a-form-item class="toolbar-form-item">
        <a-input-number
          :value="sortOrder"
          placeholder="排序号"
          :style="{ width: '100px' }"
          :min="0"
          @update:value="(val: ValueType) => emit('update:sortOrder', Number(val))"
        />
      </a-form-item>
      <a-form-item v-if="itemType === 4" class="toolbar-form-item">
        <a-select
          :value="settlementWeightStage"
          :style="{ width: '140px' }"
          :options="weightStageOptions"
          placeholder="结算重量"
          @change="(val: SelectValue) => emit('update:settlementWeightStage', val == null ? undefined : Number(val))"
        />
      </a-form-item>
    </a-form>
  </div>
</template>

<script setup lang="ts">
import type { SelectValue } from 'ant-design-vue/es/select'
import type { ValueType } from 'ant-design-vue/es/input-number/src/utils/MiniDecimal'
import { WEIGHT_STAGE_OPTIONS } from '../constants'

defineProps<{
  itemName: string
  itemType: number
  sortOrder: number
  settlementWeightStage?: number
}>()

const emit = defineEmits<{
  'update:itemName': [val: string]
  'update:itemType': [val: number]
  'update:sortOrder': [val: number]
  'update:settlementWeightStage': [val: number | undefined]
}>()

const typeOptions = [
  { label: '全国单价', value: 1 },
  { label: '省份矩阵', value: 2 },
  { label: '城市加收', value: 3 },
  { label: '一口价', value: 4 },
]

// 结算重量环节：单一真源 ../constants（避免与 CostPlanEdit 两处硬编码漂移）
const weightStageOptions = WEIGHT_STAGE_OPTIONS
</script>

<style scoped lang="scss">
.cost-item-toolbar {
  width: 100%;
}

.toolbar-form {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: 0;
  height: 100%;
}

.toolbar-form-item {
  margin-bottom: 0 !important;
}
</style>
