<template>
  <div class="goal-tree-view">
    <a-tree
      v-if="treeNodes.length"
      :tree-data="treeNodes"
      default-expand-all
      block-node
      show-line
    >
      <template #title="node">
        <div class="tree-node">
          <div class="tree-node__info">
            <span class="tree-node__title">{{ node.title }}</span>
            <a-tag :color="levelColor(node.level)" size="small">{{ levelLabel(node.level) }}</a-tag>
            <a-tag :color="statusColor(node.status)" size="small">{{ statusLabel(node.status) }}</a-tag>
          </div>
          <div class="tree-node__meta">
            <a-progress :percent="node.progress" size="small" :stroke-color="progressColor(node.progress)" style="width: 120px" />
            <span v-if="node.responsibleName" class="tree-node__owner">
              <UserOutlined /> {{ node.responsibleName }}
            </span>
            <span class="tree-node__kr">KR {{ node.keyResultCount ?? 0 }}</span>
          </div>
        </div>
      </template>
    </a-tree>
    <EmptyState v-else title="暂无目标层级数据" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { UserOutlined } from '@ant-design/icons-vue'
import type { DataNode } from 'ant-design-vue/es/tree'
import type { GoalTreeDto } from '@/types/task'

const props = defineProps<{
  data: GoalTreeDto[]
}>()

function toNodes(items: GoalTreeDto[]): DataNode[] {
  return items.map((item) => ({
    ...item,
    key: item.id,
    children: item.children?.length ? toNodes(item.children) : undefined,
  }))
}

const treeNodes = computed<DataNode[]>(() => toNodes(props.data))

const levelLabels: Record<string, string> = { yearly: '年度', quarterly: '季度', monthly: '月度' }
const levelColors: Record<string, string> = { yearly: 'blue', quarterly: 'orange', monthly: 'green' }
const statusLabels: Record<number, string> = { 0: '草稿', 1: '进行中', 2: '已完成', 3: '已取消' }
const statusColors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }

function levelLabel(l: string) { return levelLabels[l] ?? l }
function levelColor(l: string) { return levelColors[l] ?? 'default' }
function statusLabel(s: number) { return statusLabels[s] ?? `${s}` }
function statusColor(s: number) { return statusColors[s] ?? 'default' }
function progressColor(p: number) { return p >= 80 ? 'var(--color-success)' : p >= 40 ? 'var(--color-info)' : 'var(--color-warning)' }
</script>

<style scoped lang="scss">
.goal-tree-view {
  padding: 4px 0;
}

.tree-node {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 0;
  width: 100%;
  min-width: 0;

  &__info {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-shrink: 1;
    min-width: 0;
  }

  &__title {
    font-weight: 500;
    font-size: 14px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 260px;
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-shrink: 0;
    font-size: 12px;
    color: #8c8c8c;
  }

  &__owner {
    display: inline-flex;
    align-items: center;
    gap: 4px;
  }

  &__kr {
    color: var(--color-info);
  }
}
</style>
