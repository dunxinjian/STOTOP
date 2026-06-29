<script setup lang="ts">
import { computed } from 'vue'
import type { CardComponentRuntime } from '@/types/cardflow'
import type { DetailRow } from '../CardDetailTable.vue'
import AmountSummaryComponent from './components/AmountSummaryComponent.vue'
import DetailTableComponent from './components/DetailTableComponent.vue'
import RelationCardsComponent from './components/RelationCardsComponent.vue'
import BudgetStatusComponent from './components/BudgetStatusComponent.vue'
import InvoiceStatusComponent from './components/InvoiceStatusComponent.vue'
import LoanOffsetComponent from './components/LoanOffsetComponent.vue'
import PaymentInfoComponent from './components/PaymentInfoComponent.vue'
import RiskAlertComponent from './components/RiskAlertComponent.vue'
import RouteDecisionComponent from './components/RouteDecisionComponent.vue'
import DynamicApproverComponent from './components/DynamicApproverComponent.vue'

const props = withDefaults(defineProps<{
  components: CardComponentRuntime[]
  modelValue: Record<string, any>
  detailRows?: DetailRow[]
  mode: 'edit' | 'view'
  platform?: 'pc' | 'mobile'
  previewVariant?: 'runtime' | 'designer'
  accountSetId?: number | null
  orgId?: number | null
  isAdmin?: boolean
}>(), {
  detailRows: () => [],
  platform: 'pc',
  previewVariant: 'runtime',
  accountSetId: null,
  orgId: null,
  isAdmin: false,
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, any>): void
  (e: 'update:detailRows', value: DetailRow[]): void
}>()

const visibleComponents = computed(() =>
  (props.components || []).filter((component) => component.visible && component.access !== 'hidden'),
)

const isDesignerPreview = computed(() => props.previewVariant === 'designer')

function isEditable(component: CardComponentRuntime): boolean {
  return props.mode === 'edit' && component.editable
}

function isDesignerFieldControl(component: CardComponentRuntime): boolean {
  if (!isDesignerPreview.value || component.binding?.source !== 'cardField') return false
  return true
}

function renderFieldControl(component: CardComponentRuntime): boolean {
  return isEditable(component) || isDesignerFieldControl(component)
}

function fieldValue(component: CardComponentRuntime): any {
  const key = component.binding?.fieldKey
  const value = key ? props.modelValue?.[key] : component.value
  if (value === null || value === undefined || value === '') {
    return component.props?.defaultValue ?? value
  }
  return value
}

function updateField(component: CardComponentRuntime, value: any) {
  const key = component.binding?.fieldKey
  if (!key || !isEditable(component)) return
  emit('update:modelValue', { ...(props.modelValue || {}), [key]: value })
}

function fieldControlKind(component: CardComponentRuntime): string {
  return component.props?.controlKind || component.type
}

function normalizedOptions(component: CardComponentRuntime): Array<{ label: string; value: string }> {
  const options = component.props?.options
  if (!Array.isArray(options)) return []
  return options
    .map((option) => {
      if (typeof option === 'string' || typeof option === 'number') {
        return { label: String(option), value: String(option) }
      }
      return {
        label: String(option?.label ?? option?.value ?? ''),
        value: String(option?.value ?? option?.label ?? ''),
      }
    })
    .filter(option => option.label && option.value)
}

function optionLabel(component: CardComponentRuntime, value: any): string {
  const stringValue = String(value)
  return normalizedOptions(component).find(option => option.value === stringValue)?.label || stringValue
}

function isTextareaField(component: CardComponentRuntime): boolean {
  return fieldControlKind(component) === 'textarea' || component.type === 'textarea'
}

function isChoiceField(component: CardComponentRuntime): boolean {
  const kind = fieldControlKind(component)
  return ['radio', 'checkbox', 'select', 'category'].includes(kind) || ['radio', 'checkbox'].includes(component.type)
}

function isCheckboxField(component: CardComponentRuntime): boolean {
  return fieldControlKind(component) === 'checkbox' || component.type === 'checkbox'
}

