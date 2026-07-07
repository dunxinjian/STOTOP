<script setup lang="ts">
/**
 * 结构化竖向流程图（plan M1-2/M1-3，mock 总体屏 3/4）。
 * 受限编辑：只能在"+"处插入合法节点——图永远合法。
 * 无内部镜像：所有变更经投影模块的 insert 系纯函数产出新 stages/routes 后整体 emit，
 * 由编辑页 state 深监听统一进撤销栈+自动保存（B9 教训：不做 v-model 回抛）。
 */
import { computed, ref } from 'vue'
import {
  buildFlowTree,
  insertStageAfter,
  insertBranchGroup,
  type FlowTreeNode,
  type FlowTreeBranch,
  type InsertAnchor,
} from '@/utils/flowGraphProjection'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import type { StageRouteRuleRequest } from '@/types/cardflow'
import type { HealthItem } from '@/utils/cardflowDiagnostics'
import FlowGraphNode from './FlowGraphNode.vue'
import ConditionSummary from './ConditionSummary.vue'
import type { FieldOption } from '@/components/cardflow/ConditionBuilder.vue'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  diagnostics?: HealthItem[]
  selectedType?: 'blank' | 'node' | 'edge'
  selectedKey?: string | null
  conditionFields?: FieldOption[]
}>()

const emit = defineEmits<{
  'select-node': [stageKey: string]
  'select-edge': [edgeKey: string]
  /** 结构变更：新 stages/routes 整体上抛（编辑页直接替换 state） */
  'update-structure': [payload: { stages: StageDefinition[]; routes: StageRouteRuleRequest[] }]
}>()

const projection = computed(() => buildFlowTree(props.stages, props.routes))

/** 诊断按 target.key 聚合（节点角标；设计 C2） */
const issuesByKey = computed(() => {
  const map = new Map<string, { total: number; errors: number }>()
  for (const item of props.diagnostics ?? []) {
    if (!item.target?.key) continue
    const entry = map.get(item.target.key) ?? { total: 0, errors: 0 }
    entry.total++
    if (item.level === 'error') entry.errors++
    map.set(item.target.key, entry)
  }
  return map
})

const stageById = computed(() => new Map(props.stages.map((s) => [s.id, s])))
const routeByEdge = computed(() => new Map(props.routes.map((r) => [r.edgeKey, r])))

// ==================== "+" 插入菜单 ====================

const openMenuAnchor = ref<string | null>(null)

function anchorId(anchor: InsertAnchor): string {
  return 'afterStageId' in anchor ? `s:${anchor.afterStageId}` : `e:${anchor.branchEdgeKey}`
}

function toggleMenu(anchor: InsertAnchor) {
  const id = anchorId(anchor)
  openMenuAnchor.value = openMenuAnchor.value === id ? null : id
}

let stageSeq = 0
function genStageId(): string {
  stageSeq += 1
  return `stage_${Date.now().toString(36)}_${stageSeq}${Math.random().toString(36).slice(2, 6)}`
}

type InsertKind = 'approval' | 'cc' | 'branch' | 'auto'

const MENU_ITEMS: Array<{ kind: InsertKind; label: string; icon: string; iconClass: string }> = [
  { kind: 'approval', label: '审批人', icon: '审', iconClass: 'is-appr' },
  { kind: 'branch', label: '条件分支', icon: '分', iconClass: 'is-branch' },
  { kind: 'auto', label: '自动处理', icon: '自', iconClass: 'is-auto' },
]

function buildNewStage(kind: InsertKind): StageDefinition {
  const id = genStageId()
  if (kind === 'auto') {
    return { id, name: '新自动节点', type: 'auto', sortOrder: 0, failurePolicy: 'halt' }
  }
  return {
    id,
    name: kind === 'approval' ? '新审批节点' : '新节点',
    type: 'manual',
    sortOrder: 0,
    approvalMode: 'orsign',
  }
}

function handleInsert(anchor: InsertAnchor, kind: InsertKind) {
  openMenuAnchor.value = null
  if (kind === 'branch') {
    if ('afterStageId' in anchor) {
      emit('update-structure', insertBranchGroup(props.stages, props.routes, anchor, 2))
    }
    return
  }
  const newStage = buildNewStage(kind)
  const result = insertStageAfter(props.stages, props.routes, anchor, newStage)
  emit('update-structure', result)
  // 等 props 更新一拍后再选中（选中即开抽屉，需新节点已在 stages 中）
  requestAnimationFrame(() => emit('select-node', newStage.id))
}

/** 分支插入仅支持节点锚（分支组内的列锚点走 branchEdgeKey 插普通节点） */
function menuItemsFor(anchor: InsertAnchor) {
  return 'afterStageId' in anchor ? MENU_ITEMS : MENU_ITEMS.filter((m) => m.kind !== 'branch')
}

