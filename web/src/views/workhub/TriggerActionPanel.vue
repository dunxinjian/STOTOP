<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  CloudUploadOutlined,
  PlusCircleOutlined,
  FormOutlined,
  FileAddOutlined,
  SendOutlined,
  AppstoreOutlined,
  UploadOutlined,
  EditOutlined,
  SolutionOutlined,
  AuditOutlined,
  FileTextOutlined,
  RocketOutlined,
} from '@ant-design/icons-vue'
import { getAvailableTriggerActions, type TriggerAction } from '@/api/workflow'
import { getAvailableFlows, createCard } from '@/api/cardflow'
import type { AvailableFlowDto } from '@/types/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'

// 展示项：包裹静态触发动作 + 动态 CardFlow 流程
interface DisplayItem {
  key: string
  label: string
  icon: string | null
  category: 'upload' | 'create' | 'apply'
  description: string | null
  // 静态触发动作走路由跳转
  route?: string
  // 来源标记：'static' = WF触发动作表；'cardflow' = CardFlow 动态流程
  source: 'static' | 'cardflow'
  // CardFlow 流程定义 ID（source=cardflow 时使用）
  flowId?: number
  flowCode?: string
}

const router = useRouter()
const orgStore = useOrgContextStore()
const staticActions = ref<TriggerAction[]>([])
const cardFlows = ref<AvailableFlowDto[]>([])
const loading = ref(false)
const startingFlowId = ref<number | null>(null)

const emit = defineEmits<{
  // CardFlow 流程卡片创建成功后通知父组件打开 CardFlowPanel
  (e: 'start-cardflow', payload: { cardId: number; flowName: string }): void
}>()

// 静态动作按 category 分组
const uploadActions = computed<DisplayItem[]>(() =>
  staticActions.value.filter(a => a.category === 'upload').map(toDisplay)
)
const applyActions = computed<DisplayItem[]>(() =>
  staticActions.value.filter(a => a.category === 'apply').map(toDisplay)
)
// create 分组：静态动作 + CardFlow 可发起流程
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
  }))
  return [...staticItems, ...flowItems]
})

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
const iconMap: Record<string, any> = {
  CloudUploadOutlined,
  PlusCircleOutlined,
  FormOutlined,
  FileAddOutlined,
  SendOutlined,
  AppstoreOutlined,
  UploadOutlined,
  EditOutlined,
  SolutionOutlined,
  AuditOutlined,
  FileTextOutlined,
  RocketOutlined,
}

// 分组默认图标
const categoryDefaultIcon: Record<string, any> = {
  upload: CloudUploadOutlined,
  create: PlusCircleOutlined,
  apply: FormOutlined,
}

const groupMeta: Record<DisplayItem['category'], { title: string; hint: string }> = {
  upload: { title: '数据上传', hint: '运单、报价、仓储数据入口' },
  create: { title: '高频流程', hint: '发起业务流程并进入审批' },
  apply: { title: '业务申请', hint: '付款、报销、协同申请' },
}

function getIcon(iconName: string | null, category: string) {
  if (iconName && iconMap[iconName]) return iconMap[iconName]
  return categoryDefaultIcon[category] || AppstoreOutlined
}

function getActionDescription(item: DisplayItem) {
  if (item.description) return item.description
  if (item.source === 'cardflow') return `发起「${item.label}」并提交业务信息`
  return groupMeta[item.category]?.hint || '进入对应业务操作'
}