function isDateRangeField(component: CardComponentRuntime): boolean {
  return fieldControlKind(component) === 'dateRange' || component.type === 'dateRange'
}

function isAttachmentField(component: CardComponentRuntime): boolean {
  const kind = fieldControlKind(component)
  return ['attachment', 'imageList', 'signature'].includes(kind) || ['attachment', 'imageList', 'signature'].includes(component.type)
}

function isDateField(component: CardComponentRuntime): boolean {
  return fieldControlKind(component) === 'date' || component.type === 'date'
}

function isNumberField(component: CardComponentRuntime): boolean {
  return ['number', 'money'].includes(fieldControlKind(component)) || ['number', 'money'].includes(component.type)
}

function attachmentList(component: CardComponentRuntime): Array<{ name: string; url?: string }> {
  const value = fieldValue(component)
  if (!value) return []
  const list = Array.isArray(value) ? value : [value]
  return list.map((item) => {
    if (typeof item === 'string') return { name: item }
    return { name: item?.name || item?.fileName || item?.title || '附件', url: item?.url }
  })
}

function maskedValue(value: string, pattern?: string | null): string {
  if (!value) return value
  if (pattern === 'phone' && value.length >= 7) {
    return `${value.slice(0, 3)}****${value.slice(-4)}`
  }
  if (pattern === 'idCard' && value.length >= 10) {
    return `${value.slice(0, 4)}**********${value.slice(-4)}`
  }
  return value.length > 4 ? `${value.slice(0, 2)}****${value.slice(-2)}` : '****'
}

function formatFieldValue(component: CardComponentRuntime): string {
  const value = fieldValue(component)
  const emptyText = component.props?.emptyText || '-'
  if (value === null || value === undefined || value === '') return emptyText

  if (component.masked || component.access === 'masked') {
    return String(value)
  }

  if (isAttachmentField(component)) {
    const names = attachmentList(component).map(item => item.name).filter(Boolean)
    return names.length ? names.join('、') : emptyText
  }

  if (isDateRangeField(component)) {
    if (Array.isArray(value)) return `${value[0] || '-'} 至 ${value[1] || '-'}`
    if (typeof value === 'object') return `${value.start || value.startDate || '-'} 至 ${value.end || value.endDate || '-'}`
  }

  if (component.type === 'checkbox' || fieldControlKind(component) === 'checkbox') {
    const values = Array.isArray(value) ? value : [value]
    return values.map(item => optionLabel(component, item)).join('、') || emptyText
  }

  if (isChoiceField(component)) {
    return optionLabel(component, value)
  }

  if (component.type === 'money' || fieldControlKind(component) === 'money') {
    const numberValue = Number(value)
    const precision = Number.isFinite(Number(component.props?.precision)) ? Number(component.props?.precision) : 2
    const symbol = component.props?.currencySymbol || component.props?.prefix || '¥'
    return Number.isFinite(numberValue)
      ? `${symbol}${numberValue.toLocaleString('zh-CN', { minimumFractionDigits: precision, maximumFractionDigits: precision })}${component.props?.suffix || ''}`
      : String(value)
  }

  if (component.type === 'number' || fieldControlKind(component) === 'number') {
    const numberValue = Number(value)
    const precision = Number.isFinite(Number(component.props?.precision)) ? Number(component.props?.precision) : null
    const formatted = Number.isFinite(numberValue) && precision !== null ? numberValue.toFixed(precision) : String(value)
    return `${component.props?.prefix || ''}${formatted}${component.props?.suffix || ''}`
  }

  if (Array.isArray(value)) return value.map(item => typeof item === 'object' ? item.name || item.title || JSON.stringify(item) : String(item)).join('、')
  if (typeof value === 'object') return value.name || value.title || value.cardNumber || JSON.stringify(value)
  return `${component.props?.prefix || ''}${String(value)}${component.props?.suffix || ''}`
}

function updateCheckboxField(component: CardComponentRuntime, optionValue: string, checked: boolean) {
  const current = fieldValue(component)
  const values = Array.isArray(current) ? current.map(String) : current ? [String(current)] : []
  const next = checked
    ? Array.from(new Set([...values, optionValue]))
    : values.filter(value => value !== optionValue)
  updateField(component, next)
}

