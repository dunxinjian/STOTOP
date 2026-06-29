<template>
  <div class="schedule-panel">
    <!-- 顶部智能操作栏 -->
    <SmartActionBar description="管理活动日程和资源需求">
      <a-button type="primary" @click="showAddDrawer"><PlusOutlined />新增日程</a-button>
      <a-dropdown>
        <a-button><BarsOutlined />快速模板</a-button>
        <template #overlay>
          <a-menu @click="handleSelectTemplate">
            <a-menu-item key="welcome">欢迎晚宴</a-menu-item>
            <a-menu-item key="keynote">主论坛</a-menu-item>
            <a-menu-item key="visit">参观考察</a-menu-item>
            <a-menu-item key="breakout">分论坛</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
    </SmartActionBar>

    <!-- 日期Tab切换 -->
    <a-tabs v-model:activeKey="selectedDate" type="card" v-if="dateDays.length > 0">
      <a-tab-pane v-for="date in dateDays" :key="date" :tab="formatDateShort(date)" />
    </a-tabs>
    <a-alert v-else type="info" message="请先设置活动的开始和结束日期" show-icon style="margin-bottom: 12px" />

    <!-- 日程时间轴列表 -->
    <a-spin :spinning="loading">
      <a-card :bordered="false">
        <a-timeline v-if="currentDaySchedules.length > 0">
          <a-timeline-item
            v-for="schedule in currentDaySchedules"
            :key="schedule.id"
            :color="typeColorMap[schedule.type || ''] || '#1677ff'"
          >
            <div class="schedule-item" @click="showDetailDrawer(schedule)">
              <div class="schedule-time">{{ schedule.startTime }} - {{ schedule.endTime }}</div>
              <div class="schedule-title">{{ schedule.title }}</div>
              <div class="schedule-meta">
                <span v-if="schedule.location">&#x1F4CD; {{ schedule.location }}</span>
                <a-badge :count="scheduleAttendeeCount(schedule)" :overflow-count="999" style="margin-left: 8px" />
              </div>
              <a-tag :color="typeColorMap[schedule.type || '']">{{ schedule.type || '其他' }}</a-tag>
              <a-button type="link" size="small" danger @click.stop="handleDelete(schedule.id)">删除</a-button>
            </div>
          </a-timeline-item>
        </a-timeline>
        <EmptyState v-else size="small" title="当天无日程" />
      </a-card>
    </a-spin>

    <!-- 日程详情/编辑抽屉 -->
    <a-drawer
      v-model:open="showDrawer"
      :title="editingId ? '编辑日程' : '新增日程'"
      :width="800"
      :destroyOnClose="true"
      @close="resetForm"
    >
      <a-spin :spinning="drawerLoading">
        <a-form :label-col="{ span: 4 }" :wrapper-col="{ span: 18 }">
          <!-- 基本信息 -->
          <a-divider orientation="left">基本信息</a-divider>
          <a-form-item label="标题" required>
            <a-input v-model:value="formData.title" placeholder="请输入日程标题" />
          </a-form-item>
          <a-form-item label="日期" required>
            <a-select v-model:value="formData.date" placeholder="请选择日期">
              <a-select-option v-for="d in dateDays" :key="d" :value="d">{{ formatDateShort(d) }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-row>
            <a-col :span="12">
              <a-form-item label="开始时间" :label-col="{ span: 8 }" :wrapper-col="{ span: 14 }" required>
                <a-time-picker v-model:value="startTimeValue" format="HH:mm" placeholder="开始时间" style="width: 100%" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="结束时间" :label-col="{ span: 8 }" :wrapper-col="{ span: 14 }" required>
                <a-time-picker v-model:value="endTimeValue" format="HH:mm" placeholder="结束时间" style="width: 100%" />
              </a-form-item>
            </a-col>
          </a-row>
          <a-form-item label="地点">
            <a-input v-model:value="formData.location" placeholder="请输入地点" />
          </a-form-item>
          <a-form-item label="类型">
            <a-select v-model:value="formData.type" placeholder="请选择类型">
              <a-select-option v-for="t in scheduleTypes" :key="t" :value="t">{{ t }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="描述">
            <a-textarea v-model:value="formData.description" placeholder="请输入描述" :rows="3" />
          </a-form-item>

          <!-- 参会人员穿梭框 -->
          <a-divider orientation="left">参会人员</a-divider>
          <a-transfer
            :data-source="transferDataSource"
            :target-keys="selectedAttendeeIds"
            :titles="['全部参会人', '已选参会人']"
            :render="(item: any) => item.title"
            :list-style="{ width: '320px', height: '300px' }"
            show-search
            :filter-option="filterTransfer"
            @change="handleTransferChange"
          />

          <!-- 物品清单 -->
          <a-divider orientation="left">物品清单</a-divider>
          <a-table
            :dataSource="scheduleItems"
            :columns="itemColumns"
            :pagination="false"
            size="small"
            rowKey="_uid"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'itemName'">
                <a-input v-model:value="record.itemName" placeholder="物品名称" size="small" />
              </template>
              <template v-else-if="column.dataIndex === 'quantity'">
                <a-input-number v-model:value="record.quantity" :min="1" size="small" style="width: 80px" />
              </template>
              <template v-else-if="column.dataIndex === 'unit'">
                <a-input v-model:value="record.unit" placeholder="单位" size="small" style="width: 70px" />
              </template>
              <template v-else-if="column.dataIndex === 'remark'">
                <a-input v-model:value="record.remark" placeholder="备注" size="small" />
              </template>
              <template v-else-if="column.dataIndex === 'action'">
                <a-button type="link" danger size="small" @click="removeItem(index)">删除</a-button>
              </template>
            </template>
          </a-table>
          <a-button type="dashed" block style="margin-top: 8px" @click="addItem">
            <PlusOutlined /> 添加物品
          </a-button>
        </a-form>
      </a-spin>

      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px">
          <a-button @click="showDrawer = false">取消</a-button>
          <a-button type="primary" :loading="saving" @click="saveSchedule">保存</a-button>
        </div>
      </template>
    </a-drawer>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, BarsOutlined } from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import SmartActionBar from '../components/SmartActionBar.vue'
import {
  getSchedules, createSchedule, updateSchedule, deleteSchedule,
  setScheduleAttendees, setScheduleItems, getAttendees
} from '@/api/conference'
import type {
  ScheduleDto, CreateScheduleRequest, UpdateScheduleRequest,
  AttendeeListItemDto, ScheduleItemInput
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ==================== 状态 ====================
const loading = ref(false)
const drawerLoading = ref(false)
const saving = ref(false)
const showDrawer = ref(false)

const selectedDate = ref('')
const editingId = ref<number | null>(null)
const allSchedules = ref<ScheduleDto[]>([])
const allAttendees = ref<AttendeeListItemDto[]>([])
const selectedAttendeeIds = ref<string[]>([])
const startTimeValue = ref<Dayjs | undefined>(undefined)
const endTimeValue = ref<Dayjs | undefined>(undefined)

// ==================== 类型与颜色映射 ====================
const scheduleTypes = ['会议', '培训', '参观', '用餐', '休息', '其他']
const typeColorMap: Record<string, string> = {
  '会议': '#1677ff',
  '培训': '#722ed1',
  '参观': '#13c2c2',
  '用餐': '#fa8c16',
  '休息': '#8c8c8c',
  '其他': '#1677ff',
}

// ==================== 日期计算 ====================
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

// ==================== 日程分组 ====================
const schedulesByDate = computed(() => {
  const map: Record<string, ScheduleDto[]> = {}
  for (const s of allSchedules.value) {
    const dateKey = s.date?.split('T')[0] || ''
    if (!map[dateKey]) map[dateKey] = []
    map[dateKey].push(s)
  }
  return map
})

const currentDaySchedules = computed(() => {
  return (schedulesByDate.value[selectedDate.value] || [])
    .sort((a, b) => a.startTime.localeCompare(b.startTime))
})

// 列表接口可能携带聚合的 attendeeCount（非 ScheduleDto 标准字段），优先使用，回退 attendees 长度
function scheduleAttendeeCount(schedule: ScheduleDto): number {
  return (schedule as ScheduleDto & { attendeeCount?: number }).attendeeCount
    ?? schedule.attendees?.length
    ?? 0
}

// ==================== 表单 ====================
const formData = ref<Partial<CreateScheduleRequest & { id?: number }>>({
  title: '',
  date: '',
  location: '',
  type: '会议',
  description: '',
  sort: 0,
})

// 物品清单
interface ItemRow extends ScheduleItemInput {
  _uid: string
}
const scheduleItems = ref<ItemRow[]>([])
let itemUid = 0

const itemColumns = [
  { title: '物品名称', dataIndex: 'itemName', width: 180 },
  { title: '数量', dataIndex: 'quantity', width: 100 },
  { title: '单位', dataIndex: 'unit', width: 90 },
  { title: '备注', dataIndex: 'remark' },
  { title: '操作', dataIndex: 'action', width: 70 },
]

const addItem = () => {
  scheduleItems.value.push({ _uid: `item_${++itemUid}`, itemName: '', quantity: 1, unit: '', remark: '' })
}

const removeItem = (index: number) => {
  scheduleItems.value.splice(index, 1)
}

// ==================== 穿梭框数据源 ====================
const transferDataSource = computed(() => {
  return allAttendees.value.map(a => ({
    key: String(a.id),
    title: `${a.name}${a.organization ? ' - ' + a.organization : ''}`,
    description: a.role || '',
  }))
})

const filterTransfer = (inputValue: string, option: any) => {
  return option.title.toLowerCase().includes(inputValue.toLowerCase())
}

const handleTransferChange = (targetKeys: string[]) => {
  selectedAttendeeIds.value = targetKeys
}

// ==================== 日期格式化 ====================
const formatDateShort = (dateStr: string) => {
  const d = new Date(dateStr)
  const weekDays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']
  return `${d.getMonth() + 1}/${d.getDate()} ${weekDays[d.getDay()]}`
}

// ==================== 数据加载 ====================
const loadSchedules = async () => {
  loading.value = true
  try {
    const res = await getSchedules(props.eventId) as any
    allSchedules.value = Array.isArray(res) ? res : (res?.items || [])
  } catch {
    message.error('加载日程失败')
  } finally {
    loading.value = false
  }
}

const loadAttendees = async () => {
  try {
    const res = await getAttendees(props.eventId, { pageSize: 9999 }) as any
    allAttendees.value = Array.isArray(res) ? res : (res?.items || [])
  } catch {
    // ignore
  }
}

// ==================== 抽屉操作 ====================
const showAddDrawer = () => {
  editingId.value = null
  formData.value = {
    title: '',
    date: selectedDate.value || dateDays.value[0] || '',
    location: '',
    type: '会议',
    description: '',
    sort: currentDaySchedules.value.length,
  }
  startTimeValue.value = undefined
  endTimeValue.value = undefined
  selectedAttendeeIds.value = []
  scheduleItems.value = []
  showDrawer.value = true
}

const showDetailDrawer = (schedule: ScheduleDto) => {
  editingId.value = schedule.id
  formData.value = {
    title: schedule.title,
    date: schedule.date?.split('T')[0] || '',
    location: schedule.location || '',
    type: schedule.type || '会议',
    description: schedule.description || '',
    sort: schedule.sort,
  }
  startTimeValue.value = schedule.startTime ? dayjs(schedule.startTime, 'HH:mm') : undefined
  endTimeValue.value = schedule.endTime ? dayjs(schedule.endTime, 'HH:mm') : undefined
  selectedAttendeeIds.value = (schedule.attendees || []).map(a => String(a.id))
  scheduleItems.value = (schedule.items || []).map(item => ({
    _uid: `item_${++itemUid}`,
    itemName: item.itemName,
    quantity: item.quantity,
    unit: item.unit || '',
    remark: item.remark || '',
  }))
  showDrawer.value = true
}

const resetForm = () => {
  editingId.value = null
  formData.value = { title: '', date: '', location: '', type: '会议', description: '', sort: 0 }
  startTimeValue.value = undefined
  endTimeValue.value = undefined
  selectedAttendeeIds.value = []
  scheduleItems.value = []
}

// ==================== 保存 ====================
const saveSchedule = async () => {
  if (!formData.value.title?.trim()) {
    message.warning('请输入日程标题')
    return
  }
  if (!formData.value.date) {
    message.warning('请选择日期')
    return
  }
  if (!startTimeValue.value || !endTimeValue.value) {
    message.warning('请选择开始和结束时间')
    return
  }

  saving.value = true
  try {
    const payload = {
      ...formData.value,
      startTime: startTimeValue.value.format('HH:mm'),
      endTime: endTimeValue.value.format('HH:mm'),
      sort: formData.value.sort || 0,
    }

    if (editingId.value) {
      await updateSchedule(editingId.value, payload as UpdateScheduleRequest)
    } else {
      const created = await createSchedule(props.eventId, payload as CreateScheduleRequest) as any
      editingId.value = created?.id || created
    }

    // 保存参会人
    if (editingId.value) {
      await setScheduleAttendees(editingId.value, {
        attendeeIds: selectedAttendeeIds.value.map(Number),
      })
    }

    // 保存物品
    if (editingId.value && scheduleItems.value.length > 0) {
      await setScheduleItems(editingId.value, {
        items: scheduleItems.value
          .filter(i => i.itemName?.trim())
          .map(({ itemName, quantity, unit, remark }) => ({ itemName, quantity, unit, remark })),
      })
    }

    message.success('保存成功')
    showDrawer.value = false
    loadSchedules()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

// ==================== 删除 ====================
const handleDelete = async (id: number) => {
  try {
    await deleteSchedule(id)
    message.success('删除成功')
    loadSchedules()
  } catch {
    message.error('删除失败')
  }
}

// ==================== 快速模板 ====================
const handleSelectTemplate = (e: any) => {
  const templates: Record<string, any> = {
    'welcome': { title: '欢迎晚宴', type: '用餐', startTime: '18:00', endTime: '20:00' },
    'keynote': { title: '主论坛', type: '会议', startTime: '09:00', endTime: '12:00' },
    'visit': { title: '参观考察', type: '参观', startTime: '14:00', endTime: '17:00' },
    'breakout': { title: '分论坛', type: '会议', startTime: '14:00', endTime: '16:00' },
  }
  const tpl = templates[e.key]
  if (tpl) {
    editingId.value = null
    formData.value = {
      ...formData.value,
      ...tpl,
      date: selectedDate.value || dateDays.value[0] || '',
    }
    startTimeValue.value = tpl.startTime
    endTimeValue.value = tpl.endTime
    selectedAttendeeIds.value = []
    scheduleItems.value = []
    showDrawer.value = true
  }
}

// ==================== 初始化 ====================
watch(dateDays, (days) => {
  if (days.length > 0 && !selectedDate.value) {
    selectedDate.value = days[0]
  }
}, { immediate: true })

onMounted(() => {
  loadSchedules()
  loadAttendees()
})
</script>

<style scoped lang="scss">
.schedule-panel {
  padding: 0;
}

.schedule-item {
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 6px;
  transition: background-color 0.2s;
  position: relative;

  &:hover {
    background-color: #f5f5f5;
  }
}

.schedule-time {
  font-size: 13px;
  color: #8c8c8c;
  margin-bottom: 2px;
}

.schedule-title {
  font-size: 15px;
  font-weight: 500;
  margin-bottom: 4px;
}

.schedule-meta {
  font-size: 13px;
  color: #595959;
  margin-bottom: 4px;
  display: flex;
  align-items: center;
}

.schedule-delete-btn {
  position: absolute;
  top: 8px;
  right: 8px;
}

:deep(.ant-transfer) {
  justify-content: center;
  margin-bottom: 16px;
}

:deep(.ant-timeline) {
  padding-top: 8px;
}
</style>
