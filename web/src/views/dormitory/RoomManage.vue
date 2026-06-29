<template>
  <div class="page-container">
    <PageHeader :title="buildingInfo?.name ? `${buildingInfo.name} - 房间管理` : '房间管理'" description="管理房间及床位信息">
      <template #left>
        <a-button type="link" @click="handleBack" style="padding: 0">
          <template #icon><ArrowLeftOutlined /></template>返回
        </a-button>
      </template>
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增房间
        </a-button>
      </template>
    </PageHeader>

    <a-card v-if="buildingInfo" :bordered="false" style="margin-bottom: 12px">
      <a-descriptions :column="3" size="small">
        <a-descriptions-item label="楼栋编码">{{ buildingInfo.code }}</a-descriptions-item>
        <a-descriptions-item label="总楼层">{{ buildingInfo.totalFloors }} 层</a-descriptions-item>
        <a-descriptions-item label="地址">{{ buildingInfo.address || '-' }}</a-descriptions-item>
      </a-descriptions>
    </a-card>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'roomNumber'">
            <a-tooltip :title="record.roomNumber">{{ record.roomNumber }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'roomType'">
            {{ getRoomTypeText(record.roomType) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'occupancy'">
            <a-progress
              :percent="getOccupancyRate(record)"
              :size="'small'"
              :status="getOccupancyRate(record) >= 100 ? 'exception' : 'normal'"
            />
            <span class="occupancy-text">{{ record.occupiedBeds }}/{{ record.bedsCount }}</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-button type="link" size="small" @click="handleManageBeds(record)">
              <BankOutlined />管理床位
            </a-button>
            <a-popconfirm
              title="确定删除该房间吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无房间数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑房间弹窗 -->
    <a-modal
      v-model:open="roomDialogVisible"
      :title="roomDialogType === 'add' ? '新增房间' : '编辑房间'"
      width="600px"
      :destroy-on-close="true"
      @cancel="roomDialogVisible = false"
    >
      <a-form
        ref="roomFormRef"
        :model="roomFormData"
        :rules="roomFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="楼层" name="floor">
              <a-input-number
                v-model:value="roomFormData.floor"
                :min="1"
                :max="buildingInfo?.totalFloors || 100"
                style="width: 100%"
                placeholder="请输入楼层"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="房号" name="roomNumber">
              <a-input
                v-model:value="roomFormData.roomNumber"
                placeholder="请输入房号"
                :maxlength="20"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="房间类型" name="roomType">
              <a-select
                v-model:value="roomFormData.roomType"
                placeholder="请选择房间类型"
                :options="[
                  { label: '标准间', value: 'standard' },
                  { label: '单人间', value: 'single' },
                  { label: '套间', value: 'suite' },
                ]"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="床位数" name="bedsCount">
              <a-input-number
                v-model:value="roomFormData.bedsCount"
                :min="1"
                :max="20"
                style="width: 100%"
                placeholder="请输入床位数"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea
                v-model:value="roomFormData.remark"
                :rows="2"
                placeholder="请输入备注"
                :maxlength="500"
                show-count
              />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="roomDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="roomSubmitLoading" @click="handleRoomSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 床位管理弹窗 -->
    <a-modal
      v-model:open="bedDialogVisible"
      title="床位管理"
      width="800px"
      :destroy-on-close="true"
      @cancel="bedDialogVisible = false"
    >
      <div class="bed-management-header">
        <span class="room-info">房间: {{ currentRoom?.roomNumber }}</span>
        <a-button type="primary" size="small" @click="handleAddBed">
          <PlusOutlined />新增床位
        </a-button>
      </div>
      <a-table
        :columns="bedColumns"
        :data-source="bedList"
        :loading="bedLoading"
        :pagination="false"
        row-key="id"
        bordered
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'bedType'">
            {{ getBedTypeText(record.bedType) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getBedStatusColor(record.status)">
              {{ getBedStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEditBed(record)">
              <EditOutlined />
            </a-button>
            <a-popconfirm
              title="确定删除该床位吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDeleteBed(record)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无床位数据" />
        </template>
      </a-table>
      <template #footer>
        <a-button @click="bedDialogVisible = false">关闭</a-button>
      </template>
    </a-modal>

    <!-- 新增/编辑床位弹窗 -->
    <a-modal
      v-model:open="bedEditDialogVisible"
      :title="bedDialogType === 'add' ? '新增床位' : '编辑床位'"
      width="500px"
      :destroy-on-close="true"
      @cancel="bedEditDialogVisible = false"
    >
      <a-form
        ref="bedFormRef"
        :model="bedFormData"
        :rules="bedFormRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="床位号" name="bedNumber">
          <a-input
            v-model:value="bedFormData.bedNumber"
            placeholder="请输入床位号"
            :maxlength="20"
          />
        </a-form-item>
        <a-form-item label="床位类型" name="bedType">
          <a-select
            v-model:value="bedFormData.bedType"
            placeholder="请选择床位类型"
            :options="[
              { label: '上铺', value: 'upper' },
              { label: '下铺', value: 'lower' },
              { label: '单人床', value: 'single' },
            ]"
          />
        </a-form-item>
        <a-form-item label="状态" name="status">
          <a-select
            v-model:value="bedFormData.status"
            placeholder="请选择状态"
            :options="[
              { label: '空闲', value: 1 },
              { label: '已入住', value: 2 },
              { label: '维修中', value: 3 },
            ]"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="bedFormData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="200"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="bedEditDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="bedSubmitLoading" @click="handleBedSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, BankOutlined, ArrowLeftOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getBuildingDetail,
  getRoomList,
  createRoom,
  updateRoom,
  deleteRoom,
  getBedList,
  createBed,
  updateBed,
  deleteBed,
  type BuildingDto,
  type RoomDto,
  type BedDto,
} from '@/api/dormitory'

const route = useRoute()
const router = useRouter()
const buildingId = computed(() => {
  const id = route.params.buildingId
  return id ? parseInt(id as string, 10) : 0
})

// 楼栋信息
const buildingInfo = ref<BuildingDto | null>(null)

// 房间表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '楼层', dataIndex: 'floor', key: 'floor', width: 80, align: 'center' as const },
  { title: '房号', dataIndex: 'roomNumber', key: 'roomNumber', width: 100 },
  { title: '房间类型', dataIndex: 'roomType', key: 'roomType', width: 100 },
  { title: '床位数', dataIndex: 'bedsCount', key: 'bedsCount', width: 80, align: 'center' as const },
  { title: '入住率', dataIndex: 'occupancy', key: 'occupancy', width: 150 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

// 床位表格列配置
const bedColumns = [
  { title: '床位号', dataIndex: 'bedNumber', key: 'bedNumber', width: 120 },
  { title: '床位类型', dataIndex: 'bedType', key: 'bedType', width: 100 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', key: 'remark', ellipsis: true },
  { title: '操作', dataIndex: 'action', key: 'action', width: 100, align: 'center' as const },
]

// 房间表格数据
const loading = ref(false)
const tableData = ref<RoomDto[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchRoomList()
}

// 房间弹窗相关
const roomDialogVisible = ref(false)
const roomDialogType = ref<'add' | 'edit'>('add')
const roomFormRef = ref<FormInstance>()
const roomSubmitLoading = ref(false)
const currentRoomId = ref<number | null>(null)

const roomFormData = reactive({
  floor: 1,
  roomNumber: '',
  roomType: 'standard' as 'standard' | 'single' | 'suite',
  bedsCount: 4,
  remark: '',
})

const roomFormRules: Record<string, Rule[]> = {
  floor: [{ required: true, message: '请输入楼层', trigger: 'blur' }],
  roomNumber: [{ required: true, message: '请输入房号', trigger: 'blur' }],
  roomType: [{ required: true, message: '请选择房间类型', trigger: 'change' }],
  bedsCount: [{ required: true, message: '请输入床位数', trigger: 'blur' }],
}

// 床位管理相关
const bedDialogVisible = ref(false)
const bedEditDialogVisible = ref(false)
const bedDialogType = ref<'add' | 'edit'>('add')
const bedFormRef = ref<FormInstance>()
const bedSubmitLoading = ref(false)
const bedLoading = ref(false)
const currentRoom = ref<RoomDto | null>(null)
const bedList = ref<BedDto[]>([])
const currentBedId = ref<number | null>(null)

const bedFormData = reactive({
  bedNumber: '',
  bedType: 'lower' as 'upper' | 'lower' | 'single',
  status: 1 as number,
  remark: '',
})

const bedFormRules: Record<string, Rule[]> = {
  bedNumber: [{ required: true, message: '请输入床位号', trigger: 'blur' }],
  bedType: [{ required: true, message: '请选择床位类型', trigger: 'change' }],
  status: [{ required: true, message: '请选择状态', trigger: 'change' }],
}

// 获取楼栋信息
async function fetchBuildingInfo() {
  if (!buildingId.value) return
  try {
    const res = await getBuildingDetail(buildingId.value)
    buildingInfo.value = res
  } catch (error) {
    console.error('获取楼栋信息失败:', error)
    message.error('获取楼栋信息失败')
  }
}

// 获取房间列表
async function fetchRoomList() {
  if (!buildingId.value) return
  loading.value = true
  try {
    const res = await getRoomList(buildingId.value)
    if (res) {
      tableData.value = res
      pagination.total = res.length
    }
  } catch (error) {
    console.error('获取房间列表失败:', error)
    message.error('获取房间列表失败')
  } finally {
    loading.value = false
  }
}

// 返回楼栋列表
function handleBack() {
  router.push({ name: 'BuildingManage' })
}

// 获取房间类型文本
function getRoomTypeText(type: string): string {
  const typeMap: Record<string, string> = {
    standard: '标准间',
    single: '单人间',
    suite: '套间',
  }
  return typeMap[type] || type
}

// 获取入住率
function getOccupancyRate(record: any): number {
  if (!record.bedsCount || record.bedsCount === 0) return 0
  return Math.round((record.occupiedBeds / record.bedsCount) * 100)
}

// 重置房间表单
function resetRoomForm() {
  roomFormData.floor = 1
  roomFormData.roomNumber = ''
  roomFormData.roomType = 'standard'
  roomFormData.bedsCount = 4
  roomFormData.remark = ''
}

// 新增房间
function handleAdd() {
  roomDialogType.value = 'add'
  currentRoomId.value = null
  resetRoomForm()
  roomDialogVisible.value = true
}

// 编辑房间
function handleEdit(row: any) {
  roomDialogType.value = 'edit'
  currentRoomId.value = row.id
  resetRoomForm()
  roomFormData.floor = row.floor
  roomFormData.roomNumber = row.roomNumber
  roomFormData.roomType = row.roomType as 'standard' | 'single' | 'suite'
  roomFormData.bedsCount = row.bedsCount
  roomFormData.remark = row.remark || ''
  roomDialogVisible.value = true
}

// 提交房间表单
async function handleRoomSubmit() {
  if (!roomFormRef.value || !buildingId.value) return
  try {
    await roomFormRef.value.validate()
  } catch {
    return
  }

  roomSubmitLoading.value = true
  try {
    const data = {
      floor: roomFormData.floor,
      roomNumber: roomFormData.roomNumber,
      roomType: roomFormData.roomType,
      bedsCount: roomFormData.bedsCount,
      remark: roomFormData.remark || undefined,
    }

    if (roomDialogType.value === 'add') {
      await createRoom(buildingId.value, data)
      message.success('新增成功')
    } else {
      await updateRoom(buildingId.value, currentRoomId.value!, data)
      message.success('更新成功')
    }
    roomDialogVisible.value = false
    fetchRoomList()
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    roomSubmitLoading.value = false
  }
}

// 删除房间
async function handleDelete(row: any) {
  if (!buildingId.value) return
  try {
    await deleteRoom(buildingId.value, row.id)
    message.success('删除成功')
    fetchRoomList()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

// 管理床位
async function handleManageBeds(row: any) {
  currentRoom.value = row
  bedDialogVisible.value = true
  await fetchBedList(row.id)
}

// 获取床位列表
async function fetchBedList(roomId: number) {
  bedLoading.value = true
  try {
    const res = await getBedList(roomId)
    bedList.value = res
  } catch (error) {
    console.error('获取床位列表失败:', error)
    message.error('获取床位列表失败')
  } finally {
    bedLoading.value = false
  }
}

// 获取床位状态颜色
function getBedStatusColor(status: number): string {
  const colorMap: Record<number, string> = {
    1: 'green',   // 空闲
    2: 'blue',    // 已入住
    3: 'orange',  // 维修中
  }
  return colorMap[status] || 'default'
}

// 获取床位状态文本
function getBedStatusText(status: number): string {
  const textMap: Record<number, string> = {
    1: '空闲',
    2: '已入住',
    3: '维修中',
  }
  return textMap[status] || '未知'
}

// 获取床位类型文本
function getBedTypeText(type: string): string {
  const typeMap: Record<string, string> = {
    upper: '上铺',
    lower: '下铺',
    single: '单人床',
  }
  return typeMap[type] || type
}

// 重置床位表单
function resetBedForm() {
  bedFormData.bedNumber = ''
  bedFormData.bedType = 'lower'
  bedFormData.status = 1
  bedFormData.remark = ''
}

// 新增床位
function handleAddBed() {
  bedDialogType.value = 'add'
  currentBedId.value = null
  resetBedForm()
  bedEditDialogVisible.value = true
}

// 编辑床位
function handleEditBed(row: any) {
  bedDialogType.value = 'edit'
  currentBedId.value = row.id
  resetBedForm()
  bedFormData.bedNumber = row.bedNumber
  bedFormData.bedType = row.bedType as 'upper' | 'lower' | 'single'
  bedFormData.status = row.status
  bedFormData.remark = row.remark || ''
  bedEditDialogVisible.value = true
}

// 提交床位表单
async function handleBedSubmit() {
  if (!bedFormRef.value || !currentRoom.value) return
  try {
    await bedFormRef.value.validate()
  } catch {
    return
  }

  bedSubmitLoading.value = true
  try {
    if (bedDialogType.value === 'add') {
      await createBed(currentRoom.value.id, {
        bedNumber: bedFormData.bedNumber,
        bedType: bedFormData.bedType,
        remark: bedFormData.remark || undefined,
      })
      message.success('新增成功')
    } else {
      await updateBed(currentRoom.value.id, currentBedId.value!, {
        bedNumber: bedFormData.bedNumber,
        bedType: bedFormData.bedType,
        remark: bedFormData.remark || undefined,
        status: bedFormData.status,
      })
      message.success('更新成功')
    }
    bedEditDialogVisible.value = false
    fetchBedList(currentRoom.value.id)
    fetchRoomList() // 刷新房间列表以更新床位数
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    bedSubmitLoading.value = false
  }
}

// 删除床位
async function handleDeleteBed(row: any) {
  if (!currentRoom.value) return
  try {
    await deleteBed(currentRoom.value.id, row.id)
    message.success('删除成功')
    fetchBedList(currentRoom.value.id)
    fetchRoomList() // 刷新房间列表以更新床位数
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

onMounted(() => {
  fetchBuildingInfo()
  fetchRoomList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.occupancy-text {
  font-size: 12px;
  color: $text-secondary;
  margin-left: 8px;
}

.bed-management-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;

  .room-info {
    font-size: 14px;
    font-weight: 500;
    color: $text-primary;
  }
}

.text-muted {
  color: $text-secondary;
}
</style>
