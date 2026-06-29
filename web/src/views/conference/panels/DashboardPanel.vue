<template>
  <div class="dashboard-panel">
    <a-spin :spinning="loading">
      <!-- 顶部 KPI 指标区 -->
      <a-row :gutter="16" class="kpi-section">
        <a-col :span="3">
          <StatCard
            title="参会人数"
            :value="`${dash.confirmedAttendees}/${dash.totalAttendees}`"
            :sub-value="attendeeRate"
            color="var(--color-info)"
            @click="navigateTo('attendee')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="日程完成度"
            :value="`${completedSchedules}/${dash.totalSchedules}`"
            color="var(--color-info)"
            @click="navigateTo('schedule')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="接送安排率"
            :value="pickupRate + '%'"
            :progress="pickupRate"
            @click="navigateTo('transport')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="住宿安排率"
            :value="accommodationRate + '%'"
            :progress="accommodationRate"
            @click="navigateTo('accommodation')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="车辆利用率"
            :value="vehicleUsageRate + '%'"
            :sub-value="`${usedVehicles}/${dash.totalVehicles}`"
            @click="navigateTo('vehicle-schedule')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="物品到位率"
            :value="materialRate + '%'"
            :progress="materialRate"
            @click="navigateTo('material')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="收入总额"
            :value="formatMoney(dash.totalIncome)"
            :sub-value="incomeRateText"
            color="var(--color-success)"
            @click="navigateTo('finance')"
          />
        </a-col>
        <a-col :span="3">
          <StatCard
            title="异常告警"
            :value="dash.alertCount"
            :sub-value="alertSummaryText"
            :color="dash.alertCount > 0 ? 'var(--color-danger)' : 'var(--color-success)'"
            @click="scrollToAlerts"
          />
        </a-col>
      </a-row>

      <!-- 中部双列信息区 -->
      <a-row :gutter="16" class="info-section">
        <!-- 左列 -->
        <a-col :span="14">
          <a-card title="进度总览" class="dash-card" :body-style="{ padding: '16px 24px' }">
            <div class="progress-list">
              <div v-for="item in progressItems" :key="item.label" class="progress-item">
                <div class="progress-label">{{ item.label }}</div>
                <a-progress
                  :percent="item.percent"
                  :stroke-color="progressColor(item.percent)"
                  :format="() => item.percent + '%'"
                  style="flex: 1"
                />
              </div>
            </div>
          </a-card>

          <a-card title="近期日程" class="dash-card" style="margin-top: 16px" :body-style="{ padding: '16px 24px' }">
            <TimelineView :items="recentScheduleItems" highlight-today />
          </a-card>
        </a-col>

        <!-- 右列 -->
        <a-col :span="10">
          <a-card title="异常告警" class="dash-card" ref="alertCardRef" :body-style="{ padding: '12px 16px' }">
            <template v-if="allAlerts.length === 0">
              <EmptyState size="small" title="一切正常，无异常告警" />
            </template>
            <a-collapse v-else v-model:activeKey="alertActiveKeys" ghost>
              <a-collapse-panel
                v-if="alertsByLevel.critical.length"
                key="critical"
                :header="`严重 (${alertsByLevel.critical.length})`"
              >
                <div
                  v-for="(alert, idx) in alertsByLevel.critical.slice(0, 5)"
                  :key="idx"
                  class="alert-item alert-item--critical"
                >
                  <div class="alert-item__title">{{ alert.title }}</div>
                  <div v-if="alert.detail" class="alert-item__detail">{{ alert.detail }}</div>
                </div>
                <a-button
                  v-if="alertsByLevel.critical.length > 5"
                  type="link"
                  size="small"
                >查看全部 {{ alertsByLevel.critical.length }} 条</a-button>
              </a-collapse-panel>
              <a-collapse-panel
                v-if="alertsByLevel.warning.length"
                key="warning"
                :header="`警告 (${alertsByLevel.warning.length})`"
              >
                <div
                  v-for="(alert, idx) in alertsByLevel.warning.slice(0, 5)"
                  :key="idx"
                  class="alert-item alert-item--warning"
                >
                  <div class="alert-item__title">{{ alert.title }}</div>
                  <div v-if="alert.detail" class="alert-item__detail">{{ alert.detail }}</div>
                </div>
                <a-button
                  v-if="alertsByLevel.warning.length > 5"
                  type="link"
                  size="small"
                >查看全部 {{ alertsByLevel.warning.length }} 条</a-button>
              </a-collapse-panel>
              <a-collapse-panel
                v-if="alertsByLevel.info.length"
                key="info"
                :header="`提示 (${alertsByLevel.info.length})`"
              >
                <div
                  v-for="(alert, idx) in alertsByLevel.info.slice(0, 5)"
                  :key="idx"
                  class="alert-item alert-item--info"
                >
                  <div class="alert-item__title">{{ alert.title }}</div>
                  <div v-if="alert.detail" class="alert-item__detail">{{ alert.detail }}</div>
                </div>
                <a-button
                  v-if="alertsByLevel.info.length > 5"
                  type="link"
                  size="small"
                >查看全部 {{ alertsByLevel.info.length }} 条</a-button>
              </a-collapse-panel>
            </a-collapse>
          </a-card>

          <a-card title="快捷操作" class="dash-card" style="margin-top: 16px" :body-style="{ padding: '16px' }">
            <div class="quick-actions">
              <a-button
                v-for="action in quickActions"
                :key="action.key"
                :loading="actionLoading[action.key]"
                @click="handleQuickAction(action)"
              >
                <template #icon><component :is="action.icon" /></template>
                {{ action.label }}
              </a-button>
            </div>
          </a-card>
        </a-col>
      </a-row>

      <!-- 底部活动时间线 -->
      <div v-if="eventDays.length > 0" class="timeline-section">
        <a-card title="活动日程" class="dash-card" :body-style="{ padding: '12px 16px' }">
          <div class="date-tags">
            <a-badge v-for="day in eventDays" :key="day.date" :count="day.scheduleCount" :offset="[-4, -4]">
              <a-tag
                :color="day.isToday ? 'var(--color-primary)' : 'default'"
                class="date-tag"
              >{{ day.label }}</a-tag>
            </a-badge>
          </div>
        </a-card>
      </div>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive } from 'vue'
