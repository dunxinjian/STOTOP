<script setup lang="ts">
/**
 * CardFlowMonitorPage.vue — 流程监控看板（管理员）
 *
 * 双行工具栏 + 可点选统计卡片 + 实例列表（可展开）+ 30 秒轮询 + 超时告警 + CSV 导出。
 */
import { ref, reactive, computed, onMounted, onUnmounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { isRedirectingToLogin } from '@/api/request'
import type { TableColumnsType, TableProps } from 'ant-design-vue'
import {
  SearchOutlined,
  EyeOutlined,
  StopOutlined,
  BellOutlined,
  SwapOutlined,
  ExportOutlined,
  ExclamationCircleOutlined,
  WarningFilled,
  ClockCircleOutlined,
} from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import {
  getCards,
  getCard,
  getFlowDefinitions,
  voidCard,
  urgeCard,
  transferCard,
} from '@/api/cardflow'
import type {
  CardListDto,
  CardDetailDto,
  CardQueryRequest,
  FlowDefinitionDto,
  StageInstanceDto,
} from '@/types/cardflow'
import { downloadBlob } from '@/utils/download'

const router = useRouter()

// ==================== 配置 ====================

/** 超过该小时数仍在审批中视为「超时」 */
const TIMEOUT_HOURS = 24

type CardStatus = 'draft' | 'active' | 'completed' | 'returned' | 'voided'

interface StatusMeta {
  text: string
  color: string
  tagColor: string
}

const STATUS_META: Record<CardStatus, StatusMeta> = {
  draft: { text: '草稿', color: 'var(--text-3)', tagColor: 'default' },
  active: { text: '进行中', color: 'var(--color-info)', tagColor: 'processing' },
  completed: { text: '已完成', color: 'var(--color-success)', tagColor: 'success' },
  returned: { text: '已退回', color: 'var(--color-danger)', tagColor: 'error' },
  voided: { text: '已作废', color: 'var(--text-3)', tagColor: 'default' },
}

const STATUS_OPTIONS: Array<{ label: string; value: CardStatus }> = [
  { label: '草稿', value: 'draft' },
  { label: '进行中', value: 'active' },
  { label: '已完成', value: 'completed' },
  { label: '已退回', value: 'returned' },
  { label: '已作废', value: 'voided' },
]

// ==================== 状态 / 数据 ====================

const loading = ref(false)
const dataSource = ref<CardListDto[]>([])
const flowDefinitions = ref<FlowDefinitionDto[]>([])

const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const searchParams = reactive<{
  flowId: number | undefined
  status: CardStatus[]
  initiator: string
}>({
  flowId: undefined,
  status: [],
  initiator: '',
})

// 默认最近 7 天
const dateRange = ref<[Dayjs, Dayjs] | undefined>([
  dayjs().subtract(7, 'day').startOf('day'),
  dayjs().endOf('day'),
])

// 选中行（批量预留）
const selectedRowKeys = ref<number[]>([])

// 行展开缓存：cardId → 详情
const detailCache = ref<Record<number, CardDetailDto>>({})
const detailLoading = ref<Record<number, boolean>>({})
const expandedRowKeys = ref<number[]>([])

// 统计
const stats = reactive({ inProgress: 0, completed: 0, voided: 0, timeout: 0 })

// 超时告警条
const timeoutAlertVisible = ref(false)
const timeoutAlertSeenIds = ref<Set<number>>(new Set())
const timeoutAlertCount = ref(0)

// 轮询
let pollTimer: ReturnType<typeof setInterval> | null = null

// ==================== 计算 ====================

const flowDefMap = computed(() => {
  const m = new Map<number, FlowDefinitionDto>()
  flowDefinitions.value.forEach(d => m.set(d.id, d))
  return m
})

const statCards = computed(() => [
  { key: 'active' as CardStatus, label: '进行中', value: stats.inProgress, color: STATUS_META.active.color },
  { key: 'completed' as CardStatus, label: '已完成', value: stats.completed, color: STATUS_META.completed.color },
  { key: 'voided' as CardStatus, label: '已作废', value: stats.voided, color: STATUS_META.voided.color },
  { key: 'timeout' as 'timeout', label: '超时未处理', value: stats.timeout, color: 'var(--color-warning)', danger: true },
])

const activeStatCardKey = computed<string | null>(() => {
  if (searchParams.status.length === 1 && !timeoutOnly.value) return searchParams.status[0]
  if (timeoutOnly.value) return 'timeout'
  return null
})

const timeoutOnly = ref(false)

// ==================== 加载 ====================

function buildQueryParams(): CardQueryRequest {
  const params: CardQueryRequest = {
    page: pagination.current,
    pageSize: pagination.pageSize,
    flowId: searchParams.flowId ?? undefined,
  }
  if (searchParams.status.length === 1) {
    params.status = searchParams.status[0]
  }
  if (dateRange.value && dateRange.value[0] && dateRange.value[1]) {
    params.startDate = dateRange.value[0].format('YYYY-MM-DD')
    params.endDate = dateRange.value[1].format('YYYY-MM-DD')
  }
  return params
}

function applyClientFilters(items: CardListDto[]): CardListDto[] {
  let arr = items
  // 多状态客户端过滤
  if (searchParams.status.length > 1) {
    const set = new Set<string>(searchParams.status)
    arr = arr.filter(it => set.has(it.status))
  }
  // 发起人模糊过滤
  const kw = searchParams.initiator.trim()
  if (kw) {
    arr = arr.filter(it => (it.initiatorName || '').includes(kw))
  }
  // 仅看超时
  if (timeoutOnly.value) {
    arr = arr.filter(it => isTimeoutRecord(it))
  }
  return arr
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getCards(buildQueryParams())
    const items = applyClientFilters(res?.items || [])
    dataSource.value = items
    pagination.total = res?.total ?? items.length
    detectNewTimeouts(items)
  } catch {
    // 认证失效时拦截器已提示并跳转登录页，此处不再重复报错
    if (!isRedirectingToLogin) {
      message.error('加载流程实例列表失败')
    }
  } finally {
    loading.value = false
  }
}

