<script setup lang="ts">
/**
 * StageConfigPanel —— 选中节点的属性面板（5-tab：基础/处理人/节点视图/动作时限/进入条件）
 *
 * 从 StageDefinitionEditor 右栏零逻辑抽出的共用面板，以便节点链右栏与画布节点抽屉共用同一套
 * 完整配置能力（消灭双入口能力差）。
 *
 * 契约（保持原右栏行为）：
 *  - 通过 `stages`（整表）+ `selectedIndex` 定位当前编辑节点，就地修改 stages[selectedIndex]；
 *    上层（StageDefinitionEditor）对 stages 的 deep watch 负责 emitUpdate，本面板不再另发事件。
 *  - 选中节点变化（切换选择 / 撤销重做整体替换致对象换新）经 watch(selectedStage) 回显编辑态，
 *    等价于原 selectStage 的调用时机；就地编辑不改变 selectedStage 引用故不触发回显。
 */
import { computed, onMounted, ref, watch, nextTick } from 'vue'
import {
  UserOutlined,
  RobotOutlined,
  ThunderboltOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons-vue'
import ConditionBuilder from './ConditionBuilder.vue'
import StageComponentViewEditor from './designer/StageComponentViewEditor.vue'
import type { ConditionGroup, FieldOption } from './ConditionBuilder.vue'
import type { StageDefinition, StageAccessMode, AssigneeFallbackType } from './StageDefinitionEditor.vue'
import type { CardComponentDefinition, SchemaFieldDefinition, AutoPluginRegistryDto, AutoPluginRuleDto } from '@/types/cardflow'
import { getRoleList, getUserList } from '@/api/system'
import { getPluginRegistry, getPluginRules } from '@/api/cardflow'
import { DEFAULT_ACTIONS, parseAssigneeConfig, getStageHealth as computeStageHealth } from './stageDefinitionShared'

const props = defineProps<{
  stages: StageDefinition[]
  selectedIndex: number
  schemaFields?: SchemaFieldDefinition[]
  detailSchemaFields?: SchemaFieldDefinition[]
  cardComponents?: CardComponentDefinition[]
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

// ==================== 选中节点 ====================

const selectedStage = computed(() => props.selectedIndex >= 0 ? props.stages[props.selectedIndex] ?? null : null)
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
    // 隐藏/脱敏必须清掉 required，否则产生"隐藏且必填"矛盾配置被发布校验拦下
    required: access === 'required' ? true
      : (access === 'hidden' || access === 'masked') ? false
      : existing.required,
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

function buildAssigneeConfig(stage: StageDefinition) {
  const fallback = { type: editFallbackType.value }
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

// 条件变化时写回
watch(draftCondition, (val) => {
  if (props.selectedIndex < 0) return
  const stage = props.stages[props.selectedIndex]
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

// ==================== 字段映射给 ConditionBuilder ====================

const conditionFields = computed<FieldOption[]>(() =>
  (props.schemaFields || []).map(f => ({ key: f.key, label: f.label, type: f.type }))
)

// 节点健康（复用 stageDefinitionShared，明细提醒依赖 detailSchemaFields）
function getStageHealth(stage: StageDefinition) {
  return computeStageHealth(stage, props.detailSchemaFields)
}
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
                <p v-if="selectedStage.actionPolicy!.allowedActions?.includes('returnToStage')" class="sde-fld__hint">
                  「退回节点」的目标由审批人在运行时现场选择（本轮已完成的人工节点），无需在设计器指定。
                </p>
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
  .sde__right {
    padding: 14px;
  }
}
</style>
