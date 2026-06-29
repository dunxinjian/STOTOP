<script setup lang="ts">
/**
 * OrchestrationDetailPage.vue — 编排模板详情 / DAG 编辑
 *
 * 节点和边采用表格形式增删改，支持 JSON 高级模式直接编辑原始 JSON。
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  SaveOutlined,
  PlusOutlined,
  DeleteOutlined,
  CodeOutlined,
  ArrowLeftOutlined,
  SendOutlined,
  PauseCircleOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import PageLayout from '@/components/PageLayout.vue'
import {
  getOrchestrationTemplate,
  updateOrchestrationTemplate,
  publishOrchestrationTemplate,
  disableOrchestrationTemplate,
} from '@/api/orchestration'
import type {
  OrchestrationTemplate,
  OrchestrationTemplateStatus,
  DagNode,
  DagNodeType,
  DagEdge,
  DagDataProtocolLevel,
} from '@/types/orchestration'

const route = useRoute()
const router = useRouter()

const templateId = computed(() => Number(route.params.id))

// ==================== 状态 ====================

const loading = ref(false)
const saving = ref(false)
const template = ref<OrchestrationTemplate | null>(null)

const nodes = ref<DagNode[]>([])
const edges = ref<DagEdge[]>([])

const advancedMode = ref(false)
const nodesJsonText = ref('')
const edgesJsonText = ref('')

const STATUS_META: Record<OrchestrationTemplateStatus, { text: string; color: string }> = {
  draft: { text: '草稿', color: '#8c8c8c' },
  published: { text: '已发布', color: 'var(--color-success)' },
  disabled: { text: '已停用', color: 'var(--color-warning)' },
}

const NODE_TYPE_OPTIONS: Array<{ label: string; value: DagNodeType }> = [
  { label: '开始 (start)', value: 'start' },
  { label: '卡片流程 (cardflow)', value: 'cardflow' },
  { label: '汇聚 (join)', value: 'join' },
  { label: '结束 (end)', value: 'end' },
]
const COMPLETION_MODE_OPTIONS = [
  { label: '单卡 (single)', value: 'single' },
  { label: '批次 (batch)', value: 'batch' },
]
const JOIN_MODE_OPTIONS = [
  { label: '全部 (all)', value: 'all' },
  { label: '任一 (any)', value: 'any' },
]
const PROTOCOL_LEVEL_OPTIONS: Array<{ label: string; value: DagDataProtocolLevel }> = [
  { label: '信号 (signal)', value: 'signal' },
  { label: '内联 (inline)', value: 'inline' },
  { label: '引用 (ref)', value: 'ref' },
]

const COND_OP_OPTIONS = [
  { label: '==', value: '==' },
  { label: '!=', value: '!=' },
  { label: '>', value: '>' },
  { label: '<', value: '<' },
  { label: '>=', value: '>=' },
  { label: '<=', value: '<=' },
  { label: 'contains', value: 'contains' },
]

// ==================== DAG 拓扑预览：几何 / 配色 ====================
const DAG_NODE_W = 168
const DAG_NODE_H = 46
const DAG_COL_W = 230
const DAG_ROW_H = 70
const DAG_PAD = 24
const DAG_NODE_COLOR: Record<DagNodeType, string> = {
  start: 'var(--color-info)',
  cardflow: 'var(--color-success)',
  join: 'var(--color-warning)',
  end: '#8c8c8c',
}

// ==================== 列定义 ====================

const nodeColumns: TableColumnsType = [
  { title: '节点 ID', dataIndex: 'id', key: 'id', width: 160 },
  { title: '类型', dataIndex: 'type', key: 'type', width: 180 },
  { title: '名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '关联流程编码', dataIndex: 'flowCode', key: 'flowCode', width: 200 },
  { title: '完成模式', dataIndex: 'completionMode', key: 'completionMode', width: 140 },
  { title: '汇聚模式', dataIndex: 'joinMode', key: 'joinMode', width: 140 },
  { title: '操作', key: 'action', width: 80, fixed: 'right' },
]

const edgeColumns: TableColumnsType = [
  { title: '边 ID', dataIndex: 'id', key: 'id', width: 130 },
  { title: '起点', key: 'from', width: 200 },
  { title: '终点', key: 'to', width: 200 },
  { title: '条件（字段 / 操作 / 值，留空=无条件）', key: 'condition', width: 360 },
  { title: '协议级别', key: 'protocolLevel', width: 130 },
  { title: '数据传递', key: 'mapping', width: 320 },
  { title: '操作', key: 'action', width: 80, fixed: 'right' },
]

// ==================== 边表格行模型（结构化字段） ====================

interface MappingEntry {
  /** 目标字段名（写入下游卡片表单） */
  key: string
  /** 源 JSONPath（从上游数据中取值） */
  value: string
}

