<script setup lang="ts">
/**
 * StageDefinitionEditor —— 节点链可视化编辑器（左右分栏）
 *
 * 设计原则：
 *  - 左栏(280px)：拖拽排序时间线 + 添加按钮
 *  - 右栏：选中节点的属性面板（实时同步，无需保存按钮）
 *  - 根据 selectedStage.type 自动渲染对应表单
 *  - 进入条件复用 ConditionBuilder，字段从 props.schemaFields 取
 */
import { computed, onMounted, ref, watch, nextTick } from 'vue'
import {
  PlusOutlined,
  DeleteOutlined,
  HolderOutlined,
  UserOutlined,
  RobotOutlined,
  ThunderboltOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons-vue'
import draggable from 'vuedraggable'
import ConditionBuilder from './ConditionBuilder.vue'
import StageComponentViewEditor from './designer/StageComponentViewEditor.vue'
import type { ConditionGroup, FieldOption } from './ConditionBuilder.vue'
import type { CardComponentDefinition, SchemaFieldDefinition, AutoPluginRegistryDto, AutoPluginRuleDto } from '@/types/cardflow'
import { getRoleList, getUserList } from '@/api/system'
import { getPluginRegistry, getPluginRules } from '@/api/cardflow'
import { Modal } from 'ant-design-vue'

// ==================== 类型 ====================

export type StageNodeType = 'manual' | 'auto'
export type StageApprovalMode = 'single' | 'countersign' | 'orsign' | 'sequential'
export type StageAccessMode = 'hidden' | 'masked' | 'readonly' | 'editable' | 'required'
type AssigneeFallbackType = 'failSubmit' | 'flowAdmin'

/** 处理粒度：card=卡片级，batch=批次级 */
export type ProcessingGranularity = 'card' | 'batch'

export interface StageAccessRuleDraft {
  access: StageAccessMode
  required?: boolean
  maskPattern?: string | null
}

export interface StageViewProfileDraft {
  profileName?: string
  fieldAccess?: Record<string, StageAccessRuleDraft>
  detailAccess?: Record<string, StageAccessRuleDraft>
  componentAccess?: Record<string, StageAccessRuleDraft>
  summary?: { fields: string[] }
}

export interface StageActionPolicyDraft {
  allowedActions: string[]
}

export interface StageDefinition {
  /** 本地 id，仅前端使用 */
  id: string
  /** 节点名称 */
  name: string
  /** 节点类型 */
  type: StageNodeType
  /** 处理粒度（仅 auto 节点使用） */
  processingGranularity?: ProcessingGranularity
  /** 排序号 */
  sortOrder: number

  // ===== 人工 =====
  approvalMode?: StageApprovalMode
  assigneeStrategy?: string
  assigneeConfigJson?: string
  inputFields?: string[]
  viewProfile?: StageViewProfileDraft
  actionPolicy?: StageActionPolicyDraft
  ccConfigJson?: string
  timeoutHours?: number

  // ===== 自动 =====
  /** 插件注册引用的 FID（CF自动插件注册.FID） */
  pluginRegistryId?: number
  /** 插件规则引用的 FID（CF自动插件规则.FID，可选） */
  pluginRuleId?: number
  failurePolicy?: 'skip' | 'halt' | 'retry'

  // ===== 通用 =====
  conditionJson?: string
}

const props = defineProps<{
  modelValue: StageDefinition[]
  schemaFields?: SchemaFieldDefinition[]
  detailSchemaFields?: SchemaFieldDefinition[]
  cardComponents?: CardComponentDefinition[]
}>()

const emit = defineEmits<{
  'update:modelValue': [val: StageDefinition[]]
  'change': [val: StageDefinition[]]
}>()

// ==================== 选项常量 ====================

const APPROVAL_MODES = [
  { value: 'single',       label: '单签', hint: '任一处理人通过即可' },
  { value: 'countersign',  label: '会签', hint: '所有处理人都需通过' },
  { value: 'orsign',       label: '或签', hint: '任一处理人通过即可继续' },
  { value: 'sequential',   label: '顺签', hint: '按人员顺序依次处理' },
] as const

const ASSIGNEE_STRATEGIES = [
  { value: 'role',      label: '按角色',   hint: '指定角色的成员处理' },
  { value: 'fixed',     label: '指定人员', hint: '固定指定的用户处理' },
  { value: 'fieldUsers', label: '按字段取人', hint: '从卡片人员字段中读取处理人' },
  { value: 'initiator', label: '发起人',   hint: '由流程发起人处理' },
]

const FALLBACK_OPTIONS = [
  { value: 'failSubmit', label: '提交失败', hint: '未解析到处理人时阻止提交，要求配置人员或修正字段数据' },
  { value: 'flowAdmin',  label: '审批管理员', hint: '未解析到处理人时转给流程配置中的审批管理员处理' },
] as const

const ACTION_OPTIONS = [
  { value: 'approve', label: '同意' },
  { value: 'reject', label: '退回发起人' },
  { value: 'returnToStage', label: '退回节点' },
  { value: 'transfer', label: '转办' },
  { value: 'addSignBefore', label: '前加签' },
  { value: 'addSignAfter', label: '后加签' },
  { value: 'cc', label: '抄送' },
  { value: 'urge', label: '催办' },
]

const DEFAULT_ACTIONS = ['approve', 'reject', 'returnToStage', 'transfer', 'addSignAfter', 'cc']

const ACCESS_OPTIONS = [
  { value: 'hidden', label: '隐藏' },
  { value: 'masked', label: '脱敏' },
  { value: 'readonly', label: '只读' },
  { value: 'editable', label: '可编辑' },
  { value: 'required', label: '必填' },
]

const FAILURE_POLICIES = [
  { value: 'skip',  label: '跳过', hint: '失败后继续下一节点' },
  { value: 'halt',  label: '中止', hint: '失败后流程中止' },
  { value: 'retry', label: '重试', hint: '自动重试 3 次' },
]

// ==================== 内部状态 ====================

const stages = ref<StageDefinition[]>(clone(props.modelValue || []))

watch(() => props.modelValue, (v) => {
  if (JSON.stringify(v) === JSON.stringify(stages.value)) return
  stages.value = clone(v || [])
}, { deep: true })

function clone<T>(v: T): T { return JSON.parse(JSON.stringify(v)) }

function emitUpdate() {
  // 重排 sortOrder
  stages.value.forEach((s, i) => { s.sortOrder = i + 1 })
  emit('update:modelValue', clone(stages.value))
  emit('change', clone(stages.value))
}

function genId() {
  return 'stg_' + Math.random().toString(36).slice(2, 10)
}

function newStage(type: StageNodeType): StageDefinition {
  return {
    id: genId(),
    name: type === 'manual' ? '审批节点' : '自动节点',
    type,
    sortOrder: stages.value.length + 1,
    processingGranularity: type === 'auto' ? 'card' : undefined,
    approvalMode: type === 'manual' ? 'single' : undefined,
    assigneeStrategy: type === 'manual' ? 'role' : undefined,
    inputFields: type === 'manual' ? [] : undefined,
    viewProfile: type === 'manual' ? { fieldAccess: {}, detailAccess: {}, summary: { fields: [] } } : undefined,
    actionPolicy: type === 'manual' ? { allowedActions: [...DEFAULT_ACTIONS] } : undefined,
    failurePolicy: type === 'auto' ? 'halt' : undefined,
  }
}

function addStage(type: StageNodeType) {
  const s = newStage(type)
  stages.value.push(s)
  emitUpdate()
  selectedIndex.value = stages.value.length - 1
  selectStage(selectedIndex.value)
}

function removeStage(idx: number) {
  Modal.confirm({
    title: '删除节点',
    content: `确定删除节点「${stages.value[idx]?.name || '未命名节点'}」？该节点的路由/配置将一并移除。`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk: () => doRemoveStage(idx),
  })
}

function doRemoveStage(idx: number) {
  stages.value.splice(idx, 1)
  if (selectedIndex.value === idx) {
    selectedIndex.value = -1
  } else if (selectedIndex.value > idx) {
    selectedIndex.value--
  }
  emitUpdate()
}

function onDragEnd() {
  // 拖拽后维护 selectedIndex（通过 id 重新定位）
  if (selectedIndex.value >= 0) {
    const currentId = stages.value[selectedIndex.value]?.id
    if (currentId) {
      const newIdx = stages.value.findIndex(s => s.id === currentId)
      if (newIdx !== selectedIndex.value) {
        selectedIndex.value = newIdx
      }
    }
  }
  emitUpdate()
}

// ==================== 选中与编辑 ====================

const selectedIndex = ref<number>(-1)
const selectedStage = computed(() =>
  selectedIndex.value >= 0 ? stages.value[selectedIndex.value] : null
)
const draftCondition = ref<ConditionGroup>({ logic: 'and', conditions: [] })
const activeConfigTab = ref<'basic' | 'assignee' | 'view' | 'actions' | 'condition'>('basic')

// ===== 处理人策略配置状态 =====
const roleOptions = ref<{ label: string; value: string }[]>([])
interface UserOption {
  label: string
  value: number
  userName: string
  orgName?: string
}

const userOptions = ref<UserOption[]>([])
const userSearchLoading = ref(false)
const editRoleCode = ref<string>('')
const editUserIds = ref<number[]>([])
const editFieldUserKey = ref<string>('')
const editFallbackType = ref<AssigneeFallbackType>('failSubmit')
// 防止回显期间被 strategy watch 误清
let suppressStrategyReset = false

function filterOption(input: string, option: any) {
  const text = String(option?.label ?? '').toLowerCase()
  return text.includes(String(input || '').toLowerCase())
}

// 简易 debounce
function debounce<T extends (...args: any[]) => any>(fn: T, wait = 300) {
  let timer: any = null
  return (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn(...args), wait)
  }
}

