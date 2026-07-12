<script setup lang="ts">
/**
 * StageConfigPanel —— 选中节点的属性面板（五 Tab：基础/处理人/字段权限/动作/高级，M2-1 对齐 mock A4-A8）
 *
 * 从 StageDefinitionEditor 右栏零逻辑抽出的共用面板，以便节点链右栏与画布节点抽屉共用同一套
 * 完整配置能力（消灭双入口能力差）。
 *
 * 契约（保持原右栏行为）：
 *  - 通过 `stages`（整表）+ `selectedIndex` 定位当前编辑节点，就地修改 stages[selectedIndex]；
 *    上层（StageDefinitionEditor）对 stages 的 deep watch 负责 emitUpdate，本面板不再另发事件。
 *  - 选中节点变化（切换选择 / 撤销重做整体替换致对象换新）经 watch(selectedStage) 回显编辑态，
 *    等价于原 selectStage 的调用时机；就地编辑不改变 selectedStage 引用故不触发回显。
 *  - 只暴露引擎已实现的配置（M2 裁剪决策）：超时提醒接 StageTimeoutReminderJob（M2-7）；
 *    自动通过语义=进入条件跳过（引擎既有）；抄送/通知（ccConfigJson，M8-B）见「高级」Tab，
 *    仅人工节点可配——引擎 onEnter/onApprove/onReject 三触点消费，与「抄送节点」
 *    （auto 节点 + AlertNotify 插件，见 stageDefinitionShared.ts）是两套并行机制。
 */
