<script setup lang="ts">
/**
 * OrchestrationInstanceDetailPage.vue — 编排实例详情
 *
 * 展示：
 *  - 基本信息
 *  - 节点实例进度表
 *  - 派发记录时间线
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  ArrowLeftOutlined,
  ReloadOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  StopOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getOrchestrationInstance,
  getOrchestrationInstanceDispatches,
  pauseOrchestrationInstance,
  resumeOrchestrationInstance,
  cancelOrchestrationInstance,
} from '@/api/orchestration'
import type {
  OrchestrationInstanceDetail,
  OrchestrationInstanceStatus,
  OrchestrationNodeInstance,
  OrchestrationNodeInstanceStatus,
  DagNode,
  DispatchRecord,
  DispatchStatus,
} from '@/types/orchestration'

const route = useRoute()
const router = useRouter()
const instanceId = computed(() => Number(route.params.id))

// ==================== 状态映射 ====================

interface StatusMeta { text: string; color: string }

const INSTANCE_STATUS_META: Record<OrchestrationInstanceStatus, StatusMeta> = {
  running: { text: '运行中', color: 'var(--color-info)' },
  completed: { text: '已完成', color: 'var(--color-success)' },
  terminated: { text: '已终止', color: 'var(--text-3)' },
  failed: { text: '失败', color: 'var(--color-danger)' },
  cancelled: { text: '已取消', color: 'var(--text-3)' },
  paused: { text: '已暂停', color: 'var(--color-warning)' },
}

const NODE_STATUS_META: Record<OrchestrationNodeInstanceStatus, StatusMeta> = {
  pending: { text: '待执行', color: 'var(--text-3)' },
  running: { text: '运行中', color: 'var(--color-info)' },
  completed: { text: '已完成', color: 'var(--color-success)' },
  skipped: { text: '已跳过', color: 'var(--color-warning)' },
  failed: { text: '失败', color: 'var(--color-danger)' },
}

const DISPATCH_STATUS_META: Record<DispatchStatus, StatusMeta> = {
  pending: { text: '待派发', color: 'var(--text-3)' },
  triggered: { text: '已触发', color: 'var(--color-success)' },
  skipped: { text: '已跳过', color: 'var(--color-warning)' },
  failed: { text: '失败', color: 'var(--color-danger)' },
}

const DISPATCH_TIMELINE_COLORS: Record<DispatchStatus, string> = {
  pending: 'gray',
  triggered: 'green',
  skipped: 'orange',
  failed: 'red',
}

// ==================== 数据 ====================

const loading = ref(false)
const detail = ref<OrchestrationInstanceDetail | null>(null)
const dispatches = ref<DispatchRecord[]>([])

const snapshotNodes = computed<DagNode[]>(() => {
  if (!detail.value?.snapshotNodesJson) return []
  try {
    const v = JSON.parse(detail.value.snapshotNodesJson)
    return Array.isArray(v) ? v : []
  } catch {
    return []
  }
})

const nodeNameMap = computed(() => {
  const m = new Map<string, string>()
  snapshotNodes.value.forEach(n => m.set(n.id, n.name || n.id))
  return m
})

// ==================== 节点表格列 ====================

const nodeColumns: TableColumnsType = [
  { title: '节点 ID', dataIndex: 'nodeId', key: 'nodeId', width: 160 },
  { title: '节点名称', key: 'nodeName', width: 200, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 120, align: 'center' },
  { title: '关联卡片', key: 'related', width: 180 },
  { title: '终态类型', dataIndex: 'endStatusType', key: 'endStatusType', width: 140 },
  { title: '开始时间', dataIndex: 'startTime', key: 'startTime', width: 170 },
  { title: '完成时间', dataIndex: 'completedTime', key: 'completedTime', width: 170 },
]

// ==================== 加载 ====================

async function loadAll() {
  loading.value = true
  try {
    const [det, disp] = await Promise.all([
      getOrchestrationInstance(instanceId.value),
      getOrchestrationInstanceDispatches(instanceId.value).catch(() => [] as DispatchRecord[]),
    ])
    detail.value = det
    dispatches.value = (disp as DispatchRecord[]) || []
  } catch (err: any) {
    message.error(err?.response?.data?.message || '加载实例详情失败')
  } finally {
    loading.value = false
  }
}

// ==================== 实例操作 ====================

async function handlePause() {
  if (!detail.value) return
  try {
    await pauseOrchestrationInstance(detail.value.id)
    message.success('已暂停')
    loadAll()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '暂停失败')
  }
}

async function handleResume() {
  if (!detail.value) return
  try {
    await resumeOrchestrationInstance(detail.value.id)
    message.success('已恢复')
    loadAll()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '恢复失败')
  }
}

function handleCancel() {
  if (!detail.value) return
  Modal.confirm({
    title: `取消实例 #${detail.value.id}？`,
    content: '取消后实例不再执行任何派发。',
    okText: '取消实例',
    okType: 'danger',
    async onOk() {
      try {
        await cancelOrchestrationInstance(detail.value!.id)
        message.success('已取消')
        loadAll()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '取消失败')
      }
    },
  })
}

function goBack() {
  router.push({ path: '/cardflow/orchestration-instances' })
}

function formatTime(val?: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN')
}

function nodeStatusOf(n: OrchestrationNodeInstance): OrchestrationNodeInstanceStatus {
  return n.status || 'pending'
}

function dispatchStatusOf(d: DispatchRecord): DispatchStatus {
  return d.status || 'pending'
}

function relatedDesc(n: OrchestrationNodeInstance) {
  if (n.relatedCardId) return `卡片 #${n.relatedCardId}`
  if (n.relatedBatchId) return `批次 #${n.relatedBatchId}`
  return '-'
}

onMounted(loadAll)
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #left>
        <a-button type="text" size="small" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
          返回列表
        </a-button>
      </template>

      <template #actions>
        <a-space :size="8">
          <a-button @click="loadAll">
            <template #icon><ReloadOutlined /></template>
            刷新
          </a-button>
          <a-button
            v-if="detail?.status === 'running'"
            @click="handlePause"
          >
            <template #icon><PauseCircleOutlined /></template>
            暂停
          </a-button>
          <a-button
            v-if="detail?.status === 'paused'"
            type="primary"
            @click="handleResume"
          >
            <template #icon><PlayCircleOutlined /></template>
            恢复
          </a-button>
          <a-button
            v-if="detail && !['completed','cancelled','terminated','failed'].includes(detail.status)"
            danger
            @click="handleCancel"
          >
            <template #icon><StopOutlined /></template>
            取消
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 基本信息 -->
      <a-card v-if="detail" class="info-card" :bordered="false">
        <a-descriptions :column="3" size="small">
          <a-descriptions-item label="实例 ID">#{{ detail.id }}</a-descriptions-item>
          <a-descriptions-item label="编排模板">
            {{ detail.templateName || `#${detail.templateId}` }}
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag class="status-tag" :color="INSTANCE_STATUS_META[detail.status]?.color">
              {{ INSTANCE_STATUS_META[detail.status]?.text || detail.status }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="发起人 ID">{{ detail.initiatorId }}</a-descriptions-item>
          <a-descriptions-item label="发起时间">{{ formatTime(detail.initiatedTime) }}</a-descriptions-item>
          <a-descriptions-item label="完成时间">{{ formatTime(detail.completedTime) }}</a-descriptions-item>
          <a-descriptions-item label="触发次数">{{ detail.triggerCount }}</a-descriptions-item>
          <a-descriptions-item label="完成原因">{{ detail.completionReason || '-' }}</a-descriptions-item>
          <a-descriptions-item label="失败原因">
            <span v-if="detail.failureReason" style="color: var(--color-danger-text)">{{ detail.failureReason }}</span>
            <span v-else>-</span>
          </a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 节点进度 -->
      <a-card class="section-card" :bordered="false">
        <template #title>
          <span>节点执行进度（{{ detail?.nodeInstances?.length ?? 0 }}）</span>
        </template>

        <a-table
          :columns="nodeColumns"
          :data-source="detail?.nodeInstances || []"
          :pagination="false"
          row-key="id"
          size="middle"
          :scroll="{ x: 1100 }"
        >
          <template #bodyCell="{ column, record: r }">
            <template v-if="column.key === 'nodeName'">
              {{ nodeNameMap.get(r.nodeId) || '-' }}
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag
                class="status-tag"
                :color="NODE_STATUS_META[nodeStatusOf(r as OrchestrationNodeInstance)]?.color"
              >
                {{ NODE_STATUS_META[nodeStatusOf(r as OrchestrationNodeInstance)]?.text || r.status }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'related'">
              {{ relatedDesc(r as OrchestrationNodeInstance) }}
            </template>
            <template v-else-if="column.key === 'endStatusType'">
              <span v-if="r.endStatusType">{{ r.endStatusType }}</span>
              <span v-else class="text-muted">-</span>
            </template>
            <template v-else-if="column.key === 'startTime'">
              {{ formatTime(r.startTime) }}
            </template>
            <template v-else-if="column.key === 'completedTime'">
              {{ formatTime(r.completedTime) }}
            </template>
          </template>
        </a-table>
      </a-card>

      <!-- 派发记录时间线 -->
      <a-card class="section-card" :bordered="false">
        <template #title>
          <span>派发记录（{{ dispatches.length }}）</span>
        </template>

        <div v-if="dispatches.length === 0" class="empty-tip">暂无派发记录</div>

        <a-timeline v-else mode="left" class="dispatch-timeline">
          <a-timeline-item
            v-for="d in dispatches"
            :key="d.id"
            :color="DISPATCH_TIMELINE_COLORS[dispatchStatusOf(d)]"
          >
            <template #label>
              <div class="time-label">{{ formatTime(d.createdTime) }}</div>
              <div v-if="d.triggeredTime" class="time-label-sub">触发于 {{ formatTime(d.triggeredTime) }}</div>
            </template>

            <div class="dispatch-card">
              <div class="dispatch-card__header">
                <a-tag :color="DISPATCH_STATUS_META[dispatchStatusOf(d)]?.color" class="status-tag">
                  {{ DISPATCH_STATUS_META[dispatchStatusOf(d)]?.text }}
                </a-tag>
                <a-tag :color="d.dispatchType === 'auto' ? 'blue' : 'purple'">
                  {{ d.dispatchType === 'auto' ? '自动派发' : '手动派发' }}
                </a-tag>
                <span class="dispatch-card__id">派发 #{{ d.id }}</span>
              </div>

              <div class="dispatch-card__body">
                <span class="from">
                  <span class="lbl">起：</span>
                  <span v-if="d.sourceNodeId">{{ nodeNameMap.get(d.sourceNodeId) || d.sourceNodeId }}</span>
                  <span v-else-if="d.sourceFlowCode">{{ d.sourceFlowCode }}</span>
                  <span v-else class="text-muted">-</span>
                  <span v-if="d.sourceCardId" class="muted">（卡片 #{{ d.sourceCardId }}）</span>
                </span>
                <span class="arrow">→</span>
                <span class="to">
                  <span class="lbl">至：</span>
                  <span v-if="d.targetNodeId">{{ nodeNameMap.get(d.targetNodeId) || d.targetNodeId }}</span>
                  <span v-else-if="d.targetFlowCode">{{ d.targetFlowCode }}</span>
                  <span v-else class="text-muted">-</span>
                  <span v-if="d.targetCardId" class="muted">（卡片 #{{ d.targetCardId }}）</span>
                </span>
              </div>

              <div v-if="d.failureReason" class="dispatch-card__error">
                失败原因：{{ d.failureReason }}
              </div>
            </div>
          </a-timeline-item>
        </a-timeline>
      </a-card>
    </a-spin>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-card,
.section-card {
  margin: 0 12px;
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);
}

.info-card {
  margin-top: 12px;
}

.status-tag {
  color: var(--text-on-accent);
  border: none;
  font-weight: 500;
}

.text-muted {
  color: var(--text-3);
}

.empty-tip {
  text-align: center;
  padding: 40px 0;
  color: var(--text-3);
}

.dispatch-timeline {
  padding-top: 8px;

  .time-label {
    font-size: 13px;
    color: var(--text-2);
    white-space: nowrap;
  }
  .time-label-sub {
    font-size: 12px;
    color: var(--text-3);
    margin-top: 2px;
  }
}

.dispatch-card {
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 12px;

  &__header {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 6px;
  }

  &__id {
    margin-left: auto;
    font-size: 12px;
    color: var(--text-3);
  }

  &__body {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    font-size: 13px;
    color: var(--text-1);

    .lbl {
      color: var(--text-3);
      margin-right: 2px;
    }
    .arrow {
      color: var(--color-info);
      font-weight: 600;
      margin: 0 4px;
    }
    .muted {
      color: var(--text-3);
      margin-left: 2px;
    }
  }

  &__error {
    margin-top: 6px;
    font-size: 12px;
    color: var(--color-danger-text);
  }
}
</style>
