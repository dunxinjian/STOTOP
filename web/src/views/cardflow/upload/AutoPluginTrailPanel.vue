<template>
  <a-drawer
    :open="open"
    title="AutoPlugin 执行轨迹"
    :width="520"
    placement="right"
    :destroyOnClose="true"
    @close="handleClose"
  >
    <a-spin :spinning="loading">
      <!-- 流程信息 -->
      <div v-if="trail" class="trail-header">
        <a-descriptions :column="2" size="small" :labelStyle="{ fontWeight: 500 }">
          <a-descriptions-item label="流程">{{ trail.flowName }}</a-descriptions-item>
          <a-descriptions-item label="批次状态">
            <a-tag :color="batchStatusColor(trail.batchStatus)">{{ trail.batchStatus }}</a-tag>
          </a-descriptions-item>
        </a-descriptions>
      </div>

      <!-- AutoPlugin 步骤轨迹 -->
      <div v-if="trail && trail.autoPlugins.length > 0" class="trail-steps">
        <a-steps
          :current="trail.currentPluginIndex"
          direction="vertical"
          size="small"
        >
          <a-step
            v-for="(autoPlugin, idx) in trail.autoPlugins"
            :key="idx"
            :status="stepStatus(autoPlugin)"
          >
            <template #title>
              <div class="step-title-row">
                <span class="step-title-name">{{ autoPlugin.pluginName }}</span>
                <a-tag :color="autoPlugin.pluginType === 'Input' ? 'blue' : 'green'" size="small" class="step-type-tag">
                  {{ autoPlugin.pluginType === 'Input' ? '输入' : '处理' }}
                </a-tag>
                <a-tag v-if="autoPlugin.pluginImplType" size="small" class="step-impl-tag">{{ implLabel(autoPlugin.pluginImplType) }}</a-tag>
              </div>
            </template>
            <template #description>
              <div class="step-desc">
                <!-- AutoPlugin 步骤级进度（仅 Running 状态显示） -->
                <div v-if="autoPlugin.status === 'Running' && currentAutoPluginStepName && currentBatchId === batchId" class="agent-step-progress">
                  <a-tag color="processing" size="small">
                    步骤 {{ (currentAutoPluginStepIndex ?? 0) + 1 }}/{{ currentAutoPluginTotalSteps }}：{{ currentAutoPluginStepName }}
                  </a-tag>
                </div>
                <!-- 数据处理进度（仅 Running 状态显示） -->
                <div v-if="autoPlugin.status === 'Running' && dataProgressPercent > 0 && dataProgressPercent < 100" class="data-progress-section">
                  <a-progress
                    :percent="dataProgressPercent"
                    size="small"
                    status="active"
                  />
                  <div class="data-progress-label">
                    {{ dataProgressDetail || dataProgressName }}：{{ dataProcessedCount }}/{{ dataProgressTotal }} 条
                  </div>
                </div>
                <span v-if="autoPlugin.snapshotTime" class="step-time">
                  <ClockCircleOutlined /> {{ formatTime(autoPlugin.snapshotTime) }}
                </span>
                <span v-if="autoPlugin.hasSnapshot" class="step-snapshot-badge">
                  <SafetyCertificateOutlined /> 已快照
                </span>
                <!-- 回撤按钮：仅已完成且支持回撤且有快照的节点 -->
                <a-button
                  v-if="autoPlugin.status === 'Completed' && autoPlugin.supportsRollback && autoPlugin.hasSnapshot"
                  type="link"
                  size="small"
                  danger
                  class="rollback-btn"
                  @click="confirmRollback(autoPlugin, idx)"
                >
                  <RollbackOutlined /> 回撤到此步骤之前
                </a-button>
              </div>
            </template>
            <template #icon>
              <LoadingOutlined v-if="autoPlugin.status === 'Running'" style="color: var(--color-info)" />
              <CheckCircleOutlined v-else-if="autoPlugin.status === 'Completed'" style="color: var(--color-success)" />
              <CloseCircleOutlined v-else-if="autoPlugin.status === 'Failed'" style="color: var(--color-danger)" />
              <ClockCircleOutlined v-else style="color: var(--text-3)" />
            </template>
          </a-step>
        </a-steps>
      </div>

      <a-empty v-else-if="!loading && trail" description="暂无AutoPlugin配置" />
      <a-empty v-else-if="!loading && !trail" description="未关联流程" />
    </a-spin>

    <!-- 回撤确认弹窗 -->
    <a-modal
      v-model:open="rollbackModalVisible"
      title="确认回撤"
      :okText="'确认回撤'"
      :cancelText="'取消'"
      :okButtonProps="{ danger: true }"
      :confirmLoading="rollbackLoading"
      @ok="executeRollback"
    >
      <a-alert type="warning" showIcon style="margin-bottom: 12px">
        <template #message>回撤操作不可逆，将清除以下AutoPlugin的执行结果和快照数据。</template>
      </a-alert>
      <p>将回撤到 <b>{{ rollbackTarget?.pluginName }}</b> 之前，以下AutoPlugin的数据将被清除：</p>
      <ul class="rollback-agent-list">
        <li v-for="name in rollbackAgentNames" :key="name">{{ name }}</li>
      </ul>
    </a-modal>
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined,
  LoadingOutlined, RollbackOutlined, SafetyCertificateOutlined,
} from '@ant-design/icons-vue'
import {
  getBatchAutoPluginTrail, rollbackBatch,
} from '@/api/cardflow'
import type { AutoPluginTrailDto, AutoPluginTrailItemDto } from '@/api/cardflow'
import { ensureConnected } from '@/utils/signalr'
import type { HubConnection } from '@microsoft/signalr'