import { computed, onMounted, ref, watch, nextTick } from 'vue'
import {
  UserOutlined,
  RobotOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons-vue'
import ConditionBuilder from './ConditionBuilder.vue'
import StageComponentViewEditor from './designer/StageComponentViewEditor.vue'
import PermissionTri from './designer/PermissionTri.vue'
import type { PermissionValue } from './designer/permissionTriShared'
import type { ConditionGroup, FieldOption } from './ConditionBuilder.vue'
import type { StageDefinition, StageNodeType, StageAccessMode, AssigneeFallbackType } from './StageDefinitionEditor.vue'
import type { CardComponentDefinition, SchemaFieldDefinition, AutoPluginRegistryDto, AutoPluginRuleDto } from '@/types/cardflow'
import { getRoleList, getUserList } from '@/api/system'
import { getPluginRegistry, getPluginRules } from '@/api/cardflow'
import { DEFAULT_ACTIONS, parseAssigneeConfig, normalizeAssigneeStrategy, getStageHealth as computeStageHealth } from './stageDefinitionShared'
import { useUserSearch } from '@/composables/useUserSearch'
import { CC_CHANNEL_OPTIONS, CC_TIMING_OPTIONS, emptyCcConfig, parseCcConfig, serializeCcConfig, type CcNotifyConfig } from './ccConfigShared'
import { TIMEOUT_ACTION_OPTIONS, emptyTimeoutActionConfig, parseTimeoutAction, serializeTimeoutAction, type TimeoutActionConfig } from './timeoutActionShared'

const props = defineProps<{
  stages: StageDefinition[]
  selectedIndex: number
  schemaFields?: SchemaFieldDefinition[]
  detailSchemaFields?: SchemaFieldDefinition[]
  cardComponents?: CardComponentDefinition[]
  /** 被路由条件引用的字段 key 集合（字段权限 Tab 锁只读：路由字段不可 hidden） */
  routeReferencedFieldKeys?: Set<string>
}>()

// ==================== 选项常量 ====================

const APPROVAL_MODES = [
  { value: 'single',       label: '单签', hint: '任一处理人通过即可' },
  { value: 'countersign',  label: '会签', hint: '所有处理人都需通过' },
  { value: 'orsign',       label: '或签', hint: '任一处理人通过即可继续' },
  { value: 'sequential',   label: '顺签', hint: '按人员顺序依次处理' },
  { value: 'ratio',        label: '比例会签', hint: '达到指定通过比例即可' },
] as const

const ASSIGNEE_STRATEGIES = [
  { value: 'role',      label: '按角色',   hint: '指定角色的成员处理' },
  { value: 'fixed',     label: '指定人员', hint: '固定指定的用户处理' },
  { value: 'fieldUsers', label: '按字段取人', hint: '从卡片人员字段中读取处理人' },
  { value: 'orgChain',  label: '组织链主管', hint: '沿组织树逐级取负责人（连续多级主管）' },
  { value: 'superiorChain', label: '连续多级主管', hint: '从发起人沿直属上级链逐级向上取 N 级主管' },
  { value: 'initiator', label: '发起人',   hint: '由流程发起人处理' },
]

const FALLBACK_OPTIONS = [
  { value: 'failSubmit', label: '提交失败', hint: '未解析到处理人时阻止提交，要求配置人员或修正字段数据' },
  { value: 'flowAdmin',  label: '审批管理员', hint: '未解析到处理人时转给流程配置中的审批管理员处理' },
  { value: 'fixedUsers', label: '转交指定人', hint: '未解析到处理人时转交给下方指定的人员' },
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

// 引擎实现的失败策略仅 halt/retry（AutoStage 执行链）；skip 未实现故不出（M2 裁剪：不做假配置）
const FAILURE_POLICIES = [
  { value: 'halt',  label: '中止', hint: '失败后流程中止（转异常处理）' },
  { value: 'retry', label: '重试', hint: '自动重试 3 次，超限后中止' },
]

// 审批类型三态（mock A4；引擎 TryApplyAutoDecisionAsync 消费信封 autoDecision）
const AUTO_DECISION_MODES = [
  { value: 'none' as const, label: '人工审批', desc: '由处理人手动审批' },
  { value: 'autoApprove' as const, label: '满足条件时自动通过', desc: '如：报销金额 ≤ 100 时本节点自动通过' },
  { value: 'autoReject' as const, label: '满足条件时自动拒绝', desc: '如：事假时长 > 15 天时自动拒绝并退回' },
]

// 意见必填可配动作（引擎 EnsureOpinionProvidedAsync 校验的四动作）
const OPINION_REQUIRED_OPTIONS = [
  { value: 'approve', label: '同意' },
  { value: 'reject', label: '退回发起人' },
  { value: 'returnToStage', label: '退回节点' },
  { value: 'transfer', label: '转办' },
]

// 自定义动作处理器（M8-C）：webhook 系外部 URL 调用（SSRF 面），设计器置灰暂不支持
const CUSTOM_ACTION_HANDLER_OPTIONS = [
  { value: 'autoApprove', label: '自动通过当前节点', disabled: false },
  { value: 'autoReject', label: '自动驳回', disabled: false },
  { value: 'notify', label: '触发通知/抄送', disabled: false },
  { value: 'webhook', label: 'Webhook（外部调用，暂不支持）', disabled: true },
] as const

/** 意见必填动作双向代理（actionPolicy 可能无此键，就地建） */
const opinionRequiredActions = computed<string[]>({
  get: () => selectedStage.value?.actionPolicy?.opinionRequiredActions ?? [],
  set: (val) => {
    const stage = selectedManualStage()
    if (!stage?.actionPolicy) return
    stage.actionPolicy.opinionRequiredActions = val
  },
})

/** 动作 Tab 逐行启用/禁用（mock A7 toggle 式） */
function toggleAction(action: string, enabled: boolean) {
  const stage = selectedManualStage()
  if (!stage?.actionPolicy) return
  const arr = stage.actionPolicy.allowedActions || []
  if (enabled && !arr.includes(action)) {
    stage.actionPolicy.allowedActions = [...arr, action]
  } else if (!enabled) {
    stage.actionPolicy.allowedActions = arr.filter(a => a !== action)
    // 同步移除意见必填中已禁用的动作
    if (stage.actionPolicy.opinionRequiredActions?.includes(action)) {
      stage.actionPolicy.opinionRequiredActions = stage.actionPolicy.opinionRequiredActions.filter(a => a !== action)
    }
  }
}

function toggleOpinionRequired(action: string, required: boolean) {
  const stage = selectedManualStage()
  if (!stage?.actionPolicy) return
  const arr = stage.actionPolicy.opinionRequiredActions || []
  if (required && !arr.includes(action)) {
    stage.actionPolicy.opinionRequiredActions = [...arr, action]
  } else if (!required) {
    stage.actionPolicy.opinionRequiredActions = arr.filter(a => a !== action)
  }
}

/** 自定义动作按钮（M8-C）：新增行默认 autoApprove，编号/文案留空待填 */
function addCustomAction() {
  const stage = selectedManualStage()
  if (!stage?.actionPolicy) return
  stage.actionPolicy.customActions ||= []
  stage.actionPolicy.customActions.push({ code: '', label: '', handler: 'autoApprove', requireOpinion: false })
}

function removeCustomAction(index: number) {
  const stage = selectedManualStage()
  if (!stage?.actionPolicy?.customActions) return
  stage.actionPolicy.customActions.splice(index, 1)
}

/** viewProfile access（含 required 复合态）→ 权限胶囊四态（required 显示为可编辑，另有必填勾） */
function triValueOf(access: StageAccessMode): PermissionValue {
  return access === 'required' ? 'editable' : access
}

/** 字段权限锁定状态：敏感字段不可 editable、路由引用字段不可 hidden（mock A6/E3） */
function fieldLockedStates(field: SchemaFieldDefinition): PermissionValue[] {
  const locked: PermissionValue[] = []
  if (field.sensitive) locked.push('editable')
  if (props.routeReferencedFieldKeys?.has(field.key)) locked.push('hidden')
  return locked
}

function fieldLockReason(field: SchemaFieldDefinition): string {
  const reasons: string[] = []
  if (field.sensitive) reasons.push('敏感字段不可设为「可编辑」')
  if (props.routeReferencedFieldKeys?.has(field.key)) reasons.push('该字段被路由条件引用，不可隐藏')
  return reasons.join('；') || '该项被锁定'
}

/** 字段权限批量设置（mock A6 表头批量；hidden/masked 锁定字段不受影响由 setFieldAccess 语义保证） */
function batchSetFieldAccess(access: PermissionValue) {
  for (const field of props.schemaFields || []) {
    setFieldAccess(field.key, access)
  }
}

function setAutoDecisionMode(mode: 'none' | 'autoApprove' | 'autoReject') {
  autoDecisionMode.value = mode
  syncAutoDecision()
}

function syncAutoDecision() {
  const stage = selectedManualStage()
  if (!stage) return
  if (autoDecisionMode.value === 'none') {
    stage.autoDecision = undefined
    return
  }
  const hasCond = draftAutoDecisionCondition.value.conditions.length > 0
  stage.autoDecision = {
    mode: autoDecisionMode.value,
    // 空条件不落 conditionJson（引擎侧空条件不触发裁决，防漏配变无条件裁决）
    conditionJson: hasCond ? JSON.stringify(draftAutoDecisionCondition.value) : undefined,
  }
}

// 超时升级链（M8-C）：新增级别，倍数默认取「已有最大倍数 + 1」，动作默认提醒
function addTimeoutLevel() {
  const maxMultiplier = draftTimeoutAction.value.levels.reduce((max, l) => Math.max(max, l.multiplier), 0)
  draftTimeoutAction.value.levels.push({ multiplier: Math.min(maxMultiplier + 1, 10), action: 'remind' })
}

function removeTimeoutLevel(index: number) {
  draftTimeoutAction.value.levels.splice(index, 1)
}

// ==================== 选中节点 ====================

const selectedStage = computed(() => props.selectedIndex >= 0 ? props.stages[props.selectedIndex] ?? null : null)
const draftCondition = ref<ConditionGroup>({ logic: 'and', conditions: [] })
// 自动裁决（审批类型）编辑态
const autoDecisionMode = ref<'none' | 'autoApprove' | 'autoReject'>('none')
const draftAutoDecisionCondition = ref<ConditionGroup>({ logic: 'and', conditions: [] })
// 抄送/通知编辑态（M8-B，仅人工节点；见「高级」Tab）
const draftCcConfig = ref<CcNotifyConfig>(emptyCcConfig())
// 超时升级链编辑态（M8-C，仅人工节点且 timeoutHours>0；见「高级」Tab）
const draftTimeoutAction = ref<TimeoutActionConfig>(emptyTimeoutActionConfig())
const activeConfigTab = ref<'basic' | 'assignee' | 'fieldPerm' | 'actions' | 'advanced'>('basic')

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
const editFallbackUserIds = ref<number[]>([])
const editOrgChainMaxLevels = ref<number>(20)
const editSuperiorMaxLevels = ref<number>(5)
// 防止回显期间被 strategy watch 误清
let suppressStrategyReset = false

// ===== 抄送/通知配置状态（M8-B，独立远端搜索实例，不与处理人策略共享 userOptions） =====
const {
  userOptions: ccUserOptions, loading: ccUserSearchLoading,
  load: loadCcUsers, search: onCcUserSearch, pin: pinCcUser,
} = useUserSearch({ pageSize: 50 })

/** 抄送对象 id 数组 ↔ CcUser[] 双向代理（a-select mode="multiple" 只认原始值数组） */
const ccUserIds = computed<number[]>({
  get: () => draftCcConfig.value.users.map(u => u.userId),
  set: (ids) => {
    // 反查不到时回退已存配置里的姓名，避免二次序列化把 userName 抹空（镜像 buildAssigneeConfig 的 fixed 分支）
    const previousByUserId = new Map(draftCcConfig.value.users.map(u => [u.userId, u]))
    draftCcConfig.value.users = ids.map(id => {
      const opt = ccUserOptions.value.find(o => o.value === id)
      const prev = previousByUserId.get(id)
      return { userId: id, userName: opt?.name || prev?.userName || '' }
    })
  },
})

// ===== 插件注册 & 插件规则加载状态 =====
const pluginRegistryAll = ref<AutoPluginRegistryDto[]>([])
const pluginRegistryLoading = ref(false)
const pluginRulesByCode = ref<Record<string, AutoPluginRuleDto[]>>({})
const pluginRulesLoading = ref(false)

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
    console.warn('[StageConfigPanel] 加载用户列表失败:', e)
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
    console.warn('[StageConfigPanel] 加载角色列表失败:', e)
  }
})

