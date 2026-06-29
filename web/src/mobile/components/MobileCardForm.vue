<template>
  <van-form ref="formRef" class="mobile-card-form">
    <template v-for="field in schema" :key="field.name">
      <!-- text / input -->
      <van-field
        v-if="field.type === 'text'"
        v-model="formData[field.name]"
        :label="field.label"
        :placeholder="field.placeholder || `请输入${field.label}`"
        :required="field.required"
        :readonly="readonly"
        :rules="getFieldRules(field)"
      />

      <!-- textarea -->
      <van-field
        v-else-if="field.type === 'textarea' || field.type === 'richtext'"
        v-model="formData[field.name]"
        type="textarea"
        :label="field.label"
        :placeholder="field.placeholder || `请输入${field.label}`"
        :required="field.required"
        :readonly="readonly"
        :rules="getFieldRules(field)"
        autosize
        rows="2"
      />

      <!-- number -->
      <van-field
        v-else-if="field.type === 'number'"
        v-model="formData[field.name]"
        type="digit"
        :label="field.label"
        :placeholder="field.placeholder || `请输入${field.label}`"
        :required="field.required"
        :readonly="readonly"
        :rules="getFieldRules(field)"
      />

      <!-- select / dropdown -->
      <van-field
        v-else-if="field.type === 'select'"
        :label="field.label"
        :placeholder="field.placeholder || `请选择${field.label}`"
        :required="field.required"
        :model-value="getSelectDisplayText(field)"
        is-link
        readonly
        :rules="getFieldRules(field)"
        @click="openPicker(field)"
      />

      <!-- date -->
      <van-field
        v-else-if="field.type === 'date'"
        :label="field.label"
        :placeholder="field.placeholder || `请选择${field.label}`"
        :required="field.required"
        :model-value="formData[field.name] || ''"
        is-link
        readonly
        :rules="getFieldRules(field)"
        @click="openDatePicker(field, 'date')"
      />

      <!-- datetime -->
      <van-field
        v-else-if="field.type === 'datetime'"
        :label="field.label"
        :placeholder="field.placeholder || `请选择${field.label}`"
        :required="field.required"
        :model-value="formData[field.name] || ''"
        is-link
        readonly
        :rules="getFieldRules(field)"
        @click="openDatePicker(field, 'datetime')"
      />

      <!-- radio -->
      <van-field
        v-else-if="field.type === 'radio'"
        :label="field.label"
        :required="field.required"
        :rules="getFieldRules(field)"
      >
        <template #input>
          <van-radio-group
            v-model="formData[field.name]"
            direction="horizontal"
            :disabled="readonly"
          >
            <van-radio
              v-for="opt in field.options"
              :key="opt.value"
              :name="opt.value"
            >
              {{ opt.label }}
            </van-radio>
          </van-radio-group>
        </template>
      </van-field>

      <!-- checkbox -->
      <van-field
        v-else-if="field.type === 'checkbox'"
        :label="field.label"
        :required="field.required"
        :rules="getFieldRules(field)"
      >
        <template #input>
          <van-checkbox-group
            v-model="formData[field.name]"
            direction="horizontal"
            :disabled="readonly"
          >
            <van-checkbox
              v-for="opt in field.options"
              :key="opt.value"
              :name="opt.value"
              shape="square"
            >
              {{ opt.label }}
            </van-checkbox>
          </van-checkbox-group>
        </template>
      </van-field>

      <!-- image / attachment -->
      <van-field
        v-else-if="field.type === 'image'"
        :label="field.label"
        :required="field.required"
        :rules="getFieldRules(field)"
      >
        <template #input>
          <AttachmentPicker
            :model-value="formData[field.name] || []"
            :max-count="9"
            accept="all"
            @update:model-value="(val: any) => (formData[field.name] = val)"
          />
        </template>
      </van-field>

      <!-- table / grid — 降级为卡片列表 -->
      <van-field
        v-else-if="field.type === 'table'"
        :label="field.label"
      >
        <template #input>
          <div class="table-fallback">
            <div
              v-for="(row, idx) in (formData[field.name] || [])"
              :key="idx"
              class="table-card"
            >
              <div v-for="(val, key) in row" :key="key" class="table-card-row">
                <span class="card-key">{{ key }}:</span>
                <span class="card-val">{{ val }}</span>
              </div>
            </div>
            <van-empty v-if="!(formData[field.name] || []).length" description="暂无数据" />
          </div>
        </template>
      </van-field>

      <!-- signature — 简化实现 -->
      <van-field
        v-else-if="field.type === 'signature'"
        :label="field.label"
        :required="field.required"
      >
        <template #input>
          <div class="signature-area">
            <canvas
              :ref="(el) => setSignatureRef(field.name, el as HTMLCanvasElement)"
              class="signature-canvas"
              @touchstart="onSignStart(field.name, $event)"
              @touchmove="onSignMove(field.name, $event)"
              @touchend="onSignEnd(field.name)"
            />
            <div class="signature-actions">
              <van-button size="mini" @click="clearSignature(field.name)">清除</van-button>
            </div>
          </div>
        </template>
      </van-field>

      <!-- 不支持的类型 -->
      <van-field v-else :label="field.label">
        <template #input>
          <div class="unsupported-field">请在PC端操作此字段</div>
        </template>
      </van-field>
    </template>

    <!-- Picker 弹出层 -->
    <van-popup v-model:show="pickerVisible" position="bottom" round>
      <van-picker
        :columns="pickerColumns"
        @confirm="onPickerConfirm"
        @cancel="pickerVisible = false"
      />
    </van-popup>

    <!-- 日期选择弹出层 -->
    <van-popup v-model:show="datePickerVisible" position="bottom" round>
      <van-date-picker
        v-model="datePickerValue"
        :title="datePickerTitle"
        :columns-type="dateColumnsType"
        @confirm="onDateConfirm"
        @cancel="datePickerVisible = false"
      />
    </van-popup>
    <!-- 时间选择弹出层 -->
    <van-popup v-model:show="timePickerVisible" position="bottom" round>
      <van-time-picker
        v-model="timePickerValue"
        title="选择时间"
        :columns-type="['hour', 'minute']"
        @confirm="onTimeConfirm"
        @cancel="timePickerVisible = false"
      />
    </van-popup>
  </van-form>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Form as VanForm,
  Field as VanField,
  Popup as VanPopup,
  Picker as VanPicker,
  DatePicker as VanDatePicker,
  TimePicker as VanTimePicker,
  RadioGroup as VanRadioGroup,
  Radio as VanRadio,
  CheckboxGroup as VanCheckboxGroup,
  Checkbox as VanCheckbox,
  Button as VanButton,
  Empty as VanEmpty,
} from 'vant'
import AttachmentPicker from './AttachmentPicker.vue'

