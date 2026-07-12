<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  PullRefresh as VanPullRefresh,
  Loading as VanLoading,
  ActionBar as VanActionBar,
  ActionBarButton as VanActionBarButton,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  Tag as VanTag,
  Empty as VanEmpty,
  Popup as VanPopup,
  Search as VanSearch,
  CheckboxGroup as VanCheckboxGroup,
  Checkbox as VanCheckbox,
  Button as VanButton,
} from 'vant'
import { showToast, showConfirmDialog } from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/pull-refresh/style'
import 'vant/es/loading/style'
import 'vant/es/action-bar/style'
import 'vant/es/action-bar-button/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/tag/style'
import 'vant/es/empty/style'
import 'vant/es/toast/style'
import 'vant/es/dialog/style'
import 'vant/es/popup/style'
import 'vant/es/search/style'
import 'vant/es/checkbox-group/style'
import 'vant/es/checkbox/style'
import 'vant/es/button/style'

import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardDetailTable, { type DetailRow } from '@/components/cardflow/CardDetailTable.vue'
import CardRelationPicker from '@/components/cardflow/CardRelationPicker.vue'
import { useUserSearch } from '@/composables/useUserSearch'

import {
  getCard,
  updateCard,
  submitCard,
  resubmitCard,
  getFlowVersionDetail,
  getCardRelations,
  createCardRelation,
  getAvailableOffsets,
} from '@/api/cardflow'
import type {
  CardDetailDto,
  CardRelationDto,
  CardBalanceDto,
  CardListDto,
  SchemaFieldDefinition,
  UpdateCardDetailRequest,
} from '@/types/cardflow'
import { parseCardSchemaFields, parseDetailSchemaFields } from '@/utils/cardflowSchema'

// ==================== 路由参数 ====================

const route = useRoute()
const router = useRouter()
const cardId = computed(() => Number(route.params.id))

// ==================== 状态 ====================

const loading = ref(true)
const refreshing = ref(false)
const submitting = ref(false)

const card = ref<CardDetailDto | null>(null)
const flowName = ref('')
const cardSchema = ref<SchemaFieldDefinition[]>([])
const detailSchema = ref<SchemaFieldDefinition[]>([])
const flowSettings = ref<Record<string, any>>({})

const formData = ref<Record<string, any>>({})
const detailRows = ref<DetailRow[]>([])
const errors = ref<Record<string, string>>({})

// 发起人自选(initiatorSelect)：配了该策略的节点（超集：条件路由不可预知激活，全部出选人器）
const initiatorSelectStages = ref<{ stageKey: string; stageName: string }[]>([])
// { stageKey: [{userId,userName}] }
const initiatorAssignments = ref<Record<string, { userId: number; userName: string }[]>>({})
// 选人弹层
const {
  userOptions: pickerUserOptions, loading: pickerLoading,
  load: loadPickerUsers, search: searchPickerUsers, pin: pinPickerUser,
} = useUserSearch({ pageSize: 50 })
const pickerVisible = ref(false)
const pickerStageKey = ref<string>('')
const pickerKeyword = ref('')
const pickerChecked = ref<number[]>([])

const relations = ref<CardRelationDto[]>([])
const offsets = ref<CardBalanceDto[]>([])
const showRelationPicker = ref(false)

// 保存状态：idle | saving | saved | dirty | offline
type SaveState = 'idle' | 'saving' | 'saved' | 'dirty' | 'offline'
const saveState = ref<SaveState>('idle')
const isOnline = ref(typeof navigator !== 'undefined' ? navigator.onLine : true)

const AUTOSAVE_INTERVAL = 60_000
const LS_KEY_PREFIX = 'cardflow:offline:'
let autosaveTimer: number | null = null
let suppressDirty = true

// ==================== 计算属性 ====================

const lsKey = computed(() => `${LS_KEY_PREFIX}${cardId.value}`)

const navBarTitle = computed(() => `填写${flowName.value || '卡片'}`)

