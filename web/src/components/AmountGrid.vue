<template>
  <div class="amount-grid" :class="{ 'is-debit': isDebit, 'is-credit': !isDebit }" @click="handleClick">
    <div class="amount-cells">
      <div
        v-for="(cell, index) in cells"
        :key="index"
        class="cell"
        :class="{
          'red-line': cell.isRedLine,
          'blue-line': cell.isBlueLine,
          'has-value': cell.value !== ''
        }"
      >
        {{ cell.value }}
      </div>
    </div>
    <input
      v-if="isEditing"
      ref="inputRef"
      v-model="inputValue"
      type="number"
      step="0.01"
      class="amount-input"
      @blur="handleBlur"
      @keydown.enter="handleBlur"
      @keydown.esc="handleCancel"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'

interface Props {
  modelValue: number | null
  isDebit?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  isDebit: true
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: number | null): void
}>()

const isEditing = ref(false)
const inputValue = ref('')
const inputRef = ref<HTMLInputElement>()

// 金额位定义：亿 千 百 十 万 千 百 十 元 角 分
const positions = ['亿', '千', '百', '十', '万', '千', '百', '十', '元', '角', '分']

// 红线位置：万位和千位之间（索引4和5之间，即在索引4后）
// 蓝线位置：元位和角位之间（索引8和9之间，即在索引8后）
const cells = computed(() => {
  const value = props.modelValue
  const cellsData = []
  
  if (value === null || value === undefined || value === 0) {
    // 空值时显示空格子
    for (let i = 0; i < positions.length; i++) {
      cellsData.push({
        value: '',
        isRedLine: i === 4, // 万位后红线
        isBlueLine: i === 8 // 元位后蓝线
      })
    }
  } else {
    // 格式化金额
    const absValue = Math.abs(value)
    const totalFen = Math.round(absValue * 100) // 转为分（整数）
    
    // 初始化11个格子为空字符串
    const result: string[] = new Array(11).fill('')
    
    // 从右到左填入数字（分->亿）
    const digits = totalFen.toString().split('')
    for (let i = 0; i < digits.length && i < 11; i++) {
      result[10 - i] = digits[digits.length - 1 - i]
    }
    
    // 找到第一个非0数字的位置（从左到右，只检查整数部分，索引0-8对应亿到元）
    let firstNonZero = -1
    for (let i = 0; i < 9; i++) {
      if (result[i] !== '' && result[i] !== '0') {
        firstNonZero = i
        break
      }
    }
    
    // 处理前导0：将有效数字之前的位设为空
    if (firstNonZero > 0) {
      // 有非0数字，将第一个非0数字之前的位设为空
      for (let i = 0; i < firstNonZero; i++) {
        result[i] = ''
      }
    } else if (firstNonZero === -1) {
      // 整数部分全是0（金额小于1元），只保留元位的0
      for (let i = 0; i < 8; i++) {
        result[i] = ''
      }
    }
    
    // 构建返回数据
    for (let i = 0; i < positions.length; i++) {
      cellsData.push({
        value: result[i],
        isRedLine: i === 4,
        isBlueLine: i === 8
      })
    }
  }
  
  return cellsData
})

const handleClick = () => {
  isEditing.value = true
  inputValue.value = props.modelValue !== null && props.modelValue !== undefined ? String(props.modelValue) : ''
  nextTick(() => {
    inputRef.value?.focus()
    inputRef.value?.select()
  })
}

const handleBlur = () => {
  const val = parseFloat(inputValue.value)
  if (!isNaN(val) && val > 0) {
    emit('update:modelValue', val)
  } else {
    emit('update:modelValue', null)
  }
  isEditing.value = false
}

const handleCancel = () => {
  isEditing.value = false
}
</script>

<style scoped lang="scss">
.amount-grid {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 40px;
  cursor: pointer;
  background: #fff;
  
  &:hover {
    background: #f5f7fa;
  }
}

.amount-cells {
  display: flex;
  height: 100%;
  width: 100%;
}

.cell {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 500;
  color: #333;
  border-right: 1px solid #e0e0e0;
  min-width: 20px;
  height: 100%;
  
  &:last-child {
    border-right: none;
  }
  
  &.red-line {
    border-right: 2px solid var(--color-danger) !important;
  }
  
  &.blue-line {
    border-right: 2px solid var(--color-info) !important;
  }
  
  &.has-value {
    color: #000;
    font-weight: 600;
  }
}

.amount-input {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  border: 2px solid var(--color-primary);
  outline: none;
  padding: 0 8px;
  font-size: 14px;
  box-sizing: border-box;
  z-index: 10;
}
</style>