function conditionGroupOf(edgeKey: string) {
  const route = routeByEdge.value.get(edgeKey)
  if (!route?.conditionJson) return { logic: 'and' as const, conditions: [] }
  try {
    const parsed = JSON.parse(route.conditionJson)
    if (parsed && Array.isArray(parsed.conditions)) return parsed
    return { logic: 'and' as const, conditions: [] }
  } catch {
    return { logic: 'and' as const, conditions: [] }
  }
}
</script>

<template>
  <div class="cfd-graph" @click.self="openMenuAnchor = null">
    <!-- 起点（隐含节点，不在 stages 中） -->
    <div class="cfd-node cfd-node--start cfd-graph__terminal">
      <div class="cfd-node__head">
        <span class="cfd-node__icon">起</span>
        <span class="cfd-node__title">发起人</span>
        <a-tag :bordered="false">起点</a-tag>
      </div>
    </div>

    <template v-for="(node, i) in projection.tree" :key="node.kind === 'stage' ? node.stageId : `bg-${i}`">
      <!-- 节点前连接件："+"锚定在上一个 stage 或起点 -->
      <div class="cfd-connector"></div>
      <div class="cfd-graph__pluswrap">
        <button
          v-if="node.kind === 'stage' && i === 0"
          class="cfd-plus"
          :class="{ 'is-open': openMenuAnchor === null }"
          style="visibility: hidden"
          tabindex="-1"
          aria-hidden="true"
        >+</button>
        <template v-else></template>
      </div>

      <!-- stage 节点 -->
      <template v-if="node.kind === 'stage' && node.stageId">
        <FlowGraphNode
          :stage="stageById.get(node.stageId)!"
          :selected="selectedType === 'node' && selectedKey === node.stageId"
          :issue-count="issuesByKey.get(node.stageId)?.total"
          :error-count="issuesByKey.get(node.stageId)?.errors"
          @select="emit('select-node', node.stageId!)"
        />
        <div class="cfd-connector"></div>
        <div class="cfd-graph__pluswrap">
          <button
            class="cfd-plus"
            :class="{ 'is-open': openMenuAnchor === `s:${node.stageId}` }"
            :aria-label="`在 ${stageById.get(node.stageId)?.name || '节点'} 之后添加节点`"
            @click.stop="toggleMenu({ afterStageId: node.stageId })"
          >+</button>
          <div v-if="openMenuAnchor === `s:${node.stageId}`" class="cfd-pmenu cfd-graph__menu">
            <div
              v-for="item in menuItemsFor({ afterStageId: node.stageId })"
              :key="item.kind"
              class="cfd-pmenu__row"
              @click.stop="handleInsert({ afterStageId: node.stageId! }, item.kind)"
            >
              <span class="cfd-pmenu__icon" :class="item.iconClass">{{ item.icon }}</span>
              {{ item.label }}
            </div>
          </div>
        </div>
      </template>

      <!-- 分支组：横向展开（mock 屏 4） -->
      <template v-else-if="node.kind === 'branchGroup'">
        <div class="cfd-branch-bar" :style="{ width: `${Math.min(86, (node.branches?.length ?? 0) * 28)}%` }"></div>
        <div class="cfd-branch-cols">
          <div v-for="branch in node.branches" :key="branch.routeEdgeKey" class="cfd-branch-col">
            <div class="cfd-branch-stem"></div>
            <div
              class="cfd-branch-head"
              :class="{ 'is-default': branch.isDefault, 'is-selected': selectedType === 'edge' && selectedKey === branch.routeEdgeKey }"
              role="button"
              tabindex="0"
              :aria-label="`分支 ${routeByEdge.get(branch.routeEdgeKey)?.routeName || ''}${branch.isDefault ? '（兜底）' : `，优先级 ${branch.priority}`}`"
              @click="emit('select-edge', branch.routeEdgeKey)"
              @keydown.enter.prevent="emit('select-edge', branch.routeEdgeKey)"
            >
              <div class="cfd-branch-head__row">
                <span class="cfd-branch-head__name">{{ routeByEdge.get(branch.routeEdgeKey)?.routeName || (branch.isDefault ? '其他情况' : '条件分支') }}</span>
                <span class="cfd-branch-prio" :class="{ 'is-default': branch.isDefault }">
                  {{ branch.isDefault ? '兜底' : `优先级 ${branch.priority}` }}
                </span>
              </div>
              <div class="cfd-branch-cond">
                <template v-if="branch.isDefault">以上条件均不满足</template>
                <ConditionSummary
                  v-else
                  :conditions="conditionGroupOf(branch.routeEdgeKey)"
                  :fields="conditionFields ?? []"
                  @expand="emit('select-edge', branch.routeEdgeKey)"
                />
              </div>
            </div>

            <!-- 分支列内插入锚 -->
            <div class="cfd-connector"></div>
            <div class="cfd-graph__pluswrap">
              <button
                class="cfd-plus cfd-plus--sm"
                :class="{ 'is-open': openMenuAnchor === `e:${branch.routeEdgeKey}` }"
                :aria-label="`在分支 ${routeByEdge.get(branch.routeEdgeKey)?.routeName || ''} 内添加节点`"
                @click.stop="toggleMenu({ branchEdgeKey: branch.routeEdgeKey })"
              >+</button>
              <div v-if="openMenuAnchor === `e:${branch.routeEdgeKey}`" class="cfd-pmenu cfd-graph__menu">
                <div
                  v-for="item in menuItemsFor({ branchEdgeKey: branch.routeEdgeKey })"
                  :key="item.kind"
                  class="cfd-pmenu__row"
                  @click.stop="handleInsert({ branchEdgeKey: branch.routeEdgeKey }, item.kind)"
                >
                  <span class="cfd-pmenu__icon" :class="item.iconClass">{{ item.icon }}</span>
                  {{ item.label }}
                </div>
              </div>
            </div>

            <!-- 支内子链（递归渲染子 stage；嵌套分支组降级为摘要） -->
            <template v-for="child in branch.children" :key="child.kind === 'stage' ? child.stageId : 'nested'">
              <template v-if="child.kind === 'stage' && child.stageId">
                <FlowGraphNode
                  class="cfd-graph__branchnode"
                  :stage="stageById.get(child.stageId)!"
                  :selected="selectedType === 'node' && selectedKey === child.stageId"
                  :issue-count="issuesByKey.get(child.stageId)?.total"
                  :error-count="issuesByKey.get(child.stageId)?.errors"
                  @select="emit('select-node', child.stageId!)"
                />
                <div class="cfd-connector"></div>
                <div class="cfd-graph__pluswrap">
                  <button
                    class="cfd-plus cfd-plus--sm"
                    :class="{ 'is-open': openMenuAnchor === `s:${child.stageId}` }"
                    @click.stop="toggleMenu({ afterStageId: child.stageId })"
                  >+</button>
                  <div v-if="openMenuAnchor === `s:${child.stageId}`" class="cfd-pmenu cfd-graph__menu">
                    <div
                      v-for="item in menuItemsFor({ afterStageId: child.stageId })"
                      :key="item.kind"
                      class="cfd-pmenu__row"
                      @click.stop="handleInsert({ afterStageId: child.stageId! }, item.kind)"
                    >
                      <span class="cfd-pmenu__icon" :class="item.iconClass">{{ item.icon }}</span>
                      {{ item.label }}
                    </div>
                  </div>
                </div>
              </template>
              <div v-else class="cfd-graph__nested">嵌套分支 · 点击分支头进入编辑</div>
            </template>
          </div>
        </div>
        <div class="cfd-branch-bar" :style="{ width: `${Math.min(86, (node.branches?.length ?? 0) * 28)}%` }"></div>
      </template>
    </template>

    <!-- 复杂区段/孤儿提示 -->
    <div v-if="projection.complex.length" class="cfd-graph__notice is-warn">
      存在复杂连接（{{ projection.complex.map((id) => stageById.get(id)?.name || id).join('、') }}），竖向图仅展示一次，完整拓扑请切换「只读总览图」查看。
    </div>
    <div v-if="projection.orphans.length" class="cfd-graph__notice is-error">
      {{ projection.orphans.length }} 个节点不可达：{{ projection.orphans.map((id) => stageById.get(id)?.name || id).join('、') }}（点击节点链或诊断面板处理）
    </div>

    <!-- 终点 -->
    <div class="cfd-connector"></div>
    <div class="cfd-node cfd-node--end cfd-graph__terminal">
      <div class="cfd-node__head">
        <span class="cfd-node__icon">终</span>
        <span class="cfd-node__title">结束</span>
        <a-tag color="green" :bordered="false">终点</a-tag>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.cfd-graph {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 100%;
  padding: 22px 26px;
  overflow: auto;
}

