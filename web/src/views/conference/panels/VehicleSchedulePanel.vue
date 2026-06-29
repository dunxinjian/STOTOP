<template>
  <div class="vehicle-schedule-panel">
    <!-- 0. 车辆管理折叠区域 -->
    <a-collapse v-model:activeKey="vehicleMgmtActiveKey" style="margin-bottom: 16px;">
      <a-collapse-panel key="vehicles" header="车辆管理">
        <template #extra>
          <a-button type="primary" size="small" @click.stop="handleAddVehicle">
            <template #icon><PlusOutlined /></template>
            新增车辆
          </a-button>
        </template>
        <a-table
          :columns="vehicleColumns"
          :data-source="vehicles"
          :loading="vehicleLoading"
          :pagination="false"
          size="small"
          row-key="id"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'source'">
              <a-tag>{{ record.source || '-' }}</a-tag>
            </template>
            <template v-else-if="column.key === 'vehicleAction'">
              <a-space>
                <a @click="handleEditVehicle(record as VehicleListItemDto)">编辑</a>
                <a-popconfirm title="确定删除该车辆？" @confirm="handleDeleteVehicle(record.id)">
                  <a style="color: var(--color-danger);">删除</a>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-collapse-panel>
    </a-collapse>

    <!-- 1. 智能操作栏 -->
    <SmartActionBar description="根据接送任务自动推导车辆日程和司机计划">
      <a-button type="primary" @click="handleGenerate" :loading="generating">
        <ThunderboltOutlined />推导车辆日程
      </a-button>
      <a-button @click="showAddDialog">
        <PlusOutlined />添加活动用车
      </a-button>
      <a-button @click="showNotificationModal">
        <MessageOutlined />生成司机通知
      </a-button>
    </SmartActionBar>

    <!-- 2. 统计栏 -->
    <a-row :gutter="16" style="margin: 16px 0">
      <a-col :span="8">
        <StatCard title="已安排车辆" :value="scheduledVehicles" :clickable="false" />
      </a-col>
      <a-col :span="8">
        <StatCard title="总车辆数" :value="totalVehicles" :clickable="false" />
      </a-col>
      <a-col :span="8">
        <StatCard title="今日任务数" :value="todayTaskCount" :clickable="false" />
      </a-col>
    </a-row>

    <!-- 3. 三视图切换 -->
    <a-spin :spinning="loading">
      <a-tabs v-model:activeKey="activeTab" type="card">
        <!-- Tab 1: 甘特图视图 -->
        <a-tab-pane key="gantt" tab="甘特图视图">
          <GanttChart
            :rows="ganttRows"
            :tasks="ganttTasks"
            :start-date="eventStartDate"
            :end-date="eventEndDate"
            @task-click="handleTaskClick"
          />
        </a-tab-pane>

        <!-- Tab 2: 列表视图 -->
        <a-tab-pane key="list" tab="列表视图">
          <a-radio-group v-model:value="selectedDate" style="margin-bottom: 16px">
            <a-radio-button v-for="date in dateDays" :key="date" :value="date">
              {{ formatDateShort(date) }}
            </a-radio-button>
          </a-radio-group>
          <a-table
            :columns="listColumns"
            :data-source="schedulesByDate"
            :pagination="false"
            row-key="id"
            size="middle"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'taskType'">
                <a-tag :color="taskTypeTagColor(record.taskType)">{{ record.taskType || '-' }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'timeRange'">
                {{ record.startTime?.substring(11, 16) }} - {{ record.endTime?.substring(11, 16) }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-popconfirm title="确定删除此条日程？" @confirm="handleDelete(record.id)">
                  <a-button type="link" danger size="small">删除</a-button>
                </a-popconfirm>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- Tab 3: 司机卡片视图 -->
        <a-tab-pane key="cards" tab="司机卡片">
          <a-row :gutter="[16, 16]">
            <a-col v-for="driver in driverCards" :key="`${driver.vehicleId}-${driver.date}`" :span="12">
              <a-card :bordered="false" class="driver-card">
                <template #title>
                  <div style="display: flex; justify-content: space-between; align-items: center">
                    <span><CarOutlined /> {{ driver.plateNumber }} - {{ driver.driverName }}</span>
                    <span style="font-size: 12px; color: #8c8c8c">{{ driver.driverPhone }}</span>
                  </div>
                </template>
                <div class="driver-stats">
                  <a-tag>{{ driver.date }}</a-tag>
                  <a-tag color="blue">{{ driver.totalTrips }} 趟</a-tag>
                  <a-tag color="green">{{ formatMinutes(driver.totalWorkMinutes) }}</a-tag>
                </div>
                <a-divider style="margin: 8px 0" />
                <a-timeline>
                  <a-timeline-item
                    v-for="task in driver.tasks"
                    :key="task.startTime"
                    :color="taskTypeColor(task.taskType)"
                  >
                    <strong>{{ task.startTime?.substring(11, 16) }} - {{ task.endTime?.substring(11, 16) }}</strong>
                    <div>{{ task.taskType }}: {{ task.origin }} → {{ task.destination }}</div>
                  </a-timeline-item>
                </a-timeline>
              </a-card>
            </a-col>
          </a-row>
          <a-empty v-if="!driverCards.length && !loading" description="暂无司机卡片数据" style="padding: 40px 0" />
        </a-tab-pane>
      </a-tabs>
    </a-spin>

    <!-- 4. 导出操作区 -->
    <a-divider />
    <a-space>
      <a-button @click="handleExportPdf" :loading="exportingPdf">
        <FilePdfOutlined />导出日程表PDF
      </a-button>
      <a-button @click="handleExportDriverCards" :loading="exportingCards">
        <IdcardOutlined />导出司机卡
      </a-button>
    </a-space>

    <!-- 添加活动用车弹窗 -->
    <a-modal
      v-model:open="addDialogVisible"
      title="添加活动用车"
      :width="620"
      @ok="handleAddSubmit"
      :confirm-loading="addSubmitting"
      :destroy-on-close="true"
    >
      <a-form :model="addForm" :label-col="{ span: 8 }" :wrapper-col="{ span: 16 }" class="add-vehicle-form">
        <!-- 选中车辆后显示信息 -->
        <div v-if="selectedVehicleInfo" style="margin-bottom: 16px; padding: 8px 12px; background: #f5f5f5; border-radius: 4px; font-size: 13px; color: #666;">
          <span>车牌: {{ selectedVehicleInfo.plateNumber }}</span>
          <a-divider type="vertical" />
          <span>车型: {{ selectedVehicleInfo.vehicleType || '-' }}</span>
          <a-divider type="vertical" />
          <span>座位: {{ selectedVehicleInfo.seatCount }}</span>
          <a-divider type="vertical" />
          <span>司机: {{ selectedVehicleInfo.driverName || '-' }} {{ selectedVehicleInfo.driverPhone || '' }}</span>
        </div>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="选择车辆" required>
              <a-select
                v-model:value="addForm.vehicleId"
                placeholder="请选择车辆"
                show-search
                :filter-option="filterVehicleOption"
              >
                <a-select-option v-for="v in vehicles" :key="v.id" :value="v.id">
                  {{ v.plateNumber }} - {{ v.vehicleType || '未知车型' }} ({{ v.seatCount }}座)
                  <template v-if="v.driverName"> | 司机: {{ v.driverName }}</template>
                </a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="任务类型" required>
              <a-select v-model:value="addForm.taskType" placeholder="请选择任务类型">
                <a-select-option value="活动用车">活动用车</a-select-option>
                <a-select-option value="接送">接送</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="日期" required>
              <a-date-picker v-model:value="addForm.date" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="乘客数">
              <a-input-number v-model:value="addForm.passengerCount" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="开始时间" required>
              <a-time-picker v-model:value="addForm.startTime" style="width: 100%" format="HH:mm" value-format="HH:mm" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结束时间" required>
              <a-time-picker v-model:value="addForm.endTime" style="width: 100%" format="HH:mm" value-format="HH:mm" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="出发地">
              <a-input v-model:value="addForm.origin" placeholder="请输入出发地" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="目的地">
              <a-input v-model:value="addForm.destination" placeholder="请输入目的地" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row>
          <a-col :span="24">
            <a-form-item label="备注" :label-col="{ span: 4 }" :wrapper-col="{ span: 20 }">
              <a-textarea v-model:value="addForm.remark" :rows="2" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </a-modal>

    <!-- 新增/编辑车辆弹窗 -->
    <a-modal
      v-model:open="showVehicleModal"
      :title="editingVehicleId ? '编辑车辆' : '新增车辆'"
      :width="560"
      @ok="handleSaveVehicle"
      :confirmLoading="vehicleSaving"
      :destroy-on-close="true"
    >
      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 17 }" style="margin-top: 16px">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="车牌号" required>
              <a-input v-model:value="vehicleForm.plateNumber" placeholder="如：粤A00001" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="车型">
              <a-input v-model:value="vehicleForm.vehicleType" placeholder="如：7座商务车" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="座位数" required>
              <a-input-number v-model:value="vehicleForm.seatCount" :min="1" :max="100" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="来源">
              <a-select v-model:value="vehicleForm.source" placeholder="请选择" allowClear>
                <a-select-option value="公司车">公司车</a-select-option>
                <a-select-option value="租赁">租赁</a-select-option>
                <a-select-option value="员工车">员工车</a-select-option>
                <a-select-option value="其他">其他</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="司机姓名">
              <a-input v-model:value="vehicleForm.driverName" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="司机电话">
              <a-input v-model:value="vehicleForm.driverPhone" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注" :label-col="{ span: 3 }" :wrapper-col="{ span: 20 }">
          <a-textarea v-model:value="vehicleForm.remark" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 司机通知弹窗 -->
    <a-modal
      v-model:open="notificationModalVisible"
      title="生成司机通知"
      :width="700"
      :footer="null"
    >
      <div style="margin-bottom: 16px">
        <a-space>
          <span>选择日期：</span>
          <a-date-picker v-model:value="notificationDate" value-format="YYYY-MM-DD" @change="loadNotifications" />
          <a-button type="primary" :loading="loadingNotifications" @click="loadNotifications">查询</a-button>
        </a-space>
      </div>

      <a-spin :spinning="loadingNotifications">
        <div v-if="driverNotifications.length === 0" style="text-align: center; padding: 40px; color: #999">
          该日期暂无司机任务
        </div>
        <div v-else>
          <div style="margin-bottom: 12px">
            <a-checkbox v-model:checked="selectAllDrivers" @change="handleSelectAllDrivers">全选</a-checkbox>
            <a-button style="margin-left: 12px" @click="copyAllSelected"><CopyOutlined />复制已选</a-button>
          </div>

          <a-collapse v-model:activeKey="expandedDrivers">
            <a-collapse-panel v-for="item in driverNotifications" :key="String(item.driverVehicleId)" :header="`${item.driverName} (${item.plateNumber}) - ${item.taskCount}个任务`">
              <template #extra>
                <a-checkbox :checked="selectedDriverIds.includes(item.driverVehicleId)" @click.stop @change="(e: any) => toggleDriver(item.driverVehicleId, e.target.checked)" />
                <a-button size="small" style="margin-left: 8px" @click.stop="copySingle(item.message)"><CopyOutlined /></a-button>
              </template>
              <pre style="white-space: pre-wrap; font-size: 13px; background: #f5f5f5; padding: 12px; border-radius: 6px; max-height: 400px; overflow-y: auto">{{ item.message }}</pre>
            </a-collapse-panel>
          </a-collapse>
        </div>
      </a-spin>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  ThunderboltOutlined,
  PlusOutlined,
  CarOutlined,
  FilePdfOutlined,
  IdcardOutlined,
  MessageOutlined,
  CopyOutlined,
} from '@ant-design/icons-vue'
import SmartActionBar from '../components/SmartActionBar.vue'
import StatCard from '../components/StatCard.vue'
import GanttChart from '../components/GanttChart.vue'
import type { GanttRow, GanttTask } from '../components/GanttChart.vue'
import {
  getVehicleSchedules,
  generateVehicleSchedules,
  getDriverCards as fetchDriverCards,
  exportVehicleSchedulePdf,
  addVehicleTask,
  deleteVehicleSchedule,
  getDriverNotifications,
  getVehicles,
  createVehicle,
  updateVehicle,
  deleteVehicle,
} from '@/api/conference'
import type {
  VehicleScheduleDto,
  DriverCardDto,
  AddVehicleTaskRequest,
  DriverNotificationDto,
  VehicleListItemDto,
  CreateVehicleRequest,
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

// State
const loading = ref(false)
const generating = ref(false)
const exportingPdf = ref(false)
const exportingCards = ref(false)
const activeTab = ref('gantt')
const vehicleSchedules = ref<VehicleScheduleDto[]>([])
const driverCards = ref<DriverCardDto[]>([])
const selectedDate = ref('')

// 车辆列表
const vehicles = ref<VehicleListItemDto[]>([])
const vehicleLoading = ref(false)

const loadVehicles = async () => {
  vehicleLoading.value = true
  try {
    const res = await getVehicles(props.eventId) as any
    vehicles.value = res?.items ?? (Array.isArray(res) ? res : [])
  } catch {
    // ignore
  } finally {
    vehicleLoading.value = false
  }
}

// 车辆管理
const vehicleMgmtActiveKey = ref<string[]>([])
const vehicleSaving = ref(false)
const showVehicleModal = ref(false)
const editingVehicleId = ref<number | null>(null)
const vehicleForm = ref<CreateVehicleRequest>({
  plateNumber: '',
  vehicleType: '',
  seatCount: 7,
  driverName: '',
  driverPhone: '',
  source: '',
  remark: '',
})

const vehicleColumns = [
  { title: '车牌号', dataIndex: 'plateNumber', width: 120 },
  { title: '车型', dataIndex: 'vehicleType', width: 120, customRender: ({ text }: any) => text || '-' },
  { title: '座位数', dataIndex: 'seatCount', width: 80, align: 'center' as const },
  { title: '司机姓名', dataIndex: 'driverName', width: 100, customRender: ({ text }: any) => text || '-' },
  { title: '司机电话', dataIndex: 'driverPhone', width: 120, customRender: ({ text }: any) => text || '-' },
  { title: '来源', dataIndex: 'source', width: 90 },
  { title: '操作', key: 'vehicleAction', width: 120 },
]

function handleAddVehicle() {
  editingVehicleId.value = null
  vehicleForm.value = {
    plateNumber: '',
    vehicleType: '',
    seatCount: 7,
    driverName: '',
    driverPhone: '',
    source: '',
    remark: '',
  }
  showVehicleModal.value = true
}

function handleEditVehicle(record: VehicleListItemDto) {
  editingVehicleId.value = record.id
  vehicleForm.value = {
    plateNumber: record.plateNumber,
    vehicleType: record.vehicleType || '',
    seatCount: record.seatCount,
    driverName: record.driverName || '',
    driverPhone: record.driverPhone || '',
    source: record.source || '',
    remark: '',
  }
  showVehicleModal.value = true
}

async function handleSaveVehicle() {
  if (!vehicleForm.value.plateNumber) {
    message.warning('请填写车牌号')
    return
  }
  if (!vehicleForm.value.seatCount || vehicleForm.value.seatCount < 1) {
    message.warning('请填写座位数')
    return
  }
  vehicleSaving.value = true
  try {
    if (editingVehicleId.value) {
      await updateVehicle(editingVehicleId.value, vehicleForm.value)
    } else {
      await createVehicle(props.eventId, vehicleForm.value)
    }
    showVehicleModal.value = false
    message.success('保存成功')
    await loadVehicles()
  } catch {
    message.error('保存失败')
  } finally {
    vehicleSaving.value = false
  }
}

async function handleDeleteVehicle(id: number) {
  try {
    await deleteVehicle(id)
    message.success('删除成功')
    await loadVehicles()
    await loadData()
  } catch {
    message.error('删除失败')
  }
}

const filterVehicleOption = (input: string, option: any) => {
  const vehicle = vehicles.value.find(v => v.id === option.value)
  if (!vehicle) return false
  const searchText = `${vehicle.plateNumber} ${vehicle.vehicleType || ''} ${vehicle.driverName || ''}`.toLowerCase()
  return searchText.includes(input.toLowerCase())
}

const selectedVehicleInfo = computed(() => {
  if (!addForm.value.vehicleId) return null
  return vehicles.value.find(v => v.id === addForm.value.vehicleId) || null
})

// Add dialog
const addDialogVisible = ref(false)
const addSubmitting = ref(false)
const addForm = ref<AddVehicleTaskRequest>({
  vehicleId: 0,
  date: '',
  startTime: '',
  endTime: '',
  taskType: '活动用车',
  origin: '',
  destination: '',
  passengerCount: 0,
  remark: '',
})

// 统计
const scheduledVehicles = computed(() => {
  const ids = new Set(vehicleSchedules.value.map(s => s.vehicleId))
  return ids.size
})

const totalVehicles = computed(() => {
  const ids = new Set(vehicleSchedules.value.map(s => s.vehicleId))
  return ids.size
})

const todayTaskCount = computed(() => {
  const today = new Date().toISOString().split('T')[0]
  return vehicleSchedules.value.filter(s => s.date === today).length
})

// 日期列表
const dateDays = computed(() => {
  if (!props.eventData?.startDate || !props.eventData?.endDate) return []
  const days: string[] = []
  const start = new Date(props.eventData.startDate)
  const end = new Date(props.eventData.endDate)
  for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
    days.push(d.toISOString().split('T')[0])
  }
  return days
})