async function fetchStats() {
  try {
    const baseStart = dateRange.value?.[0]?.format('YYYY-MM-DD')
    const baseEnd = dateRange.value?.[1]?.format('YYYY-MM-DD')
    const baseFlow = searchParams.flowId ?? undefined
    const common = { page: 1, pageSize: 1, flowId: baseFlow, startDate: baseStart, endDate: baseEnd }
    const [a, b, c, all] = await Promise.all([
      getCards({ ...common, status: 'active' }),
      getCards({ ...common, status: 'completed' }),
      getCards({ ...common, status: 'voided' }),
      // 用于估算超时数：拉取一页活动状态最大尺寸
      getCards({ ...common, status: 'active', page: 1, pageSize: 200 }),
    ])
    stats.inProgress = a.total
    stats.completed = b.total
    stats.voided = c.total
    stats.timeout = (all.items || []).filter(isTimeoutRecord).length
  } catch {
    /* 静默 */
  }
}

async function fetchFlowDefinitions() {
  try {
    const res = await getFlowDefinitions({ page: 1, pageSize: 200 })
    flowDefinitions.value = res?.items || []
  } catch {
    /* 静默 */
  }
}

async function fetchAll(silent = false) {
  if (!silent) loading.value = true
  await Promise.all([fetchList(), fetchStats()])
}

// ==================== 超时检测 ====================

function isTimeoutRecord(rec: any): boolean {
  if (rec.status !== 'active') return false
  const start = rec.submitTime || rec.createdTime
  if (!start) return false
  const hours = dayjs().diff(dayjs(start), 'hour', true)
  return hours >= TIMEOUT_HOURS
}

function detectNewTimeouts(items: CardListDto[]) {
  const currentTimeouts = items.filter(isTimeoutRecord).map(it => it.id)
  const newOnes = currentTimeouts.filter(id => !timeoutAlertSeenIds.value.has(id))
  if (newOnes.length > 0) {
    newOnes.forEach(id => timeoutAlertSeenIds.value.add(id))
    timeoutAlertCount.value = currentTimeouts.length
    timeoutAlertVisible.value = true
  }
}

// ==================== 筛选事件 ====================

function handleSearch() {
  pagination.current = 1
  fetchAll()
}

function handleReset() {
  searchParams.flowId = undefined
  searchParams.status = []
  searchParams.initiator = ''
  dateRange.value = [dayjs().subtract(7, 'day').startOf('day'), dayjs().endOf('day')]
  timeoutOnly.value = false
  pagination.current = 1
  fetchAll()
}

