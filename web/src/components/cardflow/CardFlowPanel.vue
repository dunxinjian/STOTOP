<script setup lang="ts">
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import {
  ActionSheet as VanActionSheet,
  Field as VanField,
  Button as VanButton,
  Skeleton as VanSkeleton,
  showToast,
  showDialog,
} from 'vant'
import 'vant/es/action-sheet/style'
import 'vant/es/field/style'
import 'vant/es/button/style'
import 'vant/es/skeleton/style'
import 'vant/es/toast/style'
import 'vant/es/dialog/style'
import type {
  CardDetailDto,
  CardHeaderConfig,
  SchemaFieldDefinition,
} from '@/types/cardflow'
import {
  getCard,
  approveCard,
  rejectCard,
  submitCard,
  updateCard,
  getFlowVersionDetail,
  withdrawCard,
  urgeCard,
  resubmitCard,
  voidCard,
  deleteCard,
} from '@/api/cardflow'
import request from '@/api/request'
import SchemaRenderer from './SchemaRenderer.vue'
import CardTimeline from './CardTimeline.vue'
import CardDetailTable, { type DetailRow } from './CardDetailTable.vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'
import { useUserStore } from '@/stores/user'
import { defaultCardHeaderConfig, parseCardSchemaFields, parseCardSchemaHeader, parseDetailSchemaFields } from '@/utils/cardflowSchema'

// ==================== Props & Emits ====================

type PanelMode = 'approval' | 'readonly' | 'fill' | 'initiator'

interface Props {
  cardId?: string | number | null
  mode?: PanelMode
  visible?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  cardId: null,
  mode: 'approval',
  visible: false,
})

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'approved'): void
  (e: 'rejected'): void
  (e: 'submitted'): void
  (e: 'saved'): void
  (e: 'acknowledged'): void
  (e: 'conflict'): void
  (e: 'closed'): void
  (e: 'withdrawn'): void
  (e: 'urged'): void
  (e: 'resubmitted'): void
  (e: 'voided'): void
  (e: 'deleted'): void
  (e: 'edit-requested'): void
}>()

// ==================== 状态 ====================

const loading = ref(false)
const loadError = ref(false)
const cardDetail = ref<CardDetailDto | null>(null)
const submitting = ref(false)
const budgetPreview = ref<any | null>(null)
const budgetPreviewLoading = ref(false)

// 审批模式底栏：'default' | 'approve' | 'reject'
const actionMode = ref<'default' | 'approve' | 'reject'>('default')
const opinion = ref('')
const showActionSheet = ref(false)
const conflictError = ref(false)

// schema（来自流程版本）
const cardSchema = ref<SchemaFieldDefinition[]>([])
const detailSchema = ref<SchemaFieldDefinition[]>([])
const cardHeaderConfig = ref<CardHeaderConfig>(defaultCardHeaderConfig())

// 财务上下文（供 account/auxiliary/bankAccount 字段选择器使用）
const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()
const userStore = useUserStore()
const flowAccountSetId = ref<number | null>(null)
const contextAccountSetId = computed(() =>
  flowAccountSetId.value || accountSetStore.currentAccountSetId || null,
)
const contextOrgId = computed(() => orgContextStore.currentOrgId ?? null)

// 编辑态数据（fill 模式使用）
const editFormData = ref<Record<string, any>>({})
const editDetailRows = ref<DetailRow[]>([])
const formErrors = ref<Record<string, string>>({})

// 键盘补偿
const keyboardOffset = ref(0)
const opinionFieldRef = ref<any>(null)

// ==================== 计算属性 ====================

const statusTagType = computed(() => {
  const map: Record<string, string> = {
    Draft: 'default',
    Pending: 'warning',
    Approved: 'success',
    Rejected: 'danger',
    Voided: 'default',
  }
  return map[cardDetail.value?.status ?? ''] ?? 'default'
})

const statusLabel = computed(() => {
  const map: Record<string, string> = {
    Draft: '草稿',
    Pending: '审批中',
    Approved: '已通过',
    Rejected: '已退回',
    Voided: '已废除',
  }
  return map[cardDetail.value?.status ?? ''] ?? cardDetail.value?.status ?? ''
})

const viewFormData = computed<Record<string, any>>(() => {
  if (!cardDetail.value?.dataJson) return {}
  try {
    return JSON.parse(cardDetail.value.dataJson)
  } catch {
    return {}
  }
})

const viewDetailRows = computed<DetailRow[]>(() => {
  if (!cardDetail.value?.details) return []
  return cardDetail.value.details.map((row) => {
    let parsed: Record<string, any> = {}
    try {
      parsed = row.dataJson ? JSON.parse(row.dataJson) : {}
    } catch {
      parsed = {}
    }
    return { _id: String(row.id), ...parsed }
  })
})

const canConfirmReject = computed(() => opinion.value.trim().length > 0)

const isExpenseReimburseCard = computed(() => cardDetail.value?.flowName?.includes('费用报销') ?? false)

function isAttachmentField(field: SchemaFieldDefinition): boolean {
  return field.type === 'file' || /attach|attachment|附件/.test(`${field.key}${field.label}`)
}

const mainCardSchema = computed(() => cardSchema.value.filter((field) => !isAttachmentField(field)))
const attachmentSchema = computed(() => cardSchema.value.filter(isAttachmentField))
const stageWorkView = computed(() => cardDetail.value?.currentStageWorkView ?? null)

function applyStageFieldAccess(schema: SchemaFieldDefinition[]): SchemaFieldDefinition[] {
  const access = stageWorkView.value?.fieldAccess
  if (!access) return schema
  return schema
    .filter((field) => access[field.key]?.access !== 'hidden')
    .map((field) => {
      const rule = access[field.key]
      if (!rule) return field
      const writable = rule.access === 'editable' || rule.access === 'required'
      return {
        ...field,
        readonly: !writable || field.readonly,
        required: rule.required ?? field.required,
      }
    })
}