import { message } from 'ant-design-vue'
import {
  UserAddOutlined,
  CalendarOutlined,
  CarOutlined,
  HomeOutlined,
  AppstoreOutlined,
  ScheduleOutlined,
  ExportOutlined,
  ThunderboltOutlined,
  ReloadOutlined,
} from '@ant-design/icons-vue'
import StatCard from '../components/StatCard.vue'
import TimelineView from '../components/TimelineView.vue'
import type { TimelineItem } from '../components/TimelineView.vue'
import {
  getEventDashboard,
  getEventAlerts,
  getSchedules,
  autoGeneratePickups,
  autoAssignAccommodation,
  autoArrangeTables,
  autoGenerateMealPlans,
  generateVehicleSchedules,
  exportAttendees,
} from '@/api/conference'
import type { DashboardDto, AlertItemDto, ScheduleListItemDto } from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

const emit = defineEmits<{
  (e: 'navigate', panel: string): void
}>()

// ==================== 状态 ====================
const loading = ref(false)
const dashboardData = ref<DashboardDto | null>(null)
const allAlerts = ref<AlertItemDto[]>([])
const schedules = ref<ScheduleListItemDto[]>([])
const alertActiveKeys = ref<string[]>(['critical', 'warning'])
const alertCardRef = ref<any>(null)
const actionLoading = reactive<Record<string, boolean>>({})

// ==================== 计算属性 ====================
const dash = computed<DashboardDto>(() => dashboardData.value || {
  totalAttendees: 0, confirmedAttendees: 0, totalSchedules: 0,
  totalVehicles: 0, totalPickupTasks: 0, pendingPickupTasks: 0,
  usedVehicles: 0, arrangedPassengers: 0, totalPassengers: 0,
  totalHotels: 0, totalRooms: 0, assignedRooms: 0,
  totalMealPlans: 0, totalMaterials: 0, receivedMaterials: 0,
  totalIncome: 0, budget: 0, alertCount: 0,
})

const attendeeRate = computed(() => {
  if (!dash.value.totalAttendees) return '确认率 0%'
  return `确认率 ${Math.round(dash.value.confirmedAttendees / dash.value.totalAttendees * 100)}%`
})

