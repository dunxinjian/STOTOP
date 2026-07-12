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
  insertStageAtHead,
  insertBranchGroup,
  deleteBranch,
  copyBranch,
  reorderBranch,
  collectBranchStages,
  deleteStage,
  type FlowTreeNode,
  type FlowTreeBranch,
  type InsertAnchor,
} from '@/utils/flowGraphProjection'
import { Modal } from 'ant-design-vue'
import { LeftOutlined, RightOutlined, CopyOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import { NOTIFY_PLUGIN_REGISTRY_ID } from '@/components/cardflow/stageDefinitionShared'
import type { StageRouteRuleRequest } from '@/types/cardflow'
import type { HealthItem } from '@/utils/cardflowDiagnostics'
import FlowGraphNode from './FlowGraphNode.vue'
import ConditionSummary from './ConditionSummary.vue'
import BranchDeleteConfirm from './BranchDeleteConfirm.vue'
import type { FieldOption } from '@/components/cardflow/ConditionBuilder.vue'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  diagnostics?: HealthItem[]
  selectedType?: 'blank' | 'node' | 'edge' | 'start'
  selectedKey?: string | null
  conditionFields?: FieldOption[]
  /** 干跑命中路径的 stageKey 集合（M5-4 路径点亮） */
  hitStageKeys?: string[]
  /** 发布设置里的默认超时（小时）——新建审批节点预填（F4） */
  defaultTimeoutHours?: number
}>()