const saveStatusText = computed(() => {
  if (!isOnline.value) return '离线缓存中'
  switch (saveState.value) {
    case 'saving':
      return '暂存中...'
    case 'saved':
      return '已暂存'
    case 'dirty':
      return '未保存'
    case 'offline':
      return '离线缓存中'
    default:
      return ''
  }
})

const hasOffsetConfig = computed(() => Boolean(flowSettings.value?.offset))

const hasPrerequisite = computed(() => {
  // 配置中如有前置依赖或 schema 含 cardRef 字段，则显示关联区
  if (flowSettings.value?.prerequisites) return true
  return cardSchema.value.some(f => f.type === 'cardRef')
})

// 后端 UpdateAsync 仅接受 draft：returned 件不可再编辑/保存（保存必 400），只能原样重提（resubmit）
const isDraft = computed(() => card.value?.status === 'draft')
const isReturned = computed(() => card.value?.status === 'returned')

// ==================== 数据加载 ====================

function parseSchema(json?: string | null): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

function parseSettings(json?: string | null): Record<string, any> {
  if (!json) return {}
  try {
    const parsed = JSON.parse(json)
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

// 行ID → 明细表键，回传时保留原表归属（新增行落 default）
const detailTableKeys = new Map<string, string>()

function buildDetailRows(): DetailRow[] {
  if (!card.value?.details?.length) return []
  detailTableKeys.clear()
  return card.value.details.map((d, idx) => {
    let parsed: Record<string, any> = {}
    try {
      parsed = d.dataJson ? JSON.parse(d.dataJson) : {}
    } catch {
      parsed = {}
    }
    const rowId = String(d.id || `row_${idx}`)
    detailTableKeys.set(rowId, d.detailTableKey)
    return {
      _id: rowId,
      ...parsed,
    } as DetailRow
  })
}

async function loadCard(silent = false) {
  if (!silent) loading.value = true
  suppressDirty = true
  try {
    const cardRes = await getCard(cardId.value)
    card.value = cardRes
    flowName.value = cardRes.flowName

    // 加载 schema（流程版本）
    if (cardRes.flowDefinitionId && cardRes.flowVersionId) {
      try {
        const version = await getFlowVersionDetail(cardRes.flowDefinitionId, cardRes.flowVersionId)
        cardSchema.value = parseSchema(version.cardSchemaJson)
        detailSchema.value = parseDetailSchemaFields(version.detailSchemaJson)
        flowSettings.value = parseSettings(version.flowSettingsJson)
        // 发起人自选(initiatorSelect)：全部配了该策略的节点超集，供 fill 页选人器渲染
        initiatorSelectStages.value = (version.stages || [])
          .filter(s => s.assigneeStrategy === 'initiatorSelect' && s.stageKey)
          .map(s => ({ stageKey: s.stageKey as string, stageName: s.stageName || (s.stageKey as string) }))
      } catch {
        cardSchema.value = []
        detailSchema.value = []
        flowSettings.value = {}
        initiatorSelectStages.value = []
      }
    }

    // 解析 dataJson
    let parsedData: Record<string, any> = {}
    if (cardRes.dataJson) {
      try {
        parsedData = JSON.parse(cardRes.dataJson) || {}
      } catch {
        parsedData = {}
      }
    }
    // 发起人自选(initiatorSelect)：回显已存的选人结果
    let parsedInitiatorAssignments: Record<string, { userId: number; userName: string }[]> = {}
    try {
      parsedInitiatorAssignments = cardRes.initiatorAssignmentsJson ? JSON.parse(cardRes.initiatorAssignmentsJson) : {}
    } catch {
      parsedInitiatorAssignments = {}
    }
    // 检查 localStorage 是否有更新的离线数据
    const offlineRaw = typeof localStorage !== 'undefined' ? localStorage.getItem(lsKey.value) : null
    if (offlineRaw) {
      try {
        const offlineData = JSON.parse(offlineRaw)
        if (offlineData?.savedAt && (!cardRes.submitTime || new Date(offlineData.savedAt).getTime() > Date.parse(cardRes.submitTime || '0'))) {
          parsedData = offlineData.formData ?? parsedData
          if (Array.isArray(offlineData.detailRows)) {
            detailRows.value = offlineData.detailRows
          }
          if (offlineData.initiatorAssignments) {
            parsedInitiatorAssignments = offlineData.initiatorAssignments
          }
          showToast({ message: '已恢复离线缓存', position: 'bottom' })
        }
      } catch {
        // ignore
      }
    }
    formData.value = parsedData
    initiatorAssignments.value = parsedInitiatorAssignments
    // pin 已选项以便弹层回显名字（远端搜索换页不丢失）
    Object.values(initiatorAssignments.value).flat().forEach(u =>
      pinPickerUser({ label: u.userName || `#${u.userId}`, value: u.userId, name: u.userName || `#${u.userId}` }))

    // 明细
    if (!detailRows.value.length) {
      detailRows.value = buildDetailRows()
    }

    // 关联与冲抵
    try {
      relations.value = (await getCardRelations(cardId.value)) || []
    } catch {
      relations.value = []
    }
    if (hasOffsetConfig.value) {
      try {
        offsets.value = (await getAvailableOffsets(cardId.value)) || []
      } catch {
        offsets.value = []
      }
    }

    saveState.value = 'saved'
  } catch {
    showToast({ message: '加载失败', type: 'fail' })
  } finally {
    loading.value = false
    nextTick(() => {
      suppressDirty = false
    })
  }
}

async function onRefresh() {
  refreshing.value = true
  try {
    await loadCard(true)
  } finally {
    refreshing.value = false
  }
}

// ==================== 自动保存 ====================

// 明细必须走 UpdateCardRequest.details 顶层字段（全量替换，后端据此汇总 amount 回写主表单）；
// 内嵌进 dataJson 的明细后端零消费，审批端不可见
function buildUpdatePayload(): { dataJson: string; details: UpdateCardDetailRequest[]; initiatorAssignmentsJson?: string | null } {
  return {
    dataJson: JSON.stringify(formData.value),
    details: detailRows.value.map((row, idx) => {
      const { _id, ...data } = row
      return {
        detailTableKey: detailTableKeys.get(String(_id)) || 'default',
        sortOrder: idx,
        dataJson: JSON.stringify(data),
      }
    }),
    initiatorAssignmentsJson: initiatorSelectStages.value.length ? JSON.stringify(initiatorAssignments.value) : null,
  }
}

function cacheOffline() {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(
      lsKey.value,
      JSON.stringify({
        formData: formData.value,
        detailRows: detailRows.value,
        initiatorAssignments: initiatorAssignments.value,
        savedAt: new Date().toISOString(),
      })
    )
  } catch {
    // 忽略容量异常
  }
}

function clearOfflineCache() {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.removeItem(lsKey.value)
  } catch {
    // ignore
  }
}

