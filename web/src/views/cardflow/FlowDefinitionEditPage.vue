<script setup lang="ts">
/**
 * 流程定义编辑页 —— 两栏布局 + Schema/节点链双栏 + 撤销/重做 + 自动保存
 *
 * 布局：
 *   - 工具栏（PageHeader slots）：#left 返回 + 标题 / #actions 撤销 重做 保存 发布 预览 + 状态
 *   - 基本信息折叠区（isNew 时展开）
 *   - 两栏 a-row :gutter=16
 *      - 左 11：SchemaFieldEditor (cardSchema) + 明细 SchemaFieldEditor (detailSchema 可选)
 *      - 右 13：顶部 Tab 切换「节点链 / 流程设置」
 *
 * 行为：
 *   - useUndoRedo: 每次状态变化 commit 一次（500ms 防抖），最大 50 步
 *   - useAutoSave: 30s 周期；dirty 时自动保存草稿
 *   - 快捷键：Ctrl+S / Ctrl+Z / Ctrl+Shift+Z / Ctrl+Enter / Escape
 */
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import draggable from 'vuedraggable'
import {
  ArrowLeftOutlined,
  ArrowRightOutlined,
  RollbackOutlined,
  SaveOutlined,
  SendOutlined,
  EyeOutlined,
  CheckCircleFilled,
  CloseCircleFilled,
  ReloadOutlined,
  CopyOutlined,
  DeleteOutlined,
  HolderOutlined,
  RightOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SaveStateChip from '@/components/common/SaveStateChip.vue'
import SchemaFieldEditor from '@/components/cardflow/SchemaFieldEditor.vue'
import type { DetailRow } from '@/components/cardflow/CardDetailTable.vue'
import StageDefinitionEditor, { type StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import StageConfigPanel from '@/components/cardflow/StageConfigPanel.vue'
import FlowStateCanvas from '@/components/cardflow/designer/FlowStateCanvas.vue'
import RouteRuleCardEditor from '@/components/cardflow/designer/RouteRuleCardEditor.vue'
import DynamicApprovalPolicyEditor from '@/components/cardflow/designer/DynamicApprovalPolicyEditor.vue'
import PathPreviewPanel from '@/components/cardflow/designer/PathPreviewPanel.vue'
import RuleHealthPanel from '@/components/cardflow/designer/RuleHealthPanel.vue'
import CardComponentCatalog from '@/components/cardflow/designer/CardComponentCatalog.vue'
import CardComponentConfigDrawer from '@/components/cardflow/designer/CardComponentConfigDrawer.vue'
import { validatePublishConfig, type DiagnosticTarget } from '@/utils/cardflowDiagnostics'
import CardComponentRenderer from '@/components/cardflow/runtime/CardComponentRenderer.vue'
import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import {
  getFlowDefinition, createFlowDefinition, updateFlowDefinition,
  publishFlowDefinition, getFlowDraftVersion, saveFlowDraftVersion,
  getFlowGroups, getFlowDefinitions, previewFlowDraftPath, previewPresentation,
} from '@/api/cardflow'
import { getRoleList, getUserList, getUserDetail } from '@/api/system'
import type {
  FlowDefinitionDto, FlowVersionDetailDto, StageDefinitionRequest,
  SchemaFieldDefinition, FlowGroupDto, StageRouteRuleRequest,
  DynamicStagePolicyRequest, CardComponentDefinition,
  CardComponentRuntime, CardHeaderConfig, CardPresentationSnapshot, StageWorkView,
} from '@/types/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'
import { useUndoRedo, useAutoCommit } from '@/composables/useUndoRedo'
import { useAutoSave } from '@/composables/useAutoSave'
import {
  defaultCardHeaderConfig,
  parseCardSchemaPayload,
  parseDetailSchema,
  type DetailTableSchema,
} from '@/utils/cardflowSchema'
import StatusTag from '@/components/StatusTag.vue'
import BaseCard from '@/components/BaseCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import { FLOW_STATUS_META, type FlowStatus } from './flowStatusMeta'

// ==================== 状态形态 ====================

interface BasicInfo {
  flowName: string
  flowCode: string
  description: string
  numberTemplate: string
  titleTemplate: string
  flowGroupId: number | undefined
  allowedRoles: string[]
  status: string
  /** 导入文件名匹配通配符（如 *韵达*），保存时包装为 matchPattern JSON 的 fileNamePattern */
  matchFileNamePattern: string
}

interface FlowSettings {
  rejectStrategy: 'toInitiator' | 'toPrevious' | 'toSpecified'
  resubmitStrategy: 'fromStart' | 'fromRejected'
  approvalAdminUserIds: number[]
  prerequisites: { flowCode: string; required: boolean }[]
  offsetEnabled: boolean
  offsetSourceFlowCodes: string[]
  generateBalance: boolean
  settleBalance: boolean
  settleSourceFlowCode: string
}

interface FlowState {
  basic: BasicInfo
  cardSchema: SchemaFieldDefinition[]
  detailSchema: SchemaFieldDefinition[]
  detailTableKey: string
  extraDetailTables: DetailTableSchema[]
  cardHeader: CardHeaderConfig
  cardComponents: CardComponentDefinition[]
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  settings: FlowSettings
}

const initialState = (): FlowState => ({
  basic: {
    flowName: '', flowCode: '', description: '',
    numberTemplate: '', titleTemplate: '',
    flowGroupId: undefined, allowedRoles: [],
    status: 'draft',
    matchFileNamePattern: '',
  },
  cardSchema: [],
  detailSchema: [],
  detailTableKey: 'default',
  extraDetailTables: [],
  cardHeader: defaultCardHeaderConfig(),
  cardComponents: [],
  stages: [],
  routes: [],
  dynamicPolicies: [],
  settings: {
    rejectStrategy: 'toInitiator',
    resubmitStrategy: 'fromStart',
    approvalAdminUserIds: [],
    prerequisites: [],
    offsetEnabled: false,
    offsetSourceFlowCodes: [],
    generateBalance: false,
    settleBalance: false,
    settleSourceFlowCode: '',
  },
})

// ==================== 路由 / 标识 ====================

const route = useRoute()
const router = useRouter()
const orgStore = useOrgContextStore()

const flowId = computed(() => route.query.id ? Number(route.query.id) : null)
const isNew = computed(() => !flowId.value)
const loading = ref(false)
const loadError = ref(false)
const publishing = ref(false)
const draftVersionNumber = ref<number | null>(null)
const publishedVersionNumber = ref<number | null>(null)

// ==================== 业务状态 ====================

const state = reactive<FlowState>(initialState())
// 失败汇总（用于区域红边）
const errors = reactive({ basic: false, schema: false, stages: false, condition: false })
const selectedPreviewStageId = ref<string | undefined>()
// B4 预览工作台：视角（处理人/旁观者/发起人填单）× 设备（PC / 移动）
const previewViewerMode = ref<'assignee' | 'observer' | 'initiator'>('assignee')
const previewDevice = ref<'pc' | 'mobile'>('pc')
const designerSelection = reactive<{ type: 'blank' | 'node' | 'edge'; key: string | null }>({
  type: 'blank',
  key: null,
})
const designerDrawerOpen = ref(false)
const componentDrawerOpen = ref(false)
const editingComponentId = ref<string | null>(null)
const cardHeaderSelected = ref(false)
const cardRuntimePreviewOpen = ref(false)
const cardRuntimePreviewStageId = ref<string | undefined>()
const cardRuntimePreviewMode = ref<'view' | 'edit'>('view')
const runtimePreviewSampleData = ref<Record<string, any>>({})
const runtimePreviewDetailRows = ref<DetailRow[]>([])

// ==================== 步骤导航 ====================

const STEPS = [
  { key: 'basic',    title: '基本信息', desc: '名称 · 编码 · 模板 · 角色' },
  { key: 'schema',   title: '字段设计', desc: '卡片字段 · 明细行字段' },
  // V2 冻结：卡片视图低代码编排（简化瘦身桶B 2026-06-16），入口暂下线，数据通道保留
  { key: 'stages',   title: '节点链',   desc: '流程图 · 节点权限' },
  { key: 'settings', title: '流程配置', desc: '退回 · 重提 · 依赖 · 余额' },
  { key: 'preview',  title: '预演与校验', desc: '路径 · 卡片视图 · 发布校验' },
] as const

// 步骤索引一律按 key 解析，冻结/恢复步骤时不必追改散落的硬编码数字
type StepKey = (typeof STEPS)[number]['key']
const stepIndexOf = (key: StepKey) => STEPS.findIndex(s => s.key === key)
const STEP_BASIC = stepIndexOf('basic')
const STEP_SCHEMA = stepIndexOf('schema')
const STEP_STAGES = stepIndexOf('stages')
const STEP_SETTINGS = stepIndexOf('settings')
const STEP_PREVIEW = stepIndexOf('preview')

const activeStep = ref(0)

function handleStepChange(idx: number) {
  activeStep.value = idx
}

// 各步状态徽章：finish / error / process / wait
const stepStatus = computed<Array<'finish' | 'error' | 'process' | 'wait'>>(() => {
  return STEPS.map((s, idx) => {
    if (idx === activeStep.value) return 'process'
    switch (s.key) {
      case 'basic':
        if (errors.basic) return 'error'
        return state.basic.flowName.trim() && state.basic.flowCode.trim() ? 'finish' : 'wait'
      case 'schema':
        if (errors.schema) return 'error'
        return state.cardSchema.length > 0 ? 'finish' : 'wait'
      case 'stages':
        if (errors.stages || errors.condition) return 'error'
        return state.stages.length > 0 ? 'finish' : 'wait'
      case 'settings':
        return 'finish'
      case 'preview':
        return 'wait'
    }
    return 'wait'
  })
})

// 历史栈
const history = useUndoRedo<FlowState>(JSON.parse(JSON.stringify(state)))
const { silently, flushPending } = useAutoCommit(() => state, history, 500)

// ==================== 自动保存 ====================

const dirty = ref(false)
// 编辑序号：保存请求在途期间若产生新编辑，保存完成后不得清除 dirty（否则关页丢数据）
let editSeq = 0

const auto = useAutoSave({
  intervalMs: 30_000,
  isDirty: () => dirty.value,
  save: async () => { await silentSave() },
})

watch(() => state, () => { editSeq++; dirty.value = true; auto.markDirty() }, { deep: true })

const previewStageOptions = computed(() =>
  state.stages.map((stage, index) => ({
    value: stage.id,
    label: `${index + 1}. ${stage.name || '未命名节点'}（${stage.type === 'manual' ? '人工' : '自动'}）`,
  }))
)

const selectedPreviewStage = computed(() =>
  state.stages.find(stage => stage.id === selectedPreviewStageId.value) || state.stages[0] || null
)

interface PreviewReadinessItem {
  key: string
  title: string
  description: string
  ready: boolean
  step?: number
  actionText: string
}

const previewReadinessItems = computed<PreviewReadinessItem[]>(() => [
  {
    key: 'loaded',
    title: '流程定义已加载',
    description: loadError.value ? '当前流程定义没有成功加载，无法判断预演内容。' : '已读取当前草稿或发布版本的配置。',
    ready: !loadError.value,
    actionText: '重新加载',
  },
  {
    key: 'basic',
    title: '基本信息完整',
    description: '需要流程名称和编码，预演卡片才有可识别的标题。',
    ready: Boolean(state.basic.flowName.trim() && state.basic.flowCode.trim()),
    step: STEP_BASIC,
    actionText: '去基本信息',
  },
  {
    key: 'schema',
    title: '字段设计已配置',
    description: '至少配置一个卡片字段，才能生成样例卡片数据和节点权限视图。',
    ready: state.cardSchema.length > 0,
    step: STEP_SCHEMA,
    actionText: '去字段设计',
  },
  // V2 冻结：卡片视图就绪项随桶B入口下线（2026-06-16）——无组件时预演走扁平字段回退渲染，不再阻塞
  {
    key: 'stages',
    title: '节点链已配置',
    description: '至少需要一个审批或自动节点，才能选择节点视图并预演路径。',
    ready: state.stages.length > 0,
    step: STEP_STAGES,
    actionText: '去节点链',
  },
])

const previewBlockingItems = computed(() =>
  previewReadinessItems.value.filter(item => !item.ready)
)

const previewReady = computed(() => previewBlockingItems.value.length === 0)

const previewToolbarPlaceholder = computed(() => {
  if (loadError.value) return '流程定义未加载成功'
  if (!state.stages.length) return '先配置节点链后选择预演节点'
  return '选择要预览的节点'
})

const selectedDesignerStage = computed(() =>
  designerSelection.type === 'node'
    ? state.stages.find(stage => stage.id === designerSelection.key) || null
    : null
)

// 画布抽屉复用节点链的 StageConfigPanel（消双入口）：按选中 key 定位 state.stages 下标喂给面板，
// 面板就地改 state.stages[idx]，经本页 state 深监听统一走自动保存/撤销，与旧 patchDesignerStage 同路径。
const drawerStageIndex = computed(() =>
  designerSelection.type === 'node'
    ? state.stages.findIndex(stage => stage.id === designerSelection.key)
    : -1
)

const selectedDesignerRoute = computed(() =>
  designerSelection.type === 'edge'
    ? state.routes.find(route => route.edgeKey === designerSelection.key) || null
    : null
)

const designerDrawerTitle = computed(() => {
  if (designerSelection.type === 'node') return '节点配置'
  if (designerSelection.type === 'edge') return '条件流转'
  return '预演与校验'
})

// 选中节点的出边列表（含停用边）：画布上平行边可能重叠点不到，此处兜底可点选任一条
const selectedStageOutgoingRoutes = computed(() =>
  selectedDesignerStage.value
    ? state.routes
        .filter(route => route.fromStageKey === selectedDesignerStage.value!.id)
        .sort((a, b) => Number(a.isDefault) - Number(b.isDefault) || a.priority - b.priority)
    : []
)

const selectedCardComponent = computed(() =>
  editingComponentId.value
    ? state.cardComponents.find(component => component.id === editingComponentId.value) || null
    : null
)
const editingComponent = selectedCardComponent

// B4 预览视角：处理人=运行态审批视图 / 旁观者=再过一层脱敏 / 发起人填单=可编辑填单
const previewViewerModeOptions = [
  { value: 'assignee', label: '处理人' },
  { value: 'initiator', label: '发起人填单' },
  { value: 'observer', label: '旁观者' },
]
const previewDeviceOptions = [
  { value: 'pc', label: 'PC' },
  { value: 'mobile', label: '移动' },
]

function genStableKey(prefix: string) {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`
}

function selectDesignerNode(stageKey: string) {
  designerSelection.type = 'node'
  designerSelection.key = stageKey
  designerDrawerOpen.value = true
}

function selectDesignerEdge(edgeKey: string) {
  designerSelection.type = 'edge'
  designerSelection.key = edgeKey
  designerDrawerOpen.value = true
}

function selectDesignerBlank() {
  designerSelection.type = 'blank'
  designerSelection.key = null
  designerDrawerOpen.value = true
}

// 诊断"点击直达现场"：切到节点链步骤 + 选中对应节点/边并打开配置抽屉
function focusDiagnosticTarget(target: DiagnosticTarget) {
  activeStep.value = STEP_STAGES
  if (target.kind === 'node') selectDesignerNode(target.key)
  else selectDesignerEdge(target.key)
}

function createRoute(fromStageKey?: string) {
  if (state.stages.length < 2) {
    message.warning('至少需要两个节点才能添加条件边')
    return
  }
  const from = fromStageKey && state.stages.some(stage => stage.id === fromStageKey)
    ? fromStageKey
    : state.stages[0].id
  const fromIndex = state.stages.findIndex(stage => stage.id === from)
  const to = state.stages[fromIndex + 1]?.id || state.stages.find(stage => stage.id !== from)?.id
  if (!to) return
  const route: StageRouteRuleRequest = {
    edgeKey: genStableKey('edge'),
    fromStageKey: from,
    toStageKey: to,
    routeName: '其他情况',
    conditionJson: null,
    priority: state.routes.filter(item => item.fromStageKey === from).length + 1,
    isDefault: !state.routes.some(item => item.fromStageKey === from && item.isDefault),
    status: 'active',
    failurePolicyJson: null,
  }
  state.routes.push(route)
  selectDesignerEdge(route.edgeKey)
}

function connectRouteFromCanvas(payload: { fromStageKey: string; toStageKey: string }) {
  if (state.stages.length < 2) {
    message.warning('至少需要两个节点才能添加条件边')
    return
  }
  const fromExists = state.stages.some(stage => stage.id === payload.fromStageKey)
  const toExists = state.stages.some(stage => stage.id === payload.toStageKey)
  if (!fromExists || !toExists || payload.fromStageKey === payload.toStageKey) {
    message.warning('请选择不同的来源和目标节点')
    return
  }
  const route: StageRouteRuleRequest = {
    edgeKey: genStableKey('edge'),
    fromStageKey: payload.fromStageKey,
    toStageKey: payload.toStageKey,
    routeName: '条件分支',
    conditionJson: null,
    priority: state.routes.filter(item => item.fromStageKey === payload.fromStageKey).length + 1,
    isDefault: !state.routes.some(item => item.fromStageKey === payload.fromStageKey && item.isDefault),
    status: 'active',
    failurePolicyJson: null,
  }
  state.routes.push(route)
  selectDesignerEdge(route.edgeKey)
}

function reorderStagesByCanvas(orderedStageKeys: string[]) {
  if (orderedStageKeys.length !== state.stages.length) return
  const stageById = new Map(state.stages.map(stage => [stage.id, stage]))
  const orderedStages = orderedStageKeys
    .map(stageKey => stageById.get(stageKey))
    .filter((stage): stage is StageDefinition => Boolean(stage))
  if (orderedStages.length !== state.stages.length) return
  state.stages = orderedStages.map((stage, index) => ({
    ...stage,
    sortOrder: index + 1,
  }))
}

function updateRoute(route: StageRouteRuleRequest) {
  const index = state.routes.findIndex(item => item.edgeKey === route.edgeKey)
  if (index >= 0) {
    state.routes[index] = route
  }
}

function deleteRoute(edgeKey: string) {
  state.routes = state.routes.filter(route => route.edgeKey !== edgeKey)
  selectDesignerBlank()
}

function addCardComponent(component: CardComponentDefinition) {
  state.cardComponents.push(component)
  editingComponentId.value = component.id
  cardHeaderSelected.value = false
  syncCardComponentLayoutOrder()
}

function updateCardComponent(component: CardComponentDefinition) {
  const index = state.cardComponents.findIndex(item => item.id === component.id)
  if (index >= 0) {
    state.cardComponents[index] = component
  }
}

function selectCardComponent(componentId: string) {
  editingComponentId.value = componentId
  cardHeaderSelected.value = false
}

function selectCardHeader() {
  editingComponentId.value = null
  cardHeaderSelected.value = true
}

function patchCardHeader(partial: Partial<CardHeaderConfig>) {
  Object.assign(state.cardHeader, partial)
}

function patchCardComponent(partial: Partial<CardComponentDefinition>) {
  const component = selectedCardComponent.value
  if (!component) return
  Object.assign(component, partial)
}

function patchCardComponentBinding(partial: Partial<CardComponentDefinition['binding']>) {
  const component = selectedCardComponent.value
  if (!component) return
  component.binding = {
    ...(component.binding || { source: 'cardField' }),
    ...partial,
  }
}

function patchCardComponentLayout(partial: Record<string, any>) {
  const component = selectedCardComponent.value
  if (!component) return
  component.layout = {
    ...(component.layout || {}),
    ...partial,
  }
}

function patchCardComponentProps(partial: Record<string, any>) {
  const component = selectedCardComponent.value
  if (!component) return
  component.props = {
    ...(component.props || {}),
    ...partial,
  }
}

function duplicateCardComponent(component: CardComponentDefinition) {
  const copy: CardComponentDefinition = JSON.parse(JSON.stringify(component))
  copy.id = `${component.id}_copy_${Math.random().toString(36).slice(2, 6)}`
  copy.title = `${component.title || component.id} 副本`
  const index = state.cardComponents.findIndex(item => item.id === component.id)
  state.cardComponents.splice(index >= 0 ? index + 1 : state.cardComponents.length, 0, copy)
  editingComponentId.value = copy.id
  syncCardComponentLayoutOrder()
}

function syncCardComponentLayoutOrder() {
  state.cardComponents.forEach((component, index) => {
    component.layout = {
      ...(component.layout || {}),
      sortOrder: index + 1,
    }
  })
}

function handleCardCanvasChange(event: any) {
  const added = event?.added?.element as CardComponentDefinition | undefined
  if (added?.id) {
    editingComponentId.value = added.id
    cardHeaderSelected.value = false
  }
  syncCardComponentLayoutOrder()
}

function deleteCardComponent(componentId: string) {
  state.cardComponents = state.cardComponents.filter(component => component.id !== componentId)
  if (editingComponentId.value === componentId) editingComponentId.value = null
  state.stages.forEach(stage => {
    if (stage.viewProfile?.componentAccess) {
      delete stage.viewProfile.componentAccess[componentId]
    }
  })
  componentDrawerOpen.value = false
  editingComponentId.value = null
}

function openComponentConfig(componentId: string) {
  editingComponentId.value = componentId
  componentDrawerOpen.value = true
}

watch(
  () => state.stages.map(stage => stage.id).join('|'),
  () => {
    // 节点删除后联动清理孤儿引用：后端保存会因"来源/目标节点不存在"直接拒绝整份草稿，
    // 而画布对孤儿边是静默隐藏的，用户既看不到也删不掉，保存会被永久阻断
    const stageIds = new Set(state.stages.map(stage => stage.id))
    if (state.routes.some(route => !stageIds.has(route.fromStageKey) || !stageIds.has(route.toStageKey))) {
      state.routes = state.routes.filter(route => stageIds.has(route.fromStageKey) && stageIds.has(route.toStageKey))
    }
    if (state.dynamicPolicies.some(policy => !stageIds.has(policy.sourceStageKey))) {
      state.dynamicPolicies = state.dynamicPolicies.filter(policy => stageIds.has(policy.sourceStageKey))
    }
    for (const policy of state.dynamicPolicies) {
      if (policy.continuationStageKey && !stageIds.has(policy.continuationStageKey)) {
        policy.continuationStageKey = null
      }
    }

    if (!state.stages.length) {
      selectedPreviewStageId.value = undefined
      return
    }
    if (!selectedPreviewStageId.value || !state.stages.some(stage => stage.id === selectedPreviewStageId.value)) {
      selectedPreviewStageId.value = state.stages[0].id
    }
  },
)

type PreviewAccess = 'hidden' | 'masked' | 'readonly' | 'editable' | 'required'

function normalizeAccess(access?: string | null): PreviewAccess {
  if (access === 'hidden' || access === 'masked' || access === 'editable' || access === 'required') return access
  return 'readonly'
}

function getPreviewCardAccess(stage: StageDefinition | null, field: SchemaFieldDefinition): PreviewAccess {
  if (!stage || stage.type !== 'manual') return 'readonly'
  const configured = stage.viewProfile?.fieldAccess?.[field.key]
  if (configured?.access) return normalizeAccess(configured.access)
  return stage.inputFields?.includes(field.key) ? 'editable' : 'readonly'
}

function getPreviewDetailAccess(stage: StageDefinition | null, field: SchemaFieldDefinition): PreviewAccess {
  if (!stage || stage.type !== 'manual') return 'readonly'
  const key = `default.${field.key}`
  return normalizeAccess(stage.viewProfile?.detailAccess?.[key]?.access)
}

function isPreviewFieldRequired(stage: StageDefinition | null, field: SchemaFieldDefinition): boolean {
  if (!stage || stage.type !== 'manual') return Boolean(field.required)
  const rule = stage.viewProfile?.fieldAccess?.[field.key]
  return rule?.access === 'required' || rule?.required === true || Boolean(field.required)
}

const stagePreviewFields = computed(() => {
  const stage = selectedPreviewStage.value
  return state.cardSchema
    .map(field => ({
      field,
      access: getPreviewCardAccess(stage, field),
      required: isPreviewFieldRequired(stage, field),
    }))
    .filter(item => item.access !== 'hidden')
})

function visibleDetailSchemaFor(stage: StageDefinition | null) {
  return state.detailSchema.filter(field => getPreviewDetailAccess(stage, field) !== 'hidden')
}

const stagePreviewDetailSchema = computed(() => visibleDetailSchemaFor(selectedPreviewStage.value))

function previewValueOf(field: SchemaFieldDefinition): any {
  if (field.type === 'money') return field.key.toLowerCase().includes('offset') ? 0 : 5200
  if (field.type === 'date') return '2026-06-10'
  if (field.type === 'enum') {
    const first = field.options?.[0]
    return (first && typeof first === 'object' ? first.value : first) || '日常费用'
  }
  if (field.type === 'user') return { name: '示例发起人' }
  if (field.type === 'org') return { name: '示例部门' }
  if (field.type === 'cardRef') return { cardNumber: 'CF-20260610-001', title: '引用卡片' }
  if (field.type === 'file') return []
  if (field.type === 'bankAccount') return { name: '基本户', accountNo: '**** 8808' }
  if (field.type === 'account') return { code: '6602', name: '管理费用' }
  if (field.type === 'auxiliary') return { name: '示例辅助项' }
  if (field.type === 'voucherRef') return { voucherNo: 'V-202606-001' }
  return field.placeholder || `示例${field.label || field.key}`
}

const previewSampleData = computed<Record<string, any>>(() => {
  const data: Record<string, any> = {}
  for (const field of state.cardSchema) {
    data[field.key] = previewValueOf(field)
  }
  return data
})

const previewDetailRows = computed<DetailRow[]>(() => {
  if (!state.detailSchema.length) return []
  const row: DetailRow = { _id: 'preview_detail_1' }
  for (const field of state.detailSchema) {
    row[field.key] = previewValueOf(field)
  }
  return [row]
})

const previewDetailSummary = computed<Record<string, any>>(() => {
  const summary: Record<string, any> = {}
  for (const field of state.detailSchema) {
    if (field.type === 'money' || String(field.type) === 'number' || field.key.toLowerCase().includes('amount')) {
      summary[field.key] = previewDetailRows.value.reduce((sum, row) => sum + Number(row[field.key] || 0), 0)
    }
  }
  summary['detailSum.amount'] = Object.entries(summary)
    .filter(([key]) => key.toLowerCase().includes('amount'))
    .reduce((sum, [, value]) => sum + Number(value || 0), 0)
  return summary
})

function interpolateCardHeaderTemplate(template: string | null | undefined) {
  const source = template?.trim()
  if (!source) return ''
  const context: Record<string, any> = {
    flowName: state.basic.flowName,
    flowCode: state.basic.flowCode,
    ...previewSampleData.value,
  }
  return source.replace(/\{([^}]+)\}/g, (_, key: string) => {
    const value = context[key.trim()]
    return value === null || value === undefined || value === '' ? '-' : String(value)
  })
}

function resolveCardHeaderText(
  mode: string | null | undefined,
  fixedText: string | null | undefined,
  fieldKey: string | null | undefined,
  template: string | null | undefined,
  fallback: string,
) {
  if (mode === 'hidden') return ''
  if (mode === 'fixed') return fixedText?.trim() || fallback
  if (mode === 'field') {
    const value = fieldKey ? previewSampleData.value[fieldKey] : null
    return value === null || value === undefined || value === '' ? fallback : String(value)
  }
  if (mode === 'template') return interpolateCardHeaderTemplate(template) || fallback
  if (mode === 'flowCode') return state.basic.flowCode || fallback
  return state.basic.flowName || fallback
}

const cardHeaderTitle = computed(() =>
  resolveCardHeaderText(
    state.cardHeader.titleMode,
    state.cardHeader.titleText,
    state.cardHeader.titleFieldKey,
    state.cardHeader.titleTemplate,
    state.basic.flowName || '未命名流程',
  )
)

const cardHeaderSubtitle = computed(() =>
  resolveCardHeaderText(
    state.cardHeader.subtitleMode,
    state.cardHeader.subtitleText,
    state.cardHeader.subtitleFieldKey,
    state.cardHeader.subtitleTemplate,
    state.basic.flowCode || '—',
  )
)

const cardHeaderShowSubtitle = computed(() =>
  state.cardHeader.subtitleMode !== 'hidden' && state.cardHeader.showSubtitle !== false && Boolean(cardHeaderSubtitle.value)
)

const cardHeaderShowStatus = computed(() => state.cardHeader.showStatus === true)

function previewSnapshotsFor(snapshotType?: string | null): CardPresentationSnapshot[] {
  if (snapshotType === 'dynamicApprover') {
    return [{
      snapshotType: 'dynamicApprover',
      title: '动态审批人',
      reason: '根据发起人组织链和金额策略，运行时插入部门负责人审批。',
      metadata: {},
    }]
  }
  if (snapshotType === 'routeDecision') {
    return [{
      snapshotType: 'routeDecision',
      title: '条件流转',
      reason: '样例金额命中大额报销分支，下一节点进入总经理审批。',
      metadata: {},
    }]
  }
  return []
}

function componentBindingText(component: CardComponentDefinition | CardComponentRuntime) {
  const binding = component.binding || { source: 'cardField' }
  if (binding.source === 'cardField') return `绑定：卡片字段 ${binding.fieldKey || '未选择'}`
  if (binding.source === 'detailTable') return `绑定：明细表 ${binding.detailTableKey || 'default'}`
  if (binding.source === 'detailSummary') return `绑定：明细汇总 ${binding.summaryKey || '未选择'}`
  if (binding.source === 'relation') return `绑定：关联卡片 ${binding.relationType || '未选择'}`
  if (binding.source === 'snapshot') return `绑定：运行快照 ${binding.snapshotType || '未选择'}`
  if (binding.source === 'static') return '绑定：静态展示内容'
  return `绑定：${binding.source || '未配置'}`
}

function runtimeAccessOf(component: CardComponentDefinition, stage: StageDefinition | null): PreviewAccess {
  const stageRule = stage?.viewProfile?.componentAccess?.[component.id]
  return normalizeAccess(stageRule?.access || component.access)
}

function buildPreviewComponentDefinitions(): CardComponentDefinition[] {
  if (state.cardComponents.length) return state.cardComponents
  return stagePreviewFields.value.map(item => ({
    id: `field_preview_${item.field.key}`,
    type: item.field.type,
    title: item.field.label || item.field.key,
    access: item.access,
    binding: { source: 'cardField', fieldKey: item.field.key },
    props: {},
    validation: null,
    visibilityCondition: undefined,
    layout: {},
    aggregation: null,
    statisticKey: undefined,
  }))
}

function buildRuntimeComponent(component: CardComponentDefinition, stage: StageDefinition | null): CardComponentRuntime {
  const access = runtimeAccessOf(component, stage)
  const binding = component.binding || { source: 'cardField' }
  const fieldKey = binding.fieldKey || ''
  const sourceValue = binding.source === 'cardField'
    ? previewSampleData.value[fieldKey]
    : binding.source === 'detailSummary'
      ? previewDetailSummary.value[binding.summaryKey || 'detailSum.amount']
      : null
  const columns = component.type === 'detailTable'
    ? visibleDetailSchemaFor(stage).map(field => ({
      key: field.key,
      label: field.label || field.key,
      type: field.type,
      access: getPreviewDetailAccess(stage, field),
      editable: false,
      required: Boolean(field.required),
      masked: getPreviewDetailAccess(stage, field) === 'masked',
    }))
    : []
  const rows = component.type === 'detailTable'
    ? previewDetailRows.value.map((row, index) => ({
      id: index + 1,
      sortOrder: index + 1,
      values: Object.fromEntries(state.detailSchema.map(field => [field.key, row[field.key]])),
    }))
    : []
  return {
    id: component.id,
    type: component.type,
    title: component.title || component.id,
    access,
    visible: access !== 'hidden',
    editable: access === 'editable' || access === 'required',
    required: access === 'required' || stage?.viewProfile?.componentAccess?.[component.id]?.required === true,
    masked: access === 'masked',
    binding,
    props: component.props || {},
    value: sourceValue,
    statisticKey: component.statisticKey || null,
    columns,
    rows,
    snapshots: previewSnapshotsFor(binding.snapshotType),
    warnings: [],
  }
}

function canvasRuntimeComponentsFor(component: CardComponentDefinition): CardComponentRuntime[] {
  return [buildRuntimeComponent(component, null)]
}

// B3：节点卡片预览优先用后端 previewPresentation 真值（脱敏/聚合/access 归一与运行时一字不差），
// 端点失败或缺 stageKey 时回退前端复刻 buildRuntimeComponent（渐进接入，先并行可比对再逐步删复刻）
const previewEndpointWorkView = ref<StageWorkView | null>(null)

async function refreshPreviewPresentation() {
  const stage = selectedPreviewStage.value
  if (!flowId.value || !stage?.id) {
    previewEndpointWorkView.value = null
    return
  }
  try {
    const details = previewDetailRows.value.map((row, index) => {
      const { _id, ...data } = row
      return {
        detailTableKey: state.detailTableKey || 'default',
        dataJson: JSON.stringify(data),
        sortOrder: index,
      }
    })
    const res = await previewPresentation(flowId.value, {
      stageKey: stage.id,
      dataJson: JSON.stringify(previewSampleData.value),
      details,
      viewerMode: previewViewerMode.value,
    })
    previewEndpointWorkView.value = res.workView
  } catch {
    // 端点失败（草稿未保存/节点无 v2 视图等）→ 回退前端复刻，不阻断预览
    previewEndpointWorkView.value = null
  }
}

let previewRefreshTimer: ReturnType<typeof setTimeout> | null = null
function schedulePreviewRefresh() {
  if (previewRefreshTimer) clearTimeout(previewRefreshTimer)
  previewRefreshTimer = setTimeout(refreshPreviewPresentation, 500)
}
// 端点读已保存草稿，故按预览节点/视角切换刷新（编辑期改动经 30s 自动保存后随切换生效）
watch([selectedPreviewStageId, previewViewerMode], schedulePreviewRefresh)

// 路径预演步骤点击 → 卡片预览切到该节点（stageKey 即 stage.id）
function onPreviewStepSelect(stageKey: string) {
  if (state.stages.some(stage => stage.id === stageKey)) {
    selectedPreviewStageId.value = stageKey
  }
}

const previewRuntimeComponents = computed<CardComponentRuntime[]>(() => {
  // 优先端点真值（脱敏/聚合/access 与运行时一字不差）
  if (previewEndpointWorkView.value?.components?.length) {
    return previewEndpointWorkView.value.components
  }
  // 有真实编排组件时前端复刻；无组件则返回空 → SchemaRenderer 走扁平字段回退，与运行时一致（消灭“伪组件”漂移）
  if (state.cardComponents.length) {
    return state.cardComponents.map(component => buildRuntimeComponent(component, selectedPreviewStage.value))
  }
  return []
})

// 卡片预览统一渲染入参：视角→模式、设备→平台；无组件时喂可见字段让 SchemaRenderer 扁平回退（与运行时同一装配层）
const previewCardMode = computed<'view' | 'edit'>(() => (previewViewerMode.value === 'initiator' ? 'edit' : 'view'))
const previewVisibleFieldDefs = computed<SchemaFieldDefinition[]>(() => stagePreviewFields.value.map(item => item.field))
const previewHasVisibleContent = computed(() =>
  previewRuntimeComponents.value.some(c => c.visible && c.access !== 'hidden') || previewVisibleFieldDefs.value.length > 0,
)

const previewVisibleComponentCount = computed(() => {
  const visible = previewRuntimeComponents.value.filter(component => component.visible && component.access !== 'hidden').length
  return visible || previewVisibleFieldDefs.value.length
})

const cardRuntimePreviewStage = computed(() =>
  state.stages.find(stage => stage.id === cardRuntimePreviewStageId.value) || selectedPreviewStage.value || null
)

const cardRuntimePreviewComponents = computed<CardComponentRuntime[]>(() =>
  buildPreviewComponentDefinitions().map(component => buildRuntimeComponent(component, cardRuntimePreviewStage.value)),
)

const cardRuntimePreviewVisibleComponents = computed(() =>
  cardRuntimePreviewComponents.value.filter(component => component.visible && component.access !== 'hidden')
)

const cardRuntimePreviewCanEdit = computed(() =>
  cardRuntimePreviewVisibleComponents.value.some(component => component.editable || component.required)
)

function clonePreviewValue<T>(value: T): T {
  return JSON.parse(JSON.stringify(value ?? null))
}

function openCardRuntimePreview() {
  cardRuntimePreviewStageId.value = selectedPreviewStageId.value || state.stages[0]?.id
  cardRuntimePreviewMode.value = 'view'
  runtimePreviewSampleData.value = clonePreviewValue(previewSampleData.value)
  runtimePreviewDetailRows.value = clonePreviewValue(previewDetailRows.value)
  cardRuntimePreviewOpen.value = true
}

function updateRuntimePreviewSampleData(value: Record<string, any>) {
  runtimePreviewSampleData.value = value || {}
}

function updateRuntimePreviewDetailRows(value: DetailRow[]) {
  runtimePreviewDetailRows.value = value || []
}

function runtimeAccessLabel(access: string | null | undefined) {
  if (access === 'editable') return '可编辑'
  if (access === 'required') return '必填'
  if (access === 'masked') return '脱敏'
  if (access === 'hidden') return '隐藏'
  return '只读'
}

function runtimeAccessType(access: string | null | undefined): 'info' | 'warning' | 'default' | 'success' {
  if (access === 'editable' || access === 'required') return 'info'
  if (access === 'masked') return 'warning'
  if (access === 'hidden') return 'default'
  return 'success'
}

function runtimeComponentCapability(component: CardComponentRuntime) {
  if (component.access === 'masked') return '运行时按节点权限脱敏展示，避免敏感信息外泄。'
  if (component.type === 'detailTable') return component.editable ? '可查看并维护明细行。' : '展示明细行和汇总口径。'
  if (component.type === 'relationLookup') return '展示或选择关联表单数据，承接跨流程上下文。'
  if (component.type === 'componentSuite') return '按业务套件聚合关键字段、状态和处理结果。'
  if (component.type === 'rating') return '展示评分控件的运行态样式。'
  if (component.type === 'signature') return '展示签名采集入口。'
  if (component.type === 'imageList') return '展示图片或附件类内容的运行态占位。'
  if (component.editable || component.required) return '审批处理时可录入或修改该组件绑定的数据。'
  return '审批处理时展示该组件绑定的数据。'
}

const cardRuntimePreviewFeatureRows = computed(() =>
  cardRuntimePreviewVisibleComponents.value.map(component => ({
    id: component.id,
    title: component.title || component.id,
    type: component.type,
    access: component.access,
    binding: componentBindingText(component),
    capability: runtimeComponentCapability(component),
  }))
)

watch(cardRuntimePreviewCanEdit, (canEdit) => {
  if (!canEdit && cardRuntimePreviewMode.value === 'edit') {
    cardRuntimePreviewMode.value = 'view'
  }
})

const previewCoverageStats = computed(() => [
  { key: 'fields', label: '卡片字段', value: `${state.cardSchema.length}` },
  { key: 'details', label: '明细字段', value: `${state.detailSchema.length}` },
  { key: 'components', label: '视图组件', value: `${state.cardComponents.length}` },
  { key: 'visible', label: '当前可见', value: `${previewVisibleComponentCount.value}` },
])

// ==================== 元数据下拉 ====================

const flowGroups = ref<FlowGroupDto[]>([])
const roleOptions = ref<{ value: string; label: string }[]>([])
const availableFlows = ref<{ code: string; name: string }[]>([])

interface UserOption {
  label: string
  value: number
  userName: string
  orgName?: string
}

const approvalAdminUserOptions = ref<UserOption[]>([])
const approvalAdminSearchLoading = ref(false)

function filterOption(input: string, option: any) {
  const text = String(option?.label ?? '').toLowerCase()
  return text.includes(String(input || '').toLowerCase())
}

function debounce<T extends (...args: any[]) => any>(fn: T, wait = 300) {
  let timer: any = null
  return (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn(...args), wait)
  }
}

function getUserDisplayName(u: any) {
  return u.realName || u.name || u.userName || u.account || String(u.id)
}

function getUserOrgName(u: any) {
  return u.orgName || u.departmentName || u.department || ''
}

function formatUserOptionLabel(u: any) {
  const name = getUserDisplayName(u)
  const orgName = getUserOrgName(u)
  return orgName ? `${name} / ${orgName}` : name
}

function mergeApprovalAdminUserOptions(users: any[]) {
  const nextOptions = users.map((u: any) => ({
    label: formatUserOptionLabel(u),
    value: Number(u.id),
    userName: getUserDisplayName(u),
    orgName: getUserOrgName(u),
  })).filter((option: UserOption) => Number.isFinite(option.value) && option.value > 0)
  const selectedOptions = approvalAdminUserOptions.value.filter(option =>
    state.settings.approvalAdminUserIds.includes(option.value)
  )
  const merged = [...selectedOptions, ...approvalAdminUserOptions.value, ...nextOptions]
  approvalAdminUserOptions.value = merged.filter((option, index, arr) =>
    arr.findIndex(item => item.value === option.value) === index
  )
}

async function loadApprovalAdminUsers(keyword = '') {
  approvalAdminSearchLoading.value = true
  try {
    const res: any = await getUserList({ keyword, pageIndex: 1, pageSize: 50 })
    const items = res?.items || res?.data?.items || (Array.isArray(res) ? res : [])
    mergeApprovalAdminUserOptions(items)
  } catch {
    // 用户搜索失败不阻断流程定义编辑
  } finally {
    approvalAdminSearchLoading.value = false
  }
}

async function loadSelectedApprovalAdminUsers() {
  const missingIds = state.settings.approvalAdminUserIds.filter(id =>
    !approvalAdminUserOptions.value.some(option => option.value === id)
  )
  if (!missingIds.length) return
  const users = await Promise.all(missingIds.map(id => getUserDetail(id).catch(() => null)))
  mergeApprovalAdminUserOptions(users.filter(Boolean))
}

const onApprovalAdminSearch = debounce((keyword: string) => {
  void loadApprovalAdminUsers(keyword)
}, 300)

async function loadMeta() {
  try {
    const orgId = orgStore.currentOrgId
    if (orgId) {
      const fg: any = await getFlowGroups(orgId).catch(() => [])
      flowGroups.value = fg || []
    }
    const r: any = await getRoleList({ pageIndex: 1, pageSize: 200 }).catch(() => null)
    const list = r?.items || r?.list || r || []
    roleOptions.value = list.map((x: any) => ({
      value: String(x.id ?? x.code ?? x.name),
      label: x.name || x.roleName || String(x.code),
    }))
    await loadApprovalAdminUsers('')
    const fl: any = await getFlowDefinitions({ page: 1, pageSize: 200, status: 'published' }).catch(() => null)
    availableFlows.value = (fl?.items || []).map((d: any) => ({ code: d.flowCode, name: d.flowName }))
  } catch { /* ignore */ }
}

// ==================== 加载草稿 ====================

function buildCardSchemaPayload() {
  return {
    version: 2,
    fields: state.cardSchema,
    components: state.cardComponents,
    header: state.cardHeader,
  }
}

function buildDetailSchemaPayload() {
  // 编辑器只维护当前表（通常是 default）；其余明细表（来自导入链路/外部）在加载时保留，
  // 此处原样透传避免打开草稿再保存即静默丢表。
  const editingTable = {
    detailTableKey: state.detailTableKey || 'default',
    label: '明细',
    columns: state.detailSchema,
  }
  return {
    version: 2,
    tables: [editingTable, ...state.extraDetailTables],
  }
}

async function loadData() {
  await loadMeta()
  if (!flowId.value) {
    // 新建：跳到第一步「基本信息」
    loadError.value = false
    activeStep.value = 0
    history.reset(JSON.parse(JSON.stringify(state)))
    dirty.value = false
    auto.saveState.value = 'saved'
    return
  }
  loading.value = true
  loadError.value = false
  try {
    const [def, draft] = await Promise.all([
      getFlowDefinition(flowId.value),
      getFlowDraftVersion(flowId.value).catch(() => null),
    ])
    const d = def as FlowDefinitionDto
    state.basic.flowName = d.flowName
    state.basic.flowCode = d.flowCode
    state.basic.description = d.description || ''
    state.basic.numberTemplate = d.numberTemplate || ''
    state.basic.titleTemplate = d.titleTemplate || ''
    state.basic.flowGroupId = d.flowGroupId ?? undefined
    state.basic.status = d.status
    publishedVersionNumber.value = d.currentVersion ?? null
    try {
      state.basic.allowedRoles = d.allowedRolesJson ? JSON.parse(d.allowedRolesJson) : []
    } catch { state.basic.allowedRoles = [] }
    try {
      const mp = d.matchPattern ? JSON.parse(d.matchPattern) : null
      state.basic.matchFileNamePattern = typeof mp?.fileNamePattern === 'string' ? mp.fileNamePattern : ''
    } catch { state.basic.matchFileNamePattern = '' }

    if (draft) {
      const dv = draft as FlowVersionDetailDto
      draftVersionNumber.value = dv.versionNumber ?? null
      const cardSchemaPayload = parseCardSchemaPayload(dv.cardSchemaJson)
      state.cardSchema = cardSchemaPayload.fields
      state.cardComponents = cardSchemaPayload.components
      Object.assign(state.cardHeader, defaultCardHeaderConfig(), cardSchemaPayload.header || {})
      const detailTables = parseDetailSchema(dv.detailSchemaJson)
      const editingTable = detailTables.find(t => t.detailTableKey === 'default') || detailTables[0] || null
      state.detailSchema = editingTable?.columns || []
      state.detailTableKey = editingTable?.detailTableKey || 'default'
      state.extraDetailTables = detailTables.filter(t => t !== editingTable)
      state.routes = (dv.routes || []).map(route => ({
        edgeKey: route.edgeKey,
        fromStageKey: route.fromStageKey,
        toStageKey: route.toStageKey,
        routeName: route.routeName,
        conditionJson: route.conditionJson || null,
        priority: route.priority,
        isDefault: route.isDefault,
        status: route.status || 'active',
        failurePolicyJson: route.failurePolicyJson || null,
      }))
      state.dynamicPolicies = (dv.dynamicPolicies || []).map(policy => ({
        policyKey: policy.policyKey,
        sourceStageKey: policy.sourceStageKey,
        policyName: policy.policyName,
        strategyType: policy.strategyType,
        strategyConfigJson: policy.strategyConfigJson || null,
        conditionJson: policy.conditionJson || null,
        triggerTiming: policy.triggerTiming || 'afterRouteBeforeTarget',
        insertPosition: policy.insertPosition || 'beforeTarget',
        continuationStageKey: policy.continuationStageKey || null,
        priority: policy.priority,
        maxInsertCount: policy.maxInsertCount || 20,
        fallbackJson: policy.fallbackJson || JSON.stringify({ type: 'flowAdmin' }),
        status: policy.status || 'active',
      }))
      try {
        if (dv.flowSettingsJson) {
          const fs = JSON.parse(dv.flowSettingsJson)
          Object.assign(state.settings, fs)
          if (!Array.isArray(state.settings.approvalAdminUserIds)) {
            state.settings.approvalAdminUserIds = []
          }
          state.settings.approvalAdminUserIds = state.settings.approvalAdminUserIds
            .map((id: any) => Number(id))
            .filter((id: number) => Number.isFinite(id) && id > 0)
        }
      } catch { /* ignore */ }
      await loadSelectedApprovalAdminUsers()

      state.stages = (dv.stages || []).map(mapStageFromDto)
    } else {
      draftVersionNumber.value = null
    }

    // 编辑场景默认停留在第一步
    activeStep.value = 0

    await nextTick()
    history.reset(JSON.parse(JSON.stringify(state)))
    dirty.value = false
    auto.saveState.value = 'saved'
  } catch {
    loadError.value = true
    message.error('加载流程数据失败')
  } finally {
    loading.value = false
  }
}

function mapStageFromDto(s: any): StageDefinition {
  // 后端 type: approval / cc / human / auto / batchAuto，统一映射为前端 manual / auto
  // 旧数据 batchAuto 转为 auto + processingGranularity='batch'，保持向后兼容
  const t: 'manual' | 'auto' = (s.type === 'auto' || s.type === 'batchAuto') ? 'auto' : 'manual'
  const granularity: 'card' | 'batch' = s.type === 'batchAuto'
    ? 'batch'
    : ((s.processingGranularity === 'batch' ? 'batch' : 'card'))
  const stageInputConfig = parseStageInputConfig(s.inputFieldsJson)
  return {
    id: s.stageKey || 'stg_' + (s.id || Math.random().toString(36).slice(2, 8)),
    name: s.stageName || '',
    type: t,
    processingGranularity: t === 'auto' ? granularity : undefined,
    sortOrder: s.sortOrder || 0,
    approvalMode: s.approvalMode || stageInputConfig.approvalMode?.mode || undefined,
    assigneeStrategy: s.assigneeStrategy || undefined,
    assigneeConfigJson: s.assigneeConfigJson || undefined,
    inputFields: stageInputConfig.inputFields,
    viewProfile: stageInputConfig.viewProfile,
    actionPolicy: stageInputConfig.actionPolicy,
    ccConfigJson: s.ccConfigJson || undefined,
    timeoutHours: s.timeoutHours || undefined,
    pluginRegistryId: s.pluginRegistryId ?? undefined,
    pluginRuleId: s.pluginRuleId ?? undefined,
    failurePolicy: tryParseFailurePolicy(s.failurePolicyJson),
    conditionJson: s.conditionJson || undefined,
    priorityTemplate: s.priorityTemplate ?? undefined,
  }
}
function tryParseFailurePolicy(j?: string | null): 'skip' | 'halt' | 'retry' | undefined {
  if (!j) return undefined
  try {
    const obj = JSON.parse(j)
    return (obj.policy || obj.strategy || obj) as any
  } catch { return undefined }
}

function parseStageInputConfig(inputFieldsJson?: string | null): any {
  if (!inputFieldsJson) return { inputFields: [] }
  try {
    const parsed = JSON.parse(inputFieldsJson)
    if (Array.isArray(parsed)) {
      return { inputFields: parsed.filter((v: any) => typeof v === 'string') }
    }
    if (parsed && typeof parsed === 'object' && parsed.version === 2) {
      return {
        inputFields: Array.isArray(parsed.inputFields) ? parsed.inputFields : [],
        viewProfile: parsed.viewProfile || undefined,
        actionPolicy: parsed.actionPolicy || undefined,
        approvalMode: parsed.approvalMode || undefined,
      }
    }
  } catch {}
  return { inputFields: [] }
}

function hasAdvancedStageConfig(stage: StageDefinition) {
  return Boolean(
    stage.viewProfile?.fieldAccess && Object.keys(stage.viewProfile.fieldAccess).length > 0
    || stage.viewProfile?.detailAccess && Object.keys(stage.viewProfile.detailAccess).length > 0
    || stage.viewProfile?.componentAccess && Object.keys(stage.viewProfile.componentAccess).length > 0
    || stage.viewProfile?.summary?.fields?.length
    || stage.actionPolicy?.allowedActions?.length
  )
}

function buildStageInputFieldsJson(stage: StageDefinition): string | null {
  const inputFields = stage.inputFields || []
  if (stage.type !== 'manual') return inputFields.length ? JSON.stringify(inputFields) : null
  if (!hasAdvancedStageConfig(stage)) {
    return inputFields.length ? JSON.stringify(inputFields) : null
  }

  return JSON.stringify({
    version: 2,
    inputFields,
    viewProfile: stage.viewProfile || { fieldAccess: {}, detailAccess: {}, summary: { fields: [] } },
    actionPolicy: stage.actionPolicy || { allowedActions: [] },
    approvalMode: { mode: stage.approvalMode || 'single' },
  })
}

// ==================== 保存逻辑 ====================

function buildStageRequests(): StageDefinitionRequest[] {
  return state.stages.map((s, i) => ({
    stageKey: s.id,
    name: s.name,
    // 人工节点必须提交 'human'：引擎退回指定节点（ReturnToStageRuntime.IsHuman）只认 'human'，
    // 存 'approval' 会导致该节点不能作为退回目标、退回时下游不作废
    type: s.type === 'auto' ? 'auto' : 'human',
    processingGranularity: s.type === 'auto' ? (s.processingGranularity || 'card') : undefined,
    sortOrder: i + 1,
    approvalMode: s.approvalMode || null,
    assigneeStrategy: s.assigneeStrategy || null,
    assigneeConfigJson: s.assigneeConfigJson || null,
    conditionJson: s.conditionJson || null,
    inputFieldsJson: buildStageInputFieldsJson(s),
    // 废除 autoPluginName / autoPluginConfigJson，改为插件注册+规则引用
    pluginRegistryId: s.type === 'auto' ? (s.pluginRegistryId ?? null) : null,
    pluginRuleId: s.type === 'auto' ? (s.pluginRuleId ?? null) : null,
    failurePolicyJson: s.failurePolicy ? JSON.stringify({ policy: s.failurePolicy }) : null,
    ccConfigJson: s.ccConfigJson || null,
    timeoutHours: s.timeoutHours || null,
    // 不发该字段会被后端"全删全建"置空，存量节点的优先级模板会静默丢失
    priorityTemplate: s.priorityTemplate ?? null,
  }))
}

function buildRouteRequests(): StageRouteRuleRequest[] {
  const routes = state.routes
    .filter(route => route.fromStageKey && route.toStageKey)
    .map((route, index) => ({
      edgeKey: route.edgeKey || genStableKey('edge'),
      fromStageKey: route.fromStageKey,
      toStageKey: route.toStageKey,
      routeName: route.routeName || (route.isDefault ? '其他情况' : `条件分支 ${index + 1}`),
      conditionJson: route.isDefault ? null : route.conditionJson || null,
      priority: route.priority || index + 1,
      isDefault: Boolean(route.isDefault),
      status: route.status || 'active',
      failurePolicyJson: route.failurePolicyJson || null,
    }))
  // 后端发布校验要求同一来源节点的优先级不重复；删边/加边后可能出现重复值，
  // 按现有优先级保序重编为 1..n（默认分支固定排最后兜底）
  const byFrom = new Map<string, typeof routes>()
  for (const route of routes) {
    const group = byFrom.get(route.fromStageKey) || []
    group.push(route)
    byFrom.set(route.fromStageKey, group)
  }
  for (const group of byFrom.values()) {
    group
      .sort((a, b) => Number(a.isDefault) - Number(b.isDefault) || a.priority - b.priority)
      .forEach((route, i) => { route.priority = i + 1 })
  }
  return routes
}

function buildDynamicPolicyRequests(): DynamicStagePolicyRequest[] {
  return state.dynamicPolicies
    .filter(policy => policy.sourceStageKey && policy.policyName)
    .map((policy, index) => ({
      policyKey: policy.policyKey || genStableKey('pol'),
      sourceStageKey: policy.sourceStageKey,
      policyName: policy.policyName,
      strategyType: policy.strategyType || 'fixedUsers',
      strategyConfigJson: policy.strategyConfigJson || '{}',
      conditionJson: policy.conditionJson || null,
      triggerTiming: policy.triggerTiming || 'afterRouteBeforeTarget',
      insertPosition: policy.insertPosition || 'beforeTarget',
      continuationStageKey: policy.continuationStageKey || null,
      priority: policy.priority || index + 1,
      maxInsertCount: policy.maxInsertCount || 20,
      fallbackJson: policy.fallbackJson || JSON.stringify({ type: 'flowAdmin' }),
      status: policy.status || 'active',
    }))
}

async function ensureDefinitionId(): Promise<number | null> {
  if (flowId.value) return flowId.value
  if (!state.basic.flowName.trim() || !state.basic.flowCode.trim()) return null
  const created: any = await createFlowDefinition({
    flowName: state.basic.flowName,
    flowCode: state.basic.flowCode,
    description: state.basic.description || undefined,
    numberTemplate: state.basic.numberTemplate || undefined,
    titleTemplate: state.basic.titleTemplate || undefined,
    flowGroupId: state.basic.flowGroupId || undefined,
    allowedRolesJson: state.basic.allowedRoles.length ? JSON.stringify(state.basic.allowedRoles) : undefined,
    matchPattern: state.basic.matchFileNamePattern.trim()
      ? JSON.stringify({ fileNamePattern: state.basic.matchFileNamePattern.trim() })
      : undefined,
    orgId: orgStore.currentOrgId || undefined,
  })
  const newId = created?.id
  if (newId) {
    await router.replace({ path: '/cardflow/definition/edit', query: { id: String(newId) } })
  }
  return newId || null
}

async function doSilentSave(): Promise<number | undefined> {
  // 自动保存：仅当有 ID 时（避免误创建）；新建时仅当填齐 name+code 才创建
  const seqAtStart = editSeq
  let id = flowId.value
  if (!id) {
    if (!state.basic.flowName.trim() || !state.basic.flowCode.trim()) return undefined
    id = await ensureDefinitionId() || undefined as any
    if (!id) return undefined
  } else {
    await updateFlowDefinition(id, {
      flowName: state.basic.flowName,
      description: state.basic.description || undefined,
      numberTemplate: state.basic.numberTemplate || undefined,
      titleTemplate: state.basic.titleTemplate || undefined,
      flowGroupId: state.basic.flowGroupId || undefined,
      allowedRolesJson: JSON.stringify(state.basic.allowedRoles),
      matchPattern: state.basic.matchFileNamePattern.trim()
        ? JSON.stringify({ fileNamePattern: state.basic.matchFileNamePattern.trim() })
        : '',
    })
  }
  await saveFlowDraftVersion(id!, {
    cardSchemaJson: JSON.stringify(buildCardSchemaPayload()),
    detailSchemaJson: JSON.stringify(buildDetailSchemaPayload()),
    flowSettingsJson: JSON.stringify(state.settings),
    stages: buildStageRequests(),
    routes: buildRouteRequests(),
    dynamicPolicies: buildDynamicPolicyRequests(),
  })
  // 保存在途期间产生的新编辑不能被标记为已保存
  if (editSeq === seqAtStart) dirty.value = false
  return id!
}

// 保存串行化：30s 定时保存、Ctrl+S、发布三条路径可能并发触发，
// 并发提交 saveFlowDraftVersion（后端全删全建）会互相踩踏，这里统一排队
let saveChain: Promise<number | undefined> = Promise.resolve(undefined)
function silentSave(): Promise<number | undefined> {
  const next = saveChain.catch(() => undefined).then(() => doSilentSave())
  saveChain = next
  return next
}

async function handleSaveDraft() {
  if (!state.basic.flowName.trim()) {
    errors.basic = true
    activeStep.value = STEP_BASIC
    message.warning('请输入流程名称')
    return
  }
  if (!state.basic.flowCode.trim()) {
    errors.basic = true
    activeStep.value = STEP_BASIC
    message.warning('请输入流程编码')
    return
  }
  errors.basic = false
  auto.saveState.value = 'saving'
  try {
    await silentSave()
    auto.saveState.value = dirty.value ? 'dirty' : 'saved'
    message.success('草稿已保存')
  } catch (e) {
    auto.saveState.value = 'error'
    // 拦截器已弹出后端具体错误（如插件规则不存在、StageKey 冲突），此处不重复弹泛化提示
    console.error('[FlowDefinition] 保存草稿失败:', e)
  }
}

// ==================== 发布 ====================

// 发布/预览配置校验单一真源：utils/cardflowDiagnostics.validatePublishConfig（与规则健康面板同源）
function validateCardFlow2Config() {
  return validatePublishConfig({
    stages: state.stages,
    routes: state.routes,
    dynamicPolicies: state.dynamicPolicies,
    cardSchema: state.cardSchema,
    detailSchema: state.detailSchema,
    cardComponents: state.cardComponents,
    approvalAdminUserIds: state.settings.approvalAdminUserIds,
  })
}

const previewConfigWarnings = computed(() => validateCardFlow2Config())

function validateForPublish(): boolean {
  errors.basic = false
  errors.schema = false
  errors.stages = false
  errors.condition = false
  const msgs: string[] = []
  if (!state.basic.flowName.trim() || !state.basic.flowCode.trim()) {
    errors.basic = true
    msgs.push('基本信息不完整')
  }
  if (state.cardSchema.length === 0) {
    errors.schema = true
    msgs.push('至少需要一个卡片字段')
  }
  if (state.stages.length === 0) {
    errors.stages = true
    msgs.push('至少需要一个流程节点')
  }
  // 简单条件语法校验
  for (const s of state.stages) {
    if (!s.conditionJson) continue
    try {
      const g = JSON.parse(s.conditionJson)
      if (!g || typeof g !== 'object' || !Array.isArray(g.conditions)) {
        errors.condition = true
        msgs.push(`节点[${s.name}]条件语法错误`)
      }
    } catch {
      errors.condition = true
      msgs.push(`节点[${s.name}]条件 JSON 解析失败`)
    }
  }
  const cardFlow2Issues = validateCardFlow2Config()
  if (cardFlow2Issues.length) {
    errors.stages = true
    msgs.push(...cardFlow2Issues.map(issue => issue.message))
  }
  if (msgs.length) {
    message.error('发布前校验失败：' + msgs.join('；'))
    // 自动跳转到第一个出错的步骤
    if (errors.basic) activeStep.value = STEP_BASIC
    else if (errors.schema) activeStep.value = STEP_SCHEMA
    else if (errors.stages || errors.condition) activeStep.value = STEP_STAGES
    return false
  }
  return true
}

async function handlePublish() {
  if (!validateForPublish()) return
  Modal.confirm({
    title: '确认发布？',
    content: `发布后将生成新版本 v${(publishedVersionNumber.value || 0) + 1} 并立即生效，新发起的卡片将使用该版本。`,
    okText: '发布',
    cancelText: '取消',
    async onOk() {
      publishing.value = true
      try {
        const savedId = await silentSave()
        if (!savedId) {
          message.warning('请先填写流程名称和编码')
          return
        }
        await publishFlowDefinition(savedId)
        message.success('已发布')
        router.push('/cardflow/definitions')
      } catch (e) {
        // 拦截器已弹出后端具体校验错误（环/不可达/默认分支等），此处不再重复弹泛化提示
        console.error('[FlowDefinition] 发布失败:', e)
      } finally {
        publishing.value = false
      }
    },
  })
}

// ==================== 撤销/重做 ====================

function applyState(snap: FlowState) {
  silently(() => {
    Object.assign(state.basic, snap.basic)
    state.cardSchema = JSON.parse(JSON.stringify(snap.cardSchema))
    state.detailSchema = JSON.parse(JSON.stringify(snap.detailSchema))
    state.detailTableKey = snap.detailTableKey || 'default'
    state.extraDetailTables = JSON.parse(JSON.stringify(snap.extraDetailTables || []))
    Object.assign(state.cardHeader, defaultCardHeaderConfig(), snap.cardHeader || {})
    state.cardComponents = JSON.parse(JSON.stringify(snap.cardComponents || []))
    state.stages = JSON.parse(JSON.stringify(snap.stages))
    state.routes = JSON.parse(JSON.stringify(snap.routes || []))
    state.dynamicPolicies = JSON.parse(JSON.stringify(snap.dynamicPolicies || []))
    Object.assign(state.settings, snap.settings)
  })
}

function doUndo() {
  // 先提交防抖窗口内未入栈的编辑，否则 500ms 内按撤销会跳过最新编辑且无法重做
  flushPending()
  const s = history.undo()
  if (s) applyState(s)
}
function doRedo() {
  flushPending()
  const s = history.redo()
  if (s) applyState(s)
}

// ==================== 预览 ====================

// 预览改为末步「预演与校验」内嵌渲染，工具栏「预览」按钮直接跳到该步
function openPreview() {
  if (!selectedPreviewStageId.value && state.stages.length) {
    selectedPreviewStageId.value = state.stages[0].id
  }
  activeStep.value = STEP_PREVIEW
}

function reloadFlowDefinition() {
  void loadData()
}

function goPreviewReadinessStep(item: PreviewReadinessItem) {
  if (item.key === 'loaded') {
    reloadFlowDefinition()
    return
  }
  if (typeof item.step === 'number') {
    activeStep.value = item.step
  }
}

// ==================== 快捷键 ====================

function onKeyDown(e: KeyboardEvent) {
  const ctrl = e.ctrlKey || e.metaKey
  if (ctrl && e.key.toLowerCase() === 's') {
    e.preventDefault(); void handleSaveDraft(); return
  }
  if (ctrl && e.shiftKey && e.key.toLowerCase() === 'z') {
    e.preventDefault(); doRedo(); return
  }
  if (ctrl && e.key.toLowerCase() === 'z') {
    e.preventDefault(); doUndo(); return
  }
  if (ctrl && e.key === 'Enter') {
    e.preventDefault(); void handlePublish(); return
  }
}

function onBeforeUnload(e: BeforeUnloadEvent) {
  if (dirty.value) {
    e.preventDefault()
    e.returnValue = ''
  }
}

onMounted(() => {
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('beforeunload', onBeforeUnload)
  void loadData()
})

import { onBeforeUnmount } from 'vue'
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
  window.removeEventListener('beforeunload', onBeforeUnload)
})

// ==================== 流程设置子操作 ====================

function addPrerequisite() {
  state.settings.prerequisites.push({ flowCode: '', required: true })
}
function removePrerequisite(i: number) {
  state.settings.prerequisites.splice(i, 1)
}

function goBack() {
  if (!dirty.value) {
    router.push('/cardflow/definitions')
    return
  }
  Modal.confirm({
    title: '有未保存的更改',
    content: '离开将丢失未保存的更改，确定离开吗？',
    okText: '确定离开',
    okType: 'danger',
    cancelText: '取消',
    onOk: () => router.push('/cardflow/definitions'),
  })
}
</script>

<template>
  <div class="fdef-edit">
    <PageHeader :title="isNew ? '新建流程定义' : '编辑流程定义'">
      <template #left>
        <a-button type="link" @click="goBack" style="padding: 0 4px;">
          <ArrowLeftOutlined />返回
        </a-button>
        <span class="tb-title">
          {{ isNew ? '新建流程' : '编辑' }}
          <strong v-if="!isNew">{{ state.basic.flowName || '未命名流程' }}</strong>
        </span>
        <span
          v-if="draftVersionNumber || publishedVersionNumber"
          class="tb-version-context"
          :title="draftVersionNumber && publishedVersionNumber
            ? `正在编辑草稿 v${draftVersionNumber}，当前已发布版本为 v${publishedVersionNumber}。修改需发布后才会生效。`
            : draftVersionNumber
              ? `草稿 v${draftVersionNumber}，尚未发布`
              : `已发布 v${publishedVersionNumber}`"
        >
          <span v-if="draftVersionNumber" class="tb-version-context__draft">草稿 v{{ draftVersionNumber }}</span>
          <span v-if="publishedVersionNumber" class="tb-version-context__published">已发布 v{{ publishedVersionNumber }}</span>
          <span v-if="draftVersionNumber && publishedVersionNumber" class="tb-version-context__note">发布后生效</span>
        </span>
      </template>

      <template #actions>
        <span class="tb-history-group" aria-label="历史操作">
          <a-tooltip title="撤销 (Ctrl+Z)">
            <button
              class="tb-history-btn tb-history-btn--undo"
              :disabled="!history.canUndo.value"
              aria-label="撤销"
              @click="doUndo"
            >
              <RollbackOutlined />
              <span>撤销</span>
            </button>
          </a-tooltip>
          <a-tooltip title="重做 (Ctrl+Shift+Z)">
            <button
              class="tb-history-btn tb-history-btn--redo"
              :disabled="!history.canRedo.value"
              aria-label="重做"
              @click="doRedo"
            >
              <ArrowRightOutlined />
              <span>重做</span>
            </button>
          </a-tooltip>
        </span>

        <span class="tb-divider" />

        <a-button @click="handleSaveDraft">
          <template #icon><SaveOutlined /></template>
          保存草稿
        </a-button>
        <a-button @click="openPreview">
          <template #icon><EyeOutlined /></template>
          预演
        </a-button>
        <a-button type="primary" :loading="publishing" @click="handlePublish">
          <template #icon><SendOutlined /></template>
          发布
        </a-button>

        <SaveStateChip :state="auto.saveState.value" :saved-at="auto.lastSavedAt.value" @retry="handleSaveDraft" />
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 顶部步骤条：自由跳转 + 状态徽章 -->
      <div class="fdef-steps-wrap">
        <a-steps
          :current="activeStep"
          size="default"
          class="fdef-steps"
          @change="handleStepChange"
        >
          <a-step
            v-for="(s, idx) in STEPS"
            :key="s.key"
            :title="s.title"
            :description="s.desc"
            :status="stepStatus[idx]"
          />
        </a-steps>
      </div>

      <!-- 步骤内容区：v-show 保留子组件状态，避免切换丢失编辑数据 -->
      <div class="fdef-step-body">
        <!-- 步骤：基本信息 -->
        <div v-show="activeStep === STEP_BASIC" class="fdef-step" :class="{ 'fdef-step--err': errors.basic }">
          <div class="fdef-basic-config">
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">流程名称 <span class="fdef-required-star">*</span></div>
              <a-input v-model:value="state.basic.flowName" placeholder="如：费用报销" />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">编码 <span v-if="!isNew" class="fdef-fc-item__hint">创建后不可修改</span></div>
              <a-input v-model:value="state.basic.flowCode" placeholder="snake_case" :disabled="!isNew" />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">编号模板</div>
              <a-input v-model:value="state.basic.numberTemplate" placeholder="EXP-{YYYYMMDD}-{SEQ}" />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">标题模板</div>
              <a-input v-model:value="state.basic.titleTemplate" placeholder="{initiator} 的报销-{amount}元" />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">所属流程组</div>
              <a-select
                v-model:value="state.basic.flowGroupId"
                allow-clear placeholder="选择流程组"
                :options="flowGroups.map(g => ({ value: g.id, label: g.groupName }))"
              />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">可发起角色</div>
              <a-select
                v-model:value="state.basic.allowedRoles"
                mode="multiple" placeholder="选择允许发起此流程的角色"
                :options="roleOptions"
              />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">
                导入文件名匹配
                <span class="fdef-fc-item__hint">导入文件按列头匹配失败时，按文件名通配符回退匹配到本流程（仅导入类流程需要）</span>
              </div>
              <a-input
                v-model:value="state.basic.matchFileNamePattern"
                placeholder="如：*韵达*总部交易*，支持 * 通配符，留空表示不参与文件名匹配"
                allow-clear
              />
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">状态</div>
              <StatusTag :type="FLOW_STATUS_META[state.basic.status as FlowStatus]?.tagType ?? 'default'">
                {{ FLOW_STATUS_META[state.basic.status as FlowStatus]?.text ?? '草稿' }}
              </StatusTag>
            </div>
            <div class="fdef-fc-item">
              <div class="fdef-fc-item__label">描述</div>
              <a-textarea v-model:value="state.basic.description" :rows="2" placeholder="（可选）" />
            </div>
          </div>
        </div>

        <!-- 步骤：字段设计 -->
        <div v-show="activeStep === STEP_SCHEMA" class="fdef-step" :class="{ 'fdef-step--err': errors.schema }">
          <BaseCard no-padding class="fdef-schema-guide-card">
            <div class="fdef-schema-guide">
              <span class="fdef-schema-guide__item">
                <strong>字段 = 数据结构</strong>
                <em>保存数据、参与条件路由和统计</em>
              </span>
              <!-- V2 冻结：卡片视图引导项随桶B入口下线（简化瘦身 2026-06-16），恢复入口时一并放开 -->
              <template v-if="false">
                <span class="fdef-schema-guide__arrow"><RightOutlined /></span>
                <span class="fdef-schema-guide__item">
                  <strong>下一步配置卡片视图</strong>
                  <em>把字段、明细、关系和快照编排成审批人看到的卡片</em>
                </span>
              </template>
              <span class="fdef-schema-guide__arrow"><RightOutlined /></span>
              <span class="fdef-schema-guide__item">
                <strong>节点权限</strong>
                <em>到节点链里设置可见、可编辑、脱敏</em>
              </span>
            </div>
          </BaseCard>

          <a-row :gutter="16" class="fdef-schema-cols">
            <a-col :span="12">
              <SchemaFieldEditor
                v-model="state.cardSchema"
                title="卡片字段"
                :available-flows="availableFlows"
              />
            </a-col>
            <a-col :span="12">
              <a-alert
                v-if="state.extraDetailTables.length > 0"
                type="warning"
                show-icon
                :message="`本流程含 ${state.extraDetailTables.length + 1} 张明细表，编辑器仅维护 default 表；其余 ${state.extraDetailTables.length} 张将原样保留`"
                style="margin-bottom: 8px"
              />
              <SchemaFieldEditor
                v-model="state.detailSchema"
                title="明细行字段"
                :available-flows="availableFlows"
              />
            </a-col>
          </a-row>
        </div>

        <!-- V2 冻结：卡片视图低代码编排工作台（简化瘦身桶B 2026-06-16）。
             入口暂下线（步骤条已移除该步），整块工作台连同运行态预览 modal 用 v-show=false 保留代码
             （用 v-show 而非 v-if：保持与原实现一致的挂载与模板类型窄化环境，v-if=false 会使
             vue-tsc 丢失 selectedCardComponent 等窄化导致 TS18047）；
             cardComponents/cardHeader 的加载、保存与发布校验数据通道不变，存量已编排组件继续生效 -->
        <div v-show="false" class="fdef-step fdef-step--card-view">
          <div class="fdef-card-view-workbench">
            <aside class="fdef-card-view-library">
              <CardComponentCatalog
                :schema-fields="state.cardSchema"
                :detail-schema-fields="state.detailSchema"
                @add="addCardComponent"
              />
            </aside>

            <section class="fdef-card-canvas" aria-label="卡片视图画布">
              <header class="page-section__title fdef-card-canvas__head">
                <div>
                  <strong>运行态卡片画布</strong>
                  <span>拖拽组件 · 编辑组件 · 所见即所得，{{ state.cardComponents.length }} 个已编排</span>
                </div>
                <a-button
                  size="small"
                  aria-label="预览运行态卡片"
                  @click="openCardRuntimePreview"
                >
                  <template #icon><EyeOutlined /></template>
                  预览
                </a-button>
              </header>

              <div class="fdef-card-canvas__stage">
                <div class="fdef-card-canvas__surface">
                  <div
                    class="fdef-card-canvas__surface-header"
                    :class="[
                      cardHeaderSelected ? 'fdef-card-canvas__surface-header--selected' : '',
                      `fdef-card-canvas__surface-header--${state.cardHeader.align || 'left'}`,
                    ]"
                    role="button"
                    tabindex="0"
                    aria-label="配置卡片头部"
                    @click="selectCardHeader"
                    @keyup.enter="selectCardHeader"
                  >
                    <span class="fdef-card-canvas__surface-title">{{ cardHeaderTitle }}</span>
                    <span v-if="cardHeaderShowSubtitle" class="fdef-card-canvas__surface-code">{{ cardHeaderSubtitle }}</span>
                    <a-tag v-if="cardHeaderShowStatus" size="small">{{ state.basic.status || 'draft' }}</a-tag>
                  </div>

                  <draggable
                    v-model="state.cardComponents"
                    item-key="id"
                    :group="{ name: 'card-components', pull: true, put: true }"
                    handle=".fdef-card-canvas-item__handle"
                    class="fdef-card-canvas__list"
                    ghost-class="fdef-card-canvas-item--ghost"
                    chosen-class="fdef-card-canvas-item--chosen"
                    @change="handleCardCanvasChange"
                  >
                    <template #item="{ element: component }">
                      <article
                        class="fdef-card-canvas-item"
                        :class="[
                          selectedCardComponent?.id === component.id ? 'fdef-card-canvas-item--selected' : '',
                          `fdef-card-canvas-item--${component.layout?.width || 'full'}`,
                        ]"
                        role="button"
                        tabindex="0"
                        @click="selectCardComponent(component.id)"
                        @keyup.enter="selectCardComponent(component.id)"
                      >
                        <i class="fdef-card-canvas-item__handle" aria-hidden="true"><HolderOutlined /></i>
                        <div
                          v-if="selectedCardComponent?.id === component.id"
                          class="fdef-card-canvas-item__inline-actions"
                          aria-label="组件快捷操作"
                        >
                          <button
                            type="button"
                            class="fdef-card-canvas-item__icon-btn"
                            aria-label="复制组件"
                            title="复制"
                            @click.stop="duplicateCardComponent(component)"
                          >
                            <CopyOutlined />
                          </button>
                          <button
                            type="button"
                            class="fdef-card-canvas-item__icon-btn fdef-card-canvas-item__icon-btn--danger"
                            aria-label="删除组件"
                            title="删除"
                            @click.stop="deleteCardComponent(component.id)"
                          >
                            <DeleteOutlined />
                          </button>
                        </div>
                        <div class="fdef-card-canvas-item__runtime">
                          <CardComponentRenderer
                            :components="canvasRuntimeComponentsFor(component)"
                            :model-value="previewSampleData"
                            :detail-rows="previewDetailRows"
                            mode="view"
                            platform="pc"
                            preview-variant="designer"
                            is-admin
                          />
                        </div>
                      </article>
                    </template>
                    <template #footer>
                      <EmptyState
                        v-if="state.cardComponents.length === 0"
                        size="small"
                        title="从左侧组件库拖拽组件到这里，搭建审批人看到的卡片视图。"
                        class="fdef-card-canvas__empty"
                      />
                    </template>
                  </draggable>
                </div>
              </div>
            </section>

            <aside class="fdef-component-inspector">
              <section class="fdef-component-inspector__panel">
                <header>
                  <strong>{{ cardHeaderSelected ? '卡片头部属性' : '组件属性' }}</strong>
                  <span>{{ cardHeaderSelected ? '头部不是组件，用于定义审批卡片的身份信息。' : '选中画布组件后，在这里编辑展示、绑定和默认权限。' }}</span>
                </header>

                <div v-if="cardHeaderSelected" class="fdef-component-inspector__form">
                  <label>
                    <span>主标题来源</span>
                    <a-select
                      :value="state.cardHeader.titleMode"
                      style="width: 100%"
                      @change="(value: any) => patchCardHeader({ titleMode: value })"
                    >
                      <a-select-option value="flowName">流程名称</a-select-option>
                      <a-select-option value="fixed">固定文本</a-select-option>
                      <a-select-option value="field">绑定字段</a-select-option>
                      <a-select-option value="template">模板表达式</a-select-option>
                    </a-select>
                  </label>
                  <label v-if="state.cardHeader.titleMode === 'fixed'">
                    <span>主标题文本</span>
                    <a-input
                      :value="state.cardHeader.titleText ?? undefined"
                      @update:value="(value: string) => patchCardHeader({ titleText: value })"
                    />
                  </label>
                  <label v-if="state.cardHeader.titleMode === 'field'">
                    <span>主标题字段</span>
                    <a-select
                      :value="state.cardHeader.titleFieldKey ?? undefined"
                      :options="state.cardSchema.map(field => ({ value: field.key, label: field.label || field.key }))"
                      style="width: 100%"
                      allow-clear
                      @change="(value: any) => patchCardHeader({ titleFieldKey: value })"
                    />
                  </label>
                  <label v-if="state.cardHeader.titleMode === 'template'">
                    <span>主标题模板</span>
                    <a-input
                      :value="state.cardHeader.titleTemplate ?? undefined"
                      placeholder="{费用类型}报销"
                      @update:value="(value: string) => patchCardHeader({ titleTemplate: value })"
                    />
                  </label>
                  <label>
                    <span>副标题来源</span>
                    <a-select
                      :value="state.cardHeader.subtitleMode"
                      style="width: 100%"
                      @change="(value: any) => patchCardHeader({ subtitleMode: value })"
                    >
                      <a-select-option value="flowCode">流程编码</a-select-option>
                      <a-select-option value="fixed">固定文本</a-select-option>
                      <a-select-option value="field">绑定字段</a-select-option>
                      <a-select-option value="template">模板表达式</a-select-option>
                      <a-select-option value="hidden">不显示</a-select-option>
                    </a-select>
                  </label>
                  <label v-if="state.cardHeader.subtitleMode === 'fixed'">
                    <span>副标题文本</span>
                    <a-input
                      :value="state.cardHeader.subtitleText ?? undefined"
                      @update:value="(value: string) => patchCardHeader({ subtitleText: value })"
                    />
                  </label>
                  <label v-if="state.cardHeader.subtitleMode === 'field'">
                    <span>副标题字段</span>
                    <a-select
                      :value="state.cardHeader.subtitleFieldKey ?? undefined"
                      :options="state.cardSchema.map(field => ({ value: field.key, label: field.label || field.key }))"
                      style="width: 100%"
                      allow-clear
                      @change="(value: any) => patchCardHeader({ subtitleFieldKey: value })"
                    />
                  </label>
                  <label v-if="state.cardHeader.subtitleMode === 'template'">
                    <span>副标题模板</span>
                    <a-input
                      :value="state.cardHeader.subtitleTemplate ?? undefined"
                      placeholder="{flowCode} · {申请人}"
                      @update:value="(value: string) => patchCardHeader({ subtitleTemplate: value })"
                    />
                  </label>
                  <label>
                    <span>头部对齐</span>
                    <div class="fdef-layout-toggle" role="radiogroup" aria-label="头部对齐">
                      <button
                        type="button"
                        class="fdef-layout-toggle__btn"
                        :class="{ 'is-active': (state.cardHeader.align || 'left') === 'left' }"
                        @click="patchCardHeader({ align: 'left' })"
                      >
                        左对齐
                      </button>
                      <button
                        type="button"
                        class="fdef-layout-toggle__btn"
                        :class="{ 'is-active': state.cardHeader.align === 'center' }"
                        @click="patchCardHeader({ align: 'center' })"
                      >
                        居中
                      </button>
                    </div>
                  </label>
                  <a-checkbox
                    :checked="state.cardHeader.showStatus === true"
                    @change="(event: any) => patchCardHeader({ showStatus: event.target.checked })"
                  >
                    显示状态标签
                  </a-checkbox>
                </div>

                <div v-else-if="selectedCardComponent" class="fdef-component-inspector__form">
                  <label>
                    <span>组件标题</span>
                    <a-input
                      :value="selectedCardComponent.title"
                      @update:value="(value: string) => patchCardComponent({ title: value })"
                    />
                  </label>
                  <label>
                    <span>绑定来源</span>
                    <a-select
                      :value="selectedCardComponent.binding?.source"
                      style="width: 100%"
                      @change="(value: any) => patchCardComponentBinding({ source: value })"
                    >
                      <a-select-option value="cardField">卡片字段</a-select-option>
                      <a-select-option value="detailTable">明细表</a-select-option>
                      <a-select-option value="detailSummary">明细汇总</a-select-option>
                      <a-select-option value="relation">关联卡片</a-select-option>
                      <a-select-option value="snapshot">运行快照</a-select-option>
                      <a-select-option value="static">静态内容</a-select-option>
                    </a-select>
                  </label>
                  <label v-if="selectedCardComponent.binding?.source === 'cardField'">
                    <span>字段</span>
                    <a-select
                      :value="selectedCardComponent.binding?.fieldKey"
                      :options="state.cardSchema.map(field => ({ value: field.key, label: field.label || field.key }))"
                      style="width: 100%"
                      allow-clear
                      @change="(value: any) => patchCardComponentBinding({ fieldKey: value })"
                    />
                  </label>
                  <label>
                    <span>默认权限</span>
                    <a-select
                      :value="selectedCardComponent.access || 'readonly'"
                      style="width: 100%"
                      @change="(value: any) => patchCardComponent({ access: value })"
                    >
                      <a-select-option value="readonly">只读</a-select-option>
                      <a-select-option value="editable">可编辑</a-select-option>
                      <a-select-option value="required">必填</a-select-option>
                      <a-select-option value="masked">脱敏</a-select-option>
                      <a-select-option value="hidden">隐藏</a-select-option>
                    </a-select>
                  </label>
                  <label>
                    <span>画布宽度</span>
                    <div class="fdef-layout-toggle" role="radiogroup" aria-label="画布宽度">
                      <button
                        type="button"
                        class="fdef-layout-toggle__btn"
                        :class="{ 'is-active': (selectedCardComponent.layout?.width || 'full') === 'full' }"
                        aria-label="画布宽度整行"
                        @click="patchCardComponentLayout({ width: 'full' })"
                      >
                        整行
                      </button>
                      <button
                        type="button"
                        class="fdef-layout-toggle__btn"
                        :class="{ 'is-active': selectedCardComponent.layout?.width === 'half' }"
                        aria-label="画布宽度半行"
                        @click="patchCardComponentLayout({ width: 'half' })"
                      >
                        半行
                      </button>
                      <button
                        type="button"
                        class="fdef-layout-toggle__btn"
                        :class="{ 'is-active': selectedCardComponent.layout?.width === 'compact' }"
                        aria-label="画布宽度紧凑"
                        @click="patchCardComponentLayout({ width: 'compact' })"
                      >
                        紧凑
                      </button>
                    </div>
                  </label>
                  <label v-if="selectedCardComponent.type === 'sectionTitle'">
                    <span>说明文字</span>
                    <a-input
                      :value="selectedCardComponent.props?.description"
                      @update:value="(value: string) => patchCardComponentProps({ description: value })"
                    />
                  </label>
                  <label v-if="selectedCardComponent.type === 'textBlock'">
                    <span>正文</span>
                    <a-textarea
                      :value="selectedCardComponent.props?.body"
                      :auto-size="{ minRows: 3, maxRows: 6 }"
                      @update:value="(value: string) => patchCardComponentProps({ body: value })"
                    />
                  </label>
                  <a-button block @click="openComponentConfig(selectedCardComponent.id)">
                    打开高级配置
                  </a-button>
                </div>

                <EmptyState
                  v-else
                  size="small"
                  title="点击画布中的组件，或从左侧拖入一个组件。"
                  class="fdef-component-inspector__empty"
                />
              </section>
            </aside>
          </div>

          <a-modal
            v-model:open="cardRuntimePreviewOpen"
            title="运行态卡片预览"
            width="1040px"
            :footer="null"
            :destroy-on-close="false"
            :body-style="{ padding: 0 }"
            class="fdef-runtime-preview-modal"
          >
            <div class="fdef-runtime-preview">
              <header class="fdef-runtime-preview__toolbar">
                <div>
                  <strong>{{ cardHeaderTitle }}</strong>
                  <span>{{ cardRuntimePreviewStage?.name || '默认只读视图' }} · {{ cardRuntimePreviewVisibleComponents.length }} 个可见组件</span>
                </div>
                <label>
                  <span>节点视角</span>
                  <a-select
                    v-model:value="cardRuntimePreviewStageId"
                    :options="previewStageOptions"
                    allow-clear
                    placeholder="默认只读视图"
                    style="width: 220px"
                  />
                </label>
                <div class="fdef-runtime-preview__mode-toggle" role="radiogroup" aria-label="运行态预览模式">
                  <button
                    type="button"
                    class="fdef-runtime-preview__mode-btn"
                    :class="{ 'is-active': cardRuntimePreviewMode === 'view' }"
                    @click="cardRuntimePreviewMode = 'view'"
                  >
                    展示态
                  </button>
                  <button
                    type="button"
                    class="fdef-runtime-preview__mode-btn"
                    :class="{ 'is-active': cardRuntimePreviewMode === 'edit' }"
                    :disabled="!cardRuntimePreviewCanEdit"
                    title="当前节点存在可编辑或必填组件时可切换"
                    @click="cardRuntimePreviewMode = 'edit'"
                  >
                    处理态
                  </button>
                </div>
              </header>

              <div class="fdef-runtime-preview__body">
                <section class="fdef-runtime-preview__stage" aria-label="卡片运行态展现">
                  <div class="fdef-runtime-preview__card">
                    <div
                      class="fdef-preview-card__header"
                      :class="`fdef-preview-card__header--${state.cardHeader.align || 'left'}`"
                    >
                      <span class="fdef-preview-card__title">{{ cardHeaderTitle }}</span>
                      <span v-if="cardHeaderShowSubtitle" class="fdef-preview-card__code">{{ cardHeaderSubtitle }}</span>
                      <a-tag v-if="cardHeaderShowStatus" size="small">{{ state.basic.status || 'draft' }}</a-tag>
                    </div>
                    <div v-if="cardRuntimePreviewVisibleComponents.length" class="fdef-runtime-preview__card-body">
                      <CardComponentRenderer
                        :components="cardRuntimePreviewComponents"
                        :model-value="runtimePreviewSampleData"
                        :detail-rows="runtimePreviewDetailRows"
                        :mode="cardRuntimePreviewMode"
                        platform="pc"
                        @update:model-value="updateRuntimePreviewSampleData"
                        @update:detail-rows="updateRuntimePreviewDetailRows"
                      />
                    </div>
                    <EmptyState
                      v-else
                      size="small"
                      title="当前节点无可见组件。请到节点链中配置组件可见、可编辑或脱敏权限。"
                      class="fdef-preview-card__empty"
                    />
                  </div>
                </section>

                <aside class="fdef-runtime-preview__feature-panel" aria-label="组件功能">
                  <header>
                    <strong>组件功能</strong>
                    <span>按当前节点权限展示每个组件的运行态能力。</span>
                  </header>
                  <div v-if="cardRuntimePreviewFeatureRows.length" class="fdef-runtime-preview__feature-list">
                    <article
                      v-for="item in cardRuntimePreviewFeatureRows"
                      :key="item.id"
                      class="fdef-runtime-preview__feature-item"
                    >
                      <div>
                        <strong>{{ item.title }}</strong>
                        <StatusTag :type="runtimeAccessType(item.access)">
                          {{ runtimeAccessLabel(item.access) }}
                        </StatusTag>
                      </div>
                      <span>{{ item.binding }}</span>
                      <p>{{ item.capability }}</p>
                    </article>
                  </div>
                  <div v-else class="fdef-runtime-preview__feature-empty">
                    暂无可见组件功能。
                  </div>
                </aside>
              </div>
            </div>
          </a-modal>
        </div>

        <!-- 步骤：节点链 -->
        <div v-show="activeStep === STEP_STAGES" class="fdef-step fdef-step--nodechain" :class="{ 'fdef-step--err': errors.stages || errors.condition }">
          <a-tabs class="fdef-designer-tabs" default-active-key="canvas">
            <a-tab-pane key="canvas" tab="流程图">
              <div class="fdef-designer-layout">
                <FlowStateCanvas
                  :stages="state.stages"
                  :routes="state.routes"
                  :dynamic-policies="state.dynamicPolicies"
                  :selected-type="designerSelection.type"
                  :selected-key="designerSelection.key"
                  @select-node="selectDesignerNode"
                  @select-edge="selectDesignerEdge"
                  @select-blank="selectDesignerBlank"
                  @create-route="createRoute"
                  @connect-route="connectRouteFromCanvas"
                  @reorder-stages="reorderStagesByCanvas"
                />
                <RuleHealthPanel
                  :stages="state.stages"
                  :routes="state.routes"
                  :dynamic-policies="state.dynamicPolicies"
                  :fields="state.cardSchema"
                  @navigate="focusDiagnosticTarget"
                />
              </div>
            </a-tab-pane>

            <a-tab-pane key="nodechain" tab="节点链">
              <StageDefinitionEditor
                v-model="state.stages"
                :schema-fields="state.cardSchema"
                :detail-schema-fields="state.detailSchema"
                :card-components="state.cardComponents"
              >
                <template #left-header>
                  <div class="fdef-step__dep-bar">
                    已配置 <strong>{{ state.cardSchema.length }}</strong> 个卡片字段、<strong>{{ state.detailSchema.length }}</strong> 个明细字段、<strong>{{ state.cardComponents.length }}</strong> 个展示组件。
                  </div>
                </template>
              </StageDefinitionEditor>
            </a-tab-pane>
          </a-tabs>
        </div>

        <!-- 步骤：流程配置 -->
        <div v-show="activeStep === STEP_SETTINGS" class="fdef-step">
          <div class="fdef-flow-config">
            <BaseCard title="审批规则" class="fdef-flow-config-card">
              <template #extra>
                <small>控制退回、重提、兜底管理员和流程依赖</small>
              </template>

              <div class="fdef-flow-config-body">
                <div class="fdef-fc-item">
                  <div class="fdef-fc-item__label">退回策略</div>
                  <a-radio-group
                    v-model:value="state.settings.rejectStrategy"
                    class="fdef-fc-item__control"
                  >
                    <a-radio value="toInitiator">退至发起人</a-radio>
                    <a-radio value="toPrevious">退至上一节点</a-radio>
                    <a-radio value="toSpecified">指定节点</a-radio>
                  </a-radio-group>
                </div>

                <div class="fdef-fc-item">
                  <div class="fdef-fc-item__label">重提策略</div>
                  <a-radio-group
                    v-model:value="state.settings.resubmitStrategy"
                    class="fdef-fc-item__control"
                  >
                    <a-radio value="fromStart">从头开始</a-radio>
                    <a-radio value="fromRejected">从退回节点</a-radio>
                  </a-radio-group>
                </div>

                <div class="fdef-fc-item">
                  <div class="fdef-fc-item__label">
                    审批管理员
                    <span class="fdef-fc-item__hint">用于人工节点处理人为空时的兜底处理</span>
                  </div>
                  <a-select
                    v-model:value="state.settings.approvalAdminUserIds"
                    mode="multiple"
                    style="width: 100%"
                    placeholder="搜索并选择审批管理员"
                    :options="approvalAdminUserOptions"
                    :loading="approvalAdminSearchLoading"
                    show-search
                    option-filter-prop="label"
                    :filter-option="filterOption"
                    @search="onApprovalAdminSearch"
                  />
                </div>

                <div class="fdef-fc-item">
                  <div class="fdef-fc-item__label">
                    前置依赖
                    <span class="fdef-fc-item__hint">流程发布前必须满足的依赖项</span>
                  </div>
                  <div class="fdef-prereq">
                    <div
                      v-for="(p, i) in state.settings.prerequisites"
                      :key="i"
                      class="fdef-prereq__row"
                    >
                      <a-select
                        v-model:value="p.flowCode"
                        placeholder="选择依赖流程"
                        style="flex:1"
                        :options="availableFlows.map(f => ({ value: f.code, label: f.name }))"
                      />
                      <a-checkbox v-model:checked="p.required">必需</a-checkbox>
                      <a-button danger type="text" size="small" @click="removePrerequisite(i)">移除</a-button>
                    </div>
                    <a-button type="dashed" block @click="addPrerequisite">+ 添加前置依赖</a-button>
                  </div>
                </div>
              </div>
            </BaseCard>

            <BaseCard title="业务扩展" class="fdef-flow-config-card">
              <template #extra>
                <small>保留财务冲销、余额生成和清算等业务插件配置</small>
              </template>

              <div class="fdef-flow-config-body">
                <div class="fdef-switch-item">
                  <span class="fdef-switch-item__label">启用冲销</span>
                  <a-switch v-model:checked="state.settings.offsetEnabled" />
                </div>

                <div
                  v-if="state.settings.offsetEnabled"
                  class="fdef-fc-item fdef-fc-item--inset"
                >
                  <div class="fdef-fc-item__label">冲销来源流程</div>
                  <a-select
                    v-model:value="state.settings.offsetSourceFlowCodes"
                    mode="multiple"
                    placeholder="选择可冲销的源流程"
                    style="width:100%"
                    :options="availableFlows.map(f => ({ value: f.code, label: f.name }))"
                  />
                </div>

                <div class="fdef-switch-item">
                  <span class="fdef-switch-item__label">完成后生成余额</span>
                  <a-switch v-model:checked="state.settings.generateBalance" />
                </div>

                <div class="fdef-switch-item">
                  <span class="fdef-switch-item__label">完成后清算余额</span>
                  <a-switch v-model:checked="state.settings.settleBalance" />
                </div>

                <div
                  v-if="state.settings.settleBalance"
                  class="fdef-fc-item fdef-fc-item--inset"
                >
                  <div class="fdef-fc-item__label">清算来源流程编码</div>
                  <a-input v-model:value="state.settings.settleSourceFlowCode" placeholder="例：expense_apply" />
                </div>
              </div>
            </BaseCard>
          </div>
        </div>

        <!-- 步骤：预演与发布校验 -->
        <div v-show="activeStep === STEP_PREVIEW" class="fdef-step fdef-step--preview">
          <header class="page-section__title fdef-preview-stephead">
            <strong>节点视图预览</strong>
            <span>预演任意节点的运行态卡片、审批路径与发布前风险。</span>
          </header>
          <div class="fdef-preview-controlbar">
            <div class="fdef-preview-controlbar__node">
              <span>预演节点</span>
              <a-select
                v-model:value="selectedPreviewStageId"
                :options="previewStageOptions"
                :disabled="loadError || !state.stages.length"
                :placeholder="previewToolbarPlaceholder"
              />
              <StatusTag v-if="selectedPreviewStage" :type="selectedPreviewStage.type === 'manual' ? 'info' : 'success'">
                {{ selectedPreviewStage.type === 'manual' ? '人工节点工作视图' : '自动节点只读视图' }}
              </StatusTag>
            </div>
            <div class="fdef-preview-controlbar__stats">
              <span v-for="stat in previewCoverageStats" :key="stat.key">
                <strong>{{ stat.value }}</strong>{{ stat.label }}
              </span>
            </div>
            <a-button v-if="loadError" @click="reloadFlowDefinition">
              <template #icon><ReloadOutlined /></template>
              重新加载
            </a-button>
          </div>

          <EmptyState
            v-if="loadError"
            size="small"
            title="流程定义还没有加载成功"
            description="无法读取当前草稿、字段、卡片视图和节点链。请重新加载，或返回列表确认该流程是否存在。"
            class="fdef-preview-not-ready fdef-preview-not-ready--error"
          >
            <div class="fdef-preview-not-ready__actions">
              <a-button type="primary" @click="reloadFlowDefinition">
                <template #icon><ReloadOutlined /></template>
                重新加载
              </a-button>
              <a-button @click="goBack">返回列表</a-button>
            </div>
          </EmptyState>

          <EmptyState
            v-else-if="!previewReady"
            size="small"
            title="预演还未就绪"
            description="完成下面配置后，这里会展示真实运行态卡片、审批路径和发布前风险。"
            class="fdef-preview-not-ready"
          >
            <div class="fdef-preview-readiness">
              <button
                v-for="item in previewReadinessItems"
                :key="item.key"
                type="button"
                class="fdef-preview-readiness__item"
                :class="{ 'is-ready': item.ready, 'is-blocking': !item.ready }"
                :disabled="item.ready && item.key !== 'loaded'"
                @click="goPreviewReadinessStep(item)"
              >
                <CheckCircleFilled v-if="item.ready" />
                <CloseCircleFilled v-else />
                <span>
                  <strong>{{ item.title }}</strong>
                  <em>{{ item.description }}</em>
                </span>
                <b v-if="!item.ready || item.key === 'loaded'">{{ item.actionText }}</b>
              </button>
            </div>
          </EmptyState>

          <div v-else class="fdef-preview-workbench">
            <BaseCard title="节点卡片工作视图" no-padding class="fdef-preview-card-pane">
              <template #extra>
                <a-tag>{{ previewVisibleComponentCount }} 个可见{{ previewRuntimeComponents.length ? '组件' : '字段' }}</a-tag>
              </template>

              <!-- 节点 × 视角 × 设备：三视角双设备切换，端点按视角取真值 -->
              <div class="fdef-preview-toolbar">
                <a-select
                  v-model:value="selectedPreviewStageId"
                  size="small"
                  class="fdef-preview-toolbar__stage"
                  placeholder="选择节点"
                  :options="state.stages.map(s => ({ value: s.id, label: s.name || '未命名节点' }))"
                />
                <a-segmented v-model:value="previewViewerMode" size="small" :options="previewViewerModeOptions" />
                <a-segmented v-model:value="previewDevice" size="small" :options="previewDeviceOptions" />
              </div>

              <div class="fdef-preview-card-stage" :class="{ 'is-mobile': previewDevice === 'mobile' }">
                <div
                  class="fdef-preview-card fdef-preview-card--runtime"
                  :class="{ 'fdef-preview-card--mobile': previewDevice === 'mobile' }"
                >
                  <div
                    class="fdef-preview-card__header"
                    :class="`fdef-preview-card__header--${state.cardHeader.align || 'left'}`"
                  >
                    <span class="fdef-preview-card__title">{{ cardHeaderTitle }}</span>
                    <span v-if="cardHeaderShowSubtitle" class="fdef-preview-card__code">{{ cardHeaderSubtitle }}</span>
                    <a-tag v-if="cardHeaderShowStatus" size="small">{{ state.basic.status || 'draft' }}</a-tag>
                  </div>
                  <!-- 统一走 SchemaRenderer 装配层：有组件→CardComponentRenderer，无组件→扁平字段回退（与运行时一字不差，消灭伪组件漂移） -->
                  <div v-if="previewHasVisibleContent" class="fdef-preview-card__body">
                    <SchemaRenderer
                      :components="previewRuntimeComponents"
                      :schema="previewVisibleFieldDefs"
                      :model-value="previewSampleData"
                      :detail-rows="previewDetailRows"
                      :mode="previewCardMode"
                      :platform="previewDevice"
                    />
                  </div>
                  <EmptyState
                    v-else
                    size="small"
                    title="当前节点无可见组件。请到节点链中配置该节点的组件可见、可编辑或脱敏权限。"
                    class="fdef-preview-card__empty"
                  >
                    <a-button size="small" type="link" @click="activeStep = STEP_STAGES">去节点链</a-button>
                  </EmptyState>
                </div>
              </div>
            </BaseCard>

            <BaseCard no-padding class="fdef-preview-path-pane">
              <PathPreviewPanel
                :flow-definition-id="flowId"
                :preview-api="previewFlowDraftPath"
                :disabled="!previewReady"
                :fields="state.cardSchema"
                @step-select="onPreviewStepSelect"
              />
            </BaseCard>

            <BaseCard title="发布校验" class="fdef-preview-check-pane">
              <template #extra>
                <StatusTag :type="previewConfigWarnings.length ? 'warning' : 'success'">
                  {{ previewConfigWarnings.length ? `${previewConfigWarnings.length} 项风险` : '可发布' }}
                </StatusTag>
              </template>

              <div class="fdef-preview-check-list">
                <div
                  v-for="item in previewReadinessItems"
                  :key="item.key"
                  class="fdef-preview-check-list__item"
                  :class="{ 'is-ready': item.ready }"
                >
                  <CheckCircleFilled v-if="item.ready" />
                  <CloseCircleFilled v-else />
                  <span>{{ item.title }}</span>
                </div>
              </div>

              <div v-if="previewConfigWarnings.length" class="fdef-preview-warning-list">
                <strong>规则风险</strong>
                <span
                  v-for="warning in previewConfigWarnings"
                  :key="warning.message"
                  :class="{ 'fdef-preview-warning--clickable': warning.target }"
                  :role="warning.target ? 'button' : undefined"
                  :tabindex="warning.target ? 0 : undefined"
                  @click="warning.target && focusDiagnosticTarget(warning.target)"
                  @keydown.enter.prevent="warning.target && focusDiagnosticTarget(warning.target)"
                >
                  {{ warning.message }}
                  <em v-if="warning.target" class="fdef-preview-warning__locate">定位 →</em>
                </span>
              </div>
              <div v-else class="fdef-preview-good-state">
                <CheckCircleFilled />
                <span>默认分支、动态节点和处理人兜底已通过当前静态检查。</span>
              </div>

              <div class="fdef-preview-node-summary">
                <strong>当前节点权限</strong>
                <div>
                  <span>可见字段</span><b>{{ stagePreviewFields.length }}</b>
                </div>
                <div>
                  <span>可见明细列</span><b>{{ stagePreviewDetailSchema.length }}</b>
                </div>
                <div>
                  <span>节点链</span><b>{{ state.stages.length }}</b>
                </div>
                <div>
                  <span>条件边</span><b>{{ state.routes.length }}</b>
                </div>
              </div>
            </BaseCard>
          </div>
        </div>
      </div>
    </a-spin>

    <a-drawer
      v-model:open="designerDrawerOpen"
      :width="600"
      placement="right"
      :destroy-on-close="false"
      class="fdef-designer-drawer"
    >
      <template #title>
        <span>{{ designerDrawerTitle }}</span>
      </template>

      <section
        v-if="designerSelection.type === 'node' && selectedDesignerStage"
        class="fdef-drawer-section"
      >
        <header class="page-section__title fdef-drawer-section__head">
          <strong>{{ selectedDesignerStage.name || '未命名节点' }}</strong>
          <span>节点链内部负责审批路径、处理人策略和节点视图权限</span>
        </header>

        <!-- 画布节点抽屉复用节点链完整 5-tab 配置面板，消灭"抽屉子集配不全→发布失败"的双入口断头路 -->
        <StageConfigPanel
          :stages="state.stages"
          :selected-index="drawerStageIndex"
          :schema-fields="state.cardSchema"
          :detail-schema-fields="state.detailSchema"
          :card-components="state.cardComponents"
        />

        <!-- 出边列表兜底：画布上平行/重叠边点不到时，从这里选任一条出边（含停用边）编辑 -->
        <div v-if="selectedStageOutgoingRoutes.length" class="fdef-drawer-outedges">
          <header class="fdef-drawer-outedges__head">
            <strong>出边（{{ selectedStageOutgoingRoutes.length }}）</strong>
            <span>点击任一条编辑，避免画布上重叠边点不到</span>
          </header>
          <button
            v-for="route in selectedStageOutgoingRoutes"
            :key="route.edgeKey"
            type="button"
            class="fdef-drawer-outedge"
            :class="{ 'is-disabled': route.status === 'disabled' }"
            @click="selectDesignerEdge(route.edgeKey)"
          >
            <span class="fdef-drawer-outedge__name">{{ route.routeName || (route.isDefault ? '默认分支' : '条件分支') }}</span>
            <span class="fdef-drawer-outedge__meta">
              <a-tag v-if="route.isDefault" color="default">默认</a-tag>
              <a-tag v-else color="blue">优先级 {{ route.priority }}</a-tag>
              <a-tag v-if="route.status === 'disabled'" color="warning">已停用</a-tag>
              <span class="fdef-drawer-outedge__target">→ {{ route.toStageKey }}</span>
            </span>
          </button>
        </div>

        <!-- V2 冻结：动态加签编辑器（简化瘦身桶B 2026-06-16），入口暂下线；金额分级可用条件路由表达。
             dynamicPolicies 数据通道保留，存量策略仍随草稿加载/保存。
             条件里保留 selectedDesignerStage 判空以维持模板类型窄化 -->
        <DynamicApprovalPolicyEditor
          v-if="false"
          v-model="state.dynamicPolicies"
          :source-stage-key="selectedDesignerStage?.id ?? ''"
          :stages="state.stages"
          :fields="state.cardSchema"
        />

      </section>

      <RouteRuleCardEditor
        v-else-if="designerSelection.type === 'edge'"
        :model-value="selectedDesignerRoute"
        :stages="state.stages"
        :fields="state.cardSchema"
        @update:model-value="updateRoute"
        @delete="deleteRoute"
      />

      <section v-else class="fdef-drawer-section">
        <PathPreviewPanel
          :flow-definition-id="flowId"
          :preview-api="previewFlowDraftPath"
          :fields="state.cardSchema"
          @step-select="onPreviewStepSelect"
        />
        <RuleHealthPanel
          :stages="state.stages"
          :routes="state.routes"
          :dynamic-policies="state.dynamicPolicies"
          :fields="state.cardSchema"
          @navigate="focusDiagnosticTarget"
        />
      </section>
    </a-drawer>

    <CardComponentConfigDrawer
      v-model:open="componentDrawerOpen"
      :model-value="editingComponent"
      :schema-fields="state.cardSchema"
      :detail-schema-fields="state.detailSchema"
      @update:model-value="updateCardComponent"
      @delete="deleteCardComponent"
    />
  </div>
</template>

<style scoped lang="scss">
/* 根容器：作为 .content-scroll 的 flex item 占满剩余空间，
   并采用 flex column 布局让步骤指示器固定 + 内容区填满，
   页面本身不滚动，溢出由内容区内部承担 */
.fdef-edit {
  flex: 1;
  min-height: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;

  /* 让 a-spin 嵌套包装层继承 flex column 链路，使内部 .fdef-step-body 可拿到剩余高度 */
  :deep(.ant-spin-nested-loading) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    width: 100%;
  }
  :deep(.ant-spin-nested-loading > .ant-spin-container) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    width: 100%;
  }
}

/* ============ 工具栏元素 ============ */
.tb-title {
  display: inline-flex;
  align-items: center;
  min-width: 0;
  max-width: 280px;
  gap: 4px;
  white-space: nowrap;
  font-size: 13px;
  color: var(--text-2);
  margin-left: 4px;
  strong {
    min-width: 0;
    overflow: hidden;
    color: var(--text-1);
    font-weight: 600;
    text-overflow: ellipsis;
  }
}

.tb-version-context {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 360px;
  height: 24px;
  padding: 0 8px;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--color-info) 30%, transparent);
  border-radius: 999px;
  background: var(--color-info-light);
  color: var(--color-info);
  font-size: 12px;
  line-height: 22px;
  white-space: nowrap;
}

.tb-version-context__draft {
  flex: 0 0 auto;
  color: var(--color-info);
  font-weight: 600;
}

.tb-version-context__published,
.tb-version-context__note {
  flex: 0 0 auto;
  color: var(--text-2);
}

.tb-version-context__published::before,
.tb-version-context__note::before {
  margin-right: 6px;
  color: color-mix(in srgb, var(--color-info) 30%, transparent);
  content: "·";
}

.tb-history-group {
  display: inline-flex;
  align-items: center;
  overflow: hidden;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-page);
}

.tb-history-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  min-width: 58px;
  height: 30px;
  padding: 0 8px;
  border: 0;
  background: transparent;
  cursor: pointer;
  color: var(--text-1);
  font-size: 12px;
  line-height: 30px;
  transition: background .18s ease, color .18s ease;

  svg {
    font-size: 13px;
  }

  span {
    white-space: nowrap;
  }

  &:hover:not(:disabled) {
    background: var(--bg-card);
    color: var(--text-1);
  }

  &:disabled {
    background: var(--bg-page);
    color: var(--text-3);
    cursor: not-allowed;
  }
}

.tb-history-btn + .tb-history-btn {
  border-left: 1px solid var(--border);
}

.tb-history-btn--undo svg {
  color: var(--color-info);
}

.tb-history-btn--redo svg {
  color: var(--color-success);
}

.tb-history-btn:disabled svg {
  color: var(--text-3);
}

.tb-divider {
  width: 1px; height: 16px;
  background: var(--border);
  margin: 0 4px;
}

/* ============ 步骤条 ============ */
/* 固定高度、不参与压缩，让出剩余空间给 .fdef-step-body */
.fdef-steps-wrap {
  flex-shrink: 0;
  margin: 0;
  padding: 6px 24px 4px;
  background: var(--bg-muted);
  border: none;
  border-bottom: 2px solid var(--border-strong);
  border-radius: 0;
  position: relative;
  z-index: 1;
  box-shadow: var(--shadow-sm);
}

.fdef-steps {
  /* 让「步骤描述」与「连接线」视觉上更紧凑 */
  :deep(.ant-steps-item-title) {
    font-size: 13px;
    font-weight: 600;
  }
  :deep(.ant-steps-item-description) {
    font-size: 11.5px !important;
    color: var(--text-3) !important;
    max-width: 200px;
  }
  /* 鼠标可点击提示 */
  :deep(.ant-steps-item) {
    cursor: pointer;
  }
  :deep(.ant-steps-item-disabled) {
    cursor: pointer !important;
  }
  /* 压缩 AntD Steps 内部上下间距 */
  :deep(.ant-steps-item-icon) {
    margin-top: 0 !important;
    margin-bottom: 0 !important;
  }
  :deep(.ant-steps-item-content) {
    margin-top: 2px !important;
  }
  :deep(.ant-steps-item-tail) {
    top: 14px !important;
  }
}

/* ============ 步骤体（各步内容容器） ============ */
/* 占满剩余空间，内部超出时自行滚动，避免页面级滚动条 */
.fdef-step-body {
  flex: 1;
  min-height: 0;
  margin: 0;
  padding: 0;
  overflow-y: auto;
  overflow-x: hidden;
  /* 明确白色背景，与上方加深的步骤区形成清晰视觉切分 */
  background: var(--bg-card);
}

.fdef-step {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 0;
  border-left: none;
  border-right: none;
  border-top: none;
  padding: 12px 20px 20px;
  margin: 0;
}

.fdef-step--card-view {
  padding: 0;
  border: 0;
  min-height: 100%;
  overflow: hidden;
}

.fdef-step--err {
  border-color: var(--color-danger);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-danger) 12%, transparent);
}

.fdef-step__form { padding: 4px 0 0; }

/* ============ 基本信息：垂直单列排列 ============ */
.fdef-basic-config {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 18px 20px;
  background: var(--bg-page);
  border: 1px solid var(--border);
  border-radius: 10px;
}

/* 依赖提示条（节点链顶部） */
.fdef-step__dep-bar {
  margin: 0 0 14px;
  padding: 8px 12px;
  background: var(--bg-page);
  border: 1px dashed var(--border-strong);
  border-radius: 6px;
  font-size: 12.5px;
  color: var(--text-2);
  strong { color: var(--text-1); margin: 0 2px; }
}
.fdef-step__link {
  margin-left: 8px;
  color: var(--color-info);
  cursor: pointer;
  &:hover { text-decoration: underline; }
}

/* 步骤 4：节点链 —— 让编辑器铺满步骤容器 */
.fdef-step--nodechain {
  padding: 0;
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

/* 步骤 2 字段设计 */
.fdef-schema-guide-card {
  margin: 0 0 14px;
}

.fdef-schema-guide {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1.35fr) auto minmax(0, 1fr);
  gap: 10px;
  align-items: stretch;
  padding: 10px 12px;
}

.fdef-schema-guide__item {
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-width: 0;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);

  strong {
    color: var(--text-1);
    font-size: 13px;
    line-height: 18px;
  }

  em {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
    font-style: normal;
    line-height: 18px;
  }
}

.fdef-schema-guide__arrow {
  display: flex;
  align-items: center;
  color: var(--text-3);
  font-size: 15px;
}

.fdef-schema-cols { margin-top: 4px; }

.fdef-card-view-workbench {
  display: grid;
  grid-template-columns: 300px minmax(560px, 1fr) 320px;
  gap: 0;
  margin-top: 0;
  padding: 0;
  border: 1px solid var(--border);
  background: var(--bg-muted);
  border-radius: 0;
  min-height: calc(100vh - 210px);
  overflow: hidden;
}

.fdef-card-view-library,
.fdef-card-canvas,
.fdef-component-inspector {
  min-width: 0;
}

.fdef-card-view-library {
  max-height: calc(100vh - 210px);
  overflow-y: auto;
  padding: 0;
  border-right: 1px solid var(--border);
  background: var(--bg-card);
}

.fdef-card-canvas {
  display: flex;
  flex-direction: column;
  gap: 0;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: var(--bg-card);
}

.fdef-card-canvas__stage {
  display: grid;
  place-items: start center;
  flex: 1;
  min-height: calc(100vh - 262px);
  padding: 16px 12px 24px;
  overflow: auto;
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    linear-gradient(180deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    var(--bg-page);
  background-size: 24px 24px;
}

.fdef-card-canvas__surface {
  width: 375px;
  max-width: 100%;
  height: fit-content;
  padding: 20px 24px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  box-shadow: var(--shadow-sm);
}

.fdef-card-canvas__surface-header {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 18px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
  border-radius: 6px;
  cursor: pointer;
  outline: 1px solid transparent;
  outline-offset: 5px;
  transition: background .15s ease, outline-color .15s ease, box-shadow .15s ease;

  &:hover,
  &:focus-visible {
    background: color-mix(in srgb, var(--color-primary) 2%, transparent);
    outline-color: color-mix(in srgb, var(--color-primary) 18%, transparent);
  }
}

.fdef-card-canvas__surface-header--selected {
  background: color-mix(in srgb, var(--color-primary) 4%, transparent);
  outline-color: color-mix(in srgb, var(--color-primary) 40%, transparent);
  box-shadow: var(--shadow-sm);
}

.fdef-card-canvas__surface-header--center {
  justify-content: center;
  text-align: center;
}

.fdef-card-canvas__surface-title {
  color: var(--text-1);
  font-size: 16px;
  font-weight: 600;
}

.fdef-card-canvas__surface-code {
  color: var(--text-3);
  font-size: 12px;
  font-weight: 500;
}

.fdef-card-canvas__head,
.fdef-component-inspector__panel header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  padding: 10px 14px 8px;
  border-bottom: 1px solid var(--border);

  strong,
  span {
    display: block;
  }

  span {
    min-width: 0;
    margin-top: 2px;
    font-size: 12px;
    line-height: 18px;
  }
}

.fdef-card-canvas__list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 12px;
  row-gap: 0;
  align-content: start;
  flex: 1;
  min-height: 0;
}

.fdef-card-canvas__empty {
  grid-column: 1 / -1;
}

.fdef-card-canvas-item {
  position: relative;
  display: block;
  width: 100%;
  min-height: 28px;
  margin: 0;
  padding: 2px 6px;
  border: 1px dashed transparent;
  background: transparent;
  border-radius: 6px;
  cursor: pointer;
  box-shadow: none;
  transition: border-color .15s ease, box-shadow .15s ease, transform .15s ease, background .15s ease;

  &:hover,
  &:focus-visible {
    border-color: var(--color-primary-border);
    background: color-mix(in srgb, var(--color-primary) 2%, transparent);
    box-shadow: none;
    outline: none;
  }
}

.fdef-card-canvas-item--selected {
  border-color: color-mix(in srgb, var(--color-primary) 55%, transparent);
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
  box-shadow: var(--shadow-sm);
}

.fdef-card-canvas-item--half {
  grid-column: span 1;
}

.fdef-card-canvas-item--full {
  grid-column: 1 / -1;
}

.fdef-card-canvas-item--compact {
  grid-column: span 1;
  justify-self: start;
  width: fit-content;
  min-width: 128px;
  max-width: 100%;
}

.fdef-card-canvas-item--ghost {
  opacity: .58;
  background: var(--color-primary-light);
}

.fdef-card-canvas-item--chosen {
  transform: scale(.995);
}

.fdef-card-canvas-item__handle {
  position: absolute;
  z-index: 2;
  top: 50%;
  left: -2px;
  transform: translateY(-50%);
  color: var(--text-3);
  cursor: grab;
  font-style: normal;
  font-size: 12px;
  line-height: 1;
  opacity: 0;
  transition: opacity .15s ease, color .15s ease;
}

.fdef-card-canvas-item:hover .fdef-card-canvas-item__handle,
.fdef-card-canvas-item--selected .fdef-card-canvas-item__handle {
  opacity: 1;
}

.fdef-card-canvas-item__title {
  display: none;
  min-width: 0;

  strong,
  em {
    display: block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  em {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
    font-style: normal;
  }

  b {
    flex-shrink: 0;
    color: var(--color-info);
    font-size: 12px;
    font-weight: 600;
  }
}

.fdef-card-canvas-item__inline-actions {
  position: absolute;
  z-index: 3;
  top: 4px;
  right: 8px;
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: color-mix(in srgb, var(--bg-card) 94%, transparent);
  box-shadow: var(--shadow-sm);
}

.fdef-card-canvas-item__icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  font-size: 13px;
  line-height: 1;
  transition: background .15s ease, color .15s ease;

  &:hover,
  &:focus-visible {
    background: var(--color-primary-light);
    color: var(--color-primary);
    outline: none;
  }
}

.fdef-card-canvas-item__icon-btn--danger:hover,
.fdef-card-canvas-item__icon-btn--danger:focus-visible {
  background: var(--color-danger-light);
  color: var(--color-danger-text);
}

.fdef-card-canvas-item__runtime {
  min-width: 0;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;

  :deep(.cf-runtime-components) {
    gap: 12px;
  }

  :deep(.cf-runtime-field) {
    padding: 7px 0;
  }
}

.fdef-card-canvas-item--half .fdef-card-canvas-item__runtime {
  :deep(.cf-runtime-field) {
    grid-template-columns: minmax(52px, 64px) minmax(0, 1fr);
    gap: 8px;
  }
}

.fdef-card-canvas-item--compact .fdef-card-canvas-item__runtime {
  :deep(.cf-runtime-components) {
    width: max-content;
    max-width: 100%;
  }

  :deep(.cf-runtime-field) {
    grid-template-columns: auto auto;
    gap: 8px;
  }

  :deep(.cf-runtime-field label),
  :deep(.cf-runtime-field strong) {
    white-space: nowrap;
  }
}

.fdef-runtime-preview-modal {
  :deep(.ant-modal-content) {
    overflow: hidden;
    border-radius: 8px;
  }
}

.fdef-runtime-preview {
  background: var(--bg-muted);
}

.fdef-runtime-preview__toolbar {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-card);

  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
    line-height: 20px;
  }

  span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }

  label {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    align-items: center;
    gap: 8px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.fdef-runtime-preview__mode-toggle {
  display: inline-grid;
  grid-template-columns: repeat(2, 64px);
  gap: 4px;
  padding: 4px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-page);
}

.fdef-runtime-preview__mode-btn {
  height: 28px;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  font-size: 12px;
  font-weight: 600;
  transition: background .16s ease, border-color .16s ease, color .16s ease, opacity .16s ease;

  &:hover,
  &:focus-visible {
    background: var(--bg-card);
    color: var(--color-primary);
    outline: none;
  }

  &:disabled {
    cursor: not-allowed;
    opacity: .45;
  }

  &.is-active {
    border-color: var(--color-primary-border);
    background: var(--bg-card);
    color: var(--color-primary);
    box-shadow: var(--shadow-sm);
  }
}

.fdef-runtime-preview__body {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  height: min(68vh, 620px);
  min-height: 420px;
}

.fdef-runtime-preview__stage {
  display: grid;
  place-items: start center;
  min-width: 0;
  padding: 26px 18px;
  overflow: auto;
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    linear-gradient(180deg, color-mix(in srgb, var(--text-3) 10%, transparent) 1px, transparent 1px),
    var(--bg-page);
  background-size: 24px 24px;
}

.fdef-runtime-preview__card {
  width: 375px;
  max-width: 100%;
  min-height: 220px;
  padding: 20px 24px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  box-shadow: var(--shadow-md);
}

.fdef-runtime-preview__card-body {
  display: grid;
  gap: 12px;
}

.fdef-runtime-preview__feature-panel {
  display: flex;
  flex-direction: column;
  min-width: 0;
  border-left: 1px solid var(--border);
  background: var(--bg-card);

  > header {
    padding: 14px;
    border-bottom: 1px solid var(--border);

    strong,
    span {
      display: block;
    }

    strong {
      color: var(--text-1);
      font-size: 14px;
      line-height: 20px;
    }

    span {
      margin-top: 2px;
      color: var(--text-2);
      font-size: 12px;
      line-height: 18px;
    }
  }
}

.fdef-runtime-preview__feature-list {
  display: grid;
  gap: 8px;
  padding: 12px;
  overflow: auto;
}

.fdef-runtime-preview__feature-item {
  display: grid;
  gap: 6px;
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-muted);

  div {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    min-width: 0;
  }

  strong {
    min-width: 0;
    overflow: hidden;
    color: var(--text-1);
    font-size: 13px;
    line-height: 18px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  span,
  p {
    margin: 0;
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}

.fdef-runtime-preview__feature-empty {
  margin: 12px;
  padding: 18px 12px;
  border: 1px dashed var(--border-strong);
  border-radius: 8px;
  color: var(--text-2);
  font-size: 13px;
  text-align: center;
}

.fdef-component-inspector {
  display: flex;
  flex-direction: column;
  gap: 0;
  border-left: 1px solid var(--border);
  background: var(--bg-card);
}

.fdef-component-inspector__panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
  padding: 14px;
  border: 0;
  border-radius: 0;
  background: var(--bg-card);
}

.fdef-component-inspector__panel {
  flex: 0 0 auto;
}

.fdef-component-inspector__form {
  display: grid;
  gap: 10px;

  label {
    display: grid;
    gap: 5px;
    min-width: 0;
  }

  label > span {
    color: var(--text-3);
    font-size: 12px;
  }
}

.fdef-layout-toggle {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 4px;
  padding: 4px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-page);
}

.fdef-layout-toggle__btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
  height: 30px;
  padding: 0 8px;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  font-size: 12px;
  font-weight: 600;
  line-height: 30px;
  transition: background .16s ease, border-color .16s ease, color .16s ease, box-shadow .16s ease;

  &:hover {
    background: var(--bg-card);
    color: var(--color-primary);
  }

  &.is-active {
    border-color: var(--color-primary-border);
    background: var(--bg-card);
    color: var(--color-primary);
    box-shadow: var(--shadow-sm);
  }
}


.fdef-designer-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.ant-tabs-content-holder),
  :deep(.ant-tabs-content),
  :deep(.ant-tabs-tabpane) {
    flex: 1;
    min-height: 0;
  }

  :deep(.ant-tabs-nav) {
    margin: 0;
    padding: 0 16px;
    background: var(--bg-card);
    border-bottom: 1px solid var(--border);
  }
}

.fdef-designer-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: 14px;
  padding: 14px;
  min-height: 520px;
  background: var(--bg-page);
}

.fdef-designer-drawer :deep(.ant-drawer-body) {
  padding: 18px;
  background: var(--bg-muted);
}

.fdef-drawer-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 复用的 StageConfigPanel 根(.sde__right)原为节点链右栏设计(flex:1+内滚+内边距)；
   放进抽屉时改为自然高度、无内滚、无内边距，让抽屉整体滚动 header+面板+出边列表。 */
.fdef-drawer-section :deep(.sde__right) {
  flex: none;
  padding: 0;
  overflow: visible;
}

.fdef-drawer-section__head {
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);

  strong,
  span {
    display: block;
  }

  span {
    margin-top: 4px;
    font-size: 12px;
  }
}

/* 节点抽屉出边列表 */
.fdef-drawer-outedges {
  display: flex;
  flex-direction: column;
  gap: 6px;

  &__head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 8px;

    strong {
      color: var(--text-1);
      font-size: 13px;
    }

    span {
      color: var(--text-3);
      font-size: 12px;
    }
  }
}

.fdef-drawer-outedge {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s;

  &:hover {
    border-color: var(--color-primary);
  }

  &.is-disabled {
    opacity: 0.65;
  }

  &__name {
    color: var(--text-1);
    font-size: 13px;
    word-break: break-all;
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
  }

  &__target {
    color: var(--text-3);
    font-size: 12px;
  }
}

/* 步骤 6 预演发布 */
.fdef-step--preview {
  background: linear-gradient(180deg, var(--color-info-light), var(--bg-card));
}

.fdef-pane--err {
  border: 1px solid var(--color-danger);
  border-radius: 12px;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-danger) 12%, transparent);
}

/* ============ 流程配置：垂直单列排列 ============ */
.fdef-flow-config {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.fdef-flow-config-card {
  :deep(.base-card__extra) small {
    font-size: 12px;
    color: var(--text-3);
  }
}

.fdef-flow-config-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* 通用配置项：紧凑的 label + 控件 */
.fdef-fc-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}
.fdef-fc-item__label {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-1);
  line-height: 1.2;
}
.fdef-fc-item__hint {
  font-size: 12px;
  font-weight: 400;
  color: var(--text-3);
}
.fdef-required-star {
  color: var(--color-danger);
}
.fdef-fc-item__control {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 12px;
  padding: 6px 10px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  min-height: 36px;
}
.fdef-fc-item__control :deep(.ant-radio-wrapper) {
  margin-right: 0;
  font-size: 13px;
  color: var(--text-1);
}
.fdef-fc-item--inset {
  padding: 12px 14px;
  background: var(--bg-card);
  border: 1px dashed var(--border-strong);
  border-radius: 8px;
}

/* 开关项：label 与 switch 同行紧凑排列 */
.fdef-switch-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  transition: border-color .15s ease, box-shadow .15s ease;
}
.fdef-switch-item:hover {
  border-color: var(--border-strong);
  box-shadow: var(--shadow-sm);
}
.fdef-switch-item__label {
  font-size: 13px;
  color: var(--text-1);
  font-weight: 500;
}

/* ============ 前置依赖 ============ */
.fdef-prereq {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
}
.fdef-prereq__row {
  display: flex;
  gap: 8px;
  align-items: center;
}

/* ============ 预演与校验工作台 ============ */
.fdef-preview-stephead {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: 12px;

  > span {
    font-size: 12px;
  }
}

.fdef-preview-controlbar {
  display: grid;
  grid-template-columns: minmax(360px, 1fr) auto auto;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-page);
}

.fdef-preview-controlbar__node {
  display: grid;
  grid-template-columns: auto minmax(260px, 360px) auto;
  align-items: center;
  gap: 10px;
  min-width: 0;

  > span {
    color: var(--text-2);
    font-size: 12px;
    font-weight: 600;
  }
}

.fdef-preview-controlbar__stats {
  display: flex;
  align-items: center;
  gap: 8px;

  span {
    display: inline-flex;
    align-items: baseline;
    gap: 4px;
    padding: 4px 8px;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--bg-card);
    color: var(--text-2);
    font-size: 12px;
    white-space: nowrap;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }
}

.fdef-preview-not-ready--error {
  background: var(--color-danger-light);
  border: 1px solid color-mix(in srgb, var(--color-danger) 20%, transparent);
  border-radius: 8px;
}

.fdef-preview-not-ready__actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.fdef-preview-readiness {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  align-content: start;
}

.fdef-preview-readiness__item {
  display: grid;
  grid-template-columns: 18px minmax(0, 1fr) auto;
  gap: 10px;
  align-items: start;
  width: 100%;
  min-height: 84px;
  padding: 12px;
  border: 1px solid var(--color-warning);
  border-radius: 8px;
  background: var(--color-warning-light);
  text-align: left;
  cursor: pointer;

  svg {
    margin-top: 2px;
    color: var(--color-warning);
  }

  span,
  strong,
  em {
    display: block;
    min-width: 0;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
    line-height: 18px;
  }

  em {
    margin-top: 4px;
    color: var(--text-2);
    font-size: 12px;
    font-style: normal;
    line-height: 18px;
  }

  b {
    align-self: center;
    color: var(--color-info);
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
  }

  &.is-ready {
    border-color: var(--border);
    background: var(--bg-card);
    cursor: default;

    svg {
      color: var(--color-success);
    }
  }
}

.fdef-preview-workbench {
  display: grid;
  grid-template-columns: minmax(420px, 1.05fr) minmax(360px, .95fr) 340px;
  gap: 14px;
  min-height: 640px;
}

.fdef-preview-card-pane,
.fdef-preview-path-pane,
.fdef-preview-check-pane {
  min-width: 0;
}

.fdef-preview-card-stage {
  display: grid;
  place-items: start center;
  min-height: 584px;
  padding: 24px 18px;
  overflow: auto;
  background:
    radial-gradient(circle, color-mix(in srgb, var(--text-3) 18%, transparent) 1px, transparent 1px),
    var(--bg-page);
  background-size: 18px 18px;
}

.fdef-preview-card {
  width: 375px;
  max-width: 100%;
  height: fit-content;
  padding: 20px 24px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  box-shadow: var(--shadow-sm);
}

.fdef-preview-card__header {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 18px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.fdef-preview-card__header--center {
  justify-content: center;
  text-align: center;
}

.fdef-preview-card__title {
  color: var(--text-1);
  font-size: 16px;
  font-weight: 600;
}

.fdef-preview-card__code {
  color: var(--text-3);
  font-family: 'JetBrains Mono', 'SF Mono', Consolas, monospace;
  font-size: 12px;
}

.fdef-preview-card__body {
  display: flex;
  flex-direction: column;
}

// B4 预览工具条：节点 × 视角 × 设备
.fdef-preview-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-card);

  &__stage {
    min-width: 140px;
  }
}

// PC 视角更宽，移动视角限宽 390px 呈手机形态
.fdef-preview-card--runtime {
  width: 560px;
}

.fdef-preview-card--mobile {
  width: 390px;
  border-radius: 18px;
}

.fdef-preview-path-pane {
  padding: 12px;
  overflow: auto;

  :deep(.cf-path-preview__form) {
    grid-template-columns: 1fr 1fr;
  }
}

.fdef-preview-check-pane {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.fdef-preview-check-list,
.fdef-preview-warning-list,
.fdef-preview-good-state,
.fdef-preview-node-summary {
  margin: 12px;
}

.fdef-preview-check-list {
  display: grid;
  gap: 8px;
}

.fdef-preview-check-list__item {
  display: grid;
  grid-template-columns: 16px minmax(0, 1fr);
  gap: 8px;
  align-items: center;
  padding: 8px 10px;
  border: 1px solid var(--color-warning);
  border-radius: 6px;
  background: var(--color-warning-light);
  color: var(--color-warning-text);
  font-size: 12px;

  svg {
    color: var(--color-warning);
  }

  &.is-ready {
    border-color: var(--border);
    background: var(--bg-muted);
    color: var(--color-success-text);

    svg {
      color: var(--color-success);
    }
  }
}

.fdef-preview-warning-list {
  display: grid;
  gap: 8px;
  padding: 10px;
  border: 1px solid var(--color-warning);
  border-radius: 8px;
  background: var(--color-warning-light);

  strong,
  span {
    color: var(--color-warning-text);
    font-size: 12px;
    line-height: 18px;
  }
}

.fdef-preview-warning--clickable {
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  border-radius: 4px;
  transition: background .15s;

  &:hover { background: color-mix(in srgb, var(--color-warning) 14%, transparent); }
  &:focus-visible { outline: 2px solid var(--color-warning); outline-offset: 1px; }
}

.fdef-preview-warning__locate {
  margin-left: auto;
  font-style: normal;
  font-weight: 600;
  white-space: nowrap;
  opacity: 0.9;
}

.fdef-preview-good-state {
  display: grid;
  grid-template-columns: 18px minmax(0, 1fr);
  gap: 8px;
  align-items: start;
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-muted);
  color: var(--color-success-text);
  font-size: 12px;
  line-height: 18px;

  svg {
    margin-top: 2px;
    color: var(--color-success);
  }
}

.fdef-preview-node-summary {
  display: grid;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--border);

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  div {
    display: flex;
    justify-content: space-between;
    gap: 10px;
    color: var(--text-2);
    font-size: 12px;
  }

  b {
    color: var(--text-1);
    font-weight: 600;
  }
}

@media (max-width: 1500px) {
  .tb-version-context__note {
    display: none;
  }

  .fdef-card-view-workbench {
    grid-template-columns: 260px minmax(0, 1fr) 300px;
  }

  .fdef-component-inspector {
    grid-column: auto;
  }

  .fdef-preview-workbench {
    grid-template-columns: minmax(380px, 1fr) minmax(340px, .9fr);
  }

  .fdef-preview-check-pane {
    grid-column: 1 / -1;
  }
}

@media (max-width: 1180px) {
  .tb-version-context {
    max-width: 190px;
  }

  .tb-version-context__published {
    display: none;
  }

  .fdef-schema-guide,
  .fdef-card-view-workbench,
  .fdef-component-inspector,
  .fdef-preview-controlbar,
  .fdef-preview-controlbar__node,
  .fdef-preview-workbench,
  .fdef-preview-readiness {
    grid-template-columns: 1fr;
  }

  .fdef-schema-guide__arrow {
    display: none;
  }

  .fdef-preview-controlbar__stats {
    flex-wrap: wrap;
  }

  .fdef-preview-check-pane {
    grid-column: auto;
  }
}
</style>
