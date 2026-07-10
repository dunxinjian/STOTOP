<template>
  <div class="initiate-page">
    <div class="ip-head">
      <h2 class="ip-title">发起流程</h2>
      <span class="ip-subtitle">选择一个业务入口，快速发起</span>
    </div>

    <div v-if="loading" class="ip-state"><a-spin /></div>

    <template v-else-if="hasAny">
      <section v-for="g in groups" :key="g.key" class="ip-group">
        <div class="ip-group-head">
          <span class="ip-group-title">{{ g.title }}</span>
          <span class="ip-group-hint">{{ g.hint }}</span>
        </div>
        <div class="ip-grid">
          <button
            v-for="action in g.items"
            :key="action.key"
            type="button"
            class="ip-card"
            :class="{ 'is-flow': action.source === 'cardflow', 'is-loading': startingFlowId === action.flowId }"
            :title="action.description || action.label"
            @click="handleAction(action)"
          >
            <span class="ip-card-icon"><component :is="getIcon(action.icon, action.category)" /></span>
            <span class="ip-card-copy">
              <span class="ip-card-label">{{ action.label }}</span>
              <span class="ip-card-desc">{{ getActionDescription(action) }}</span>
            </span>
            <span v-if="action.source === 'cardflow'" class="ip-card-badge">流程</span>
          </button>
        </div>
      </section>
    </template>

    <div v-else class="ip-state ip-empty">暂无可发起流程，请联系管理员配置业务入口</div>

    <CardFlowPanel
      v-model:visible="cardPanelVisible"
      :card-id="cardPanelCardId"
      mode="fill"
      @closed="handleCardFlowClosed"
      @submitted="handleCardFlowClosed"
      @saved="handleCardFlowClosed"
    />

    <a-modal
      v-model:open="onBehalfModalVisible"
      title="代谁发起"
      ok-text="发起"
      cancel-text="取消"
      @ok="confirmOnBehalfStart"
      @cancel="cancelOnBehalfStart"
    >
      <p class="ip-onbehalf-hint">留空则以本人身份发起「{{ onBehalfTargetItem?.label }}」；选择后将代其发起并留痕。</p>
      <UserSelect v-model="onBehalfUser" placeholder="选择被代理人（可选，留空=本人）" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  CloudUploadOutlined, PlusCircleOutlined, FormOutlined, FileAddOutlined,
  SendOutlined, AppstoreOutlined, UploadOutlined, EditOutlined,
  SolutionOutlined, AuditOutlined, FileTextOutlined,
} from '@ant-design/icons-vue'
import { getAvailableTriggerActions, type TriggerAction } from '@/api/workflow'
import { getAvailableFlows, createCard } from '@/api/cardflow'
import type { AvailableFlowDto, UserFieldValue } from '@/types/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'
import CardFlowPanel from '@/components/cardflow/CardFlowPanel.vue'
import UserSelect from '@/components/cardflow/fields/UserSelect.vue'

// 展示项：包裹静态触发动作 + 动态 CardFlow 流程
interface DisplayItem {
  key: string
  label: string
  icon: string | null
  category: 'upload' | 'create' | 'apply'
  description: string | null
  route?: string
  source: 'static' | 'cardflow'
  flowId?: number
  flowCode?: string
  /** 当前用户对该流程是否可代提交（M8-A 件③） */
  onBehalfEnabled?: boolean
}

const router = useRouter()
const orgStore = useOrgContextStore()
const staticActions = ref<TriggerAction[]>([])
const cardFlows = ref<AvailableFlowDto[]>([])
const loading = ref(false)
const startingFlowId = ref<number | null>(null)

// CardFlow 填写面板
const cardPanelVisible = ref(false)
const cardPanelCardId = ref<number | null>(null)

// 代提交（M8-A 件③）：onBehalfEnabled 的流程发起前先弹窗选被代理人，留空=本人发起
const onBehalfModalVisible = ref(false)
const onBehalfTargetItem = ref<DisplayItem | null>(null)
const onBehalfUser = ref<UserFieldValue | null>(null)

const uploadActions = computed<DisplayItem[]>(() =>
  staticActions.value.filter(a => a.category === 'upload').map(toDisplay)
)
const applyActions = computed<DisplayItem[]>(() =>
  staticActions.value.filter(a => a.category === 'apply').map(toDisplay)
)
const createActions = computed<DisplayItem[]>(() => {
  const staticItems = staticActions.value.filter(a => a.category === 'create').map(toDisplay)
  const flowItems: DisplayItem[] = cardFlows.value.map(f => ({
    key: `cardflow:${f.id}`,
    label: f.flowName,
    icon: 'FileAddOutlined',
    category: 'create',
    description: f.description ?? `发起「${f.flowName}」卡片流程`,
    source: 'cardflow',
    flowId: f.id,
    flowCode: f.flowCode,
    onBehalfEnabled: f.onBehalfEnabled,
  }))
  return [...staticItems, ...flowItems]
})

const groupHints: Record<DisplayItem['category'], string> = {
  upload: '运单、报价、仓储数据入口',
  create: '发起业务流程并进入审批',
  apply: '付款、报销、协同申请',
}

const groups = computed(() =>
  [
    { key: 'upload', title: '数据上传', hint: groupHints.upload, items: uploadActions.value },
    { key: 'create', title: '高频流程', hint: groupHints.create, items: createActions.value },
    { key: 'apply', title: '业务申请', hint: groupHints.apply, items: applyActions.value },
  ].filter(g => g.items.length)
)

