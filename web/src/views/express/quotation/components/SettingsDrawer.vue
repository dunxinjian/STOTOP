<template>
  <a-drawer
    :open="visible"
    title="商务条款配置"
    placement="right"
    :width="400"
    @close="handleCancel"
  >
    <a-form :model="localForm" layout="vertical" size="small">
      <!-- 付款配置 -->
      <div class="form-section">
        <div class="section-label">付款配置</div>
        <a-form-item label="付款方式">
          <a-select v-model:value="localForm.paymentMode" placeholder="请选择" allowClear>
            <a-select-option value="prepay">预付</a-select-option>
            <a-select-option value="postpay">后付</a-select-option>
            <a-select-option value="mixed">混合</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="预付比例 (%)" v-if="localForm.paymentMode === 'prepay' || localForm.paymentMode === 'mixed'">
          <a-input-number
            v-model:value="localForm.prepayRatio"
            :min="0"
            :max="100"
            :precision="1"
            placeholder="0-100"
            style="width: 100%"
          />
        </a-form-item>
      </div>

      <!-- 账单配置 -->
      <div class="form-section">
        <div class="section-label">账单配置</div>
        <a-form-item label="账单周期">
          <a-select v-model:value="localForm.billingCycle" placeholder="请选择" allowClear>
            <a-select-option value="week">周</a-select-option>
            <a-select-option value="month">月</a-select-option>
            <a-select-option value="quarter">季</a-select-option>
            <a-select-option value="year">年</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="出账日">
          <a-input-number
            v-model:value="localForm.billingDay"
            :min="1"
            :max="31"
            placeholder="每月第几日"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="付款截止日">
          <a-input-number
            v-model:value="localForm.paymentDueDay"
            :min="1"
            :max="90"
            placeholder="出账后几日内付款"
            style="width: 100%"
          />
        </a-form-item>
      </div>

      <!-- 计费参数 -->
      <div class="form-section">
        <div class="section-label">计费参数</div>
        <a-form-item label="抛比">
          <a-input-number
            v-model:value="localForm.throwRatio"
            :min="1000"
            :max="12000"
            :step="1000"
            placeholder="默认 8000"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="保价费率 (%)">
          <a-input-number
            v-model:value="localForm.insuranceRate"
            :min="0"
            :max="10"
            :precision="2"
            :step="0.1"
            placeholder="如 0.3"
            style="width: 100%"
          />
        </a-form-item>
      </div>

      <!-- 其他 -->
      <div class="form-section">
        <div class="section-label">其他</div>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="localForm.remark"
            :rows="3"
            :maxlength="500"
            show-count
            placeholder="请输入备注"
          />
        </a-form-item>
        <a-form-item label="OA审批信息" v-if="localForm.oaApprovalInfo">
          <div class="oa-info">{{ localForm.oaApprovalInfo }}</div>
        </a-form-item>
      </div>
    </a-form>

    <template #footer>
      <div class="drawer-footer">
        <a-button @click="handleCancel">取消</a-button>
        <a-button type="primary" @click="handleSave">保存</a-button>
      </div>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { QuotationFormData } from '../composables/usePriceMatrix'

const props = defineProps<{
  visible: boolean
  formData: QuotationFormData
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'save', data: Partial<QuotationFormData>): void
}>()

const localForm = reactive({
  paymentMode: undefined as string | undefined,
  prepayRatio: undefined as number | undefined,
  billingCycle: undefined as string | undefined,
  billingDay: undefined as number | undefined,
  paymentDueDay: undefined as number | undefined,
  throwRatio: 8000 as number | undefined,
  insuranceRate: undefined as number | undefined,
  remark: undefined as string | undefined,
  oaApprovalInfo: undefined as string | undefined,
})

// 打开时同步父数据
watch(() => props.visible, (val) => {
  if (val) {
    localForm.paymentMode = props.formData.paymentMode
    localForm.prepayRatio = props.formData.prepayRatio
    localForm.billingCycle = props.formData.billingCycle
    localForm.billingDay = props.formData.billingDay
    localForm.paymentDueDay = props.formData.paymentDueDay
    localForm.throwRatio = props.formData.throwRatio ?? 8000
    localForm.insuranceRate = props.formData.insuranceRate
    localForm.remark = props.formData.remark
    localForm.oaApprovalInfo = props.formData.oaApprovalInfo
  }
})

function handleSave() {
  emit('save', { ...localForm })
  emit('update:visible', false)
}

function handleCancel() {
  emit('update:visible', false)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.form-section {
  margin-bottom: 20px;

  .section-label {
    font-size: $font-size-base;
    font-weight: 500;
    color: $text-primary;
    margin-bottom: 12px;
    padding-bottom: 6px;
    border-bottom: 1px solid $border-color-lighter;
  }
}

.oa-info {
  padding: 8px 12px;
  background: var(--bg-muted);
  border-radius: $border-radius-sm;
  font-size: $font-size-sm;
  color: $text-regular;
  line-height: 1.6;
}

.drawer-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

:deep(.ant-form-item) {
  margin-bottom: 12px;
}
</style>