function updateChoiceField(component: CardComponentRuntime, optionValue: string) {
  if (!isEditable(component)) return
  if (isCheckboxField(component)) {
    updateCheckboxField(component, optionValue, !isOptionChecked(component, optionValue))
    return
  }
  updateField(component, optionValue)
}

function isOptionChecked(component: CardComponentRuntime, optionValue: string): boolean {
  const value = fieldValue(component)
  return Array.isArray(value) ? value.map(String).includes(optionValue) : String(value ?? '') === optionValue
}

function dateRangePart(component: CardComponentRuntime, part: 'start' | 'end'): string {
  const value = fieldValue(component)
  if (Array.isArray(value)) return String(part === 'start' ? value[0] || '' : value[1] || '')
  if (value && typeof value === 'object') return String(value[part] || value[part === 'start' ? 'startDate' : 'endDate'] || '')
  return ''
}

function updateDateRangeField(component: CardComponentRuntime, part: 'start' | 'end', value: string) {
  const current = fieldValue(component)
  const next = current && typeof current === 'object' && !Array.isArray(current)
    ? { ...current, [part]: value }
    : { start: dateRangePart(component, 'start'), end: dateRangePart(component, 'end'), [part]: value }
  updateField(component, next)
}

function updateAttachmentField(component: CardComponentRuntime, event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files || [])
  const maxCount = Number(component.props?.maxCount || files.length || 1)
  updateField(component, files.slice(0, maxCount).map(file => ({
    name: file.name,
    size: file.size,
    type: file.type,
  })))
}

function componentFor(type: string) {
  const map: Record<string, any> = {
    amountSummary: AmountSummaryComponent,
    detailTable: DetailTableComponent,
    relationCards: RelationCardsComponent,
    budgetStatus: BudgetStatusComponent,
    invoiceStatus: InvoiceStatusComponent,
    loanOffset: LoanOffsetComponent,
    paymentInfo: PaymentInfoComponent,
    riskAlert: RiskAlertComponent,
    routeDecision: RouteDecisionComponent,
    dynamicApprover: DynamicApproverComponent,
  }
  return map[type] || null
}

function formatValue(component: CardComponentRuntime): string {
  return formatFieldValue(component)
}

function formatBusinessValue(component: CardComponentRuntime): string {
  const value = fieldValue(component)
  if (value === null || value === undefined || value === '') {
    return component.props?.description || component.props?.controlName || component.title || '-'
  }
  return formatFieldValue(component)
}
</script>

