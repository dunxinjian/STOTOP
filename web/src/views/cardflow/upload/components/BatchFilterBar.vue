<template>
  <div class="filter-bar">
    <div class="filter-tags">
      <span
        v-for="tag in statusTags"
        :key="tag.key"
        class="filter-tag"
        :class="{ active: filters.status === tag.key }"
        @click="emit('update:filters', { ...filters, status: tag.key })"
      >
        {{ tag.label }}
      </span>
    </div>
    <div class="filter-right">
      <!-- 看板 / 列表切换（仅管理者） -->
      <div v-if="showViewToggle" class="view-toggle">
        <button
          class="view-toggle-btn"
          :class="{ active: viewMode === 'list' }"
          @click="emit('update:viewMode', 'list')"
        >列表</button>
        <button
          class="view-toggle-btn"
          :class="{ active: viewMode === 'board' }"
          @click="emit('update:viewMode', 'board')"
        >看板</button>
      </div>
      <!-- 业务类型 -->
      <a-select
        :value="filters.bizType"
        style="width: 96px"
        size="small"
        @change="(val: SelectValue) => emit('update:filters', { ...filters, bizType: String(val) })"
      >
        <a-select-option value="all">全部类型</a-select-option>
        <a-select-option value="expense">费用报销</a-select-option>
        <a-select-option value="express">快递运费</a-select-option>
        <a-select-option value="customer">客户数据</a-select-option>
      </a-select>
      <!-- 时间范围 -->
      <a-select
        :value="filters.timeRange"
        style="width: 76px"
        size="small"
        @change="(val: SelectValue) => emit('update:filters', { ...filters, timeRange: val as FilterState['timeRange'] })"
      >
        <a-select-option value="today">今天</a-select-option>
        <a-select-option value="week">本周</a-select-option>
        <a-select-option value="month">本月</a-select-option>
      </a-select>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { SelectValue } from 'ant-design-vue/es/select'
import type { FilterState, ViewMode } from '../composables/useUploadCenter'

defineProps<{
  filters: FilterState
  viewMode: ViewMode
  showViewToggle: boolean
}>()

const emit = defineEmits<{
  'update:filters': [value: FilterState]
  'update:viewMode': [value: ViewMode]
}>()

const statusTags = [
  { key: 'all', label: '全部' },
  { key: 'processing', label: '处理中' },
  { key: 'pending', label: '待确认' },
  { key: 'error', label: '有异常' },
  { key: 'success', label: '已完成' },
] as const
</script>

<style scoped lang="scss">
.filter-bar {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  padding: 0;
  margin-bottom: 0;
  gap: 8px;
}

.filter-tags {
  display: flex;
  gap: 4px;
}

.filter-tag {
  padding: 2px 8px;
  border-radius: 3px;
  font-size: 12px;
  line-height: 20px;
  cursor: pointer;
  border: 1px solid var(--border);
  color: var(--text-2);
  transition: all 0.2s;
  user-select: none;

  &:hover {
    color: var(--color-primary);
    border-color: var(--color-primary);
  }

  &.active {
    background: var(--bg-card);
    color: var(--text-1);
    border-color: var(--border-strong);
    box-shadow: var(--shadow-sm);
  }
}

.filter-right {
  display: flex;
  gap: 6px;
  align-items: center;

  :deep(.ant-select) {
    font-size: 12px;

    .ant-select-selector {
      height: 24px !important;
      padding: 0 8px !important;
      font-size: 12px;
    }

    .ant-select-selection-item {
      line-height: 22px !important;
      font-size: 12px;
    }

    .ant-select-arrow {
      font-size: 10px;
    }
  }
}

.view-toggle {
  display: flex;
  background: var(--bg-muted);
  border-radius: 4px;
  padding: 2px;
}

.view-toggle-btn {
  padding: 2px 8px;
  border-radius: 3px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 12px;
  color: var(--text-2);
  transition: all 0.2s;

  &.active {
    background: var(--bg-card);
    color: var(--text-1);
    box-shadow: var(--shadow-sm);
  }
}
</style>
