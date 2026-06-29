<template>
  <div class="transport-panel">
    <!-- 1. 智能操作栏 -->
    <SmartActionBar description="根据参会人行程信息，智能生成和优化接送安排" bg-color="#f0f5ff">
      <a-button type="primary" :loading="generating" @click="handleAutoGenerate">
        <ThunderboltOutlined />一键生成接送任务
      </a-button>
      <a-button :loading="optimizing" @click="handleOptimize">
        <SwapOutlined />智能优化合并
      </a-button>
      <a-button @click="handleAddTask">
        <PlusOutlined />手动添加
      </a-button>
    </SmartActionBar>

    <!-- 2. 统计栏 -->
    <a-row :gutter="16" style="margin: 16px 0" align="stretch">
      <a-col :span="6">
        <StatCard title="接送任务总数" :value="totalTasks" />
      </a-col>
      <a-col :span="6">
        <StatCard title="已安排车辆" :value="assignedTasks" :progress="assignProgress" />
      </a-col>
      <a-col :span="6">
        <StatCard title="待接送人数" :value="pendingPassengers" />
      </a-col>
      <a-col :span="6">
        <StatCard title="今日任务" :value="todayTasks" />
      </a-col>
    </a-row>

    <!-- 3. 筛选栏 -->
    <a-card :bordered="false" style="margin-bottom: 16px">
      <a-row :gutter="16" align="middle">
        <a-col :span="5">
          <a-select v-model:value="filterType" placeholder="类型筛选" allowClear style="width: 100%">
            <a-select-option value="接机">接机</a-select-option>
            <a-select-option value="接站">接站</a-select-option>
            <a-select-option value="送机">送机</a-select-option>
            <a-select-option value="送站">送站</a-select-option>
          </a-select>
        </a-col>
        <a-col :span="5">
          <a-select v-model:value="filterStatus" placeholder="状态筛选" allowClear style="width: 100%">
            <a-select-option value="待安排">待安排</a-select-option>
            <a-select-option value="已安排">已安排</a-select-option>
            <a-select-option value="进行中">进行中</a-select-option>
            <a-select-option value="已完成">已完成</a-select-option>
          </a-select>
        </a-col>
        <a-col :span="5">
          <a-date-picker v-model:value="filterDate" placeholder="日期筛选" style="width: 100%" valueFormat="YYYY-MM-DD" />
        </a-col>
        <a-col :span="4">
          <a-input-search v-model:value="searchKeyword" placeholder="搜索车牌/司机" allowClear @search="loadPickupTasks" />
        </a-col>
        <a-col :span="5" style="text-align: right">
          <a-button @click="resetFilters">重置</a-button>
        </a-col>
      </a-row>
    </a-card>

    <!-- 4. 接送任务表格 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="filteredTasks"
        :loading="loading"
        :scroll="{ x: 1200 }"
        row-key="id"
        :pagination="{ pageSize: 10, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` }"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'type'">
            <a-tag :color="typeColorMap[record.type] || 'default'">{{ record.type || '-' }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'vehiclePlateNumber'">
            {{ record.vehiclePlateNumber || '未分配' }}
          </template>
          <template v-else-if="column.dataIndex === 'driverName'">
            {{ record.driverName || '-' }}
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-badge :status="statusBadgeMap[record.status] || 'default'" :text="record.status" />
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a @click="handleEditTask(record as PickupTaskListItemDto)">编辑</a>
              <a-popconfirm title="确定删除该任务？" @confirm="handleDeleteTask(record.id)">
                <a style="color: var(--color-danger)">删除</a>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 5. 车辆管理区域 -->
    <a-card :bordered="false" style="margin-top: 16px">
      <template #title>
        <div style="display: flex; justify-content: space-between; align-items: center">
          <span>车辆管理</span>
          <a-button type="primary" size="small" @click="handleAddVehicle">
            <PlusOutlined />新增车辆
          </a-button>
        </div>
      </template>
      <a-table
        :columns="vehicleColumns"
        :data-source="vehicles"
        :loading="vehicleLoading"
        row-key="id"
        :pagination="{ pageSize: 5, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 辆` }"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'source'">
            <a-tag>{{ record.source || '-' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'vehicleAction'">
            <a-space>
              <a @click="handleEditVehicle(record as VehicleListItemDto)">编辑</a>
              <a-popconfirm title="确定删除该车辆？" @confirm="handleDeleteVehicle(record.id)">
                <a style="color: var(--color-danger)">删除</a>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 6. 甘特图区 -->
    <a-card title="车辆调度甘特图" :bordered="false" style="margin-top: 16px">
      <GanttChart
        :rows="ganttRows"
        :tasks="ganttTasks"
        :start-date="eventStartDate"
        :end-date="eventEndDate"
        @task-click="handleGanttTaskClick"
      />
    </a-card>

    <!-- 6. 编辑抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="editingTask ? '编辑接送任务' : '新增接送任务'"
      :width="600"
      @close="resetForm"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item label="类型" required>
          <a-select v-model:value="formState.type" placeholder="请选择类型">
            <a-select-option value="接机">接机</a-select-option>
            <a-select-option value="接站">接站</a-select-option>
            <a-select-option value="送机">送机</a-select-option>
            <a-select-option value="送站">送站</a-select-option>
          </a-select>
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="日期" required>
              <a-date-picker v-model:value="formState.date" style="width: 100%" valueFormat="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="时间" required>
              <a-time-picker v-model:value="formState.time" format="HH:mm" valueFormat="HH:mm" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="出发地">
          <a-input v-model:value="formState.origin" placeholder="请输入出发地" />
        </a-form-item>
        <a-form-item label="目的地">
          <a-input v-model:value="formState.destination" placeholder="请输入目的地" />
        </a-form-item>
        <a-form-item label="车辆">
          <a-select v-model:value="formState.vehicleId" placeholder="请选择车辆" allowClear>
            <a-select-option v-for="v in vehicles" :key="v.id" :value="v.id">
              {{ v.plateNumber }} ({{ v.driverName || '无司机' }})
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="乘客">
          <a-select
            v-model:value="formState.passengerIds"
            mode="multiple"
            placeholder="请选择乘客"
            :options="attendeeOptions"
            :filter-option="filterAttendeeOption"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="formState.remark" :rows="3" placeholder="备注信息" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="drawerVisible = false">取消</a-button>
          <a-button type="primary" :loading="saving" @click="handleSaveTask">保存</a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- 7. 新增/编辑车辆 Modal -->
    <a-modal
      v-model:open="showVehicleModal"
      :title="editingVehicleId ? '编辑车辆' : '新增车辆'"
      :width="560"
      @ok="handleSaveVehicle"
      :confirm-loading="vehicleSaving"
      :destroy-on-close="true"
    >
      <a-form :model="vehicleForm" layout="vertical" style="margin-top: 16px">
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
            <a-form-item label="车辆来源">
              <a-select v-model:value="vehicleForm.source" placeholder="请选择来源" allowClear>
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
              <a-input v-model:value="vehicleForm.driverName" placeholder="请输入司机姓名" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="司机电话">
              <a-input v-model:value="vehicleForm.driverPhone" placeholder="请输入司机电话" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注">
          <a-textarea v-model:value="vehicleForm.remark" :rows="2" placeholder="备注信息" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 8. 自动生成预览 Modal -->
    <a-modal v-model:open="showPreview" title="接送任务生成预览" :width="900" @ok="confirmGenerate">
      <a-alert message="系统将根据参会人行程自动生成接送任务" type="info" show-icon style="margin-bottom: 16px" />
      <a-table :columns="previewColumns" :data-source="previewData" :pagination="false" size="small" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { ThunderboltOutlined, SwapOutlined, PlusOutlined } from '@ant-design/icons-vue'
import SmartActionBar from '../components/SmartActionBar.vue'
import StatCard from '../components/StatCard.vue'
import GanttChart from '../components/GanttChart.vue'
import type { GanttRow, GanttTask } from '../components/GanttChart.vue'
import {
  getPickupTasks,
  createPickupTask,
  updatePickupTask,
  deletePickupTask,
  autoGeneratePickups,
  commitAutoGeneratePickups,
  optimizePickups,
  getAttendees,
  getVehicles,
  createVehicle,
  updateVehicle,
  deleteVehicle,
  setPickupPassengers,
  getPickupTaskDetail,
} from '@/api/conference'
import type {
  PickupTaskListItemDto,
  PickupTaskDetailDto,
  VehicleListItemDto,
  AttendeeListItemDto,
  CreatePickupTaskRequest,
  UpdatePickupTaskRequest,
  CreateVehicleRequest,
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ==================== 状态 ====================
const loading = ref(false)
const generating = ref(false)
const optimizing = ref(false)
const saving = ref(false)
const drawerVisible = ref(false)
const showPreview = ref(false)

const pickupTasks = ref<PickupTaskListItemDto[]>([])
const vehicles = ref<VehicleListItemDto[]>([])
const attendees = ref<AttendeeListItemDto[]>([])
const previewData = ref<any[]>([])
const editingTask = ref<PickupTaskListItemDto | null>(null)

// 筛选
const filterType = ref<string | undefined>(undefined)
const filterStatus = ref<string | undefined>(undefined)
const filterDate = ref<string | undefined>(undefined)
const searchKeyword = ref('')

// 表单
const formState = ref<{
  type?: string
  date?: string
  time?: string
  origin?: string
  destination?: string
  vehicleId?: number
  passengerIds: number[]
  remark?: string
}>({
  passengerIds: [],
})

// ==================== 计算属性 ====================
const typeColorMap: Record<string, string> = {
  '接机': 'blue',
  '接站': 'cyan',
  '送机': 'orange',
  '送站': 'purple',
}

const statusBadgeMap: Record<string, 'default' | 'error' | 'warning' | 'success' | 'processing'> = {
  '待安排': 'default',
  '已安排': 'processing',
  '进行中': 'success',
  '已完成': 'default',
}

const totalTasks = computed(() => pickupTasks.value.length)
const assignedTasks = computed(() => pickupTasks.value.filter(t => t.vehicleId).length)
const assignProgress = computed(() => (totalTasks.value > 0 ? Math.round((assignedTasks.value / totalTasks.value) * 100) : 0))
const pendingPassengers = computed(() =>
  pickupTasks.value
    .filter(t => t.status === '待安排' || t.status === '已安排')
    .reduce((sum, t) => sum + (t.passengerCount || 0), 0),
)
const todayTasks = computed(() => {
  const today = new Date().toISOString().slice(0, 10)
  return pickupTasks.value.filter(t => formatDate(t.date) === today).length
})

const filteredTasks = computed(() => {
  let list = pickupTasks.value
  if (filterType.value) list = list.filter(t => t.type === filterType.value)
  if (filterStatus.value) list = list.filter(t => t.status === filterStatus.value)
  if (filterDate.value) list = list.filter(t => formatDate(t.date) === filterDate.value)
  if (searchKeyword.value) {
    const kw = searchKeyword.value.toLowerCase()
    list = list.filter(
      t =>
        (t.vehiclePlateNumber || '').toLowerCase().includes(kw) ||
        (t.driverName || '').toLowerCase().includes(kw),
    )
  }
  return list
})

const eventStartDate = computed(() => props.eventData?.startDate || new Date().toISOString().slice(0, 10))
const eventEndDate = computed(() => props.eventData?.endDate || new Date().toISOString().slice(0, 10))

const attendeeOptions = computed(() =>
  attendees.value.map(a => ({ label: `${a.name}${a.organization ? ' - ' + a.organization : ''}`, value: a.id })),
)

// ==================== 甘特图数据 ====================
const ganttRows = computed<GanttRow[]>(() => {
  const vehicleMap = new Map<number, GanttRow>()
  pickupTasks.value.forEach(t => {
    if (t.vehicleId && !vehicleMap.has(t.vehicleId)) {
      vehicleMap.set(t.vehicleId, {
        id: t.vehicleId,
        label: t.vehiclePlateNumber || '未知',
        subLabel: t.driverName || '',
      })
    }
  })
  return [...vehicleMap.values()]
})

const ganttTasks = computed<GanttTask[]>(() => {
  return pickupTasks.value
    .filter(t => t.vehicleId)
    .map(t => ({
      id: t.id,
      rowId: t.vehicleId!,
      startTime: `${t.date}T${t.time}:00`,
      endTime: `${t.date}T${addMinutes(t.time, 90)}:00`,
      title: `${t.type || ''} ${t.passengerCount}人`,
      type: t.type || '',
      status: t.status,
      passengers: t.passengerCount,
      detail: `${t.origin || ''} → ${t.destination || ''}`,
    }))
})

// ==================== 表格列 ====================
// 辅助：格式化ISO日期为 YYYY-MM-DD
function formatDate(val: any): string {
  if (!val) return '-'
  return typeof val === 'string' ? val.split('T')[0] : String(val)
}
// 辅助：格式化时间为 HH:mm
function formatTime(val: any): string {
  if (!val) return '-'
  return typeof val === 'string' ? val.substring(0, 5) : String(val)
}

const columns = [
  { title: '类型', dataIndex: 'type', width: 90 },
  { title: '日期', dataIndex: 'date', width: 100, customRender: ({ text }: any) => formatDate(text) },
  { title: '时间', dataIndex: 'time', width: 80, customRender: ({ text }: any) => formatTime(text) },
  { title: '出发地', dataIndex: 'origin', width: 150, ellipsis: true },
  { title: '目的地', dataIndex: 'destination', width: 150, ellipsis: true },
  { title: '乘客数', dataIndex: 'passengerCount', width: 80, align: 'center' as const },
  { title: '乘客姓名', dataIndex: 'passengerNames', width: 200, ellipsis: true },
  { title: '车牌', dataIndex: 'vehiclePlateNumber', width: 100 },
  { title: '司机', dataIndex: 'driverName', width: 80 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '操作', key: 'action', width: 120, fixed: 'right' as const },
]

const previewColumns = [
  { title: '类型', dataIndex: 'type', width: 80 },
  { title: '日期', dataIndex: 'date', width: 100 },
  { title: '时间', dataIndex: 'time', width: 80 },
  { title: '出发地', dataIndex: 'origin', width: 150 },
  { title: '目的地', dataIndex: 'destination', width: 150 },
  { title: '乘客', dataIndex: 'passengerNames', customRender: ({ text }: any) => (text || []).join('、') },
]

// ==================== 车辆管理 ====================
const vehicleLoading = ref(false)
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
  { title: '操作', key: 'vehicleAction', width: 120, fixed: 'right' as const },
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
  } catch {
    message.error('删除失败')
  }
}

// ==================== 数据加载 ====================
async function loadPickupTasks() {
  loading.value = true
  try {
    const res: any = await getPickupTasks(props.eventId)
    pickupTasks.value = Array.isArray(res) ? res : res?.items || []
  } catch {
    pickupTasks.value = []
  } finally {
    loading.value = false
  }
}

async function loadVehicles() {
  vehicleLoading.value = true
  try {
    const res: any = await getVehicles(props.eventId)
    vehicles.value = Array.isArray(res) ? res : res?.items || []
  } catch {
    vehicles.value = []
  } finally {
    vehicleLoading.value = false
  }
}

async function loadAttendees() {
  try {
    const res: any = await getAttendees(props.eventId, {})
    attendees.value = Array.isArray(res) ? res : res?.items || []
  } catch {
    attendees.value = []
  }
}

// ==================== 操作 ====================
function handleAddTask() {
  editingTask.value = null
  resetForm()
  drawerVisible.value = true
}

async function handleEditTask(record: PickupTaskListItemDto) {
  editingTask.value = record

  // 加载任务详情（包括乘客列表）
  try {
    const detail = await getPickupTaskDetail(record.id) as unknown as PickupTaskDetailDto
    formState.value = {
      type: detail?.type ?? record.type,
      date: formatDate(detail?.date ?? record.date),
      time: formatTime(detail?.time ?? record.time),
      origin: detail?.origin ?? record.origin,
      destination: detail?.destination ?? record.destination,
      vehicleId: detail?.vehicleId ?? record.vehicleId,
      passengerIds: detail?.passengers?.map(p => p.attendeeId) || [],
      remark: detail?.remark,
    }
  } catch {
    // 加载详情失败时，使用基本信息
    formState.value = {
      type: record.type,
      date: formatDate(record.date),
      time: formatTime(record.time),
      origin: record.origin,
      destination: record.destination,
      vehicleId: record.vehicleId,
      passengerIds: [],
      remark: undefined,
    }
  }
  drawerVisible.value = true
}

async function handleSaveTask() {
  if (!formState.value.date || !formState.value.time) {
    message.warning('请填写日期和时间')
    return
  }
  saving.value = true
  try {
    const payload: CreatePickupTaskRequest | UpdatePickupTaskRequest = {
      type: formState.value.type,
      date: formState.value.date,
      time: formState.value.time,
      origin: formState.value.origin,
      destination: formState.value.destination,
      vehicleId: formState.value.vehicleId,
      remark: formState.value.remark,
    }
    if (editingTask.value) {
      await updatePickupTask(editingTask.value.id, payload as UpdatePickupTaskRequest)
      await setPickupPassengers(editingTask.value.id, { attendeeIds: formState.value.passengerIds })
      message.success('更新成功')
    } else {
      const res: any = await createPickupTask(props.eventId, payload as CreatePickupTaskRequest)
      const newId = res?.id || res
      if (newId) {
        await setPickupPassengers(newId, { attendeeIds: formState.value.passengerIds })
      }
      message.success('创建成功')
    }
    drawerVisible.value = false
    loadPickupTasks()
  } catch (error: any) {
    const errorMsg = error?.response?.data?.message || error?.message || '保存失败'
    message.error(errorMsg)
  } finally {
    saving.value = false
  }
}

async function handleDeleteTask(id: number) {
  try {
    await deletePickupTask(id)
    message.success('删除成功')
    loadPickupTasks()
  } catch {
    message.error('删除失败')
  }
}

async function handleAutoGenerate() {
  generating.value = true
  try {
    const result: any = await autoGeneratePickups(props.eventId)
    if (result?.tasksToCreate?.length) {
      previewData.value = result.tasksToCreate
      showPreview.value = true
      // 如果有无法安排的人员，额外提示
      if (result.unableToArrange?.length > 0) {
        message.warning(`${result.unableToArrange.length}人因缺少行程信息无法安排接送`)
      }
    } else {
      // 区分不同情况给出明确提示
      const messages: string[] = []
      if (result?.unableToArrange?.length > 0) {
        messages.push(`${result.unableToArrange.length}人缺少行程信息（到达/离开时间或站点）`)
      }
      if (result?.skippedAttendees?.length > 0) {
        messages.push(`${result.skippedAttendees.length}人已安排接送`)
      }
      if (messages.length > 0) {
        message.warning('没有可生成的接送任务：' + messages.join('；'))
      } else {
        message.warning('没有找到需要接送的已确认参会人，请确认参会人状态为"已确认"且标记了"需要接送"')
      }
    }
  } catch (error: any) {
    const errorMsg = error?.response?.data?.message || error?.message || '生成失败'
    message.error(`生成失败: ${errorMsg}`)
    console.error('AutoGenerate error:', error)
  } finally {
    generating.value = false
  }
}

async function confirmGenerate() {
  if (!previewData.value.length) {
    message.warning('没有任务可提交')
    return
  }
  try {
    await commitAutoGeneratePickups(props.eventId, previewData.value)
    message.success(`已成功创建 ${previewData.value.length} 个接送任务`)
    showPreview.value = false
    previewData.value = []
    await loadPickupTasks()
  } catch (error: any) {
    const errorMsg = error?.response?.data?.message || error?.message || '任务保存失败'
    message.error(`${errorMsg}，请重试`)
    console.error('CommitGenerate error:', error)
  }
}

async function handleOptimize() {
  Modal.confirm({
    title: '智能优化合并',
    content: '系统将对时间和路线相近的接送任务进行合并优化，是否继续？',
    onOk: async () => {
      optimizing.value = true
      try {
        await optimizePickups(props.eventId)
        message.success('优化完成')
        loadPickupTasks()
      } catch {
        message.error('优化失败')
      } finally {
        optimizing.value = false
      }
    },
  })
}

function handleGanttTaskClick(task: GanttTask) {
  const found = pickupTasks.value.find(t => t.id === task.id)
  if (found) handleEditTask(found)
}

function resetForm() {
  formState.value = { passengerIds: [] }
  editingTask.value = null
}

function resetFilters() {
  filterType.value = undefined
  filterStatus.value = undefined
  filterDate.value = undefined
  searchKeyword.value = ''
}

function filterAttendeeOption(input: string, option: any) {
  return (option?.label || '').toLowerCase().includes(input.toLowerCase())
}

// ==================== 辅助函数 ====================
function addMinutes(time: string, mins: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = h * 60 + m + mins
  return `${String(Math.floor(total / 60) % 24).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

// ==================== 生命周期 ====================
onMounted(() => {
  loadPickupTasks()
  loadVehicles()
  loadAttendees()
})

watch(() => props.eventId, () => {
  loadPickupTasks()
  loadVehicles()
  loadAttendees()
})
</script>

<style scoped lang="scss">
.transport-panel {
  padding: 0;
}
</style>