function applyStageDetailAccess(schema: SchemaFieldDefinition[]): SchemaFieldDefinition[] {
  const access = stageWorkView.value?.detailAccess
  if (!access) return schema
  return schema
    .filter((field) => {
      const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
      return rules.length === 0 || rules.some(([, rule]) => rule.access !== 'hidden')
    })
    .map((field) => {
      const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
      const editable = rules.some(([, rule]) => rule.access === 'editable' || rule.access === 'required')
      const required = rules.some(([, rule]) => rule.required || rule.access === 'required')
      return rules.length === 0
        ? field
        : { ...field, readonly: !editable || field.readonly, required: required || field.required }
    })
}

const visibleMainCardSchema = computed(() => applyStageFieldAccess(mainCardSchema.value))
const visibleAttachmentSchema = computed(() => applyStageFieldAccess(attachmentSchema.value))
const visibleDetailSchema = computed(() => applyStageDetailAccess(detailSchema.value))
const stageRuntimeComponents = computed(() => stageWorkView.value?.components ?? [])
const hasStageRuntimeComponents = computed(() => stageRuntimeComponents.value.length > 0)

function updateRuntimeDetailRows(rows: DetailRow[]) {
  editDetailRows.value = rows
}

const activeFormData = computed<Record<string, any>>(() =>
  props.mode === 'fill' ? editFormData.value : viewFormData.value,
)

function interpolateCardHeaderTemplate(template: string | null | undefined) {
  const source = template?.trim()
  if (!source) return ''
  const context: Record<string, any> = {
    flowName: cardDetail.value?.flowName,
    flowCode: cardDetail.value?.cardNumber,
    cardNumber: cardDetail.value?.cardNumber,
    initiatorName: cardDetail.value?.initiatorName,
    ...activeFormData.value,
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
    const value = fieldKey ? activeFormData.value[fieldKey] : null
    return value === null || value === undefined || value === '' ? fallback : String(value)
  }
  if (mode === 'template') return interpolateCardHeaderTemplate(template) || fallback
  if (mode === 'flowCode') return cardDetail.value?.cardNumber || fallback
  return cardDetail.value?.flowName || fallback
}

const cardHeaderTitle = computed(() =>
  resolveCardHeaderText(
    cardHeaderConfig.value.titleMode,
    cardHeaderConfig.value.titleText,
    cardHeaderConfig.value.titleFieldKey,
    cardHeaderConfig.value.titleTemplate,
    cardDetail.value?.flowName || '卡片',
  )
)

const cardHeaderSubtitle = computed(() =>
  resolveCardHeaderText(
    cardHeaderConfig.value.subtitleMode,
    cardHeaderConfig.value.subtitleText,
    cardHeaderConfig.value.subtitleFieldKey,
    cardHeaderConfig.value.subtitleTemplate,
    cardDetail.value?.cardNumber || '',
  )
)

const cardHeaderShowSubtitle = computed(() =>
  cardHeaderConfig.value.subtitleMode !== 'hidden'
  && cardHeaderConfig.value.showSubtitle !== false
  && Boolean(cardHeaderSubtitle.value)
)

const cardHeaderShowStatus = computed(() => cardHeaderConfig.value.showStatus === true)

const expenseAmount = computed(() => {
  const num = Number(activeFormData.value.amount)
  return Number.isFinite(num) ? num : 0
})

const expenseAmountText = computed(() =>
  `¥${expenseAmount.value.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`,
)

const expenseCategoryText = computed(() => activeFormData.value.category || '未选择类别')
const applicantText = computed(() => {
  const applicant = activeFormData.value.applicant
  return applicant?.name || applicant || cardDetail.value?.initiatorName || '-'
})
const departmentText = computed(() => {
  const department = activeFormData.value.department
  return department?.name || department || orgContextStore.currentOrgName || '-'
})
const budgetPreviewOver = computed(() =>
  Boolean(budgetPreview.value?.blocked || Number(budgetPreview.value?.gapAmount || 0) > 0),
)

// 更多操作选项
const moreActions = [
  { name: '转交', subname: '将审批转给他人', action: 'transfer' },
  { name: '加签', subname: '增加审批人', action: 'addSignAfter' },
  { name: '抄送', subname: '知会相关人员', action: 'cc' },
  { name: '催办', subname: '催促当前审批人' },
]
const stageAllowedActions = computed(() => stageWorkView.value?.actionPolicy?.allowedActions ?? null)

function canStageAction(action: string): boolean {
  const allowed = stageAllowedActions.value
  if (!allowed || allowed.length === 0) return true
  return allowed.some((item) => item.toLowerCase() === action.toLowerCase())
}

const visibleMoreActions = computed(() =>
  moreActions.filter((item) => !item.action || canStageAction(item.action)),
)

// ==================== 数据加载 ====================

function parseSchema(json: string | null | undefined): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

/** 从 flowSettingsJson 中提取账套 ID（兼容 accountSetId / FAccountSetId 多种字段名） */
function parseAccountSetId(json: string | null | undefined): number | null {
  if (!json) return null
  try {
    const obj = typeof json === 'string' ? JSON.parse(json) : json
    const v = obj?.accountSetId ?? obj?.FAccountSetId ?? obj?.accountSet?.id
    return v ? Number(v) : null
  } catch {
    return null
  }
}

async function previewBudgetControl(data: Record<string, any>): Promise<any> {
  return request.post('/finance/budget-control/preview', data)
}

function readFormString(data: Record<string, any>, keys: string[]): string | null {
  for (const key of keys) {
    const value = data[key]
    if (value === null || value === undefined || value === '') continue
    if (typeof value === 'object') {
      const nested = value.name ?? value.label ?? value.value ?? value.id
      if (nested !== null && nested !== undefined && nested !== '') return String(nested)
      continue
    }
    return String(value)
  }
  return null
}

function readFormNumber(data: Record<string, any>, keys: string[]): number | null {
  for (const key of keys) {
    const value = data[key]
    if (value === null || value === undefined || value === '') continue
    const raw = typeof value === 'object'
      ? (value.id ?? value.value ?? value.amount ?? value.name)
      : value
    const num = Number(raw)
    if (Number.isFinite(num)) return num
  }
  return null
}