const completedSchedules = computed(() => {
  const now = new Date()
  return schedules.value.filter(s => new Date(s.date + 'T' + s.endTime) < now).length
})

const pickupRate = computed(() => {
  if (!dash.value.totalPassengers) return 0
  return Math.round(dash.value.arrangedPassengers / dash.value.totalPassengers * 100)
})

const accommodationRate = computed(() => {
  if (!dash.value.totalRooms) return 0
  return Math.round(dash.value.assignedRooms / dash.value.totalRooms * 100)
})

const vehicleUsageRate = computed(() => {
  if (!dash.value.totalVehicles) return 0
  return Math.round(usedVehicles.value / dash.value.totalVehicles * 100)
})

const usedVehicles = computed(() => {
  return dash.value.usedVehicles ?? 0
})

const materialRate = computed(() => {
  if (!dash.value.totalMaterials) return 0
  return Math.round(dash.value.receivedMaterials / dash.value.totalMaterials * 100)
})

const incomeRateText = computed(() => {
  if (!dash.value.budget) return ''
  return `缴费率 ${Math.round(dash.value.totalIncome / dash.value.budget * 100)}%`
})

const alertSummaryText = computed(() => {
  const c = alertsByLevel.value.critical.length
  const w = alertsByLevel.value.warning.length
  const i = alertsByLevel.value.info.length
  const parts: string[] = []
  if (c) parts.push(`${c}红`)
  if (w) parts.push(`${w}黄`)
  if (i) parts.push(`${i}蓝`)
  return parts.join('/') || '无'
})

const alertsByLevel = computed(() => ({
  critical: allAlerts.value.filter(a => a.level === 'Critical'),
  warning: allAlerts.value.filter(a => a.level === 'Warning'),
  info: allAlerts.value.filter(a => a.level === 'Info'),
}))

const progressItems = computed(() => [
  { label: '接送安排', percent: pickupRate.value },
  { label: '住宿分配', percent: accommodationRate.value },
  { label: '餐食安排', percent: dash.value.totalMealPlans ? 100 : 0 },
  { label: '桌次编排', percent: 0 }, // 需要更多数据支持
  { label: '物品到位', percent: materialRate.value },
  { label: '车辆日程', percent: vehicleUsageRate.value },
])

const recentScheduleItems = computed<TimelineItem[]>(() => {
  const today = new Date().toISOString().slice(0, 10)
  const tomorrow = new Date(Date.now() + 86400000).toISOString().slice(0, 10)
  return schedules.value
    .filter(s => s.date === today || s.date === tomorrow)
    .sort((a, b) => (a.date + a.startTime).localeCompare(b.date + b.startTime))
    .map(s => ({
      time: `${s.date} ${s.startTime}-${s.endTime}`,
      title: s.title,
      location: s.location,
      type: s.type,
      count: s.attendeeCount,
    }))
})

const eventDays = computed(() => {
  if (!props.eventData?.startDate || !props.eventData?.endDate) return []
  const start = new Date(props.eventData.startDate)
  const end = new Date(props.eventData.endDate)
  const today = new Date().toISOString().slice(0, 10)
  const days: { date: string; label: string; isToday: boolean; scheduleCount: number }[] = []
  const cur = new Date(start)
  while (cur <= end) {
    const dateStr = cur.toISOString().slice(0, 10)
    const count = schedules.value.filter(s => s.date === dateStr).length
    days.push({
      date: dateStr,
      label: `${cur.getMonth() + 1}/${cur.getDate()}`,
      isToday: dateStr === today,
      scheduleCount: count,
    })
    cur.setDate(cur.getDate() + 1)
  }
  return days
})

