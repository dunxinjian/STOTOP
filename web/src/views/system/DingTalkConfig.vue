<template>
  <div class="page-container">
    <PageHeader title="钉钉同步" />

    <div class="dingtalk-layout">
      <!-- ========== 左栏：钉钉应用配置 ========== -->
      <a-card :bordered="false" class="config-card">
        <template #title>
          <div class="card-title-row">
            <span class="card-title-text">钉钉应用配置</span>
          </div>
        </template>



        <!-- 配置表单 -->
        <a-form layout="vertical" :model="configForm" class="config-form">
          <a-form-item label="AppKey（应用Key）">
            <a-input
              v-model:value="configForm.appKey"
              placeholder="请输入钉钉应用AppKey"
              allow-clear
            />
          </a-form-item>
          <a-form-item label="AppSecret（应用密钥）">
            <a-input-password
              v-model:value="configForm.appSecret"
              placeholder="请输入钉钉应用AppSecret"
            />
          </a-form-item>
          <a-form-item label="CorpId（企业ID）">
            <a-input
              v-model:value="configForm.corpId"
              placeholder="请输入企业CorpId"
              allow-clear
            />
          </a-form-item>
          <a-form-item label="AgentId（应用AgentId）">
            <a-input
              v-model:value="configForm.agentId"
              placeholder="请输入应用AgentId"
              allow-clear
            />
          </a-form-item>
        </a-form>

        <!-- 操作按钮 -->
        <a-space>
          <a-button type="primary" :loading="saveLoading" @click="handleSaveConfig">
            <template #icon><SaveOutlined /></template>
            保存配置
          </a-button>
          <a-button :loading="testConnLoading" @click="handleTestConnection">
            <template #icon><LinkOutlined /></template>
            测试连接
          </a-button>
        </a-space>

        <a-divider style="margin: 16px 0" />

        <!-- 定时自动同步 -->
        <div class="auto-sync-section">
          <div class="auto-sync-header">
            <span class="auto-sync-title">定时自动同步</span>
            <a-switch
              v-model:checked="autoSyncEnabled"
              :loading="autoSyncLoading"
              @change="(checked) => handleAutoSyncToggle(Boolean(checked))"
            />
          </div>
          <div v-if="autoSyncEnabled" class="auto-sync-config">
            <div class="cron-select">
              <span class="cron-label">执行频率</span>
              <a-select v-model:value="cronPreset" @change="(value) => handleCronPresetChange(String(value))" style="width: 200px">
                <a-select-option value="0 0 2 * * ?">每天凌晨 2:00</a-select-option>
                <a-select-option value="0 0 */6 * * ?">每 6 小时</a-select-option>
                <a-select-option value="0 0 */12 * * ?">每 12 小时</a-select-option>
                <a-select-option value="custom">自定义</a-select-option>
              </a-select>
            </div>
            <div v-if="cronPreset === 'custom'" class="cron-input">
              <a-input v-model:value="cronExpression" placeholder="Cron表达式，如: 0 0 2 * * ?" style="width: 200px" />
              <a-button size="small" type="link" @click="saveAutoSyncCron">确认</a-button>
            </div>
          </div>
          <div v-if="lastSyncTime" class="last-sync-time">
            <span>最后同步：{{ lastSyncTime }}</span>
          </div>
        </div>
      </a-card>

      <!-- ========== 右栏：同步操作 ========== -->
      <div class="sync-area">
        <a-card :bordered="false" class="sync-card">
          <template #title>
            <div class="card-title-row">
              <span class="card-title-text">同步操作</span>
              <a-tooltip title="刷新同步状态">
                <a-button size="small" type="text" @click="refreshStatus">
                  <template #icon><ReloadOutlined /></template>
                </a-button>
              </a-tooltip>
            </div>
          </template>

          <!-- 2×2 操作卡片网格 -->
          <div class="sync-grid" style="margin-bottom: 16px">
            <div
              v-for="item in syncCards"
              :key="item.key"
              :class="['sync-item', { 'is-disabled': !configValid, 'is-syncing': item.key === currentSyncKey }]"
              :style="{ '--accent': item.color }"
              @mouseenter="item.hover = true"
              @mouseleave="item.hover = false"
            >
              <div class="sync-item-accent"></div>
              <div class="sync-item-body">
                <div class="sync-item-icon" :style="{ background: item.bgColor }">
                  <component :is="item.icon" :style="{ color: item.color, fontSize: '22px' }" />
                </div>
                <div class="sync-item-info">
                  <div class="sync-item-title">{{ item.title }}</div>
                  <div class="sync-item-desc">{{ item.desc }}</div>
                </div>
                <a-tooltip v-if="!configValid" title="请先填写并保存配置">
                  <a-button size="small" disabled>同步</a-button>
                </a-tooltip>
                <a-button
                  v-else
                  size="small"
                  type="primary"
                  :ghost="item.key !== 'full'"
                  :loading="item.key === currentSyncKey"
                  :disabled="syncing && item.key !== currentSyncKey"
                  @click="handleSync(item.key)"
                >
                  {{ item.key === 'full' ? '全量同步' : '同步' }}
                </a-button>
              </div>
            </div>
          </div>

          <!-- 指定用户同步 -->
          <div class="specific-sync-section">
            <div class="specific-sync-inline">
              <span class="specific-sync-title">指定用户同步</span>
              <a-textarea
                v-model:value="specificUserIds"
                placeholder="钉钉用户ID，多个用逗号/换行/空格分隔"
                :rows="2"
                :disabled="specificSyncLoading"
                class="specific-sync-input"
              />
              <a-button
                type="primary"
                :loading="specificSyncLoading"
                :disabled="!configValid"
                @click="handleSpecificSync"
              >
                同步
              </a-button>
            </div>
            <!-- 结果展示（折叠） -->
            <div v-if="specificSyncResult" class="specific-sync-result">
              <a-space :size="12" wrap>
                <a-statistic title="成功" :value="specificSyncResult.successCount" :value-style="{ color: 'var(--color-success)', fontSize: '14px' }" />
                <a-statistic title="跳过" :value="specificSyncResult.skipCount" :value-style="{ color: 'var(--color-warning)', fontSize: '14px' }" />
                <a-statistic title="失败" :value="specificSyncResult.failCount" :value-style="{ color: 'var(--color-danger)', fontSize: '14px' }" />
              </a-space>
              <div v-if="specificSyncResult.errors?.length" class="specific-sync-errors">
                <div v-for="(err, i) in specificSyncResult.errors" :key="`specific-${i}-${err}`" class="specific-sync-error-item">
                  {{ err }}
                </div>
              </div>
            </div>
          </div>
        </a-card>

        <!-- ========== 同步进度区 ========== -->
        <Transition name="fade-slide">
          <a-card v-if="progressVisible" :bordered="false" class="progress-card">
            <template #title>
              <div class="card-title-row">
                <span class="card-title-text">同步进度</span>
                <a-button
                  v-if="progressStage === 'completed' || progressStage === 'error'"
                  size="small"
                  type="text"
                  @click="progressVisible = false"
                >
                  关闭
                </a-button>
              </div>
            </template>

            <!-- 进度条 -->
            <a-progress
              :percent="progressPercent"
              :status="progressBarStatus"
              :stroke-color="progressStrokeColor"
              style="margin-bottom: 20px"
            />

            <!-- 步骤状态列表（全量同步时） -->
            <div v-if="isFullSync" class="step-list">
              <div
                v-for="step in syncSteps"
                :key="step.key"
                :class="['step-row', `step-${step.status}`]"
              >
                <span class="step-icon">
                  <CheckCircleFilled v-if="step.status === 'done'" style="color: var(--color-success)" />
                  <LoadingOutlined v-else-if="step.status === 'running'" spin style="color: var(--color-info)" />
                  <ClockCircleOutlined v-else style="color: #d9d9d9" />
                </span>
                <span class="step-label">{{ step.label }}：</span>
                <span class="step-message">{{ step.message }}</span>
                <span v-if="step.duration" class="step-duration">耗时 {{ step.duration }}</span>
              </div>
            </div>

            <!-- 单步同步消息 -->
            <div v-else class="single-progress-msg">
              <LoadingOutlined v-if="progressStage !== 'completed' && progressStage !== 'error'" spin />
              <span>{{ progressMessage }}</span>
            </div>

            <!-- 完成结果摘要 -->
            <Transition name="fade-slide">
              <div v-if="syncResult" class="result-summary">
                <a-divider style="margin: 16px 0 12px" />
                <a-space :size="24" wrap>
                  <a-statistic title="总计" :value="syncResult.totalCount" />
                  <a-statistic title="成功" :value="syncResult.successCount" :value-style="{ color: 'var(--color-success)' }" />
                  <a-statistic title="失败" :value="syncResult.failCount" :value-style="{ color: 'var(--color-danger)' }" />
                  <a-statistic title="跳过" :value="syncResult.skipCount" :value-style="{ color: 'var(--color-warning)' }" />
                </a-space>

                <!-- 错误详情 -->
                <div v-if="syncResult.errors?.length" style="margin-top: 12px">
                  <a-collapse :bordered="false">
                    <a-collapse-panel key="errors" header="错误详情">
                      <div
                        v-for="(err, i) in syncResult.errors"
                        :key="`sync-${i}-${err}`"
                        style="color: var(--color-danger); margin-bottom: 4px; font-size: 13px"
                      >
                        {{ err }}
                      </div>
                    </a-collapse-panel>
                  </a-collapse>
                </div>
              </div>
            </Transition>

            <!-- 超时提示 -->
            <a-alert
              v-if="progressStage === 'timeout'"
              type="warning"
              show-icon
              style="margin-top: 12px"
              message="同步操作可能仍在后台执行中"
              description="请稍后点击右上角刷新按钮查看最新状态"
            />
          </a-card>
        </Transition>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted, markRaw, type Component } from 'vue'
