<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { CardDetailDto, ActionLogDto, SchemaFieldDefinition, CardRelationDto, CardBalanceDto } from '@/types/cardflow'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  Tag as VanTag,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  Empty as VanEmpty,
} from 'vant'
import { showToast } from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/loading/style'
import 'vant/es/tag/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/empty/style'
import 'vant/es/toast/style'
import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardTimeline from '@/components/cardflow/CardTimeline.vue'
import { getCard, getCardLogs, getCardRelations, getCardBalance } from '@/api/cardflow'

const route = useRoute()
const router = useRouter()
const cardId = computed(() => Number(route.params.cardId))

const loading = ref(true)
const card = ref<CardDetailDto | null>(null)
const logs = ref<ActionLogDto[]>([])
const schema = ref<SchemaFieldDefinition[]>([])
const cardData = ref<Record<string, any>>({})
const relations = ref<CardRelationDto[]>([])
const balances = ref<CardBalanceDto[]>([])
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

function formatAmount(val: any): string {
  const num = Number(val)
  if (isNaN(num)) return '-'
  return num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatTime(time?: string | null): string {
  if (!time) return '-'
  const d = new Date(time)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function loadData() {
  loading.value = true
  try {
    const [cardRes, logsRes, relRes, balRes] = await Promise.all([
      getCard(cardId.value),
      getCardLogs(cardId.value),
      getCardRelations(cardId.value).catch(() => [] as CardRelationDto[]),
      getCardBalance(cardId.value).catch(() => [] as CardBalanceDto[]),
    ])

    card.value = cardRes
    logs.value = logsRes || []
    relations.value = relRes || []
    balances.value = balRes || []

    // 解析 card data
    if (cardRes.dataJson) {
      try { cardData.value = JSON.parse(cardRes.dataJson) } catch { cardData.value = {} }
    }

    // Schema 简化处理
    schema.value = []
  } catch {
    showToast({ message: '加载失败', type: 'fail' })
  } finally {
    loading.value = false
  }
}

function onClickLeft() {
  router.back()
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="card-detail-view">
    <VanNavBar title="卡片详情" left-arrow @click-left="onClickLeft" fixed placeholder />

    <div v-if="loading" class="loading-wrap">
      <VanLoading size="36px" vertical>加载中...</VanLoading>
    </div>

    <template v-else-if="card">
      <!-- 基本信息 -->
      <VanCellGroup inset class="info-card">
        <VanCell :title="card.title || card.cardNumber || '卡片详情'" center>
          <template #right-icon>
            <VanTag :type="statusInfo.type" size="large">{{ statusInfo.label }}</VanTag>
          </template>
        </VanCell>
        <VanCell title="流程" :value="card.flowName" />
        <VanCell title="编号" :value="card.cardNumber || '-'" />
        <VanCell title="发起人" :value="card.initiatorName" />
        <VanCell title="提交时间" :value="formatTime(card.submitTime)" />
        <VanCell v-if="card.completedTime" title="完成时间" :value="formatTime(card.completedTime)" />
      </VanCellGroup>

      <!-- 卡片数据展示 -->
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

      <!-- 明细行 -->
      <VanCellGroup inset title="明细行" class="detail-section" v-if="runtimeComponents.length === 0 && card.details && card.details.length > 0">
        <VanCell
          v-for="(row, idx) in card.details"
          :key="row.id"
          :title="`第 ${idx + 1} 行`"
          :label="row.dataJson ? '点击查看' : '无数据'"
        />
      </VanCellGroup>

      <!-- 关联卡片 -->
      <VanCellGroup inset title="关联卡片" class="relation-section" v-if="relations.length > 0">
        <VanCell
          v-for="rel in relations"
          :key="rel.id"
          :title="rel.targetCardNumber || `卡片#${rel.targetCardId}`"
          :value="rel.relationType"
          :label="rel.description || undefined"
          is-link
          @click="router.push(`/m/cardflow/detail/${rel.targetCardId}`)"
        />
      </VanCellGroup>

      <!-- 余额信息 -->
      <VanCellGroup inset title="余额信息" class="balance-section" v-if="balances.length > 0">
        <VanCell
          v-for="bal in balances"
          :key="bal.id"
          :title="bal.cardTitle || bal.cardNumber || '-'"
        >
          <template #value>
            <div class="balance-info">
              <span>原始: ¥{{ formatAmount(bal.originalAmount) }}</span>
              <span>剩余: <b style="color: var(--color-success);">¥{{ formatAmount(bal.remainingAmount) }}</b></span>
            </div>
          </template>
        </VanCell>
      </VanCellGroup>

      <!-- 流转时间线 -->
      <VanCellGroup inset title="流转记录" class="timeline-section">
        <CardTimeline :stages="(logs as any)" mode="compact" />
      </VanCellGroup>

      <div class="bottom-spacer" />
    </template>

    <VanEmpty v-else description="未找到卡片信息" />
  </div>
</template>

<style scoped>
.card-detail-view {
  min-height: 100vh;
  background: #f7f8fa;
  padding-bottom: env(safe-area-inset-bottom);
}
.loading-wrap {
  display: flex;
  justify-content: center;
  padding-top: 30vh;
}
.info-card {
  margin-top: 12px !important;
}
.data-section {
  margin-top: 12px !important;
}
.detail-section {
  margin-top: 12px !important;
}
.relation-section {
  margin-top: 12px !important;
}
.balance-section {
  margin-top: 12px !important;
}
.timeline-section {
  margin-top: 12px !important;
}
.bottom-spacer {
  height: 24px;
}
.balance-info {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  font-size: 12px;
  gap: 2px;
}
</style>
