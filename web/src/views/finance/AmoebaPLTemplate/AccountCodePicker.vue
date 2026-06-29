<template>
  <!-- 通配模式的 Enter 必须在捕获阶段拦截：a-select 会在 keydown 默认选中高亮项并清空搜索词，
       keyup 阶段再处理已经晚了（误选一个科目 + 关键词丢失，批量选择无法触发） -->
  <div class="account-code-picker" @keydown.capture="onKeydownCapture">
    <a-select
      v-model:value="innerValue"
      mode="multiple"
      :options="processedOptions"
      :placeholder="placeholder"
      show-search
      :filter-option="filterOption"
      :max-tag-count="maxTagCount"
      :max-tag-placeholder="(omitted: any[]) => `+${omitted.length} 个科目`"
      style="width: 100%"
      @search="onSearch"
    >
      <template #suffixIcon>
        <SearchOutlined />
      </template>
      <template #notFoundContent>
        <div style="padding: 8px 12px; color: var(--text-3); font-size: 12px;">
          <div>未找到匹配科目</div>
          <div style="margin-top: 4px;">
            提示：输入 <code style="background:var(--border);padding:1px 4px;">5001*</code> 一键选中所有 5001 开头的科目
          </div>
        </div>
      </template>
    </a-select>
    <div v-if="hint" class="account-code-picker__hint">
      <InfoCircleOutlined style="color: var(--text-3);" />
      <span>{{ hint }}</span>
      <span v-if="groupedSummary" class="account-code-picker__summary">{{ groupedSummary }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Modal, message } from 'ant-design-vue'
import { SearchOutlined, InfoCircleOutlined } from '@ant-design/icons-vue'

interface AccountOption {
  value: string
  label: string
}

const props = defineProps<{
  value: string[]
  options: AccountOption[]
  placeholder?: string
  hint?: string
  maxTagCount?: number
}>()

const emit = defineEmits<{
  (e: 'update:value', val: string[]): void
}>()

const innerValue = computed({
  get: () => props.value,
  set: (val: string[]) => emit('update:value', val),
})

const searchKeyword = ref('')

function onSearch(val: string) {
  searchKeyword.value = val
}

function filterOption(input: string, option: any) {
  const label = (option.label || '').toLowerCase()
  const value = (option.value || '').toLowerCase()
  const q = input.toLowerCase()
  // 末尾 * 通配模式：仅按 value 前缀匹配，便于用户预览要选的范围
  if (q.endsWith('*')) {
    const prefix = q.slice(0, -1)
    return value.startsWith(prefix)
  }
  return label.includes(q) || value.includes(q)
}

/** 捕获阶段拦截通配模式的 Enter，阻止 a-select 默认选中高亮项/清空搜索词 */
function onKeydownCapture(e: KeyboardEvent) {
  if (e.key !== 'Enter') return
  const raw = searchKeyword.value.trim()
  if (!raw.endsWith('*')) return // 非通配模式保持 a-select 默认 Enter 行为
  e.preventDefault()
  e.stopPropagation()
  triggerWildcardSelect(raw)
}

/** 末尾 * 通配触发：一键选中所有匹配前缀的科目 */
function triggerWildcardSelect(raw: string) {
  const prefix = raw.slice(0, -1)
  if (!prefix) return

  const matches = props.options.filter(o => o.value.startsWith(prefix))
  if (matches.length === 0) {
    message.warning(`未找到 ${prefix} 开头的科目`)
    return
  }

  Modal.confirm({
    title: '通配批量选择',
    content: `将选中所有 ${prefix} 开头的科目，共 ${matches.length} 个，是否继续？`,
    okText: '全部选中',
    onOk: () => {
      const merged = new Set([...innerValue.value, ...matches.map(m => m.value)])
      innerValue.value = [...merged]
      message.success(`已选中 ${matches.length} 个科目`)
      searchKeyword.value = ''
    },
  })
}

/** 分组摘要：当选中数量 > 阈值时按一级科目分组 */
const groupedSummary = computed(() => {
  if (!innerValue.value || innerValue.value.length <= 8) return ''
  const groups = new Map<string, number>()
  for (const code of innerValue.value) {
    const root = code.slice(0, 4)
    groups.set(root, (groups.get(root) || 0) + 1)
  }
  const parts: string[] = []
  for (const [root, count] of groups.entries()) {
    parts.push(`${root}（${count}）`)
  }
  return `已选 ${innerValue.value.length} 个：${parts.slice(0, 4).join('、')}${parts.length > 4 ? ' ...' : ''}`
})

const processedOptions = computed(() => props.options)

watch(() => props.options, () => {
  // options 变化时无需特殊处理，computed 自动响应
})
</script>

<style scoped lang="scss">
.account-code-picker {
  width: 100%;

  &__hint {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-top: 4px;
    font-size: 12px;
    color: var(--text-3);
    line-height: 1.5;
    flex-wrap: wrap;
  }

  &__summary {
    color: var(--color-info);
    margin-left: 4px;
  }
}

code {
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 12px;
}
</style>
