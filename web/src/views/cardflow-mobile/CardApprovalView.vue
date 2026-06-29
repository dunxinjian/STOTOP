<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { CardDetailDto, ActionLogDto, SchemaFieldDefinition } from '@/types/cardflow'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  Tag as VanTag,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  Empty as VanEmpty,
  ActionBar as VanActionBar,
  ActionBarButton as VanActionBarButton,
  Field as VanField,
  Popup as VanPopup,
  Button as VanButton,
} from 'vant'
import { showToast } from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/loading/style'
import 'vant/es/tag/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/empty/style'
import 'vant/es/action-bar/style'
import 'vant/es/action-bar-button/style'
import 'vant/es/field/style'
import 'vant/es/popup/style'
import 'vant/es/button/style'
import 'vant/es/toast/style'
import 'vant/es/dialog/style'
import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardTimeline from '@/components/cardflow/CardTimeline.vue'
import StageInputFields from '@/components/cardflow/StageInputFields.vue'
import { getCard, getCardLogs, approveCard, rejectCard, transferCard, countersignCard } from '@/api/cardflow'
import { useCardFlowStore } from '@/stores/cardflow'

const route = useRoute()
const router = useRouter()
const store = useCardFlowStore()
const cardId = computed(() => Number(route.params.cardId))

const loading = ref(true)
const submitting = ref(false)
const card = ref<CardDetailDto | null>(null)
const logs = ref<ActionLogDto[]>([])
const schema = ref<SchemaFieldDefinition[]>([])
const cardData = ref<Record<string, any>>({})

// 操作弹窗
const showRejectPopup = ref(false)
const showTransferPopup = ref(false)
const showCountersignPopup = ref(false)
const opinionText = ref('')
const transferUserId = ref<number | undefined>(undefined)
const countersignUserId = ref<number | undefined>(undefined)

// 节点附加字段
const stageFields = ref<SchemaFieldDefinition[]>([])
const stageFieldData = ref<Record<string, any>>({})
const runtimeComponents = computed(() => card.value?.currentStageWorkView?.components ?? [])

const statusMap: Record<string, { label: string; type: 'primary' | 'success' | 'danger' | 'warning' | 'default' }> = {
  draft: { label: '草稿', type: 'default' },
  active: { label: '审批中', type: 'primary' },
  returned: { label: '已退回', type: 'danger' },
  completed: { label: '已完成', type: 'success' },
  voided: { label: '已作废', type: 'danger' },
}

const statusInfo = computed(() => {
  if (!card.value) return { label: '-', type: 'default' as const }
  return statusMap[card.value.status] || { label: card.value.status, type: 'default' as const }
})

// 判断当前用户是否可审批（简化逻辑）
const canApprove = computed(() => {
  return card.value?.status === 'active'
})

async function loadData() {
  loading.value = true
  try {
    const [cardRes, logsRes] = await Promise.all([
      getCard(cardId.value),
      getCardLogs(cardId.value),
    ])
    card.value = cardRes
    logs.value = logsRes || []

    // 解析 card data
    if (cardRes.dataJson) {
      try { cardData.value = JSON.parse(cardRes.dataJson) } catch { cardData.value = {} }
    }

    // 解析 schema（从版本中获取，这里简化处理）
    // 实际场景可从 flowVersion 的 cardSchemaJson 获取
    schema.value = []

    // 解析当前节点的 inputFields
    if (cardRes.currentStageInstanceId) {
      const currentStage = cardRes.stageInstances.find(s => s.id === cardRes.currentStageInstanceId)
      if (currentStage) {
        // 如果有阶段配置的附加字段
        stageFields.value = []
      }
    }
  } catch {
    showToast({ message: '加载失败', type: 'fail' })
  } finally {
    loading.value = false
  }
}

