<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, reactive, ref, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  PullRefresh as VanPullRefresh,
  Field as VanField,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  Button as VanButton,
  ActionSheet as VanActionSheet,
  Empty as VanEmpty,
  showToast,
  showDialog,
} from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/loading/style'
import 'vant/es/pull-refresh/style'
import 'vant/es/field/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/button/style'
import 'vant/es/action-sheet/style'
import 'vant/es/empty/style'
import 'vant/es/toast/style'
import 'vant/es/dialog/style'

import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardDetailTable from '@/components/cardflow/CardDetailTable.vue'
import CardTimeline from '@/components/cardflow/CardTimeline.vue'
import StageInputFields from '@/components/cardflow/StageInputFields.vue'

import {
  getCard,
  getCardLogs,
  getFlowVersionDetail,
  approveCard,
  rejectCard,
  transferCard,
  countersignCard,
  ccCard,
  urgeCard,
  submitCard,
  updateCard,
  resubmitCard,
} from '@/api/cardflow'
import { useUserStore } from '@/stores/user'
import type {
  CardDetailDto,
  ActionLogDto,
  SchemaFieldDefinition,
  StageInstanceDto,
  StageDefinitionDto,
  UpdateCardDetailRequest,
} from '@/types/cardflow'
import { parseCardSchemaFields, parseDetailSchemaFields } from '@/utils/cardflowSchema'
import { useStageWorkView } from '@/composables/useStageWorkView'

// ==================== Route / Store ====================

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const cardId = computed(() => Number(route.params.id))
const currentUserId = computed<number | null>(() => userStore.userInfo?.id ?? null)

// ==================== State ====================

const loading = ref(true)
const refreshing = ref(false)
const submitting = ref(false)

const card = ref<CardDetailDto | null>(null)
const logs = ref<ActionLogDto[]>([])
const cardSchema = ref<SchemaFieldDefinition[]>([])
const detailSchema = ref<SchemaFieldDefinition[]>([])
const stageDefinitions = ref<StageDefinitionDto[]>([])

const cardData = ref<Record<string, unknown>>({})
const detailRows = ref<Array<Record<string, unknown> & { _id: string }>>([])
// 行ID → 明细表键，回传时保留原表归属（新增行落 default）
const detailTableKeys = new Map<string, string>()
const stageFieldsData = reactive<Record<string, unknown>>({})

// 底部操作面板：default(默认两键) | reject(展开退回) | approve(展开通过) | fill(填写态)
type BottomMode = 'default' | 'reject' | 'approve' | 'fill'
const bottomMode = ref<BottomMode>('default')
const opinionText = ref('')
const showMoreSheet = ref(false)

// 转交 / 加签 / 抄送 子表单
const showTransferSheet = ref(false)
const showCountersignSheet = ref(false)
const showCcSheet = ref(false)
const transferUserId = ref<number | undefined>(undefined)
const countersignUserId = ref<number | undefined>(undefined)
const countersignInsertMode = ref<'before' | 'after'>('after')
const ccUserIds = ref<string>('')

// 键盘自适应底距（visualViewport）
const kbOffset = ref(0)

// ==================== Status ====================

interface StatusMeta { text: string; tone: 'draft' | 'active' | 'done' | 'warn' | 'mute' }
const STATUS_MAP: Record<string, StatusMeta> = {
  draft:     { text: '草稿',   tone: 'draft' },
  active:    { text: '进行中', tone: 'active' },
  completed: { text: '已完成', tone: 'done' },
  returned:  { text: '已退回', tone: 'warn' },
  voided:    { text: '已作废', tone: 'mute' },
}
const statusMeta = computed<StatusMeta>(() =>
  card.value ? (STATUS_MAP[card.value.status] || { text: card.value.status, tone: 'mute' }) : STATUS_MAP.draft,
)

// ==================== Derived ====================

const currentStage = computed<StageInstanceDto | null>(() => {
  if (!card.value || !card.value.currentStageInstanceId) return null
  return card.value.stageInstances.find(s => s.id === card.value!.currentStageInstanceId) || null
})

const currentAssigneeIds = computed<number[]>(() => {
  if (!currentStage.value) return []
  return (currentStage.value.assignees || []).map(a => a.userId)
})

const isInitiator = computed(() =>
  !!card.value && currentUserId.value !== null && card.value.initiatorId === currentUserId.value)

const isAssignee = computed(() =>
  currentUserId.value !== null && currentAssigneeIds.value.includes(currentUserId.value))

