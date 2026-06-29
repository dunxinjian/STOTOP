<template>
  <div class="page-container page-container--flush">
    <PageHeader title="楼栋管理" description="管理宿舍楼栋信息">
      <template #left>
        <a-input-search
          v-model:value="searchForm.keyword"
          placeholder="编码/名称"
          allow-clear
          size="middle"
          style="width: 240px"
          @search="handleSearch"
        />
        <a-select
          v-model:value="searchForm.status"
          placeholder="状态"
          allow-clear
          size="middle"
          style="width: 110px"
          :options="[
            { label: '启用', value: 1 },
            { label: '停用', value: 0 },
          ]"
          @change="handleSearch"
        />
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
      <template #right>
        <a-button type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增楼栋
        </a-button>
      </template>
    </PageHeader>

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1100 }"
      row-key="id"
      empty-text="暂无楼栋数据"
      @change="fetchBuildingList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'name'">
          <a-tooltip :title="record.name">{{ record.name }}</a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'address'">
          <a-tooltip :title="record.address">{{ record.address }}</a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="record.status === 1 ? 'success' : 'danger'" dot>
            {{ record.status === 1 ? '启用' : '停用' }}
          </StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" @click="handleEdit(record)">
            <EditOutlined />编辑
          </a-button>
          <a-button type="link" @click="handleManageRooms(record)">
            <HomeOutlined />管理房间
          </a-button>
          <a-button
            type="link"
            size="small"
            :class="record.status === 1 ? 'btn-disable' : 'btn-enable'"
            @click="handleToggleStatus(record)"
          >
            {{ record.status === 1 ? '停用' : '启用' }}
          </a-button>
          <a-popconfirm
            title="确定删除该楼栋吗？"
            ok-text="确定"
            cancel-text="取消"
            @confirm="handleDelete(record)"
          >
            <a-button type="link" danger>
              <DeleteOutlined />删除
            </a-button>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增楼栋' : '编辑楼栋'"
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
            <a-form-item label="楼栋编码" name="code">
              <a-input
                v-model:value="formData.code"
                placeholder="请输入楼栋编码"
                :maxlength="50"
                :disabled="dialogType === 'edit'"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="楼栋名称" name="name">
              <a-input
                v-model:value="formData.name"
                placeholder="请输入楼栋名称"
                :maxlength="100"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="总楼层" name="totalFloors">
              <a-input-number
                v-model:value="formData.totalFloors"
                :min="1"
                :max="100"
                style="width: 100%"
                placeholder="请输入总楼层"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="宿舍类型" name="dormitoryType">
              <a-select
                v-model:value="formData.dormitoryType"
                placeholder="请选择宿舍类型"
                :options="[
                  { label: '男生宿舍', value: 'male' },
                  { label: '女生宿舍', value: 'female' },
                  { label: '混合宿舍', value: 'mixed' },
                ]"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="管理员">
              <DormEmployeeSelect
                v-model="formData.managerId"
                :initial-label="formData.managerName"
                placeholder="搜索管理员姓名/工号"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="地址">
              <a-input
                v-model:value="formData.address"
                placeholder="请输入地址"
                :maxlength="300"
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
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, HomeOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DormEmployeeSelect from '@/components/DormEmployeeSelect.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getBuildingList,
  getBuildingDetail,
  createBuilding,
  updateBuilding,
  deleteBuilding,
  updateBuildingStatus,
  checkBuildingCode,
  type BuildingListItemDto,
} from '@/api/dormitory'

const router = useRouter()

// 表格列配置
const tableColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 100 },
  { title: '名称', dataIndex: 'name', key: 'name', width: 150, ellipsis: true },
  { title: '地址', dataIndex: 'address', key: 'address', width: 200, ellipsis: true },
  { title: '总楼层', dataIndex: 'totalFloors', key: 'totalFloors', width: 80, align: 'center' as const },
  { title: '房间数', dataIndex: 'roomCount', key: 'roomCount', width: 80, align: 'center' as const },
  { title: '床位数', dataIndex: 'bedCount', key: 'bedCount', width: 80, align: 'center' as const },
  { title: '已入住', dataIndex: 'occupiedBeds', key: 'occupiedBeds', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 280, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  status: '' as number | string,
})

