<template>
  <a-spin :spinning="loading">
    <!-- 1. 智能操作栏 -->
    <SmartActionBar description="基于参会人住宿需求，智能分配房间（CSP约束满足）">
      <a-button type="primary" @click="handleAutoAssign" :loading="assigning">
        <ThunderboltOutlined />智能分房
      </a-button>
      <a-button @click="showAddHotelModal"><PlusOutlined />添加酒店</a-button>
      <a-button @click="showAddRoomModal"><PlusOutlined />添加房间</a-button>
    </SmartActionBar>

    <!-- 2. 统计栏 -->
    <a-row :gutter="16" style="margin: 16px 0">
      <a-col :span="6"><StatCard title="酒店数" :value="hotels.length" /></a-col>
      <a-col :span="6"><StatCard title="总房间数" :value="totalRooms" /></a-col>
      <a-col :span="6"><StatCard title="已分配" :value="assignedRooms" :progress="assignProgress" /></a-col>
      <a-col :span="6"><StatCard title="需住宿人数" :value="needAccommodation" /></a-col>
    </a-row>

    <!-- 3. 双视图切换 -->
    <a-tabs v-model:activeKey="activeTab">
      <!-- Tab 1: 酒店/房间视图 -->
      <a-tab-pane key="hotels" tab="酒店房间视图">
        <a-empty v-if="!hotels.length" description="暂无酒店数据" />
        <a-collapse v-else v-model:activeKey="expandedHotels" accordion>
          <a-collapse-panel v-for="hotel in hotels" :key="hotel.id" :header="hotel.hotelName">
            <template #extra>
              <a-tag color="blue">{{ getRoomCount(hotel) }} 间</a-tag>
              <a-tag :color="getAssignedCount(hotel) === getRoomCount(hotel) ? 'green' : 'orange'">
                已分 {{ getAssignedCount(hotel) }} 间
              </a-tag>
            </template>
            <a-empty v-if="!hotel.rooms?.length" description="暂无房间" />
            <a-row v-else :gutter="[12, 12]">
              <a-col v-for="room in hotel.rooms" :key="room.id" :span="6">
                <RoomCard
                  :room="toRoomData(room)"
                  @click="showRoomDetail(room)"
                  @drop="handleRoomDrop(room, $event)"
                />
              </a-col>
            </a-row>
          </a-collapse-panel>
        </a-collapse>
      </a-tab-pane>

      <!-- Tab 2: 人员视图 -->
      <a-tab-pane key="attendees" tab="人员分配视图" force-render>
        <a-table
          :columns="attendeeColumns"
          :data-source="accommodationAttendees"
          row-key="id"
          :pagination="{ pageSize: 20, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 人` }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'roomInfo'">
              <span v-if="record.roomNumber">{{ record.hotelName }} - {{ record.roomNumber }}</span>
              <a-tag v-else color="orange">未分配</a-tag>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-button type="link" size="small" @click="handleManualAssignForAttendee(record)">
                {{ record.roomNumber ? '换房' : '分配' }}
              </a-button>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <!-- Tab 3: 需求统计 -->
      <a-tab-pane key="demandStats" tab="需求统计">
        <div style="margin-bottom: 12px; display: flex; justify-content: space-between; align-items: center">
          <span style="font-weight: 500">住宿需求统计表</span>
          <span style="display: flex; gap: 8px">
            <a-button size="small" @click="handleExportDemandStats" :loading="exportingDemandStats">
              <DownloadOutlined />导出Excel
            </a-button>
            <a-button size="small" @click="loadDemandStats" :loading="loadingStats">
            </a-button>
          </span>
        </div>
        <a-spin :spinning="loadingStats">
          <a-table
            v-if="demandStats"
            :columns="demandColumns"
            :data-source="demandTableData"
            row-key="date"
            :pagination="false"
            bordered
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'date'">
                <span style="font-weight: 500">{{ record.date }}</span>
              </template>
              <template v-else-if="column.dataIndex === 'totalDemand'">
                <a-tag color="purple">{{ record.totalDemand }}</a-tag>
              </template>
              <template v-else-if="isRoomTypeColumn(column)">
                <span
                  :style="getCellStyle(record[colDataIndex(column)])"
                  @click="handleRoomTypeCellClick(record, colDataIndex(column))"
                >
                  {{ getCellText(record[colDataIndex(column)]) }}
                </span>
              </template>
            </template>
            <template #summary>
              <a-table-summary-row>
                <a-table-summary-cell :index="0">
                  <span style="font-weight: 600">合计</span>
                </a-table-summary-cell>
                <a-table-summary-cell
                  v-for="(col, idx) in demandRoomTypeKeys"
                  :key="col"
                  :index="idx + 1"
                >
                  <a-tag color="blue">{{ getRoomTypeTotalDemand(col) }}</a-tag>
                </a-table-summary-cell>
                <a-table-summary-cell :index="demandRoomTypeKeys.length + 1">
                  <a-tag color="purple">{{ totalDemandSum }}</a-tag>
                </a-table-summary-cell>
              </a-table-summary-row>
            </template>
          </a-table>
          <EmptyState v-else size="small" title="暂无统计数据，点击刷新加载" />
        </a-spin>
      </a-tab-pane>
    </a-tabs>

    <!-- 4. 手动分配 Modal -->
    <a-modal v-model:open="showAssignModal" title="手动分配房间" @ok="confirmAssign" :confirm-loading="submitting">
      <a-form layout="vertical">
        <a-form-item label="选择人员">
          <a-select
            v-model:value="assignForm.attendeeId"
            :options="unassignedAttendeeOptions"
            show-search
            :filter-option="filterOption"
            placeholder="搜索并选择人员"
          />
        </a-form-item>
        <a-form-item label="选择酒店">
          <a-select
            v-model:value="assignForm.hotelId"
            :options="hotelOptions"
            placeholder="选择酒店"
            @change="onHotelChange"
          />
        </a-form-item>
        <a-form-item label="选择房间">
          <a-select
            v-model:value="assignForm.roomId"
            :options="availableRoomOptions"
            placeholder="选择房间"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 5. 添加酒店 Modal -->
    <a-modal v-model:open="showHotelModal" title="添加酒店" @ok="confirmAddHotel" :confirm-loading="submitting">
      <a-form layout="vertical">
        <a-form-item label="酒店名称" required>
          <a-input v-model:value="hotelForm.hotelName" placeholder="请输入酒店名称" />
        </a-form-item>
        <a-form-item label="地址">
          <a-input v-model:value="hotelForm.address" placeholder="请输入地址" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="联系人">
              <a-input v-model:value="hotelForm.contact" placeholder="联系人" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系电话">
              <a-input v-model:value="hotelForm.contactPhone" placeholder="联系电话" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="协议价格">
          <a-input v-model:value="hotelForm.agreedPrice" placeholder="如：标间280/晚" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="hotelForm.remark" :rows="2" placeholder="备注信息" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 6. 房型人员列表 Modal -->
    <a-modal
      v-model:open="guestModalVisible"
      :title="guestModalTitle"
      :footer="null"
      width="800px"
    >
      <a-table
        :dataSource="roomTypeGuests"
        :columns="guestColumns"
        :loading="guestLoading"
        :pagination="false"
        size="small"
        rowKey="attendeeId"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'preferredRoomType'">
            <a-select
              v-model:value="record.preferredRoomType"
              size="small"
              style="width: 120px"
              @change="(val: SelectValue) => handleGuestRoomTypeChange(record as RoomTypeGuestDto, String(val))"
            >
              <a-select-option value="标单">标单</a-select-option>
              <a-select-option value="标双">标双</a-select-option>
              <a-select-option value="套房">套房</a-select-option>
              <a-select-option value="大床房">大床房</a-select-option>
              <a-select-option value="行政大床">行政大床</a-select-option>
              <a-select-option value="其他">其他</a-select-option>
            </a-select>
          </template>
        </template>
      </a-table>
    </a-modal>

    <!-- 7. 添加房间 Modal -->
    <a-modal v-model:open="showRoomModal" title="添加房间" @ok="confirmAddRoom" :confirm-loading="submitting">
      <a-form layout="vertical">
        <a-form-item label="所属酒店" required>
          <a-select v-model:value="roomForm.hotelId" :options="hotelOptions" placeholder="选择酒店" />
        </a-form-item>
        <a-form-item label="房间号" required>
          <a-input v-model:value="roomForm.roomNumber" placeholder="如：301" />
        </a-form-item>
        <a-form-item label="房型">
          <a-select v-model:value="roomForm.roomType" placeholder="选择房型">
            <a-select-option value="标单">标单</a-select-option>
            <a-select-option value="标双">标双</a-select-option>
            <a-select-option value="套房">套房</a-select-option>
            <a-select-option value="大床房">大床房</a-select-option>
            <a-select-option value="行政大床">行政大床</a-select-option>
            <a-select-option value="其他">其他</a-select-option>
          </a-select>
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="入住日期" required>
              <a-date-picker v-model:value="roomForm.checkInDate" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="退房日期" required>
              <a-date-picker v-model:value="roomForm.checkOutDate" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注">
          <a-textarea v-model:value="roomForm.remark" :rows="2" placeholder="备注信息" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-spin>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { ThunderboltOutlined, PlusOutlined, DownloadOutlined } from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import SmartActionBar from '../components/SmartActionBar.vue'