const handleTableChange: TableProps['onChange'] = (pag) => {
  pagination.current = (pag as any).current ?? 1
  pagination.pageSize = (pag as any).pageSize ?? 20
  fetchList()
}

function handleStatCardClick(key: string) {
  if (key === 'timeout') {
    timeoutOnly.value = true
    searchParams.status = ['active']
  } else {
    timeoutOnly.value = false
    // 切换式筛选：再次点击取消
    if (searchParams.status.length === 1 && searchParams.status[0] === key) {
      searchParams.status = []
    } else {
      searchParams.status = [key as CardStatus]
    }
  }
  pagination.current = 1
  fetchList()
}

// ==================== 行展开（按需取详情） ====================

async function handleExpand(expanded: boolean, record: any) {
  if (!expanded) return
  if (detailCache.value[record.id]) return
  detailLoading.value[record.id] = true
  try {
    const detail = await getCard(record.id)
    detailCache.value[record.id] = detail
  } catch {
    message.error(`加载 #${record.cardNumber || record.id} 详情失败`)
  } finally {
    detailLoading.value[record.id] = false
  }
}

function currentStageOf(record: any): StageInstanceDto | null {
  const detail = detailCache.value[record.id]
  if (!detail) return null
  return (
    detail.stageInstances.find(s => s.id === detail.currentStageInstanceId) ||
    detail.stageInstances.find(s => s.status === 'pending' || s.status === 'active') ||
    null
  )
}

// ==================== 行操作 ====================

function handleView(record: any) {
  router.push({ path: `/cardflow/cards/${record.id}` })
}

function handleVoid(record: any) {
  Modal.confirm({
    title: '确认强制终止？',
    icon: h(ExclamationCircleOutlined),
    content: '相关待办将被取消，该操作不可恢复。',
    okText: '强制终止',
    okType: 'danger',
    async onOk() {
      try {
        await voidCard(record.id, '管理员强制终止')
        message.success('已强制终止')
        fetchAll()
      } catch {
        message.error('操作失败')
      }
    },
  })
}

// ===== 催办 Modal =====
const urgeModal = reactive({ visible: false, cardId: 0, reason: '请尽快处理', processing: false })

function handleUrge(record: any) {
  urgeModal.cardId = record.id
  urgeModal.reason = '请尽快处理'
  urgeModal.visible = true
}

async function doUrge() {
  if (!urgeModal.reason.trim()) {
    message.warning('请填写催办理由')
    return
  }
  urgeModal.processing = true
  try {
    await urgeCard(urgeModal.cardId, urgeModal.reason)
    message.success('催办已发送')
    urgeModal.visible = false
  } catch {
    message.error('催办失败')
  } finally {
    urgeModal.processing = false
  }
}

// ===== 转办 Modal =====
const transferModal = reactive({
  visible: false,
  cardId: 0,
  newUserId: undefined as number | undefined,
  opinion: '',
  processing: false,
})

function handleTransfer(record: any) {
  transferModal.cardId = record.id
  transferModal.newUserId = undefined
  transferModal.opinion = ''
  transferModal.visible = true
}

async function doTransfer() {
  if (!transferModal.newUserId) {
    message.warning('请选择目标人 (用户ID)')
    return
  }
  transferModal.processing = true
  try {
    await transferCard(transferModal.cardId, {
      newUserId: transferModal.newUserId,
      opinion: transferModal.opinion || '管理员转办',
    })
    message.success('转办成功')
    transferModal.visible = false
    fetchAll()
  } catch {
    message.error('转办失败')
  } finally {
    transferModal.processing = false
  }
}

// ==================== 导出（CSV，UTF-8 BOM，Excel 兼容） ====================