const onUserSearch = debounce(async (keyword: string) => {
  if (!keyword) {
    userOptions.value = []
    return
  }
  userSearchLoading.value = true
  try {
    const res: any = await getUserList({ keyword, pageIndex: 1, pageSize: 20 })
    const items = res?.items || res?.data?.items || (Array.isArray(res) ? res : [])
    userOptions.value = items.map((u: any) => ({
      label: formatUserOptionLabel(u),
      value: u.id,
      userName: getUserDisplayName(u),
      orgName: getUserOrgName(u),
    }))
  } catch (e) {
    console.warn('[StageDefinitionEditor] 加载用户列表失败:', e)
  } finally {
    userSearchLoading.value = false
  }
}, 300)

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

onMounted(async () => {
  try {
    const res: any = await getRoleList({ pageIndex: 1, pageSize: 200 })
    const list = res?.items || res?.list || (Array.isArray(res) ? res : [])
    roleOptions.value = list.map((r: any) => ({
      label: r.name,
      value: r.code,
    }))
  } catch (e) {
    console.warn('[StageDefinitionEditor] 加载角色列表失败:', e)
  }
})

// ===== 插件注册 & 插件规则加载状态 =====
const pluginRegistryAll = ref<AutoPluginRegistryDto[]>([])
const pluginRegistryLoading = ref(false)
const pluginRulesByCode = ref<Record<string, AutoPluginRuleDto[]>>({})
const pluginRulesLoading = ref(false)

async function loadPluginRegistry() {
  if (pluginRegistryAll.value.length || pluginRegistryLoading.value) return
  pluginRegistryLoading.value = true
  try {
    const res: any = await getPluginRegistry()
    pluginRegistryAll.value = (res?.items || res?.data || (Array.isArray(res) ? res : [])) as AutoPluginRegistryDto[]
  } catch (e) {
    console.warn('[StageDefinitionEditor] 加载插件注册列表失败:', e)
  } finally {
    pluginRegistryLoading.value = false
  }
}

async function loadPluginRules(pluginCode: string | undefined) {
  if (!pluginCode) return
  if (pluginRulesByCode.value[pluginCode]) return
  pluginRulesLoading.value = true
  try {
    const res: any = await getPluginRules(pluginCode)
    const list = (res?.items || res?.data || (Array.isArray(res) ? res : [])) as AutoPluginRuleDto[]
    pluginRulesByCode.value = { ...pluginRulesByCode.value, [pluginCode]: list }
  } catch (e) {
    console.warn('[StageDefinitionEditor] 加载插件规则列表失败:', e)
    pluginRulesByCode.value = { ...pluginRulesByCode.value, [pluginCode]: [] }
  } finally {
    pluginRulesLoading.value = false
  }
}

/** 根据当前选中节点的处理粒度过滤插件选项 */
const pluginOptions = computed(() => {
  const granularity = selectedStage.value?.processingGranularity
  const list = granularity
    ? pluginRegistryAll.value.filter(p => p.granularity === granularity)
    : pluginRegistryAll.value
  return list.map(p => ({ value: p.id, label: p.pluginName, code: p.pluginCode }))
})

