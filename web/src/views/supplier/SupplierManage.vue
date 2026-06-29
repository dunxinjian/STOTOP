<template>
  <div class="page-container">
    <PageHeader title="供应商管理" description="管理供应商信息与收款账户">
      <template #actions>
        <a-input-search v-model:value="searchForm.keyword" placeholder="编码/全称/简称/联系人" style="width: 280px" @search="handleSearch" allowClear />
        <a-select v-model:value="searchForm.status" placeholder="状态" allow-clear style="width: 100px" :options="[{ label: '启用', value: 1 }, { label: '停用', value: 0 }]" @change="handleSearch" />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增供应商
        </a-button>
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
          <template v-if="column.dataIndex === 'fullName'">
            <a-tooltip :title="record.fullName">{{ record.fullName }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'shortName'">
            <a-tooltip :title="record.shortName">{{ record.shortName }}</a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-button
              type="link"
              size="small"
              @click="handleToggleStatus(record)"
            >
              {{ record.status === 1 ? '停用' : '启用' }}
            </a-button>
            <a-popconfirm
              title="确定删除该供应商吗？"
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
          <EmptyState description="暂无供应商数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增供应商' : '编辑供应商'"
      width="860px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '130px' } }"
        style="padding: 10px 20px"
      >
        <!-- 基本信息 -->
        <div class="section-title">基本信息</div>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="供应商编码" name="code">
              <a-input
                v-model:value="formData.code"
                :placeholder="dialogType === 'add' ? '保存后自动生成' : ''"
                :maxlength="50"
                disabled
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="供应商全称" name="fullName">
              <a-input v-model:value="formData.fullName" placeholder="请输入供应商全称" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="简称">
              <a-input v-model:value="formData.shortName" placeholder="请输入简称" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="统一社会信用代码">
              <a-input v-model:value="formData.creditCode" placeholder="请输入统一社会信用代码" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="纳税人识别号">
              <a-input v-model:value="formData.taxNumber" placeholder="请输入纳税人识别号" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系人">
              <a-input v-model:value="formData.contact" placeholder="请输入联系人" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="电话">
              <a-input v-model:value="formData.phone" placeholder="请输入电话" :maxlength="30" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="邮箱">
              <a-input v-model:value="formData.email" placeholder="请输入邮箱" :maxlength="100" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="地址">
              <a-input v-model:value="formData.address" placeholder="请输入地址" :maxlength="300" />
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

        <!-- 收款账户 -->
        <div class="section-title">
          收款账户
          <a-button type="link" @click="handleAddBankAccount">
            <PlusOutlined />添加收款账户
          </a-button>
        </div>
        <a-table
          v-if="formData.bankAccounts.length > 0"
          :columns="bankColumns"
          :data-source="formData.bankAccounts"
          :pagination="false"
          bordered
          size="small"
          style="width: 100%; margin-bottom: 12px"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.dataIndex === 'accountName'">
              <a-input
                v-model:value="record.accountName"
                placeholder="请输入"
                size="small"
                :class="{ 'is-error-input': bankErrors[index]?.accountName }"
              />
            </template>
            <template v-if="column.dataIndex === 'bankName'">
              <a-input
                v-model:value="record.bankName"
                placeholder="请输入"
                size="small"
                :class="{ 'is-error-input': bankErrors[index]?.bankName }"
              />
            </template>
            <template v-if="column.dataIndex === 'bankAccountNumber'">
              <a-input
                v-model:value="record.bankAccountNumber"
                placeholder="请输入"
                size="small"
                :class="{ 'is-error-input': bankErrors[index]?.bankAccountNumber }"
              />
            </template>
            <template v-if="column.dataIndex === 'branchName'">
              <a-input v-model:value="record.branchName" placeholder="请输入" size="small" />
            </template>
            <template v-if="column.dataIndex === 'isDefault'">
              <a-switch
                :checked="record.isDefault"
                @change="(checked: any) => { record.isDefault = checked; handleDefaultChange(index) }"
              />
            </template>
            <template v-if="column.dataIndex === 'bankAction'">
              <a-button type="link" size="small" danger @click="handleRemoveBankAccount(index)">
                <DeleteOutlined />
              </a-button>
            </template>
          </template>
        </a-table>
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
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getSupplierList,
  getSupplierDetail,
  createSupplier,
  updateSupplier,
  deleteSupplier,
  updateSupplierStatus,
  type SupplierListItemDto,
  type CreateBankAccountRequest,
} from '@/api/supplier'

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '编码', dataIndex: 'code', key: 'code', width: 120 },
  { title: '全称', dataIndex: 'fullName', key: 'fullName', width: 180, ellipsis: true },
  { title: '简称', dataIndex: 'shortName', key: 'shortName', width: 120, ellipsis: true },
  { title: '联系人', dataIndex: 'contact', key: 'contact', width: 100 },
  { title: '电话', dataIndex: 'phone', key: 'phone', width: 140 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

const bankColumns = [
  { title: '账户名称', dataIndex: 'accountName', key: 'accountName' },
  { title: '银行名称', dataIndex: 'bankName', key: 'bankName' },
  { title: '银行账号', dataIndex: 'bankAccountNumber', key: 'bankAccountNumber' },
  { title: '开户行', dataIndex: 'branchName', key: 'branchName' },
  { title: '是否默认', dataIndex: 'isDefault', key: 'isDefault', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'bankAction', key: 'bankAction', width: 70, align: 'center' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
  status: '' as number | string,
})

// 表格数据
const loading = ref(false)
const tableData = ref<SupplierListItemDto[]>([])
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
  fetchSupplierList()
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentSupplierId = ref<number | null>(null)

interface BankAccountRow extends CreateBankAccountRequest {
  id?: number
  isDefault: boolean
}

const formData = reactive({
  code: '',
  fullName: '',
  shortName: '',
  creditCode: '',
  taxNumber: '',
  contact: '',
  phone: '',
  email: '',
  address: '',
  remark: '',
  bankAccounts: [] as BankAccountRow[],
})

const bankErrors = ref<Record<number, Record<string, boolean>>>({})

const formRules: Record<string, Rule[]> = {
  fullName: [{ required: true, message: '请输入供应商全称', trigger: 'blur' }],
}

// 获取供应商列表
async function fetchSupplierList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.status !== '' && searchForm.status !== undefined) params.status = searchForm.status
    const res = await getSupplierList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchSupplierList()
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
  searchForm.status = ''
  pagination.pageIndex = 1
  fetchSupplierList()
}