// ==================== 发起人自选(initiatorSelect) ====================

function openInitiatorPicker(stageKey: string) {
  pickerStageKey.value = stageKey
  pickerKeyword.value = ''
  pickerChecked.value = (initiatorAssignments.value[stageKey] || []).map(u => u.userId)
  pickerVisible.value = true
  loadPickerUsers()
}

function confirmInitiatorPicker() {
  const prev = new Map((initiatorAssignments.value[pickerStageKey.value] || []).map(u => [u.userId, u]))
  initiatorAssignments.value = {
    ...initiatorAssignments.value,
    [pickerStageKey.value]: pickerChecked.value.map(id => {
      const opt = pickerUserOptions.value.find(o => o.value === id)
      return { userId: id, userName: opt?.name || prev.get(id)?.userName || `#${id}` }
    }),
  }
  pickerVisible.value = false
}

function toggleInitiatorChecked(id: number) {
  pickerChecked.value = pickerChecked.value.includes(id)
    ? pickerChecked.value.filter(i => i !== id)
    : [...pickerChecked.value, id]
}

function initiatorSummary(stageKey: string): string {
  const list = initiatorAssignments.value[stageKey] || []
  if (!list.length) return '未指定'
  const names = list.map(u => u.userName || `#${u.userId}`)
  return names.length > 2 ? `${names.slice(0, 2).join('、')} 等 ${names.length} 人` : names.join('、')
}