.cfd-graph__terminal {
  width: 340px;
}

.cfd-graph__pluswrap {
  position: relative;
  display: flex;
  justify-content: center;
}

.cfd-graph__menu {
  position: absolute;
  top: -4px;
  left: calc(50% + 22px);
}

.cfd-plus--sm {
  width: 22px;
  height: 22px;
  font-size: 14px;
}

.cfd-graph__branchnode {
  width: 100%;
}

.cfd-graph__nested {
  padding: 6px 10px;
  font-size: $font-size-sm;
  color: $text-secondary;
  background: $bg-page;
  border: 1px dashed $border-color;
  border-radius: 6px;
}

.cfd-graph__notice {
  max-width: 560px;
  padding: 8px 12px;
  margin-top: 12px;
  font-size: $font-size-sm;
  border-radius: 8px;

  &.is-warn {
    color: var(--color-warning-text);
    background: var(--color-warning-light);
    border: 1px solid var(--color-warning-border);
  }

  &.is-error {
    color: var(--color-danger-text);
    background: var(--color-danger-light);
    border: 1px solid var(--color-danger-border);
  }
}

.cfd-pmenu__icon {
  &.is-appr { background: var(--color-primary); }
  &.is-branch { background: var(--color-warning); }
  &.is-auto { background: var(--color-flow-auto); }
}
</style>