// 重置表单
function resetForm() {
  formData.code = ''
  formData.fullName = ''
  formData.shortName = ''
  formData.creditCode = ''
  formData.taxNumber = ''
  formData.contact = ''
  formData.phone = ''
  formData.email = ''
  formData.address = ''
  formData.remark = ''
  formData.bankAccounts = []
  bankErrors.value = {}
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  currentSupplierId.value = null
  resetForm()
  dialogVisible.value = true
}

// 编辑
async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentSupplierId.value = row.id
  resetForm()
  dialogVisible.value = true

  try {
    const detail = await getSupplierDetail(row.id)
    if (detail) {
      formData.code = detail.code
      formData.fullName = detail.fullName
      formData.shortName = detail.shortName || ''
      formData.creditCode = detail.creditCode || ''
      formData.taxNumber = detail.taxNumber || ''
      formData.contact = detail.contact || ''
      formData.phone = detail.phone || ''
      formData.email = detail.email || ''
      formData.address = detail.address || ''
      formData.remark = detail.remark || ''
      formData.bankAccounts = (detail.bankAccounts || []).map((b) => ({
        id: b.id,
        accountName: b.accountName,
        bankName: b.bankName,
        bankAccountNumber: b.bankAccountNumber,
        branchName: b.branchName || '',
        isDefault: b.isDefault,
        remark: b.remark || '',
      }))
    }
  } catch (error) {
    console.error('获取供应商详情失败:', error)
  }
}

// 收款账户操作
function handleAddBankAccount() {
  formData.bankAccounts.push({
    accountName: '',
    bankName: '',
    bankAccountNumber: '',
    branchName: '',
    isDefault: false,
    remark: '',
  })
}

function handleRemoveBankAccount(index: number) {
  formData.bankAccounts.splice(index, 1)
  delete bankErrors.value[index]
}

function handleDefaultChange(index: number) {
  if (formData.bankAccounts[index].isDefault) {
    formData.bankAccounts.forEach((item, i) => {
      if (i !== index) item.isDefault = false
    })
  }
}

// 校验收款账户
function validateBankAccounts(): boolean {
  bankErrors.value = {}
  let valid = true
  formData.bankAccounts.forEach((row, index) => {
    const errors: Record<string, boolean> = {}
    if (!row.accountName?.trim()) { errors.accountName = true; valid = false }
    if (!row.bankName?.trim()) { errors.bankName = true; valid = false }
    if (!row.bankAccountNumber?.trim()) { errors.bankAccountNumber = true; valid = false }
    if (Object.keys(errors).length > 0) {
      bankErrors.value[index] = errors
    }
  })
  if (!valid) {
    message.warning('请完善收款账户信息')
  }
  return valid
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }
  if (!validateBankAccounts()) return

  submitLoading.value = true
  try {
    const data: any = {
      fullName: formData.fullName,
      shortName: formData.shortName || undefined,
      creditCode: formData.creditCode || undefined,
      taxNumber: formData.taxNumber || undefined,
      contact: formData.contact || undefined,
      phone: formData.phone || undefined,
      email: formData.email || undefined,
      address: formData.address || undefined,
      remark: formData.remark || undefined,
      bankAccounts: formData.bankAccounts.map((b) => ({
        accountName: b.accountName,
        bankName: b.bankName,
        bankAccountNumber: b.bankAccountNumber,
        branchName: b.branchName || undefined,
        isDefault: b.isDefault,
        remark: b.remark || undefined,
      })),
    }

    if (dialogType.value === 'add') {
      await createSupplier(data)
      message.success('新增成功')
    } else {
      data.code = formData.code
      await updateSupplier(currentSupplierId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchSupplierList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deleteSupplier(row.id)
    message.success('删除成功')
    fetchSupplierList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

// 启用/停用
async function handleToggleStatus(row: any) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateSupplierStatus(row.id, newStatus)
    message.success(`${actionText}成功`)
    fetchSupplierList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
  }
}

onMounted(() => {
  fetchSupplierList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.is-error-input {
  :deep(.ant-input) {
    border-color: $color-danger;
  }
}
</style>