interface EdgeRow {
  id: string
  from: string
  to: string
  // 条件三元组（字段为空表示无条件）
  condField: string
  condOp: string
  condValue: string
  // 数据协议
  protocolLevel: DagDataProtocolLevel
  // inline 模式：字段映射条目
  mappingEntries: MappingEntry[]
  // ref 模式：表名 + 过滤表达式
  refTable: string
  refFilterExpr: string
}

const edgeRows = ref<EdgeRow[]>([])

/** 卡片折叠状态（仅影响展示，不影响保存数据） */
const collapsed = reactive({
  preview: false,
  nodes: false,
  edges: false,
})

/** 节点下拉选项：显示「名称（ID）」，避免用户直接面对裸 nodeId */
const nodeOptions = computed(() =>
  nodes.value
    .filter(n => !!n.id)
    .map(n => ({
      label: `${n.name || '(未命名)'}（${n.id}）`,
      value: n.id,
    }))
)

function filterNodeOption(input: string, opt: any) {
  const label = String(opt?.label ?? opt?.value ?? '').toLowerCase()
  return label.includes(String(input || '').toLowerCase())
}

// ==================== DAG 拓扑预览：分层布局 / SVG 路径 ====================

/** 当前节点 ID 集合，用于检测边的游离引用 */
const nodeIdSet = computed(() => new Set(nodes.value.filter(n => !!n.id).map(n => n.id)))

/** 检查给定节点 ID 是否在节点列表中缺失（用于边表起点/终点的红框提示） */
function isMissingNode(id?: string): boolean {
  if (!id) return false
  return !nodeIdSet.value.has(id)
}

/** Kahn 拓扑分层（环路兜底：剩余节点全部塞进最后一层） */
const dagLayers = computed<DagNode[][]>(() => {
  const ns = nodes.value.filter(n => !!n.id)
  if (ns.length === 0) return []
  const idSet = new Set(ns.map(n => n.id))
  const inDeg = new Map<string, number>()
  ns.forEach(n => inDeg.set(n.id, 0))
  for (const r of edgeRows.value) {
    if (idSet.has(r.from) && idSet.has(r.to)) {
      inDeg.set(r.to, (inDeg.get(r.to) || 0) + 1)
    }
  }
  const layers: DagNode[][] = []
  const placed = new Set<string>()
  let guard = 0
  while (placed.size < ns.length && guard < 64) {
    guard++
    const layer = ns.filter(n => !placed.has(n.id) && (inDeg.get(n.id) || 0) === 0)
    if (layer.length === 0) {
      layers.push(ns.filter(n => !placed.has(n.id)))
      break
    }
    layers.push(layer)
    layer.forEach(n => placed.add(n.id))
    for (const r of edgeRows.value) {
      if (layer.some(n => n.id === r.from) && idSet.has(r.to) && !placed.has(r.to)) {
        inDeg.set(r.to, (inDeg.get(r.to) || 0) - 1)
      }
    }
  }
  return layers
})

interface DagNodePos {
  x: number
  y: number
  node: DagNode
}

/** 节点坐标表（id -> {x,y,node}） */
const dagNodePos = computed<Map<string, DagNodePos>>(() => {
  const map = new Map<string, DagNodePos>()
  dagLayers.value.forEach((layer, ci) => {
    layer.forEach((n, ri) => {
      map.set(n.id, {
        x: DAG_PAD + ci * DAG_COL_W,
        y: DAG_PAD + ri * DAG_ROW_H,
        node: n,
      })
    })
  })
  return map
})

/** SVG 画布尺寸 */
const dagViewBox = computed(() => {
  const cols = Math.max(dagLayers.value.length, 1)
  const maxRows = Math.max(1, ...dagLayers.value.map(l => l.length))
  const w = DAG_PAD * 2 + (cols - 1) * DAG_COL_W + DAG_NODE_W
  const h = DAG_PAD * 2 + (maxRows - 1) * DAG_ROW_H + DAG_NODE_H
  return { w: Math.max(w, 320), h: Math.max(h, 100) }
})

interface DagSvgEdge {
  id: string
  path: string
  midX: number
  midY: number
  label: string
}