// 预拉初始抄送人候选（无关键词），与角色列表同批加载；search 时 useUserSearch 再按关键词换页
onMounted(() => {
  void loadCcUsers('')
})

async function loadPluginRegistry() {
  if (pluginRegistryAll.value.length || pluginRegistryLoading.value) return
  pluginRegistryLoading.value = true
  try {
    const res: any = await getPluginRegistry()
    pluginRegistryAll.value = (res?.items || res?.data || (Array.isArray(res) ? res : [])) as AutoPluginRegistryDto[]
  } catch (e) {
    console.warn('[StageConfigPanel] 加载插件注册列表失败:', e)
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
    console.warn('[StageConfigPanel] 加载插件规则列表失败:', e)
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

/** 凭证插件：预留试算按钮位（M5-3 解禁，桥接契约见总体 plan §六.五） */
const isVoucherPlugin = computed(() => /voucher/i.test(currentPluginCode.value || ''))

/** 当前插件的规则选项 */
const pluginRuleOptions = computed(() => {
  const code = currentPluginCode.value
  if (!code) return []
  return (pluginRulesByCode.value[code] || []).map(r => ({ value: r.id, label: r.ruleName }))
})

function onPluginChange(newId: number | undefined) {
  if (props.selectedIndex < 0) return
  const stage = props.stages[props.selectedIndex]
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

// 切换节点类型（manual↔auto）并按新类型补齐默认配置，避免切换后缺配置导致发布失败。
// 不清理另一类型的既有字段（自动/人工各自忽略无关字段），防误切丢失配置。
function onTypeChange(newType: StageNodeType) {
  const stage = selectedStage.value
  if (!stage || stage.type === newType) return
  stage.type = newType
  if (newType === 'manual') {
    stage.approvalMode ||= 'single'
    stage.assigneeStrategy ||= 'role'
    ensureStageConfigDefaults(stage)
  } else {
    stage.processingGranularity ||= 'card'
    stage.failurePolicy ||= 'halt'
  }
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
    // 仅 required/editable 保留必填；readonly/hidden/masked 与"必填"矛盾一律清（终审 bug#3）
    required: access === 'required' ? true
      : access === 'editable' ? existing.required
      : false,
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
  return strategy === 'role' || strategy === 'fixed' || strategy === 'fieldUsers' || strategy === 'orgChain' || strategy === 'superiorChain'
}

function fallbackFromConfig(config: any): AssigneeFallbackType {
  const type = config?.fallback?.type
  if (type === 'flowAdmin') return 'flowAdmin'
  if (type === 'fixedUsers') return 'fixedUsers'
  return 'failSubmit'
}

function buildAssigneeConfig(stage: StageDefinition) {
  // 兜底转指定人：users 即兜底人清单（引擎 ApplyFallbackAsync 读 fallback.users）
  const fallback: Record<string, unknown> = { type: editFallbackType.value }
  if (editFallbackType.value === 'fixedUsers') {
    fallback.users = editFallbackUserIds.value.map(id => {
      const opt = userOptions.value.find(o => o.value === id)
      return { userId: id, userName: opt?.userName || '' }
    })
  }
  if (stage.assigneeStrategy === 'role') {
    return { roleCode: editRoleCode.value || '', users: [], fallback }
  }
  if (stage.assigneeStrategy === 'fixed') {
    // userOptions 会被搜索整体替换，反查不到时回退已存配置里的姓名，避免二次序列化把 userName 抹空
    const previous = parseAssigneeConfig(stage)
    const previousUsers = new Map<number, any>((previous?.users || []).map((u: any) => [u.userId, u]))
    const users = editUserIds.value.map(id => {
      const opt = userOptions.value.find(o => o.value === id)
      const prev = previousUsers.get(id)
      return {
        userId: id,
        userName: opt?.userName || prev?.userName || '',
        orgName: opt?.orgName ?? prev?.orgName ?? null,
      }
    })
    return { users, roleCode: null, fallback }
  }
  if (stage.assigneeStrategy === 'fieldUsers') {
    return { fieldKey: editFieldUserKey.value || '', fallback }
  }
  if (stage.assigneeStrategy === 'orgChain') {
    const config: Record<string, unknown> = { maxLevels: editOrgChainMaxLevels.value || 20, fallback }
    return config
  }
  if (stage.assigneeStrategy === 'superiorChain') {
    return { maxLevels: editSuperiorMaxLevels.value || 5, fallback }
  }
  return null
}

function syncAssigneeConfig() {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (!stage || stage.type !== 'manual') return
  const config = buildAssigneeConfig(stage)
  stage.assigneeConfigJson = config ? JSON.stringify(config) : undefined
}

// 选中节点变化（切换选择 / 撤销重做整体替换致对象换新）时回显编辑态；
// 就地编辑不改变 selectedStage 引用故不触发，等价于旧 selectStage 的调用时机。
function rehydrateSelection(src: StageDefinition | null | undefined) {
  if (!src) return
  activeConfigTab.value = 'basic'
  suppressStrategyReset = true
  // 回显处理人配置（仅人工节点）
  editRoleCode.value = ''
  editUserIds.value = []
  editFieldUserKey.value = ''
  editFallbackType.value = 'failSubmit'
  editFallbackUserIds.value = []
  editOrgChainMaxLevels.value = 20
  editSuperiorMaxLevels.value = 5
  ensureStageConfigDefaults(src)
  // 存量策略变体（fixedusers/specified 等）归一到 UI 规范值，避免下拉显示裸值
  if (src.type === 'manual' && src.assigneeStrategy) {
    const normalized = normalizeAssigneeStrategy(src.assigneeStrategy)
    if (normalized !== src.assigneeStrategy) src.assigneeStrategy = normalized
  }
  if (src.assigneeConfigJson) {
    try {
      const config = JSON.parse(src.assigneeConfigJson)
      editRoleCode.value = config?.roleCode || ''
      editUserIds.value = (config?.users || []).map((u: any) => u.userId)
      editFieldUserKey.value = config?.fieldKey || ''
      editFallbackType.value = fallbackFromConfig(config)
      editFallbackUserIds.value = (config?.fallback?.users || []).map((u: any) => u.userId)
      editOrgChainMaxLevels.value = config?.maxLevels || 20
      editSuperiorMaxLevels.value = config?.maxLevels || 5
      const knownUsers = [...(config?.users || []), ...(config?.fallback?.users || [])]
      if (knownUsers.length) {
        userOptions.value = knownUsers.map((u: any) => ({
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
  // 回显自动裁决（审批类型）
  autoDecisionMode.value = src.autoDecision?.mode ?? 'none'
  try {
    draftAutoDecisionCondition.value = src.autoDecision?.conditionJson
      ? JSON.parse(src.autoDecision.conditionJson)
      : { logic: 'and', conditions: [] }
  } catch {
    draftAutoDecisionCondition.value = { logic: 'and', conditions: [] }
  }
  // 回显抄送/通知配置（仅人工节点消费，auto 节点解析出空配置也不回写——watch 已按 type 守卫）
  draftCcConfig.value = parseCcConfig(src.ccConfigJson)
  draftCcConfig.value.users.forEach(u => pinCcUser({
    label: u.userName || `#${u.userId}`,
    value: u.userId,
    name: u.userName || `#${u.userId}`,
  }))
  // 回显超时升级链（仅人工节点消费，同上守卫）
  draftTimeoutAction.value = parseTimeoutAction(src.timeoutActionJson)
  // 预加载当前插件的规则列表
  if (src.type === 'auto' && src.pluginRegistryId) {
    const code = pluginRegistryAll.value.find(p => p.id === src.pluginRegistryId)?.pluginCode
    if (code) loadPluginRules(code)
  }
  // 释放抑制
  nextTick(() => { suppressStrategyReset = false })
}

watch(selectedStage, (src) => rehydrateSelection(src), { immediate: true })

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
watch(editRoleCode, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (stage?.assigneeStrategy === 'role') {
    syncAssigneeConfig()
  }
})

// 处理人配置序列化：人员变化时
watch(editUserIds, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (stage?.assigneeStrategy === 'fixed') {
    syncAssigneeConfig()
  }
}, { deep: true })

watch(editFieldUserKey, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (stage?.assigneeStrategy === 'fieldUsers') {
    syncAssigneeConfig()
  }
})

watch(editFallbackType, () => {
  syncAssigneeConfig()
})

watch(editFallbackUserIds, () => {
  if (suppressStrategyReset) return
  if (editFallbackType.value === 'fixedUsers') syncAssigneeConfig()
}, { deep: true })

watch(editOrgChainMaxLevels, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (stage?.assigneeStrategy === 'orgChain') syncAssigneeConfig()
})

watch(editSuperiorMaxLevels, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  const stage = props.stages[props.selectedIndex]
  if (stage?.assigneeStrategy === 'superiorChain') syncAssigneeConfig()
})

// 条件变化时写回
watch(draftCondition, (val) => {
  if (props.selectedIndex < 0) return
  const stage = props.stages[props.selectedIndex]
  if (!stage) return
  const hasCond = val && val.conditions && val.conditions.length > 0
  stage.conditionJson = hasCond ? JSON.stringify(val) : undefined
}, { deep: true })

// 自动裁决条件变化时写回（回显期间抑制）
watch(draftAutoDecisionCondition, () => {
  if (props.selectedIndex < 0 || suppressStrategyReset) return
  if (autoDecisionMode.value !== 'none') syncAutoDecision()
}, { deep: true })

// 抄送/通知配置变化时写回（仅人工节点消费；镜像 syncAssigneeConfig 的 type 守卫）
watch(draftCcConfig, (val) => {
  if (props.selectedIndex < 0) return
  const stage = props.stages[props.selectedIndex]
  if (!stage || stage.type !== 'manual') return
  stage.ccConfigJson = serializeCcConfig(val)
}, { deep: true })

// 超时升级链变化时写回（仅人工节点消费；同上守卫）
watch(draftTimeoutAction, (val) => {
  if (props.selectedIndex < 0) return
  const stage = props.stages[props.selectedIndex]
  if (!stage || stage.type !== 'manual') return
  stage.timeoutActionJson = serializeTimeoutAction(val)
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

// ==================== 字段映射给 ConditionBuilder ====================

const conditionFields = computed<FieldOption[]>(() =>
  (props.schemaFields || []).map(f => ({ key: f.key, label: f.label, type: f.type }))
)

// 节点健康（复用 stageDefinitionShared，明细提醒依赖 detailSchemaFields）
function getStageHealth(stage: StageDefinition) {
  return computeStageHealth(stage, props.detailSchemaFields)
}

/** 每个 Tab 的诊断计数（用于红点badge，mock A6 dtabs .dotbadge） */
const tabIssueCounts = computed(() => {
  if (!selectedStage.value) return { basic: 0, assignee: 0, fieldPerm: 0, actions: 0, advanced: 0 }
  const h = getStageHealth(selectedStage.value)
  const all = [...h.issues, ...h.warnings]
  const counts = { basic: 0, assignee: 0, fieldPerm: 0, actions: 0, advanced: 0 }
  for (const msg of all) {
    if (msg.includes('名称') || msg.includes('审批模式') || msg.includes('类型')) counts.basic++
    else if (msg.includes('处理人') || msg.includes('角色') || msg.includes('兜底')) counts.assignee++
    else if (msg.includes('字段权限') || msg.includes('明细')) counts.fieldPerm++
    else if (msg.includes('动作')) counts.actions++
    else counts.advanced++
  }
  return counts
})
</script>

<template>
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
        <a-tab-pane key="basic">
          <template #tab>
            <span class="sde-tab-label">基础<span v-if="tabIssueCounts.basic" class="sde-tab-dot" /></span>
          </template>
          <div class="sde-tab-panel">
            <div class="sde-fld">
              <label class="sde-fld__label">节点类型</label>
              <a-radio-group
                :value="selectedStage.type"
                button-style="solid"
                size="small"
                @change="(e: any) => onTypeChange(e.target.value)"
              >
                <a-radio-button value="manual"><UserOutlined /> 人工节点</a-radio-button>
                <a-radio-button value="auto"><RobotOutlined /> 自动节点</a-radio-button>
              </a-radio-group>
            </div>

            <div class="sde-fld">
              <label class="sde-fld__label">节点名称 <span class="sde-fld__req">*</span></label>
              <a-input v-model:value="selectedStage.name" placeholder="例：部门主管审批" />
            </div>

            <div class="sde-fld">
              <label class="sde-fld__label">节点说明</label>
              <a-textarea
                v-model:value="selectedStage.note"
                placeholder="可填写审批人看到的操作提示，如「请核对发票与明细金额一致后再同意」"
                :rows="2"
                :maxlength="200"
                show-count
              />
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

            <div v-if="selectedStage.type === 'manual' && selectedStage.approvalMode === 'ratio'" class="sde-fld">
              <label class="sde-fld__label">通过比例</label>
              <a-input-number
                v-model:value="selectedStage.approvalThreshold"
                :min="1"
                :max="99"
                addon-after="%"
                placeholder="留空默认 100%"
                style="width: 140px"
              />
              <p class="sde-fld__hint">已通过人数占比达到该阈值即完成节点；留空或超出 1-99 范围按 100%（等同会签）处理</p>
            </div>

            <div v-if="selectedStage.type === 'manual'" class="sde-fld">
              <label class="sde-fld__label">审批类型</label>
              <div class="cfd-opts">
                <div
                  v-for="opt in AUTO_DECISION_MODES"
                  :key="opt.value"
                  class="cfd-opts__item"
                  :class="{ 'is-active': autoDecisionMode === opt.value }"
                  role="radio"
                  :aria-checked="autoDecisionMode === opt.value"
                  tabindex="0"
                  @click="setAutoDecisionMode(opt.value)"
                  @keydown.enter.prevent="setAutoDecisionMode(opt.value)"
                >
                  <div>
                    <div class="cfd-opts__title">{{ opt.label }}</div>
                    <div class="cfd-opts__desc">{{ opt.desc }}</div>
                  </div>
                </div>
              </div>
              <div v-if="autoDecisionMode !== 'none'" class="sde-autodecision">
                <div class="sde-fld__label-row">
                  <label class="sde-fld__label">{{ autoDecisionMode === 'autoApprove' ? '自动通过条件' : '自动拒绝条件' }} <span class="sde-fld__req">*</span></label>
                  <span class="sde-fld__hint">不满足条件时仍走人工审批</span>
                </div>
                <ConditionBuilder v-model="draftAutoDecisionCondition" :fields="conditionFields" />
              </div>
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

        <a-tab-pane key="assignee" :disabled="selectedStage.type !== 'manual'">
          <template #tab>
            <span class="sde-tab-label">处理人<span v-if="tabIssueCounts.assignee" class="sde-tab-dot" /></span>
          </template>
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

            <div v-if="selectedStage.assigneeStrategy === 'orgChain'" class="sde-fld">
              <label class="sde-fld__label">最多逐级层数</label>
              <a-input-number v-model:value="editOrgChainMaxLevels" :min="1" :max="20" style="width: 120px" />
              <p class="sde-fld__hint">从发起组织沿组织树向上逐级取负责人，直到根或达到层数上限</p>
            </div>

            <div v-if="selectedStage.assigneeStrategy === 'superiorChain'" class="sde-fld">
              <label class="sde-fld__label">逐级主管层数</label>
              <a-input-number v-model:value="editSuperiorMaxLevels" :min="1" :max="20" style="width: 120px" />
              <p class="sde-fld__hint">从发起人沿直属上级逐级向上取指定层数（在职上级）</p>
            </div>

            <div v-if="isFallbackConfigStrategy(selectedStage.assigneeStrategy)" class="sde-fld">
              <label class="sde-fld__label">处理人兜底 <span class="sde-fld__req">*</span></label>
              <a-radio-group v-model:value="editFallbackType" button-style="solid" size="small">
                <a-radio-button v-for="option in FALLBACK_OPTIONS" :key="option.value" :value="option.value">
                  {{ option.label }}
                </a-radio-button>
              </a-radio-group>
              <p class="sde-fld__hint">
                {{ FALLBACK_OPTIONS.find(option => option.value === editFallbackType)?.hint }}
              </p>
              <a-select
                v-if="editFallbackType === 'fixedUsers'"
                v-model:value="editFallbackUserIds"
                mode="multiple"
                style="width: 100%; margin-top: 6px"
                placeholder="搜索并选择兜底处理人"
                :options="userOptions"
                :loading="userSearchLoading"
                show-search
                @search="onUserSearch"
                option-filter-prop="label"
                :filter-option="filterOption"
              />
            </div>

            <div class="sde-fld">
              <div class="sde-fld__label-row">
                <label class="sde-fld__label">跳过重复审批人</label>
                <a-switch v-model:checked="selectedStage.skipDuplicateApprover" size="small" />
              </div>
              <p class="sde-fld__hint">若某人在本卡更早的节点已审批过，本节点自动跳过该人（全部跳过则本节点自动通过）</p>
            </div>
          </div>
        </a-tab-pane>

        <a-tab-pane key="fieldPerm" :disabled="selectedStage.type !== 'manual'">
          <template #tab>
            <span class="sde-tab-label">字段权限<span v-if="tabIssueCounts.fieldPerm" class="sde-tab-dot" /></span>
          </template>
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
                <span class="sde-fld__batch">
                  批量：
                  <a-button size="small" type="link" @click="batchSetFieldAccess('readonly')">全部只读</a-button>
                  <a-button size="small" type="link" @click="batchSetFieldAccess('editable')">全部可编辑</a-button>
                </span>
              </div>
              <div class="cfd-list">
                <div
                  v-for="field in (schemaFields || [])"
                  :key="field.key"
                  class="cfd-list__row"
                  :class="{ 'is-sensitive': field.sensitive }"
                >
                  <span class="cfd-list__label" :title="field.label">
                    <span v-if="field.sensitive" class="cfd-list__lock" title="敏感字段">🔒</span>
                    <span v-if="routeReferencedFieldKeys?.has(field.key)" class="cfd-list__lock" title="路由条件引用">🔗</span>
                    {{ field.label }}
                  </span>
                  <PermissionTri
                    :value="triValueOf(getFieldAccess(field.key))"
                    :locked-states="fieldLockedStates(field)"
                    :lock-reason="fieldLockReason(field)"
                    @update:value="(v: PermissionValue) => setFieldAccess(field.key, v)"
                  />
                  <a-checkbox
                    class="sde-access__req"
                    :checked="isFieldRequired(field.key)"
                    :disabled="getFieldAccess(field.key) === 'hidden' || getFieldAccess(field.key) === 'masked'"
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
                <span class="sde-fld__hint">列级权限：同一套明细数据按节点职责分层展示</span>
              </div>
              <div v-if="(detailSchemaFields || []).length" class="cfd-list">
                <div v-for="field in (detailSchemaFields || [])" :key="field.key" class="cfd-list__row">
                  <span class="cfd-list__label" :title="field.label">└ {{ field.label }}</span>
                  <PermissionTri
                    :value="triValueOf(getDetailAccess(field.key))"
                    @update:value="(v: PermissionValue) => setDetailAccess(field.key, v)"
                  />
                  <a-checkbox
                    class="sde-access__req"
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
                :options="(schemaFields || []).map(f => ({
                  value: f.key,
                  label: f.sensitive ? `${f.label}（敏感字段不可作摘要）` : f.label,
                  disabled: !!f.sensitive,
                }))"
              />
              <p class="sde-fld__hint">摘要出现在待办列表与通知，敏感字段在此禁选以防绕过节点权限泄漏</p>
            </div>

            <div class="sde-fld sde-fld--block">
              <StageComponentViewEditor
                :components="cardComponents || []"
                v-model="selectedStage.viewProfile!.componentAccess"
              />
            </div>
          </div>
        </a-tab-pane>

        <a-tab-pane key="actions">
          <template #tab>
            <span class="sde-tab-label">{{ selectedStage.type === 'manual' ? '动作' : '执行配置' }}<span v-if="tabIssueCounts.actions" class="sde-tab-dot" /></span>
          </template>
          <div class="sde-tab-panel">
            <template v-if="selectedStage.type === 'manual'">
              <div class="sde-fld">
                <label class="sde-fld__label">动作配置</label>
                <p class="sde-fld__hint">逐动作启用/禁用，并配置是否必须填写处理意见。</p>
                <div class="cfd-list">
                  <div class="cfd-list__row is-head">
                    <span class="cfd-list__label">动作</span>
                    <span class="sde-action__col">启用</span>
                    <span class="sde-action__col">意见必填</span>
                  </div>
                  <div v-for="act in ACTION_OPTIONS" :key="act.value" class="cfd-list__row">
                    <span class="cfd-list__label">{{ act.label }}</span>
                    <span class="sde-action__col">
                      <a-switch
                        size="small"
                        :checked="selectedStage.actionPolicy!.allowedActions?.includes(act.value)"
                        @change="(v: string | number | boolean) => toggleAction(act.value, !!v)"
                      />
                    </span>
                    <span class="sde-action__col">
                      <a-segmented
                        v-if="selectedStage.actionPolicy!.allowedActions?.includes(act.value) && OPINION_REQUIRED_OPTIONS.some(o => o.value === act.value)"
                        size="small"
                        :value="opinionRequiredActions.includes(act.value) ? 'required' : 'optional'"
                        :options="[{ value: 'optional', label: '选填' }, { value: 'required', label: '必填' }]"
                        @change="(v: string | number | boolean) => toggleOpinionRequired(act.value, v === 'required')"
                      />
                      <span v-else class="sde-action__na">—</span>
                    </span>
                  </div>
                </div>
              </div>
              <p v-if="selectedStage.actionPolicy!.allowedActions?.includes('returnToStage')" class="sde-fld__hint">
                「退回节点」的目标由审批人在运行时现场选择（本轮已完成的人工节点），无需在设计器指定。
              </p>

              <div class="sde-fld">
                <label class="sde-fld__label">自定义动作</label>
                <p class="sde-fld__hint">
                  审批面板动态渲染的额外按钮，引擎按处理器分派执行（自动通过当前节点 / 自动驳回 / 触发通知抄送）。
                </p>
                <div class="cfd-list">
                  <div
                    v-for="(item, idx) in (selectedStage.actionPolicy!.customActions || [])"
                    :key="idx"
                    class="cfd-list__row"
                    style="gap: 8px"
                  >
                    <a-input v-model:value="item.code" placeholder="编码，如 quickApprove" style="width: 130px; flex: none" />
                    <a-input v-model:value="item.label" placeholder="按钮文案" style="width: 110px; flex: none" />
                    <a-select
                      v-model:value="item.handler"
                      :options="CUSTOM_ACTION_HANDLER_OPTIONS.map(o => ({ value: o.value, label: o.label, disabled: o.disabled }))"
                      style="width: 190px; flex: none"
                    />
                    <span class="sde-action__col">
                      <a-switch size="small" v-model:checked="item.requireOpinion" />
                    </span>
                    <a-button type="text" danger size="small" @click="removeCustomAction(idx)">删除</a-button>
                  </div>
                  <div v-if="!selectedStage.actionPolicy!.customActions?.length" class="cfd-list__row">
                    <span class="cfd-list__label sde-action__na">尚未配置自定义动作</span>
                  </div>
                </div>
                <a-button type="dashed" size="small" style="margin-top: 8px" @click="addCustomAction">+ 新增自定义动作</a-button>
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

              <div v-if="isVoucherPlugin" class="sde-fld">
                <a-tooltip title="随干跑工作台上线（预演步骤将支持凭证分录试算）">
                  <a-button size="small" disabled>🧪 试算凭证分录</a-button>
                </a-tooltip>
              </div>
            </template>
          </div>
        </a-tab-pane>

        <a-tab-pane key="advanced">
          <template #tab>
            <span class="sde-tab-label">高级<span v-if="tabIssueCounts.advanced" class="sde-tab-dot" /></span>
          </template>
          <div class="sde-tab-panel">
            <div v-if="selectedStage.type === 'manual'" class="sde-fld">
              <label class="sde-fld__label">超时提醒（小时）</label>
              <a-input-number
                v-model:value="selectedStage.timeoutHours"
                :min="0"
                placeholder="0 表示不提醒"
                style="width: 140px"
              />
              <p class="sde-fld__hint">超过时长未处理时提醒处理人（0 或留空 = 不提醒）</p>
            </div>

            <div v-if="selectedStage.type === 'manual' && (selectedStage.timeoutHours ?? 0) > 0" class="sde-fld sde-fld--block">
              <div class="sde-fld__label-row">
                <label class="sde-fld__label">超时升级链</label>
                <span class="sde-fld__hint">按超时时长的倍数分级触发动作，未配置级别时仅提醒</span>
              </div>
              <div class="sde-timeout-levels">
                <div v-for="(level, idx) in draftTimeoutAction.levels" :key="idx" class="sde-timeout-levels__row">
                  <a-input-number
                    v-model:value="level.multiplier"
                    :min="1"
                    :max="10"
                    addon-before="×"
                    addon-after="超时"
                    style="width: 128px"
                  />
                  <a-select
                    v-model:value="level.action"
                    style="flex: 1"
                    :options="TIMEOUT_ACTION_OPTIONS.map(o => ({ value: o.value, label: o.label }))"
                  />
                  <a-button type="text" danger size="small" aria-label="删除级别" @click="removeTimeoutLevel(idx)">
                    ✕
                  </a-button>
                </div>
                <div v-if="!draftTimeoutAction.levels.length" class="sde-fld__hint">暂无级别，将始终仅提醒处理人</div>
              </div>
              <a-button type="dashed" size="small" block @click="addTimeoutLevel">+ 添加级别</a-button>
            </div>

            <div v-if="selectedStage.type === 'manual'" class="sde-fld sde-fld--block">
              <div class="sde-fld__label-row">
                <label class="sde-fld__label">抄送 / 通知</label>
                <span class="sde-fld__hint">按时机将处理进度推送给相关人，不参与审批流转</span>
              </div>
              <div class="sde-fld">
                <label class="sde-fld__label">抄送对象</label>
                <a-select
                  v-model:value="ccUserIds"
                  mode="multiple"
                  style="width: 100%"
                  placeholder="搜索并选择抄送人"
                  :options="ccUserOptions"
                  :loading="ccUserSearchLoading"
                  show-search
                  option-filter-prop="label"
                  :filter-option="filterOption"
                  @search="onCcUserSearch"
                />
                <p class="sde-fld__hint">留空则不触发抄送</p>
              </div>
              <template v-if="ccUserIds.length">
                <div class="sde-fld">
                  <label class="sde-fld__label">抄送时机</label>
                  <a-radio-group v-model:value="draftCcConfig.timing" button-style="solid" size="small">
                    <a-radio-button v-for="opt in CC_TIMING_OPTIONS" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </a-radio-button>
                  </a-radio-group>
                </div>
                <div class="sde-fld">
                  <label class="sde-fld__label">通知渠道</label>
                  <a-checkbox-group v-model:value="draftCcConfig.channels">
                    <a-checkbox v-for="opt in CC_CHANNEL_OPTIONS" :key="opt.value" :value="opt.value" :disabled="opt.disabled">
                      {{ opt.label }}
                    </a-checkbox>
                  </a-checkbox-group>
                  <p class="sde-fld__hint">企微 / Bot 渠道尚未接入，暂不可选</p>
                </div>
              </template>
            </div>

            <div class="sde-fld sde-fld--block">
              <div class="sde-fld__label-row">
                <label class="sde-fld__label">进入条件</label>
                <span class="sde-fld__hint">满足时此节点激活，否则跳过（可实现"小额自动通过"类流转）</span>
              </div>
              <ConditionBuilder v-model="draftCondition" :fields="conditionFields" />
            </div>
          </div>
        </a-tab-pane>
      </a-tabs>
    </div>
  </section>
</template>

<style scoped lang="scss">
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
    padding: 0 8px;
  }

  :deep(.ant-tabs-tab) {
    padding: 10px 12px;
    font-size: 13px;
    position: relative;
  }

  :deep(.ant-tabs-tab-active .ant-tabs-tab-btn) {
    font-weight: 600;
  }
}

.sde-tab-label {
  position: relative;
}

.sde-tab-dot {
  position: absolute;
  top: -2px;
  right: -8px;
  width: 6px;
  height: 6px;
  background: var(--color-danger);
  border-radius: 50%;
}

.sde-action__col {
  flex: none;
  width: 72px;
  text-align: center;
  font-size: 12px;
}

.sde-action__na {
  color: var(--text-3);
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

.sde-timeout-levels {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 8px;

  &__row {
    display: flex;
    align-items: center;
    gap: 8px;
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
  .sde__right {
    padding: 14px;
  }
}
</style>