/** 当前选中插件的 pluginCode，用于查找规则 */
const currentPluginCode = computed<string | undefined>(() => {
  const id = selectedStage.value?.pluginRegistryId
  if (!id) return undefined
  return pluginRegistryAll.value.find(p => p.id === id)?.pluginCode
})

/** 当前插件的规则选项 */
const pluginRuleOptions = computed(() => {
  const code = currentPluginCode.value
  if (!code) return []
  return (pluginRulesByCode.value[code] || []).map(r => ({ value: r.id, label: r.ruleName }))
})

function onPluginChange(newId: number | undefined) {
  if (selectedIndex.value < 0) return
  const stage = stages.value[selectedIndex.value]
  if (!stage) return
  stage.pluginRegistryId = newId
  // 切换插件后清空已选规则
  stage.pluginRuleId = undefined
  const code = pluginRegistryAll.value.find(p => p.id === newId)?.pluginCode
  if (code) loadPluginRules(code)
}

onMounted(() => {
  loadPluginRegistry()
})

function ensureStageConfigDefaults(stage: StageDefinition | null | undefined) {
  if (!stage || stage.type !== 'manual') return
  stage.inputFields ||= []
  stage.viewProfile ||= { fieldAccess: {}, detailAccess: {}, summary: { fields: [] } }
  stage.viewProfile.fieldAccess ||= {}
  stage.viewProfile.detailAccess ||= {}
  stage.viewProfile.componentAccess ||= {}
  stage.viewProfile.summary ||= { fields: [] }
  stage.actionPolicy ||= { allowedActions: [...DEFAULT_ACTIONS] }
  if (!stage.actionPolicy.allowedActions?.length) {
    stage.actionPolicy.allowedActions = [...DEFAULT_ACTIONS]
  }
}

function selectedManualStage() {
  const stage = selectedStage.value
  if (!stage || stage.type !== 'manual') return null
  ensureStageConfigDefaults(stage)
  return stage
}

function getFieldAccess(fieldKey: string): StageAccessMode {
  const stage = selectedManualStage()
  if (!stage) return 'readonly'
  const configured = stage.viewProfile?.fieldAccess?.[fieldKey]?.access
  if (configured) return configured
  return stage.inputFields?.includes(fieldKey) ? 'editable' : 'readonly'
}

function setFieldAccess(fieldKey: string, access: StageAccessMode) {
  const stage = selectedManualStage()
  if (!stage) return
  const existing = stage.viewProfile!.fieldAccess![fieldKey] || { access: 'readonly' as StageAccessMode }
  stage.viewProfile!.fieldAccess![fieldKey] = {
    ...existing,
    access,
    required: access === 'required' ? true : existing.required,
  }
  if (access === 'editable' || access === 'required') {
    stage.inputFields = Array.from(new Set([...(stage.inputFields || []), fieldKey]))
  } else {
    stage.inputFields = (stage.inputFields || []).filter(key => key !== fieldKey)
  }
}

function isFieldRequired(fieldKey: string) {
  const stage = selectedManualStage()
  if (!stage) return false
  const rule = stage.viewProfile?.fieldAccess?.[fieldKey]
  return rule?.access === 'required' || rule?.required === true
}

function setFieldRequired(fieldKey: string, checked: boolean) {
  const stage = selectedManualStage()
  if (!stage) return
  const access = checked ? 'required' : (getFieldAccess(fieldKey) === 'required' ? 'editable' : getFieldAccess(fieldKey))
  setFieldAccess(fieldKey, access)
  stage.viewProfile!.fieldAccess![fieldKey].required = checked
}

function toDetailAccessKey(fieldKey: string) {
  return `default.${fieldKey}`
}

function getDetailAccess(fieldKey: string): StageAccessMode {
  const stage = selectedManualStage()
  if (!stage) return 'readonly'
  const key = toDetailAccessKey(fieldKey)
  return stage.viewProfile?.detailAccess?.[key]?.access || 'readonly'
}

function setDetailAccess(fieldKey: string, access: StageAccessMode) {
  const stage = selectedManualStage()
  if (!stage) return
  const key = toDetailAccessKey(fieldKey)
  const existing = stage.viewProfile!.detailAccess![key] || { access: 'readonly' as StageAccessMode }
  stage.viewProfile!.detailAccess![key] = {
    ...existing,
    access,
    required: access === 'required' ? true : existing.required,
  }
}

function isDetailRequired(fieldKey: string) {
  const stage = selectedManualStage()
  if (!stage) return false
  const rule = stage.viewProfile?.detailAccess?.[toDetailAccessKey(fieldKey)]
  return rule?.access === 'required' || rule?.required === true
}

function setDetailRequired(fieldKey: string, checked: boolean) {
  const stage = selectedManualStage()
  if (!stage) return
  const access = checked ? 'required' : (getDetailAccess(fieldKey) === 'required' ? 'editable' : getDetailAccess(fieldKey))
  setDetailAccess(fieldKey, access)
  stage.viewProfile!.detailAccess![toDetailAccessKey(fieldKey)].required = checked
}

function isFallbackConfigStrategy(strategy?: string) {
  return strategy === 'role' || strategy === 'fixed' || strategy === 'fieldUsers'
}

function fallbackFromConfig(config: any): AssigneeFallbackType {
  return config?.fallback?.type === 'flowAdmin' ? 'flowAdmin' : 'failSubmit'
}

function parseAssigneeConfig(stage: StageDefinition) {
  if (!stage.assigneeConfigJson) return null
  try {
    return JSON.parse(stage.assigneeConfigJson)
  } catch {
    return null
  }
}

function buildAssigneeConfig(stage: StageDefinition) {
  const fallback = { type: editFallbackType.value }
  if (stage.assigneeStrategy === 'role') {
    return { roleCode: editRoleCode.value || '', users: [], fallback }
  }
  if (stage.assigneeStrategy === 'fixed') {
    const users = editUserIds.value.map(id => {
      const opt = userOptions.value.find(o => o.value === id)
      return { userId: id, userName: opt?.userName || '', orgName: opt?.orgName || null }
    })
    return { users, roleCode: null, fallback }
  }
  if (stage.assigneeStrategy === 'fieldUsers') {
    return { fieldKey: editFieldUserKey.value || '', fallback }
  }
  return null
}

function syncAssigneeConfig() {
  if (selectedIndex.value < 0 || suppressStrategyReset) return
  const stage = stages.value[selectedIndex.value]
  if (!stage || stage.type !== 'manual') return
  const config = buildAssigneeConfig(stage)
  stage.assigneeConfigJson = config ? JSON.stringify(config) : undefined
}