/** SVG 边路径列表（仅渲染起终点都可定位的边） */
const dagSvgEdges = computed<DagSvgEdge[]>(() => {
  const out: DagSvgEdge[] = []
  for (const r of edgeRows.value) {
    const a = dagNodePos.value.get(r.from)
    const b = dagNodePos.value.get(r.to)
    if (!a || !b) continue
    const x1 = a.x + DAG_NODE_W
    const y1 = a.y + DAG_NODE_H / 2
    const x2 = b.x
    const y2 = b.y + DAG_NODE_H / 2
    const dx = Math.max(40, Math.abs(x2 - x1) / 3)
    const path = `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`
    const label = r.condField
      ? `${r.condField} ${r.condOp || '=='} ${r.condValue ?? ''}`.trim()
      : ''
    out.push({
      id: r.id,
      path,
      midX: (x1 + x2) / 2,
      midY: (y1 + y2) / 2 - 4,
      label,
    })
  }
  return out
})

/** 节点矩形配色 */
function dagNodeColor(t: DagNodeType): string {
  return DAG_NODE_COLOR[t] || '#8c8c8c'
}

// ==================== 锚点跳转 ====================
function scrollToSection(id: string) {
  const el = document.getElementById(id)
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function edgeToRow(e: DagEdge): EdgeRow {
  const cond = e.condition
  const lv = e.dataProtocol?.level ?? 'signal'
  let mappingEntries: MappingEntry[] = []
  let refTable = ''
  let refFilterExpr = ''
  if (lv === 'inline' && e.dataProtocol?.mapping) {
    mappingEntries = Object.entries(e.dataProtocol.mapping).map(([k, v]) => ({
      key: k,
      value: String(v ?? ''),
    }))
  }
  if (lv === 'ref' && e.dataProtocol?.ref) {
    refTable = e.dataProtocol.ref.table || ''
    refFilterExpr = e.dataProtocol.ref.filterExpr || ''
  }
  return {
    id: e.id,
    from: e.from,
    to: e.to,
    condField: cond?.field ?? '',
    condOp: cond?.op ?? '==',
    condValue: cond && cond.value !== undefined && cond.value !== null ? String(cond.value) : '',
    protocolLevel: lv,
    mappingEntries,
    refTable,
    refFilterExpr,
  }
}

function rowToEdge(r: EdgeRow): DagEdge {
  const edge: DagEdge = { id: r.id, from: r.from, to: r.to }
  const fld = (r.condField || '').trim()
  if (fld) {
    edge.condition = {
      field: fld,
      op: r.condOp || '==',
      value: r.condValue,
    }
  }
  if (r.protocolLevel === 'signal') {
    edge.dataProtocol = { level: 'signal' }
  } else if (r.protocolLevel === 'inline') {
    const mapping: Record<string, string> = {}
    for (const m of r.mappingEntries) {
      const k = (m.key || '').trim()
      const v = (m.value || '').trim()
      if (k && v) mapping[k] = v
    }
    edge.dataProtocol = Object.keys(mapping).length
      ? { level: 'inline', mapping }
      : { level: 'inline' }
  } else if (r.protocolLevel === 'ref') {
    const t = (r.refTable || '').trim()
    const f = (r.refFilterExpr || '').trim()
    if (t || f) {
      edge.dataProtocol = { level: 'ref', ref: { table: t, filterExpr: f } }
    } else {
      edge.dataProtocol = { level: 'ref' } as any
    }
  } else {
    edge.dataProtocol = { level: r.protocolLevel }
  }
  return edge
}

/** inline 映射的紧凑摘要（表格内显示） */
function summaryMapping(r: { mappingEntries?: MappingEntry[] } | any): string {
  const entries: MappingEntry[] = Array.isArray(r?.mappingEntries) ? r.mappingEntries : []
  if (!entries.length) return '尚未配置映射'
  const head = entries
    .slice(0, 2)
    .map(m => `${m.key || '?'} ← ${m.value || '?'}`)
    .join('，')
  return entries.length > 2 ? `${head} … 共 ${entries.length} 项` : head
}

// ==================== 映射编辑弹窗状态 ====================

const mappingModalVisible = ref(false)
const mappingEditingIndex = ref(-1)
const mappingDraft = ref<MappingEntry[]>([])

function openMappingEditor(idx: number) {
  mappingEditingIndex.value = idx
  const src = edgeRows.value[idx]?.mappingEntries ?? []
  mappingDraft.value = src.map(m => ({ key: m.key, value: m.value }))
  if (mappingDraft.value.length === 0) {
    mappingDraft.value.push({ key: '', value: '' })
  }
  mappingModalVisible.value = true
}

function addMappingEntry() {
  mappingDraft.value.push({ key: '', value: '' })
}

function removeMappingEntry(i: number) {
  mappingDraft.value.splice(i, 1)
}

function saveMappingEditor() {
  const cleaned = mappingDraft.value.filter(
    m => (m.key || '').trim() && (m.value || '').trim()
  )
  const idx = mappingEditingIndex.value
  if (idx >= 0 && idx < edgeRows.value.length) {
    edgeRows.value[idx].mappingEntries = cleaned
  }
  mappingModalVisible.value = false
}

// ==================== 加载 ====================

async function loadDetail() {
  loading.value = true
  try {
    const data = await getOrchestrationTemplate(templateId.value)
    template.value = data
    nodes.value = parseJsonArray<DagNode>(data.nodesJson, 'nodes')
    edges.value = parseJsonArray<DagEdge>(data.edgesJson, 'edges')
    edgeRows.value = edges.value.map(edgeToRow)
    nodesJsonText.value = formatJson(nodes.value)
    edgesJsonText.value = formatJson(edges.value)
  } catch (err: any) {
    message.error(err?.response?.data?.message || '加载编排模板失败')
  } finally {
    loading.value = false
  }
}

function parseJsonArray<T>(raw?: string, key?: 'nodes' | 'edges'): T[] {
  if (!raw) return []
  try {
    const v = JSON.parse(raw)
    // 1) 纯数组格式：[ {...}, {...} ]（前端保存格式）
    if (Array.isArray(v)) return v as T[]
    // 2) 包装对象格式：{ "nodes": [...] } / { "edges": [...] }（Seeder 预置格式）
    if (v && typeof v === 'object') {
      if (key && Array.isArray((v as any)[key])) return (v as any)[key] as T[]
      if (Array.isArray((v as any).nodes)) return (v as any).nodes as T[]
      if (Array.isArray((v as any).edges)) return (v as any).edges as T[]
    }
    return []
  } catch {
    return []
  }
}

function formatJson(v: unknown) {
  return JSON.stringify(v ?? [], null, 2)
}

// ==================== 节点 / 边 表格操作 ====================

function addNode() {
  const nextIdx = nodes.value.length + 1
  nodes.value.push({
    id: `node_${nextIdx}`,
    type: 'cardflow',
    name: `节点 ${nextIdx}`,
    flowCode: '',
    completionMode: 'single',
  })
}

function removeNode(idx: number) {
  nodes.value.splice(idx, 1)
}

function addEdge() {
  const nextIdx = edgeRows.value.length + 1
  edgeRows.value.push({
    id: `edge_${nextIdx}`,
    from: '',
    to: '',
    condField: '',
    condOp: '==',
    condValue: '',
    protocolLevel: 'signal',
    mappingEntries: [],
    refTable: '',
    refFilterExpr: '',
  })
}

function removeEdge(idx: number) {
  edgeRows.value.splice(idx, 1)
}

// ==================== JSON 高级模式 ====================

function toggleAdvanced() {
  if (!advancedMode.value) {
    // 进入高级模式：先把当前表格状态序列化
    nodesJsonText.value = formatJson(nodes.value)
    try {
      const built = edgeRows.value.map(rowToEdge)
      edgesJsonText.value = formatJson(built)
    } catch (err: any) {
      message.warning(err?.message || '边配置存在 JSON 错误，已尝试保留原文')
      edgesJsonText.value = formatJson(edges.value)
    }
  } else {
    // 退出高级模式：把 JSON 反序列化回表格
    try {
      const ns = JSON.parse(nodesJsonText.value || '[]')
      const es = JSON.parse(edgesJsonText.value || '[]')
      if (!Array.isArray(ns) || !Array.isArray(es)) throw new Error('节点和边必须是数组')
      nodes.value = ns
      edges.value = es
      edgeRows.value = edges.value.map(edgeToRow)
    } catch (err: any) {
      message.error(`JSON 解析失败：${err?.message || '格式错误'}`)
      return
    }
  }
  advancedMode.value = !advancedMode.value
}

// ==================== 保存 ====================

async function saveAll() {
  if (!template.value) return
  let nodesJson: string
  let edgesJson: string
  try {
    if (advancedMode.value) {
      // 校验 JSON
      const ns = JSON.parse(nodesJsonText.value || '[]')
      const es = JSON.parse(edgesJsonText.value || '[]')
      if (!Array.isArray(ns) || !Array.isArray(es)) {
        throw new Error('节点和边必须是数组')
      }
      nodesJson = JSON.stringify(ns)
      edgesJson = JSON.stringify(es)
    } else {
      const builtEdges = edgeRows.value.map(rowToEdge)
      nodesJson = JSON.stringify(nodes.value)
      edgesJson = JSON.stringify(builtEdges)
    }
  } catch (err: any) {
    message.error(err?.message || '配置存在错误')
    return
  }

  saving.value = true
  try {
    await updateOrchestrationTemplate(template.value.id, {
      name: template.value.name,
      description: template.value.description,
      maxTriggerCount: template.value.maxTriggerCount,
      nodesJson,
      edgesJson,
    })
    message.success('保存成功')
    loadDetail()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// ==================== 状态切换 ====================

async function handlePublish() {
  if (!template.value) return
  try {
    await publishOrchestrationTemplate(template.value.id)
    message.success('已发布')
    loadDetail()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '发布失败')
  }
}

function handleDisable() {
  if (!template.value) return
  Modal.confirm({
    title: '停用此编排模板？',
    content: '停用后无法发起新实例。',
    okText: '停用',
    okType: 'danger',
    async onOk() {
      try {
        await disableOrchestrationTemplate(template.value!.id)
        message.success('已停用')
        loadDetail()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '停用失败')
      }
    },
  })
}

function goBack() {
  router.push({ path: '/cardflow/orchestrations' })
}

function formatTime(val?: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN')
}

onMounted(loadDetail)
</script>

<template>
  <PageLayout variant="flow">
    <PageHeader>
      <template #left>
        <a-button type="text" size="small" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
          返回列表
        </a-button>
      </template>

      <template #actions>
        <a-space :size="8">
          <a-button @click="toggleAdvanced">
            <template #icon><CodeOutlined /></template>
            {{ advancedMode ? '退出高级模式' : '高级模式 (JSON)' }}
          </a-button>
          <a-button
            v-if="template?.status === 'draft'"
            @click="handlePublish"
          >
            <template #icon><SendOutlined /></template>
            发布
          </a-button>
          <a-button
            v-if="template?.status === 'published'"
            danger
            @click="handleDisable"
          >
            <template #icon><PauseCircleOutlined /></template>
            停用
          </a-button>
          <a-button type="primary" :loading="saving" @click="saveAll">
            <template #icon><SaveOutlined /></template>
            保存
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 基本信息 -->
      <a-card v-if="template" class="info-card" :bordered="false">
        <a-descriptions :column="3" size="small">
          <a-descriptions-item label="编码">{{ template.code }}</a-descriptions-item>
          <a-descriptions-item label="名称">
            <a-input v-model:value="template.name" size="small" />
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag class="status-tag" :color="STATUS_META[template.status]?.color">
              {{ STATUS_META[template.status]?.text }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="最大触发次数">
            <a-input-number
              v-model:value="template.maxTriggerCount"
              :min="1"
              :max="100000"
              size="small"
              style="width: 120px"
            />
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatTime(template.createdTime) }}</a-descriptions-item>
          <a-descriptions-item label="更新时间">{{ formatTime(template.updatedTime) }}</a-descriptions-item>
          <a-descriptions-item label="描述" :span="3">
            <a-textarea v-model:value="template.description" :rows="2" />
          </a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 表格模式 -->
      <template v-if="!advancedMode">
        <!-- DAG 拓扑预览 -->
        <a-card id="dag-preview" class="section-card dag-preview-card" :bordered="false">
          <template #title>
            <span>DAG 拓扑预览（{{ nodes.length }} 节点 · {{ edgeRows.length }} 边）</span>
          </template>
          <template #extra>
            <a-button size="small" type="text" @click="collapsed.preview = !collapsed.preview">
              {{ collapsed.preview ? '展开' : '收起' }}
            </a-button>
          </template>

          <div v-show="!collapsed.preview" class="dag-svg-wrap">
            <div v-if="dagLayers.length === 0" class="dag-empty">
              尚无节点，请在下方「DAG 节点」表中新增。
            </div>
            <svg
              v-else
              :viewBox="`0 0 ${dagViewBox.w} ${dagViewBox.h}`"
              :width="dagViewBox.w"
              :height="dagViewBox.h"
              class="dag-svg"
            >
              <defs>
                <marker
                  id="dag-arrow"
                  viewBox="0 0 10 10"
                  refX="9"
                  refY="5"
                  markerWidth="8"
                  markerHeight="8"
                  orient="auto-start-reverse"
                >
                  <path d="M 0 0 L 10 5 L 0 10 z" fill="#8c8c8c" />
                </marker>
              </defs>
              <g class="dag-edges">
                <g v-for="e in dagSvgEdges" :key="e.id">
                  <path
                    :d="e.path"
                    fill="none"
                    stroke="#8c8c8c"
                    stroke-width="1.5"
                    marker-end="url(#dag-arrow)"
                  />
                  <text
                    v-if="e.label"
                    :x="e.midX"
                    :y="e.midY"
                    text-anchor="middle"
                    class="dag-edge-label"
                  >
                    {{ e.label }}
                  </text>
                </g>
              </g>
              <g class="dag-nodes">
                <g
                  v-for="[id, p] in dagNodePos"
                  :key="id"
                  :transform="`translate(${p.x}, ${p.y})`"
                >
                  <rect
                    :width="DAG_NODE_W"
                    :height="DAG_NODE_H"
                    rx="6"
                    :fill="dagNodeColor(p.node.type)"
                    opacity="0.92"
                  />
                  <text
                    :x="DAG_NODE_W / 2"
                    :y="DAG_NODE_H / 2 - 2"
                    text-anchor="middle"
                    class="dag-node-name"
                  >
                    {{ p.node.name || p.node.id }}
                  </text>
                  <text
                    :x="DAG_NODE_W / 2"
                    :y="DAG_NODE_H / 2 + 13"
                    text-anchor="middle"
                    class="dag-node-meta"
                  >
                    {{ p.node.id }} · {{ p.node.type }}
                  </text>
                </g>
              </g>
            </svg>
          </div>
        </a-card>

        <!-- 节点表格 -->
        <a-card id="dag-nodes" class="section-card" :bordered="false">
          <template #title>
            <span>DAG 节点（{{ nodes.length }}）</span>
          </template>
          <template #extra>
            <a-space :size="6">
              <a-button size="small" type="primary" @click="addNode">
                <template #icon><PlusOutlined /></template>
                新增节点
              </a-button>
              <a-button size="small" type="text" @click="collapsed.nodes = !collapsed.nodes">
                {{ collapsed.nodes ? '展开' : '收起' }}
              </a-button>
            </a-space>
          </template>

          <a-table
            v-show="!collapsed.nodes"
            :columns="nodeColumns"
            :data-source="nodes"
            :pagination="false"
            :row-key="(_: unknown, idx?: number) => `node-${idx ?? 0}`"
            size="middle"
            :scroll="{ x: 1100 }"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'id'">
                <a-input v-model:value="record.id" size="small" placeholder="节点 ID" />
              </template>
              <template v-else-if="column.key === 'type'">
                <a-select
                  v-model:value="record.type"
                  :options="NODE_TYPE_OPTIONS"
                  size="small"
                  style="width: 100%"
                />
              </template>
              <template v-else-if="column.key === 'name'">
                <a-input v-model:value="record.name" size="small" placeholder="节点名称" />
              </template>
              <template v-else-if="column.key === 'flowCode'">
                <a-input
                  v-model:value="record.flowCode"
                  size="small"
                  :placeholder="record.type === 'cardflow' ? '关联 FlowCode（必填）' : '不适用'"
                  :disabled="record.type !== 'cardflow'"
                />
              </template>
              <template v-else-if="column.key === 'completionMode'">
                <a-select
                  v-model:value="record.completionMode"
                  :options="COMPLETION_MODE_OPTIONS"
                  size="small"
                  style="width: 100%"
                  allow-clear
                  :disabled="record.type !== 'cardflow'"
                />
              </template>
              <template v-else-if="column.key === 'joinMode'">
                <a-select
                  v-model:value="record.joinMode"
                  :options="JOIN_MODE_OPTIONS"
                  size="small"
                  style="width: 100%"
                  allow-clear
                  :disabled="record.type !== 'join'"
                />
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small" danger @click="removeNode(index!)">
                  <DeleteOutlined />
                </a-button>
              </template>
            </template>
          </a-table>
        </a-card>

        <!-- 边表格 -->
        <a-card id="dag-edges" class="section-card" :bordered="false">
          <template #title>
            <span>DAG 边（{{ edgeRows.length }}）</span>
          </template>
          <template #extra>
            <a-space :size="6">
              <a-button size="small" type="primary" @click="addEdge">
                <template #icon><PlusOutlined /></template>
                新增边
              </a-button>
              <a-button size="small" type="text" @click="collapsed.edges = !collapsed.edges">
                {{ collapsed.edges ? '展开' : '收起' }}
              </a-button>
            </a-space>
          </template>

          <a-table
            v-show="!collapsed.edges"
            :columns="edgeColumns"
            :data-source="edgeRows"
            :pagination="false"
            :row-key="(_: unknown, idx?: number) => `edge-${idx ?? 0}`"
            size="middle"
            :scroll="{ x: 1280 }"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'id'">
                <a-input v-model:value="record.id" size="small" />
              </template>
              <template v-else-if="column.key === 'from'">
                <a-tooltip
                  v-if="isMissingNode(record.from)"
                  title="该节点 ID 在节点列表中不存在（游离引用），请重新选择"
                  color="var(--color-danger)"
                >
                  <div class="edge-ref-broken">
                    <a-select
                      v-model:value="record.from"
                      :options="nodeOptions"
                      size="small"
                      show-search
                      :filter-option="filterNodeOption"
                      placeholder="选择起点节点"
                      style="width: 100%"
                    />
                  </div>
                </a-tooltip>
                <a-select
                  v-else
                  v-model:value="record.from"
                  :options="nodeOptions"
                  size="small"
                  show-search
                  :filter-option="filterNodeOption"
                  placeholder="选择起点节点"
                  style="width: 100%"
                />
              </template>
              <template v-else-if="column.key === 'to'">
                <a-tooltip
                  v-if="isMissingNode(record.to)"
                  title="该节点 ID 在节点列表中不存在（游离引用），请重新选择"
                  color="var(--color-danger)"
                >
                  <div class="edge-ref-broken">
                    <a-select
                      v-model:value="record.to"
                      :options="nodeOptions"
                      size="small"
                      show-search
                      :filter-option="filterNodeOption"
                      placeholder="选择终点节点"
                      style="width: 100%"
                    />
                  </div>
                </a-tooltip>
                <a-select
                  v-else
                  v-model:value="record.to"
                  :options="nodeOptions"
                  size="small"
                  show-search
                  :filter-option="filterNodeOption"
                  placeholder="选择终点节点"
                  style="width: 100%"
                />
              </template>
              <template v-else-if="column.key === 'condition'">
                <a-input-group compact class="cond-group">
                  <a-input
                    v-model:value="record.condField"
                    size="small"
                    style="width: 40%"
                    placeholder="字段（如 endStatus）"
                    allow-clear
                  />
                  <a-select
                    v-model:value="record.condOp"
                    :options="COND_OP_OPTIONS"
                    size="small"
                    style="width: 22%"
                    :disabled="!record.condField"
                  />
                  <a-input
                    v-model:value="record.condValue"
                    size="small"
                    style="width: 38%"
                    placeholder="比较值"
                    :disabled="!record.condField"
                  />
                </a-input-group>
              </template>
              <template v-else-if="column.key === 'protocolLevel'">
                <a-select
                  v-model:value="record.protocolLevel"
                  :options="PROTOCOL_LEVEL_OPTIONS"
                  size="small"
                  style="width: 100%"
                />
              </template>
              <template v-else-if="column.key === 'mapping'">
                <span v-if="record.protocolLevel === 'signal'" class="mapping-hint">
                  无需配置（仅触发信号）
                </span>
                <a-input-group v-else-if="record.protocolLevel === 'ref'" compact class="cond-group">
                  <a-input
                    v-model:value="record.refTable"
                    size="small"
                    style="width: 45%"
                    placeholder="表名（如 CF卡片）"
                  />
                  <a-input
                    v-model:value="record.refFilterExpr"
                    size="small"
                    style="width: 55%"
                    placeholder="过滤表达式"
                  />
                </a-input-group>
                <div v-else class="mapping-cell">
                  <span class="mapping-summary" :title="summaryMapping(record)">
                    {{ summaryMapping(record) }}
                  </span>
                  <a-button size="small" type="link" @click="openMappingEditor(index!)">
                    编辑映射
                  </a-button>
                </div>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small" danger @click="removeEdge(index!)">
                  <DeleteOutlined />
                </a-button>
              </template>
            </template>
          </a-table>
        </a-card>
      </template>

      <!-- JSON 高级模式 -->
      <template v-else>
        <a-card class="section-card" :bordered="false" title="DAG 节点 JSON">
          <a-textarea
            v-model:value="nodesJsonText"
            :rows="14"
            class="mono-textarea"
            placeholder="DagNode[] JSON"
          />
        </a-card>
        <a-card class="section-card" :bordered="false" title="DAG 边 JSON">
          <a-textarea
            v-model:value="edgesJsonText"
            :rows="14"
            class="mono-textarea"
            placeholder="DagEdge[] JSON"
          />
        </a-card>
      </template>
    </a-spin>

    <!-- inline 映射编辑弹窗：以「目标字段 ← 源 JSONPath」结构编辑 -->
    <a-modal
      v-model:open="mappingModalVisible"
      title="编辑字段映射（inline 协议）"
      :width="640"
      ok-text="保存"
      cancel-text="取消"
      @ok="saveMappingEditor"
    >
      <div class="mapping-modal-tip">
        左侧填「目标字段名」（写入下游卡片表单，如 <code>amount</code>），右侧填「源 JSONPath」（从上游数据中取值，如 <code>$.amount</code>）。
        <br />保存后将生成 <code>{ "目标字段": "$.源路径" }</code> 格式的映射。
      </div>
      <div class="mapping-row mapping-row--head">
        <span class="mapping-col">目标字段名</span>
        <span class="mapping-arrow">←</span>
        <span class="mapping-col">源 JSONPath</span>
        <span class="mapping-action"></span>
      </div>
      <div
        v-for="(m, i) in mappingDraft"
        :key="i"
        class="mapping-row"
      >
        <a-input
          v-model:value="m.key"
          placeholder="如 amount"
          size="small"
          class="mapping-col"
        />
        <span class="mapping-arrow">←</span>
        <a-input
          v-model:value="m.value"
          placeholder="如 $.amount"
          size="small"
          class="mapping-col"
        />
        <a-button
          type="link"
          danger
          size="small"
          class="mapping-action"
          @click="removeMappingEntry(i)"
        >
          <DeleteOutlined />
        </a-button>
      </div>
      <a-button type="dashed" block size="small" @click="addMappingEntry">
        <template #icon><PlusOutlined /></template>
        添加映射项
      </a-button>
    </a-modal>

    <!-- 右侧悬浮快捷导航：在多卡片与预览图之间快速定位 -->
    <div v-if="!advancedMode" class="dag-quick-nav">
      <a-button type="text" size="small" @click="scrollToSection('dag-preview')">预览</a-button>
      <a-button type="text" size="small" @click="scrollToSection('dag-nodes')">节点</a-button>
      <a-button type="text" size="small" @click="scrollToSection('dag-edges')">边</a-button>
    </div>
  </PageLayout>