<template>
  <div class="cf-runtime-components" :class="[`cf-runtime-components--${platform}`]">
    <template v-for="component in visibleComponents" :key="component.id">
      <component
        :is="componentFor(component.type)"
        v-if="componentFor(component.type)"
        :component="component"
        :model-value="component.type === 'detailTable' ? detailRows : undefined"
        :mode="mode"
        :platform="platform"
        :account-set-id="accountSetId"
        :org-id="orgId"
        @update:model-value="(value: DetailRow[]) => emit('update:detailRows', value)"
      />

      <section v-else-if="component.type === 'sectionTitle'" class="cf-runtime-section-title">
        <strong>{{ component.title }}</strong>
        <span v-if="component.props?.description">{{ component.props?.description }}</span>
      </section>

      <section v-else-if="component.type === 'textBlock'" class="cf-runtime-text-block">
        <strong v-if="component.title">{{ component.title }}</strong>
        <p>{{ component.props?.body || component.value || '-' }}</p>
      </section>

      <section
        v-else-if="component.binding?.source === 'cardField'"
        class="cf-runtime-field"
        :class="[
          isTextareaField(component) ? 'cf-runtime-field--textarea' : '',
          isAttachmentField(component) ? 'cf-runtime-field--attachment' : '',
          isChoiceField(component) ? 'cf-runtime-field--choice' : '',
          isDateRangeField(component) ? 'cf-runtime-field--date-range' : '',
          isDesignerFieldControl(component) ? 'cf-runtime-field--designer-control' : '',
        ]"
      >
        <label>
          <span v-if="component.required" class="cf-runtime-field__required">*</span>
          {{ component.title }}
        </label>

        <template v-if="renderFieldControl(component)">
          <textarea
            v-if="isTextareaField(component)"
            :value="fieldValue(component)"
            :rows="component.props?.rows || 3"
            :maxlength="component.props?.maxLength || undefined"
            :placeholder="component.props?.placeholder || '请输入'"
            :readonly="!isEditable(component)"
            @input="updateField(component, ($event.target as HTMLTextAreaElement).value)"
          />

          <div v-else-if="isDateRangeField(component)" class="cf-runtime-date-range">
            <input
              type="date"
              :value="dateRangePart(component, 'start')"
              :readonly="!isEditable(component)"
              @input="updateDateRangeField(component, 'start', ($event.target as HTMLInputElement).value)"
            />
            <span>至</span>
            <input
              type="date"
              :value="dateRangePart(component, 'end')"
              :readonly="!isEditable(component)"
              @input="updateDateRangeField(component, 'end', ($event.target as HTMLInputElement).value)"
            />
          </div>

          <div v-else-if="isChoiceField(component)" class="cf-runtime-choice-list">
            <button
              v-for="option in normalizedOptions(component)"
              :key="option.value"
              type="button"
              class="cf-runtime-choice-control"
              :class="[
                isCheckboxField(component) ? 'cf-runtime-choice-control--checkbox' : 'cf-runtime-choice-control--radio',
                isOptionChecked(component, option.value) ? 'is-selected' : '',
              ]"
              @click.stop="updateChoiceField(component, option.value)"
            >
              <i class="cf-runtime-choice-dot" aria-hidden="true" />
              {{ option.label }}
            </button>
          </div>

          <div v-else-if="isAttachmentField(component)" class="cf-runtime-attachment-editor">
            <input
              type="file"
              :accept="component.props?.accept || undefined"
              :multiple="(component.props?.maxCount || 1) > 1"
              @change="updateAttachmentField(component, $event)"
            />
            <ul v-if="attachmentList(component).length" class="cf-runtime-attachment-list">
              <li v-for="file in attachmentList(component)" :key="file.name">{{ file.name }}</li>
            </ul>
          </div>

          <input
            v-else
            :value="fieldValue(component)"
            :type="isDateField(component) ? 'date' : isNumberField(component) ? 'number' : 'text'"
            :maxlength="component.props?.maxLength || undefined"
            :placeholder="component.props?.placeholder || '请输入'"
            :readonly="!isEditable(component)"
            @input="updateField(component, ($event.target as HTMLInputElement).value)"
          />
        </template>

        <template v-else>
          <ul v-if="isAttachmentField(component) && attachmentList(component).length" class="cf-runtime-attachment-list">
            <li v-for="file in attachmentList(component)" :key="file.name">{{ file.name }}</li>
          </ul>
          <div v-else-if="isChoiceField(component)" class="cf-runtime-choice-list cf-runtime-choice-list--readonly">
            <span
              v-for="option in normalizedOptions(component).filter(option => isOptionChecked(component, option.value))"
              :key="option.value"
            >
              {{ option.label }}
            </span>
            <strong v-if="!normalizedOptions(component).some(option => isOptionChecked(component, option.value))">{{ formatFieldValue(component) }}</strong>
          </div>
          <strong v-else>{{ formatFieldValue(component) }}</strong>
        </template>
      </section>

      <section v-else-if="component.type === 'placeholderControl'" class="cf-runtime-placeholder">
        <label>{{ component.title }}</label>
        <strong>{{ formatBusinessValue(component) }}</strong>
      </section>

      <section v-else-if="component.type === 'imageList'" class="cf-runtime-media-placeholder">
        <strong>{{ component.title }}</strong>
        <span>{{ component.props?.emptyText || '暂无图片' }}</span>
      </section>

      <section v-else-if="component.type === 'signature'" class="cf-runtime-media-placeholder cf-runtime-media-placeholder--signature">
        <strong>{{ component.title }}</strong>
        <span>{{ component.props?.emptyText || '待签名' }}</span>
      </section>

      <section v-else-if="component.type === 'rating'" class="cf-runtime-rating">
        <label>{{ component.title }}</label>
        <span aria-label="评分">
          <i
            v-for="index in Number(component.props?.max || 5)"
            :key="index"
            class="cf-runtime-rating__star"
            :class="{ 'is-active': index <= Number(fieldValue(component) || 0) }"
          >★</i>
        </span>
      </section>

      <section v-else-if="component.type === 'ocrText'" class="cf-runtime-placeholder">
        <label>{{ component.title }}</label>
        <strong>{{ component.props?.ocrType === 'idCard' ? '身份证识别结果' : '文字识别结果' }}</strong>
      </section>

      <section v-else-if="component.type === 'componentSuite'" class="cf-runtime-suite">
        <header>
          <strong>{{ component.title }}</strong>
          <span>{{ component.props?.suiteSource || component.props?.suiteDomain || '组件套件' }}</span>
        </header>
        <p>{{ component.props?.description || '套件会在运行态展示该业务场景的关键字段、状态和处理结果。' }}</p>
      </section>

      <section v-else-if="component.type === 'relationLookup'" class="cf-runtime-relation-lookup">
        <label>{{ component.title }}</label>
        <div>
          <strong>{{ formatBusinessValue(component) }}</strong>
          <span>{{ component.props?.relationKind === 'data' ? '数据表单' : '流程表单' }}</span>
        </div>
      </section>

      <section v-else-if="isAdmin" class="cf-runtime-unknown">
        未支持的卡片组件：{{ component.type }} / {{ component.id }}
      </section>
    </template>
  </div>
