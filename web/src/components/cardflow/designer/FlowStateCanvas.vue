<script setup lang="ts">
// SVG豁免区：vue-flow edge/handle stroke不解析var()，用真hex收口
const CANVAS_COLORS = {
  accent: '#E85E00',       // 选中边 → 橙化主色
  condition: '#2878a8',    // 条件分支边 → 图论语义蓝，保留
  defaultEdge: '#475569',  // 默认分支边 → 图论语义灰，保留
  muted: '#9aa7a1',        // 线性边/拖动标签 → 中性灰，保留
  labelBg: '#ffffff',      // edge label背景 → SVG fill必须真hex
  linearLabel: '#66736d',  // 线性边 label文字 → 中性绿灰，保留
}

import { computed, ref, watch } from 'vue'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import { Handle, Position, VueFlow } from '@vue-flow/core'
import type { Edge, Node, XYPosition } from '@vue-flow/core'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import type { DynamicStagePolicyRequest, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  selectedType?: 'node' | 'edge' | 'blank'
  selectedKey?: string | null
}>()

const emit = defineEmits<{
  (e: 'select-node', stageKey: string): void
  (e: 'select-edge', edgeKey: string): void
  (e: 'select-blank'): void
  (e: 'create-route', fromStageKey?: string): void
  (e: 'connect-route', payload: { fromStageKey: string; toStageKey: string }): void
  (e: 'reorder-stages', orderedStageKeys: string[]): void
}>()

const flowNodes = ref<Node[]>([])

const stageByKey = computed(() => {
  const map = new Map<string, StageDefinition>()
  props.stages.forEach(stage => map.set(stage.id, stage))
  return map
})

const routeCountBySource = computed(() => {
  const map = new Map<string, number>()
  props.routes.forEach(route => {
    if (!route.fromStageKey || route.status === 'disabled') return
    map.set(route.fromStageKey, (map.get(route.fromStageKey) || 0) + 1)
  })
  return map
})

const linearEdges = computed(() =>
  props.stages.slice(0, -1).map((stage, index) => {
    const target = props.stages[index + 1]
    return {
      id: `linear_${stage.id}_${target.id}`,
      source: stage.id,
      target: target.id,
      sourceHandle: 'next',
      targetHandle: 'in',
      type: 'smoothstep',
      label: '默认顺序',
      data: { kind: 'linear' },
      class: 'cf-flow-graph-edge cf-flow-graph-edge--linear',
      style: { stroke: CANVAS_COLORS.muted, strokeWidth: 2 },
      labelBgStyle: { fill: CANVAS_COLORS.labelBg, fillOpacity: 0.9 },
      labelStyle: { fill: CANVAS_COLORS.linearLabel, fontWeight: 600, fontSize: 12 },
    } satisfies Edge
  }),
)

const routeEdges = computed(() =>
  props.routes
    .filter(route => route.fromStageKey && route.toStageKey && stageByKey.value.has(route.fromStageKey) && stageByKey.value.has(route.toStageKey))
    .map(route => {
      const selected = props.selectedType === 'edge' && props.selectedKey === route.edgeKey
      const isDefault = route.isDefault
      const stroke = selected ? CANVAS_COLORS.accent : isDefault ? CANVAS_COLORS.defaultEdge : CANVAS_COLORS.condition
      return {
        id: route.edgeKey,
        source: route.fromStageKey,
        target: route.toStageKey,
        sourceHandle: isDefault ? 'next' : 'branch',
        targetHandle: 'in',
        type: 'smoothstep',
        animated: !isDefault,
        label: `${route.routeName || (isDefault ? '默认分支' : '条件分支')} · ${isDefault ? '默认分支' : `优先级 ${route.priority}`}`,
        data: { kind: isDefault ? 'default' : 'conditional' },
        class: [
          'cf-flow-graph-edge',
          isDefault ? 'cf-flow-graph-edge--default' : 'cf-flow-graph-edge--conditional',
          selected ? 'cf-flow-graph-edge--selected' : '',
        ].filter(Boolean).join(' '),
        style: { stroke, strokeWidth: selected ? 3 : 2.5 },
        labelBgStyle: { fill: CANVAS_COLORS.labelBg, fillOpacity: 0.94 },
        labelStyle: { fill: stroke, fontWeight: 700, fontSize: 12 },
      } satisfies Edge
    }),
)

const flowEdges = computed<Edge[]>(() => [...linearEdges.value, ...routeEdges.value])