/** 视图模式：fill | resubmit | approve | readonly */
const viewMode = computed<'fill' | 'resubmit' | 'approve' | 'readonly'>(() => {
  if (!card.value) return 'readonly'
  const s = card.value.status
  if (s === 'draft' && isInitiator.value) return 'fill'
  // 后端 UpdateAsync 仅接受 draft，退回件只能原样重提（resubmit），不可再编辑
  if (s === 'returned' && isInitiator.value) return 'resubmit'
  if (s === 'active' && isAssignee.value) return 'approve'
  return 'readonly'
})

// StageWorkView 应用逻辑统一走 useStageWorkView（移动口径 cardRequiredFallback='access'，D1 分歧见 composable 注释）。
const stageView = useStageWorkView(card, { cardRequiredFallback: 'access' })
const runtimeComponents = stageView.runtimeComponents
const hasRuntimeComponents = stageView.hasRuntimeComponents

function legacyStageInputKeys(): string[] {
  if (!currentStage.value || stageDefinitions.value.length === 0) return []
  const def = stageDefinitions.value.find(d =>
    d.id === currentStage.value!.stageDefinitionId || d.stageName === currentStage.value!.stageName)
  if (!def?.inputFieldsJson) return []
  try {
    const parsed = JSON.parse(def.inputFieldsJson)
    if (Array.isArray(parsed)) return parsed.filter((key: unknown) => typeof key === 'string')
    if (parsed?.version === 2 && Array.isArray(parsed.inputFields)) return parsed.inputFields
  } catch {}
  return []
}

/** 当前节点的补充输入字段（来自 work view；无 fieldAccess 时兼容旧版 inputFieldsJson）。 */
const stageInputFields = computed<SchemaFieldDefinition[]>(() =>
  stageView.buildStageInputFields(
    cardSchema.value.length > 0 ? cardSchema.value : fallbackSchemaFromData(cardData.value),
    { legacyKeys: legacyStageInputKeys },
  ))

const initiatorTime = computed(() => formatDate(card.value?.submitTime || card.value?.createdTime))

// ==================== Helpers ====================

function safeParseSchema(json: string | null | undefined): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

