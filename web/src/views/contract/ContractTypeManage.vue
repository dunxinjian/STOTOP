<template>
  <div class="page-container page-container--flush">
    <PageHeader title="合同类型管理" description="管理合同类型信息">
      <template #right>
        <a-button v-if="has(ContractPermissions.TypeManage)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增类型
        </a-button>
      </template>
    </PageHeader>

    <a-alert
      type="info"
      message="系统预置的合同类型（客户合同、劳动合同等）不可删除，但可以编辑和停用。"
      show-icon
      style="margin-bottom: 16px"
    />

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 900 }"
      row-key="id"
      empty-text="暂无合同类型数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '启用' : '停用' }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button v-if="has(ContractPermissions.TypeManage)" type="link" size="small" @click="handleEdit(record as ContractTypeDto)">
            <EditOutlined />编辑
          </a-button>
          <a-button v-if="has(ContractPermissions.TypeManage)" type="link" size="small" @click="handleToggleStatus(record as ContractTypeDto)">
            {{ record.status === 1 ? '停用' : '启用' }}
          </a-button>
        </template>
      </template>
    </DataTable>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增合同类型' : '编辑合同类型'"
      width="500px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入类型名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="编码" name="code">
          <a-input v-model:value="formData.code" placeholder="请输入类型编码" :maxlength="50" :disabled="dialogType === 'edit'" />
        </a-form-item>
        <a-form-item label="说明">
          <a-textarea v-model:value="formData.description" :rows="3" placeholder="请输入类型说明" :maxlength="500" show-count />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formData.sortOrder" :min="0" :max="9999" placeholder="排序号" style="width: 100%" />
        </a-form-item>
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
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import { usePermission, ContractPermissions } from '@/utils/permission'
import {
  getContractTypeList,
  createContractType,
  updateContractType,
  updateContractTypeStatus,
  type ContractTypeDto,
} from '@/api/contract'

const { has } = usePermission()

const tableColumns = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '编码', dataIndex: 'code', key: 'code', width: 120 },
  { title: '说明', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '排序', dataIndex: 'sortOrder', key: 'sortOrder', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 160, align: 'center' as const, fixed: 'right' as const },
]

const loading = ref(false)
const tableData = ref<ContractTypeDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  name: '',
  code: '',
  description: '',
  sortOrder: 0,
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入类型名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入类型编码', trigger: 'blur' }],
  sortOrder: [{ required: true, message: '请输入排序号', trigger: 'blur' }],
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getContractTypeList({
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.value.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function resetForm() {
  formData.name = ''
  formData.code = ''
  formData.description = ''
  formData.sortOrder = 0
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: ContractTypeDto) {
  dialogType.value = 'edit'
  currentId.value = row.id
  formData.name = row.name
  formData.code = row.code
  formData.description = row.description || ''
  formData.sortOrder = row.sortOrder
  dialogVisible.value = true
}

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
      name: formData.name,
      code: formData.code,
      description: formData.description || undefined,
      sortOrder: formData.sortOrder,
    }
    if (dialogType.value === 'add') {
      await createContractType(data)
      message.success('新增成功')
    } else {
      await updateContractType(currentId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handleToggleStatus(row: ContractTypeDto) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateContractTypeStatus(row.id, newStatus)
    message.success(`${actionText}成功`)
    fetchList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