const eventStartDate = computed(() => props.eventData?.startDate || '')
const eventEndDate = computed(() => props.eventData?.endDate || '')

// 列表视图按日期过滤
const schedulesByDate = computed(() => {
  if (!selectedDate.value) return vehicleSchedules.value
  return vehicleSchedules.value.filter(s => s.date === selectedDate.value)
})

const listColumns = [
  { title: '时间段', dataIndex: 'timeRange', key: 'timeRange', width: 140 },
  { title: '任务类型', dataIndex: 'taskType', key: 'taskType', width: 100 },
  { title: '出发地', dataIndex: 'origin', key: 'origin', ellipsis: true },
  { title: '目的地', dataIndex: 'destination', key: 'destination', ellipsis: true },
  { title: '车牌', dataIndex: 'vehiclePlateNumber', key: 'vehiclePlateNumber', width: 120 },
  { title: '司机', dataIndex: 'driverName', key: 'driverName', width: 100 },
  { title: '乘客数', dataIndex: 'passengerCount', key: 'passengerCount', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const },
]

// 甘特图数据
const ganttRows = computed<GanttRow[]>(() => {
  const map = new Map<number, GanttRow>()
  vehicleSchedules.value.forEach(s => {
    if (!map.has(s.vehicleId)) {
      map.set(s.vehicleId, {
        id: s.vehicleId,
        label: s.vehiclePlateNumber || `车辆${s.vehicleId}`,
        subLabel: s.driverName,
      })
    }
  })
  return [...map.values()]
})