const flowLayoutSignature = computed(() => [
  props.stages.map(stage => [
    stage.id,
    stage.name,
    stage.type,
    stage.processingGranularity || '',
    stage.sortOrder || '',
  ].join(':')).join('|'),
  props.dynamicPolicies.map(policy => [
    policy.policyKey,
    policy.sourceStageKey,
    policy.status || '',
  ].join(':')).join('|'),
  [...routeCountBySource.value.entries()].map(([stageKey, count]) => `${stageKey}:${count}`).join('|'),
].join('::'))

watch(
  () => flowLayoutSignature.value,
  () => syncFlowNodeLayout(),
  { immediate: true },
)

watch(
  () => [props.selectedType, props.selectedKey] as const,
  () => syncFlowNodeSelection(),
)

function syncFlowNodeLayout() {
  const previousPositions = new Map<string, XYPosition>()
  for (const node of flowNodes.value) {
    if (node.position) previousPositions.set(node.id, node.position)
  }
  flowNodes.value = props.stages.map<Node>((stage, index) => buildFlowNode(stage, index, previousPositions.get(stage.id)))
}

function syncFlowNodeSelection() {
  for (const node of flowNodes.value) {
    const stage = stageByKey.value.get(node.id)
    if (!stage) continue
    node.data = buildFlowNodeData(stage, Number(node.data?.index || 0))
  }
  flowNodes.value = flowNodes.value.slice()
}

function buildFlowNode(stage: StageDefinition, index: number, previousPosition?: XYPosition): Node {
  const preservedPosition = previousPosition || {
    x: 72 + (routeCountBySource.value.get(stage.id) ? 16 : 0),
    y: 48 + index * 142,
  }
  return {
    id: stage.id,
    type: 'stage',
    position: preservedPosition,
    draggable: true,
    selectable: true,
    data: buildFlowNodeData(stage, index),
  }
}

function buildFlowNodeData(stage: StageDefinition, index: number) {
  return {
    stage,
    index,
    stageTypeLabel: stageTypeLabel(stage),
    policyCount: stagePolicyCount(stage.id),
    routeCount: routeCountBySource.value.get(stage.id) || 0,
    selected: props.selectedType === 'node' && props.selectedKey === stage.id,
  }
}

function stageTypeLabel(stage: StageDefinition) {
  if (stage.type === 'manual') return '人工审批'
  return stage.processingGranularity === 'batch' ? '批次自动' : '自动处理'
}

function stagePolicyCount(stageKey: string) {
  return props.dynamicPolicies.filter(policy => policy.sourceStageKey === stageKey && policy.status !== 'disabled').length
}

function handleConnect(connection: { source?: string | null; target?: string | null }) {
  const source = connection.source
  const target = connection.target
  if (!source || !target || source === target) return
  emit('connect-route', { fromStageKey: source, toStageKey: target })
}

function handleNodeDragStop() {
  const orderedStageKeys = flowNodes.value
    .map<{ id: string; x: number; y: number }>(node => ({
      id: node.id,
      x: node.position?.x || 0,
      y: node.position?.y || 0,
    }))
    .filter(node => stageByKey.value.has(node.id))
    .sort((left, right) => {
      const yDelta = left.y - right.y
      if (Math.abs(yDelta) > 8) return yDelta
      return left.x - right.x
    })
    .map(node => node.id)
  const current = props.stages.map(stage => stage.id).join(',')
  const next = orderedStageKeys.join(',')
  if (orderedStageKeys.length === props.stages.length && next !== current) {
    emit('reorder-stages', orderedStageKeys)
  }
}

function handleNodeClick(...args: any[]) {
  const node = resolveFlowPayload(args, 'node')
  if (node?.id && stageByKey.value.has(node.id)) {
    emit('select-node', node.id)
  }
}

function handleEdgeClick(...args: any[]) {
  const edge = resolveFlowPayload(args, 'edge')
  if (!edge?.id) return
  if (String(edge.id).startsWith('linear_')) {
    emit('create-route', edge.source)
    return
  }
  emit('select-edge', edge.id)
}

function resolveFlowPayload(args: any[], key: 'node' | 'edge') {
  const wrapped = args.find(arg => arg?.[key])?.[key]
  if (wrapped) return wrapped
  return args.find(arg => arg?.id)
}
</script>

