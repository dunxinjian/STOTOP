<template>
  <div class="page-container">
    <PageHeader title="保险公司" description="管理保险公司基础信息">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增公司
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="公司名称/编码" style="width: 200px" allowClear @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.companyType" size="small" placeholder="公司类型" style="width: 120px" allowClear :options="companyTypeOptions" />
          <a-select v-model:value="searchForm.status" size="small" placeholder="状态" style="width: 120px" allowClear :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="Id"
        bordered
        :scroll="{ x: 1000 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'CompanyType'">
            <a-tag :color="companyTypeColorMap[record.CompanyType] || 'default'">
              {{ companyTypeMap[record.CompanyType] || '未知' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'Status'">
            <a-tag :color="record.Status === 1 ? 'success' : 'default'">
              {{ record.Status === 1 ? '启用' : '禁用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as InsCompanyListItemDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button
              type="link"
              size="small"
              :danger="record.Status === 1"
              @click="handleToggleStatus(record as InsCompanyListItemDto)"
            >
              {{ record.Status === 1 ? '禁用' : '启用' }}
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无保险公司数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增保险公司' : '编辑保险公司'"
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
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="公司名称" name="CompanyName">
              <a-input v-model:value="formData.CompanyName" placeholder="请输入公司名称" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="公司编码" name="CompanyCode">
              <a-input v-model:value="formData.CompanyCode" placeholder="请输入公司编码" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="公司类型" name="CompanyType">
              <a-select v-model:value="formData.CompanyType" placeholder="请选择" :options="companyTypeEditOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系人">
              <a-input v-model:value="formData.ContactPerson" placeholder="请输入联系人" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="联系电话">
              <a-input v-model:value="formData.ContactPhone" placeholder="请输入联系电话" :maxlength="20" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="地址">
              <a-input v-model:value="formData.Address" placeholder="请输入地址" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea v-model:value="formData.Remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
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
import { PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getCompanyList,
  createCompany,
  updateCompany,
  updateCompanyStatus,
  type InsCompanyListItemDto,
} from '@/api/insurance'

const companyTypeMap: Record<number, string> = { 1: '财产险', 2: '寿险', 3: '综合' }
const companyTypeColorMap: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'purple' }
const companyTypeOptions = [
  { label: '财产险', value: 1 },
  { label: '寿险', value: 2 },
  { label: '综合', value: 3 },
]
const companyTypeEditOptions = [...companyTypeOptions]
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '禁用', value: 0 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '公司名称', dataIndex: 'CompanyName', width: 180 },
  { title: '公司编码', dataIndex: 'CompanyCode', width: 120 },
  { title: '公司类型', dataIndex: 'CompanyType', width: 100, align: 'center' as const },
  { title: '联系人', dataIndex: 'ContactPerson', width: 100 },
  { title: '联系电话', dataIndex: 'ContactPhone', width: 130 },
  { title: '状态', dataIndex: 'Status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({ keyword: '', companyType: undefined as number | undefined, status: undefined as number | undefined })
const loading = ref(false)
const tableData = ref<InsCompanyListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)
const formData = reactive({
  CompanyName: '',
  CompanyCode: '',
  CompanyType: 1,
  ContactPerson: '',
  ContactPhone: '',
  Address: '',
  Remark: '',
})
const formRules: Record<string, Rule[]> = {
  CompanyName: [{ required: true, message: '请输入公司名称', trigger: 'blur' }],
  CompanyCode: [{ required: true, message: '请输入公司编码', trigger: 'blur' }],
  CompanyType: [{ required: true, message: '请选择公司类型', trigger: 'change' }],
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.companyType != null) params.companyType = searchForm.companyType
    if (searchForm.status != null) params.status = searchForm.status
    const res = await getCompanyList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.total || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() {
  searchForm.keyword = ''
  searchForm.companyType = undefined
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchList()
}
function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function resetForm() {
  formData.CompanyName = ''
  formData.CompanyCode = ''
  formData.CompanyType = 1
  formData.ContactPerson = ''
  formData.ContactPhone = ''
  formData.Address = ''
  formData.Remark = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: InsCompanyListItemDto) {
  dialogType.value = 'edit'
  currentId.value = row.Id
  formData.CompanyName = row.CompanyName
  formData.CompanyCode = row.CompanyCode
  formData.CompanyType = row.CompanyType
  formData.ContactPerson = row.ContactPerson || ''
  formData.ContactPhone = row.ContactPhone || ''
  formData.Address = ''
  formData.Remark = ''
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const data = { ...formData }
    if (dialogType.value === 'add') {
      await createCompany(data)
      message.success('新增成功')
    } else {
      await updateCompany(currentId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch { message.error('提交失败') } finally { submitLoading.value = false }
}

async function handleToggleStatus(row: InsCompanyListItemDto) {
  try {
    await updateCompanyStatus(row.Id, row.Status === 1 ? 0 : 1)
    message.success('状态更新成功')
    fetchList()
  } catch { message.error('状态更新失败') }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