</template>

<style scoped lang="scss">
.cf-runtime-components {
  display: grid;
  gap: 12px;

  &--mobile {
    gap: 10px;
  }
}

.cf-runtime-field {
  display: grid;
  grid-template-columns: minmax(92px, 140px) minmax(0, 1fr);
  gap: 10px;
  align-items: center;
  border-bottom: 1px solid var(--border);
  padding: 7px 0;

  label {
    color: var(--text-2);
    font-size: 13px;
    line-height: 20px;
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
    font-weight: 500;
    min-width: 0;
    word-break: break-word;
  }

  input,
  textarea {
    width: 100%;
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 6px 8px;
    color: var(--text-1);
    font-size: 14px;
    outline: none;

    &:focus {
      border-color: var(--color-primary);
      box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 12%, transparent);
    }
  }

  textarea {
    resize: vertical;
    min-height: 68px;
    line-height: 20px;
  }

  &__required {
    color: var(--color-danger-text);
    margin-right: 2px;
  }

  &--textarea,
  &--attachment,
  &--date-range {
    align-items: flex-start;
  }

  &--designer-control {
    input,
    textarea,
    button {
      pointer-events: none;
    }
  }
}

.cf-runtime-components--mobile .cf-runtime-field {
  grid-template-columns: 92px minmax(0, 1fr);
}

.cf-runtime-section-title {
  display: grid;
  gap: 3px;
  padding: 10px 0 6px;
  border-bottom: 1px solid var(--border);

  strong {
    color: var(--text-1);
    font-size: 15px;
    line-height: 22px;
    font-weight: 700;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}

.cf-runtime-text-block {
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);

  strong {
    display: block;
    margin-bottom: 4px;
    color: var(--text-1);
    font-size: 13px;
    line-height: 19px;
  }

  p {
    margin: 0;
    color: var(--text-3);
    font-size: 13px;
    line-height: 20px;
    white-space: pre-wrap;
  }
}

.cf-runtime-placeholder,
.cf-runtime-rating {
  display: grid;
  grid-template-columns: minmax(92px, 140px) minmax(0, 1fr);
  gap: 10px;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--border);

  label {
    color: var(--text-2);
    font-size: 13px;
    line-height: 20px;
  }

  strong,
  span {
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
    font-weight: 500;
    min-width: 0;
    word-break: break-word;
  }
}