async function autoSave() {
  if (!cardId.value || saveState.value === 'saving') return
  if (saveState.value !== 'dirty') return
  if (!isDraft.value) return

  if (!isOnline.value) {
    cacheOffline()
    saveState.value = 'offline'
    return
  }

  saveState.value = 'saving'
  try {
    const res = await updateCard(cardId.value, {
      ...buildUpdatePayload(),
      concurrencyStamp: card.value?.concurrencyStamp || undefined,
    })
    if (card.value) {
      card.value.concurrencyStamp = res.concurrencyStamp
    }
    saveState.value = 'saved'
    clearOfflineCache()
  } catch {
    cacheOffline()
    saveState.value = 'offline'
  }
}

function startAutosaveTimer() {
  stopAutosaveTimer()
  autosaveTimer = window.setInterval(() => {
    autoSave()
  }, AUTOSAVE_INTERVAL)
}

function stopAutosaveTimer() {
  if (autosaveTimer !== null) {
    clearInterval(autosaveTimer)
    autosaveTimer = null
  }
}

// ==================== 数据变更监听 ====================

watch(
  [formData, detailRows, initiatorAssignments],
  () => {
    if (suppressDirty) return
    saveState.value = 'dirty'
    if (!isOnline.value) cacheOffline()
  },
  { deep: true }
)

// ==================== 网络状态 ====================

function handleOnline() {
  isOnline.value = true
  // 网络恢复后立即触发同步
  if (saveState.value === 'offline' || saveState.value === 'dirty') {
    autoSave()
  }
}

function handleOffline() {
  isOnline.value = false
  if (saveState.value !== 'saved') {
    cacheOffline()
    saveState.value = 'offline'
  }
}

// ==================== 校验 ====================

function isMoneyField(f: SchemaFieldDefinition) {
  return f.type === 'money'
}

function validate(): boolean {
  const newErrors: Record<string, string> = {}

  for (const f of cardSchema.value) {
    const val = formData.value[f.key]
    if (f.required) {
      const empty =
        val === null ||
        val === undefined ||
        val === '' ||
        (Array.isArray(val) && val.length === 0)
      if (empty) {
        newErrors[f.key] = `请填写${f.label}`
        continue
      }
    }
    if (isMoneyField(f) && val !== null && val !== undefined && val !== '') {
      const num = Number(val)
      if (isNaN(num)) {
        newErrors[f.key] = '金额格式不正确'
      } else if (num < 0) {
        newErrors[f.key] = '金额不能为负'
      }
    }
    if (f.type === 'file' && Array.isArray(val) && f.maxSize) {
      const tooBig = val.find((v: any) => v?.file?.size && v.file.size / 1024 / 1024 > (f.maxSize as number))
      if (tooBig) {
        newErrors[f.key] = `文件不能超过 ${f.maxSize}MB`
      }
    }
  }

  errors.value = newErrors

  if (Object.keys(newErrors).length > 0) {
    nextTick(() => {
      const firstKey = Object.keys(newErrors)[0]
      const el = document.querySelector(`.fill-form [data-field-key="${firstKey}"]`)
      if (el && (el as HTMLElement).scrollIntoView) {
        ;(el as HTMLElement).scrollIntoView({ behavior: 'smooth', block: 'center' })
      } else {
        const fallback = document.querySelector('.fill-form .van-field--error')
        ;(fallback as HTMLElement | null)?.scrollIntoView?.({ behavior: 'smooth', block: 'center' })
      }
    })
    return false
  }
  return true
}

// ==================== 操作 ====================