import StatCard from '../components/StatCard.vue'
import RoomCard from '../components/RoomCard.vue'
import type { RoomData, GuestData } from '../components/RoomCard.vue'
import {
  getHotels, createHotel, batchAddRooms, assignRoom,
  autoAssignAccommodation, getAttendees,
  getUnassignedAttendees, getAccommodationDemandStats,
  getRoomTypeGuests, updateAttendeeRoomPreference,
  exportAccommodationDemandStatsExcel,
} from '@/api/conference'
import type {
  HotelDto, RoomDto, CreateHotelRequest,
  AttendeeListItemDto, AccommodationDemandStatsDto, RoomTypeStat,
  RoomTypeGuestDto,
} from '@/api/conference'
import type { SelectValue } from 'ant-design-vue/es/select'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ==================== 状态 ====================
const loading = ref(false)
const assigning = ref(false)
const submitting = ref(false)
const activeTab = ref('hotels')
const expandedHotels = ref<number[]>([])

const hotels = ref<HotelDto[]>([])
const accommodationAttendees = ref<any[]>([])

// ==================== 计算属性 ====================
const totalRooms = computed(() =>
  hotels.value.reduce((sum, h) => sum + (h.rooms?.length || 0), 0)
)

const assignedRooms = computed(() =>
  hotels.value.reduce(
    (sum, h) => sum + (h.rooms?.filter(r => r.guests?.length > 0).length || 0),
    0
  )
)