// 表格数据
const loading = ref(false)
const tableData = ref<BuildingListItemDto[]>([])
const pagination = ref({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentBuildingId = ref<number | null>(null)

const formData = reactive({
  code: '',
  name: '',
  address: '',
  totalFloors: 1,
  dormitoryType: 'male' as 'male' | 'female' | 'mixed',
  managerId: undefined as number | undefined,
  managerName: '',
  remark: '',
})

// 编码唯一性校验
const validateCode = async (_rule: any, value: string) => {
  if (!value) {
    return Promise.resolve()
  }
  if (dialogType.value === 'edit' && currentBuildingId.value) {
    const exists = await checkBuildingCode(value, currentBuildingId.value)
    if (exists) {
      return Promise.reject(new Error('楼栋编码已存在'))
    }
  } else {
    const exists = await checkBuildingCode(value)
    if (exists) {
      return Promise.reject(new Error('楼栋编码已存在'))
    }
  }
  return Promise.resolve()
}

const formRules: Record<string, Rule[]> = {
  code: [
    { required: true, message: '请输入楼栋编码', trigger: 'blur' },
    { validator: validateCode, trigger: 'blur' },
  ],
  name: [{ required: true, message: '请输入楼栋名称', trigger: 'blur' }],
  totalFloors: [{ required: true, message: '请输入总楼层', trigger: 'blur' }],
  dormitoryType: [{ required: true, message: '请选择宿舍类型', trigger: 'change' }],
}

// 获取楼栋列表
async function fetchBuildingList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.status !== '' && searchForm.status !== undefined) params.status = searchForm.status
    const res = await getBuildingList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.value.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  pagination.value.pageIndex = 1
  fetchBuildingList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.status = ''
  pagination.value.pageIndex = 1
  fetchBuildingList()
}

// 重置表单
function resetForm() {
  formData.code = ''
  formData.name = ''
  formData.address = ''
  formData.totalFloors = 1
  formData.dormitoryType = 'male'
  formData.managerId = undefined
  formData.managerName = ''
  formData.remark = ''
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  currentBuildingId.value = null
  resetForm()
  dialogVisible.value = true
}

// 编辑
async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentBuildingId.value = row.id
  resetForm()
  dialogVisible.value = true

  try {
    const detail = await getBuildingDetail(row.id)
    if (detail) {
      formData.code = detail.code
      formData.name = detail.name
      formData.address = detail.address || ''
      formData.totalFloors = detail.totalFloors
      formData.dormitoryType = (detail.dormitoryType as 'male' | 'female' | 'mixed') || 'male'
      formData.managerId = detail.managerId
      formData.managerName = detail.managerName || ''
      formData.remark = detail.remark || ''
    }
  } catch (error) {
    console.error('获取楼栋详情失败:', error)
    message.error('获取楼栋详情失败')
  }
}

// 管理房间
function handleManageRooms(record: any) {
  router.push({ name: 'RoomManage', params: { buildingId: record.id.toString() } })
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
      code: formData.code,
      name: formData.name,
      address: formData.address || undefined,
      totalFloors: formData.totalFloors,
      managerId: formData.managerId,
      dormitoryType: formData.dormitoryType,
      remark: formData.remark || undefined,
    }

    if (dialogType.value === 'add') {
      await createBuilding(data)
      message.success('新增成功')
    } else {
      await updateBuilding(currentBuildingId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchBuildingList()
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deleteBuilding(row.id)
    message.success('删除成功')
    fetchBuildingList()
  } catch (error) {
    console.error('删除失败:', error)
    message.error('删除失败')
  }
}

// 启用/停用
async function handleToggleStatus(row: any) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateBuildingStatus(row.id, newStatus)
    message.success(`${actionText}成功`)
    fetchBuildingList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
    message.error(`${actionText}失败`)
  }
}

onMounted(() => {
  fetchBuildingList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