</template>

<style scoped lang="scss">
.info-card,
.section-card {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);
  flex: none;
}

.status-tag {
  color: var(--text-on-accent);
  border: none;
  font-weight: 500;
}

.mono-textarea {
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
}

/* === 边表格结构化编辑 === */
.cond-group {
  display: flex !important;
  width: 100%;
}

.mapping-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}

.mapping-summary {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-2);
  font-size: 12px;
}

.mapping-hint {
  color: var(--text-3);
  font-size: 12px;
  font-style: italic;
}

/* === 映射编辑弹窗 === */
.mapping-modal-tip {
  margin-bottom: 12px;
  padding: 8px 10px;
  background: var(--bg-muted);
  border-radius: 4px;
  font-size: 12px;
  color: var(--text-2);
  line-height: 1.6;

  code {
    background: var(--bg-card);
    padding: 1px 6px;
    border-radius: 3px;
    border: 1px solid var(--border);
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 12px;
    color: var(--color-danger-text);
  }
}

.mapping-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;

  .mapping-col {
    flex: 1;
    min-width: 0;
  }

  .mapping-arrow {
    width: 24px;
    text-align: center;
    color: var(--text-3);
  }

  .mapping-action {
    width: 32px;
    flex: none;
  }

  &--head {
    margin-bottom: 6px;
    color: var(--text-3);
    font-size: 12px;

    .mapping-col {
      padding-left: 4px;
    }
  }
}

