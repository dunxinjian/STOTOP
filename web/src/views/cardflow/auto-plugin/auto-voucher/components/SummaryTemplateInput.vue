<!-- 摘要模板编辑器：支持 {字段名} 变量自动补全和实时预览 -->
<template>
  <div class="summary-template-input">
    <a-auto-complete
      :value="modelValue"
      :options="autocompleteOptions"
      placeholder="如：{F客户名} {F日期} 运费"
      @change="onInput"
      @search="onSearch"
      @select="onSelect"
      allow-clear
    >
      <template #dataSource>
        <a-select-option v-for="opt in autocompleteOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </a-select-option>
      </template>
    </a-auto-complete>
    <!-- 可用变量提示 -->
    <div class="field-hint">
      可用变量：
      <a-tag
        v-for="f in fields.slice(0, 8)"
        :key="f"
        size="small"
        class="field-tag"
        @click="insertVariable(f)"
      >{{ '{' + f + '}' }}</a-tag>
      <span v-if="fields.length > 8" style="color: var(--text-3);">...</span>
    </div>
    <!-- 实时预览 -->
    <div v-if="previewText !== null" class="preview-box">
      <span class="preview-label">预览：</span>
      <span class="preview-text">{{ previewText || '(空)' }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'

const props = defineProps<{
  /** v-model 绑定值 */
  modelValue: string
  /** 可用字段列表（暂存表列名） */
  fields: string[]
  /** 预览用的第一行数据 */
  sampleRow?: Record<string, any> | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

// ==================== 自动补全 ====================
const searchText = ref('')
const autocompleteOptions = computed(() => {
  if (!searchText.value) return []
  return props.fields
    .filter(f => f.toLowerCase().includes(searchText.value.toLowerCase()))
    .slice(0, 10)
    .map(f => ({ value: `{${f}}`, label: f }))
})

function onInput(value: SelectValue) {
  emit('update:modelValue', value == null ? '' : String(value))
}

function onSearch(val: string) {
  searchText.value = val
}

function onSelect(value: SelectValue) {
  const val = value == null ? '' : String(value)
  // 将选中的变量替换掉当前输入的 { 部分
  const current = props.modelValue || ''
  // 找到最后一个 { 的位置
  const lastBrace = current.lastIndexOf('{')
  if (lastBrace >= 0) {
    const newValue = current.substring(0, lastBrace) + val
    emit('update:modelValue', newValue)
  } else {
    emit('update:modelValue', current + val)
  }
  searchText.value = ''
}

function insertVariable(fieldName: string) {
  const current = props.modelValue || ''
  emit('update:modelValue', current + `{${fieldName}}`)
}

// ==================== 实时预览 ====================
const previewText = computed(() => {
  if (!props.modelValue) return null
  if (!props.sampleRow) return null

  // 渲染模板：替换 {字段名} 为实际值，变量不存在时输出空串
  return props.modelValue.replace(/\{([^}]+)\}/g, (_match, varName) => {
    if (props.sampleRow && varName in props.sampleRow) {
      const val = props.sampleRow[varName]
      return val != null ? String(val) : ''
    }
    return '' // 变量不存在时输出空串
  })
})
</script>

<style lang="scss" scoped>
.summary-template-input {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-hint {
  font-size: 11px;
  color: var(--text-3);
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}

.field-tag {
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    border-color: var(--color-primary);
  }
}

.preview-box {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 10px;
  background: var(--color-success-light);
  border: 1px solid var(--color-success);
  border-radius: 4px;
  font-size: 12px;
}

.preview-label {
  color: var(--color-success-text);
  font-weight: 500;
  white-space: nowrap;
}

.preview-text {
  color: var(--text-1);
  word-break: break-all;
}
</style>