const props = defineProps<{
  open: boolean
  batchId: number | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'refreshed'): void
}>()

// ==================== 数据 ====================
const loading = ref(false)
const trail = ref<AutoPluginTrailDto | null>(null)

// ==================== 回撤 ====================
const rollbackModalVisible = ref(false)
const rollbackLoading = ref(false)
const rollbackTarget = ref<AutoPluginTrailItemDto | null>(null)
const rollbackTargetIndex = ref(-1)

const rollbackAgentNames = computed(() => {
  if (!trail.value || rollbackTargetIndex.value < 0) return []
  return trail.value.autoPlugins
    .filter((_a, i) => i >= rollbackTargetIndex.value)
    .filter(a => a.status === 'Completed' || a.status === 'Running')
    .map(a => a.pluginName)
})

// ==================== 实现类型标签 ====================
const implLabelMap: Record<string, string> = {
  ExcelInput: 'Excel导入',
  TextFileInput: '文本文件导入',
  DbInput: '数据库导入',
  ManualInput: '手工录入',
  SecurityCheck: '安全检查',
  QualityAnalysis: '质量分析',
  Classification: '数据分类',
  AutoVoucher: '自动凭证',
  WorkTask: '工作任务',
  AlertNotify: '告警通知',
  InfoRecord: '信息记录',
}

function implLabel(type: string) {
  return implLabelMap[type] || type
}

// ==================== 状态 ====================
function stepStatus(autoPlugin: AutoPluginTrailItemDto): 'wait' | 'process' | 'finish' | 'error' {
  switch (autoPlugin.status) {
    case 'Completed': return 'finish'
    case 'Running': return 'process'
    case 'Failed': return 'error'
    default: return 'wait'
  }
}

function batchStatusColor(status: string) {
  if (status === '已完成' || status === 'Completed') return 'success'
  if (status === '处理中' || status === 'Running') return 'processing'
  if (status === '失败' || status === 'Failed') return 'error'
  return 'default'
}

function formatTime(t: string | null) {
  if (!t) return ''
  try {
    const d = new Date(t)
    return d.toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return t
  }
}

// ==================== 加载数据 ====================
async function fetchTrail() {
  if (!props.batchId) return
  loading.value = true
  try {
    const res: any = await getBatchAutoPluginTrail(props.batchId)
    trail.value = res ?? null
    // 从 API 返回的数据恢复步骤级进度（仅当有正在运行的 AutoPlugin 且 SignalR 尚未推送数据时）
    if (trail.value && !currentAutoPluginStepName.value) {
      const runningAutoPlugin = trail.value.autoPlugins.find(a => a.status === 'Running')
      if (runningAutoPlugin?.currentStepName) {
        currentAutoPluginStepName.value = runningAutoPlugin.currentStepName
        currentAutoPluginStepIndex.value = runningAutoPlugin.currentStepIndex ?? 0
        currentAutoPluginTotalSteps.value = runningAutoPlugin.totalSteps ?? 0
        currentAutoPluginStepStatus.value = runningAutoPlugin.currentStepStatus ?? ''
        currentBatchId.value = props.batchId
      }
    }
  } catch {
    trail.value = null
  } finally {
    loading.value = false
  }
}

// ==================== 回撤 ====================
function confirmRollback(autoPlugin: AutoPluginTrailItemDto, index: number) {
  rollbackTarget.value = autoPlugin
  rollbackTargetIndex.value = index
  rollbackModalVisible.value = true
}

async function executeRollback() {
  if (!props.batchId || rollbackTargetIndex.value < 0) return
  rollbackLoading.value = true
  try {
    const res: any = await rollbackBatch(props.batchId, rollbackTargetIndex.value)
    if (res?.success !== false) {
      message.success(res?.message || '回撤成功')
      rollbackModalVisible.value = false
      fetchTrail()
      emit('refreshed')
    } else {
      message.error(res?.message || '回撤失败')
    }
  } catch {
    message.error('回撤请求失败')
  } finally {
    rollbackLoading.value = false
  }
}

// ==================== SignalR 实时更新 ====================
let hubConnection: HubConnection | null = null

function onAutoPluginStarted(data: { batchId: number; pluginName: string; pluginIndex: number }) {
  if (!trail.value || data.batchId !== props.batchId) return
  const autoPlugin = trail.value.autoPlugins[data.pluginIndex]
  if (autoPlugin) {
    autoPlugin.status = 'Running'
    trail.value.currentPluginIndex = data.pluginIndex
  }
}

