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

import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardDetailTable, { type DetailRow } from '@/components/cardflow/CardDetailTable.vue'
import CardRelationPicker from '@/components/cardflow/CardRelationPicker.vue'

import {
  getCard,
  updateCard,
  submitCard,
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

const isDraft = computed(() => card.value?.status === 'draft' || card.value?.status === 'returned')

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

function buildDetailRows(): DetailRow[] {
  if (!card.value?.details?.length) return []
  return card.value.details.map((d, idx) => {
    let parsed: Record<string, any> = {}
    try {
      parsed = d.dataJson ? JSON.parse(d.dataJson) : {}
    } catch {
      parsed = {}
    }
    return {
      _id: String(d.id || `row_${idx}`),
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
      } catch {
        cardSchema.value = []
        detailSchema.value = []
        flowSettings.value = {}
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
          showToast({ message: '已恢复离线缓存', position: 'bottom' })
        }
      } catch {
        // ignore
      }
    }
    formData.value = parsedData

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

function buildPersistData(): string {
  const payload = {
    ...formData.value,
    __details: detailRows.value.map(r => ({ ...r })),
  }
  return JSON.stringify(payload)
}

function cacheOffline() {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(
      lsKey.value,
      JSON.stringify({
        formData: formData.value,
        detailRows: detailRows.value,
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
    const dataJson = buildPersistData()
    const res = await updateCard(cardId.value, {
      dataJson,
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
  [formData, detailRows],
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
    const dataJson = buildPersistData()
    const res = await updateCard(cardId.value, {
      dataJson,
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
    const dataJson = buildPersistData()
    const updRes = await updateCard(cardId.value, {
      dataJson,
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
              <VanTag :type="isDraft ? 'primary' : 'default'" size="medium">
                {{ isDraft ? '草稿' : (card.status || '-') }}
              </VanTag>
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
            mode="edit"
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
            mode="edit"
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

        <div class="bottom-spacer" />
      </template>

      <VanEmpty v-else description="未找到卡片信息" />
    </VanPullRefresh>

    <!-- 底部操作栏 -->
    <VanActionBar v-if="!loading && card">
      <VanActionBarButton
        type="warning"
        text="暂存草稿"
        :loading="submitting"
        :disabled="!isDraft"
        @click="handleSaveDraft"
      />
      <VanActionBarButton
        type="danger"
        text="提交审批"
        :loading="submitting"
        :disabled="!isDraft"
        @click="handleSubmit"
      />
    </VanActionBar>

    <!-- 关联选择器 -->
    <CardRelationPicker
      v-if="cardId"
      :card-id="cardId"
      v-model:show="showRelationPicker"
      @select="onRelationSelect"
    />
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
</style>