export interface FieldSchema {
  name: string
  label: string
  type: 'text' | 'textarea' | 'number' | 'select' | 'date' | 'datetime' | 'radio' | 'checkbox' | 'image' | 'richtext' | 'table' | 'signature'
  required?: boolean
  placeholder?: string
  options?: Array<{ label: string; value: string | number }>
  rules?: Array<{ required?: boolean; message?: string }>
}

const props = withDefaults(defineProps<{
  schema: FieldSchema[]
  modelValue: Record<string, any>
  readonly?: boolean
}>(), {
  readonly: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: Record<string, any>]
}>()

const formRef = ref()

// 内部表单数据 (双向绑定代理)
const formData = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

// 监听深层变化，通知父组件
watch(
  () => props.modelValue,
  (val) => {
    // 已通过 computed setter 同步
  },
  { deep: true }
)

// --- Picker (select) ---
const pickerVisible = ref(false)
const pickerColumns = ref<Array<{ text: string; value: string | number }>>([])
const currentPickerField = ref<string>('')

function openPicker(field: FieldSchema) {
  if (props.readonly) return
  currentPickerField.value = field.name
  pickerColumns.value = (field.options || []).map(o => ({ text: o.label, value: o.value }))
  pickerVisible.value = true
}

function onPickerConfirm({ selectedOptions }: any) {
  const opt = selectedOptions?.[0]
  if (opt && currentPickerField.value) {
    const newData = { ...props.modelValue, [currentPickerField.value]: opt.value }
    emit('update:modelValue', newData)
  }
  pickerVisible.value = false
}

function getSelectDisplayText(field: FieldSchema): string {
  const val = props.modelValue[field.name]
  if (val == null) return ''
  const opt = field.options?.find(o => o.value === val)
  return opt?.label || String(val)
}

// --- DatePicker ---
const datePickerVisible = ref(false)
const datePickerValue = ref<string[]>([])
const datePickerTitle = ref('')
const dateColumnsType = ref<Array<'year' | 'month' | 'day'>>(['year', 'month', 'day'])
const isDateTimeMode = ref(false)
const timePickerVisible = ref(false)
const timePickerValue = ref<string[]>(['00', '00'])
const currentDateField = ref('')

function openDatePicker(field: FieldSchema, mode: 'date' | 'datetime') {
  if (props.readonly) return
  currentDateField.value = field.name
  datePickerTitle.value = `选择${field.label}`
  isDateTimeMode.value = mode === 'datetime'
  dateColumnsType.value = ['year', 'month', 'day']

  // 初始化当前值
  const current = props.modelValue[field.name]
  if (current) {
    const d = new Date(current)
    datePickerValue.value = [String(d.getFullYear()), String(d.getMonth() + 1).padStart(2, '0'), String(d.getDate()).padStart(2, '0')]
    if (mode === 'datetime') {
      timePickerValue.value = [String(d.getHours()).padStart(2, '0'), String(d.getMinutes()).padStart(2, '0')]
    }
  } else {
    const now = new Date()
    datePickerValue.value = [String(now.getFullYear()), String(now.getMonth() + 1).padStart(2, '0'), String(now.getDate()).padStart(2, '0')]
    timePickerValue.value = [String(now.getHours()).padStart(2, '0'), String(now.getMinutes()).padStart(2, '0')]
  }
  datePickerVisible.value = true
}