function onAutoPluginCompleted(data: { batchId: number; pluginName: string; pluginIndex: number; success: boolean; message?: string; autoPluginTrail?: AutoPluginTrailDto }) {
  if (!trail.value || data.batchId !== props.batchId) return
  const autoPlugin = trail.value.autoPlugins[data.pluginIndex]
  if (autoPlugin) {
    autoPlugin.status = data.success ? 'Completed' : 'Failed'
  }
  // 如果推送携带了 autoPluginTrail 快照，直接用快照更新本地状态，跳过异步 fetchTrail()
  if (data.autoPluginTrail) {
    trail.value = data.autoPluginTrail
  } else {
    // 兼容旧版本：无快照时仍走原有的 fetchTrail() 逻辑
    fetchTrail()
  }
}

function onBatchRollback(data: { batchId: number; targetAgentIndex: number; rolledBackAgents: string[] }) {
  if (data.batchId !== props.batchId) return
  fetchTrail()
}

// AutoPlugin 步骤级进度
const currentAutoPluginStepName = ref('')
const currentAutoPluginStepIndex = ref(0)
const currentAutoPluginTotalSteps = ref(0)
const currentAutoPluginStepStatus = ref('')
const currentBatchId = ref<number | null>(null)

// AutoPlugin 数据处理进度
const dataProgressName = ref('')
const dataProcessedCount = ref(0)
const dataProgressTotal = ref(0)
const dataProgressPercent = ref(0)
const dataProgressDetail = ref('')

function onAutoPluginStep(data: { batchId: number; pluginName: string; stepIndex: number; totalSteps: number; stepName: string; status: string }) {
  if (data.batchId !== props.batchId) return
  currentAutoPluginStepName.value = data.stepName
  currentAutoPluginStepIndex.value = data.stepIndex
  currentAutoPluginTotalSteps.value = data.totalSteps
  currentAutoPluginStepStatus.value = data.status
  currentBatchId.value = data.batchId
}

function onDataProgress(data: any) {
  if (data.batchId !== props.batchId) return
  dataProgressName.value = data.pluginName
  dataProcessedCount.value = data.processedCount
  dataProgressTotal.value = data.totalCount
  dataProgressPercent.value = data.percent
  dataProgressDetail.value = data.detail || ''
}

async function connectSignalR() {
  try {
    hubConnection = await ensureConnected()
    hubConnection.on('OnAutoPluginStarted', onAutoPluginStarted)
    hubConnection.on('OnAutoPluginCompleted', onAutoPluginCompleted)
    hubConnection.on('OnBatchRollback', onBatchRollback)
    hubConnection.on('OnAutoPluginStep', onAutoPluginStep)
    hubConnection.on('OnAutoPluginDataProgress', onDataProgress)
  } catch {
    console.warn('AutoPluginTrailPanel: SignalR 连接失败，实时更新不可用')
  }
}

function disconnectSignalR() {
  if (hubConnection) {
    hubConnection.off('OnAutoPluginStarted', onAutoPluginStarted)
    hubConnection.off('OnAutoPluginCompleted', onAutoPluginCompleted)
    hubConnection.off('OnBatchRollback', onBatchRollback)
    hubConnection.off('OnAutoPluginStep', onAutoPluginStep)
    hubConnection.off('OnAutoPluginDataProgress', onDataProgress)
    hubConnection = null
  }
}

// ==================== 生命周期 ====================
watch(() => props.open, async (val) => {
  if (val && props.batchId) {
    trail.value = null
    // 重置步骤级进度
    currentAutoPluginStepName.value = ''
    currentAutoPluginStepIndex.value = 0
    currentAutoPluginTotalSteps.value = 0
    currentAutoPluginStepStatus.value = ''
    // 重置数据处理进度
    dataProgressName.value = ''
    dataProcessedCount.value = 0
    dataProgressTotal.value = 0
    dataProgressPercent.value = 0
    dataProgressDetail.value = ''
    fetchTrail()
    connectSignalR()
  } else {
    disconnectSignalR()
  }
})

function handleClose() {
  emit('update:open', false)
}

onUnmounted(() => {
  disconnectSignalR()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.trail-header {
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid $border-color-lighter;
}

.trail-steps {
  padding: 4px 0;
}

.step-title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.step-title-name {
  font-weight: 500;
}

.step-type-tag,
.step-impl-tag {
  font-size: 11px;
  line-height: 18px;
  padding: 0 5px;
}

.step-desc {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 12px;
  color: $text-secondary;
}

.step-time {
  display: inline-flex;
  align-items: center;
  gap: 3px;
}

.step-snapshot-badge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: $color-success;
}

.rollback-btn {
  padding: 0;
  height: auto;
  font-size: 12px;
}

.agent-step-progress {
  margin-bottom: 4px;
}

.data-progress-section {
  margin: 4px 0;
  max-width: 280px;

  .data-progress-label {
    font-size: 12px;
    color: $text-secondary;
    margin-top: 2px;
  }
}

.rollback-agent-list {
  margin: 8px 0;
  padding-left: 20px;

  li {
    padding: 2px 0;
    color: $text-primary;
  }
}
</style>