const assignProgress = computed(() =>
  totalRooms.value > 0 ? Math.round((assignedRooms.value / totalRooms.value) * 100) : 0
)

const needAccommodation = computed(() => accommodationAttendees.value.length)

// ==================== 酒店辅助 ====================
function getRoomCount(hotel: HotelDto) {
  return hotel.rooms?.length || 0
}

function getAssignedCount(hotel: HotelDto) {
  return hotel.rooms?.filter(r => r.guests?.length > 0).length || 0
}

// ==================== RoomCard 数据适配 ====================
function toRoomData(room: RoomDto): RoomData {
  const guests: GuestData[] = (room.guests || []).map(g => ({
    id: g.id,
    name: g.name,
    company: g.organization,
    gender: g.gender,
  }))
  const maxGuests = room.roomType === '标单' ? 1 : room.roomType === '套房' ? 4 : 2
  return {
    id: room.id,
    roomNumber: room.roomNumber || `房间${room.id}`,
    roomType: room.roomType || '标间',
    status: guests.length > 0 ? '已分配' : '空闲',
    guests,
    maxGuests,
  }
}

// ==================== 人员视图列定义 ====================
const attendeeColumns = [
  { title: '姓名', dataIndex: 'name', width: 100 },
  { title: '单位', dataIndex: 'organization', width: 150, ellipsis: true },
  { title: '性别', dataIndex: 'gender', width: 60 },
  { title: '房型偏好', dataIndex: 'roomTypePreference', width: 100 },
  { title: '当前房间', dataIndex: 'roomInfo', width: 180 },
  { title: '操作', dataIndex: 'action', width: 100, fixed: 'right' as const },
]

// ==================== 手动分配 ====================
const showAssignModal = ref(false)
const assignForm = ref<{
  attendeeId: number | undefined
  hotelId: number | undefined
  roomId: number | undefined
}>({
  attendeeId: undefined,
  hotelId: undefined,
  roomId: undefined,
})

const hotelOptions = computed(() =>
  hotels.value.map(h => ({ label: h.hotelName, value: h.id }))
)