const ganttTasks = computed<GanttTask[]>(() => {
  return vehicleSchedules.value.map(s => ({
    id: s.id,
    rowId: s.vehicleId,
    startTime: s.startTime,
    endTime: s.endTime,
    title: s.taskType || '任务',
    type: s.taskType || '任务',
    status: taskTypeToStatus(s.taskType),
    detail: `${s.origin || ''} → ${s.destination || ''}`,
    passengers: s.passengerCount,
  }))
})

function taskTypeToStatus(taskType?: string): string {
  if (taskType === '接送') return '已安排'
  if (taskType === '活动用车') return '进行中'
  if (taskType === '空驶' || taskType === '待命') return '待安排'
  return '已安排'
}

// Helpers
const formatDateShort = (dateStr: string) => {
  const d = new Date(dateStr)
  return `${d.getMonth() + 1}/${d.getDate()}`
}

const formatMinutes = (mins: number) => `${Math.floor(mins / 60)}h${mins % 60}m`

const taskTypeColor = (type?: string) => {
  const map: Record<string, string> = {
    '接送': '#1677ff',
    '活动用车': '#52c41a',
    '空驶': '#d9d9d9',
    '待命': '#d9d9d9',
  }
  return map[type || ''] || '#1677ff'
}

const taskTypeTagColor = (type?: string) => {
  const map: Record<string, string> = {
    '接送': 'blue',
    '活动用车': 'green',
    '空驶': 'default',
    '待命': 'default',
  }
  return map[type || ''] || 'blue'
}