.cf-runtime-rating span {
  color: var(--color-warning);
  letter-spacing: 0;
}

.cf-runtime-rating__star {
  color: var(--text-3);
  font-style: normal;
  margin-right: 3px;

  &.is-active {
    color: var(--color-warning);
  }
}

.cf-runtime-date-range {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
  gap: 6px;
  align-items: center;

  span {
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}

.cf-runtime-choice-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  min-width: 0;

  button,
  span {
    min-height: 28px;
    border: 1px solid var(--border);
    border-radius: 5px;
    padding: 4px 9px;
    background: var(--bg-card);
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }

  button {
    cursor: pointer;

    &:hover,
    &.is-selected {
      border-color: var(--color-primary);
      background: color-mix(in srgb, var(--color-primary) 8%, transparent);
      color: var(--color-primary);
    }
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
    font-weight: 500;
  }
}

.cf-runtime-choice-control {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
  text-align: left;

  &:disabled {
    cursor: default;
  }
}

.cf-runtime-choice-dot {
  position: relative;
  display: inline-block;
  flex: 0 0 auto;
  width: 14px;
  height: 14px;
  border: 1.5px solid var(--border-strong);
  background: var(--bg-card);
}

.cf-runtime-choice-control--radio .cf-runtime-choice-dot {
  border-radius: 50%;
}

.cf-runtime-choice-control--checkbox .cf-runtime-choice-dot {
  border-radius: 3px;
}

.cf-runtime-choice-control.is-selected .cf-runtime-choice-dot {
  border-color: var(--color-primary);
  background: var(--color-primary);
}

.cf-runtime-choice-control--radio.is-selected .cf-runtime-choice-dot::after {
  content: '';
  position: absolute;
  inset: 3px;
  border-radius: 50%;
  background: var(--text-on-accent);
}

.cf-runtime-choice-control--checkbox.is-selected .cf-runtime-choice-dot::after {
  content: '';
  position: absolute;
  left: 3px;
  top: 1px;
  width: 5px;
  height: 8px;
  border: solid var(--text-on-accent);
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.cf-runtime-attachment-editor {
  display: grid;
  gap: 8px;
  min-width: 0;
}

.cf-runtime-attachment-list {
  display: grid;
  gap: 5px;
  margin: 0;
  padding: 0;
  list-style: none;

  li {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: 5px;
    padding: 5px 8px;
    background: var(--bg-muted);
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
    word-break: break-word;
  }
}

.cf-runtime-media-placeholder {
  display: grid;
  gap: 6px;
  padding: 12px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  background: var(--bg-muted);

  strong {
    color: var(--text-1);
    font-size: 13px;
    line-height: 18px;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}

.cf-runtime-media-placeholder--signature {
  min-height: 72px;
  background:
    linear-gradient(135deg, transparent 47%, color-mix(in srgb, var(--color-primary) 18%, transparent) 48%, color-mix(in srgb, var(--color-primary) 18%, transparent) 52%, transparent 53%),
    var(--bg-muted);
}

.cf-runtime-suite {
  display: grid;
  gap: 8px;
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-muted);

  header {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 10px;
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
    font-weight: 700;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
    white-space: nowrap;
  }

  p {
    margin: 0;
    color: var(--text-2);
    font-size: 12px;
    line-height: 19px;
  }
}

.cf-runtime-relation-lookup {
  display: grid;
  grid-template-columns: minmax(92px, 140px) minmax(0, 1fr);
  gap: 10px;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--border);

  label {
    color: var(--text-2);
    font-size: 13px;
    line-height: 20px;
  }

  div {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
  }

  strong {
    min-width: 0;
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
    font-weight: 500;
    word-break: break-word;
  }

  span {
    flex: 0 0 auto;
    border-radius: 999px;
    padding: 1px 6px;
    background: var(--color-info-light);
    color: var(--color-info);
    font-size: 11px;
    line-height: 17px;
  }
}

.cf-runtime-unknown {
  border: 1px dashed var(--border);
  border-radius: 6px;
  padding: 10px 12px;
  color: var(--text-3);
  font-size: 12px;
}
</style>
