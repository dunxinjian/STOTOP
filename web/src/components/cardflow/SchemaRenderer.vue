<script setup lang="ts">
import { computed, ref } from 'vue'
import type { CardComponentRuntime, SchemaFieldDefinition } from '@/types/cardflow'
import AccountSelector from './fields/AccountSelector.vue'
import AuxiliarySelector from './fields/AuxiliarySelector.vue'
import BankAccountSelector from './fields/BankAccountSelector.vue'
import CardComponentRenderer from './runtime/CardComponentRenderer.vue'
import type { DetailRow } from './CardDetailTable.vue'
import {
  Field as VanField,
  DatePicker as VanDatePicker,
  Picker as VanPicker,
  Popup as VanPopup,
  Uploader as VanUploader,
} from 'vant'
import 'vant/es/field/style'
import 'vant/es/date-picker/style'
import 'vant/es/picker/style'
import 'vant/es/popup/style'
import 'vant/es/uploader/style'

// ==================== Props & Emits ====================

interface Props {
  schema: SchemaFieldDefinition[]
  components?: CardComponentRuntime[]
  modelValue: Record<string, any>
  detailRows?: DetailRow[]
  mode: 'edit' | 'view'
  platform?: 'pc' | 'mobile'
  errors?: Record<string, string>
  /** 财务上下文：账套 ID，用于 account/auxiliary 选择器 */
  accountSetId?: number | null
  /** 组织上下文：用于 bankAccount 选择器 */
  orgId?: number | null
  /** 移动端 Popup 自定义类名（用于在卡片侧栏中限制宽度对齐） */
  popupClass?: string
  /** 移动端 Popup 遮罩自定义类名 */
  popupOverlayClass?: string
  /** 移动端 Popup teleport 目标，默认 body；传入 selector 可限定范围 */
  popupTeleport?: string
}

const props = withDefaults(defineProps<Props>(), {
  platform: 'pc',
  components: () => [],
  detailRows: () => [],
  errors: () => ({}),
  accountSetId: null,
  orgId: null,
  popupClass: '',
  popupOverlayClass: '',
  popupTeleport: 'body',
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, any>): void
  (e: 'update:detailRows', value: DetailRow[]): void
}>()

// ==================== State ====================

const formData = computed({
  get: () => props.modelValue || {},
  set: (val) => emit('update:modelValue', val),
})

// Vant 弹窗状态
const datePopupVisible = ref<Record<string, boolean>>({})
const enumPopupVisible = ref<Record<string, boolean>>({})

// ==================== Helpers ====================

function updateField(key: string, value: any) {
  emit('update:modelValue', { ...formData.value, [key]: value })
}