function onDateConfirm({ selectedValues }: any) {
  if (!currentDateField.value || !selectedValues) return
  const parts = selectedValues as string[]
  let formatted = `${parts[0]}-${parts[1]}-${parts[2]}`
  if (isDateTimeMode.value) {
    // 日期确认后打开时间选择器
    datePickerVisible.value = false
    timePickerVisible.value = true
    return
  }
  const newData = { ...props.modelValue, [currentDateField.value]: formatted }
  emit('update:modelValue', newData)
  datePickerVisible.value = false
}

function onTimeConfirm({ selectedValues }: any) {
  if (!currentDateField.value || !selectedValues) return
  const timeParts = selectedValues as string[]
  const dateParts = datePickerValue.value
  const formatted = `${dateParts[0]}-${dateParts[1]}-${dateParts[2]} ${timeParts[0]}:${timeParts[1]}`
  const newData = { ...props.modelValue, [currentDateField.value]: formatted }
  emit('update:modelValue', newData)
  timePickerVisible.value = false
}

// --- 字段校验规则 ---
function getFieldRules(field: FieldSchema) {
  if (field.rules?.length) return field.rules
  if (field.required) {
    return [{ required: true, message: `请填写${field.label}` }]
  }
  return []
}

// --- Signature Canvas ---
const signatureRefs = ref<Record<string, HTMLCanvasElement | null>>({})
const isDrawing = ref(false)

function setSignatureRef(name: string, el: HTMLCanvasElement | null) {
  signatureRefs.value[name] = el
}

function getCtx(name: string) {
  const canvas = signatureRefs.value[name]
  if (!canvas) return null
  // 初始化 canvas 尺寸
  if (canvas.width === 0 || canvas.width === 300) {
    canvas.width = canvas.offsetWidth * 2
    canvas.height = canvas.offsetHeight * 2
    const ctx = canvas.getContext('2d')
    if (ctx) {
      ctx.scale(2, 2)
      ctx.lineWidth = 2
      ctx.lineCap = 'round'
      ctx.strokeStyle = '#333'
    }
  }
  return canvas.getContext('2d')
}

function onSignStart(name: string, e: TouchEvent) {
  if (props.readonly) return
  isDrawing.value = true
  const ctx = getCtx(name)
  if (!ctx) return
  const touch = e.touches[0]
  const canvas = signatureRefs.value[name]!
  const rect = canvas.getBoundingClientRect()
  ctx.beginPath()
  ctx.moveTo(touch.clientX - rect.left, touch.clientY - rect.top)
  e.preventDefault()
}

function onSignMove(name: string, e: TouchEvent) {
  if (!isDrawing.value || props.readonly) return
  const ctx = getCtx(name)
  if (!ctx) return
  const touch = e.touches[0]
  const canvas = signatureRefs.value[name]!
  const rect = canvas.getBoundingClientRect()
  ctx.lineTo(touch.clientX - rect.left, touch.clientY - rect.top)
  ctx.stroke()
  e.preventDefault()
}

function onSignEnd(name: string) {
  isDrawing.value = false
  // 保存签名 dataURL 到 formData
  const canvas = signatureRefs.value[name]
  if (canvas) {
    const dataUrl = canvas.toDataURL('image/png')
    const newData = { ...props.modelValue, [name]: dataUrl }
    emit('update:modelValue', newData)
  }
}

function clearSignature(name: string) {
  const canvas = signatureRefs.value[name]
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (ctx) {
    ctx.clearRect(0, 0, canvas.width, canvas.height)
  }
  const newData = { ...props.modelValue, [name]: '' }
  emit('update:modelValue', newData)
}

/** 暴露 validate 方法给父组件 */
async function validate() {
  return formRef.value?.validate()
}

defineExpose({ validate })
</script>

<style scoped>
.mobile-card-form {
  padding-bottom: 16px;
}

.unsupported-field {
  color: #999;
  font-size: 13px;
  padding: 8px 0;
}

.table-fallback {
  width: 100%;
}

.table-card {
  background: #f7f8fa;
  border-radius: 6px;
  padding: 10px 12px;
  margin-bottom: 8px;
}

.table-card-row {
  display: flex;
  font-size: 13px;
  line-height: 1.8;
}

.card-key {
  color: #666;
  margin-right: 8px;
  white-space: nowrap;
}

.card-val {
  color: #333;
  flex: 1;
}

.signature-area {
  width: 100%;
}

.signature-canvas {
  width: 100%;
  height: 120px;
  border: 1px solid #eee;
  border-radius: 4px;
  touch-action: none;
}

.signature-actions {
  margin-top: 6px;
  text-align: right;
}
</style>