// Data loading
const loadData = async () => {
  loading.value = true
  try {
    const [schedules, cards] = await Promise.all([
      getVehicleSchedules(props.eventId),
      fetchDriverCards(props.eventId),
    ])
    vehicleSchedules.value = (schedules as VehicleScheduleDto[]) || []
    driverCards.value = (cards as DriverCardDto[]) || []
  } catch (err: any) {
    message.error('加载车辆日程失败：' + (err.message || ''))
  } finally {
    loading.value = false
  }
}

// 推导车辆日程
const handleGenerate = async () => {
  generating.value = true
  try {
    await generateVehicleSchedules(props.eventId)
    message.success('车辆日程推导完成')
    await loadData()
  } catch (err: any) {
    message.error('推导失败：' + (err.message || ''))
  } finally {
    generating.value = false
  }
}

// 甘特图任务点击
const handleTaskClick = (task: GanttTask) => {
  message.info(`任务：${task.title} - ${task.detail}`)
}

// 删除日程
const handleDelete = async (id: number) => {
  try {
    await deleteVehicleSchedule(id)
    message.success('删除成功')
    await loadData()
  } catch (err: any) {
    message.error('删除失败：' + (err.message || ''))
  }
}

// 导出PDF
const handleExportPdf = async () => {
  exportingPdf.value = true
  try {
    const blob = await exportVehicleSchedulePdf(props.eventId)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = '车辆日程表.pdf'
    a.click()
    URL.revokeObjectURL(url)
  } catch (err: any) {
    message.error('导出失败：' + (err.message || ''))
  } finally {
    exportingPdf.value = false
  }
}