<template>
  <section class="cf-flow-canvas">
    <header class="cf-flow-canvas__head">
      <div>
        <strong>审批状态机画布</strong>
        <span>{{ stages.length }} 个节点 · {{ routes.length }} 条条件边 · {{ dynamicPolicies.length }} 个动态审批策略</span>
      </div>
      <div class="cf-flow-canvas__actions">
        <span>拖动节点调整主链顺序 · 从节点连接点拖出条件分支</span>
        <a-button size="small" type="primary" ghost @click="emit('create-route', stages[0]?.id)">
          添加条件边
        </a-button>
      </div>
    </header>

    <div v-if="stages.length === 0" class="cf-flow-canvas__empty">
      先在节点链中添加人工或自动节点
    </div>

    <div v-else class="cf-flow-canvas__graph">
      <VueFlow
        v-model:nodes="flowNodes"
        :edges="flowEdges"
        :default-viewport="{ zoom: 1, x: 0, y: 0 }"
        :min-zoom="0.55"
        :max-zoom="1.35"
        :snap-to-grid="true"
        :snap-grid="[12, 12]"
        fit-view-on-init
        class="cf-flow-vue"
        @connect="handleConnect"
        @node-click="handleNodeClick"
        @edge-click="handleEdgeClick"
        @node-drag-stop="handleNodeDragStop"
        @pane-click="emit('select-blank')"
      >
        <Background />
        <Controls />
        <MiniMap />

        <template #node-stage="nodeProps">
          <article
            class="cf-flow-node"
            :class="[
              `cf-flow-node--${nodeProps.data.stage.type}`,
              nodeProps.data.selected ? 'cf-flow-node--selected' : '',
              nodeProps.data.routeCount ? 'cf-flow-node--branch-source' : '',
            ]"
            role="button"
            tabindex="0"
            @click.stop="emit('select-node', nodeProps.id)"
            @keyup.enter.stop="emit('select-node', nodeProps.id)"
          >
            <Handle id="in" type="target" :position="Position.Top" />
            <div class="cf-flow-node__stripe" />
            <div class="cf-flow-node__body">
              <span class="cf-flow-node__index">{{ nodeProps.data.index + 1 }}</span>
              <div class="cf-flow-node__main">
                <strong>{{ nodeProps.data.stage.name || '未命名节点' }}</strong>
                <small>{{ nodeProps.data.stageTypeLabel }}</small>
              </div>
              <div class="cf-flow-node__badges">
                <span v-if="nodeProps.data.policyCount" class="cf-flow-node__badge cf-flow-node__badge--policy">
                  动态审批 {{ nodeProps.data.policyCount }}
                </span>
                <span v-if="nodeProps.data.routeCount" class="cf-flow-node__badge cf-flow-node__badge--route">
                  条件分支 {{ nodeProps.data.routeCount }}
                </span>
              </div>
            </div>
            <span class="cf-flow-node__drag">拖动排序</span>
            <Handle id="next" type="source" :position="Position.Bottom" />
            <Handle id="branch" class="cf-flow-node__branch-handle" type="source" :position="Position.Right" />
            <span class="cf-flow-node__branch-label">条件</span>
          </article>
        </template>
      </VueFlow>

      <aside class="cf-flow-canvas__legend" aria-label="流程图图例">
        <span><i class="cf-flow-canvas__dot cf-flow-canvas__dot--linear" />默认顺序</span>
        <span><i class="cf-flow-canvas__dot cf-flow-canvas__dot--conditional" />条件分支</span>
        <span><i class="cf-flow-canvas__dot cf-flow-canvas__dot--policy" />动态审批</span>
      </aside>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-flow-canvas {
  display: flex;
  flex-direction: column;
  min-height: 620px;
  overflow: hidden;
  border: 1px solid var(--border);
  background: var(--bg-page);
}

.cf-flow-canvas__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
  background: color-mix(in srgb, var(--bg-card) 96%, transparent);

  strong {
    display: block;
    color: var(--text-1);
    font-size: 15px;
  }

  span {
    display: block;
    margin-top: 4px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-flow-canvas__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;

  span {
    color: var(--text-2);
    font-size: 12px;
    white-space: nowrap;
  }
}

.cf-flow-canvas__empty {
  display: grid;
  place-items: center;
  min-height: 520px;
  color: var(--text-3);
  font-size: 13px;
}

.cf-flow-canvas__graph {
  position: relative;
  flex: 1;
  min-height: 560px;
}

