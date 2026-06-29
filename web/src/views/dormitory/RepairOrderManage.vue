<template>
  <div class="page-container">
    <PageHeader title="报修工单管理" description="管理宿舍报修工单">
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增报修
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-form-item label="房间" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.roomId"
                placeholder="全部房间"
                allow-clear
                show-search
                :filter-option="filterOption"
                style="width: 200px"
                :options="roomOptions"
              />
            </a-form-item>
            <a-form-item label="状态" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.status"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="statusOptions"
              />
            </a-form-item>
            <a-form-item label="紧急程度" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.priority"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="priorityOptions"
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
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'roomInfo'">
            <span>{{ record.buildingName }} - {{ record.roomNumber }}</span>
          </template>
          <template v-if="column.dataIndex === 'description'">
            <a-tooltip :title="record.description">
              <span class="ellipsis-text">{{ record.description }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'priority'">
            <a-tag :color="getPriorityColor(record.priority)">
              {{ getPriorityText(record.priority) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button
              type="link"
              size="small"
              :disabled="record.status === 3 || record.status === 4"
              @click="handleEdit(record)"
            >
              <EditOutlined />编辑
            </a-button>
            <a-button
              type="link"
              size="small"
              :disabled="record.status === 3 || record.status === 4"
              @click="handleProcess(record)"
            >
              处理
            </a-button>
            <a-popconfirm
              title="确定删除该报修工单吗？"
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
          <EmptyState description="暂无报修工单数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑报修弹窗 -->
    <a-modal
      v-model:open="formDialogVisible"
      :title="dialogMode === 'add' ? '新增报修工单' : '编辑报修工单'"
      width="600px"
      :destroy-on-close="true"
      @cancel="formDialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="房间" name="roomId">
          <a-select
            v-model:value="formData.roomId"
            placeholder="请选择房间"
            show-search
            :filter-option="filterOption"
            :options="roomOptions"
            :disabled="dialogMode === 'edit'"
          />
        </a-form-item>
        <a-form-item label="报修人" name="reporterId">
          <DormEmployeeSelect
            v-model="formData.reporterId"
            :initial-label="formData.reporterName"
            :disabled="dialogMode === 'edit'"
            placeholder="搜索报修人姓名/工号"
          />
        </a-form-item>
        <a-form-item label="问题描述" name="description">
          <a-textarea
            v-model:value="formData.description"
            :rows="4"
            placeholder="请输入问题描述"
            :maxlength="500"
            show-count
          />
        </a-form-item>
        <a-form-item label="紧急程度" name="priority">
          <a-select
            v-model:value="formData.priority"
            placeholder="请选择紧急程度"
            :options="priorityOptions"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="formDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmitForm">确定</a-button>
      </template>
    </a-modal>

    <!-- 处理工单弹窗 -->
    <a-modal
      v-model:open="processDialogVisible"
      title="处理报修工单"
      width="600px"
      :destroy-on-close="true"
      @cancel="processDialogVisible = false"
    >
      <a-form
        ref="processFormRef"
        :model="processFormData"
        :rules="processFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="处理人" name="handlerId">
          <DormEmployeeSelect v-model="processFormData.handlerId" placeholder="搜索处理人姓名/工号" />
        </a-form-item>
        <a-form-item label="处理结果" name="result">
          <a-textarea
            v-model:value="processFormData.result"
            :rows="4"
            placeholder="请输入处理结果"
            :maxlength="500"
            show-count
          />
        </a-form-item>
        <a-form-item label="新状态" name="status">
          <a-select
            v-model:value="processFormData.status"
            placeholder="请选择新状态"
            :options="processStatusOptions"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="processDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmitProcess">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import DormEmployeeSelect from '@/components/DormEmployeeSelect.vue'
import {
  getRepairOrderList,
  createRepairOrder,
  updateRepairOrder,
  handleRepairOrder,
  deleteRepairOrder,
  getAllBuildings,
  getRoomList,
  type RepairOrderDto,
  type CreateRepairOrderRequest,
  type UpdateRepairOrderRequest,
  type HandleRepairOrderRequest,
} from '@/api/dormitory'

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '房间信息', dataIndex: 'roomInfo', key: 'roomInfo', width: 180 },
  { title: '报修人', dataIndex: 'reporterName', key: 'reporterName', width: 100 },
  { title: '问题描述', dataIndex: 'description', key: 'description', width: 200, ellipsis: true },
  { title: '紧急程度', dataIndex: 'priority', key: 'priority', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '处理人', dataIndex: 'handlerName', key: 'handlerName', width: 100 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

// 状态选项
const statusOptions = [
  { label: '待处理', value: 1 },
  { label: '处理中', value: 2 },
  { label: '已完成', value: 3 },
  { label: '已关闭', value: 4 },
]

// 紧急程度选项
const priorityOptions = [
  { label: '一般', value: 1 },
  { label: '紧急', value: 2 },
  { label: '非常紧急', value: 3 },
]

// 处理状态选项
const processStatusOptions = [
  { label: '处理中', value: 2 },
  { label: '已完成', value: 3 },
  { label: '已关闭', value: 4 },
]

// 房间选项
const roomOptions = ref<{ label: string; value: number }[]>([])

// 搜索表单
const searchForm = reactive({
  roomId: undefined as number | undefined,
  status: undefined as number | undefined,
  priority: undefined as number | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<RepairOrderDto[]>([])
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
  fetchRepairOrderList()
}

// 新增/编辑弹窗
const formDialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentOrderId = ref<number | null>(null)

const formData = reactive({
  roomId: undefined as number | undefined,
  reporterId: undefined as number | undefined,
  reporterName: '',
  description: '',
  priority: undefined as number | undefined,
})

const formRules: Record<string, Rule[]> = {
  roomId: [{ required: true, message: '请选择房间', trigger: 'change' }],
  reporterId: [{ required: true, message: '请输入报修人ID', trigger: 'blur' }],
  description: [{ required: true, message: '请输入问题描述', trigger: 'blur' }],
  priority: [{ required: true, message: '请选择紧急程度', trigger: 'change' }],
}

// 处理弹窗
const processDialogVisible = ref(false)
const processFormRef = ref<FormInstance>()

const processFormData = reactive({
  handlerId: undefined as number | undefined,
  result: '',
  status: undefined as number | undefined,
})

const processFormRules: Record<string, Rule[]> = {
  handlerId: [{ required: true, message: '请输入处理人ID', trigger: 'blur' }],
  result: [{ required: true, message: '请输入处理结果', trigger: 'blur' }],
  status: [{ required: true, message: '请选择新状态', trigger: 'change' }],
}

// 紧急程度颜色
function getPriorityColor(priority: number): string {
  const colors: Record<number, string> = { 1: 'blue', 2: 'orange', 3: 'red' }
  return colors[priority] || 'default'
}

// 紧急程度文本
function getPriorityText(priority: number): string {
  const texts: Record<number, string> = { 1: '一般', 2: '紧急', 3: '非常紧急' }
  return texts[priority] || '未知'
}

// 状态颜色
function getStatusColor(status: number): string {
  const colors: Record<number, string> = { 1: 'default', 2: 'processing', 3: 'success', 4: 'default' }
  return colors[status] || 'default'
}

// 状态文本
function getStatusText(status: number): string {
  const texts: Record<number, string> = { 1: '待处理', 2: '处理中', 3: '已完成', 4: '已关闭' }
  return texts[status] || '未知'
}

// 筛选选项
function filterOption(input: string, option: any) {
  return option.label.toLowerCase().indexOf(input.toLowerCase()) >= 0
}

// 加载房间选项
async function loadRoomOptions() {
  try {
    const buildings = await getAllBuildings()
    const options: { label: string; value: number }[] = []
    for (const building of buildings) {
      const rooms = await getRoomList(building.id)
      for (const room of rooms) {
        options.push({ label: `${building.name} - ${room.roomNumber}`, value: room.id })
      }
    }
    roomOptions.value = options
  } catch (error) {
    console.error('加载房间选项失败:', error)
  }
}

// 获取报修工单列表
async function fetchRepairOrderList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.roomId) params.roomId = searchForm.roomId
    if (searchForm.status !== undefined) params.status = searchForm.status
    if (searchForm.priority !== undefined) params.priority = searchForm.priority
    const res = await getRepairOrderList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchRepairOrderList()
}

// 重置搜索
function handleReset() {
  searchForm.roomId = undefined
  searchForm.status = undefined
  searchForm.priority = undefined
  pagination.pageIndex = 1
  fetchRepairOrderList()
}

// 重置表单
function resetForm() {
  formData.roomId = undefined
  formData.reporterId = undefined
  formData.reporterName = ''
  formData.description = ''
  formData.priority = undefined
  currentOrderId.value = null
}

// 新增
function handleAdd() {
  dialogMode.value = 'add'
  resetForm()
  formDialogVisible.value = true
}

// 编辑
function handleEdit(record: any) {
  dialogMode.value = 'edit'
  currentOrderId.value = record.id
  formData.roomId = record.roomId
  formData.reporterId = record.reporterId
  formData.reporterName = record.reporterName || ''
  formData.description = record.description
  formData.priority = record.priority
  formDialogVisible.value = true
}

// 提交新增/编辑
async function handleSubmitForm() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    if (dialogMode.value === 'add') {
      const data: CreateRepairOrderRequest = {
        roomId: formData.roomId!,
        reporterId: formData.reporterId!,
        description: formData.description,
        priority: formData.priority!,
      }
      await createRepairOrder(data)
      message.success('新增成功')
    } else {
      const data: UpdateRepairOrderRequest = {
        description: formData.description,
        priority: formData.priority!,
      }
      await updateRepairOrder(currentOrderId.value!, data)
      message.success('更新成功')
    }
    formDialogVisible.value = false
    fetchRepairOrderList()
  } finally {
    submitLoading.value = false
  }
}

// 处理工单
function handleProcess(record: any) {
  currentOrderId.value = record.id
  processFormData.handlerId = record.handlerId
  processFormData.result = record.result || ''
  processFormData.status = record.status === 1 ? 2 : record.status
  processDialogVisible.value = true
}

// 提交处理
async function handleSubmitProcess() {
  if (!processFormRef.value) return
  try {
    await processFormRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const data: HandleRepairOrderRequest = {
      handlerId: processFormData.handlerId!,
      result: processFormData.result,
      status: processFormData.status!,
    }
    await handleRepairOrder(currentOrderId.value!, data)
    message.success('处理成功')
    processDialogVisible.value = false
    fetchRepairOrderList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(record: any) {
  try {
    await deleteRepairOrder(record.id)
    message.success('删除成功')
    fetchRepairOrderList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

onMounted(() => {
  loadRoomOptions()
  fetchRepairOrderList()
})
</script>

<style scoped lang="scss">
.ellipsis-text {
  display: inline-block;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