// 导出司机卡 — 复用PDF导出逻辑（API中未提供独立exportDriverCards，使用getDriverCards数据下载）
const handleExportDriverCards = async () => {
  exportingCards.value = true
  try {
    const cards = await fetchDriverCards(props.eventId)
    const blob = new Blob([JSON.stringify(cards, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = '司机卡.json'
    a.click()
    URL.revokeObjectURL(url)
  } catch (err: any) {
    message.error('导出失败：' + (err.message || ''))
  } finally {
    exportingCards.value = false
  }
}

// 添加活动用车
const showAddDialog = () => {
  addForm.value = {
    vehicleId: 0,
    date: '',
    startTime: '',
    endTime: '',
    taskType: '活动用车',
    origin: '',
    destination: '',
    passengerCount: 0,
    remark: '',
  }
  addDialogVisible.value = true
  if (vehicles.value.length === 0) {
    loadVehicles()
  }
}

const handleAddSubmit = async () => {
  if (!addForm.value.vehicleId || !addForm.value.date || !addForm.value.startTime || !addForm.value.endTime) {
    message.warning('请填写必填项')
    return
  }
  addSubmitting.value = true
  try {
    await addVehicleTask(props.eventId, {
      ...addForm.value,
      startTime: `${addForm.value.startTime}:00`,
      endTime: `${addForm.value.endTime}:00`,
    })
    message.success('添加成功')
    addDialogVisible.value = false
    await loadData()
  } catch (err: any) {
    message.error('添加失败：' + (err.message || ''))
  } finally {
    addSubmitting.value = false
  }
}

// 司机通知相关状态
const notificationModalVisible = ref(false)
const notificationDate = ref('')
const loadingNotifications = ref(false)
const driverNotifications = ref<DriverNotificationDto[]>([])
const selectedDriverIds = ref<number[]>([])
const expandedDrivers = ref<string[]>([])
const selectAllDrivers = ref(false)

const showNotificationModal = () => {
  notificationModalVisible.value = true
  if (dateDays.value.length > 0 && !notificationDate.value) {
    notificationDate.value = dateDays.value[0]
  }
  if (notificationDate.value) {
    loadNotifications()
  }
}

const loadNotifications = async () => {
  if (!notificationDate.value) return
  loadingNotifications.value = true
  try {
    const res = await getDriverNotifications(props.eventId, notificationDate.value) as any
    driverNotifications.value = Array.isArray(res) ? res : (res?.data || [])
    selectedDriverIds.value = driverNotifications.value.map(d => d.driverVehicleId)
    selectAllDrivers.value = true
  } catch {
    message.error('加载司机通知失败')
  } finally {
    loadingNotifications.value = false
  }
}

const handleSelectAllDrivers = (e: any) => {
  if (e.target.checked) {
    selectedDriverIds.value = driverNotifications.value.map(d => d.driverVehicleId)
  } else {
    selectedDriverIds.value = []
  }
}

const toggleDriver = (id: number, checked: boolean) => {
  if (checked) {
    selectedDriverIds.value.push(id)
  } else {
    selectedDriverIds.value = selectedDriverIds.value.filter(d => d !== id)
  }
  selectAllDrivers.value = selectedDriverIds.value.length === driverNotifications.value.length
}

const copySingle = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    message.success('已复制到剪贴板')
  } catch {
    message.error('复制失败')
  }
}

const copyAllSelected = async () => {
  const texts = driverNotifications.value
    .filter(d => selectedDriverIds.value.includes(d.driverVehicleId))
    .map(d => d.message)
    .join('\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n')
  try {
    await navigator.clipboard.writeText(texts)
    message.success(`已复制 ${selectedDriverIds.value.length} 位司机的通知`)
  } catch {
    message.error('复制失败')
  }
}

// 自动选中第一个日期
watch(dateDays, (days) => {
  if (days.length && !selectedDate.value) {
    selectedDate.value = days[0]
  }
}, { immediate: true })

watch(() => props.eventId, () => {
  if (props.eventId) loadData()
}, { immediate: true })

onMounted(() => {
  if (props.eventId) {
    loadData()
    loadVehicles()
  }
})
</script>

<style scoped lang="scss">
.vehicle-schedule-panel {
  padding: 0;
}

.add-vehicle-form {
  max-height: 60vh;
  overflow-y: auto;
  padding-right: 4px;
}

.driver-card {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  border-radius: 8px;

  .driver-stats {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }

  :deep(.ant-timeline) {
    margin-top: 8px;
    padding-top: 4px;
  }

  :deep(.ant-timeline-item) {
    padding-bottom: 12px;
  }
}
</style>