import { message } from 'ant-design-vue'
import { HubConnectionState } from '@microsoft/signalr'
import {
  SaveOutlined,
  LinkOutlined,
  SyncOutlined,
  ReloadOutlined,
  ApartmentOutlined,
  TeamOutlined,
  IdcardOutlined,
  CheckCircleFilled,
  LoadingOutlined,
  ClockCircleOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { createSignalRConnection, type SignalRManager } from '@/utils/signalr'
import {
  getDingTalkConfig,
  saveDingTalkConfig,
  testDingTalkConnection,
  pullDingTalkDepartments,
  pullDingTalkUsers,
  pullDingTalkPositions,
  fullSyncFromDingTalk,
  syncSpecificDingTalkUsers,
  getDingTalkSyncStatus,
  getAutoSyncConfig,
  updateAutoSync,
  type DingTalkConfig,
} from '@/api/system'

// ===================== 配置管理 =====================

const configLoading = ref(false)
const saveLoading = ref(false)
const lastSyncTime = ref('')

const configForm = reactive({
  appKey: '',
  appSecret: '',
  corpId: '',
  agentId: '',
})

// 配置是否填写完整
const configValid = computed(() =>
  !!configForm.appKey && !!configForm.corpId
)

// 定时同步状态
const autoSyncEnabled = ref(false)
const autoSyncLoading = ref(false)
const cronPreset = ref('0 0 2 * * ?')
const cronExpression = ref('0 0 2 * * ?')

/** 加载钉钉配置 */
async function loadConfig() {
  configLoading.value = true
  try {
    const config = await getDingTalkConfig()
    if (config) {
      configForm.appKey = config.appKey || ''
      configForm.appSecret = config.appSecret || ''
      configForm.corpId = config.corpId || ''
      configForm.agentId = config.agentId || ''
      lastSyncTime.value = config.lastSyncTime || ''
    }
  } catch {
    // 配置 API 可能不存在，静默处理
    console.warn('加载钉钉配置失败，配置API可能尚未实现')
  } finally {
    configLoading.value = false
  }
}

/** 测试连接 */
const testConnLoading = ref(false)
async function handleTestConnection() {
  if (!configForm.appKey || !configForm.appSecret || !configForm.corpId) {
    message.warning('请先填写配置信息')
    return
  }
  testConnLoading.value = true
  try {
    await testDingTalkConnection()
    message.success('连接成功')
  } catch (err: any) {
    const msg = err?.response?.data?.message || err?.message || '未知错误'
    message.error('连接失败: ' + msg)
  } finally {
    testConnLoading.value = false
  }
}

/** 保存配置 */
async function handleSaveConfig() {
  saveLoading.value = true
  try {
    await saveDingTalkConfig({
      appKey: configForm.appKey,
      appSecret: configForm.appSecret,
      corpId: configForm.corpId,
      agentId: configForm.agentId,
    })
    await loadConfig()
    message.success('配置保存成功')
  } catch {
    message.error('配置保存失败，请重试')
  } finally {
    saveLoading.value = false
  }
}

// ===================== 定时同步 =====================

/** 加载定时同步配置 */
async function loadAutoSyncConfig() {
  try {
    const res = await getAutoSyncConfig()
    if (res) {
      autoSyncEnabled.value = res.enabled
      cronExpression.value = res.cronExpression || '0 0 2 * * ?'
      const presets = ['0 0 2 * * ?', '0 0 */6 * * ?', '0 0 */12 * * ?']
      cronPreset.value = presets.includes(cronExpression.value) ? cronExpression.value : 'custom'
    }
  } catch {
    // 静默处理
  }
}

/** 定时同步开关切换（即时生效） */
async function handleAutoSyncToggle(checked: boolean) {
  autoSyncLoading.value = true
  try {
    await updateAutoSync({
      enabled: checked,
      cronExpression: cronExpression.value,
    })
    message.success(checked ? '定时同步已开启' : '定时同步已关闭')
  } catch {
    autoSyncEnabled.value = !checked
    message.error('操作失败，请重试')
  } finally {
    autoSyncLoading.value = false
  }
}

/** Cron预设切换 */
function handleCronPresetChange(value: string) {
  if (value !== 'custom') {
    cronExpression.value = value
    if (autoSyncEnabled.value) {
      saveAutoSyncCron()
    }
  }
}

/** 保存自定义Cron */
async function saveAutoSyncCron() {
  autoSyncLoading.value = true
  try {
    await updateAutoSync({
      enabled: autoSyncEnabled.value,
      cronExpression: cronExpression.value,
    })
    message.success('同步频率已更新')
  } catch {
    message.error('更新失败')
  } finally {
    autoSyncLoading.value = false
  }
}

// ===================== 同步卡片定义 =====================

interface SyncCard {
  key: string
  title: string
  desc: string
  icon: Component
  color: string
  bgColor: string
  hover: boolean
}

const syncCards = reactive<SyncCard[]>([
  {
    key: 'departments',
    title: '部门同步',
    desc: '同步钉钉部门到本地组织架构',
    icon: markRaw(ApartmentOutlined),
    color: 'var(--color-info)',
    bgColor: 'var(--color-info-light)',
    hover: false,
  },
  {
    key: 'users',
    title: '人员同步',
    desc: '同步钉钉用户到本地员工信息',
    icon: markRaw(TeamOutlined),
    color: 'var(--color-success)',
    bgColor: 'rgba(82,196,26,0.08)',
    hover: false,
  },
  {
    key: 'positions',
    title: '职位同步',
    desc: '同步钉钉职位到本地岗位数据',
    icon: markRaw(IdcardOutlined),
    color: 'var(--color-warning)',
    bgColor: 'rgba(250,173,20,0.08)',
    hover: false,
  },
  {
    key: 'full',
    title: '全量同步',
    desc: '按部门→人员→职位顺序全量同步',
    icon: markRaw(SyncOutlined),
    color: 'var(--biz-waybill)',
    bgColor: 'rgba(114,46,209,0.08)',
    hover: false,
  },
])

// ===================== 同步逻辑 =====================

const syncing = ref(false)
const currentSyncKey = ref('')
const progressVisible = ref(false)
const progressStage = ref('')
const progressMessage = ref('')
const progressPercent = ref(0)
const isFullSync = ref(false)

/** 同步结果 */
interface SyncResult {
  totalCount: number
  successCount: number
  failCount: number
  skipCount: number
  errors?: string[]
}
const syncResult = ref<SyncResult | null>(null)

/** 步骤状态 */
interface StepInfo {
  key: string
  label: string
  status: 'pending' | 'running' | 'done' | 'error'
  message: string
  duration?: string
  startTime?: number
}

const syncSteps = reactive<StepInfo[]>([
  { key: 'departments', label: '部门同步', status: 'pending', message: '等待中' },
  { key: 'users', label: '人员同步', status: 'pending', message: '等待中' },
  { key: 'positions', label: '职位同步', status: 'pending', message: '等待中' },
])

/** 进度条状态 */
const progressBarStatus = computed(() => {
  if (progressStage.value === 'error') return 'exception' as const
  if (progressStage.value === 'completed') return 'success' as const
  return 'active' as const
})

const progressStrokeColor = computed(() => {
  if (progressStage.value === 'error') return 'var(--color-danger)'
  if (progressStage.value === 'completed') return 'var(--color-success)'
  return 'var(--color-info)'
})

/** 重置步骤状态 */
function resetSteps() {
  syncSteps.forEach((step) => {
    step.status = 'pending'
    step.message = '等待中'
    step.duration = undefined
    step.startTime = undefined
  })
}

/** 更新步骤状态（由 SignalR 消息驱动） */
function updateStepFromProgress(stage: string, msg: string, current: number, total: number, percent: number, result?: SyncResult) {
  progressStage.value = stage
  progressMessage.value = msg
  progressPercent.value = percent

  if (stage === 'completed') {
    // 所有步骤标记完成
    syncSteps.forEach((step) => {
      if (step.status === 'running') {
        step.status = 'done'
        if (step.startTime) {
          step.duration = ((Date.now() - step.startTime) / 1000).toFixed(1) + 's'
        }
      }
    })
    if (result) {
      syncResult.value = result as SyncResult
    }
    syncing.value = false
    currentSyncKey.value = ''
    return
  }

  if (stage === 'error') {
    const runningStep = syncSteps.find((s) => s.status === 'running')
    if (runningStep) {
      runningStep.status = 'error'
      runningStep.message = msg
    }
    syncing.value = false
    currentSyncKey.value = ''
    return
  }

  // 正常进度更新
  const stepMap: Record<string, number> = { departments: 0, users: 1, positions: 2 }
  const idx = stepMap[stage]
  if (idx !== undefined) {
    const step = syncSteps[idx]
    // 上一步标记完成
    if (idx > 0 && syncSteps[idx - 1].status === 'running') {
      const prev = syncSteps[idx - 1]
      prev.status = 'done'
      if (prev.startTime) {
        prev.duration = ((Date.now() - prev.startTime) / 1000).toFixed(1) + 's'
      }
    }
    if (step.status !== 'running' && step.status !== 'done') {
      step.status = 'running'
      step.startTime = Date.now()
    }
    step.message = total > 0 ? `进行中 (${current}/${total})...` : msg
  }
}

// ===================== SignalR 连接 =====================

let signalRManager: SignalRManager | null = null

async function connectSignalR() {
  if (signalRManager) return

  signalRManager = createSignalRConnection({
    url: '/hubs/progress',
    onStateChange: (state) => {
      if (state === HubConnectionState.Connected) {
        // 订阅钉钉同步进度组
        signalRManager?.connection.invoke('SubscribeDingTalkSync').catch((err) => { console.warn('[SignalR] invoke failed:', err?.message) })
      }
    },
  })

  // 监听同步进度
  signalRManager.connection.on('OnDingTalkSyncProgress', (data: {
    stage: string
    message: string
    current: number
    total: number
    percent: number
    result?: SyncResult
  }) => {
    updateStepFromProgress(data.stage, data.message, data.current, data.total, data.percent, data.result)
  })

  try {
    await signalRManager.start()
    await signalRManager.connection.invoke('SubscribeDingTalkSync')
  } catch (err) {
    console.warn('SignalR 连接失败，同步进度可能无法实时显示', err)
  }
}

function disconnectSignalR() {
  if (signalRManager) {
    signalRManager.connection.invoke('UnsubscribeDingTalkSync').catch((err) => { console.warn('[SignalR] invoke failed:', err?.message) })
    signalRManager.stop()
    signalRManager = null
  }
}

// ===================== 同步操作 =====================

/** 执行同步（单步或全量） */
async function handleSync(key: string) {
  if (syncing.value) return

  syncing.value = true
  currentSyncKey.value = key
  progressVisible.value = true
  progressStage.value = 'syncing'
  progressPercent.value = 0
  progressMessage.value = '正在启动同步...'
  syncResult.value = null

  // 确保 SignalR 已连接
  await connectSignalR()

  if (key === 'full') {
    // 全量同步：后端一次性处理，进度由 SignalR 推送
    isFullSync.value = true
    resetSteps()
    try {
      await fullSyncFromDingTalk()
      // 全量同步是后台任务，结果由 SignalR 通知
      if (progressStage.value === 'syncing') {
        progressMessage.value = '同步任务已启动，等待后台处理...'
      }
    } catch {
      handleSyncTimeout()
    }
  } else {
    // 单步同步
    isFullSync.value = false
    try {
      const apiMap: Record<string, () => Promise<any>> = {
        departments: pullDingTalkDepartments,
        users: pullDingTalkUsers,
        positions: pullDingTalkPositions,
      }
      const apiFn = apiMap[key]
      if (!apiFn) {
        message.info('功能开发中')
        syncing.value = false
        currentSyncKey.value = ''
        return
      }

      progressMessage.value = `正在同步${key === 'departments' ? '部门' : key === 'users' ? '人员' : '职位'}...`
      const res = await apiFn()
      const count = Array.isArray(res) ? res.length : 0

      progressStage.value = 'completed'
      progressPercent.value = 100
      progressMessage.value = `同步完成，共获取 ${count} 条数据`
      message.success(progressMessage.value)
    } catch (err: any) {
      if (err?.code === 'ECONNABORTED' || err?.message?.includes('timeout')) {
        handleSyncTimeout()
      } else {
        progressStage.value = 'error'
        progressMessage.value = '同步失败，请检查钉钉配置后重试'
      }
    } finally {
      syncing.value = false
      currentSyncKey.value = ''
    }
  }
}

/** 处理超时 */
function handleSyncTimeout() {
  progressStage.value = 'timeout'
  progressMessage.value = '请求超时，同步操作可能仍在后台执行中'
  syncing.value = false
  currentSyncKey.value = ''
}

/** 刷新状态 */
function refreshStatus() {
  loadConfig()
  message.info('状态已刷新')
}

// ===================== 指定用户同步 =====================

const specificUserIds = ref('')
const specificSyncLoading = ref(false)
const specificSyncResult = ref<{ totalCount: number; successCount: number; failCount: number; skipCount: number; errors: string[] | null } | null>(null)

async function handleSpecificSync() {
  const raw = specificUserIds.value.trim()
  if (!raw) {
    message.warning('请输入至少一个钉钉用户ID')
    return
  }
  const ids = raw.split(/[,\s\n，]+/).map(s => s.trim()).filter(Boolean)
  if (ids.length === 0) {
    message.warning('请输入至少一个钉钉用户ID')
    return
  }
  if (ids.length > 50) {
    message.warning('每次最多同步 50 个用户')
    return
  }
  specificSyncLoading.value = true
  specificSyncResult.value = null
  try {
    const res = await syncSpecificDingTalkUsers(ids)
    specificSyncResult.value = res
    if (res.failCount === 0) {
      message.success(`同步完成，成功 ${res.successCount} 人`)
    } else {
      message.warning(`同步完成，成功 ${res.successCount}，失败 ${res.failCount}`)
    }
  } catch (err: any) {
    message.error('同步失败：' + (err?.message || '未知错误'))
  } finally {
    specificSyncLoading.value = false
  }
}

// ===================== 生命周期 =====================

/** 检查是否有正在进行的同步，恢复进度显示 */
async function checkSyncStatus() {
  try {
    const status = await getDingTalkSyncStatus()
    if (status && status.isSyncing) {
      // 恢复同步进度显示
      syncing.value = true
      progressVisible.value = true
      isFullSync.value = true
      updateStepFromProgress(
        status.stage || '',
        status.message || '',
        status.current || 0,
        status.total || 0,
        status.percent || 0,
        undefined
      )
    }
  } catch {
    // 静默处理，不影响页面加载
  }
}

onMounted(async () => {
  await loadConfig()
  connectSignalR()
  await checkSyncStatus()
  await loadAutoSyncConfig()
})

onUnmounted(() => {
  disconnectSignalR()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// ===== 整体布局 =====
.dingtalk-layout {
  display: flex;
  gap: 16px;
  align-items: stretch;
}

// ===== 左栏配置卡片 =====
.config-card {
  width: 380px;
  flex-shrink: 0;
}

.card-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.card-title-text {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
}

.auto-sync-section {
  margin-top: 8px;
}

.auto-sync-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.auto-sync-title {
  font-weight: 500;
  font-size: 14px;
}

.auto-sync-config {
  margin-bottom: 12px;
}

.cron-select {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.cron-label {
  color: #666;
  font-size: 13px;
}

.cron-input {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
}

.last-sync-time {
  color: #999;
  font-size: 12px;
  margin-top: 8px;
}

.config-form {
  :deep(.ant-form-item) {
    margin-bottom: 16px;
  }

  :deep(.ant-form-item-label > label) {
    font-size: 13px;
    color: $text-regular;
  }
}



// ===== 右栏同步区 =====
.sync-area {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sync-card,
.progress-card {
  width: 100%;
}

// ===== 同步卡片网格 =====
.sync-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.sync-item {
  position: relative;
  border-radius: 12px;
  border: 1px solid $border-color-lighter;
  background: #fff;
  overflow: hidden;
  transition: all 0.2s ease;
  cursor: default;

  &:hover:not(.is-disabled) {
    transform: translateY(-2px);
    box-shadow: $shadow-card-hover;
  }

  &.is-disabled {
    opacity: 0.55;
  }

  &.is-syncing {
    border-color: var(--accent);
    box-shadow: 0 0 0 1px var(--accent), $shadow-sm;
  }
}

.sync-item-accent {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
  background: var(--accent);
  border-radius: 4px 0 0 4px;
}

.sync-item-body {
  padding: 16px 16px 16px 20px;
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 12px;
}

.sync-item-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.sync-item-info {
  flex: 1;
}

.sync-item-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 4px;
}

.sync-item-desc {
  font-size: 13px;
  color: $text-secondary;
  line-height: 1.4;
}

// ===== 指定用户同步 =====
.specific-sync-section {
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid $border-color-lighter;
}

.specific-sync-inline {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}

.specific-sync-title {
  font-size: 13px;
  font-weight: 600;
  color: $text-primary;
  white-space: nowrap;
  line-height: 32px;
}

.specific-sync-input {
  flex: 1;
  min-width: 0;
}

.specific-sync-result {
  margin-top: 8px;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.02);
  border-radius: $border-radius-md;

  :deep(.ant-statistic-title) {
    font-size: 11px;
    margin-bottom: 0;
  }
}

.specific-sync-errors {
  margin-top: 6px;
  max-height: 80px;
  overflow-y: auto;
}

.specific-sync-error-item {
  color: var(--color-danger);
  font-size: 12px;
  margin-bottom: 2px;
}

// ===== 进度区 =====
.step-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.step-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: $text-regular;
  padding: 8px 12px;
  border-radius: $border-radius-md;
  background: rgba(0, 0, 0, 0.02);

  &.step-done {
    background: rgba(82, 196, 26, 0.04);
    color: $text-primary;
  }

  &.step-running {
    background: var(--color-primary-light);
    color: $color-primary;
  }

  &.step-error {
    background: rgba(255, 77, 79, 0.04);
    color: $color-danger;
  }
}

.step-icon {
  font-size: 16px;
  display: flex;
  align-items: center;
}

.step-label {
  font-weight: 500;
  white-space: nowrap;
}

.step-message {
  flex: 1;
}

.step-duration {
  color: $text-secondary;
  font-size: 12px;
  white-space: nowrap;
}

.single-progress-msg {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: $text-regular;
  padding: 8px 0;
}

.result-summary {
  :deep(.ant-statistic-title) {
    font-size: 12px;
  }

  :deep(.ant-statistic-content-value) {
    font-size: 20px;
  }
}

// ===== 过渡动画 =====
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition: all 0.3s ease;
}

.fade-slide-enter-from {
  opacity: 0;
  transform: translateY(-10px);
}

.fade-slide-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

// ===== 响应式 =====
@media (max-width: $breakpoint-md) {
  .dingtalk-layout {
    flex-direction: column;
  }

  .config-card {
    width: 100%;
  }

  .sync-grid {
    grid-template-columns: 1fr;
  }
}
</style>
