<template>
  <div class="page-container">
    <PageHeader title="访客登记" description="管理宿舍访客登记信息">
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增访客
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
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'roomInfo'">
            <span>{{ record.buildingName }} - {{ record.roomNumber }}</span>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'green' : 'default'">
              {{ record.status === 1 ? '来访中' : '已离开' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-popconfirm
              v-if="record.status === 1"
              title="确定为该访客登记离开吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleLeave(record)"
            >
              <a-button type="link" size="small">登记离开</a-button>
            </a-popconfirm>
            <a-button v-else type="link" size="small" disabled>登记离开</a-button>
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该访客记录吗？"
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
          <EmptyState description="暂无访客登记数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑访客弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增访客登记' : '编辑访客登记'"
      width="700px"
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
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="房间" name="roomId">
              <a-select
                v-model:value="formData.roomId"
                placeholder="请选择房间"
                show-search
                :filter-option="filterOption"
                :options="roomOptions"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="访客姓名" name="visitorName">
              <a-input v-model:value="formData.visitorName" placeholder="请输入访客姓名" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="电话">
              <a-input v-model:value="formData.visitorPhone" placeholder="请输入电话" :maxlength="20" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="身份证">
              <a-input v-model:value="formData.visitorIdCard" placeholder="请输入身份证号" :maxlength="18" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="来访事由">
              <a-input v-model:value="formData.visitReason" placeholder="请输入来访事由" :maxlength="200" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="被访人">
              <DormEmployeeSelect
                v-model="formData.visitedPersonId"
                :initial-label="formData.visitedPersonName"
                placeholder="搜索被访人姓名/工号"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="到访时间" name="arrivalTime">
              <a-date-picker
                v-model:value="formData.arrivalTimeValue"
                show-time
                format="YYYY-MM-DD HH:mm:ss"
                placeholder="请选择到访时间"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea
                v-model:value="formData.remark"
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
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
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
import dayjs from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import DormEmployeeSelect from '@/components/DormEmployeeSelect.vue'
import {
  getVisitorList,
  createVisitor,
  updateVisitor,
  deleteVisitor,
  visitorLeave,
  getAllBuildings,
  getRoomList,
  type VisitorDto,
  type CreateVisitorRequest,
  type UpdateVisitorRequest,
} from '@/api/dormitory'

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '访客姓名', dataIndex: 'visitorName', key: 'visitorName', width: 100 },
  { title: '电话', dataIndex: 'visitorPhone', key: 'visitorPhone', width: 130 },
  { title: '身份证', dataIndex: 'visitorIdCard', key: 'visitorIdCard', width: 180 },
  { title: '来访房间', dataIndex: 'roomInfo', key: 'roomInfo', width: 160 },
  { title: '来访事由', dataIndex: 'visitReason', key: 'visitReason', width: 150, ellipsis: true },
  { title: '被访人', dataIndex: 'visitedPersonName', key: 'visitedPersonName', width: 100 },
  { title: '到访时间', dataIndex: 'arrivalTime', key: 'arrivalTime', width: 160 },
  { title: '离开时间', dataIndex: 'departureTime', key: 'departureTime', width: 160 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

// 状态选项
const statusOptions = [
  { label: '来访中', value: 1 },
  { label: '已离开', value: 2 },
]

// 房间选项
const roomOptions = ref<{ label: string; value: number }[]>([])

// 搜索表单
const searchForm = reactive({
  roomId: undefined as number | undefined,
  status: undefined as number | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<VisitorDto[]>([])
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
  fetchVisitorList()
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentVisitorId = ref<number | null>(null)

const formData = reactive({
  roomId: undefined as number | undefined,
  visitorName: '',
  visitorPhone: '',
  visitorIdCard: '',
  visitReason: '',
  visitedPersonId: undefined as number | undefined,
  visitedPersonName: '',
  arrivalTimeValue: null as any,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  roomId: [{ required: true, message: '请选择房间', trigger: 'change' }],
  visitorName: [{ required: true, message: '请输入访客姓名', trigger: 'blur' }],
  arrivalTime: [{ required: true, message: '请选择到访时间', trigger: 'change' }],
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
        options.push({
          label: `${building.name} - ${room.roomNumber}`,
          value: room.id,
        })
      }
    }
    roomOptions.value = options
  } catch (error) {
    console.error('加载房间选项失败:', error)
  }
}

// 获取访客列表
async function fetchVisitorList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.roomId) params.roomId = searchForm.roomId
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getVisitorList(params)
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
  fetchVisitorList()
}

// 重置搜索
function handleReset() {
  searchForm.roomId = undefined
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchVisitorList()
}

// 重置表单
function resetForm() {
  formData.roomId = undefined
  formData.visitorName = ''
  formData.visitorPhone = ''
  formData.visitorIdCard = ''
  formData.visitReason = ''
  formData.visitedPersonId = undefined
  formData.visitedPersonName = ''
  formData.arrivalTimeValue = dayjs()
  formData.remark = ''
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  currentVisitorId.value = null
  resetForm()
  dialogVisible.value = true
}

// 编辑
function handleEdit(record: any) {
  dialogType.value = 'edit'
  currentVisitorId.value = record.id
  resetForm()

  formData.roomId = record.roomId
  formData.visitorName = record.visitorName
  formData.visitorPhone = record.visitorPhone || ''
  formData.visitorIdCard = record.visitorIdCard || ''
  formData.visitReason = record.visitReason || ''
  formData.visitedPersonId = record.visitedPersonId
  formData.visitedPersonName = record.visitedPersonName || ''
  formData.arrivalTimeValue = record.arrivalTime ? dayjs(record.arrivalTime) : dayjs()
  formData.remark = record.remark || ''

  dialogVisible.value = true
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
    if (dialogType.value === 'add') {
      const data: CreateVisitorRequest = {
        roomId: formData.roomId!,
        visitorName: formData.visitorName,
        visitorPhone: formData.visitorPhone || undefined,
        visitorIdCard: formData.visitorIdCard || undefined,
        visitReason: formData.visitReason || undefined,
        visitedPersonId: formData.visitedPersonId,
        arrivalTime: formData.arrivalTimeValue
          ? formData.arrivalTimeValue.format('YYYY-MM-DD HH:mm:ss')
          : dayjs().format('YYYY-MM-DD HH:mm:ss'),
        remark: formData.remark || undefined,
      }
      await createVisitor(data)
      message.success('新增成功')
    } else {
      const data: UpdateVisitorRequest = {
        visitorName: formData.visitorName,
        visitorPhone: formData.visitorPhone || undefined,
        visitorIdCard: formData.visitorIdCard || undefined,
        visitReason: formData.visitReason || undefined,
        visitedPersonId: formData.visitedPersonId,
        remark: formData.remark || undefined,
      }
      await updateVisitor(currentVisitorId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchVisitorList()
  } finally {
    submitLoading.value = false
  }
}

// 登记离开
async function handleLeave(record: any) {
  try {
    await visitorLeave(record.id)
    message.success('登记离开成功')
    fetchVisitorList()
  } catch (error) {
    console.error('登记离开失败:', error)
  }
}

// 删除
async function handleDelete(record: any) {
  try {
    await deleteVisitor(record.id)
    message.success('删除成功')
    fetchVisitorList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

onMounted(() => {
  loadRoomOptions()
  fetchVisitorList()
})
</script>

<style scoped lang="scss">
</style>