const availableRoomOptions = computed(() => {
  if (!assignForm.value.hotelId) return []
  const hotel = hotels.value.find(h => h.id === assignForm.value.hotelId)
  if (!hotel) return []
  return (hotel.rooms || [])
    .filter(r => !r.guests?.length)
    .map(r => ({
      label: `${r.roomNumber || r.id} (${r.roomType || '标间'})`,
      value: r.id,
    }))
})

const unassignedAttendeeOptions = computed(() =>
  accommodationAttendees.value
    .filter(a => !a.roomNumber)
    .map(a => ({ label: `${a.name}${a.organization ? ' - ' + a.organization : ''}`, value: a.id }))
)

function filterOption(input: string, option: any) {
  return (option?.label || '').toLowerCase().includes(input.toLowerCase())
}

function onHotelChange() {
  assignForm.value.roomId = undefined
}

function handleManualAssignForAttendee(record: any) {
  assignForm.value = {
    attendeeId: record.id,
    hotelId: undefined,
    roomId: undefined,
  }
  showAssignModal.value = true
}

function showRoomDetail(room: RoomDto) {
  // 点击房间卡片 → 打开分配弹窗
  const hotel = hotels.value.find(h => h.rooms?.some(r => r.id === room.id))
  assignForm.value = {
    attendeeId: undefined,
    hotelId: hotel?.id,
    roomId: room.id,
  }
  showAssignModal.value = true
}

function handleRoomDrop(_room: RoomDto, _event: any) {
  // 拖拽暂不处理
}

async function confirmAssign() {
  if (!assignForm.value.roomId || !assignForm.value.attendeeId) {
    message.warning('请选择人员和房间')
    return
  }
  submitting.value = true
  try {
    await assignRoom(assignForm.value.roomId, {
      attendeeIds: [assignForm.value.attendeeId],
    })
    message.success('分配成功')
    showAssignModal.value = false
    loadData()
  } catch (err: any) {
    message.error('分配失败：' + (err.message || ''))
  } finally {
    submitting.value = false
  }
}

// ==================== 智能分房 ====================
async function handleAutoAssign() {
  assigning.value = true
  try {
    await autoAssignAccommodation(props.eventId)
    message.success('智能分房完成')
    loadData()
  } catch (err: any) {
    message.error('分房失败：' + (err.message || ''))
  } finally {
    assigning.value = false
  }
}

// ==================== 添加酒店 ====================
const showHotelModal = ref(false)
const hotelForm = ref<CreateHotelRequest>({
  hotelName: '',
  address: '',
  contact: '',
  contactPhone: '',
  agreedPrice: '',
  remark: '',
})

function showAddHotelModal() {
  hotelForm.value = { hotelName: '', address: '', contact: '', contactPhone: '', agreedPrice: '', remark: '' }
  showHotelModal.value = true
}

async function confirmAddHotel() {
  if (!hotelForm.value.hotelName) {
    message.warning('请输入酒店名称')
    return
  }
  submitting.value = true
  try {
    await createHotel(props.eventId, hotelForm.value)
    message.success('酒店添加成功')
    showHotelModal.value = false
    loadData()
  } catch (err: any) {
    message.error('添加失败：' + (err.message || ''))
  } finally {
    submitting.value = false
  }
}

// ==================== 添加房间 ====================
const showRoomModal = ref(false)
const roomForm = ref<{
  hotelId: number | undefined
  roomNumber: string
  roomType: string
  checkInDate: Dayjs | undefined
  checkOutDate: Dayjs | undefined
  remark: string
}>({
  hotelId: undefined,
  roomNumber: '',
  roomType: '标双',
  checkInDate: undefined,
  checkOutDate: undefined,
  remark: '',
})

function showAddRoomModal() {
  roomForm.value = {
    hotelId: hotels.value.length ? hotels.value[0].id : undefined,
    roomNumber: '',
    roomType: '标双',
    checkInDate: props.eventData?.startDate ? dayjs(props.eventData.startDate) : undefined,
    checkOutDate: props.eventData?.endDate ? dayjs(props.eventData.endDate) : undefined,
    remark: '',
  }
  showRoomModal.value = true
}