function selectStage(index: number) {
  selectedIndex.value = index
  const src = stages.value[index]
  if (!src) return
  activeConfigTab.value = 'basic'
  suppressStrategyReset = true
  // 回显处理人配置（仅人工节点）
  editRoleCode.value = ''
  editUserIds.value = []
  editFieldUserKey.value = ''
  editFallbackType.value = 'failSubmit'
  ensureStageConfigDefaults(src)
  if (src.assigneeConfigJson) {
    try {
      const config = JSON.parse(src.assigneeConfigJson)
      editRoleCode.value = config?.roleCode || ''
      editUserIds.value = (config?.users || []).map((u: any) => u.userId)
      editFieldUserKey.value = config?.fieldKey || ''
      editFallbackType.value = fallbackFromConfig(config)
      if (config?.users?.length) {
        userOptions.value = config.users.map((u: any) => ({
          label: formatUserOptionLabel(u),
          value: u.userId,
          userName: u.userName || String(u.userId),
          orgName: getUserOrgName(u),
        }))
      }
    } catch {}
  }
  // 解析条件
  try {
    draftCondition.value = src.conditionJson
      ? JSON.parse(src.conditionJson)
      : { logic: 'and', conditions: [] }
  } catch {
    draftCondition.value = { logic: 'and', conditions: [] }
  }
  // 预加载当前插件的规则列表
  if (src.type === 'auto' && src.pluginRegistryId) {
    const code = pluginRegistryAll.value.find(p => p.id === src.pluginRegistryId)?.pluginCode
    if (code) loadPluginRules(code)
  }
  // 释放抑制
  nextTick(() => { suppressStrategyReset = false })
}

// 策略变更时重置配置（跳过回显期间的变化）
watch(
  () => selectedStage.value?.assigneeStrategy,
  () => {
    if (suppressStrategyReset) return
    editRoleCode.value = ''
    editUserIds.value = []
    editFieldUserKey.value = ''
    editFallbackType.value = 'failSubmit'
    const stage = selectedStage.value
    if (stage) {
      if (isFallbackConfigStrategy(stage.assigneeStrategy)) {
        syncAssigneeConfig()
      } else {
        stage.assigneeConfigJson = undefined
      }
    }
  },
)

// 处理人配置序列化：角色变化时
watch(editRoleCode, (val) => {
  if (selectedIndex.value < 0 || suppressStrategyReset) return
  const stage = stages.value[selectedIndex.value]
  if (stage?.assigneeStrategy === 'role') {
    syncAssigneeConfig()
  }
})

// 处理人配置序列化：人员变化时
watch(editUserIds, () => {
  if (selectedIndex.value < 0 || suppressStrategyReset) return
  const stage = stages.value[selectedIndex.value]
  if (stage?.assigneeStrategy === 'fixed') {
    syncAssigneeConfig()
  }
}, { deep: true })

watch(editFieldUserKey, () => {
  if (selectedIndex.value < 0 || suppressStrategyReset) return
  const stage = stages.value[selectedIndex.value]
  if (stage?.assigneeStrategy === 'fieldUsers') {
    syncAssigneeConfig()
  }
})

watch(editFallbackType, () => {
  syncAssigneeConfig()
})

// 条件变化时写回
watch(draftCondition, (val) => {
  if (selectedIndex.value < 0) return
  const stage = stages.value[selectedIndex.value]
  if (!stage) return
  const hasCond = val && val.conditions && val.conditions.length > 0
  stage.conditionJson = hasCond ? JSON.stringify(val) : undefined
}, { deep: true })

// 处理粒度变更时，若已选插件与新粒度不匹配则清空
watch(
  () => selectedStage.value?.processingGranularity,
  (newGran) => {
    if (suppressStrategyReset) return
    const stage = selectedStage.value
    if (!stage || stage.type !== 'auto' || !newGran) return
    if (!stage.pluginRegistryId) return
    const plugin = pluginRegistryAll.value.find(p => p.id === stage.pluginRegistryId)
    if (plugin && plugin.granularity !== newGran) {
      stage.pluginRegistryId = undefined
      stage.pluginRuleId = undefined
    }
  },
)

// stages 变化时自动 emit
watch(stages, () => {
  emitUpdate()
}, { deep: true })

// ==================== 字段映射给 ConditionBuilder ====================

const conditionFields = computed<FieldOption[]>(() =>
  (props.schemaFields || []).map(f => ({ key: f.key, label: f.label, type: f.type }))
)

// ==================== 摘要展示 ====================

const operatorLabelMap: Record<string, string> = {
  eq: '=', neq: '≠', gt: '>', gte: '≥', lt: '<', lte: '≤',
  contains: '含', startsWith: '始于', in: '∈', between: '范围',
  exists: '存在', notExists: '不存在',
}

function fieldLabel(key: string) {
  return (props.schemaFields || []).find(f => f.key === key)?.label || key
}

function describeCond(g?: ConditionGroup): string {
  if (!g || !g.conditions.length) return ''
  const parts = g.conditions.map((c: any) => {
    if ('logic' in c) return `(${describeCond(c)})`
    const op = operatorLabelMap[c.operator] || c.operator
    const val = Array.isArray(c.value) ? c.value.join(',') : (c.value ?? '?')
    return `${fieldLabel(c.field)} ${op} ${val}`
  })
  const conn = g.logic === 'and' ? ' 且 ' : ' 或 '
  return parts.join(conn)
}

function stageCondSummary(stage: StageDefinition): string {
  if (!stage.conditionJson) return ''
  try {
    const g = JSON.parse(stage.conditionJson) as ConditionGroup
    return describeCond(g)
  } catch { return '' }
}

function approvalModeLabel(m?: string) {
  return APPROVAL_MODES.find(x => x.value === m)?.label || ''
}