// ==================== 快捷操作 ====================
const quickActions = [
  { key: 'addAttendee', label: '新增人员', icon: UserAddOutlined, action: () => navigateTo('attendee') },
  { key: 'addSchedule', label: '新增日程', icon: CalendarOutlined, action: () => navigateTo('schedule') },
  { key: 'autoPickup', label: '一键排接送', icon: CarOutlined, action: async () => { await autoGeneratePickups(props.eventId); message.success('接送任务已生成'); await loadDashboard() } },
  { key: 'autoRoom', label: '一键分房', icon: HomeOutlined, action: async () => { await autoAssignAccommodation(props.eventId); message.success('住宿已自动分配'); await loadDashboard() } },
  { key: 'autoTable', label: '一键排桌', icon: AppstoreOutlined, action: async () => { message.info('请前往餐食管理选择具体餐次进行排桌'); navigateTo('meal') } },
  { key: 'genVehicle', label: '生成车辆日程', icon: ScheduleOutlined, action: async () => { await generateVehicleSchedules(props.eventId); message.success('车辆日程已生成'); await loadDashboard() } },
  { key: 'exportRoster', label: '导出花名册', icon: ExportOutlined, action: async () => { const blob = await exportAttendees(props.eventId) as any; downloadBlob(blob, '花名册.xlsx') } },
  { key: 'fullArrange', label: '全量编排', icon: ThunderboltOutlined, action: async () => { await runFullArrange() } },
  { key: 'refresh', label: '刷新数据', icon: ReloadOutlined, action: async () => { await loadDashboard() } },
]

// ==================== 方法 ====================
function navigateTo(panel: string) {
  emit('navigate', panel)
}

function scrollToAlerts() {
  alertCardRef.value?.$el?.scrollIntoView({ behavior: 'smooth' })
}

function progressColor(percent: number): string {
  if (percent >= 100) return 'var(--color-success)'
  if (percent >= 50) return 'var(--color-info)'
  return 'var(--color-warning)'
}

function formatMoney(amount: number): string {
  if (amount >= 10000) return (amount / 10000).toFixed(1) + '万'
  return amount.toLocaleString()
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

async function handleQuickAction(action: { key: string; action: () => Promise<void> | void }) {
  actionLoading[action.key] = true
  try {
    await action.action()
  } catch (err: any) {
    message.error(err?.message || '操作失败')
  } finally {
    actionLoading[action.key] = false
  }
}

async function runFullArrange() {
  message.loading({ content: '正在执行全量编排...', key: 'fullArrange', duration: 0 })
  try {
    await autoGeneratePickups(props.eventId)
    await autoAssignAccommodation(props.eventId)
    await autoGenerateMealPlans(props.eventId)
    await generateVehicleSchedules(props.eventId)
    message.success({ content: '全量编排完成', key: 'fullArrange' })
    await loadDashboard()
  } catch (err: any) {
    message.error({ content: err?.message || '全量编排失败', key: 'fullArrange' })
    throw err
  }
}

const loadDashboard = async () => {
  loading.value = true
  try {
    const [dashData, alerts, scheduleData] = await Promise.all([
      getEventDashboard(props.eventId),
      getEventAlerts(props.eventId),
      getSchedules(props.eventId),
    ])
    dashboardData.value = dashData as any
    const alertList = (alerts as any)?.items ?? alerts ?? []
    allAlerts.value = alertList
    const scheduleList = (scheduleData as any)?.items ?? scheduleData ?? []
    schedules.value = scheduleList
  } catch (err: any) {
    message.error('加载看板数据失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadDashboard()
})
</script>

<style scoped lang="scss">
.dashboard-panel {
  padding: 0;
}

.kpi-section {
  margin-bottom: 24px;
}

.info-section {
  margin-bottom: 16px;
}

.dash-card {
  border-radius: 8px;
  transition: box-shadow 0.3s;

  &:hover {
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
  }
}

.progress-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.progress-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.progress-label {
  width: 80px;
  flex-shrink: 0;
  font-size: 14px;
  color: #595959;
}

.alert-item {
  padding: 8px 12px;
  margin-bottom: 8px;
  border-radius: 4px;
  background: #fafafa;

  &--critical {
    border-left: 3px solid var(--color-danger);
  }

  &--warning {
    border-left: 3px solid var(--color-warning);
  }

  &--info {
    border-left: 3px solid var(--color-info);
  }

  &__title {
    font-size: 14px;
    color: #262626;
  }

  &__detail {
    font-size: 12px;
    color: #8c8c8c;
    margin-top: 4px;
  }
}

.quick-actions {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;

  :deep(.ant-btn) {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 4px;
  }
}

.timeline-section {
  margin-top: 16px;
}

.date-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.date-tag {
  cursor: default;
  font-size: 14px;
  padding: 4px 12px;
}
</style>