function readBusinessPeriod(): string {
  const value = readFormString(editFormData.value, ['businessDate', 'expenseDate', 'applyDate', 'date', '业务日期', '申请日期'])
  const date = value ? new Date(value) : new Date()
  const safeDate = Number.isNaN(date.getTime()) ? new Date() : date
  return `${safeDate.getFullYear()}${String(safeDate.getMonth() + 1).padStart(2, '0')}`
}

function buildBudgetPreviewPayload(): Record<string, any> | null {
  syncDetailSourcedFields()
  const detailAmount = editDetailRows.value.reduce((sum, row) => {
    const amount = readFormNumber(row, ['amount', 'expenseAmount', 'totalAmount', '金额', '报销金额', '申请金额']) ?? 0
    return sum + amount
  }, 0)
  const amount = detailAmount > 0
    ? detailAmount
    : (readFormNumber(editFormData.value, ['amount', 'expenseAmount', 'totalAmount', '金额', '报销金额', '申请金额']) ?? 0)

  if (amount <= 0) return null

  const firstDetail = editDetailRows.value.find((row) =>
    readFormString(row, ['expenseType', 'feeType', '费用类型']) ||
    readFormString(row, ['accountCode', '科目编码']) ||
    readFormNumber(row, ['plItemId', 'PLItemId', 'plItemID', '损益项ID']),
  )

  return {
    accountSetId: readFormNumber(editFormData.value, ['accountSetId', 'accountSetID', '账套ID']) || contextAccountSetId.value || 0,
    orgId: readFormNumber(editFormData.value, ['orgId', 'departmentId', '部门ID', '组织ID']) || contextOrgId.value || 0,
    period: readBusinessPeriod(),
    sourceType: 'cardflow_card',
    sourceId: cardDetail.value?.id,
    expenseType: readFormString(editFormData.value, ['expenseType', 'feeType', '费用类型']) ||
      (firstDetail ? readFormString(firstDetail, ['expenseType', 'feeType', '费用类型']) : null),
    accountCode: readFormString(editFormData.value, ['accountCode', '科目编码']) ||
      (firstDetail ? readFormString(firstDetail, ['accountCode', '科目编码']) : null),
    plItemId: readFormNumber(editFormData.value, ['plItemId', 'PLItemId', 'plItemID', '损益项ID']) ||
      (firstDetail ? readFormNumber(firstDetail, ['plItemId', 'PLItemId', 'plItemID', '损益项ID']) : null),
    amount,
  }
}

async function previewBudgetControlBeforeSubmit(): Promise<boolean> {
  const payload = buildBudgetPreviewPayload()
  if (!payload) {
    budgetPreview.value = null
    return true
  }

  budgetPreviewLoading.value = true
  try {
    const result = await previewBudgetControl(payload)
    budgetPreview.value = result
    if (result?.blocked) {
      await showDialog({
        title: '超预算',
        message: `可用预算 ${formatMoney(result.availableAmount)}，本次占用 ${formatMoney(result.requestAmount)}，缺口 ${formatMoney(result.gapAmount)}。`,
      })
      return false
    }
    return true
  } catch (err: any) {
    showToast({ message: err?.message || '预算预览失败', type: 'fail' })
    return true
  } finally {
    budgetPreviewLoading.value = false
  }
}

