<template>
  <div class="formula-editor">
    <!-- 公式编辑区 -->
    <div class="formula-editor__textarea-wrap">
      <textarea
        ref="textareaRef"
        class="formula-editor__textarea"
        :value="modelValue"
        placeholder="拖入损益项或直接输入公式，如 ${出港收入} - ${出港成本}"
        @input="onInput"
        @drop.prevent="onDrop"
        @dragover.prevent
        @click="saveCursor"
        @keyup="saveCursor"
      />
    </div>

    <!-- 下方面板 -->
    <div class="formula-editor__panel">
      <!-- 左侧：可用损益项 -->
      <div class="formula-editor__items">
        <div class="formula-editor__items-title">可用损益项</div>
        <a-input
          v-model:value="searchText"
          placeholder="搜索..."
          allow-clear
          size="small"
          class="formula-editor__search"
        >
          <template #prefix>
            <search-outlined />
          </template>
        </a-input>
        <div class="formula-editor__items-columns">
          <div class="formula-editor__items-col" v-for="dir in ['出港', '进港', '综合']" :key="dir">
            <div class="formula-editor__col-title">{{ dir }}</div>
            <div class="formula-editor__col-list">
              <div
                v-for="item in groupedItems[dir]"
                :key="item.id"
                class="formula-editor__item"
                draggable="true"
                @dragstart="onDragStart($event, item)"
                @dblclick="insertItem(item)"
              >
                <span class="formula-editor__item-name">{{ item.name }}</span>
                <span class="formula-editor__item-drag">⋮⋮</span>
              </div>
              <div v-if="groupedItems[dir].length === 0" class="formula-editor__empty">无</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 右侧：运算符 -->
      <div class="formula-editor__operators">
        <div class="formula-editor__operators-title">运算符</div>
        <div class="formula-editor__operators-grid">
          <a-button
            v-for="op in operators"
            :key="op.label"
            class="formula-editor__op-btn"
            @click="insertOperator(op.value)"
          >
            {{ op.label }}
          </a-button>
        </div>
        <a-button danger class="formula-editor__clear-btn" @click="clearFormula">
          清空
        </a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { SearchOutlined } from '@ant-design/icons-vue'

interface ItemType {
  id: number
  name: string
  direction?: string
}

interface Props {
  modelValue: string
  items: ItemType[]
}

const props = defineProps<Props>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const textareaRef = ref<HTMLTextAreaElement | null>(null)
const searchText = ref('')
const cursorPos = ref(0)

const operators = [
  { label: '+', value: ' + ' },
  { label: '-', value: ' - ' },
  { label: '×', value: ' * ' },
  { label: '÷', value: ' / ' },
  { label: '(', value: '(' },
  { label: ')', value: ')' },
]

const groupedItems = computed(() => {
  const groups: Record<string, ItemType[]> = {
    '出港': [],
    '进港': [],
    '综合': [],
  }
  const keyword = searchText.value.trim().toLowerCase()
  for (const item of props.items) {
    if (keyword && !item.name.toLowerCase().includes(keyword)) continue
    const dir = item.direction || '综合'
    if (groups[dir]) {
      groups[dir].push(item)
    } else {
      groups['综合'].push(item)
    }
  }
  return groups
})

function saveCursor() {
  const el = textareaRef.value
  if (el) {
    cursorPos.value = el.selectionStart ?? 0
  }
}

function onInput(e: Event) {
  const target = e.target as HTMLTextAreaElement
  emit('update:modelValue', target.value)
  cursorPos.value = target.selectionStart ?? 0
}

function insertAtCursor(text: string) {
  const el = textareaRef.value
  const current = props.modelValue || ''
  const pos = el ? (el.selectionStart ?? cursorPos.value) : cursorPos.value
  const before = current.slice(0, pos)
  const after = current.slice(pos)
  const newValue = before + text + after
  emit('update:modelValue', newValue)

  // 恢复光标位置
  const newPos = pos + text.length
  cursorPos.value = newPos
  requestAnimationFrame(() => {
    if (el) {
      el.focus()
      el.setSelectionRange(newPos, newPos)
    }
  })
}

function onDragStart(e: DragEvent, item: ItemType) {
  if (e.dataTransfer) {
    e.dataTransfer.setData('text/plain', `\${${item.name}}`)
    e.dataTransfer.effectAllowed = 'copy'
  }
}

function onDrop(e: DragEvent) {
  const text = e.dataTransfer?.getData('text/plain')
  if (text) {
    // 使用 drop 位置插入
    const el = textareaRef.value
    if (el) {
      // 尝试获取 drop 位置的光标
      const rect = el.getBoundingClientRect()
      // 简化处理：插入到当前光标位置
      insertAtCursor(text)
    }
  }
}

function insertItem(item: ItemType) {
  insertAtCursor(`\${${item.name}}`)
}

function insertOperator(op: string) {
  insertAtCursor(op)
}

function clearFormula() {
  emit('update:modelValue', '')
  cursorPos.value = 0
  requestAnimationFrame(() => {
    textareaRef.value?.focus()
  })
}
</script>

<style scoped>
.formula-editor {
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  overflow: hidden;
}

.formula-editor__textarea-wrap {
  padding: 12px;
  border-bottom: 1px solid #f0f0f0;
}

.formula-editor__textarea {
  width: 100%;
  min-height: 100px;
  padding: 10px 12px;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  font-family: 'JetBrains Mono', 'Fira Code', 'Consolas', monospace;
  font-size: 14px;
  line-height: 1.6;
  resize: vertical;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
}

.formula-editor__textarea:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary-border);
}

.formula-editor__panel {
  display: flex;
  min-height: 320px;
}

.formula-editor__items {
  flex: 1;
  padding: 12px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
}

.formula-editor__items-title,
.formula-editor__operators-title {
  font-size: 13px;
  font-weight: 600;
  color: #333;
  margin-bottom: 8px;
}

.formula-editor__search {
  margin-bottom: 8px;
}

.formula-editor__items-columns {
  display: flex;
  gap: 8px;
  flex: 1;
  overflow: hidden;
}

.formula-editor__items-col {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
  overflow: hidden;
}

.formula-editor__col-title {
  font-size: 12px;
  font-weight: 600;
  color: #666;
  padding: 6px 8px;
  background: #fafafa;
  border-bottom: 1px solid #f0f0f0;
  text-align: center;
}

.formula-editor__col-list {
  flex: 1;
  overflow-y: auto;
  max-height: 240px;
}

.formula-editor__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  border-left: 3px solid var(--border);
  margin: 4px 6px;
  border-radius: 3px;
  background: #fafafa;
  cursor: grab;
  transition: all 0.2s;
  user-select: none;
}

.formula-editor__item:hover {
  background: var(--color-primary-light);
  border-left-color: var(--color-primary-hover);
}

.formula-editor__item:active {
  cursor: grabbing;
}

.formula-editor__item-name {
  font-size: 13px;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
  min-width: 0;
}

.formula-editor__item-drag {
  color: #bbb;
  font-size: 12px;
  letter-spacing: 1px;
  flex-shrink: 0;
}

.formula-editor__empty {
  padding: 16px;
  text-align: center;
  color: #999;
  font-size: 13px;
}

.formula-editor__operators {
  width: 150px;
  padding: 12px;
  display: flex;
  flex-direction: column;
}

.formula-editor__operators-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
  margin-bottom: 12px;
}

.formula-editor__op-btn {
  width: 36px;
  height: 36px;
  padding: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 500;
}

.formula-editor__clear-btn {
  margin-top: auto;
}
</style>