const emit = defineEmits<{
  'select-node': [stageKey: string]
  'select-edge': [edgeKey: string]
  /** 结构变更：新 stages/routes 整体上抛（编辑页直接替换 state） */
  'update-structure': [payload: { stages: StageDefinition[]; routes: StageRouteRuleRequest[]; label?: string }]
  /** 点击起点节点：唤起发起抽屉（M8-A 件②——发起范围配置） */
  'select-start': []
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
const hitSet = computed(() => new Set(props.hitStageKeys ?? []))

/** 键盘导航（M7-4/D8）：↑↓ 在可聚焦节点/分支头间移动 roving focus */
function onGraphKeydown(e: KeyboardEvent) {
  if (e.key !== 'ArrowUp' && e.key !== 'ArrowDown') return
  const root = e.currentTarget as HTMLElement
  const focusables = Array.from(
    root.querySelectorAll<HTMLElement>('.cfd-node[tabindex="0"], .cfd-branch-head[tabindex="0"]'),
  )
  if (!focusables.length) return
  const active = document.activeElement as HTMLElement
  const idx = focusables.indexOf(active)
  e.preventDefault()
  const next = e.key === 'ArrowDown'
    ? focusables[Math.min(idx + 1, focusables.length - 1)] ?? focusables[0]
    : focusables[Math.max(idx - 1, 0)] ?? focusables[0]
  next.focus()
}

// ==================== "+" 插入菜单 ====================

const openMenuAnchor = ref<string | null>(null)

function anchorId(anchor: InsertAnchor): string {
  if ('afterStageId' in anchor) return `s:${anchor.afterStageId}`
  if ('branchEdgeKey' in anchor) return `e:${anchor.branchEdgeKey}`
  return 'head'
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
  { kind: 'cc', label: '抄送人', icon: '抄', iconClass: 'is-cc' },
  { kind: 'branch', label: '条件分支', icon: '分', iconClass: 'is-branch' },
  { kind: 'auto', label: '自动处理', icon: '自', iconClass: 'is-auto' },
]

function buildNewStage(kind: InsertKind): StageDefinition {
  const id = genStageId()
  if (kind === 'auto') {
    return { id, name: '新自动节点', type: 'auto', sortOrder: 0, failurePolicy: 'halt' }
  }
  if (kind === 'cc') {
    // 抄送 = auto 节点 + 通知插件（AlertNotify）预设——引擎按 auto 执行通知，不新增 cc FType。
    return {
      id,
      name: '新抄送节点',
      type: 'auto',
      sortOrder: 0,
      pluginRegistryId: NOTIFY_PLUGIN_REGISTRY_ID,
      ccConfigJson: JSON.stringify({ users: [], timing: 'onEnter' }),
      failurePolicy: 'skip',
    }
  }
  return {
    id,
    name: kind === 'approval' ? '新审批节点' : '新节点',
    type: 'manual',
    sortOrder: 0,
    approvalMode: 'orsign',
    ...(props.defaultTimeoutHours ? { timeoutHours: props.defaultTimeoutHours } : {}),
  }
}

function handleInsert(anchor: InsertAnchor, kind: InsertKind) {
  openMenuAnchor.value = null
  if (kind === 'branch') {
    if ('afterStageId' in anchor) {
      // 每个条件支自动补一个占位审批节点（业务约束：条件分支汇合前须至少一个处理节点）；兜底支不补。
      emit('update-structure', { ...insertBranchGroup(props.stages, props.routes, anchor, 2, () => buildNewStage('approval')), label: '添加条件分支' })
    }
    return
  }
  const newStage = buildNewStage(kind)
  const result = 'atHead' in anchor
    ? insertStageAtHead(props.stages, props.routes, newStage)
    : insertStageAfter(props.stages, props.routes, anchor, newStage)
  emit('update-structure', { ...result, label: `添加节点「${newStage.name}」` })
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

// ==================== 分支操作（M1-4）====================

const deleteTarget = ref<{ edgeKey: string; name: string; stageNames: string[] } | null>(null)

function askDeleteBranch(edgeKey: string) {
  const route = routeByEdge.value.get(edgeKey)
  if (!route || route.isDefault) return
  const stageIds = collectBranchStages(props.stages, props.routes, edgeKey)
  deleteTarget.value = {
    edgeKey,
    name: route.routeName || '条件分支',
    stageNames: stageIds.map((id) => stageById.value.get(id)?.name || id),
  }
}

function confirmDeleteBranch() {
  if (!deleteTarget.value) return
  emit('update-structure', { ...deleteBranch(props.stages, props.routes, deleteTarget.value.edgeKey), label: `删除分支「${deleteTarget.value.name}」` })
  deleteTarget.value = null
}

function handleCopyBranch(edgeKey: string) {
  emit('update-structure', { ...copyBranch(props.stages, props.routes, edgeKey), label: '复制分支' })
}

function handleReorderBranch(edgeKey: string, dir: 'left' | 'right') {
  emit('update-structure', { ...reorderBranch(props.stages, props.routes, edgeKey, dir), label: '调整分支优先级' })
}

/** 条件列在同组内的位置（首/末列禁用对应方向按钮） */
function branchPosition(group: FlowTreeNode, edgeKey: string): { first: boolean; last: boolean } {
  const conds = (group.branches ?? []).filter((b) => !b.isDefault)
  const idx = conds.findIndex((b) => b.routeEdgeKey === edgeKey)
  return { first: idx <= 0, last: idx === conds.length - 1 }
}

// ==================== 圆角分叉/汇合连线（钉钉/企微式） ====================
// viewBox 恒 0 0 1000 24，preserveAspectRatio=none 拉伸至列容器宽；列中心 x=(i+0.5)/n*1000，
// 外列圆角、内列直下 T 接，与网格列中心对齐（padding 8 ≈ 抵消 gap，误差 <1%）。
const FORK_R = 8

function branchColXs(n: number): number[] {
  return Array.from({ length: n }, (_, i) => Math.round(((i + 0.5) / n) * 1000))
}

/** 顶部分叉：主干下探到横母线，母线圆角分到各列（bus y=8，底 y=24）。 */
function forkPaths(n: number): string[] {
  if (n < 1) return []
  const busY = 8, botY = 24, cx = 500
  const xs = branchColXs(n)
  const x0 = xs[0], xL = xs[n - 1]
  const centerCol = xs.some((x) => Math.abs(x - cx) < 2)
  const paths = [`M${cx} 0 V${centerCol ? botY : busY}`]
  if (n >= 2) paths.push(`M${x0} ${botY} V${busY + FORK_R} Q${x0} ${busY} ${x0 + FORK_R} ${busY} H${xL - FORK_R} Q${xL} ${busY} ${xL} ${busY + FORK_R} V${botY}`)
  for (let i = 1; i < n - 1; i++) {
    if (Math.abs(xs[i] - cx) < 2) continue
    paths.push(`M${xs[i]} ${busY} V${botY}`)
  }
  return paths
}

/** 底部汇合：各列圆角收拢到横母线，主干续下（顶 y=0，bus y=16，出 y=24）。 */
function mergePaths(n: number): string[] {
  if (n < 1) return []
  const topY = 0, busY = 16, outY = 24, cx = 500
  const xs = branchColXs(n)
  const x0 = xs[0], xL = xs[n - 1]
  const centerCol = xs.some((x) => Math.abs(x - cx) < 2)
  const paths = [`M${cx} ${centerCol ? topY : busY} V${outY}`]
  if (n >= 2) paths.push(`M${x0} ${topY} V${busY - FORK_R} Q${x0} ${busY} ${x0 + FORK_R} ${busY} H${xL - FORK_R} Q${xL} ${busY} ${xL} ${busY - FORK_R} V${topY}`)
  for (let i = 1; i < n - 1; i++) {
    if (Math.abs(xs[i] - cx) < 2) continue
    paths.push(`M${xs[i]} ${topY} V${busY}`)
  }
  return paths
}

// ==================== 节点删除（E0-D1，设计 C7 分级确认）====================

/** 节点是否"有配置"（决定轻/重确认） */
function stageHasConfig(stage: StageDefinition): boolean {
  return Boolean(
    stage.assigneeConfigJson ||
    Object.keys(stage.viewProfile?.fieldAccess || {}).length ||
    stage.conditionJson ||
    stage.pluginRegistryId,
  )
}

function askDeleteStage(stageId: string) {
  const stage = stageById.value.get(stageId)
  if (!stage) return
  const outgoing = props.routes.filter((r) => (r.status ?? 'active') === 'active' && r.fromStageKey === stageId)
  const isBranchSource = outgoing.length > 1
  const heavy = stageHasConfig(stage) || isBranchSource
  const doDelete = () => {
    emit('update-structure', { ...deleteStage(props.stages, props.routes, stageId), label: `删除节点「${stage.name || stageId}」` })
    if (props.selectedKey === stageId) emit('select-node', '')
  }
  if (!heavy) {
    Modal.confirm({
      title: `删除节点「${stage.name || '未命名节点'}」？`,
      okText: '删除', okType: 'danger', cancelText: '取消',
      onOk: doDelete,
    })
    return
  }
  const lost: string[] = []
  if (stage.assigneeConfigJson) lost.push('处理人策略')
  const permCount = Object.keys(stage.viewProfile?.fieldAccess || {}).length
  if (permCount) lost.push(`${permCount} 项字段权限`)
  if (stage.conditionJson) lost.push('进入条件')
  if (stage.pluginRegistryId) lost.push('插件配置')
  Modal.confirm({
    title: `删除节点「${stage.name || '未命名节点'}」？`,
    content: `将丢失：${lost.join('、') || '基础配置'}。${isBranchSource ? '该节点是分支源，其条件分支组的全部条件边将一并删除。' : ''}入边将重定向到其后继节点。在途卡片按发布时的在途策略处理。`,
    okText: '删除节点', okType: 'danger', cancelText: '取消', width: 440,
    onOk: doDelete,
  })
}
</script>

<template>
  <div
    class="cfd-graph"
    role="tree"
    aria-label="流程节点树，方向键在节点间移动，Enter 编辑，Delete 删除"
    @click.self="openMenuAnchor = null"
    @keydown="onGraphKeydown"
  >
    <!-- 起点（隐含节点，不在 stages 中）；点击唤起发起抽屉配置发起范围（M8-A 件②）。代提交/重提走向仍属后续规划 -->
    <a-popover placement="right" trigger="click">
      <template #content>
        <div class="cfd-graph__startpop">
          <p><b>发起人节点</b></p>
          <p>谁可以发起：按发起范围（角色/组织/岗位/人员）圈定；四维留空=不限制。</p>
          <p class="cfd-graph__startpop-muted">代他人提交 / 被退回后重提走向 属后续规划，暂不可配置。</p>
        </div>
      </template>
      <div
        class="cfd-node cfd-node--start cfd-graph__terminal"
        role="button" tabindex="0" aria-label="发起人节点，点击配置发起范围"
        @click.stop="$emit('select-start')"
        @keydown.enter.prevent="$emit('select-start')"
      >
        <div class="cfd-node__head">
          <span class="cfd-node__icon">起</span>
          <span class="cfd-node__title">发起人</span>
          <a-tag :bordered="false">起点</a-tag>
        </div>
      </div>
    </a-popover>

    <template v-for="(node, i) in projection.tree" :key="node.kind === 'stage' ? node.stageId : `bg-${i}`">
      <!-- 节点前连接件；i===0 为"起点后/首节点前"头部插入锚（+ 可插入新首节点），前后各一段连接件保证连线连续 -->
      <div class="cfd-connector"></div>
      <div class="cfd-graph__pluswrap">
        <template v-if="node.kind === 'stage' && i === 0">
          <button
            class="cfd-plus"
            :class="{ 'is-open': openMenuAnchor === 'head' }"
            aria-label="在起点后添加节点"
            @click.stop="toggleMenu({ atHead: true })"
          >+</button>
          <div v-if="openMenuAnchor === 'head'" class="cfd-pmenu cfd-graph__menu">
            <div
              v-for="item in menuItemsFor({ atHead: true })"
              :key="item.kind"
              class="cfd-pmenu__row"
              @click.stop="handleInsert({ atHead: true }, item.kind)"
            >
              <span class="cfd-pmenu__icon" :class="item.iconClass">{{ item.icon }}</span>
              {{ item.label }}
            </div>
          </div>
        </template>
      </div>
      <div v-if="node.kind === 'stage' && i === 0" class="cfd-connector"></div>

      <!-- stage 节点 -->
      <template v-if="node.kind === 'stage' && node.stageId">
        <FlowGraphNode
          :stage="stageById.get(node.stageId)!"
          :selected="selectedType === 'node' && selectedKey === node.stageId"
          :hit="hitSet.has(node.stageId)"
          :issue-count="issuesByKey.get(node.stageId)?.total"
          :error-count="issuesByKey.get(node.stageId)?.errors"
          @select="emit('select-node', node.stageId!)"
          @remove="askDeleteStage(node.stageId!)"
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
        <div class="cfd-branch-fork">
          <svg viewBox="0 0 1000 24" preserveAspectRatio="none" aria-hidden="true">
            <path v-for="(d, di) in forkPaths(node.branches?.length ?? 0)" :key="di" :d="d" fill="none" vector-effect="non-scaling-stroke" />
          </svg>
        </div>
        <div class="cfd-branch-cols">
          <div v-for="branch in node.branches" :key="branch.routeEdgeKey" class="cfd-branch-col">
            <div
              class="cfd-branch-head"
              :class="{ 'is-default': branch.isDefault, 'is-selected': selectedType === 'edge' && selectedKey === branch.routeEdgeKey }"
              role="button"
              tabindex="0"
              :aria-label="`分支 ${routeByEdge.get(branch.routeEdgeKey)?.routeName || ''}${branch.isDefault ? '（兜底）' : `，优先级 ${branch.priority}`}`"
              @click="emit('select-edge', branch.routeEdgeKey)"
              @keydown.enter.prevent="emit('select-edge', branch.routeEdgeKey)"
            >
              <!-- 悬浮操作工具条：浮在分支头上方，不占行内空间（标题不再被挤） -->
              <span v-if="!branch.isDefault" class="cfd-branch-head__tools" @click.stop>
                <button
                  class="cfd-branch-op"
                  :disabled="branchPosition(node, branch.routeEdgeKey).first"
                  title="左移（提升优先级）"
                  aria-label="左移分支"
                  @click.stop="handleReorderBranch(branch.routeEdgeKey, 'left')"
                ><LeftOutlined /></button>
                <button
                  class="cfd-branch-op"
                  :disabled="branchPosition(node, branch.routeEdgeKey).last"
                  title="右移（降低优先级）"
                  aria-label="右移分支"
                  @click.stop="handleReorderBranch(branch.routeEdgeKey, 'right')"
                ><RightOutlined /></button>
                <button class="cfd-branch-op" title="复制分支" aria-label="复制分支" @click.stop="handleCopyBranch(branch.routeEdgeKey)"><CopyOutlined /></button>
                <button class="cfd-branch-op is-danger" title="删除分支" aria-label="删除分支" @click.stop="askDeleteBranch(branch.routeEdgeKey)"><DeleteOutlined /></button>
              </span>
              <div class="cfd-branch-head__row">
                <span class="cfd-branch-head__name" :title="routeByEdge.get(branch.routeEdgeKey)?.routeName || ''">{{ routeByEdge.get(branch.routeEdgeKey)?.routeName || (branch.isDefault ? '其他情况' : '条件分支') }}</span>
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
            <div class="cfd-connector"></div>

            <!-- 支内子链（递归渲染子 stage；嵌套分支组降级为摘要） -->
            <template v-for="child in branch.children" :key="child.kind === 'stage' ? child.stageId : 'nested'">
              <template v-if="child.kind === 'stage' && child.stageId">
                <FlowGraphNode
                  class="cfd-graph__branchnode"
                  :stage="stageById.get(child.stageId)!"
                  :selected="selectedType === 'node' && selectedKey === child.stageId"
                  :hit="hitSet.has(child.stageId)"
                  :issue-count="issuesByKey.get(child.stageId)?.total"
                  :error-count="issuesByKey.get(child.stageId)?.errors"
                  @select="emit('select-node', child.stageId!)"
                  @remove="askDeleteStage(child.stageId!)"
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
                <div class="cfd-connector"></div>
              </template>
              <div v-else-if="child.kind === 'stageRef' && child.stageId" class="cfd-graph__stageref" role="note">
                ↳ 汇入「{{ stageById.get(child.stageId)?.name || child.stageId }}」
              </div>
              <div v-else-if="child.kind === 'branchGroup'" class="cfd-graph__nested">嵌套分支 · 点击分支头进入编辑</div>
            </template>
            <!-- 支尾连线：撑满列剩余高度，把本支末节点接到底部汇合线 -->
            <div class="cfd-branch-tail"></div>
          </div>
        </div>
        <div class="cfd-branch-merge">
          <svg viewBox="0 0 1000 24" preserveAspectRatio="none" aria-hidden="true">
            <path v-for="(d, di) in mergePaths(node.branches?.length ?? 0)" :key="di" :d="d" fill="none" vector-effect="non-scaling-stroke" />
          </svg>
        </div>
      </template>

      <!-- 顶层交叉引用（罕见：主干汇入他支已渲染节点） -->
      <div v-else-if="node.kind === 'stageRef' && node.stageId" class="cfd-graph__stageref" role="note">
        ↳ 汇入「{{ stageById.get(node.stageId)?.name || node.stageId }}」
      </div>
    </template>

    <!-- 复杂区段/孤儿提示 -->
    <div v-if="projection.complex.length" class="cfd-graph__notice is-warn">
      存在复杂连接（{{ projection.complex.map((id) => stageById.get(id)?.name || id).join('、') }}），竖向图仅展示一次，完整拓扑请切换「只读总览图」查看。
    </div>
    <div v-if="projection.orphans.length" class="cfd-graph__notice is-warn">
      {{ projection.orphans.length }} 个节点未被路由引用（{{ projection.orphans.map((id) => stageById.get(id)?.name || id).join('、') }}），已按排序追加显示；批次链/线性推进段属正常，其余请检查。
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

    <BranchDeleteConfirm
      :open="!!deleteTarget"
      :branch-name="deleteTarget?.name ?? ''"
      :stage-names="deleteTarget?.stageNames ?? []"
      @update:open="(v: boolean) => { if (!v) deleteTarget = null }"
      @confirm="confirmDeleteBranch"
    />
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

.cfd-graph__stageref {
  padding: 5px 10px;
  font-size: $font-size-sm;
  color: $text-secondary;
  background: $bg-page;
  border-radius: 12px;
}

.cfd-graph__startpop {
  max-width: 260px;
  font-size: $font-size-sm2;

  p { margin-bottom: 6px; }

  .cfd-graph__startpop-muted {
    font-size: $font-size-sm;
    color: $text-secondary;
  }
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
  &.is-cc { background: var(--color-flow-cc); }
  &.is-branch { background: var(--color-warning); }
  &.is-auto { background: var(--color-flow-auto); }
}

// 悬浮操作工具条：浮在分支头上方（不占行内空间，标题不被挤），随分支头 hover/focus 浮现
.cfd-branch-head__tools {
  position: absolute;
  top: -13px;
  right: 8px;
  z-index: 3;
  display: flex;
  gap: 2px;
  padding: 2px;
  visibility: hidden;
  background: $bg-card;
  border: 1px solid $border-color;
  border-radius: 7px;
  box-shadow: $shadow-card;
}

.cfd-branch-head:hover .cfd-branch-head__tools,
.cfd-branch-head:focus-within .cfd-branch-head__tools {
  visibility: visible;
}

.cfd-branch-op {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  padding: 0;
  font-size: 13px;
  color: $text-secondary;
  cursor: pointer;
  background: transparent;
  border: 0;
  border-radius: 5px;

  &:hover:not(:disabled) {
    color: var(--color-primary);
    background: $bg-page;
  }

  &.is-danger {
    color: $color-danger;

    &:hover:not(:disabled) {
      color: $text-on-accent;
      background: $color-danger;
    }
  }

  &:disabled {
    color: $text-placeholder;
    cursor: not-allowed;
  }
}
</style>