/* === DAG 拓扑预览 === */
.dag-preview-card {
  :deep(.ant-card-body) {
    padding: 12px 16px;
  }
}

.dag-svg-wrap {
  width: 100%;
  overflow: auto;
  background:
    radial-gradient(circle, var(--border) 1px, transparent 1px) 0 0/16px 16px,
    var(--bg-muted);
  border-radius: 6px;
  padding: 8px;
  border: 1px solid var(--border);
}

.dag-empty {
  padding: 24px;
  text-align: center;
  color: var(--text-3);
  font-size: 13px;
}

.dag-svg {
  display: block;
  min-width: 100%;
}

.dag-node-name {
  fill: var(--bg-card);
  font-size: 13px;
  font-weight: 500;
  pointer-events: none;
}

.dag-node-meta {
  fill: color-mix(in srgb, var(--bg-card) 85%, transparent);
  font-size: 10px;
  pointer-events: none;
}

.dag-edge-label {
  fill: var(--text-2);
  font-size: 11px;
  paint-order: stroke;
  stroke: var(--bg-card);
  stroke-width: 3px;
  stroke-linejoin: round;
}

/* === 边引用游离高亮 === */
.edge-ref-broken {
  :deep(.ant-select-selector) {
    border-color: var(--color-danger) !important;
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-danger) 12%, transparent) !important;
  }
}

/* === 右侧悬浮快捷导航 === */
.dag-quick-nav {
  position: fixed;
  right: 12px;
  top: 38%;
  z-index: 10;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 6px 4px;
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-md);
  border: 1px solid var(--border);

  :deep(.ant-btn) {
    padding: 0 12px;
    font-size: 12px;
    height: 26px;
    line-height: 26px;
  }
}
</style>
