<template>
  <div class="basic-info-bar">
    <!-- 第一行：业务对象 + 名称 + 结算重量 -->
    <div class="info-row">
      <a-select
        :value="formData.clientType"
        @update:value="(val: SelectValue) => emit('clientTypeChange', String(val))"
        :options="clientTypeOptions"
        size="small"
        class="client-type-select"
        placeholder="对象类型"
        :disabled="isClientLocked"
      />
      <a-select
        :value="formData.clientId"
        @update:value="updateField('clientId', $event)"
        size="small"
        class="client-id-select"
        placeholder="选择业务对象"
        :disabled="isClientLocked"
        show-search
        :filter-option="false"
        :options="clientSelectOptions"
        :loading="clientSearchLoading"
        @search="emit('clientSearch', $event)"
        @focus="emit('clientSearch', '')"
      />
      <a-input
        :value="formData.planName"
        @update:value="updateField('planName', $event)"
        placeholder="输入报价名称"
        :maxlength="100"
        size="small"
        class="name-input"
      />
      <a-select
        :value="formData.settlementWeightStage"
        @update:value="updateField('settlementWeightStage', $event)"
        :options="weightStageOptions"
        size="small"
        class="weight-select"
        placeholder="结算重量"
      />
      <a-select
        :value="formData.weightRoundingMethod"
        @update:value="updateField('weightRoundingMethod', $event)"
        :options="roundingMethodOptions"
        size="small"
        class="rounding-select"
        placeholder="重量进位方式"
      />
      <a-date-picker
        :value="formData.effectiveDate ? dayjs(formData.effectiveDate) : undefined"
        @change="(val: any) => updateField('effectiveDate', val ? val.format('YYYY-MM-DD') : undefined)"
        size="small"
        class="effective-date-picker"
        placeholder="生效日期"
        format="YYYY-MM-DD"
        value-format="YYYY-MM-DD"
      />
      <div v-if="formData.clientType !== 'WD'" class="shared-shop-switch">
        <span class="switch-label">共享店铺</span>
        <a-switch
          :checked="formData.sharedShopEnabled"
          @update:checked="updateField('sharedShopEnabled', $event)"
          size="small"
        />
      </div>
    </div>
    <!-- 第二行：商务条款摘要 -->
    <div class="settings-summary" @click="emit('openSettings')">
      <span v-if="hasBizTerms" class="summary-text">{{ bizTermsSummary }}</span>
      <span v-else class="summary-placeholder">商务条款未设置 · 点击配置</span>
      <RightOutlined class="summary-arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import dayjs from 'dayjs'
import { RightOutlined } from '@ant-design/icons-vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { QuotationFormData } from '../composables/usePriceMatrix'
import { weightStageOptions } from '../composables/useQuotationForm'

const clientTypeOptions = [
  { value: 'KH', label: '客户' },
  { value: 'DL', label: '代理' },
  { value: 'WD', label: '网点' },
  { value: 'CB', label: '承包区' },
  { value: 'YZ', label: '驿站' },
]

const roundingMethodOptions = [
  { value: 1, label: '实际重量' },
  { value: 2, label: '四舍五入' },
  { value: 3, label: '点五进位' },
  { value: 4, label: '进位舍位' },
  { value: 5, label: '向上取整' },
  { value: 6, label: '分段进位' },
  { value: 7, label: '进舍百位' },
]

const props = defineProps<{
  formData: QuotationFormData
  isClientLocked?: boolean
  clientOptions?: { id: number | string; name: string; code?: string }[]
  clientSearchLoading?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:formData', data: QuotationFormData): void
  (e: 'openSettings'): void
  (e: 'clientTypeChange', type: string): void
  (e: 'clientSearch', keyword: string): void
}>()

const clientSelectOptions = computed(() =>
  (props.clientOptions || []).map(c => ({ value: c.id, label: c.name }))
)

function updateField(field: keyof QuotationFormData, value: any) {
  emit('update:formData', { ...props.formData, [field]: value })
}

// 商务条款摘要计算
const hasBizTerms = computed(() => {
  const d = props.formData
  return !!(d.paymentMode || d.billingCycle || d.throwRatio !== 8000)
})

const bizTermsSummary = computed(() => {
  const d = props.formData
  const parts: string[] = []
  if (d.paymentMode) {
    const modeMap: Record<string, string> = { prepay: '预付', postpay: '后付', mixed: '混合' }
    parts.push(modeMap[d.paymentMode] || d.paymentMode)
  }
  if (d.billingCycle && d.billingDay) {
    const cycleMap: Record<string, string> = { week: '周', month: '月', quarter: '季', year: '年' }
    parts.push(`每${cycleMap[d.billingCycle] || d.billingCycle}${d.billingDay}号出账`)
  }
  if (d.paymentDueDay) {
    parts.push(`${d.paymentDueDay}日付款`)
  }
  if (d.throwRatio && d.throwRatio !== 8000) {
    parts.push(`抛比${d.throwRatio}`)
  } else if (d.throwRatio === 8000) {
    parts.push('抛比8000')
  }
  return parts.join(' · ') || '商务条款未设置'
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.basic-info-bar {
  padding: 8px 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: $bg-card;
}

.info-row {
  display: flex;
  align-items: center;
  gap: 12px;

  .name-input {
    flex: 1;
    min-width: 0;
  }

  .client-type-select {
    width: 110px;
    flex-shrink: 0;
  }

  .client-id-select {
    width: 180px;
    flex-shrink: 0;
  }

  .weight-select {
    width: 160px;
    flex-shrink: 0;
  }

  .rounding-select {
    width: 150px;
    flex-shrink: 0;
  }

  .effective-date-picker {
    width: 140px;
    flex-shrink: 0;
  }

  .shared-shop-switch {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-shrink: 0;

    .switch-label {
      font-size: 12px;
      color: $text-regular;
      white-space: nowrap;
    }
  }
}

.settings-summary {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: $border-radius-sm;
  cursor: pointer;
  transition: $transition-base;
  font-size: $font-size-sm;

  &:hover {
    background: $color-primary-bg;
  }

  .summary-text {
    color: $text-regular;
  }

  .summary-placeholder {
    color: $text-secondary;
    font-style: italic;
  }

  .summary-arrow {
    color: $text-secondary;
    font-size: 10px;
    margin-left: auto;
  }
}
</style>