function formatMoney(value: number | string | null | undefined): string {
  const num = Number(value || 0)
  return `¥${num.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

function isBlankAutoValue(value: any): boolean {
  if (value === null || value === undefined || value === '') return true
  if (Array.isArray(value)) return value.length === 0
  if (typeof value === 'object') return !value.id && !value.name && !value.cardNumber
  return false
}

function isAutoApplicantField(field: SchemaFieldDefinition): boolean {
  return field.type === 'user' &&
    field.dataSource === 'auto' &&
    (/applicant|initiator/i.test(field.key) || /申请人|发起人/.test(field.label))
}

function isAutoDepartmentField(field: SchemaFieldDefinition): boolean {
  return field.type === 'org' &&
    field.dataSource === 'auto' &&
    (/department|dept|org/i.test(field.key) || /部门|组织/.test(field.label))
}

function hasAutoUserDepartmentField(): boolean {
  return cardSchema.value.some((field) =>
    isAutoDepartmentField(field) && field.autoSource === 'currentUserDepartment',
  )
}

function currentUserDefault() {
  const info = userStore.userInfo
  const id = info?.id ?? cardDetail.value?.initiatorId
  const name = info?.realName || info?.username || cardDetail.value?.initiatorName
  if (!id && !name) return null
  return {
    id,
    name,
    avatar: info?.avatar,
  }
}

function isDepartmentOrganization(org: { orgType?: string | null }) {
  const type = org.orgType || ''
  return /部门|DEPT|department/i.test(type)
}

function buildOrgValue(org: { orgId?: number | null, orgName?: string | null }) {
  if (!org.orgId && !org.orgName) return null
  return {
    id: org.orgId,
    name: org.orgName,
  }
}

function currentUserDepartmentDefault() {
  const orgs = orgContextStore.organizations || []
  const primaryDepartment = orgs.find((org) => org.isPrimaryOrg === 1 && isDepartmentOrganization(org))
  const primaryOrg = orgs.find((org) => org.isPrimaryOrg === 1)
  const firstDepartment = orgs.find(isDepartmentOrganization)
  const firstOrg = orgs[0]
  return buildOrgValue(primaryDepartment || primaryOrg || firstDepartment || firstOrg || {})
}

function currentOrgDefault() {
  const id = orgContextStore.currentOrgId
  const name = orgContextStore.currentOrgName
  if (!id && !name) return null
  return {
    id,
    name,
  }
}

function currentDepartmentDefault(field: SchemaFieldDefinition) {
  if (field.autoSource === 'currentUserDepartment') {
    return currentUserDepartmentDefault() || currentOrgDefault()
  }
  return currentOrgDefault()
}

function applyAutoIdentityDefaults() {
  if (props.mode !== 'fill' || cardSchema.value.length === 0) return

  let changed = false
  const next = { ...editFormData.value }

  for (const field of cardSchema.value) {
    if (!isBlankAutoValue(next[field.key])) continue

    if (isAutoApplicantField(field)) {
      const value = currentUserDefault()
      if (value) {
        next[field.key] = value
        changed = true
      }
    }

    if (isAutoDepartmentField(field)) {
      const value = currentDepartmentDefault(field)
      if (value) {
        next[field.key] = value
        changed = true
      }
    }
  }

  if (changed) {
    editFormData.value = next
  }
}

async function ensureUserContextDefaults() {
  if (props.mode !== 'fill' || !userStore.token) {
    applyAutoIdentityDefaults()
    return
  }

  const tasks: Promise<unknown>[] = []
  if (!userStore.userInfo) {
    tasks.push(userStore.fetchUserInfo())
  }
  if (hasAutoUserDepartmentField() && orgContextStore.organizations.length === 0) {
    tasks.push(orgContextStore.fetchOrganizations())
  }

  if (tasks.length > 0) {
    try {
      await Promise.all(tasks)
    } catch {
      // 路由守卫通常已加载上下文；失败时保留当前组织兜底值。
    }
  }

  applyAutoIdentityDefaults()
}

async function loadCardDetail(id: number | string) {
  const numId = Number(id)
  loading.value = true
  loadError.value = false
  conflictError.value = false
  actionMode.value = 'default'
  opinion.value = ''
  formErrors.value = {}
  budgetPreview.value = null
  try {
    const card = (await getCard(numId)) as CardDetailDto
    cardDetail.value = card

    // 加载 schema
    if (card.flowDefinitionId && card.flowVersionId) {
      try {
        const ver = await getFlowVersionDetail(card.flowDefinitionId, card.flowVersionId)
        cardSchema.value = parseSchema(ver.cardSchemaJson)
        cardHeaderConfig.value = parseCardSchemaHeader(ver.cardSchemaJson)
        detailSchema.value = parseDetailSchemaFields(ver.detailSchemaJson)
        // 从流程设置中提取账套 ID（若存在）
        flowAccountSetId.value = parseAccountSetId(ver.flowSettingsJson)
      } catch {
        cardSchema.value = []
        cardHeaderConfig.value = defaultCardHeaderConfig()
        detailSchema.value = []
        flowAccountSetId.value = null
      }
    }

    // fill 模式时初始化编辑数据
    if (props.mode === 'fill') {
      editFormData.value = { ...viewFormData.value }
      editDetailRows.value = [...viewDetailRows.value]
      applyAutoIdentityDefaults()
      void ensureUserContextDefaults()
    }
  } catch (err: any) {
    loadError.value = true
    cardDetail.value = null
  } finally {
    loading.value = false
  }
}

watch(
  () => [props.cardId, props.visible] as const,
  ([newId, vis]) => {
    if (vis && newId != null) {
      loadCardDetail(newId)
    } else if (!vis) {
      cardDetail.value = null
    }
  },
  { immediate: true }
)

watch(
  () => [
    props.mode,
    cardSchema.value,
    userStore.userInfo,
    orgContextStore.currentOrgId,
    orgContextStore.currentOrgName,
  ],
  () => applyAutoIdentityDefaults(),
)

// ==================== 操作方法（通用） ====================

function close() {
  emit('update:visible', false)
  emit('closed')
}

function handleConflict() {
  conflictError.value = true
  emit('conflict')
}

// ==================== 审批模式 ====================

function startApprove() {
  actionMode.value = 'approve'
  opinion.value = ''
  focusOpinionInput()
}

function startReject() {
  actionMode.value = 'reject'
  opinion.value = ''
  focusOpinionInput()
}

function cancelAction() {
  actionMode.value = 'default'
  opinion.value = ''
}

async function confirmApprove() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await approveCard(cardDetail.value.id, {
      opinion: opinion.value.trim() || null,
      concurrencyStamp: cardDetail.value.concurrencyStamp,
    })
    showToast({ message: '审批通过', type: 'success' })
    emit('approved')
    close()
  } catch (err: any) {
    if (err?.response?.status === 409 || err?.status === 409) {
      handleConflict()
    } else {
      showToast({ message: err?.message || '操作失败', type: 'fail' })
    }
  } finally {
    submitting.value = false
  }
}

async function confirmReject() {
  if (!cardDetail.value || submitting.value) return
  if (!opinion.value.trim()) {
    showToast('请填写退回原因')
    return
  }
  submitting.value = true
  try {
    await rejectCard(cardDetail.value.id, {
      opinion: opinion.value.trim(),
      concurrencyStamp: cardDetail.value.concurrencyStamp,
    })
    showToast({ message: '已退回', type: 'success' })
    emit('rejected')
    close()
  } catch (err: any) {
    if (err?.response?.status === 409 || err?.status === 409) {
      handleConflict()
    } else {
      showToast({ message: err?.message || '操作失败', type: 'fail' })
    }
  } finally {
    submitting.value = false
  }
}

function handleMoreAction(action: { name: string }) {
  // 转交/加签/抄送/催办 — 需要选人界面，这里仅占位
  showToast(`${action.name}功能开发中`)
}

// ==================== 只读（抄送）模式 ====================

async function doAcknowledge() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    // 后端暂未提供"标记已读"接口，这里直接通知父组件做乐观更新
    // 后续接入时可调用 markCcRead(cardId)
    emit('acknowledged')
    showToast({ message: '已知悉', type: 'success' })
    close()
  } finally {
    submitting.value = false
  }
}

// ==================== 我发起的（initiator）模式 ====================

async function doWithdraw() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await withdrawCard(cardDetail.value.id)
    showToast({ message: '已撤回', type: 'success' })
    emit('withdrawn')
    close()
  } catch (err: any) {
    showToast({ message: err?.message || '撤回失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function doUrge() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await urgeCard(cardDetail.value.id)
    showToast({ message: '已催办', type: 'success' })
    emit('urged')
  } catch (err: any) {
    showToast({ message: err?.message || '催办失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function doResubmit() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await resubmitCard(cardDetail.value.id)
    showToast({ message: '已重新提交', type: 'success' })
    emit('resubmitted')
    close()
  } catch (err: any) {
    showToast({ message: err?.message || '提交失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function doVoid() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await voidCard(cardDetail.value.id, opinion.value.trim() || undefined)
    showToast({ message: '已废除', type: 'success' })
    emit('voided')
    close()
  } catch (err: any) {
    showToast({ message: err?.message || '废除失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function doDelete() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await deleteCard(cardDetail.value.id)
    showToast({ message: '已删除', type: 'success' })
    emit('deleted')
    close()
  } catch (err: any) {
    showToast({ message: err?.message || '删除失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

function doRequestEdit() {
  emit('edit-requested')
}

// ==================== 填写模式（发起/重新提交） ====================

function syncDetailSourcedFields() {
  for (const field of cardSchema.value) {
    if ((field as any).source === 'detailSum') {
      editFormData.value[field.key] = editDetailRows.value.reduce((sum, row) => {
        const val = Number(row[field.key])
        return sum + (Number.isFinite(val) ? val : 0)
      }, 0)
    }
  }
}

function validateFillForm(): boolean {
  const errs: Record<string, string> = {}
  if (detailSchema.value.length > 0 && editDetailRows.value.length === 0) {
    showToast({ message: '请至少添加一条费用明细', type: 'fail' })
    return false
  }
  for (const row of editDetailRows.value) {
    for (const f of detailSchema.value) {
      if (!f.required) continue
      const val = row[f.key]
      const empty =
        val === null ||
        val === undefined ||
        val === '' ||
        (Array.isArray(val) && val.length === 0)
      if (empty) {
        showToast({ message: `${f.label}为必填项`, type: 'fail' })
        return false
      }
    }
  }
  syncDetailSourcedFields()
  for (const f of cardSchema.value) {
    if (!f.required) continue
    const val = editFormData.value[f.key]
    const empty =
      val === null ||
      val === undefined ||
      val === '' ||
      (Array.isArray(val) && val.length === 0)
    if (empty) errs[f.key] = `${f.label}为必填项`
  }
  formErrors.value = errs
  if (Object.keys(errs).length > 0) {
    const first = Object.values(errs)[0]
    showToast({ message: first, type: 'fail' })
    return false
  }
  return true
}

function buildSavePayload() {
  syncDetailSourcedFields()
  const details = editDetailRows.value.map((row, index) => {
    const { _id, ...data } = row
    return {
      sortOrder: index + 1,
      dataJson: JSON.stringify(data),
    }
  })

  return {
    dataJson: JSON.stringify(editFormData.value),
    concurrencyStamp: cardDetail.value?.concurrencyStamp ?? null,
    details,
  }
}

async function doSaveDraft() {
  if (!cardDetail.value || submitting.value) return
  submitting.value = true
  try {
    await updateCard(cardDetail.value.id, buildSavePayload())
    showToast({ message: '已暂存', type: 'success' })
    emit('saved')
  } catch (err: any) {
    if (err?.response?.status === 409 || err?.status === 409) {
      handleConflict()
    } else {
      showToast({ message: err?.message || '暂存失败', type: 'fail' })
    }
  } finally {
    submitting.value = false
  }
}

async function doSubmit() {
  if (!cardDetail.value || submitting.value) return
  if (!validateFillForm()) return
  const budgetOk = await previewBudgetControlBeforeSubmit()
  if (!budgetOk) return
  submitting.value = true
  try {
    // 先保存最新内容
    await updateCard(cardDetail.value.id, buildSavePayload())
    // 再发起审批
    await submitCard(cardDetail.value.id)
    showToast({ message: '已提交审批', type: 'success' })
    emit('submitted')
    close()
  } catch (err: any) {
    if (err?.response?.status === 409 || err?.status === 409) {
      handleConflict()
    } else {
      showToast({ message: err?.message || '提交失败', type: 'fail' })
    }
  } finally {
    submitting.value = false
  }
}

function addDetailRow() {
  const newRow: DetailRow = { _id: `new_${Date.now()}` }
  for (const f of detailSchema.value) {
    newRow[f.key] = f.defaultValue ?? null
  }
  editDetailRows.value = [...editDetailRows.value, newRow]
}

// ==================== 键盘适配 ====================

function handleViewportResize() {
  const vv = window.visualViewport
  if (!vv) {
    keyboardOffset.value = 0
    return
  }
  const offset = window.innerHeight - vv.height - vv.offsetTop
  // 仅在键盘弹出时（offset > 80px 视为键盘弹起）补偿
  keyboardOffset.value = offset > 80 ? offset : 0
}

function focusOpinionInput() {
  nextTick(() => {
    const el = opinionFieldRef.value?.$el?.querySelector?.('textarea')
    if (el && typeof el.scrollIntoView === 'function') {
      setTimeout(() => {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' })
      }, 250)
    }
  })
}

onMounted(() => {
  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', handleViewportResize)
    window.visualViewport.addEventListener('scroll', handleViewportResize)
  }
})

onBeforeUnmount(() => {
  if (window.visualViewport) {
    window.visualViewport.removeEventListener('resize', handleViewportResize)
    window.visualViewport.removeEventListener('scroll', handleViewportResize)
  }
})

// 防止未使用 lint 警告
void showDialog
</script>

<template>
  <Transition name="cf-slide">
    <div v-if="visible" class="cf-panel">
      <!-- 头部 -->
      <div class="cf-panel__header">
        <span class="cf-panel__close" @click="close">×</span>
        <span class="cf-panel__number">{{ cardDetail?.cardNumber ?? '--' }}</span>
        <span
          class="cf-panel__status"
          :class="`cf-panel__status--${statusTagType}`"
        >
          {{ statusLabel }}
        </span>
      </div>

      <!-- 内容区 -->
      <div
        class="cf-panel__body"
        :style="{ paddingBottom: keyboardOffset > 0 ? `${keyboardOffset + 16}px` : undefined }"
      >
        <!-- 加载中 -->
        <template v-if="loading">
          <VanSkeleton :row="6" />
        </template>

        <!-- 加载失败 -->
        <template v-else-if="loadError">
          <div class="cf-panel__error">
            <p>加载失败</p>
            <VanButton size="small" type="primary" @click="cardId != null && loadCardDetail(cardId)">
              重试
            </VanButton>
          </div>
        </template>

        <!-- 409 冲突 -->
        <template v-else-if="conflictError">
          <div class="cf-panel__conflict">
            <p>该待办已被他人处理</p>
            <VanButton size="small" @click="close">关闭</VanButton>
          </div>
        </template>

        <!-- 正常内容 -->
        <template v-else-if="cardDetail">
          <div
            class="cf-panel__intro"
            :class="{ 'cf-panel__intro--expense': isExpenseReimburseCard }"
          >
            <div class="cf-panel__initiator">
              <span class="cf-panel__avatar">单</span>
              <span class="cf-panel__name">{{ cardDetail.initiatorName }}</span>
              <span class="cf-panel__sep">·</span>
              <span class="cf-panel__flow">{{ cardHeaderTitle }}</span>
              <span v-if="cardHeaderShowSubtitle" class="cf-panel__sep">·</span>
              <span v-if="cardHeaderShowSubtitle" class="cf-panel__subtitle">{{ cardHeaderSubtitle }}</span>
              <span
                v-if="cardHeaderShowStatus"
                class="cf-panel__inline-status"
                :class="`cf-panel__inline-status--${statusTagType}`"
              >
                {{ statusLabel }}
              </span>
              <span class="cf-panel__sep">·</span>
              <span class="cf-panel__time">{{ cardDetail.createdTime?.slice(0, 16) }}</span>
            </div>

            <div v-if="isExpenseReimburseCard" class="cf-panel__expense-summary">
              <div>
                <div class="cf-panel__summary-label">报销金额</div>
                <div class="cf-panel__amount-value">{{ expenseAmountText }}</div>
              </div>
              <div class="cf-panel__summary-side">
                <span>{{ expenseCategoryText }}</span>
                <span>{{ applicantText }} / {{ departmentText }}</span>
              </div>
            </div>
          </div>

          <!-- 表单：fill 模式可编辑，其他只读 -->
          <div class="cf-panel__form cf-panel__form-section">
            <div class="cf-panel__section-title">
              {{ isExpenseReimburseCard ? '报销信息' : '卡片信息' }}
            </div>
            <SchemaRenderer
              v-if="mode === 'fill'"
              v-model="editFormData"
              :schema="visibleMainCardSchema"
              :components="stageRuntimeComponents"
              :detail-rows="editDetailRows"
              mode="edit"
              platform="pc"
              :errors="formErrors"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
              @update:detail-rows="updateRuntimeDetailRows"
            />
            <SchemaRenderer
              v-else
              :schema="visibleMainCardSchema"
              :components="stageRuntimeComponents"
              :detail-rows="viewDetailRows"
              :model-value="viewFormData"
              mode="view"
              platform="pc"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
            />
          </div>

          <!-- 明细行 -->
          <div v-if="!hasStageRuntimeComponents && visibleDetailSchema.length > 0" class="cf-panel__details cf-panel__form-section">
            <div class="cf-panel__section-title">
              {{ isExpenseReimburseCard ? '报销明细' : '明细' }}
            </div>
            <CardDetailTable
              v-if="mode === 'fill'"
              v-model="editDetailRows"
              :schema="visibleDetailSchema"
              mode="edit"
              platform="pc"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
              compact
            />
            <CardDetailTable
              v-else
              :schema="visibleDetailSchema"
              :model-value="viewDetailRows"
              mode="view"
              platform="pc"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
              compact
            />
            <!-- CardDetailTable compact 模式已内置添加按鈕，无需外层重复 -->
          </div>

          <div
            v-if="mode === 'fill' && budgetPreview"
            class="cf-panel__budget-preview cf-panel__form-section"
            :class="{ 'cf-panel__budget-preview--over': budgetPreviewOver }"
          >
            <div class="cf-panel__budget-head">
              <span>预算</span>
              <strong v-if="budgetPreview.mappingMissing">映射缺失</strong>
              <strong v-else-if="budgetPreviewOver">超预算</strong>
              <strong v-else>可提交</strong>
            </div>
            <div class="cf-panel__budget-grid">
              <div>
                <span>可用预算</span>
                <b>{{ formatMoney(budgetPreview.availableAmount) }}</b>
              </div>
              <div>
                <span>本次占用</span>
                <b>{{ formatMoney(budgetPreview.requestAmount) }}</b>
              </div>
              <div>
                <span>缺口</span>
                <b>{{ formatMoney(budgetPreview.gapAmount) }}</b>
              </div>
            </div>
            <p v-if="budgetPreview.mappingMissing">
              {{ budgetPreview.missingReason || '预算映射缺失' }}
            </p>
          </div>

          <!-- 附件 -->
          <div v-if="!hasStageRuntimeComponents && visibleAttachmentSchema.length > 0" class="cf-panel__attachments cf-panel__form-section">
            <div class="cf-panel__section-title">
              {{ isExpenseReimburseCard ? '票据附件' : '附件' }}
            </div>
            <SchemaRenderer
              v-if="mode === 'fill'"
              v-model="editFormData"
              :schema="visibleAttachmentSchema"
              mode="edit"
              platform="pc"
              :errors="formErrors"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
            />
            <SchemaRenderer
              v-else
              :schema="visibleAttachmentSchema"
              :model-value="viewFormData"
              mode="view"
              platform="pc"
              :account-set-id="contextAccountSetId"
              :org-id="contextOrgId"
            />
          </div>

          <!-- 审批记录 -->
          <div class="cf-panel__timeline cf-panel__form-section">
            <CardTimeline
              :stages="cardDetail.stageInstances"
              mode="compact"
              :current-round="cardDetail.currentRound"
            />
          </div>
        </template>
      </div>

      <!-- 底部操作栏 -->
      <div
        v-if="cardDetail && !loading && !loadError && !conflictError"
        class="cf-panel__footer"
        :style="{ paddingBottom: keyboardOffset > 0 ? `${keyboardOffset}px` : undefined }"
      >
        <!-- 审批模式 -->
        <template v-if="mode === 'approval'">
          <!-- 意见输入展开区 -->
          <div
            class="cf-panel__opinion"
            :class="{ 'cf-panel__opinion--open': actionMode !== 'default' }"
          >
            <VanField
              ref="opinionFieldRef"
              v-model="opinion"
              type="textarea"
              rows="3"
              :placeholder="actionMode === 'reject' ? '请填写退回原因（必填）' : '选填审批意见'"
              maxlength="500"
              show-word-limit
            />
            <div class="cf-panel__opinion-actions">
              <VanButton size="small" @click="cancelAction">取消</VanButton>
              <VanButton
                v-if="actionMode === 'approve'"
                size="small"
                type="primary"
                :loading="submitting"
                :disabled="submitting"
                @click="confirmApprove"
              >
                确认通过
              </VanButton>
              <VanButton
                v-if="actionMode === 'reject'"
                size="small"
                type="danger"
                :loading="submitting"
                :disabled="submitting || !canConfirmReject"
                @click="confirmReject"
              >
                确认退回
              </VanButton>
            </div>
          </div>

          <!-- 默认按钮行 -->
          <div v-if="actionMode === 'default'" class="cf-panel__actions">
            <VanButton v-if="canStageAction('reject')" size="small" plain type="danger" @click="startReject">退回</VanButton>
            <VanButton v-if="canStageAction('approve')" size="small" type="primary" @click="startApprove">通过</VanButton>
            <span v-if="visibleMoreActions.length > 0" class="cf-panel__more" @click="showActionSheet = true">···</span>
          </div>
        </template>

        <!-- 只读（抄送）模式 -->
        <template v-else-if="mode === 'readonly'">
          <div class="cf-panel__actions cf-panel__actions--single">
            <VanButton
              type="primary"
              block
              :loading="submitting"
              :disabled="submitting"
              @click="doAcknowledge"
            >
              知悉
            </VanButton>
          </div>
        </template>

        <!-- 我发起的模式：按状态显示操作 -->
        <template v-else-if="mode === 'initiator'">
          <div
            v-if="cardDetail.status === 'active' || cardDetail.status === 'Pending'"
            class="cf-panel__actions cf-panel__actions--fill"
          >
            <VanButton size="small" plain :loading="submitting" :disabled="submitting" @click="doWithdraw">撤回</VanButton>
            <VanButton size="small" type="primary" :loading="submitting" :disabled="submitting" @click="doUrge">催办</VanButton>
          </div>
          <div
            v-else-if="cardDetail.status === 'returned' || cardDetail.status === 'Rejected'"
            class="cf-panel__actions cf-panel__actions--fill"
          >
            <VanButton size="small" plain type="danger" :loading="submitting" :disabled="submitting" @click="doVoid">废除</VanButton>
            <VanButton size="small" type="primary" :loading="submitting" :disabled="submitting" @click="doResubmit">重新提交</VanButton>
          </div>
          <div
            v-else-if="cardDetail.status === 'draft' || cardDetail.status === 'Draft'"
            class="cf-panel__actions cf-panel__actions--fill"
          >
            <VanButton size="small" plain type="danger" :loading="submitting" :disabled="submitting" @click="doDelete">删除</VanButton>
            <VanButton size="small" type="primary" @click="doRequestEdit">继续填写</VanButton>
          </div>
          <div v-else class="cf-panel__actions cf-panel__actions--single">
            <VanButton block @click="close">关闭</VanButton>
          </div>
        </template>

        <!-- 填写模式 -->
        <template v-else-if="mode === 'fill'">
          <div class="cf-panel__actions cf-panel__actions--fill">
            <VanButton
              size="small"
              plain
              :loading="submitting"
              :disabled="submitting"
              @click="doSaveDraft"
            >
              暂存草稿
            </VanButton>
            <VanButton
              size="small"
              type="primary"
              :loading="submitting || budgetPreviewLoading"
              :disabled="submitting || budgetPreviewLoading"
              @click="doSubmit"
            >
              提交审批
            </VanButton>
          </div>
        </template>
      </div>

      <!-- 更多操作 ActionSheet -->
      <VanActionSheet
        v-model:show="showActionSheet"
        :actions="visibleMoreActions"
        cancel-text="取消"
        @select="handleMoreAction"
      />
    </div>
  </Transition>
</template>

<style scoped lang="scss">
// ==================== 动画 ====================
.cf-slide-enter-active {
  animation: cf-slide-in-right 300ms ease-out;
}
.cf-slide-leave-active {
  animation: cf-slide-out-right 200ms ease-in;
}
@keyframes cf-slide-in-right {
  from { transform: translateX(100%); }
  to   { transform: translateX(0); }
}
@keyframes cf-slide-out-right {
  from { transform: translateX(0); }
  to   { transform: translateX(100%); }
}

// ==================== 面板容器 ====================
.cf-panel {
  position: fixed;
  right: 0;
  top: 0;
  height: 100vh;
  width: 400px;
  z-index: 1000;
  background: var(--bg-muted);
  // 右侧滑出抽屉：保留向左投射的边缘阴影方向，仅令牌化颜色
  box-shadow: -6px 0 16px color-mix(in srgb, var(--text-1) 8%, transparent);
  display: flex;
  flex-direction: column;

  // 响应式宽度：< 1440px 时全屏 Drawer
  @media (max-width: 1439px) {
    width: 100vw;
  }

  @media (max-width: 768px) {
    width: 100vw;
  }

  // ===== 头部 =====
  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 16px 20px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
    background: var(--bg-card);
  }

  &__close {
    font-size: 20px;
    cursor: pointer;
    color: var(--text-3);
    margin-right: 4px;
    line-height: 1;
    &:hover { color: var(--text-1); }
  }

  &__number {
    font-size: 15px;
    font-weight: 500;
    color: var(--text-1);
  }

  &__status {
    font-size: 12px;
    padding: 2px 8px;
    border-radius: 4px;
    margin-left: auto;

    &--default { background: var(--bg-muted); color: var(--text-3); }
    &--warning { background: var(--color-warning-light); color: var(--color-warning); }
    &--success { background: var(--color-success-light); color: var(--color-success); }
    &--danger  { background: var(--color-danger-light); color: var(--color-danger); }
  }

  // ===== 内容区 =====
  &__body {
    flex: 1;
    overflow-y: auto;
    padding: 16px 20px 20px;
    transition: padding-bottom 200ms ease;
  }

  &__error,
  &__conflict {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 60px 0;
    gap: 12px;
    color: var(--text-3);

    p { margin: 0; font-size: 14px; }
  }

  &__conflict {
    border: 1px solid var(--border);
    border-radius: 8px;
    margin: 24px 0;
    padding: 36px 24px;
    background: var(--bg-muted);
  }

  &__intro {
    margin-bottom: 12px;

    &--expense {
      padding: 14px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: linear-gradient(180deg, var(--bg-card) 0%, color-mix(in srgb, var(--color-success) 5%, var(--bg-card)) 100%);
    }
  }

  &__initiator {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    color: var(--text-2);
    flex-wrap: wrap;
  }

  &__avatar {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border-radius: 6px;
    background: color-mix(in srgb, var(--color-success) 60%, black);
    color: var(--text-on-accent);
    font-size: 12px;
    font-weight: 700;
  }
  &__name   { font-weight: 500; color: var(--text-1); }
  &__sep    { color: var(--text-3); }
  &__flow   { color: color-mix(in srgb, var(--color-success) 40%, black); font-weight: 600; }
  &__subtitle { color: var(--text-3); font-size: 12px; }
  &__time   { color: var(--text-3); font-size: 12px; }

  &__inline-status {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 6px;
    border-radius: 4px;
    background: var(--color-success-light);
    color: var(--color-success-text);
    font-size: 11px;
    line-height: 20px;
  }

  &__inline-status--warning { background: var(--color-warning-light); color: var(--color-warning-text); }
  &__inline-status--success { background: var(--color-success-light); color: var(--color-success-text); }
  &__inline-status--danger { background: var(--color-danger-light); color: var(--color-danger-text); }

  &__expense-summary {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    margin-top: 14px;
    padding-top: 12px;
    border-top: 1px dashed var(--border);
  }

  &__summary-label {
    font-size: 12px;
    color: var(--text-2);
    margin-bottom: 2px;
  }

  &__amount-value {
    color: var(--color-success-text);
    font-size: 24px;
    font-weight: 700;
    line-height: 1.2;
    font-variant-numeric: tabular-nums;
  }

  &__summary-side {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    justify-content: center;
    gap: 4px;
    color: var(--text-2);
    font-size: 12px;
    text-align: right;
    min-width: 0;

    span {
      max-width: 190px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  &__form-section {
    margin-bottom: 12px;
    padding: 14px;
    border: 1px solid var(--border);
    border-radius: 8px;
    background: var(--bg-card);
  }

  &__form {
    :deep(.ant-form-item:last-child) {
      margin-bottom: 0;
    }
  }

  &__section-title {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 14px;
    font-weight: 700;
    color: var(--text-1);
    margin-bottom: 10px;

    &::before {
      content: '';
      width: 3px;
      height: 14px;
      border-radius: 2px;
      background: var(--color-success);
    }
  }

  &__add-row {
    margin-top: 8px;
  }

  &__timeline {
    margin-bottom: 16px;
  }

  &__budget-preview {
    border-color: color-mix(in srgb, var(--color-success) 25%, var(--border));
    background: color-mix(in srgb, var(--color-success) 3%, var(--bg-card));

    &--over {
      border-color: color-mix(in srgb, var(--color-danger) 30%, var(--border));
      background: color-mix(in srgb, var(--color-danger) 5%, var(--bg-card));
    }

    p {
      margin: 8px 0 0;
      color: var(--color-warning-text);
      font-size: 12px;
      line-height: 1.5;
    }
  }

  &__budget-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    margin-bottom: 10px;
    color: var(--text-1);
    font-weight: 700;

    strong {
      font-size: 12px;
      color: var(--color-warning);
    }
  }

  &__budget-grid {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 8px;

    div {
      min-width: 0;
      padding: 8px;
      border-radius: 6px;
      background: var(--bg-card);
      border: 1px solid var(--border);
    }

    span {
      display: block;
      color: var(--text-2);
      font-size: 12px;
      margin-bottom: 4px;
    }

    b {
      display: block;
      color: var(--text-1);
      font-size: 13px;
      font-weight: 700;
      line-height: 1.2;
      overflow-wrap: anywhere;
    }
  }

  // ===== 底部操作栏 =====
  &__footer {
    flex-shrink: 0;
    border-top: 1px solid var(--border);
    background: var(--bg-card);
    position: sticky;
    bottom: 0;
    z-index: 2;
    transition: padding-bottom 200ms ease;
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 20px;

    &--single {
      padding: 12px 20px;

      :deep(.van-button) {
        flex: 1;
      }
    }

    &--fill {
      justify-content: space-between;

      :deep(.van-button) {
        flex: 1;
      }
    }
  }

  &__more {
    margin-left: auto;
    cursor: pointer;
    font-size: 18px;
    color: var(--text-3);
    letter-spacing: 2px;
    &:hover { color: var(--text-1); }
  }

  // ===== 意见输入区 =====
  &__opinion {
    max-height: 0;
    overflow: hidden;
    transition: max-height 200ms ease, padding 200ms ease;
    padding: 0 20px;

    &--open {
      max-height: 260px;
      padding: 12px 20px;
      border-bottom: 1px solid var(--border);
    }
  }

  &__opinion-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 8px;
  }
}
</style>

<!-- 全局样式：限制 PC 端 Ant Design 下拉/弹出类组件（如 a-date-picker 的日历面板）在卡片内不跨出 -->
<style lang="scss">
// 预留位置以后需要限制 popper 宽度时使用。
// 当前 Ant Design 的 popper 会 teleport 到 body，按触发元素位置的 popper 算法自动定位，
// 不会全屏占据，因此不需额外处理。
</style>