function formatMoney(val: any): string {
  const num = Number(val)
  if (isNaN(num) || val === null || val === undefined || val === '') return ''
  return `¥${num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function formatDate(val: any): string {
  if (!val) return ''
  const d = new Date(val)
  if (isNaN(d.getTime())) return String(val)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function getEnumLabel(field: SchemaFieldDefinition, val: any): string {
  if (!val && val !== 0) return ''
  return String(val)
}

function getViewValue(field: SchemaFieldDefinition): string {
  const val = formData.value[field.key]
  if (val === null || val === undefined || val === '') return '-'
  switch (field.type) {
    case 'money':
      return formatMoney(val)
    case 'date':
      return formatDate(val)
    case 'enum':
      return getEnumLabel(field, val)
    default:
      return String(val)
  }
}

function isImageFile(name: string): boolean {
  return /\.(jpg|jpeg|png|gif|webp|bmp|svg)$/i.test(name)
}

function getPickerColumns(field: SchemaFieldDefinition) {
  return (field.options || []).map((opt) => ({ text: opt, value: opt }))
}

// Vant 日期确认
function onDateConfirm(key: string, { selectedValues }: any) {
  updateField(key, selectedValues.join('-'))
  datePopupVisible.value[key] = false
}

// Vant 枚举确认
function onEnumConfirm(key: string, { selectedOptions }: any) {
  updateField(key, selectedOptions[0]?.value ?? '')
  enumPopupVisible.value[key] = false
}

// PC 日期变更
function onPcDateChange(key: string, _date: any, dateStr: string) {
  updateField(key, dateStr)
}

// ==================== Computed ====================

const isPC = computed(() => props.platform === 'pc')
const isEdit = computed(() => props.mode === 'edit')
const hasRuntimeComponents = computed(() => (props.components?.length ?? 0) > 0)
</script>

<template>
  <div class="schema-renderer" :class="[`schema-renderer--${platform}`, `schema-renderer--${mode}`]">
    <CardComponentRenderer
      v-if="hasRuntimeComponents"
      :components="components"
      :model-value="formData"
      :detail-rows="detailRows"
      :mode="mode"
      :platform="platform"
      :account-set-id="accountSetId"
      :org-id="orgId"
      @update:model-value="(value) => emit('update:modelValue', value)"
      @update:detail-rows="(value) => emit('update:detailRows', value)"
    />

    <!-- ======================== PC 模式 ======================== -->
    <template v-else-if="isPC">
      <!-- PC 编辑模式 -->
      <a-form v-if="isEdit" layout="vertical" class="schema-form">
        <a-form-item
          v-for="field in schema"
          :key="field.key"
          :label="field.label"
          :required="field.required"
          :validate-status="errors?.[field.key] ? 'error' : undefined"
          :help="errors?.[field.key]"
        >
          <!-- text -->
          <a-input
            v-if="field.type === 'text'"
            :value="formData[field.key]"
            :placeholder="field.placeholder || `请输入${field.label}`"
            :disabled="field.readonly"
            @update:value="(v: string) => updateField(field.key, v)"
          />

          <!-- money -->
          <a-input-number
            v-else-if="field.type === 'money'"
            :value="formData[field.key]"
            :placeholder="field.placeholder || '请输入金额'"
            :disabled="field.readonly"
            :precision="2"
            :formatter="(v: any) => v ? `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : ''"
            :parser="(v: string) => v.replace(/,/g, '')"
            style="width: 100%"
            @update:value="(v: any) => updateField(field.key, v)"
          />

          <!-- enum -->
          <a-select
            v-else-if="field.type === 'enum'"
            :value="formData[field.key]"
            :placeholder="field.placeholder || `请选择${field.label}`"
            :disabled="field.readonly"
            :options="(field.options || []).map(o => ({ label: o, value: o }))"
            @update:value="(v: any) => updateField(field.key, v)"
          />

          <!-- date -->
          <a-date-picker
            v-else-if="field.type === 'date'"
            :value="formData[field.key]"
            :placeholder="field.placeholder || '请选择日期'"
            :disabled="field.readonly"
            style="width: 100%"
            value-format="YYYY-MM-DD"
            @change="(d: any, ds: string) => onPcDateChange(field.key, d, ds)"
          />

          <!-- file -->
          <a-upload
            v-else-if="field.type === 'file'"
            :file-list="formData[field.key] || []"
            :accept="field.accept"
            :disabled="field.readonly"
            list-type="picture-card"
            @change="({ fileList }: any) => updateField(field.key, fileList)"
          >
            <div>
              <plus-outlined />
              <div style="margin-top: 4px">上传</div>
            </div>
          </a-upload>

          <!-- user -->
          <a-input
            v-else-if="field.type === 'user'"
            :value="formData[field.key]?.name || formData[field.key] || ''"
            :placeholder="field.placeholder || '请选择人员'"
            readonly
            @click="() => { /* TODO: 接入 UserPicker */ }"
          >
            <template #suffix>
              <user-outlined />
            </template>
          </a-input>

          <!-- org -->
          <a-input
            v-else-if="field.type === 'org'"
            :value="formData[field.key]?.name || formData[field.key] || ''"
            :placeholder="field.placeholder || '请选择组织'"
            readonly
            @click="() => { /* TODO: 接入 OrgPicker */ }"
          >
            <template #suffix>
              <apartment-outlined />
            </template>
          </a-input>

          <!-- cardRef -->
          <a-input
            v-else-if="field.type === 'cardRef'"
            :value="formData[field.key]?.cardNumber || formData[field.key] || ''"
            :placeholder="field.placeholder || '请选择关联卡片'"
            readonly
            @click="() => { /* TODO: 接入 CardRelationPicker */ }"
          >
            <template #suffix>
              <link-outlined />
            </template>
          </a-input>

          <!-- account 财务科目 -->
          <AccountSelector
            v-else-if="field.type === 'account'"
            :model-value="formData[field.key]"
            :account-set-id="accountSetId"
            :disabled="field.readonly"
            :placeholder="field.placeholder || `请选择${field.label}`"
            @update:model-value="(v: any) => updateField(field.key, v)"
          />

          <!-- auxiliary 辅助核算 -->
          <AuxiliarySelector
            v-else-if="field.type === 'auxiliary'"
            :model-value="formData[field.key]"
            :aux-type="field.auxType || 'employee'"
            :account-set-id="accountSetId"
            :disabled="field.readonly"
            :placeholder="field.placeholder || `请选择${field.label}`"
            @update:model-value="(v: any) => updateField(field.key, v)"
          />

          <!-- bankAccount 银行账户 -->
          <BankAccountSelector
            v-else-if="field.type === 'bankAccount'"
            :model-value="formData[field.key]"
            :org-id="orgId"
            :disabled="field.readonly"
            :placeholder="field.placeholder || `请选择${field.label}`"
            @update:model-value="(v: any) => updateField(field.key, v)"
          />

          <!-- voucherRef 凭证引用（只读链接） -->
          <a
            v-else-if="field.type === 'voucherRef'"
            class="schema-form__voucher-link"
            :href="formData[field.key]?.id ? `/finance/vouchers/${formData[field.key].id}` : undefined"
            target="_blank"
          >
            <template v-if="formData[field.key]?.voucherNumber">
              {{ formData[field.key].voucherNumber }}
              <span v-if="formData[field.key]?.period" style="color: var(--text-3)">
                ({{ formData[field.key].period }})
              </span>
            </template>
            <span v-else style="color: var(--text-3)">-</span>
          </a>
        </a-form-item>
      </a-form>

      <!-- PC 只读模式 -->
      <div v-else class="schema-view">
        <div v-for="field in schema" :key="field.key" class="schema-view__item">
          <span class="schema-view__label">
            <span v-if="field.required" class="schema-view__required">*</span>
            {{ field.label }}
          </span>
          <span class="schema-view__value">
            <!-- money -->
            <template v-if="field.type === 'money'">
              {{ formatMoney(formData[field.key]) || '-' }}
            </template>

            <!-- file -->
            <template v-else-if="field.type === 'file'">
              <span v-if="!formData[field.key]?.length">-</span>
              <span v-else class="schema-view__files">
                <template v-for="(f, idx) in formData[field.key]" :key="idx">
                  <img v-if="isImageFile(f.name || f.fileName || '')" :src="f.url || f.thumbUrl" class="schema-view__thumb" />
                  <a v-else :href="f.url" target="_blank" class="schema-view__file-link">
                    {{ f.name || f.fileName || '文件' }}
                  </a>
                </template>
              </span>
            </template>

            <!-- user -->
            <template v-else-if="field.type === 'user'">
              <span v-if="formData[field.key]" class="schema-view__user">
                <a-avatar v-if="formData[field.key]?.avatar" :src="formData[field.key].avatar" :size="20" />
                {{ formData[field.key]?.name || formData[field.key] || '-' }}
              </span>
              <span v-else>-</span>
            </template>

            <!-- org -->
            <template v-else-if="field.type === 'org'">
              {{ formData[field.key]?.name || formData[field.key] || '-' }}
            </template>

            <!-- cardRef -->
            <template v-else-if="field.type === 'cardRef'">
              <a v-if="formData[field.key]" class="schema-view__card-link">
                {{ formData[field.key]?.cardNumber }} {{ formData[field.key]?.title }}
              </a>
              <span v-else>-</span>
            </template>

            <!-- account / auxiliary / bankAccount / voucherRef -->
            <template v-else-if="field.type === 'account'">
              <span v-if="formData[field.key]">
                {{ formData[field.key]?.code }} {{ formData[field.key]?.name }}
              </span>
              <span v-else>-</span>
            </template>
            <template v-else-if="field.type === 'auxiliary'">
              <span v-if="formData[field.key]">
                {{ formData[field.key]?.name }}
              </span>
              <span v-else>-</span>
            </template>
            <template v-else-if="field.type === 'bankAccount'">
              <span v-if="formData[field.key]">
                {{ formData[field.key]?.accountNo }}
                <span v-if="formData[field.key]?.bankName" style="color: var(--text-3)">
                  ({{ formData[field.key].bankName }})
                </span>
              </span>
              <span v-else>-</span>
            </template>
            <template v-else-if="field.type === 'voucherRef'">
              <a
                v-if="formData[field.key]?.id"
                class="schema-view__card-link"
                :href="`/finance/vouchers/${formData[field.key].id}`"
                target="_blank"
              >
                {{ formData[field.key]?.voucherNumber || ('#' + formData[field.key].id) }}
              </a>
              <span v-else>-</span>
            </template>

            <!-- text / enum / date / default -->
            <template v-else>
              {{ getViewValue(field) }}
            </template>
          </span>
        </div>
      </div>
    </template>

    <!-- ======================== 移动端模式 ======================== -->
    <template v-else>
      <!-- 移动端编辑模式 -->
      <template v-if="isEdit">
        <template v-for="field in schema" :key="field.key">
          <!-- text -->
          <VanField
            v-if="field.type === 'text'"
            :label="field.label"
            :model-value="formData[field.key] || ''"
            :required="field.required"
            :readonly="field.readonly"
            :placeholder="field.placeholder || `请输入${field.label}`"
            :error-message="errors?.[field.key]"
            @update:model-value="(v: string) => updateField(field.key, v)"
          />

          <!-- money -->
          <VanField
            v-else-if="field.type === 'money'"
            :label="field.label"
            :model-value="formData[field.key]?.toString() || ''"
            :required="field.required"
            :readonly="field.readonly"
            type="number"
            :placeholder="field.placeholder || '请输入金额'"
            :error-message="errors?.[field.key]"
            @update:model-value="(v: string) => updateField(field.key, Number(v))"
          >
            <template #left-icon>
              <span style="color: var(--color-danger); font-weight: bold">¥</span>
            </template>
          </VanField>

          <!-- enum -->
          <VanField
            v-else-if="field.type === 'enum'"
            :label="field.label"
            :model-value="formData[field.key] || ''"
            :required="field.required"
            readonly
            is-link
            :placeholder="field.placeholder || `请选择${field.label}`"
            :error-message="errors?.[field.key]"
            @click="enumPopupVisible[field.key] = true"
          />

          <!-- date -->
          <VanField
            v-else-if="field.type === 'date'"
            :label="field.label"
            :model-value="formatDate(formData[field.key])"
            :required="field.required"
            readonly
            is-link
            :placeholder="field.placeholder || '请选择日期'"
            :error-message="errors?.[field.key]"
            @click="datePopupVisible[field.key] = true"
          />

          <!-- file -->
          <VanField
            v-else-if="field.type === 'file'"
            :label="field.label"
            :required="field.required"
            :error-message="errors?.[field.key]"
          >
            <template #input>
              <VanUploader
                :model-value="formData[field.key] || []"
                :accept="field.accept || 'image/*'"
                :max-count="5"
                @update:model-value="(v: any) => updateField(field.key, v)"
              />
            </template>
          </VanField>

          <!-- user / org / cardRef -->
          <VanField
            v-else-if="field.type === 'user' || field.type === 'org' || field.type === 'cardRef'"
            :label="field.label"
            :model-value="formData[field.key]?.name || formData[field.key]?.cardNumber || formData[field.key] || ''"
            :required="field.required"
            readonly
            :is-link="!field.readonly"
            :placeholder="field.placeholder || `请选择${field.label}`"
            :error-message="errors?.[field.key]"
            @click="() => { if (!field.readonly) { /* TODO: 接入对应选择器 */ } }"
          />
        </template>

        <!-- 日期弹窗 -->
        <VanPopup
          v-for="field in schema.filter((f) => f.type === 'date')"
          :key="'dp_' + field.key"
          v-model:show="datePopupVisible[field.key]"
          position="bottom"
          round
          :class="props.popupClass"
          :overlay-class="props.popupOverlayClass"
          :teleport="props.popupTeleport"
        >
          <VanDatePicker
            :title="`选择${field.label}`"
            @confirm="(val: any) => onDateConfirm(field.key, val)"
            @cancel="datePopupVisible[field.key] = false"
          />
        </VanPopup>

        <!-- 枚举选择弹窗 -->
        <VanPopup
          v-for="field in schema.filter((f) => f.type === 'enum')"
          :key="'pk_' + field.key"
          v-model:show="enumPopupVisible[field.key]"
          position="bottom"
          round
          :class="props.popupClass"
          :overlay-class="props.popupOverlayClass"
          :teleport="props.popupTeleport"
        >
          <VanPicker
            :title="field.label"
            :columns="getPickerColumns(field)"
            @confirm="(val: any) => onEnumConfirm(field.key, val)"
            @cancel="enumPopupVisible[field.key] = false"
          />
        </VanPopup>
      </template>

      <!-- 移动端只读模式 -->
      <div v-else class="schema-view schema-view--mobile">
        <div v-for="field in schema" :key="field.key" class="schema-view__item">
          <span class="schema-view__label">
            <span v-if="field.required" class="schema-view__required">*</span>
            {{ field.label }}
          </span>
          <span class="schema-view__value">
            <template v-if="field.type === 'money'">
              {{ formatMoney(formData[field.key]) || '-' }}
            </template>
            <template v-else-if="field.type === 'file'">
              <span v-if="!formData[field.key]?.length">-</span>
              <span v-else class="schema-view__files">
                <template v-for="(f, idx) in formData[field.key]" :key="idx">
                  <img v-if="isImageFile(f.name || '')" :src="f.url || f.thumbUrl" class="schema-view__thumb" />
                  <a v-else :href="f.url" target="_blank">{{ f.name || '文件' }}</a>
                </template>
              </span>
            </template>
            <template v-else-if="field.type === 'cardRef' && formData[field.key]">
              <a class="schema-view__card-link">
                {{ formData[field.key]?.cardNumber }} {{ formData[field.key]?.title }}
              </a>
            </template>
            <template v-else>
              {{ getViewValue(field) }}
            </template>
          </span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="scss">
.schema-renderer {
  width: 100%;

  &--pc {
    .schema-form {
      :deep(.ant-form-item) {
        margin-bottom: 16px;
      }
    }
  }
}

.schema-view {
  display: flex;
  flex-direction: column;
  gap: 12px;

  &__item {
    display: flex;
    align-items: flex-start;
    line-height: 1.6;
    padding: 6px 0;
    border-bottom: 1px solid var(--border);
  }

  &__label {
    flex-shrink: 0;
    width: 100px;
    color: var(--text-2);
    font-size: 13px;
  }

  &__required {
    color: var(--color-danger);
    margin-right: 2px;
  }

  &__value {
    flex: 1;
    color: var(--text-1);
    font-size: 14px;
    word-break: break-all;
  }

  &__files {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
  }

  &__thumb {
    width: 48px;
    height: 48px;
    object-fit: cover;
    border-radius: 4px;
    border: 1px solid var(--border);
  }

  &__file-link {
    color: var(--text-1);
    text-decoration: none;

    &:hover {
      color: var(--color-primary);
      text-decoration: underline;
    }
  }

  &__user {
    display: inline-flex;
    align-items: center;
    gap: 6px;
  }

  &__card-link {
    color: var(--text-1);
    cursor: pointer;

    &:hover {
      color: var(--color-primary);
      text-decoration: underline;
    }
  }

  &--mobile {
    padding: 0 16px;

    .schema-view__item {
      padding: 10px 0;
    }

    .schema-view__label {
      width: 80px;
      font-size: 13px;
    }

    .schema-view__value {
      font-size: 14px;
    }
  }
}
</style>