async function confirmAddRoom() {
  if (!roomForm.value.hotelId) {
    message.warning('请选择所属酒店')
    return
  }
  if (!roomForm.value.roomNumber) {
    message.warning('请输入房间号')
    return
  }
  if (!roomForm.value.checkInDate || !roomForm.value.checkOutDate) {
    message.warning('请选择入住和退房日期')
    return
  }
  submitting.value = true
  try {
    await batchAddRooms(roomForm.value.hotelId, {
      rooms: [{
        roomNumber: roomForm.value.roomNumber,
        roomType: roomForm.value.roomType,
        checkInDate: roomForm.value.checkInDate.format('YYYY-MM-DD'),
        checkOutDate: roomForm.value.checkOutDate.format('YYYY-MM-DD'),
        remark: roomForm.value.remark || undefined,
      }],
    })
    message.success('房间添加成功')
    showRoomModal.value = false
    loadData()
  } catch (err: any) {
    message.error('添加失败：' + (err.message || ''))
  } finally {
    submitting.value = false
  }
}

// ==================== 需求统计 ====================
const demandStats = ref<AccommodationDemandStatsDto | null>(null)
const loadingStats = ref(false)
const exportingDemandStats = ref(false)

const handleExportDemandStats = async () => {
  exportingDemandStats.value = true
  try {
    const blob: any = await exportAccommodationDemandStatsExcel(props.eventId)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = '住宿需求统计.xlsx'
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('导出失败')
  } finally {
    exportingDemandStats.value = false
  }
}

async function loadDemandStats() {
  loadingStats.value = true
  try {
    const res = await getAccommodationDemandStats(props.eventId)
    demandStats.value = res
  } catch {
    message.error('加载统计数据失败')
  } finally {
    loadingStats.value = false
  }
}

const demandRoomTypeKeys = computed(() => {
  if (!demandStats.value) return []
  const keys = new Set<string>()
  for (const day of demandStats.value.dailyStats) {
    for (const k of Object.keys(day.roomTypes)) {
      keys.add(k)
    }
  }
  // 预定义排序
  const order = ['标单', '标双', '套房', '大床房', '行政大床', '其他']
  return [...keys].sort((a, b) => {
    const ai = order.indexOf(a)
    const bi = order.indexOf(b)
    return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi)
  })
})

interface DemandColumn {
  title: string
  dataIndex: string
  width: number
  isRoomType?: boolean
}

const demandColumns = computed<DemandColumn[]>(() => {
  const cols: DemandColumn[] = [{ title: '日期', dataIndex: 'date', width: 90 }]
  for (const key of demandRoomTypeKeys.value) {
    cols.push({ title: key, dataIndex: key, width: 100, isRoomType: true })
  }
  cols.push({ title: '当日总需求', dataIndex: 'totalDemand', width: 100 })
  return cols
})

// #bodyCell 中的 column 由 AntD 推断为 ColumnType，借助下列守卫窄化到 DemandColumn
function isRoomTypeColumn(column: unknown): boolean {
  return !!(column as DemandColumn).isRoomType
}

function colDataIndex(column: unknown): string {
  return String((column as DemandColumn).dataIndex)
}

const demandTableData = computed(() => {
  if (!demandStats.value) return []
  return demandStats.value.dailyStats.map(day => {
    const row: Record<string, any> = {
      date: dayjs(day.date).format('MM-DD'),
      rawDate: day.date,
      totalDemand: day.totalDemand,
    }
    for (const key of demandRoomTypeKeys.value) {
      row[key] = day.roomTypes[key] || null
    }
    return row
  })
})

const totalDemandSum = computed(() => {
  if (!demandStats.value?.dailyStats) return 0
  return demandStats.value.dailyStats.reduce((sum, day) => sum + (day.totalDemand || 0), 0)
})

function getRoomTypeTotalDemand(roomType: string): number {
  if (!demandStats.value?.dailyStats) return 0
  return demandStats.value.dailyStats.reduce((sum, day) => {
    const stat = day.roomTypes?.[roomType]
    return sum + (stat?.demand || 0)
  }, 0)
}

function getCellStyle(stat: RoomTypeStat | null): Record<string, string> {
  if (!stat) return {}
  if (stat.available === 0 && stat.allocated === 0 && stat.demand > 0) {
    return { color: 'var(--color-warning)', cursor: 'pointer' }  // 警告
  }
  if (stat.available > 0 && stat.allocated >= stat.available) {
    return { color: 'var(--color-success)', cursor: 'pointer' }  // 满房绿色
  }
  if (stat.available > 0) {
    return { color: 'var(--color-info)', cursor: 'pointer' }  // 有空余（信息）
  }
  return {}
}