async function handleExport() {
  try {
    // 取较大一页用于导出（最多 1000 条）
    const params = buildQueryParams()
    const res = await getCards({ ...params, page: 1, pageSize: 1000 })
    const items = applyClientFilters(res?.items || [])
    if (items.length === 0) {
      message.warning('当前筛选条件下没有可导出的数据')
      return
    }
    const headers = ['编号', '标题', '流程名称', '发起人', '状态', '创建时间', '提交时间', '完成时间', '时长(小时)']
    const rows = items.map(it => [
      it.cardNumber || '',
      it.title || '',
      it.flowName || '',
      it.initiatorName || '',
      STATUS_META[(it.status as CardStatus)]?.text || it.status,
      formatTime(it.createdTime),
      formatTime(it.submitTime),
      formatTime(it.completedTime),
      computeDurationHours(it).toFixed(1),
    ])
    const csv = [headers, ...rows]
      .map(r => r.map(escapeCsvCell).join(','))
      .join('\r\n')
    const blob = new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8;' })
    downloadBlob(blob, `流程监控_${dayjs().format('YYYYMMDD_HHmmss')}.csv`)
    message.success(`已导出 ${items.length} 条记录`)
  } catch {
    message.error('导出失败')
  }
}

function escapeCsvCell(v: string | number): string {
  const s = String(v ?? '')
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`
  return s
}

// ==================== 工具方法 ====================

function formatTime(val?: string | null): string {
  if (!val) return '-'
  return dayjs(val).format('YYYY-MM-DD HH:mm')
}

function computeDurationHours(rec: any): number {
  const start = rec.submitTime || rec.createdTime
  if (!start) return 0
  const end = rec.completedTime ? dayjs(rec.completedTime) : dayjs()
  return Math.max(0, end.diff(dayjs(start), 'hour', true))
}

function formatDuration(rec: any): string {
  const hours = computeDurationHours(rec)
  if (hours < 1) {
    const minutes = Math.max(1, Math.round(hours * 60))
    return `${minutes} 分钟`
  }
  const days = Math.floor(hours / 24)
  const restH = Math.floor(hours - days * 24)
  if (days > 0) return `${days} 天 ${restH} 小时`
  return `${Math.floor(hours)} 小时 ${Math.round((hours - Math.floor(hours)) * 60)} 分`
}

function avatarText(name?: string | null): string {
  if (!name) return '?'
  const trimmed = name.trim()
  return trimmed.charAt(0)
}

// ==================== 表格列 ====================

const columns: TableColumnsType = [
  { title: '编号', dataIndex: 'cardNumber', key: 'cardNumber', width: 160 },
  { title: '标题', dataIndex: 'title', key: 'title', width: 220, ellipsis: true },
  { title: '流程名称', dataIndex: 'flowName', key: 'flowName', width: 160, ellipsis: true },
  { title: '发起人', dataIndex: 'initiatorName', key: 'initiatorName', width: 110 },
  { title: '当前节点', key: 'currentStage', width: 220 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' },
  { title: '时长', key: 'duration', width: 150 },
  { title: '操作', key: 'action', width: 280, fixed: 'right' },
]

// ==================== 生命周期 ====================

onMounted(async () => {
  await Promise.all([fetchFlowDefinitions(), fetchAll()])
  pollTimer = setInterval(() => {
    fetchAll(true)
  }, 30000)
})

onUnmounted(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
})
</script>

<template>
  <div class="page-container monitor-page">
    <PageHeader>
      <!-- 第一行：操作按钮 -->
      <template #actions>
        <a-space :size="8">
          <a-button type="primary" @click="handleExport">
            <template #icon><ExportOutlined /></template>
            导出 Excel
          </a-button>
        </a-space>
      </template>

      <!-- 第二行：筛选条件 -->
      <template #toolbar>
        <div class="filter-bar">
          <a-select
            v-model:value="searchParams.flowId"
            placeholder="流程类型"
            allow-clear
            show-search
            :filter-option="(input: string, option: any) => String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())"
            :options="flowDefinitions.map(f => ({ label: f.flowName, value: f.id }))"
            style="width: 180px"
          />
          <a-select
            v-model:value="searchParams.status"
            mode="multiple"
            placeholder="状态"
            allow-clear
            :max-tag-count="2"
            :options="STATUS_OPTIONS"
            style="width: 220px"
          />
          <a-input
            v-model:value="searchParams.initiator"
            placeholder="发起人"
            allow-clear
            style="width: 160px"
            @press-enter="handleSearch"
          />
          <a-range-picker
            v-model:value="dateRange"
            :allow-clear="false"
            format="YYYY-MM-DD"
            style="width: 240px"
          />
          <a-button type="primary" @click="handleSearch">
            <template #icon><SearchOutlined /></template>
            查询
          </a-button>
          <a-button @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <div class="monitor-body">
      <!-- 超时告警条 -->
      <a-alert
        v-if="timeoutAlertVisible && timeoutAlertCount > 0"
        class="timeout-alert"
        type="warning"
        show-icon
        closable
        :message="`共发现 ${timeoutAlertCount} 条流程已超时未处理（>${TIMEOUT_HOURS} 小时），请及时跟进。`"
        @close="timeoutAlertVisible = false"
      >
        <template #action>
          <a-button size="small" type="link" @click="handleStatCardClick('timeout')">
            仅看超时
          </a-button>
        </template>
      </a-alert>

      <!-- 统计卡片 -->
      <div class="stat-grid">
        <div
          v-for="card in statCards"
          :key="card.key"
          class="stat-card"
          :class="{
            'is-active': activeStatCardKey === card.key,
            'is-danger': (card as any).danger,
          }"
          :style="{ '--stat-color': card.color }"
          role="button"
          tabindex="0"
          @click="handleStatCardClick(card.key)"
          @keyup.enter="handleStatCardClick(card.key)"
        >
          <span class="stat-accent" />
          <div class="stat-meta">
            <span class="stat-label">
              {{ card.label }}
              <WarningFilled v-if="(card as any).danger && card.value > 0" class="stat-warn-icon" />
            </span>
            <span class="stat-value">{{ card.value }}</span>
          </div>
        </div>
      </div>

      <!-- 实例列表 -->
      <a-table
        class="monitor-table"
        :columns="columns"
        :data-source="dataSource"
        :loading="loading"
        row-key="id"
        size="middle"
        :pagination="{
          current: pagination.current,
          pageSize: pagination.pageSize,
          total: pagination.total,
          showSizeChanger: true,
          showQuickJumper: true,
          pageSizeOptions: ['10', '20', '50', '100'],
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        :row-selection="{
          selectedRowKeys,
          onChange: (keys: any[]) => (selectedRowKeys = keys.map(Number)),
          columnWidth: 44,
        }"
        :expanded-row-keys="expandedRowKeys"
        :scroll="{ x: 1500 }"
        @change="handleTableChange"
        @expand="handleExpand"
        @update:expanded-row-keys="(v: any[]) => (expandedRowKeys = v.map(Number))"
      >
        <template #bodyCell="{ column, record }">
          <!-- 编号 -->
          <template v-if="column.key === 'cardNumber'">
            <span class="mono">{{ record.cardNumber || '-' }}</span>
          </template>

          <!-- 标题 -->
          <template v-else-if="column.key === 'title'">
            <a class="row-title" @click="handleView(record)">{{ record.title || '(无标题)' }}</a>
          </template>

          <!-- 当前节点 -->
          <template v-else-if="column.key === 'currentStage'">
            <template v-if="detailLoading[record.id]">
              <span class="text-muted">加载中…</span>
            </template>
            <template v-else-if="detailCache[record.id] && currentStageOf(record)">
              <div class="stage-cell">
                <span class="stage-name">{{ currentStageOf(record)?.stageName }}</span>
                <a-avatar-group :max-count="3" size="small" class="stage-avatars">
                  <a-avatar
                    v-for="a in currentStageOf(record)?.assignees || []"
                    :key="a.id"
                    :size="20"
                    class="stage-avatar"
                    :class="{ done: a.status === 'completed' }"
                  >
                    {{ avatarText(a.userName) }}
                  </a-avatar>
                </a-avatar-group>
              </div>
            </template>
            <template v-else>
              <span class="text-muted">展开查看</span>
            </template>
          </template>

          <!-- 状态 -->
          <template v-else-if="column.key === 'status'">
            <a-tag class="status-tag" :color="STATUS_META[(record.status as CardStatus)]?.tagColor || 'default'">
              {{ STATUS_META[(record.status as CardStatus)]?.text || record.status }}
            </a-tag>
          </template>

          <!-- 时长 -->
          <template v-else-if="column.key === 'duration'">
            <span :class="['duration', { 'is-timeout': isTimeoutRecord(record) }]">
              <WarningFilled v-if="isTimeoutRecord(record)" class="dur-icon" />
              <ClockCircleOutlined v-else class="dur-icon" />
              {{ formatDuration(record) }}
            </span>
          </template>

          <!-- 操作 -->
          <template v-else-if="column.key === 'action'">
            <div class="row-actions">
              <a-button type="link" size="small" @click="handleView(record)">
                <EyeOutlined /> 详情
              </a-button>
              <a-button
                v-if="record.status === 'active'"
                type="link"
                size="small"
                @click="handleUrge(record)"
              >
                <BellOutlined /> 催办
              </a-button>
              <a-button
                v-if="record.status === 'active'"
                type="link"
                size="small"
                @click="handleTransfer(record)"
              >
                <SwapOutlined /> 转办
              </a-button>
              <a-button
                v-if="record.status === 'active'"
                type="link"
                size="small"
                danger
                @click="handleVoid(record)"
              >
                <StopOutlined /> 强制终止
              </a-button>
            </div>
          </template>
        </template>

        <!-- 展开行：当前节点全部审批人 + 进度 -->
        <template #expandedRowRender="{ record }">
          <div class="expand-pane">
            <template v-if="detailLoading[record.id]">
              <a-spin />
            </template>
            <template v-else-if="detailCache[record.id]">
              <div class="expand-stages">
                <div
                  v-for="stage in detailCache[record.id].stageInstances"
                  :key="stage.id"
                  class="expand-stage"
                  :class="{ active: stage.id === detailCache[record.id].currentStageInstanceId }"
                >
                  <div class="expand-stage-head">
                    <span class="es-name">{{ stage.stageName }}</span>
                    <a-tag :color="stage.status === 'completed' ? 'success' : stage.status === 'active' ? 'processing' : 'default'">
                      {{ stage.status }}
                    </a-tag>
                    <span v-if="stage.isTimeout" class="es-timeout">
                      <WarningFilled /> 超时
                    </span>
                  </div>
                  <div class="expand-assignees">
                    <div v-for="a in stage.assignees" :key="a.id" class="assignee">
                      <a-avatar
                        :size="24"
                        class="assignee-avatar"
                        :class="{ done: a.status === 'completed' }"
                      >
                        {{ avatarText(a.userName) }}
                      </a-avatar>
                      <span class="assignee-name">{{ a.userName }}</span>
                      <a-tag
                        :color="a.status === 'completed' ? 'success' : a.status === 'pending' ? 'processing' : 'default'"
                      >
                        {{ a.status }}
                      </a-tag>
                      <span v-if="a.completedTime" class="assignee-time">
                        {{ formatTime(a.completedTime) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </template>
            <template v-else>
              <span class="text-muted">暂无详情</span>
            </template>
          </div>
        </template>
      </a-table>
    </div>

    <!-- 催办 Modal -->
    <a-modal
      v-model:open="urgeModal.visible"
      title="催办"
      :confirm-loading="urgeModal.processing"
      :width="440"
      @ok="doUrge"
    >
      <a-form layout="vertical" style="margin-top: 8px;">
        <a-form-item label="催办理由" required>
          <a-textarea
            v-model:value="urgeModal.reason"
            :rows="3"
            placeholder="请填写催办理由"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 转办 Modal -->
    <a-modal
      v-model:open="transferModal.visible"
      title="转办"
      :confirm-loading="transferModal.processing"
      :width="440"
      @ok="doTransfer"
    >
      <a-form layout="vertical" style="margin-top: 8px;">
        <a-form-item label="目标人 (用户ID)" required>
          <a-input-number
            v-model:value="transferModal.newUserId"
            placeholder="请输入目标用户ID"
            style="width: 100%"
            :min="1"
          />
        </a-form-item>
        <a-form-item label="转办说明">
          <a-textarea
            v-model:value="transferModal.opinion"
            :rows="3"
            placeholder="请输入转办说明（可选）"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<style scoped lang="scss">
.page-container.monitor-page {
  padding: 0;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}

.monitor-body {
  padding: 16px;
}

.timeout-alert {
  margin-bottom: 16px;
  border-radius: 8px;
}

/* ============= 统计卡片：极简编辑式监控仪表 ============= */

.stat-grid {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
}

.stat-card {
  position: relative;
  flex: 1;
  height: 80px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px 16px 12px 22px;
  cursor: pointer;
  display: flex;
  align-items: center;
  transition:
    transform 0.18s ease,
    box-shadow 0.18s ease,
    border-color 0.18s ease,
    background 0.18s ease;

  /* 左侧 3px 状态色装饰条（默认半透明，hover/选中变实色） */
  .stat-accent {
    position: absolute;
    left: 0;
    top: 12px;
    bottom: 12px;
    width: 3px;
    background: var(--stat-color);
    border-radius: 0 2px 2px 0;
    opacity: 0.35;
    transition: opacity 0.18s ease;
  }

  &:hover {
    border-color: var(--stat-color);
    box-shadow: var(--shadow-sm);
    transform: translateY(-1px);
    .stat-accent {
      opacity: 1;
    }
  }

  &.is-active {
    border-color: var(--stat-color);
    background: var(--bg-muted);
    box-shadow: var(--shadow-sm);
    .stat-accent {
      opacity: 1;
      width: 4px;
    }
  }

  &.is-danger.is-active {
    background: linear-gradient(180deg, color-mix(in srgb, var(--color-warning) 6%, transparent) 0%, var(--bg-card) 100%);
  }
}

.stat-meta {
  display: flex;
  flex-direction: column;
  gap: 2px;
  width: 100%;
}

.stat-label {
  font-size: 12px;
  color: var(--text-3);
  letter-spacing: 0.04em;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.stat-warn-icon {
  color: var(--color-warning-text);
  font-size: 12px;
}

.stat-value {
  font-size: 28px;
  font-weight: 500;
  line-height: 1.1;
  color: var(--stat-color);
  font-variant-numeric: tabular-nums;
  font-feature-settings: 'tnum';
  letter-spacing: -0.02em;
}

/* ============= 列表 ============= */

.monitor-table {
  :deep(.ant-table) {
    background: var(--bg-card);
    border-radius: 8px;
  }
  :deep(.ant-table-thead > tr > th) {
    background: var(--bg-muted);
    font-weight: 500;
    color: var(--text-2);
  }
  :deep(.row-actions) {
    opacity: 0.65;
    transition: opacity 0.15s ease;
  }
  :deep(.ant-table-row:hover .row-actions) {
    opacity: 1;
  }
}

.row-title {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.mono {
  font-family: 'SF Mono', Menlo, Consolas, 'Courier New', monospace;
  font-size: 13px;
  color: var(--text-2);
  font-variant-numeric: tabular-nums;
}

.stage-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.stage-name {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.stage-avatars :deep(.ant-avatar) {
  background: var(--color-info-light);
  color: var(--color-info);
  font-size: 11px;
  border: 1px solid var(--bg-card);
}

.stage-avatar.done {
  background: var(--color-success-light) !important;
  color: var(--color-success-text) !important;
}

.status-tag {
  font-weight: 500;
  border: none;
}

.duration {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--text-3);
  font-variant-numeric: tabular-nums;
  .dur-icon {
    font-size: 12px;
  }
  &.is-timeout {
    color: var(--color-warning-text);
    font-weight: 500;
  }
}

.row-actions {
  display: inline-flex;
  align-items: center;
  flex-wrap: nowrap;
  :deep(.ant-btn-link) {
    padding-inline: 6px;
  }
}

.text-muted {
  color: var(--text-3);
}

/* ============= 展开行 ============= */

.expand-pane {
  padding: 8px 4px 4px;
}

.expand-stages {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.expand-stage {
  background: var(--bg-muted);
  border-left: 3px solid var(--border);
  padding: 10px 14px;
  border-radius: 4px;
  &.active {
    background: var(--bg-muted);
    border-left-color: var(--color-primary);
  }
}

.expand-stage-head {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
  .es-name {
    font-weight: 500;
    color: var(--text-1);
  }
  .es-timeout {
    color: var(--color-warning-text);
    font-size: 12px;
    display: inline-flex;
    align-items: center;
    gap: 3px;
  }
}

.expand-assignees {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
}

.assignee {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 16px;
  font-size: 12px;

  .assignee-avatar {
    background: var(--color-info-light);
    color: var(--color-info);
    font-size: 11px;
    &.done {
      background: var(--color-success-light);
      color: var(--color-success-text);
    }
  }
  .assignee-name {
    color: var(--text-1);
  }
  .assignee-time {
    color: var(--text-3);
    font-variant-numeric: tabular-nums;
  }
}
</style>