function getStageHealth(stage: StageDefinition) {
  const issues: string[] = []
  const warnings: string[] = []

  if (!stage.name?.trim()) issues.push('节点名称未配置')

  if (stage.type === 'manual') {
    const config = parseAssigneeConfig(stage)
    if (!stage.approvalMode) issues.push('审批模式未配置')
    if (!stage.assigneeStrategy) {
      issues.push('处理人策略未配置')
    } else if (stage.assigneeStrategy === 'role' && !config?.roleCode) {
      issues.push('角色处理人未选择')
    } else if (stage.assigneeStrategy === 'fixed' && !(config?.users || []).length) {
      issues.push('固定处理人未选择')
    } else if (stage.assigneeStrategy === 'fieldUsers' && !config?.fieldKey) {
      issues.push('人员字段未选择')
    }
    if (!stage.actionPolicy?.allowedActions?.length) issues.push('允许动作未配置')
    if (!Object.keys(stage.viewProfile?.fieldAccess || {}).length) warnings.push('未单独配置卡片字段权限')
    if ((props.detailSchemaFields || []).length && !Object.keys(stage.viewProfile?.detailAccess || {}).length) {
      warnings.push('未单独配置明细字段权限')
    }
  }

  if (stage.type === 'auto') {
    if (!stage.processingGranularity) issues.push('处理粒度未配置')
    if (!stage.pluginRegistryId) issues.push('自动插件未选择')
    if (!stage.failurePolicy) warnings.push('失败策略未配置')
  }

  if (stage.conditionJson) {
    try {
      const g = JSON.parse(stage.conditionJson)
      if (!g || typeof g !== 'object' || !Array.isArray(g.conditions)) issues.push('进入条件格式异常')
    } catch {
      issues.push('进入条件 JSON 解析失败')
    }
  }

  return {
    status: issues.length ? 'error' : warnings.length ? 'warning' : 'ok',
    label: issues.length ? `${issues.length} 个问题` : warnings.length ? `${warnings.length} 个提醒` : '正常',
    issues,
    warnings,
  }
}

const stageCount = computed(() => stages.value.length)
</script>