function formatDate(val: string | null | undefined): string {
  if (!val) return ''
  const d = new Date(val)
  if (isNaN(d.getTime())) return String(val)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function fallbackSchemaFromData(data: Record<string, unknown>): SchemaFieldDefinition[] {
  return Object.keys(data).map(k => ({
    key: k, label: k,
    type: typeof data[k] === 'number' ? 'money' : 'text',
  })) as SchemaFieldDefinition[]
}

// ==================== Loaders ====================

async function loadAll() {
  try {
    const [c, l] = await Promise.all([
      getCard(cardId.value),
      getCardLogs(cardId.value).catch(() => [] as ActionLogDto[]),
    ])
    card.value = c as CardDetailDto
    logs.value = (l as ActionLogDto[]) || []

    // dataJson
    cardData.value = (() => {
      if (!c?.dataJson) return {}
      try { return JSON.parse(c.dataJson) as Record<string, unknown> } catch { return {} }
    })()

    // detail rows
    detailTableKeys.clear()
    detailRows.value = (c?.details || []).map(d => {
      let obj: Record<string, unknown> = {}
      try { obj = d.dataJson ? JSON.parse(d.dataJson) : {} } catch { obj = {} }
      detailTableKeys.set(String(d.id), d.detailTableKey)
      return { _id: String(d.id), ...obj }
    })

    // version → schema + stage defs
    if (c) {
      try {
        const ver = await getFlowVersionDetail(c.flowDefinitionId, c.flowVersionId)
        cardSchema.value = safeParseSchema(ver.cardSchemaJson)
        detailSchema.value = parseDetailSchemaFields(ver.detailSchemaJson)
        stageDefinitions.value = ver.stages || []
      } catch {
        cardSchema.value = []; detailSchema.value = []; stageDefinitions.value = []
      }
    }
  } catch {
    showToast({ message: '加载失败', type: 'fail' })
  }
}

async function refresh() {
  await loadAll()
  refreshing.value = false
}

async function init() {
  loading.value = true
  await loadAll()
  loading.value = false
}

// ==================== Effective schemas ====================

const effectiveCardSchema = computed(() =>
  stageView.applyFieldAccess(cardSchema.value.length > 0 ? cardSchema.value : fallbackSchemaFromData(cardData.value)))

const effectiveDetailSchema = computed<SchemaFieldDefinition[]>(() => {
  if (detailSchema.value.length > 0) return stageView.applyDetailAccess(detailSchema.value)
  if (detailRows.value.length === 0) return []
  const first = detailRows.value[0]
  return stageView.applyDetailAccess(Object.keys(first).filter(k => k !== '_id').map(k => ({
    key: k, label: k,
    type: typeof (first as Record<string, unknown>)[k] === 'number' ? 'money' : 'text',
  })) as SchemaFieldDefinition[])
})

// ==================== Actions ====================

function onClickBack() {
  if (window.history.length > 1) router.back()
  else router.push('/cardflow/upload-center')
}

const canStageAction = stageView.canStageAction

function openReject() {
  if (!canStageAction('reject')) { showToast('当前节点不允许退回'); return }
  opinionText.value = ''
  bottomMode.value = 'reject'
}
function openApprove() {
  if (!canStageAction('approve')) { showToast('当前节点不允许通过'); return }
  opinionText.value = ''
  bottomMode.value = 'approve'
}
function cancelBottom() { bottomMode.value = viewMode.value === 'fill' ? 'fill' : 'default'; opinionText.value = '' }

async function handleApprove() {
  if (!card.value) return
  // 校验 supplement
  if (stageInputFields.value.length > 0) {
    const missing = stageInputFields.value.find(f => f.required && (
      stageFieldsData[f.key] === null || stageFieldsData[f.key] === undefined || stageFieldsData[f.key] === ''
    ))
    if (missing) { showToast(`请填写：${missing.label}`); return }
  }
  submitting.value = true
  try {
    const r = await approveCard(card.value.id, {
      opinion: opinionText.value || null,
      supplementData: Object.keys(stageFieldsData).length ? { ...stageFieldsData } : null,
      concurrencyStamp: card.value.concurrencyStamp,
    })
    if (r.success) {
      showToast({ message: '已通过', type: 'success' })
      bottomMode.value = 'default'; opinionText.value = ''
      await loadAll()
    } else { showToast({ message: r.message || '操作失败', type: 'fail' }) }
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

async function handleReject() {
  if (!card.value) return
  if (!opinionText.value.trim()) { showToast('请输入退回原因'); return }
  submitting.value = true
  try {
    const r = await rejectCard(card.value.id, {
      opinion: opinionText.value,
      concurrencyStamp: card.value.concurrencyStamp,
    })
    if (r.success) {
      showToast({ message: '已退回', type: 'success' })
      bottomMode.value = 'default'; opinionText.value = ''
      await loadAll()
    } else { showToast({ message: r.message || '操作失败', type: 'fail' }) }
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

// 后端 UpdateAsync 对 details 为全量替换：必须回传完整明细集（含未改动行），否则旧明细被清空
function buildDetailsPayload(): UpdateCardDetailRequest[] {
  return detailRows.value.map((row, idx) => {
    const { _id, ...data } = row
    return {
      detailTableKey: detailTableKeys.get(_id) || 'default',
      sortOrder: idx,
      dataJson: JSON.stringify(data),
    }
  })
}

async function handleSaveDraft() {
  if (!card.value) return
  submitting.value = true
  try {
    await updateCard(card.value.id, {
      dataJson: JSON.stringify(cardData.value),
      details: buildDetailsPayload(),
      concurrencyStamp: card.value.concurrencyStamp,
    })
    showToast({ message: '已暂存', type: 'success' })
    await loadAll()
  } catch { showToast({ message: '暂存失败', type: 'fail' }) }
  finally { submitting.value = false }
}

async function handleSubmit() {
  if (!card.value) return
  try {
    await showDialog({ title: '确认提交', message: '提交后将进入审批流程，是否继续？', showCancelButton: true })
  } catch { return }
  submitting.value = true
  try {
    // 先保存
    await updateCard(card.value.id, {
      dataJson: JSON.stringify(cardData.value),
      details: buildDetailsPayload(),
      concurrencyStamp: card.value.concurrencyStamp,
    })
    const r = await submitCard(card.value.id)
    if (r.success) {
      showToast({ message: '已提交', type: 'success' })
      await loadAll()
    } else { showToast({ message: r.message || '提交失败', type: 'fail' }) }
  } catch { showToast({ message: '提交失败', type: 'fail' }) }
  finally { submitting.value = false }
}

async function handleResubmit() {
  if (!card.value) return
  try {
    await showDialog({
      title: '重新提交',
      message: '将按原内容重新提交，进入新一轮审批，是否继续？',
      showCancelButton: true,
    })
  } catch { return }
  submitting.value = true
  try {
    const r = await resubmitCard(card.value.id)
    if (r.success) {
      showToast({ message: '已重新提交', type: 'success' })
      await loadAll()
    } else { showToast({ message: r.message || '操作失败', type: 'fail' }) }
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

// ===== more menu =====

const moreActions = computed(() => [
  { name: '转交', value: 'transfer', action: 'transfer', subname: '将审批转给他人' },
  { name: '前加签', value: 'countersign-before', action: 'addSignBefore', subname: '先由他人补充审批' },
  { name: '后加签', value: 'countersign-after', action: 'addSignAfter', subname: '本人通过后邀请他人' },
  { name: '抄送', value: 'cc', action: 'cc', subname: '通知相关人员' },
  { name: '催办', value: 'urge', action: 'urge', subname: '提醒处理人' },
].filter(item => item.value === 'urge'
  // 后端 UrgeAsync 不做动作策略校验且默认策略不含 urge：卡片进行中即可催办
  ? card.value?.status === 'active'
  : canStageAction(item.action)))

function onMoreSelect(item: { value: string }) {
  showMoreSheet.value = false
  switch (item.value) {
    case 'transfer':    transferUserId.value = undefined; opinionText.value = ''; showTransferSheet.value = true; break
    case 'countersign-before':
      countersignInsertMode.value = 'before'
      countersignUserId.value = undefined
      opinionText.value = ''
      showCountersignSheet.value = true
      break
    case 'countersign-after':
      countersignInsertMode.value = 'after'
      countersignUserId.value = undefined
      opinionText.value = ''
      showCountersignSheet.value = true
      break
    case 'cc':          ccUserIds.value = ''; opinionText.value = ''; showCcSheet.value = true; break
    case 'urge':        handleUrge(); break
  }
}

async function handleUrge() {
  if (!card.value) return
  try {
    await urgeCard(card.value.id)
    showToast({ message: '催办已发送', type: 'success' })
  } catch { showToast({ message: '催办失败', type: 'fail' }) }
}

async function handleTransfer() {
  if (!card.value || !transferUserId.value) { showToast('请填写转交人 ID'); return }
  submitting.value = true
  try {
    const r = await transferCard(card.value.id, { newUserId: transferUserId.value, opinion: opinionText.value || null })
    if (r.success) { showToast({ message: '已转交', type: 'success' }); showTransferSheet.value = false; await loadAll() }
    else showToast({ message: r.message || '操作失败', type: 'fail' })
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

async function handleCountersign() {
  if (!card.value || !countersignUserId.value) { showToast('请填写加签人 ID'); return }
  submitting.value = true
  try {
    const r = await countersignCard(card.value.id, {
      userId: countersignUserId.value,
      insertMode: countersignInsertMode.value,
      opinion: opinionText.value || null,
    })
    if (r.success) { showToast({ message: '已加签', type: 'success' }); showCountersignSheet.value = false; await loadAll() }
    else showToast({ message: r.message || '操作失败', type: 'fail' })
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

async function handleCc() {
  if (!card.value) return
  const ids = ccUserIds.value.split(/[,，;\s]+/).map(s => Number(s.trim())).filter(n => !isNaN(n) && n > 0)
  if (ids.length === 0) { showToast('请填写抄送人 ID'); return }
  submitting.value = true
  try {
    const r = await ccCard(card.value.id, { userIds: ids, opinion: opinionText.value || null })
    if (r.success) { showToast({ message: '已抄送', type: 'success' }); showCcSheet.value = false; await loadAll() }
    else showToast({ message: r.message || '操作失败', type: 'fail' })
  } catch { showToast({ message: '操作失败', type: 'fail' }) }
  finally { submitting.value = false }
}

// ==================== Keyboard / visualViewport ====================

function onViewportResize() {
  const vv = window.visualViewport
  if (!vv) { kbOffset.value = 0; return }
  const offset = Math.max(0, window.innerHeight - vv.height - vv.offsetTop)
  kbOffset.value = offset
}

onMounted(() => {
  init()
  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', onViewportResize)
    window.visualViewport.addEventListener('scroll', onViewportResize)
  }
})
onBeforeUnmount(() => {
  if (window.visualViewport) {
    window.visualViewport.removeEventListener('resize', onViewportResize)
    window.visualViewport.removeEventListener('scroll', onViewportResize)
  }
})

// 进入填写态时让底部模式同步
function syncBottomMode() {
  if (viewMode.value === 'fill' && bottomMode.value !== 'fill') bottomMode.value = 'fill'
  if (viewMode.value !== 'fill' && bottomMode.value === 'fill') bottomMode.value = 'default'
}

// 初次加载完成后同步
import { watch } from 'vue'
watch(viewMode, syncBottomMode, { immediate: false })
watch(() => card.value?.id, () => nextTick(syncBottomMode))
</script>

<template>
  <div class="m-card-approval">
    <!-- ===== NavBar ===== -->
    <VanNavBar fixed placeholder safe-area-inset-top class="m-nav" :border="false">
      <template #left>
        <span class="m-nav__back" @click="onClickBack">
          <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true">
            <path fill="currentColor" d="M15.5 4.5 7 12l8.5 7.5 1.4-1.5-7-6 7-6z" />
          </svg>
        </span>
      </template>
      <template #title>
        <div class="m-nav__title">
          <span class="m-nav__label">审批</span>
          <span v-if="card?.cardNumber" class="m-nav__no">{{ card.cardNumber }}</span>
          <span v-if="card" class="m-nav__pill" :class="`m-nav__pill--${statusMeta.tone}`">
            {{ statusMeta.text }}
          </span>
        </div>
      </template>
    </VanNavBar>

    <!-- ===== Loading ===== -->
    <div v-if="loading" class="m-loading">
      <VanLoading size="32px" vertical>加载中…</VanLoading>
    </div>

    <!-- ===== Empty ===== -->
    <VanEmpty v-else-if="!card" description="未找到卡片" />

    <!-- ===== Body ===== -->
    <VanPullRefresh v-else v-model="refreshing" @refresh="refresh" class="m-scroll">
      <!-- 发起人信息 -->
      <section class="m-strip">
        <div class="m-strip__avatar">{{ (card.initiatorName || '?').slice(0, 1) }}</div>
        <div class="m-strip__main">
          <div class="m-strip__name">
            <span>{{ card.initiatorName }}</span>
            <span class="m-strip__dot">·</span>
            <span class="m-strip__flow">{{ card.flowName }}</span>
          </div>
          <div class="m-strip__meta">
            <span>{{ initiatorTime || '—' }}</span>
            <span v-if="card.title" class="m-strip__title">{{ card.title }}</span>
          </div>
        </div>
      </section>

      <!-- 卡片字段 -->
      <section class="m-block" v-if="effectiveCardSchema.length > 0">
        <div class="m-block__head"><span class="m-block__bar"></span><h3>卡片信息</h3></div>
        <SchemaRenderer
          :schema="effectiveCardSchema"
          :components="runtimeComponents"
          :detail-rows="detailRows"
          :model-value="cardData"
          :mode="viewMode === 'fill' ? 'edit' : 'view'"
          platform="mobile"
          @update:model-value="cardData = $event"
          @update:detail-rows="detailRows = $event"
        />
      </section>
      <section v-else-if="Object.keys(cardData).length > 0" class="m-block">
        <div class="m-block__head"><span class="m-block__bar"></span><h3>卡片信息</h3></div>
        <VanCellGroup inset>
          <VanCell v-for="(v, k) in cardData" :key="String(k)" :title="String(k)" :value="String(v ?? '-')" />
        </VanCellGroup>
      </section>

      <!-- 明细 -->
      <section class="m-block" v-if="!hasRuntimeComponents && detailRows.length > 0 && effectiveDetailSchema.length > 0">
        <div class="m-block__head"><span class="m-block__bar"></span><h3>明细</h3></div>
        <CardDetailTable
          :schema="effectiveDetailSchema"
          :model-value="detailRows"
          :mode="viewMode === 'fill' ? 'edit' : 'view'"
          platform="mobile"
          @update:model-value="detailRows = $event"
        />
      </section>

      <!-- 补充字段 -->
      <section class="m-block" v-if="viewMode === 'approve' && stageInputFields.length > 0">
        <div class="m-block__head"><span class="m-block__bar m-block__bar--accent"></span><h3>补充信息</h3></div>
        <StageInputFields
          :fields="stageInputFields"
          :model-value="stageFieldsData"
          platform="mobile"
          @update:model-value="(v) => Object.assign(stageFieldsData, v)"
        />
      </section>

      <!-- 审批记录 -->
      <section class="m-block">
        <div class="m-block__head"><span class="m-block__bar"></span><h3>审批记录</h3></div>
        <div class="m-timeline-wrap">
          <CardTimeline
            :stages="card.stageInstances || []"
            :current-round="card.currentRound"
            mode="compact"
          />
        </div>
      </section>

      <!-- 触发结果（completed/voided 时占位提示） -->
      <section v-if="card.status === 'completed' || card.status === 'voided'" class="m-result">
        <div class="m-result__icon">
          <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true">
            <path v-if="card.status === 'completed'" fill="currentColor"
              d="M9 16.2 4.8 12l-1.4 1.4L9 19l12-12-1.4-1.4z"/>
            <path v-else fill="currentColor"
              d="M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm5 11H7v-2h10z"/>
          </svg>
        </div>
        <div class="m-result__text">
          {{ card.status === 'completed' ? '流程已完成' : '卡片已作废' }}
          <small v-if="card.completedTime">{{ formatDate(card.completedTime) }}</small>
        </div>
      </section>

      <!-- 底部安全占位 -->
      <div class="m-bottom-spacer" :style="{ height: viewMode === 'readonly' ? '24px' : '120px' }" />
    </VanPullRefresh>

    <!-- ===== 底部操作栏 ===== -->
    <div
      v-if="!loading && card && viewMode !== 'readonly'"
      class="m-bar"
      :class="[`m-bar--${bottomMode}`]"
      :style="{ paddingBottom: `calc(${kbOffset}px + env(safe-area-inset-bottom, 0px))` }"
    >
      <!-- 填写态：暂存 / 提交 -->
      <template v-if="viewMode === 'fill'">
        <div class="m-bar__row">
          <VanButton class="m-btn m-btn--ghost" :loading="submitting" @click="handleSaveDraft">暂存</VanButton>
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleSubmit">提交</VanButton>
        </div>
      </template>

      <!-- 已退回（发起人）：仅原样重提 -->
      <template v-else-if="viewMode === 'resubmit'">
        <div class="m-bar__hint">卡片已退回：将按原内容重新提交，进入新一轮审批</div>
        <div class="m-bar__row">
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleResubmit">重新提交</VanButton>
        </div>
      </template>

      <!-- 审批态：默认两键 -->
      <template v-else-if="bottomMode === 'default'">
        <div class="m-bar__row">
          <VanButton v-if="canStageAction('reject')" class="m-btn m-btn--ghost" @click="openReject">退回</VanButton>
          <VanButton v-if="canStageAction('approve')" class="m-btn m-btn--primary" @click="openApprove">通过</VanButton>
          <button v-if="moreActions.length > 0" class="m-bar__more" aria-label="更多" @click="showMoreSheet = true">···</button>
        </div>
      </template>

      <!-- 退回展开态 -->
      <template v-else-if="bottomMode === 'reject'">
        <div class="m-bar__head">
          <span class="m-bar__title m-bar__title--warn">退回意见 <em>*</em></span>
          <span class="m-bar__close" @click="cancelBottom">取消</span>
        </div>
        <VanField
          v-model="opinionText"
          type="textarea"
          rows="3"
          autosize
          maxlength="500"
          show-word-limit
          placeholder="请输入退回原因"
          class="m-bar__textarea"
        />
        <div class="m-bar__row">
          <VanButton class="m-btn m-btn--ghost" @click="cancelBottom">取消</VanButton>
          <VanButton class="m-btn m-btn--danger" :loading="submitting" @click="handleReject">确认退回</VanButton>
        </div>
      </template>

      <!-- 通过展开态 -->
      <template v-else-if="bottomMode === 'approve'">
        <div class="m-bar__head">
          <span class="m-bar__title">审批意见</span>
          <span class="m-bar__close" @click="cancelBottom">取消</span>
        </div>
        <VanField
          v-model="opinionText"
          type="textarea"
          rows="2"
          autosize
          maxlength="500"
          show-word-limit
          placeholder="选填审批意见"
          class="m-bar__textarea"
        />
        <div class="m-bar__row">
          <VanButton class="m-btn m-btn--ghost" @click="cancelBottom">取消</VanButton>
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleApprove">确认通过</VanButton>
        </div>
      </template>
    </div>

    <!-- ===== ActionSheet: 更多 ===== -->
    <VanActionSheet
      v-model:show="showMoreSheet"
      :actions="moreActions"
      cancel-text="取消"
      close-on-click-action
      @select="onMoreSelect"
    />

    <!-- ===== Sheet: 转交 ===== -->
    <VanActionSheet v-model:show="showTransferSheet" title="转交">
      <div class="m-sheet">
        <VanCellGroup inset>
          <VanField v-model.number="transferUserId" label="转交人 ID" type="number" placeholder="请输入用户 ID" />
          <VanField v-model="opinionText" label="说明" type="textarea" rows="2" autosize placeholder="选填说明" />
        </VanCellGroup>
        <div class="m-sheet__row">
          <VanButton class="m-btn m-btn--ghost" @click="showTransferSheet = false">取消</VanButton>
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleTransfer">确认转交</VanButton>
        </div>
      </div>
    </VanActionSheet>

    <!-- ===== Sheet: 加签 ===== -->
    <VanActionSheet v-model:show="showCountersignSheet" :title="countersignInsertMode === 'before' ? '前加签' : '后加签'">
      <div class="m-sheet">
        <VanCellGroup inset>
          <VanField v-model.number="countersignUserId" label="加签人 ID" type="number" placeholder="请输入用户 ID" />
          <VanCell title="加签方式" :value="countersignInsertMode === 'before' ? '前加签' : '后加签'" />
          <VanField v-model="opinionText" label="说明" type="textarea" rows="2" autosize placeholder="选填说明" />
        </VanCellGroup>
        <div class="m-sheet__row">
          <VanButton class="m-btn m-btn--ghost" @click="showCountersignSheet = false">取消</VanButton>
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleCountersign">确认加签</VanButton>
        </div>
      </div>
    </VanActionSheet>

    <!-- ===== Sheet: 抄送 ===== -->
    <VanActionSheet v-model:show="showCcSheet" title="抄送">
      <div class="m-sheet">
        <VanCellGroup inset>
          <VanField v-model="ccUserIds" label="抄送人 ID" placeholder="多人用逗号分隔" />
          <VanField v-model="opinionText" label="说明" type="textarea" rows="2" autosize placeholder="选填说明" />
        </VanCellGroup>
        <div class="m-sheet__row">
          <VanButton class="m-btn m-btn--ghost" @click="showCcSheet = false">取消</VanButton>
          <VanButton class="m-btn m-btn--primary" :loading="submitting" @click="handleCc">确认抄送</VanButton>
        </div>
      </div>
    </VanActionSheet>
  </div>
</template>

<style scoped lang="scss">
// ====== Tokens ======
$bg:        #f3f5f9;
$surface:   #ffffff;
$ink:       #1f2430;
$ink-2:     #4a5568;
$ink-3:     #8a93a6;
$line:      #e9ecf2;
$brand:     var(--color-primary);
$brand-ink: var(--color-primary-active);
$warn:      var(--color-danger);
$ok:        var(--color-success);

.m-card-approval {
  min-height: 100vh;
  background:
    radial-gradient(1200px 400px at 0% -10%, rgba(47, 84, 235, 0.06), transparent 60%),
    radial-gradient(1000px 300px at 100% 0%, rgba(56, 158, 13, 0.04), transparent 60%),
    $bg;
  font-family: 'PingFang SC', 'MiSans', 'HarmonyOS Sans SC', system-ui, sans-serif;
  color: $ink;
  letter-spacing: 0.01em;
  padding-bottom: env(safe-area-inset-bottom, 0px);
}

// ====== NavBar ======
.m-nav {
  :deep(.van-nav-bar) {
    background: rgba(255, 255, 255, 0.92);
    backdrop-filter: saturate(180%) blur(14px);
    -webkit-backdrop-filter: saturate(180%) blur(14px);
    border-bottom: 1px solid $line;
  }
  :deep(.van-nav-bar__title) { max-width: 80%; }

  &__back {
    display: inline-flex; align-items: center; justify-content: center;
    width: 32px; height: 32px; color: $ink;
    border-radius: 8px;
    &:active { background: rgba(0, 0, 0, 0.05); }
  }
  &__title { display: flex; align-items: center; gap: 6px; }
  &__label { font-weight: 600; font-size: 16px; color: $ink; }
  &__no {
    font-family: 'JetBrains Mono', 'SF Mono', ui-monospace, monospace;
    font-size: 12px; color: $ink-3; padding: 2px 6px;
    background: #f1f3f7; border-radius: 4px;
  }
  &__pill {
    font-size: 11px; line-height: 1; padding: 3px 7px; border-radius: 999px;
    font-weight: 500;
    &--active { background: var(--color-primary-light); color: $brand-ink; }
    &--done   { background: rgba(56,158,13,.14); color: var(--color-success-text); }
    &--warn   { background: rgba(212,56,13,.12); color: $warn; }
    &--draft  { background: #eef0f4; color: $ink-2; }
    &--mute   { background: #eef0f4; color: $ink-3; }
  }
}

// ====== Loading ======
.m-loading { display: flex; justify-content: center; padding: 30vh 0; }

// ====== Strip (initiator) ======
.m-strip {
  display: flex; align-items: center; gap: 12px;
  margin: 12px 12px 0; padding: 14px 14px;
  background: $surface; border: 1px solid $line; border-radius: 12px;
  box-shadow: 0 1px 0 rgba(20, 30, 60, 0.02);

  &__avatar {
    width: 40px; height: 40px; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    background: linear-gradient(135deg, var(--color-primary) 0%, var(--color-primary-hover) 100%);
    color: #fff; font-weight: 600; font-size: 16px; flex: 0 0 40px;
    letter-spacing: 0;
  }
  &__main { flex: 1; min-width: 0; }
  &__name { font-size: 14px; font-weight: 600; color: $ink; display: flex; gap: 6px; align-items: center; }
  &__dot { color: $ink-3; }
  &__flow { color: $ink-2; font-weight: 500; }
  &__meta {
    margin-top: 4px; font-size: 12px; color: $ink-3;
    display: flex; gap: 10px; align-items: center;
  }
  &__title { color: $ink-2; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
}

// ====== Block (section) ======
.m-block {
  margin: 12px 12px 0;
  background: $surface;
  border: 1px solid $line;
  border-radius: 12px;
  padding: 8px 0 12px;

  &__head {
    display: flex; align-items: center; gap: 8px;
    padding: 12px 16px 6px;
    h3 { margin: 0; font-size: 14px; font-weight: 600; color: $ink; letter-spacing: 0.02em; }
  }
  &__bar {
    width: 3px; height: 14px; border-radius: 2px;
    background: $brand;
    &--accent { background: $ok; }
  }
  :deep(.van-cell-group--inset) { margin: 0 12px; }
}

.m-timeline-wrap { padding: 0 12px; }

// ====== Result ======
.m-result {
  margin: 12px 12px 0;
  padding: 16px;
  display: flex; gap: 12px; align-items: center;
  border-radius: 12px;
  background: linear-gradient(135deg, var(--color-success-light) 0%, #fafafa 100%);
  border: 1px solid #d9f7be;
  &__icon { color: $ok; }
  &__text {
    font-size: 14px; font-weight: 600; color: $ink;
    small { display: block; font-weight: 400; color: $ink-3; font-size: 12px; margin-top: 2px; }
  }
}

// ====== Action Bar (sticky bottom) ======
.m-bar {
  position: sticky;
  position: -webkit-sticky;
  bottom: 0;
  left: 0; right: 0;
  z-index: 30;
  background: rgba(255, 255, 255, 0.96);
  backdrop-filter: saturate(180%) blur(12px);
  -webkit-backdrop-filter: saturate(180%) blur(12px);
  border-top: 1px solid $line;
  padding: 10px 12px;
  transition: padding 200ms ease;

  // 让浮动叠加
  margin-top: -1px;

  &__row {
    display: flex; align-items: center; gap: 10px;
  }
  &__head {
    display: flex; align-items: center; justify-content: space-between;
    padding: 4px 4px 8px;
  }
  &__title {
    font-size: 13px; font-weight: 600; color: $ink;
    em { color: $warn; font-style: normal; margin-left: 2px; }
    &--warn { color: $warn; }
  }
  &__close {
    font-size: 13px; color: $ink-3; cursor: pointer;
    padding: 2px 6px;
    &:active { color: $brand; }
  }
  &__hint {
    padding: 2px 4px 8px;
    font-size: 12px;
    color: $ink-2;
  }
  &__textarea {
    border-radius: 10px;
    background: #f6f7fb;
    margin-bottom: 10px;
    :deep(.van-cell::after) { display: none; }
    :deep(.van-field__control) { font-size: 14px; line-height: 1.6; }
  }
  &__more {
    flex: 0 0 40px;
    height: 40px; width: 40px;
    border-radius: 10px;
    border: 1px solid $line;
    background: #fff;
    color: $ink-2;
    font-size: 18px; line-height: 1; letter-spacing: 1px;
    padding: 0 0 6px;
    cursor: pointer;
    &:active { background: #f1f3f7; }
  }

  // 退回态强调
  &--reject { box-shadow: 0 -8px 24px rgba(212,56,13,0.06); }
  &--approve { box-shadow: 0 -8px 24px rgba(47,84,235,0.08); }
}

// ====== Buttons ======
.m-btn {
  flex: 1;
  height: 44px;
  border-radius: 10px;
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.04em;
  transition: transform 100ms ease;

  &:active { transform: scale(0.98); }

  &--ghost {
    background: #fff;
    color: $ink-2;
    border: 1px solid $line;
  }
  &--primary {
    background: linear-gradient(180deg, var(--color-primary-hover) 0%, $brand 100%);
    color: #fff;
    border: none;
    box-shadow: 0 4px 12px rgba(47,84,235,0.22);
  }
  &--danger {
    background: linear-gradient(180deg, #ff5a3c 0%, var(--color-danger) 100%);
    color: #fff;
    border: none;
    box-shadow: 0 4px 12px rgba(212,56,13,0.22);
  }
}

// ====== Sheet inner ======
.m-sheet {
  padding: 12px 0 16px;
  &__row {
    display: flex; gap: 10px;
    margin: 12px 16px env(safe-area-inset-bottom, 0px);
  }
}

// ====== Misc ======
.m-scroll { min-height: calc(100vh - 46px); }
.m-bottom-spacer { width: 100%; }
</style>