async function handleSaveDraft() {
  if (!cardId.value) return
  submitting.value = true
  saveState.value = 'saving'
  try {
    const res = await updateCard(cardId.value, {
      ...buildUpdatePayload(),
      concurrencyStamp: card.value?.concurrencyStamp || undefined,
    })
    if (card.value) {
      card.value.concurrencyStamp = res.concurrencyStamp
    }
    saveState.value = 'saved'
    clearOfflineCache()
    showToast({ message: '草稿已保存', type: 'success' })
  } catch {
    cacheOffline()
    saveState.value = 'offline'
    showToast({ message: '保存失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function handleSubmit() {
  if (!validate()) {
    showToast({ message: '请补全必填字段', type: 'fail' })
    return
  }
  try {
    await showConfirmDialog({
      title: '确认提交',
      message: '提交后将进入审批流程，确定继续？',
    })
  } catch {
    return
  }

  submitting.value = true
  saveState.value = 'saving'
  try {
    // 先保存
    const updRes = await updateCard(cardId.value, {
      ...buildUpdatePayload(),
      concurrencyStamp: card.value?.concurrencyStamp || undefined,
    })
    if (card.value) {
      card.value.concurrencyStamp = updRes.concurrencyStamp
    }
    clearOfflineCache()

    // 提交
    const result = await submitCard(cardId.value)
    if (result.success) {
      saveState.value = 'saved'
      showToast({ message: '提交成功', type: 'success' })
      router.back()
    } else {
      saveState.value = 'dirty'
      showToast({ message: result.message || '提交失败', type: 'fail' })
    }
  } catch {
    saveState.value = 'dirty'
    showToast({ message: '提交失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

// 退回件原样重提：不走 updateCard（后端拒 draft 以外的更新），直接 resubmit 进入新一轮审批
async function handleResubmit() {
  if (!cardId.value) return
  try {
    await showConfirmDialog({
      title: '确认重新提交',
      message: '退回件将按原内容重新提交，进入新一轮审批，是否继续？',
    })
  } catch {
    return
  }
  submitting.value = true
  try {
    const result = await resubmitCard(cardId.value)
    if (result.success) {
      showToast({ message: '已重新提交', type: 'success' })
      router.back()
    } else {
      showToast({ message: result.message || '提交失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '提交失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function onRelationSelect(c: CardListDto) {
  if (!cardId.value) return
  try {
    await createCardRelation(cardId.value, {
      targetCardId: c.id,
      relationType: 'prerequisite',
    })
    relations.value = (await getCardRelations(cardId.value)) || []
    showToast({ message: '关联成功', type: 'success' })
  } catch {
    showToast({ message: '关联失败', type: 'fail' })
  }
}

function onClickLeft() {
  if (saveState.value === 'dirty' || saveState.value === 'offline') {
    showConfirmDialog({
      title: '尚未保存',
      message: '当前内容未保存，是否退出？',
      confirmButtonText: '保存并退出',
      cancelButtonText: '直接退出',
      showCancelButton: true,
    })
      .then(async () => {
        await handleSaveDraft()
        router.back()
      })
      .catch(() => {
        router.back()
      })
  } else {
    router.back()
  }
}

function totalOffsetAmount(b: CardBalanceDto) {
  return b.remainingAmount
}

// ==================== 生命周期 ====================

onMounted(() => {
  loadCard()
  startAutosaveTimer()
  if (typeof window !== 'undefined') {
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
  }
})

onBeforeUnmount(() => {
  stopAutosaveTimer()
  if (typeof window !== 'undefined') {
    window.removeEventListener('online', handleOnline)
    window.removeEventListener('offline', handleOffline)
  }
})
</script>

<template>
  <div class="mobile-fill-page">
    <VanNavBar
      :title="navBarTitle"
      left-arrow
      fixed
      placeholder
      @click-left="onClickLeft"
    >
      <template #right>
        <span class="save-status" :class="`save-status--${saveState}`">
          {{ saveStatusText }}
        </span>
      </template>
    </VanNavBar>

    <div v-if="loading" class="loading-wrap">
      <VanLoading size="36px" vertical>加载中...</VanLoading>
    </div>

    <VanPullRefresh
      v-else
      v-model="refreshing"
      @refresh="onRefresh"
      class="page-scroll"
    >
      <template v-if="card">
        <!-- 头部信息 -->
        <VanCellGroup inset class="info-card">
          <VanCell title="流程" :value="flowName || '-'" />
          <VanCell title="编号">
            <template #value>
              <span>{{ card.cardNumber || '（自动生成）' }}</span>
            </template>
          </VanCell>
          <VanCell title="状态">
            <template #value>
              <VanTag :type="isDraft ? 'primary' : (isReturned ? 'warning' : 'default')" size="medium">
                {{ isDraft ? '草稿' : (isReturned ? '已退回' : (card.status || '-')) }}
              </VanTag>
            </template>
          </VanCell>
        </VanCellGroup>

        <!-- 退回件提示：只读回显 + 原样重提引导 -->
        <VanCellGroup v-if="isReturned" inset class="returned-tip">
          <VanCell>
            <template #title>
              <span class="returned-tip__text">该卡片已被退回，内容为只读。如需修改请前往 PC 端，或按原内容“重新提交”进入新一轮审批。</span>
            </template>
          </VanCell>
        </VanCellGroup>

        <!-- 主表单 -->
        <div class="section fill-form">
          <div class="section-header">基本信息</div>
          <SchemaRenderer
            v-if="cardSchema.length > 0"
            v-model="formData"
            :schema="cardSchema"
            :errors="errors"
            :mode="isDraft ? 'edit' : 'view'"
            platform="mobile"
          />
          <VanEmpty v-else description="该流程未配置表单字段" />
        </div>

        <!-- 明细行 -->
        <div v-if="detailSchema.length > 0" class="section">
          <div class="section-header">明细行</div>
          <CardDetailTable
            v-model="detailRows"
            :schema="detailSchema"
            :mode="isDraft ? 'edit' : 'view'"
            platform="mobile"
          />
        </div>

        <!-- 关联卡片 -->
        <div v-if="hasPrerequisite" class="section">
          <div class="section-header">关联卡片</div>
          <VanCellGroup inset>
            <VanCell
              title="选择关联卡片"
              is-link
              value="去选择"
              @click="showRelationPicker = true"
            />
            <template v-if="relations.length > 0">
              <VanCell
                v-for="rel in relations"
                :key="rel.id"
                :title="rel.targetCardNumber || `卡片#${rel.targetCardId}`"
                :label="rel.description || rel.relationType"
              >
                <template #value>
                  <VanTag type="primary">{{ rel.relationType }}</VanTag>
                </template>
              </VanCell>
            </template>
          </VanCellGroup>
        </div>

        <!-- 冲抵借款 -->
        <div v-if="hasOffsetConfig" class="section">
          <div class="section-header">冲抵借款</div>
          <VanCellGroup inset>
            <VanEmpty v-if="!offsets.length" description="暂无可冲抵卡片" />
            <VanCell
              v-for="b in offsets"
              :key="b.id"
              :title="b.cardTitle || b.cardNumber || '-'"
              :label="`原始 ¥${b.originalAmount.toLocaleString('zh-CN')}`"
            >
              <template #value>
                <span class="offset-remain">
                  剩余 <b>¥{{ totalOffsetAmount(b).toLocaleString('zh-CN') }}</b>
                </span>
              </template>
            </VanCell>
          </VanCellGroup>
        </div>

        <!-- 发起人自选(initiatorSelect)：为配了该策略的节点指定处理人 -->
        <div v-if="initiatorSelectStages.length" class="section fill-initiator">
          <div class="section-header">指定处理人</div>
          <VanCellGroup inset>
            <VanCell
              v-for="st in initiatorSelectStages"
              :key="st.stageKey"
              :title="st.stageName"
              :value="initiatorSummary(st.stageKey)"
              is-link
              @click="openInitiatorPicker(st.stageKey)"
            />
          </VanCellGroup>
        </div>

        <div class="bottom-spacer" />
      </template>

      <VanEmpty v-else description="未找到卡片信息" />
    </VanPullRefresh>

    <!-- 底部操作栏 -->
    <VanActionBar v-if="!loading && card && isDraft">
      <VanActionBarButton
        type="warning"
        text="暂存草稿"
        :loading="submitting"
        @click="handleSaveDraft"
      />
      <VanActionBarButton
        type="danger"
        text="提交审批"
        :loading="submitting"
        @click="handleSubmit"
      />
    </VanActionBar>
    <!-- 退回件：仅原样重提 -->
    <VanActionBar v-else-if="!loading && card && isReturned">
      <VanActionBarButton
        type="danger"
        text="重新提交"
        :loading="submitting"
        @click="handleResubmit"
      />
    </VanActionBar>

    <!-- 关联选择器 -->
    <CardRelationPicker
      v-if="cardId"
      :card-id="cardId"
      v-model:show="showRelationPicker"
      @select="onRelationSelect"
    />

    <!-- 发起人自选：选人弹层 -->
    <VanPopup v-model:show="pickerVisible" position="bottom" round :style="{ height: '70%' }">
      <div class="initiator-picker">
        <VanSearch
          v-model="pickerKeyword"
          placeholder="搜索姓名/账号/部门"
          @update:model-value="searchPickerUsers"
        />
        <VanCheckboxGroup v-model="pickerChecked" class="initiator-picker__list">
          <VanCell
            v-for="opt in pickerUserOptions"
            :key="opt.value"
            :title="opt.name"
            :label="opt.orgName"
            clickable
            @click="toggleInitiatorChecked(opt.value)"
          >
            <template #right-icon>
              <VanCheckbox :name="opt.value" @click.stop />
            </template>
          </VanCell>
        </VanCheckboxGroup>
        <div class="initiator-picker__footer">
          <VanButton block type="primary" :loading="pickerLoading" @click="confirmInitiatorPicker">
            确定
          </VanButton>
        </div>
      </div>
    </VanPopup>
  </div>
</template>

<style scoped lang="scss">
.mobile-fill-page {
  min-height: 100vh;
  background: var(--bg-page);
  padding-bottom: calc(60px + env(safe-area-inset-bottom));
}

.loading-wrap {
  display: flex;
  justify-content: center;
  padding-top: 30vh;
}

.page-scroll {
  min-height: calc(100vh - 46px);
}

.returned-tip {
  margin-top: 8px;

  &__text {
    font-size: 13px;
    line-height: 1.5;
    color: var(--color-warning);
  }
}

.save-status {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 10px;
  background: #f0f1f3;
  color: #969799;
  transition: background 0.2s, color 0.2s;

  &--saving {
    background: var(--color-warning-light);
    color: var(--color-warning);
  }
  &--saved {
    background: #e6fffb;
    color: var(--color-success);
  }
  &--dirty {
    background: var(--color-danger-light);
    color: #ee0a24;
  }
  &--offline {
    background: var(--color-warning-light);
    color: #d46b08;
  }
}

.info-card {
  margin: 12px 0 0 !important;
}

.section {
  margin-top: 14px;

  .section-header {
    padding: 0 20px 6px;
    font-size: 13px;
    font-weight: 600;
    color: #646566;
    letter-spacing: 0.3px;
  }
}

.fill-form {
  :deep(.van-field) {
    --van-field-label-width: 6.6em;
  }
}

.offset-remain {
  font-size: 13px;
  color: #333;

  b {
    color: var(--color-success);
    margin-left: 2px;
  }
}

.bottom-spacer {
  height: 24px;
}

.initiator-picker {
  display: flex;
  flex-direction: column;
  height: 100%;

  &__list {
    flex: 1;
    overflow-y: auto;
  }

  &__footer {
    padding: 12px 16px calc(12px + env(safe-area-inset-bottom));
    border-top: 1px solid var(--border);
  }
}
</style>
