<!--
  StatFilterTabs —— 带计数的状态快筛 Tab 条
  统一替代各页手写的「KPI 统计卡 + 状态下拉筛选」（VehicleManage 顶部统计卡 / CustomerManage .status-tab-bar / ServiceOrderManage .tab-item）。
  KPI 计数 + 点击过滤合一：v-model:active 绑当前选中 key（'' 通常表示「全部」），点击切换并 emit change(key)。
  全令牌、无裸 hex；可选 color（传 var(--token) 语义色）渲染状态圆点。
-->
<template>
  <div class="stat-filter-tabs" :class="{ 'stat-filter-tabs--inline': inline }">
    <button
      v-for="tab in tabs"
      :key="tab.key"
      type="button"
      class="stat-filter-tab"
      :class="{ 'stat-filter-tab--active': tab.key === active }"
      @click="select(tab.key)"
    >
      <span v-if="tab.color" class="stat-filter-tab__dot" :style="{ background: tab.color }" />
      <span class="stat-filter-tab__label">{{ tab.label }}</span>
      <span class="stat-filter-tab__count">{{ tab.count ?? 0 }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
interface TabItem {
  /** 选中值；'' 常表示「全部」 */
  key: string | number
  /** 文案 */
  label: string
  /** 计数 */
  count?: number
  /** 可选状态圆点色，传 var(--token) */
  color?: string
}

const props = withDefaults(defineProps<{
  tabs: TabItem[]
  active: string | number
  /** 置于工具栏内时去掉底部外边距 */
  inline?: boolean
}>(), {
  inline: false,
})

const emit = defineEmits<{
  (e: 'update:active', key: string | number): void
  (e: 'change', key: string | number): void
}>()

function select(key: string | number) {
  emit('update:active', key)
  emit('change', key)
}
</script>

<style scoped lang="scss">
.stat-filter-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm8);
  margin-bottom: var(--space-md12);
}

.stat-filter-tabs--inline {
  margin-bottom: 0;
}

.stat-filter-tab {
  display: inline-flex;
  align-items: center;
  gap: var(--space-xs4);
  padding: var(--space-sm8) var(--space-md12);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  background: var(--bg-card);
  color: var(--text-2);
  font-size: var(--font-sm2);
  line-height: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.stat-filter-tab:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.stat-filter-tab--active {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: var(--color-primary-light);
}

.stat-filter-tab__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
}

.stat-filter-tab__count {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
</style>