<template>
  <section class="sde">
    <!-- 左栏：标题区 + 节点列表 + 添加按钮 -->
    <aside class="sde__left">
      <!-- 父组件通过 slot 传入的步骤标题与依赖信息栏 -->
      <slot name="left-header" />

      <!-- 节点链小标题 -->
      <header class="sde__head">
        <div class="sde__head-left">
          <span class="sde__title">节点链</span>
          <span class="sde__count">{{ stageCount }}</span>
        </div>
      </header>

      <!-- 节点列表区 -->
      <div v-if="stageCount === 0" class="sde__left-empty">
        <p>尚未添加节点</p>
        <span>点击下方按钮添加</span>
      </div>

      <draggable
        v-else
        v-model="stages"
        :animation="200"
        item-key="id"
        handle=".sde-node__handle"
        ghost-class="sde-node--ghost"
        class="sde__left-list"
        @end="onDragEnd"
      >
        <template #item="{ element, index }">
          <div
            class="sde-line"
            :class="{ 'sde-line--active': selectedIndex === index }"
            @click="selectStage(index)"
          >
            <!-- 序号圆圈 + 竖线 -->
            <div class="sde-line__rail">
              <div
                class="sde-line__dot"
                :class="[
                  `sde-line__dot--${element.type}`,
                  element.type === 'auto' && element.processingGranularity === 'batch' ? 'sde-line__dot--batch' : ''
                ]"
              >
                {{ index + 1 }}
              </div>
              <div v-if="index < stages.length - 1" class="sde-line__bar" />
            </div>

            <!-- 节点卡片 -->
            <div class="sde-node-wrap">
              <article
                class="sde-node"
                :class="[
                  `sde-node--${element.type}`,
                  element.type === 'auto' && element.processingGranularity === 'batch' ? 'sde-node--batch' : '',
                  { 'sde-node--selected': selectedIndex === index }
                ]"
              >
                <span class="sde-node__icon">
                  <UserOutlined v-if="element.type === 'manual'" />
                  <ThunderboltOutlined v-else-if="element.processingGranularity === 'batch'" />
                  <RobotOutlined v-else />
                </span>
                <span class="sde-node__body">
                  <span class="sde-node__name">{{ element.name || '未命名' }}</span>
                  <a-tooltip :title="[...getStageHealth(element).issues, ...getStageHealth(element).warnings].join('；') || '节点健康正常'">
                    <span
                      class="sde-node-health"
                      :class="`sde-node-health--${getStageHealth(element).status}`"
                    >
                      {{ getStageHealth(element).label }}
                    </span>
                  </a-tooltip>
                </span>
                <div class="sde-node__actions" @click.stop>
                  <span class="sde-node__handle"><HolderOutlined /></span>
                  <button class="sde-node__del" @click.stop="removeStage(index)">
                    <DeleteOutlined />
                  </button>
                </div>
              </article>
            </div>
          </div>
        </template>
      </draggable>

      <!-- 底部添加按钮 -->
      <div class="sde__foot">
        <button class="sde__add sde__add--manual" @click="addStage('manual')">
          <UserOutlined />
          <span>人工</span>
        </button>
        <button class="sde__add sde__add--auto" @click="addStage('auto')">
          <RobotOutlined />
          <span>自动</span>
        </button>
      </div>
    </aside>

    <!-- 右栏：编辑面板（从顶部开始，最大空间） -->
    <section class="sde__right">
        <!-- 空状态 -->
        <div v-if="!selectedStage" class="sde__right-empty">
          <span class="sde__right-empty-icon">⤳</span>
          <p>点击左侧节点进行编辑</p>
        </div>

        <!-- 编辑面板 -->
        <div v-else class="sde__editor">
          <div
            class="sde-health"
            :class="`sde-health--${getStageHealth(selectedStage).status}`"
          >
            <div class="sde-health__title">
              <CheckCircleOutlined v-if="getStageHealth(selectedStage).status === 'ok'" />
              <ExclamationCircleOutlined v-else />
              <span>节点健康</span>
              <strong>{{ getStageHealth(selectedStage).label }}</strong>
            </div>
            <div
              v-if="getStageHealth(selectedStage).issues.length || getStageHealth(selectedStage).warnings.length"
              class="sde-health__body"
            >
              <span
                v-for="item in [...getStageHealth(selectedStage).issues, ...getStageHealth(selectedStage).warnings]"
                :key="item"
              >
                {{ item }}
              </span>
            </div>
          </div>

          <a-tabs v-model:active-key="activeConfigTab" size="small" class="sde-tabs">
            <a-tab-pane key="basic" tab="基础">
              <div class="sde-tab-panel">
                <div
                  class="sde-editor__type-badge"
                  :class="[
                    `sde-editor__type-badge--${selectedStage.type}`,
                    selectedStage.type === 'auto' && selectedStage.processingGranularity === 'batch' ? 'sde-editor__type-badge--batch' : ''
                  ]"
                >
                  <UserOutlined v-if="selectedStage.type === 'manual'" />
                  <ThunderboltOutlined v-else-if="selectedStage.processingGranularity === 'batch'" />
                  <RobotOutlined v-else />
                  <span>{{ selectedStage.type === 'manual' ? '人工节点' : '自动节点' }}</span>
                </div>

                <div class="sde-fld">
                  <label class="sde-fld__label">节点名称 <span class="sde-fld__req">*</span></label>
                  <a-input v-model:value="selectedStage.name" placeholder="例：部门主管审批" />
                </div>

                <div v-if="selectedStage.type === 'manual'" class="sde-fld">
                  <label class="sde-fld__label">审批模式</label>
                  <a-radio-group v-model:value="selectedStage.approvalMode" button-style="solid" size="small">
                    <a-radio-button v-for="m in APPROVAL_MODES" :key="m.value" :value="m.value">
                      {{ m.label }}
                    </a-radio-button>
                  </a-radio-group>
                  <p class="sde-fld__hint">
                    {{ APPROVAL_MODES.find(m => m.value === selectedStage!.approvalMode)?.hint }}
                  </p>
                </div>

                <div v-if="selectedStage.type === 'auto'" class="sde-fld">
                  <label class="sde-fld__label">处理粒度</label>
                  <a-select v-model:value="selectedStage.processingGranularity" style="width: 100%">
                    <a-select-option value="card">卡片级 — 每张卡片独立经过此节点</a-select-option>
                    <a-select-option value="batch">批次级 — 作用于整个上传批次</a-select-option>
                  </a-select>
                </div>
              </div>
            </a-tab-pane>

            <a-tab-pane key="assignee" tab="处理人" :disabled="selectedStage.type !== 'manual'">
              <div v-if="selectedStage.type === 'manual'" class="sde-tab-panel">
                <div class="sde-fld">
                  <label class="sde-fld__label">处理人策略</label>
                  <a-select
                    v-model:value="selectedStage.assigneeStrategy"
                    style="width: 100%"
                    :options="ASSIGNEE_STRATEGIES.map(s => ({ value: s.value, label: s.label }))"
                    placeholder="请选择"
                  />
                  <p v-if="selectedStage.assigneeStrategy" class="sde-fld__hint">
                    {{ ASSIGNEE_STRATEGIES.find(s => s.value === selectedStage!.assigneeStrategy)?.hint }}
                  </p>
                </div>

                <div v-if="selectedStage.assigneeStrategy === 'role'" class="sde-fld">
                  <label class="sde-fld__label">选择角色</label>
                  <a-select
                    v-model:value="editRoleCode"
                    style="width: 100%"
                    placeholder="请选择角色"
                    :options="roleOptions"
                    show-search
                    :filter-option="filterOption"
                  />
                </div>

                <div v-if="selectedStage.assigneeStrategy === 'fixed'" class="sde-fld">
                  <label class="sde-fld__label">选择人员</label>
                  <a-select
                    v-model:value="editUserIds"
                    mode="multiple"
                    style="width: 100%"
                    placeholder="搜索并选择人员"
                    :options="userOptions"
                    :loading="userSearchLoading"
                    show-search
                    @search="onUserSearch"
                    option-filter-prop="label"
                    :filter-option="filterOption"
                  />
                  <p class="sde-fld__hint">输入关键词可搜索用户（姓名 / 账号 / 部门）</p>
                </div>

                <div v-if="selectedStage.assigneeStrategy === 'fieldUsers'" class="sde-fld">
                  <label class="sde-fld__label">人员字段</label>
                  <a-select
                    v-model:value="editFieldUserKey"
                    style="width: 100%"
                    placeholder="请选择卡片中的人员字段"
                    :options="(schemaFields || []).map(f => ({ value: f.key, label: f.label }))"
                    show-search
                    :filter-option="filterOption"
                  />
                </div>

                <div v-if="isFallbackConfigStrategy(selectedStage.assigneeStrategy)" class="sde-fld">
                  <label class="sde-fld__label">处理人兜底</label>
                  <a-radio-group v-model:value="editFallbackType" button-style="solid" size="small">
                    <a-radio-button v-for="option in FALLBACK_OPTIONS" :key="option.value" :value="option.value">
                      {{ option.label }}
                    </a-radio-button>
                  </a-radio-group>
                  <p class="sde-fld__hint">
                    {{ FALLBACK_OPTIONS.find(option => option.value === editFallbackType)?.hint }}
                  </p>
                </div>
              </div>
            </a-tab-pane>

            <a-tab-pane key="view" tab="节点视图" :disabled="selectedStage.type !== 'manual'">
              <div v-if="selectedStage.type === 'manual'" class="sde-tab-panel">
                <div class="sde-fld">
                  <label class="sde-fld__label">补充字段</label>
                  <a-select
                    v-model:value="selectedStage.inputFields"
                    mode="multiple"
                    style="width: 100%"
                    placeholder="本节点处理人需补充填写的字段"
                    :options="(schemaFields || []).map(f => ({ value: f.key, label: f.label }))"
                  />
                </div>

                <div class="sde-fld sde-fld--block">
                  <div class="sde-fld__label-row">
                    <label class="sde-fld__label">字段展示权限</label>
                    <span class="sde-fld__hint">按节点职责配置可见、可写和必填</span>
                  </div>
                  <div class="sde-access">
                    <div v-for="field in (schemaFields || [])" :key="field.key" class="sde-access__row">
                      <span class="sde-access__name" :title="field.label">{{ field.label }}</span>
                      <a-select
                        class="sde-access__select"
                        size="small"
                        :value="getFieldAccess(field.key)"
                        :options="ACCESS_OPTIONS"
                        @change="(value: any) => setFieldAccess(field.key, value)"
                      />
                      <a-checkbox
                        :checked="isFieldRequired(field.key)"
                        @change="(event: any) => setFieldRequired(field.key, event.target.checked)"
                      >
                        必填
                      </a-checkbox>
                    </div>
                  </div>
                </div>

                <div class="sde-fld sde-fld--block">
                  <div class="sde-fld__label-row">
                    <label class="sde-fld__label">明细字段权限</label>
                    <span class="sde-fld__hint">同一套明细数据可按节点职责分层展示</span>
                  </div>
                  <div v-if="(detailSchemaFields || []).length" class="sde-access">
                    <div v-for="field in (detailSchemaFields || [])" :key="field.key" class="sde-access__row">
                      <span class="sde-access__name" :title="field.label">{{ field.label }}</span>
                      <a-select
                        class="sde-access__select"
                        size="small"
                        :value="getDetailAccess(field.key)"
                        :options="ACCESS_OPTIONS"
                        @change="(value: any) => setDetailAccess(field.key, value)"
                      />
                      <a-checkbox
                        :checked="isDetailRequired(field.key)"
                        @change="(event: any) => setDetailRequired(field.key, event.target.checked)"
                      >
                        必填
                      </a-checkbox>
                    </div>
                  </div>
                  <div v-else class="sde-access__empty">暂无明细字段</div>
                </div>

                <div class="sde-fld">
                  <label class="sde-fld__label">摘要字段</label>
                  <a-select
                    v-model:value="selectedStage.viewProfile!.summary!.fields"
                    mode="multiple"
                    style="width: 100%"
                    placeholder="卡片摘要区优先展示的字段"
                    :options="(schemaFields || []).map(f => ({ value: f.key, label: f.label }))"
                  />
                </div>

                <div class="sde-fld sde-fld--block">
                  <StageComponentViewEditor
                    :components="cardComponents || []"
                    v-model="selectedStage.viewProfile!.componentAccess"
                  />
                </div>
              </div>
            </a-tab-pane>

            <a-tab-pane key="actions" :tab="selectedStage.type === 'manual' ? '动作/时限' : '执行配置'">
              <div class="sde-tab-panel">
                <template v-if="selectedStage.type === 'manual'">
                  <div class="sde-fld">
                    <label class="sde-fld__label">允许动作</label>
                    <a-select
                      v-model:value="selectedStage.actionPolicy!.allowedActions"
                      mode="multiple"
                      style="width: 100%"
                      placeholder="当前节点可执行的审批动作"
                      :options="ACTION_OPTIONS"
                    />
                  </div>

                  <div class="sde-fld">
                    <label class="sde-fld__label">抄送配置</label>
                    <a-input v-model:value="selectedStage.ccConfigJson" placeholder="抄送人员/角色 JSON" />
                  </div>

                  <div class="sde-fld">
                    <label class="sde-fld__label">超时（小时）</label>
                    <a-input-number
                      v-model:value="selectedStage.timeoutHours"
                      :min="0"
                      placeholder="0 表示不限制"
                      style="width: 120px"
                    />
                  </div>
                </template>

                <template v-else>
                  <div class="sde-fld">
                    <label class="sde-fld__label">插件 <span class="sde-fld__req">*</span></label>
                    <a-select
                      :value="selectedStage.pluginRegistryId"
                      :options="pluginOptions"
                      :loading="pluginRegistryLoading"
                      style="width: 100%"
                      placeholder="请选择插件"
                      show-search
                      :filter-option="filterOption"
                      allow-clear
                      @change="(v: any) => onPluginChange(v)"
                    />
                    <p v-if="!pluginRegistryLoading && pluginOptions.length === 0" class="sde-fld__hint">
                      当前处理粒度下暂无可用插件
                    </p>
                  </div>

                  <div class="sde-fld">
                    <label class="sde-fld__label">插件规则</label>
                    <a-select
                      v-model:value="selectedStage.pluginRuleId"
                      :options="pluginRuleOptions"
                      :loading="pluginRulesLoading"
                      :disabled="!selectedStage.pluginRegistryId"
                      style="width: 100%"
                      :placeholder="selectedStage.pluginRegistryId
                        ? (pluginRuleOptions.length ? '请选择插件规则' : '无需选择规则')
                        : '请先选择插件'"
                      show-search
                      :filter-option="filterOption"
                      allow-clear
                    />
                  </div>

                  <div class="sde-fld">
                    <label class="sde-fld__label">失败策略</label>
                    <a-radio-group v-model:value="selectedStage.failurePolicy" button-style="solid" size="small">
                      <a-radio-button v-for="p in FAILURE_POLICIES" :key="p.value" :value="p.value">
                        {{ p.label }}
                      </a-radio-button>
                    </a-radio-group>
                    <p class="sde-fld__hint">
                      {{ FAILURE_POLICIES.find(p => p.value === selectedStage!.failurePolicy)?.hint }}
                    </p>
                  </div>
                </template>
              </div>
            </a-tab-pane>

            <a-tab-pane key="condition" tab="进入条件">
              <div class="sde-tab-panel">
                <div class="sde-fld sde-fld--block">
                  <div class="sde-fld__label-row">
                    <label class="sde-fld__label">进入条件</label>
                    <span class="sde-fld__hint">满足时此节点激活，否则跳过</span>
                  </div>
                  <ConditionBuilder v-model="draftCondition" :fields="conditionFields" />
                </div>
              </div>
            </a-tab-pane>
          </a-tabs>
        </div>
    </section>
  </section>
