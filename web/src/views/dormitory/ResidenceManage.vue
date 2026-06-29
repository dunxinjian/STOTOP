<template>
  <div class="page-container">
    <PageHeader title="入住管理" description="管理员工宿舍入住与退宿">
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增入住
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-form-item label="员工姓名/ID" style="margin-bottom:0">
              <a-input
                v-model:value="searchForm.keyword"
                placeholder="请输入员工姓名或ID"
                allow-clear
                style="width: 200px"
                @keyup.enter="handleSearch"
              />
            </a-form-item>
            <a-form-item label="楼栋" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.buildingId"
                placeholder="全部楼栋"
                allow-clear
                style="width: 150px"
                :options="buildingOptions"
              />
            </a-form-item>
            <a-form-item label="状态" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.status"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="[
                  { label: '在住', value: 1 },
                  { label: '已退宿', value: 2 },
                ]"
              />
            </a-form-item>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-button type="primary" @click="handleSearch">
              <template #icon><SearchOutlined /></template>查询
            </a-button>
            <a-button @click="handleReset">
              <template #icon><ReloadOutlined /></template>重置
            </a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1100 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'employeeName'">
            <a-tooltip :title="record.employeeName">{{ record.employeeName }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'bedInfo'">
            <span>{{ record.buildingName }} - {{ record.roomNumber }} - {{ record.bedNumber }}</span>
          </template>
          <template v-if="column.dataIndex === 'checkInDate'">
            {{ formatDate(record.checkInDate) }}
          </template>
          <template v-if="column.dataIndex === 'checkOutDate'">
            {{ record.checkOutDate ? formatDate(record.checkOutDate) : '-' }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : record.status === 2 ? 'default' : 'warning'">
              {{ record.status === 1 ? '在住' : record.status === 2 ? '已退宿' : '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'employeeName'">
            <span>{{ record.employeeName || '-' }}</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button
              v-if="record.status === 1"
              type="link"
              size="small"
              @click="handleCheckOut(record)"
            >
              <LogoutOutlined />退宿
            </a-button>
            <a-popconfirm
              title="确定删除该入住记录吗？"
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
          <EmptyState description="暂无入住数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增入住弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      title="新增入住"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="楼栋" name="buildingId">
          <a-select
            v-model:value="formData.buildingId"
            placeholder="请选择楼栋"
            :options="buildingOptions"
            @change="handleBuildingChange"
          />
        </a-form-item>
        <a-form-item label="房间" name="roomId">
          <a-select
            v-model:value="formData.roomId"
            placeholder="请选择房间"
            :options="roomOptions"
            :disabled="!formData.buildingId"
            @change="handleRoomChange"
          />
        </a-form-item>
        <a-form-item label="床位" name="bedId">
          <a-select
            v-model:value="formData.bedId"
            placeholder="请选择床位"
            :options="bedOptions"
            :disabled="!formData.roomId"
          />
        </a-form-item>
        <a-form-item label="员工" name="employeeId">
          <a-select
            v-model:value="formData.employeeId"
            placeholder="请输入姓名或工号搜索员工"
            show-search
            :filter-option="false"
            :options="employeeOptions"
            :loading="employeeSearching"
            allow-clear
            @search="handleEmployeeSearch"
            @change="handleEmployeeChange"
          />
        </a-form-item>
        <a-form-item label="入住日期" name="checkInDate">
          <a-date-picker
            v-model:value="formData.checkInDate"
            placeholder="请选择入住日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="formData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 退宿确认弹窗 -->
    <a-modal
      v-model:open="checkOutVisible"
      title="退宿确认"
      width="480px"
      :destroy-on-close="true"
      @cancel="checkOutVisible = false"
    >
      <a-form
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="员工">
          <span>{{ currentRecord?.employeeName || currentRecord?.employeeId }}</span>
        </a-form-item>
        <a-form-item label="床位">
          <span>{{ currentRecord?.buildingName }} - {{ currentRecord?.roomNumber }} - {{ currentRecord?.bedNumber }}</span>
        </a-form-item>
        <a-form-item label="退宿日期" required>
          <a-date-picker
            v-model:value="checkOutForm.checkOutDate"
            placeholder="请选择退宿日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="checkOutForm.remark"
            :rows="2"
            placeholder="请输入备注（可选）"
            :maxlength="500"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="checkOutVisible = false">取消</a-button>
        <a-button type="primary" :loading="checkOutLoading" @click="confirmCheckOut">确认退宿</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, DeleteOutlined, LogoutOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getResidenceList,
  createResidence,
  deleteResidence,
  checkOut,
  getAllBuildings,
  getRoomList,
  getBedList,
  type ResidenceDto,
  type BuildingListItemDto,
  type RoomDto,
  type BedDto,
} from '@/api/dormitory'
import { getEmployeeList, type EmployeeDto } from '@/api/hr'

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '员工ID', dataIndex: 'employeeId', key: 'employeeId', width: 80 },
  { title: '员工姓名', dataIndex: 'employeeName', key: 'employeeName', width: 120 },
  { title: '床位信息', dataIndex: 'bedInfo', key: 'bedInfo', width: 200 },
  { title: '入住日期', dataIndex: 'checkInDate', key: 'checkInDate', width: 120 },
  { title: '退宿日期', dataIndex: 'checkOutDate', key: 'checkOutDate', width: 120 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  buildingId: undefined as number | undefined,
  status: undefined as number | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<ResidenceDto[]>([])
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
  fetchResidenceList()
}

// 楼栋选项
const buildingOptions = ref<{ label: string; value: number }[]>([])
const roomOptions = ref<{ label: string; value: number }[]>([])
const bedOptions = ref<{ label: string; value: number }[]>([])

// 员工搜索选项
const employeeOptions = ref<{ label: string; value: number; data: EmployeeDto }[]>([])
const employeeSearching = ref(false)
let employeeSearchTimer: ReturnType<typeof setTimeout> | null = null

// 弹窗相关
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)

const formData = reactive({
  employeeId: undefined as number | undefined,
  buildingId: undefined as number | undefined,
  roomId: undefined as number | undefined,
  bedId: undefined as number | undefined,
  checkInDate: '',
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  buildingId: [{ required: true, message: '请选择楼栋', trigger: 'change' }],
  roomId: [{ required: true, message: '请选择房间', trigger: 'change' }],
  bedId: [{ required: true, message: '请选择床位', trigger: 'change' }],
  employeeId: [{ required: true, message: '请选择员工', trigger: 'change' }],
  checkInDate: [{ required: true, message: '请选择入住日期', trigger: 'change' }],
}

// 退宿相关
const checkOutVisible = ref(false)
const checkOutLoading = ref(false)
const currentRecord = ref<ResidenceDto | null>(null)
const checkOutForm = reactive({
  checkOutDate: '',
  remark: '',
})

// 获取入住列表
async function fetchResidenceList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) {
      params.keyword = searchForm.keyword
    }
    if (searchForm.buildingId) params.buildingId = searchForm.buildingId
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getResidenceList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = (res as any).total || res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取楼栋列表
async function fetchBuildings() {
  try {
    const res = await getAllBuildings()
    buildingOptions.value = (res || []).map((b: BuildingListItemDto) => ({
      label: b.name,
      value: b.id,
    }))
  } catch (error) {
    console.error('获取楼栋列表失败:', error)
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchResidenceList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.buildingId = undefined
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchResidenceList()
}

// 重置表单
function resetForm() {
  formData.employeeId = undefined
  formData.buildingId = undefined
  formData.roomId = undefined
  formData.bedId = undefined
  formData.checkInDate = ''
  formData.remark = ''
  roomOptions.value = []
  bedOptions.value = []
  employeeOptions.value = []
}

// 新增
function handleAdd() {
  resetForm()
  dialogVisible.value = true
}

// 房间类型中文映射
const roomTypeMap: Record<string, string> = {
  standard: '标准间',
  single: '单人间',
  suite: '套间',
}

function getRoomTypeText(type: string): string {
  return roomTypeMap[type] || type
}

// 床位状态中文映射
const bedStatusMap: Record<number, string> = {
  1: '空闲',
  2: '已入住',
  3: '维修中',
}

function getBedStatusText(status: number): string {
  return bedStatusMap[status] || '未知'
}

// 楼栋变化
async function handleBuildingChange(buildingId: any) {
  formData.roomId = undefined
  formData.bedId = undefined
  roomOptions.value = []
  bedOptions.value = []
  if (!buildingId) return
  try {
    const res = await getRoomList(buildingId)
    roomOptions.value = (res || []).map((r: RoomDto) => ({
      label: `${r.roomNumber} (${getRoomTypeText(r.roomType || '')})`,
      value: r.id,
    }))
  } catch (error) {
    console.error('获取房间列表失败:', error)
  }
}

// 房间变化
async function handleRoomChange(roomId: any) {
  formData.bedId = undefined
  bedOptions.value = []
  if (!roomId) return
  try {
    const res = await getBedList(roomId)
    bedOptions.value = (res || []).map((b: BedDto) => ({
      label: `${b.bedNumber} (${getBedStatusText(b.status)})`,
      value: b.id,
    }))
  } catch (error) {
    console.error('获取床位列表失败:', error)
  }
}

// 员工搜索（带防抖）
function handleEmployeeSearch(value: string) {
  if (employeeSearchTimer) {
    clearTimeout(employeeSearchTimer)
  }
  if (!value || value.trim().length < 1) {
    employeeOptions.value = []
    return
  }
  employeeSearchTimer = setTimeout(async () => {
    employeeSearching.value = true
    try {
      const res = await getEmployeeList({
        keyword: value.trim(),
        pageIndex: 1,
        pageSize: 20,
        employeeStatus: 1, // 只搜索在职员工
      })
      if (res && res.items) {
        employeeOptions.value = res.items.map((emp: EmployeeDto) => ({
          label: `${emp.name} (${emp.fuid || emp.id})`,
          value: emp.id,
          data: emp,
        }))
      } else {
        employeeOptions.value = []
      }
    } catch (error) {
      console.error('搜索员工失败:', error)
      employeeOptions.value = []
    } finally {
      employeeSearching.value = false
    }
  }, 300)
}

// 员工选择变化
function handleEmployeeChange(employeeId: any) {
  if (!employeeId) {
    return
  }
  // 可以在这里做一些额外处理，比如显示选中员工的其他信息
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const data = {
      employeeId: formData.employeeId!,
      bedId: formData.bedId!,
      checkInDate: formData.checkInDate,
      remark: formData.remark || undefined,
    }
    await createResidence(data)
    message.success('新增入住成功')
    dialogVisible.value = false
    fetchResidenceList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deleteResidence(row.id)
    message.success('删除成功')
    fetchResidenceList()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

// 退宿
function handleCheckOut(row: any) {
  currentRecord.value = row
  checkOutForm.checkOutDate = new Date().toISOString().slice(0, 10)
  checkOutForm.remark = ''
  checkOutVisible.value = true
}

// 确认退宿
async function confirmCheckOut() {
  if (!currentRecord.value) return
  if (!checkOutForm.checkOutDate) {
    message.warning('请选择退宿日期')
    return
  }
  checkOutLoading.value = true
  try {
    await checkOut(currentRecord.value.id, {
      checkOutDate: checkOutForm.checkOutDate,
      remark: checkOutForm.remark || undefined,
    })
    message.success('退宿成功')
    checkOutVisible.value = false
    fetchResidenceList()
  } catch (error) {
    console.error('退宿失败:', error)
    message.error('退宿失败')
  } finally {
    checkOutLoading.value = false
  }
}

// 格式化日期
function formatDate(dateStr: string) {
  if (!dateStr) return '-'
  return dateStr.slice(0, 10)
}

onMounted(() => {
  fetchResidenceList()
  fetchBuildings()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