function toDisplay(a: TriggerAction): DisplayItem {
  return {
    key: a.key,
    label: a.label,
    icon: a.icon,
    category: a.category,
    description: a.description,
    route: a.route,
    source: 'static',
  }
}

// 图标映射
const iconMap: Record<string, unknown> = {
  CloudUploadOutlined, PlusCircleOutlined, FormOutlined, FileAddOutlined, SendOutlined,
  AppstoreOutlined, UploadOutlined, EditOutlined, SolutionOutlined, AuditOutlined, FileTextOutlined,
}
const categoryDefaultIcon: Record<string, unknown> = {
  upload: CloudUploadOutlined,
  create: PlusCircleOutlined,
  apply: FormOutlined,
}

function getIcon(iconName: string | null, category: string) {
  if (iconName && iconMap[iconName]) return iconMap[iconName]
  return categoryDefaultIcon[category] || AppstoreOutlined
}

function getActionDescription(item: DisplayItem) {
  if (item.description) return item.description
  if (item.source === 'cardflow') return `发起「${item.label}」并提交业务信息`
  return groupHints[item.category] || '进入对应业务操作'
}

async function handleAction(item: DisplayItem) {
  if (item.source === 'cardflow') {
    if (startingFlowId.value !== null) return
    if (item.onBehalfEnabled) {
      onBehalfTargetItem.value = item
      onBehalfUser.value = null
      onBehalfModalVisible.value = true
      return
    }
    await startFlow(item, null)
    return
  }
  if (item.route) router.push(item.route)
}

async function startFlow(item: DisplayItem, actualInitiatorId: number | null) {
  const flowId = item.flowId as number
  startingFlowId.value = flowId
  try {
    const orgId = orgStore.currentOrgId ?? 0
    const draft = await createCard({
      flowDefinitionId: flowId,
      orgId,
      dataJson: '{}',
      actualInitiatorId: actualInitiatorId ?? undefined,
    })
    cardPanelCardId.value = draft.id
    cardPanelVisible.value = true
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : '发起流程失败')
  } finally {
    startingFlowId.value = null
  }
}

function confirmOnBehalfStart() {
  const item = onBehalfTargetItem.value
  const agentUserId = onBehalfUser.value?.id ?? null
  onBehalfModalVisible.value = false
  onBehalfTargetItem.value = null
  if (!item) return
  void startFlow(item, agentUserId)
}

function cancelOnBehalfStart() {
  onBehalfModalVisible.value = false
  onBehalfTargetItem.value = null
}

function handleCardFlowClosed() {
  cardPanelCardId.value = null
}

async function loadAll() {
  loading.value = true
  try {
    const orgId = orgStore.currentOrgId ?? 0
    const [actionsRes, flowsRes] = await Promise.all([
      getAvailableTriggerActions().catch(() => [] as TriggerAction[]),
      getAvailableFlows(orgId).catch(() => [] as AvailableFlowDto[]),
    ])
    staticActions.value = actionsRes || []
    cardFlows.value = flowsRes || []
  } finally {
    loading.value = false
  }
}

const hasAny = computed(() => groups.value.length > 0)

onMounted(() => loadAll())
</script>

<style scoped lang="scss">
.initiate-page {
  height: 100%;
  overflow-y: auto;
  padding: 16px 20px 24px;
  background: var(--bg-muted);
}

.ip-head {
  margin-bottom: 16px;
}

.ip-title {
  margin: 0 0 2px;
  font-size: 18px;
  font-weight: 600;
  color: var(--text-1);
}

.ip-subtitle {
  font-size: 12px;
  color: var(--text-3);
}

.ip-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 160px;
}

.ip-empty {
  color: var(--text-3);
  font-size: 13px;
}

.ip-group {
  margin-bottom: 22px;
}

.ip-group-head {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 10px;
}

.ip-group-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-1);
}

.ip-group-hint {
  font-size: 11px;
  color: var(--text-3);
}

.ip-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.ip-card {
  position: relative;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px;
  min-height: 64px;
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  background: var(--bg-card);
  cursor: pointer;
  text-align: left;
  transition: background 0.18s, border-color 0.18s, box-shadow 0.18s, transform 0.18s;

  &:hover {
    border-color: var(--color-primary-border);
    box-shadow: var(--shadow-md);
    transform: translateY(-1px);
  }

  &.is-loading {
    pointer-events: none;
    opacity: 0.5;
  }
}

.ip-card-icon {
  width: 38px;
  height: 38px;
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-md);
  background: var(--bg-muted);
  color: var(--text-2);
  font-size: 19px;
}

.ip-card.is-flow .ip-card-icon {
  background: var(--color-primary-light);
  color: var(--color-primary);
}

.ip-card-copy {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding-right: 16px;
}

.ip-card-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-1);
  line-height: 1.25;
}

.ip-card-desc {
  font-size: 11px;
  color: var(--text-3);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.ip-card-badge {
  position: absolute;
  top: 8px;
  right: 8px;
  font-size: 9px;
  line-height: 14px;
  padding: 0 5px;
  border-radius: var(--radius-sm);
  background: var(--color-primary-light);
  color: var(--color-primary);
}

.ip-onbehalf-hint {
  margin: 0 0 10px;
  font-size: 12px;
  color: var(--text-3);
}
</style>