async function handleApprove() {
  submitting.value = true
  try {
    const result = await approveCard(cardId.value, {
      opinion: opinionText.value || undefined,
      supplementData: Object.keys(stageFieldData.value).length ? stageFieldData.value : undefined,
      concurrencyStamp: card.value?.concurrencyStamp || undefined,
    })
    if (result.success) {
      showToast({ message: '审批通过', type: 'success' })
      store.refreshTodoCount()
      await loadData()
    } else {
      showToast({ message: result.message || '操作失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '操作失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function handleReject() {
  if (!opinionText.value.trim()) {
    showToast('请填写驳回意见')
    return
  }
  submitting.value = true
  try {
    const result = await rejectCard(cardId.value, {
      opinion: opinionText.value,
      concurrencyStamp: card.value?.concurrencyStamp || undefined,
    })
    if (result.success) {
      showToast({ message: '已驳回', type: 'success' })
      showRejectPopup.value = false
      opinionText.value = ''
      store.refreshTodoCount()
      await loadData()
    } else {
      showToast({ message: result.message || '操作失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '操作失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function handleTransfer() {
  if (!transferUserId.value) {
    showToast('请选择转办人')
    return
  }
  submitting.value = true
  try {
    const result = await transferCard(cardId.value, {
      newUserId: transferUserId.value,
      opinion: opinionText.value || undefined,
    })
    if (result.success) {
      showToast({ message: '转办成功', type: 'success' })
      showTransferPopup.value = false
      opinionText.value = ''
      store.refreshTodoCount()
      await loadData()
    } else {
      showToast({ message: result.message || '操作失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '操作失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function handleCountersign() {
  if (!countersignUserId.value) {
    showToast('请选择加签人')
    return
  }
  submitting.value = true
  try {
    const result = await countersignCard(cardId.value, {
      userId: countersignUserId.value,
      opinion: opinionText.value || undefined,
    })
    if (result.success) {
      showToast({ message: '加签成功', type: 'success' })
      showCountersignPopup.value = false
      opinionText.value = ''
      await loadData()
    } else {
      showToast({ message: result.message || '操作失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '操作失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

function openRejectDialog() {
  opinionText.value = ''
  showRejectPopup.value = true
}

function openTransferDialog() {
  opinionText.value = ''
  transferUserId.value = undefined
  showTransferPopup.value = true
}

function openCountersignDialog() {
  opinionText.value = ''
  countersignUserId.value = undefined
  showCountersignPopup.value = true
}

function onClickLeft() {
  router.back()
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="card-approval-view">
    <VanNavBar title="审批" left-arrow @click-left="onClickLeft" fixed placeholder />

    <div v-if="loading" class="loading-wrap">
      <VanLoading size="36px" vertical>加载中...</VanLoading>
    </div>

    <template v-else-if="card">
      <!-- 卡片标题 + 状态 -->
      <VanCellGroup inset class="card-header">
        <VanCell :title="card.title || card.cardNumber || '卡片详情'" center>
          <template #right-icon>
            <VanTag :type="statusInfo.type" size="large">{{ statusInfo.label }}</VanTag>
          </template>
        </VanCell>
        <VanCell title="流程" :value="card.flowName" />
        <VanCell title="编号" :value="card.cardNumber || '-'" />
        <VanCell title="发起人" :value="card.initiatorName" />
        <VanCell title="提交时间" :value="card.submitTime || '-'" />
      </VanCellGroup>

      <!-- 卡片数据展示（只读） -->
      <div class="data-section" v-if="schema.length > 0 || runtimeComponents.length > 0">
        <SchemaRenderer
          :schema="schema"
          :components="runtimeComponents"
          :model-value="cardData"
          mode="view"
          platform="mobile"
        />
      </div>
      <VanCellGroup inset class="data-section" v-else-if="Object.keys(cardData).length > 0">
        <VanCell
          v-for="(value, key) in cardData"
          :key="String(key)"
          :title="String(key)"
          :value="String(value ?? '-')"
        />
      </VanCellGroup>

      <!-- 节点附加字段 -->
      <StageInputFields
        v-if="canApprove && stageFields.length > 0"
        :fields="stageFields"
        :model-value="stageFieldData"
        platform="mobile"
        @update:model-value="stageFieldData = $event"
      />

      <!-- 审批意见（快捷输入） -->
      <VanCellGroup inset class="opinion-section" v-if="canApprove">
        <VanField
          v-model="opinionText"
          label="审批意见"
          type="textarea"
          rows="2"
          autosize
          placeholder="请输入审批意见（可选）"
        />
      </VanCellGroup>

      <!-- 流转时间线 -->
      <VanCellGroup inset title="流转记录" class="timeline-section">
        <CardTimeline :stages="(logs as any)" mode="compact" />
      </VanCellGroup>

      <!-- 底部安全间距 -->
      <div class="bottom-spacer" />

      <!-- 底部操作栏 -->
      <VanActionBar v-if="canApprove">
        <VanActionBarButton type="default" text="转办" @click="openTransferDialog" />
        <VanActionBarButton type="default" text="加签" @click="openCountersignDialog" />
        <VanActionBarButton type="warning" text="驳回" @click="openRejectDialog" />
        <VanActionBarButton type="danger" text="同意" :loading="submitting" @click="handleApprove" />
      </VanActionBar>
    </template>

    <VanEmpty v-else description="未找到卡片信息" />

    <!-- 驳回弹窗 -->
    <VanPopup v-model:show="showRejectPopup" position="bottom" round :style="{ padding: '16px' }">
      <div class="popup-title">驳回意见</div>
      <VanField v-model="opinionText" type="textarea" rows="3" autosize placeholder="请输入驳回原因（必填）" />
      <div class="popup-actions">
        <VanButton block type="danger" :loading="submitting" @click="handleReject">确认驳回</VanButton>
      </div>
    </VanPopup>

    <!-- 转办弹窗 -->
    <VanPopup v-model:show="showTransferPopup" position="bottom" round :style="{ padding: '16px' }">
      <div class="popup-title">转办</div>
      <VanField v-model.number="transferUserId" label="转办人ID" type="number" placeholder="请输入用户ID" />
      <VanField v-model="opinionText" type="textarea" rows="2" autosize placeholder="转办说明（可选）" />
      <div class="popup-actions">
        <VanButton block type="primary" :loading="submitting" @click="handleTransfer">确认转办</VanButton>
      </div>
    </VanPopup>

    <!-- 加签弹窗 -->
    <VanPopup v-model:show="showCountersignPopup" position="bottom" round :style="{ padding: '16px' }">
      <div class="popup-title">加签</div>
      <VanField v-model.number="countersignUserId" label="加签人ID" type="number" placeholder="请输入用户ID" />
      <VanField v-model="opinionText" type="textarea" rows="2" autosize placeholder="加签说明（可选）" />
      <div class="popup-actions">
        <VanButton block type="primary" :loading="submitting" @click="handleCountersign">确认加签</VanButton>
      </div>
    </VanPopup>
  </div>
</template>

<style scoped>
.card-approval-view {
  min-height: 100vh;
  background: #f7f8fa;
  padding-bottom: env(safe-area-inset-bottom);
}
.loading-wrap {
  display: flex;
  justify-content: center;
  padding-top: 30vh;
}
.card-header {
  margin-top: 12px !important;
}
.data-section {
  margin-top: 12px !important;
}
.opinion-section {
  margin-top: 12px !important;
}
.timeline-section {
  margin-top: 12px !important;
}
.bottom-spacer {
  height: 60px;
}
.popup-title {
  font-size: 16px;
  font-weight: 600;
  color: #323233;
  margin-bottom: 12px;
  text-align: center;
}
.popup-actions {
  margin-top: 16px;
}
</style>
