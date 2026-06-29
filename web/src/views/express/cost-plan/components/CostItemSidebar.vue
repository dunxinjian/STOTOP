<template>
  <div
    ref="sidebarRef"
    class="cost-item-sidebar"
    tabindex="0"
    @keydown="handleKeydown"
  >
    <div class="sidebar-title">成本项目</div>
    <div class="sidebar-list">
      <div
        v-for="item in items"
        :key="item.id"
        class="sidebar-item"
        :class="{
          'is-active': item.id === activeId,
          'is-empty': (detailCounts[item.id] ?? 0) === 0,
          'is-editing': editingId === item.id,
        }"
        @click="handleSelect(item.id)"
      >
        <!-- 内联编辑输入框 -->
        <input
          v-if="editingId === item.id"
          ref="editInputRef"
          v-model="editingName"
          class="item-name-input"
          @blur="commitEdit(item)"
          @keydown.enter.prevent="commitEdit(item)"
          @keydown.esc.prevent="cancelEdit"
          @click.stop
        />
        <!-- 普通显示 -->
        <template v-else>
          <span
            class="item-name"
            :title="!readonly ? '双击重命名' : undefined"
            @dblclick.stop="!readonly && startEdit(item)"
          >{{ item.name }}</span>
        </template>
        <a-tag v-if="item.isRebate" color="green" class="rebate-tag">返利</a-tag>
        <a-badge
          v-if="(detailCounts[item.id] ?? 0) > 0"
          :count="detailCounts[item.id]"
          :number-style="{ backgroundColor: item.id === activeId ? 'var(--color-primary)' : '#bfbfbf' }"
          class="item-badge"
        />
      </div>
      <div v-if="items.length === 0" class="empty-tip">
        暂无成本项目
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import { updateCostItem, type CostItemDto } from '@/api/express'

interface Props {
  items: CostItemDto[]
  activeId: number | null
  detailCounts: Record<number, number>
  readonly?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  readonly: false,
})

const emit = defineEmits<{
  (e: 'select', costItemId: number): void
  (e: 'renamed', costItemId: number, newName: string): void
}>()

const sidebarRef = ref<HTMLElement | null>(null)
const editInputRef = ref<HTMLInputElement | null>(null)
const editingId = ref<number | null>(null)
const editingName = ref('')

function handleSelect(id: number) {
  if (editingId.value !== null) return  // 编辑中不切换
  if (id === props.activeId) return
  emit('select', id)
}

function startEdit(item: CostItemDto) {
  editingId.value = item.id
  editingName.value = item.name
  nextTick(() => {
    editInputRef.value?.focus()
    editInputRef.value?.select()
  })
}

function cancelEdit() {
  editingId.value = null
  editingName.value = ''
}

async function commitEdit(item: CostItemDto) {
  const name = editingName.value.trim()
  if (!name) {
    message.warning('名称不能为空')
    nextTick(() => editInputRef.value?.focus())
    return
  }
  const oldName = item.name
  editingId.value = null  // 先关闭输入框，避免 blur 重入
  if (name === oldName) return
  try {
    await updateCostItem(item.id, { name, isRebate: item.isRebate, sortOrder: item.sortOrder })
    emit('renamed', item.id, name)
    message.success('重命名成功')
  } catch {
    message.error('重命名失败')
  }
}

function handleKeydown(e: KeyboardEvent) {
  if (editingId.value !== null) return  // 编辑中不响应导航
  if (props.items.length === 0) return
  if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return
  e.preventDefault()
  const currentIdx = props.items.findIndex(i => i.id === props.activeId)
  let nextIdx: number
  if (e.key === 'ArrowDown') {
    nextIdx = currentIdx < 0 ? 0 : Math.min(currentIdx + 1, props.items.length - 1)
  }
  else {
    nextIdx = currentIdx < 0 ? 0 : Math.max(currentIdx - 1, 0)
  }
  const next = props.items[nextIdx]
  if (next && next.id !== props.activeId) {
    emit('select', next.id)
  }
}
</script>

<style scoped lang="scss">
.cost-item-sidebar {
  height: 100%;
  background-color: #fff;
  padding: 8px;
  outline: none;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sidebar-title {
  font-size: 13px;
  font-weight: 600;
  color: #666;
  padding: 4px 8px 8px;
  flex-shrink: 0;
}

.sidebar-list {
  flex: 1;
  overflow-y: auto;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 12px;
  border-radius: 4px;
  margin-bottom: 4px;
  cursor: pointer;
  font-size: 14px;
  color: #333;
  border-left: 3px solid transparent;
  transition: background-color 0.15s, border-color 0.15s, color 0.15s;
  user-select: none;

  &:hover {
    background-color: #f5f5f5;
  }

  &.is-active {
    background-color: var(--bg-muted);
    border-left-color: var(--color-primary);
    color: var(--text-1);
    font-weight: 500;

    &:hover {
      background-color: var(--bg-muted);
    }
  }

  &.is-empty:not(.is-active) {
    color: #999;
  }
}

.item-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rebate-tag {
  margin-right: 0;
  font-size: 11px;
  line-height: 16px;
  padding: 0 4px;
  flex-shrink: 0;
}

.item-badge {
  flex-shrink: 0;
}

.empty-tip {
  text-align: center;
  color: #bfbfbf;
  padding: 24px 0;
  font-size: 13px;
}

.item-name-input {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--color-primary);
  border-radius: 3px;
  padding: 1px 5px;
  font-size: 13px;
  outline: none;
  background: #fff;
  color: #333;
  height: 22px;
  line-height: 20px;
}
</style>