</template>

<style scoped lang="scss">
.sde {
  display: flex;
  flex-direction: row;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 10px;
  overflow: hidden;
  height: 100%;
  min-height: 0;
}

.sde__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
}
.sde__head-left { display: flex; align-items: center; gap: 10px; }
.sde__title { font-size: 14px; font-weight: 600; color: var(--text-1); letter-spacing: 0.4px; }
.sde__count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 20px;
  padding: 0 6px;
  border-radius: 10px;
  background: var(--text-1);
  color: var(--text-on-accent);
  font-size: 11px;
  font-weight: 600;
}
.sde__head-hint {
  font-size: 11px;
  color: var(--text-3);
  font-style: italic;
}

/* ============ 左右分栏 ============ */
.sde__left {
  width: 280px;
  min-width: 280px;
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  background: var(--bg-muted);
  overflow-y: auto;
}

.sde__left-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 16px;
  color: var(--text-3);
  text-align: center;
  flex: 1;
  p { margin: 0; font-size: 13px; color: var(--text-2); }
  span { font-size: 12px; margin-top: 4px; }
}

.sde__left-list {
  flex: 1;
  padding: 8px;
  min-height: 0;
}

.sde__right {
  flex: 1;
  padding: 16px 20px;
  overflow-y: auto;
  min-width: 0;
}

.sde__right-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--text-3);
  text-align: center;
  gap: 8px;

  .sde__right-empty-icon { font-size: 32px; color: var(--text-3); }
  p { margin: 0; font-size: 14px; }
}

.sde__editor {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.sde-editor__type-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  background: var(--bg-muted);
  color: var(--text-2);
  width: fit-content;

  &--manual { background: color-mix(in srgb, var(--cf-node-manual) 8%, transparent); color: var(--cf-node-manual); }
  &--auto { background: color-mix(in srgb, var(--cf-node-auto) 8%, transparent); color: var(--cf-node-auto); }
  &--batch { background: color-mix(in srgb, var(--cf-node-batch) 8%, transparent); color: var(--cf-node-batch); }
}

