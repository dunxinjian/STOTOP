<template>
  <div
    class="price-cell"
    :class="{ 'is-editing': editing, 'is-disabled': disabled, 'has-override': hasOverride }"
    @click="handleClick"
    @dblclick="handleDblClick"
  >
    <!-- 覆盖值角标 -->
    <span v-if="hasOverride && !editing" class="override-badge" title="已设置覆盖值">
      <SettingOutlined style="font-size: 8px" />
    </span>
    <!-- 显示态 -->
    <span v-if="!editing" class="cell-display">
      {{ displayText }}
    </span>
    <!-- 编辑态 -->
    <input
      v-else
      ref="inputRef"
      class="cell-input"
      :class="{ 'has-error': hasError }"
      :value="inputValue"
      :placeholder="placeholder"
      :disabled="disabled"
      @input="handleInput"
      @blur="handleBlur"
      @keydown="handleKeydown"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { SettingOutlined } from '@ant-design/icons-vue'
import type { PriceCell } from '../composables/usePriceMatrix'
import { formatCellValue, parseCellValue, validateCellInput } from '../composables/usePriceMatrix'

interface Props {
  value: PriceCell
  editing: boolean
  disabled?: boolean
  hasOverride?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  hasOverride: false,
})

const emit = defineEmits<{
  'update:value': [cell: PriceCell]
  startEdit: []
  endEdit: []
  tabNext: []
  openOverride: []
}>()

const inputRef = ref<HTMLInputElement | null>(null)
const inputValue = ref('')
const hasError = ref(false)

// 显示文本
const displayText = computed(() => {
  const text = formatCellValue(props.value)
  return text || '-'
})

// placeholder 统一：单值=固定单价，首重+续重=首续重
const placeholder = '价格 或 首重+续重'

// 进入编辑态时初始化输入值并自动聚焦
watch(
  () => props.editing,
  (val) => {
    if (val) {
      inputValue.value = formatCellValue(props.value)
      hasError.value = false
      nextTick(() => {
        inputRef.value?.focus()
        inputRef.value?.select()
      })
    }
  },
)

function handleClick() {
  if (!props.disabled && !props.editing) {
    emit('startEdit')
  }
}

function handleDblClick(e: MouseEvent) {
  if (e.ctrlKey && !props.disabled) {
    e.preventDefault()
    e.stopPropagation()
    emit('openOverride')
  }
}

function handleInput(e: Event) {
  const val = (e.target as HTMLInputElement).value
  inputValue.value = val
  const result = validateCellInput(val)
  hasError.value = !result.valid
}

function saveAndExit() {
  // 输入清空 = 删除该单元格价格（否则 parseCellValue('') 返回空对象，旧值会回弹、价格删不掉）
  if (!inputValue.value.trim()) {
    emit('update:value', {
      ...props.value,
      basePrice: null,
      continuePrice: null,
      firstWeight: 0,
      continueStep: 1,
    })
    return true
  }
  const result = validateCellInput(inputValue.value)
  if (!result.valid) {
    hasError.value = true
    return false
  }
  // 解析并提交
  const parsed = parseCellValue(inputValue.value)
  emit('update:value', { ...props.value, ...parsed })
  return true
}

function handleBlur() {
  saveAndExit()
  emit('endEdit')
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter') {
    e.preventDefault()
    if (saveAndExit()) {
      emit('endEdit')
      emit('tabNext') // Enter → 下一行
    }
  } else if (e.key === 'Tab') {
    e.preventDefault()
    if (saveAndExit()) {
      emit('endEdit')
      emit('tabNext') // Tab → 右侧单元格
    }
  } else if (e.key === 'Escape') {
    e.preventDefault()
    hasError.value = false
    emit('endEdit')
  }
}
</script>

<style scoped lang="scss">
.price-cell {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  padding: 0 4px;
  border-radius: 2px;
  transition: background-color 0.15s;

  &:hover:not(.is-editing):not(.is-disabled) {
    background-color: var(--bg-muted);
  }

  &.is-disabled {
    cursor: not-allowed;
    opacity: 0.6;
  }

  &.has-override:not(.is-editing) {
    background-color: var(--color-primary-light);
  }
}

.override-badge {
  position: absolute;
  top: 0;
  right: 0;
  width: 14px;
  height: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--color-primary);
  z-index: 1;
}

.cell-display {
  font-size: 13px;
  line-height: 22px;
  color: var(--text-1);
  user-select: none;
  white-space: nowrap;
}

.cell-input {
  width: 100%;
  height: 24px;
  border: 1px solid var(--border-strong);
  border-radius: 2px;
  outline: none;
  text-align: center;
  font-size: 13px;
  padding: 0 4px;
  background: var(--bg-card);
  transition: border-color 0.2s;

  &:focus {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 1px var(--color-primary-border);
  }

  &.has-error {
    border-color: var(--color-danger);
    &:focus {
      box-shadow: 0 0 0 1px var(--color-danger-border);
    }
  }
}
</style>