function getCellText(stat: RoomTypeStat | null): string {
  if (!stat) return '-'
  if (stat.available === 0 && stat.allocated === 0 && stat.demand === 0) return '-'
  if (stat.available === 0 && stat.allocated === 0 && stat.demand > 0) return `需${stat.demand}人`
  return `${stat.allocated}/${stat.available} (需${stat.demand}人)`
}

// ==================== 房型人员弹窗 ====================
const guestModalVisible = ref(false)
const guestModalTitle = ref('')
const roomTypeGuests = ref<RoomTypeGuestDto[]>([])
const guestLoading = ref(false)

const guestColumns = [
  {
    title: '姓名',
    dataIndex: 'name',
    width: 120,
    customRender: ({ record }: any) => {
      if (record.primaryGuestName) {
        return `${record.primaryGuestName}-${record.name}`
      }
      return record.name
    }
  },
  { title: '性别', dataIndex: 'gender', width: 60 },
  { title: '单位', dataIndex: 'organization', ellipsis: true },
  { title: '电话', dataIndex: 'phone', width: 120 },
  { title: '房型偏好', dataIndex: 'preferredRoomType', width: 140, key: 'preferredRoomType' },
  { title: '酒店', dataIndex: 'hotelName', width: 120 },
  { title: '房号', dataIndex: 'roomNumber', width: 80 },
]

async function handleGuestRoomTypeChange(record: RoomTypeGuestDto, newRoomType: string) {
  try {
    await updateAttendeeRoomPreference(record.attendeeId, newRoomType)
    message.success('房型偏好已更新')
    await loadDemandStats()
  } catch (e) {
    message.error('更新失败')
    console.error(e)
  }
}

async function handleRoomTypeCellClick(record: any, roomType: string) {
  if (!props.eventId) return
  const stat = record[roomType] as RoomTypeStat | null
  if (!stat || (stat.available === 0 && stat.allocated === 0 && stat.demand === 0)) return

  guestModalTitle.value = `${record.date} - ${roomType} 入住人员`
  guestModalVisible.value = true
  guestLoading.value = true

  try {
    const rawDate = record.rawDate
    const dateStr = dayjs(rawDate).format('YYYY-MM-DD')
    const res = await getRoomTypeGuests(props.eventId, dateStr, roomType)
    roomTypeGuests.value = Array.isArray(res) ? res : (res as any)?.data || []
  } catch (e) {
    console.error('获取房型人员失败', e)
    roomTypeGuests.value = []
  } finally {
    guestLoading.value = false
  }
}

// ==================== 数据加载 ====================
async function loadData() {
  loading.value = true
  try {
    const [hotelsData, attendeesData] = await Promise.all([
      getHotels(props.eventId),
      getAttendees(props.eventId, { needAccommodation: true, status: '已确认' }),
    ])
    hotels.value = (hotelsData as any) || []
    // 构建人员视图数据：将房间分配信息合入参会人
    const rawAttendees: AttendeeListItemDto[] = Array.isArray(attendeesData)
      ? attendeesData
      : (attendeesData as any)?.items || []
    const roomMap = new Map<number, { hotelName: string; roomNumber: string; roomId: number }>()
    for (const hotel of hotels.value) {
      for (const room of hotel.rooms || []) {
        for (const guest of room.guests || []) {
          roomMap.set(guest.id, {
            hotelName: hotel.hotelName,
            roomNumber: room.roomNumber || `${room.id}`,
            roomId: room.id,
          })
        }
      }
    }
    accommodationAttendees.value = rawAttendees.map(a => {
      const info = roomMap.get(a.id)
      return {
        ...a,
        hotelName: info?.hotelName || '',
        roomNumber: info?.roomNumber || '',
        roomId: info?.roomId,
        roomTypePreference: '',
      }
    })
    // 默认展开第一个酒店
    if (hotels.value.length && !expandedHotels.value.length) {
      expandedHotels.value = [hotels.value[0].id]
    }
  } finally {
    loading.value = false
  }
}

watch(() => props.eventId, () => {
  if (props.eventId) loadData()
}, { immediate: false })

watch(activeTab, (tab) => {
  if (tab === 'demandStats' && !demandStats.value) {
    loadDemandStats()
  }
})

onMounted(() => {
  if (props.eventId) loadData()
})
</script>

<style scoped lang="scss">
:deep(.ant-collapse-header) {
  font-weight: 500;
}

:deep(.ant-tabs-nav) {
  margin-bottom: 16px;
}

:deep(.ant-collapse-content-box) {
  padding: 12px !important;
}
</style>
