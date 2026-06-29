<template>
  <div class="page-container page-container--flush">
    <PageHeader title="合同模板" description="管理合同模板信息与版本">
      <template #left>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="模板名称" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.typeId" size="middle" placeholder="合同类型" style="width: 160px" allow-clear :options="typeOptions" @change="handleSearch" />
        <a-select v-model:value="searchForm.status" size="middle" placeholder="状态" style="width: 120px" allow-clear :options="statusOptions" @change="handleSearch" />
        <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
      <template #right>
        <a-button v-if="has(ContractPermissions.TemplateManage)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增模板
        </a-button>
      </template>
    </PageHeader>

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1000 }"
      row-key="id"
      empty-text="暂无模板数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="templateStatusTagType(record.status)">{{ templateStatusText(record.status) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button v-if="has(ContractPermissions.TemplateManage)" type="link" size="small" @click="handleEdit(record as ContractTemplateListItemDto)">
            <EditOutlined />编辑
          </a-button>
          <a-popconfirm
            v-if="has(ContractPermissions.TemplateManage) && record.status !== 1"
            title="发布后旧版本将自动停用，确定发布吗？" ok-text="确定" cancel-text="取消" @confirm="handlePublish(record as ContractTemplateListItemDto)"
          >
            <a-button type="link" size="small"><CheckOutlined />发布</a-button>
          </a-popconfirm>
          <a-button
            v-if="has(ContractPermissions.TemplateManage) && record.status === 1"
            type="link" size="small" danger @click="handleDisable(record as ContractTemplateListItemDto)"
          >停用</a-button>
        </template>
      </template>
    </DataTable>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增模板' : '编辑模板'"
      width="900px"
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
            <a-form-item label="合同类型" name="typeId">
              <a-select
                v-model:value="formData.typeId"
                placeholder="请选择合同类型"
                :options="typeOptions"
                :disabled="dialogType === 'edit'"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="模板名称" name="templateName">
              <a-input v-model:value="formData.templateName" placeholder="请输入模板名称" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20" v-if="dialogType === 'edit'">
          <a-col :span="12">
            <a-form-item label="版本号">
              <a-input :value="`V${formData.version}`" disabled />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="模板内容">
          <a-textarea
            v-model:value="formData.templateContent"
            :rows="15"
            placeholder="请输入模板内容（支持文本格式，后续可升级为富文本编辑器）"
            :maxlength="50000"
            show-count
          />
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
import { PlusOutlined, EditOutlined, CheckOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import { usePermission, ContractPermissions } from '@/utils/permission'
import {
  getContractTemplateList,
  getContractTemplateById,
  createContractTemplate,
  updateContractTemplate,
  publishContractTemplate,
  getAllEnabledContractTypes,
  type ContractTemplateListItemDto,
} from '@/api/contract'

const { has } = usePermission()

const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '已发布', value: 1 },
  { label: '已停用', value: 2 },
]

function templateStatusText(status: number) {
  return ['草稿', '已发布', '已停用'][status] || '未知'
}

function templateStatusTagType(s: number): 'success' | 'default' {
  return (['default', 'success', 'default'] as const)[s] || 'default'
}

const tableColumns = [
  { title: '模板名称', dataIndex: 'templateName', key: 'templateName', width: 200, ellipsis: true },
  { title: '合同类型', dataIndex: 'typeName', key: 'typeName', width: 120 },
  { title: '版本号', dataIndex: 'version', key: 'version', width: 80, align: 'center' as const, customRender: ({ text }: any) => `V${text}` },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '更新时间', dataIndex: 'createdTime', key: 'createdTime', width: 180 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({
  keyword: '',
  typeId: undefined as number | undefined,
  status: undefined as number | undefined,
})

const loading = ref(false)
const tableData = ref<ContractTemplateListItemDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })
const typeOptions = ref<{ label: string; value: number }[]>([])

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  typeId: undefined as number | undefined,
  templateName: '',
  templateContent: '',
  version: 1,
})

const formRules: Record<string, Rule[]> = {
  typeId: [{ required: true, message: '请选择合同类型', trigger: 'change' }],
  templateName: [{ required: true, message: '请输入模板名称', trigger: 'blur' }],
}

async function fetchTypeOptions() {
  try {
    const res = await getAllEnabledContractTypes() as any
    const list = res?.items || res || []
    typeOptions.value = list.map((t: any) => ({ label: t.name, value: t.id }))
  } catch (error) {
    console.error('获取合同类型失败:', error)
  }
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.typeId !== undefined) params.typeId = searchForm.typeId
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getContractTemplateList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.value.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.value.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.typeId = undefined
  searchForm.status = undefined
  pagination.value.pageIndex = 1
  fetchList()
}

function resetForm() {
  formData.typeId = undefined
  formData.templateName = ''
  formData.templateContent = ''
  formData.version = 1
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: ContractTemplateListItemDto) {
  dialogType.value = 'edit'
  currentId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getContractTemplateById(row.id) as any
    if (detail) {
      formData.typeId = detail.typeId
      formData.templateName = detail.templateName
      formData.templateContent = detail.templateContent || ''
      formData.version = detail.version
    }
  } catch (error) {
    console.error('获取模板详情失败:', error)
  }
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
    if (dialogType.value === 'add') {
      await createContractTemplate({
        typeId: formData.typeId!,
        templateName: formData.templateName,
        templateContent: formData.templateContent || undefined,
      })
      message.success('新增成功')
    } else {
      await updateContractTemplate(currentId.value!, {
        templateName: formData.templateName,
        templateContent: formData.templateContent || undefined,
      })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handlePublish(row: ContractTemplateListItemDto) {
  try {
    await publishContractTemplate(row.id)
    message.success('发布成功')
    fetchList()
  } catch (error) {
    console.error('发布失败:', error)
  }
}

async function handleDisable(row: ContractTemplateListItemDto) {
  try {
    await updateContractTemplate(row.id, {
      templateName: row.templateName,
    })
    message.success('已停用')
    fetchList()
  } catch (error) {
    console.error('停用失败:', error)
  }
}

onMounted(() => {
  fetchTypeOptions()
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