/* ============ 时间线节点 ============ */
.sde-line {
  display: flex;
  align-items: stretch;
  gap: 8px;
  position: relative;
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 6px;
  transition: background .15s;

  &:hover { background: var(--color-primary-light); }
  &--active { background: var(--bg-muted); }
  &--ghost { opacity: 0.4; }
}

.sde-line__rail {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 24px;
  flex-shrink: 0;
}

.sde-line__dot {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  background: var(--bg-card);
  border: 2px solid var(--text-1);
  color: var(--text-1);
  font-variant-numeric: tabular-nums;

  &--manual { border-color: var(--cf-node-manual); color: var(--cf-node-manual); }
  &--auto   { border-color: var(--cf-node-auto); color: var(--cf-node-auto); }
  &--batch  { border-color: var(--cf-node-batch); color: var(--cf-node-batch); }
}
.sde-line__bar {
  flex: 1;
  width: 2px;
  background: repeating-linear-gradient(
    to bottom,
    var(--border) 0,
    var(--border) 3px,
    transparent 3px,
    transparent 6px
  );
  min-height: 12px;
  margin-top: 2px;
}

.sde-node-wrap {
  flex: 1;
  padding-bottom: 6px;
  min-width: 0;
}

.sde-node {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  transition: border-color .18s, box-shadow .18s;
  font-size: 13px;

  &--manual { border-left: 3px solid var(--cf-node-manual); }
  &--auto   { border-left: 3px solid var(--cf-node-auto); }
  &--batch  { border-left: 3px solid var(--cf-node-batch); }

  &--selected {
    background: var(--bg-muted);
    border-color: var(--border-strong);
    box-shadow: 0 0 0 2px var(--border-strong);
  }

  &:hover {
    border-color: var(--text-1);
    .sde-node__del { opacity: 1; transform: translateX(0); }
  }
}

.sde-node__icon {
  width: 22px; height: 22px;
  display: flex; align-items: center; justify-content: center;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  font-size: 11px;
  color: var(--text-2);
  flex-shrink: 0;
}

.sde-node__body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.sde-node__name {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sde-node-health {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  max-width: 100%;
  padding: 1px 5px;
  border-radius: 4px;
  font-size: 10.5px;
  line-height: 16px;
  border: 1px solid transparent;

  &--ok {
    color: var(--color-success-text);
    background: var(--color-success-light);
    border-color: var(--color-success-border);
  }

  &--warning {
    color: var(--color-warning-text);
    background: var(--color-warning-light);
    border-color: var(--color-warning-border);
  }

  &--error {
    color: var(--color-danger-text);
    background: var(--color-danger-light);
    border-color: var(--color-danger-border);
  }
}

.sde-node__actions {
  display: flex;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
}
.sde-node__handle {
  cursor: grab;
  color: var(--text-3);
  font-size: 12px;
  &:active { cursor: grabbing; }
}
.sde-node__del {
  border: none;
  background: transparent;
  color: var(--color-danger);
  width: 22px; height: 22px;
  border-radius: 4px;
  cursor: pointer;
  opacity: 0;
  transform: translateX(4px);
  transition: opacity .18s, transform .18s, background .18s;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  &:hover { background: var(--color-danger-light); }
}

/* ============ 添加按钮组 ============ */
.sde__foot {
  display: flex;
  gap: 6px;
  padding: 10px 8px;
  border-top: 1px dashed var(--border);
  margin-top: auto;
}
.sde__add {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 6px 8px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  background: var(--bg-card);
  cursor: pointer;
  font-size: 11px;
  color: var(--text-2);
  transition: all .18s ease;

  &--manual:hover { border-color: var(--cf-node-manual); color: var(--cf-node-manual); background: color-mix(in srgb, var(--cf-node-manual) 8%, transparent); }
  &--auto:hover   { border-color: var(--cf-node-auto); color: var(--cf-node-auto); background: color-mix(in srgb, var(--cf-node-auto) 8%, transparent); }
}

.sde-health {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--bg-muted);

  &--ok {
    background: var(--color-success-light);
    border-color: var(--color-success-border);
    color: var(--color-success-text);
  }

  &--warning {
    background: var(--color-warning-light);
    border-color: var(--color-warning-border);
    color: var(--color-warning-text);
  }

  &--error {
    background: var(--color-danger-light);
    border-color: var(--color-danger-border);
    color: var(--color-danger-text);
  }
}

.sde-health__title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;

  strong {
    margin-left: auto;
    font-size: 12px;
  }
}

.sde-health__body {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;

  span {
    padding: 2px 6px;
    border-radius: 4px;
    background: color-mix(in srgb, var(--bg-card) 70%, transparent);
    font-size: 11px;
    color: inherit;
  }
}

.sde-tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: 12px;
  }

  :deep(.ant-tabs-tab) {
    padding: 7px 0;
    font-size: 12px;
  }
}

.sde-tab-panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

/* ============ 表单字段 ============ */
.sde-fld {
  display: flex;
  flex-direction: column;
  gap: 6px;

  &--block { padding: 12px; background: var(--bg-muted); border-radius: 8px; }

  &__label-row { display: flex; justify-content: space-between; align-items: center; }
  &__label {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-2);
    letter-spacing: 0.3px;
  }
  &__req { color: var(--color-danger); }
  &__hint { margin: 0; font-size: 11px; color: var(--text-3); font-style: italic; }
  &__mono :deep(textarea) {
    font-family: 'JetBrains Mono', 'SF Mono', Consolas, monospace;
    font-size: 12px;
  }
}

.sde-access {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 260px;
  overflow: auto;
  padding-right: 2px;
}

.sde-access__row {
  display: grid;
  grid-template-columns: minmax(96px, 1fr) 116px 58px;
  align-items: center;
  gap: 8px;
  min-height: 32px;
  padding: 4px 6px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);
}

.sde-access__name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: var(--text-2);
}

.sde-access__select {
  width: 116px;
}

.sde-access__empty {
  min-height: 34px;
  display: flex;
  align-items: center;
  padding: 0 8px;
  border: 1px dashed var(--border-strong);
  border-radius: 6px;
  background: var(--bg-card);
  font-size: 12px;
  color: var(--text-3);
}

@media (max-width: 1080px) {
  .sde {
    flex-direction: column;
  }

  .sde__left {
    width: 100%;
    min-width: 0;
    max-height: 260px;
    border-right: none;
    border-bottom: 1px solid var(--border);
  }

  .sde__right {
    padding: 14px;
  }
}
</style>