.cf-flow-vue {
  width: 100%;
  height: 100%;
  min-height: 560px;
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    linear-gradient(180deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    var(--bg-page);
  background-size: 28px 28px;
}

.cf-flow-node {
  position: relative;
  width: 340px;
  min-height: 96px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  box-shadow: var(--shadow-sm);
  cursor: grab;
  transition: border-color .16s ease, box-shadow .16s ease, transform .16s ease;

  &:focus-visible,
  &:hover {
    border-color: var(--color-primary);
    box-shadow: var(--shadow-md);
    outline: none;
  }

  &:active {
    cursor: grabbing;
  }
}

.cf-flow-node--selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 16%, transparent), var(--shadow-md);
}

.cf-flow-node__stripe {
  position: absolute;
  inset: 0 auto 0 0;
  width: 5px;
  border-radius: 8px 0 0 8px;
  background: var(--color-info);
}

.cf-flow-node--auto .cf-flow-node__stripe {
  background: var(--color-warning);
}

.cf-flow-node__body {
  display: grid;
  grid-template-columns: 38px minmax(0, 1fr);
  gap: 12px;
  padding: 16px 18px 12px 20px;
}

.cf-flow-node__index {
  display: inline-grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border-radius: 50%;
  background: var(--color-primary-light);
  color: var(--color-primary);
  font-size: 13px;
  font-weight: 800;
}

.cf-flow-node__main {
  min-width: 0;

  strong,
  small {
    display: block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  strong {
    color: var(--text-1);
    font-size: 15px;
    line-height: 22px;
  }

  small {
    margin-top: 3px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-flow-node__badges {
  grid-column: 2;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  min-height: 24px;
}

.cf-flow-node__badge {
  display: inline-flex;
  align-items: center;
  max-width: 100%;
  height: 22px;
  padding: 0 8px;
  border-radius: 999px;
  font-size: 12px;
  line-height: 22px;
  white-space: nowrap;
}

.cf-flow-node__badge--policy {
  background: var(--color-warning-light);
  color: var(--color-warning-text);
}

.cf-flow-node__badge--route {
  background: var(--color-info-light);
  color: var(--color-info);
}

.cf-flow-node__drag {
  position: absolute;
  right: 14px;
  bottom: 10px;
  color: var(--text-3);
  font-size: 12px;
}

.cf-flow-node__branch-label {
  position: absolute;
  top: 34px;
  right: -34px;
  color: var(--color-info);
  font-size: 12px;
  font-weight: 700;
}

:deep(.cf-flow-node__branch-handle) {
  width: 11px;
  height: 11px;
  border: 2px solid var(--bg-card);
  background: var(--color-info);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-info) 28%, transparent);
}

:deep(.vue-flow__handle) {
  width: 10px;
  height: 10px;
  border: 2px solid var(--bg-card);
  background: var(--color-primary);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 22%, transparent);
}

:deep(.cf-flow-graph-edge--linear .vue-flow__edge-path) {
  stroke-dasharray: 6 5;
}

:deep(.cf-flow-graph-edge--conditional .vue-flow__edge-path) {
  stroke: var(--color-info);
}

:deep(.cf-flow-graph-edge--default .vue-flow__edge-path) {
  stroke: var(--text-2);
}

:deep(.cf-flow-graph-edge--selected .vue-flow__edge-path) {
  filter: drop-shadow(0 3px 5px color-mix(in srgb, var(--color-primary) 25%, transparent));
}

:deep(.vue-flow__edge-textbg) {
  rx: 5px;
  ry: 5px;
}

.cf-flow-canvas__legend {
  position: absolute;
  right: 14px;
  bottom: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 10px;
  border: 1px solid color-mix(in srgb, var(--border) 90%, transparent);
  border-radius: 8px;
  background: color-mix(in srgb, var(--bg-card) 92%, transparent);
  box-shadow: var(--shadow-sm);
  color: var(--text-2);
  font-size: 12px;
}

.cf-flow-canvas__legend span {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.cf-flow-canvas__dot {
  display: inline-block;
  width: 9px;
  height: 9px;
  border-radius: 50%;
}

.cf-flow-canvas__dot--linear {
  background: var(--text-3);
}

.cf-flow-canvas__dot--conditional {
  background: var(--color-info);
}

.cf-flow-canvas__dot--policy {
  background: var(--color-warning);
}

@media (max-width: 900px) {
  .cf-flow-canvas__head,
  .cf-flow-canvas__actions {
    align-items: flex-start;
    flex-direction: column;
  }

  .cf-flow-node {
    width: 300px;
  }

  .cf-flow-canvas__legend {
    left: 12px;
    right: auto;
    flex-wrap: wrap;
  }
}
</style>