async function handleAction(item: DisplayItem) {
  if (item.source === 'cardflow') {
    // CardFlow 流程：创建草稿卡 + 开面板
    if (startingFlowId.value !== null) return
    const flowId = item.flowId!
    startingFlowId.value = flowId
    try {
      const orgId = orgStore.currentOrgId ?? 0
      const draft = await createCard({ flowDefinitionId: flowId, orgId, dataJson: '{}' } as any)
      emit('start-cardflow', { cardId: draft.id, flowName: item.label })
    } catch (e: any) {
      message.error(e?.message || '发起流程失败')
    } finally {
      startingFlowId.value = null
    }
    return
  }
  // 静态触发动作：路由跳转
  if (item.route) router.push(item.route)
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

const hasAny = computed(
  () => uploadActions.value.length + createActions.value.length + applyActions.value.length > 0
)

onMounted(() => loadAll())
</script>

<template>
  <div class="trigger-action-panel">
    <div class="panel-header">
      <RocketOutlined class="panel-header__icon" />
      <span class="panel-header__title">我要发起</span>
      <span class="panel-header__hint">物流与仓配入口</span>
    </div>

    <div class="panel-body" v-if="!loading && hasAny">
      <!-- 上传组 -->
      <div v-if="uploadActions.length" class="action-group">
        <div class="group-title">
          <span>{{ groupMeta.upload.title }}</span>
          <small>{{ groupMeta.upload.hint }}</small>
        </div>
        <div class="action-list">
          <div
            v-for="action in uploadActions"
            :key="action.key"
            class="action-item"
            :title="action.description || action.label"
            @click="handleAction(action)"
          >
            <div class="action-icon-wrap">
              <component :is="getIcon(action.icon, action.category)" class="action-icon" />
            </div>
            <div class="action-copy">
              <span class="action-label">{{ action.label }}</span>
              <span class="action-desc">{{ getActionDescription(action) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 新建组（包含 CardFlow 可发起流程） -->
      <div v-if="createActions.length" class="action-group">
        <div class="group-title">
          <span>{{ groupMeta.create.title }}</span>
          <small>{{ groupMeta.create.hint }}</small>
        </div>
        <div class="action-list">
          <div
            v-for="action in createActions"
            :key="action.key"
            class="action-item"
            :class="{ 'action-item--cardflow': action.source === 'cardflow', 'action-item--loading': startingFlowId === action.flowId }"
            :title="action.description || action.label"
            @click="handleAction(action)"
          >
            <div class="action-icon-wrap">
              <component :is="getIcon(action.icon, action.category)" class="action-icon" />
            </div>
            <div class="action-copy">
              <span class="action-label">{{ action.label }}</span>
              <span class="action-desc">{{ getActionDescription(action) }}</span>
            </div>
            <span v-if="action.source === 'cardflow'" class="action-badge">流程</span>
          </div>
        </div>
      </div>

      <!-- 申请组 -->
      <div v-if="applyActions.length" class="action-group">
        <div class="group-title">
          <span>{{ groupMeta.apply.title }}</span>
          <small>{{ groupMeta.apply.hint }}</small>
        </div>
        <div class="action-list">
          <div
            v-for="action in applyActions"
            :key="action.key"
            class="action-item"
            :title="action.description || action.label"
            @click="handleAction(action)"
          >
            <div class="action-icon-wrap">
              <component :is="getIcon(action.icon, action.category)" class="action-icon" />
            </div>
            <div class="action-copy">
              <span class="action-label">{{ action.label }}</span>
              <span class="action-desc">{{ getActionDescription(action) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 加载态 -->
    <div v-else-if="loading" class="panel-body panel-loading">
      <a-spin size="small" />
    </div>

    <!-- 空态 -->
    <div v-else class="panel-body panel-empty">
      <span class="empty-text">暂无可发起流程，请联系管理员配置业务入口</span>
    </div>
  </div>
</template>

<style scoped>
.trigger-action-panel {
  height: 100%;
  border-bottom: 0;
  padding: 14px 12px 16px;
  overflow-y: auto;
  background: #f7f8fa;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 2px 12px;
}

.panel-header__icon {
  font-size: 15px;
  color: var(--color-info);
}

.panel-header__title {
  font-size: 15px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.86);
}

.panel-header__hint {
  margin-left: auto;
  font-size: 11px;
  color: rgba(0, 0, 0, 0.42);
}

.panel-body {
  padding: 0;
}

.panel-loading,
.panel-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 60px;
}

.empty-text {
  font-size: 12px;
  color: #999;
  line-height: 1.5;
  text-align: center;
  padding: 0 10px;
}

.action-group {
  margin-bottom: 16px;
}

.action-group:last-child {
  margin-bottom: 0;
}

.group-title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.62);
  margin-bottom: 8px;
  font-weight: 600;
}

.group-title small {
  font-size: 11px;
  color: rgba(0, 0, 0, 0.38);
  font-weight: 400;
}

.action-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.action-item {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 72px;
  padding: 10px 12px;
  border-radius: 8px;
  background: #fff;
  border: 1px solid rgba(18, 31, 53, 0.06);
  box-shadow: 0 1px 2px rgba(18, 31, 53, 0.04);
  cursor: pointer;
  transition: background-color 0.18s ease, border-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease;
  position: relative;
}

.action-item:hover {
  background-color: var(--color-primary-light);
  border-color: var(--color-primary-border);
  /* 阴影需透明度，用主色低透明 color-mix 取代固定橙 */
  box-shadow: 0 6px 14px color-mix(in srgb, var(--color-primary) 8%, transparent);
  transform: translateY(-1px);
}

.action-item:active {
  background-color: #fff1e8;
  transform: translateY(0);
}

.action-item--cardflow .action-icon-wrap {
  background: rgba(114, 46, 209, 0.08);
}

.action-item--cardflow .action-icon {
  color: var(--biz-waybill);
}

.action-item--loading {
  pointer-events: none;
  opacity: 0.5;
}

.action-badge {
  position: absolute;
  top: 8px;
  right: 8px;
  font-size: 9px;
  line-height: 12px;
  padding: 0 4px;
  border-radius: 6px;
  background: #f9f0ff;
  color: var(--biz-waybill);
  border: 1px solid #efdbff;
}

.action-icon-wrap {
  width: 34px;
  height: 34px;
  border-radius: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  background: var(--bg-muted);
}

.action-icon {
  font-size: 19px;
  color: var(--text-2);
}

.action-copy {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding-right: 18px;
}

.action-label {
  font-size: 13px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.82);
  text-align: left;
  line-height: 1.25;
  word-break: keep-all;
  overflow-wrap: anywhere;
  max-width: 100%;
}

.action-desc {
  color: rgba(0, 0, 0, 0.45);
  font-size: 11px;
  line-height: 1.35;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

@media (max-width: 1280px) {
  .trigger-action-panel {
    padding-inline: 8px;
  }

  .action-item {
    min-height: 64px;
  }
}
</style>
