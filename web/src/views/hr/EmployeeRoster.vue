<template>
  <div class="employee-roster-page">
    <PageHeader title="员工花名册">
      <template #right>
        <a-button type="primary" @click="openAddDialog">
          <template #icon><PlusOutlined /></template>新增员工
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; gap:8px; width:100%;">
          <a-input
            v-model:value="keyword"
            placeholder="搜索姓名/手机号/身份证号"
            allow-clear
            style="width: 260px"
            @keyup.enter="handleSearch"
            @change="(e: any) => { if (!e.target.value) handleSearch() }"
          >
            <template #prefix>
              <SearchOutlined />
            </template>
          </a-input>
          <a-select
            v-model:value="statusFilter"
            placeholder="在职状态"
            allow-clear
            style="width: 120px"
            :options="[
              { label: '在职', value: 1 },
              { label: '试用', value: 2 },
              { label: '离职', value: 3 },
            ]"
            @change="handleSearch"
          />
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-table
      :columns="columns"
      :data-source="tableData"
      :loading="loading"
      :pagination="paginationConfig"
      row-key="id"
      bordered
      :scroll="{ x: 1100 }"
      class="roster-table"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.dataIndex === 'index'">
          {{ (pageIndex - 1) * pageSize + index + 1 }}
        </template>
        <template v-if="column.dataIndex === 'gender'">
          {{ record.gender === 1 ? '男' : record.gender === 2 ? '女' : '' }}
        </template>
        <template v-if="column.dataIndex === 'hireDate'">
          {{ formatDate(record.hireDate) }}
        </template>
        <template v-if="column.dataIndex === 'employeeStatus'">
          <a-tag :color="statusTagColor(record.employeeStatus)">
            {{ statusLabel(record.employeeStatus) }}
          </a-tag>
        </template>
        <template v-if="column.dataIndex === 'defaultAccount'">
          {{ getDefaultAccount(record.paymentAccounts) }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click="openEditDialog(record)">编辑</a-button>
          <a-popconfirm :title="`确定删除员工「${record.name}」？`" @confirm="handleDelete(record)">
            <a-button type="link" size="small" danger>删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="editingId ? '编辑员工' : '新增员工'"
      width="750px"
      :destroy-on-close="true"
      centered
      @cancel="dialogVisible = false"
    >
      <a-tabs v-model:activeKey="activeTab">
        <!-- Tab 1: 基本信息 -->
        <a-tab-pane key="basic" tab="基本信息">
          <a-form
            ref="formRef"
            :model="form"
            :rules="rules"
            :label-col="{ style: { width: '110px' } }"
          >
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="关联用户">
                  <a-select
                    v-model:value="form.userId"
                    placeholder="搜索用户"
                    show-search
                    allow-clear
                    :filter-option="false"
                    :disabled="!!editingId"
                    style="width: 100%"
                    :loading="userSearchLoading"
                    :options="userSelectOptions"
                    @search="remoteSearchUsers"
                    @change="onUserSelect"
                  />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="姓名" name="name">
                  <a-input v-model:value="form.name" placeholder="请输入姓名" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="性别">
                  <a-radio-group v-model:value="form.gender">
                    <a-radio :value="1">男</a-radio>
                    <a-radio :value="2">女</a-radio>
                  </a-radio-group>
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="出生日期">
                  <a-date-picker
                    v-model:value="form.birthDate"
                    value-format="YYYY-MM-DD"
                    placeholder="请选择"
                    style="width: 100%"
                  />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="身份证号">
                  <a-input v-model:value="form.idCard" placeholder="请输入身份证号" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="手机号">
                  <a-input v-model:value="form.phone" placeholder="请输入手机号" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="民族">
                  <a-input v-model:value="form.ethnicity" placeholder="请输入民族" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="学历">
                  <a-select
                    v-model:value="form.education"
                    placeholder="请选择"
                    allow-clear
                    style="width: 100%"
                    :options="[
                      { label: '高中', value: '高中' },
                      { label: '中专', value: '中专' },
                      { label: '大专', value: '大专' },
                      { label: '本科', value: '本科' },
                      { label: '硕士', value: '硕士' },
                      { label: '博士', value: '博士' },
                      { label: '其他', value: '其他' },
                    ]"
                  />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="婚姻状况">
                  <a-select
                    v-model:value="form.maritalStatus"
                    placeholder="请选择"
                    allow-clear
                    style="width: 100%"
                    :options="[
                      { label: '未婚', value: '未婚' },
                      { label: '已婚', value: '已婚' },
                      { label: '离异', value: '离异' },
                    ]"
                  />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="在职状态" name="employeeStatus">
                  <a-select
                    v-model:value="form.employeeStatus"
                    placeholder="请选择"
                    style="width: 100%"
                    :options="[
                      { label: '在职', value: 1 },
                      { label: '试用', value: 2 },
                      { label: '离职', value: 3 },
                    ]"
                  />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="家庭住址">
                  <a-input v-model:value="form.homeAddress" placeholder="请输入家庭住址" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="户籍地址">
                  <a-input v-model:value="form.registeredAddress" placeholder="请输入户籍地址" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="紧急联系人">
                  <a-input v-model:value="form.emergencyContact" placeholder="请输入" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="紧急联系电话">
                  <a-input v-model:value="form.emergencyPhone" placeholder="请输入" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="紧急联系关系">
                  <a-input v-model:value="form.emergencyRelation" placeholder="请输入" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="入职日期">
                  <a-date-picker
                    v-model:value="form.hireDate"
                    value-format="YYYY-MM-DD"
                    placeholder="请选择"
                    style="width: 100%"
                  />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item label="转正日期">
                  <a-date-picker
                    v-model:value="form.confirmationDate"
                    value-format="YYYY-MM-DD"
                    placeholder="请选择"
                    style="width: 100%"
                  />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item label="备注">
                  <a-input v-model:value="form.remark" placeholder="请输入备注" />
                </a-form-item>
              </a-col>
            </a-row>
          </a-form>
        </a-tab-pane>

        <!-- Tab 2: 收款账号 -->
        <a-tab-pane key="accounts" tab="收款账号">
          <div style="margin-bottom: 10px">
            <a-button type="primary" size="small" @click="addAccount">
              <template #icon><PlusOutlined /></template>添加账号
            </a-button>
          </div>
          <a-table
            :columns="accountColumns"
            :data-source="form.paymentAccounts"
            :pagination="false"
            bordered
            size="small"
            row-key="(_, index) => index"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'accountType'">
                <a-select
                  v-model:value="record.accountType"
                  placeholder="请选择"
                  size="small"
                  :options="[
                    { label: '银行卡', value: '银行卡' },
                    { label: '支付宝', value: '支付宝' },
                    { label: '微信', value: '微信' },
                  ]"
                />
              </template>
              <template v-if="column.dataIndex === 'accountName'">
                <a-input v-model:value="record.accountName" size="small" placeholder="户名" />
              </template>
              <template v-if="column.dataIndex === 'accountNumber'">
                <a-input v-model:value="record.accountNumber" size="small" placeholder="账号" />
              </template>
              <template v-if="column.dataIndex === 'bankName'">
                <a-input
                  v-model:value="record.bankName"
                  size="small"
                  placeholder="开户银行"
                  :disabled="record.accountType !== '银行卡'"
                />
              </template>
              <template v-if="column.dataIndex === 'bankBranch'">
                <a-input
                  v-model:value="record.bankBranch"
                  size="small"
                  placeholder="开户支行"
                  :disabled="record.accountType !== '银行卡'"
                />
              </template>
              <template v-if="column.dataIndex === 'isDefault'">
                <a-switch
                  :checked="record.isDefault === 1"
                  @change="(checked: any) => { record.isDefault = checked ? 1 : 0; onDefaultChange(index) }"
                />
              </template>
              <template v-if="column.dataIndex === 'remark'">
                <a-input v-model:value="record.remark" size="small" placeholder="备注" />
              </template>
              <template v-if="column.dataIndex === 'accountAction'">
                <a-button type="link" size="small" danger @click="removeAccount(index)">删除</a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>

      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, SearchOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getEmployeeList,
  getEmployeeDetail,
  createEmployee,
  updateEmployee,
  deleteEmployee,
  searchAvailableUsers,
} from '@/api/hr'

// ==================== 列表相关 ====================
const keyword = ref('')
const statusFilter = ref<number | undefined>(undefined)
const pageIndex = ref(1)
const pageSize = ref(20)
const total = ref(0)
const tableData = ref<any[]>([])
const loading = ref(false)

const columns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '姓名', dataIndex: 'name', key: 'name', width: 100, align: 'center' as const },
  { title: '性别', dataIndex: 'gender', key: 'gender', width: 70, align: 'center' as const },
  { title: '手机号', dataIndex: 'phone', key: 'phone', width: 130, align: 'center' as const },
  { title: '身份证号', dataIndex: 'idCard', key: 'idCard', width: 190, align: 'center' as const },
  { title: '入职日期', dataIndex: 'hireDate', key: 'hireDate', width: 120, align: 'center' as const },
  { title: '在职状态', dataIndex: 'employeeStatus', key: 'employeeStatus', width: 100, align: 'center' as const },
  { title: '默认收款账号', dataIndex: 'defaultAccount', key: 'defaultAccount' },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

const accountColumns = [
  { title: '账号类型', dataIndex: 'accountType', key: 'accountType', width: 130, align: 'center' as const },
  { title: '户名', dataIndex: 'accountName', key: 'accountName', width: 120 },
  { title: '账号', dataIndex: 'accountNumber', key: 'accountNumber' },
  { title: '开户银行', dataIndex: 'bankName', key: 'bankName', width: 130 },
  { title: '开户支行', dataIndex: 'bankBranch', key: 'bankBranch', width: 130 },
  { title: '默认', dataIndex: 'isDefault', key: 'isDefault', width: 70, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 120 },
  { title: '操作', dataIndex: 'accountAction', key: 'accountAction', width: 70, align: 'center' as const },
]

const paginationConfig = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  pageSizeOptions: ['20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pageIndex.value = pag.current
  pageSize.value = pag.pageSize
  loadList()
}

function handleSearch() {
  pageIndex.value = 1
  loadList()
}

async function loadList() {
  loading.value = true
  try {
    const res: any = await getEmployeeList({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      employeeStatus: statusFilter.value,
    })
    tableData.value = res.items || res.data || res || []
    total.value = res.total ?? 0
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

// ==================== 状态 helpers ====================
function statusLabel(status: number) {
  return status === 1 ? '在职' : status === 2 ? '试用' : status === 3 ? '离职' : ''
}

function statusTagColor(status: number) {
  return status === 1 ? 'success' : status === 2 ? 'warning' : 'default'
}

function getDefaultAccount(accounts: any[] | undefined) {
  if (!accounts || accounts.length === 0) return ''
  const def = accounts.find((a: any) => a.isDefault === 1) || accounts[0]
  return `${def.accountType}: ${def.accountNumber}`
}

function formatDate(dateStr: string) {
  if (!dateStr) return ''
  return dateStr.substring(0, 10)
}

// ==================== 弹窗相关 ====================
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<number | null>(null)
const activeTab = ref('basic')
const formRef = ref<FormInstance>()

interface PaymentAccountForm {
  id?: number
  accountType: string
  accountName: string
  accountNumber: string
  bankName: string
  bankBranch: string
  isDefault: number
  remark: string
}

const defaultForm = () => ({
  userId: undefined as number | undefined,
  name: '',
  gender: 1,
  birthDate: '',
  idCard: '',
  phone: '',
  ethnicity: '',
  education: '',
  maritalStatus: '',
  homeAddress: '',
  registeredAddress: '',
  emergencyContact: '',
  emergencyPhone: '',
  emergencyRelation: '',
  hireDate: '',
  confirmationDate: '',
  employeeStatus: 1,
  remark: '',
  paymentAccounts: [] as PaymentAccountForm[],
})

const form = reactive(defaultForm())

const rules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  employeeStatus: [{ required: true, message: '请选择在职状态', trigger: 'change' }],
}

// ==================== 用户搜索 ====================
const userOptions = ref<any[]>([])
const userSearchLoading = ref(false)

const userSelectOptions = computed(() =>
  userOptions.value.map((u: any) => ({
    label: `${u.name}（${u.account}）`,
    value: u.id,
  }))
)

async function remoteSearchUsers(query: string) {
  if (!query) {
    userOptions.value = []
    return
  }
  userSearchLoading.value = true
  try {
    const res: any = await searchAvailableUsers(query)
    userOptions.value = Array.isArray(res) ? res : res?.data || []
  } catch {
    userOptions.value = []
  } finally {
    userSearchLoading.value = false
  }
}

function onUserSelect(userId: any) {
  if (!userId) return
  const user = userOptions.value.find((u: any) => u.id === userId)
  if (user) {
    form.name = user.name || ''
    form.phone = user.phone || ''
  }
}

// ==================== 收款账号操作 ====================
function addAccount() {
  form.paymentAccounts.push({
    accountType: '银行卡',
    accountName: '',
    accountNumber: '',
    bankName: '',
    bankBranch: '',
    isDefault: form.paymentAccounts.length === 0 ? 1 : 0,
    remark: '',
  })
}

function removeAccount(index: number) {
  form.paymentAccounts.splice(index, 1)
}

function onDefaultChange(index: number) {
  if (form.paymentAccounts[index].isDefault === 1) {
    form.paymentAccounts.forEach((acc, i) => {
      if (i !== index) acc.isDefault = 0
    })
  }
}

// ==================== 新增 / 编辑 ====================
function openAddDialog() {
  editingId.value = null
  activeTab.value = 'basic'
  Object.assign(form, defaultForm())
  userOptions.value = []
  dialogVisible.value = true
}

async function openEditDialog(row: any) {
  editingId.value = row.id
  activeTab.value = 'basic'
  userOptions.value = []
  try {
    const detail: any = await getEmployeeDetail(row.id)
    Object.assign(form, {
      userId: detail.userId ?? null,
      name: detail.name ?? '',
      gender: detail.gender ?? 1,
      birthDate: detail.birthDate ? detail.birthDate.substring(0, 10) : '',
      idCard: detail.idCard ?? '',
      phone: detail.phone ?? '',
      ethnicity: detail.ethnicity ?? '',
      education: detail.education ?? '',
      maritalStatus: detail.maritalStatus ?? '',
      homeAddress: detail.homeAddress ?? '',
      registeredAddress: detail.registeredAddress ?? '',
      emergencyContact: detail.emergencyContact ?? '',
      emergencyPhone: detail.emergencyPhone ?? '',
      emergencyRelation: detail.emergencyRelation ?? '',
      hireDate: detail.hireDate ? detail.hireDate.substring(0, 10) : '',
      confirmationDate: detail.confirmationDate ? detail.confirmationDate.substring(0, 10) : '',
      employeeStatus: detail.employeeStatus ?? 1,
      remark: detail.remark ?? '',
      paymentAccounts: (detail.paymentAccounts || []).map((a: any) => ({
        id: a.id,
        accountType: a.accountType ?? '银行卡',
        accountName: a.accountName ?? '',
        accountNumber: a.accountNumber ?? '',
        bankName: a.bankName ?? '',
        bankBranch: a.bankBranch ?? '',
        isDefault: a.isDefault ?? 0,
        remark: a.remark ?? '',
      })),
    })
    // 如果编辑时有关联用户，把用户放进选项中以便显示
    if (detail.userId && detail.userName) {
      userOptions.value = [{ id: detail.userId, name: detail.userName, account: detail.userAccount || '' }]
    }
    dialogVisible.value = true
  } catch (e: any) {
    message.error(e?.message || '加载详情失败')
  }
}

async function handleSave() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  saving.value = true
  try {
    const payload = {
      userId: form.userId,
      name: form.name,
      gender: form.gender,
      birthDate: form.birthDate || null,
      idCard: form.idCard,
      phone: form.phone,
      ethnicity: form.ethnicity,
      education: form.education,
      maritalStatus: form.maritalStatus,
      homeAddress: form.homeAddress,
      registeredAddress: form.registeredAddress,
      emergencyContact: form.emergencyContact,
      emergencyPhone: form.emergencyPhone,
      emergencyRelation: form.emergencyRelation,
      hireDate: form.hireDate || null,
      confirmationDate: form.confirmationDate || null,
      employeeStatus: form.employeeStatus,
      remark: form.remark,
      paymentAccounts: form.paymentAccounts,
    }
    if (editingId.value) {
      await updateEmployee(editingId.value, payload)
    } else {
      await createEmployee(payload)
    }
    message.success('保存成功')
    dialogVisible.value = false
    await loadList()
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// ==================== 删除 ====================
async function handleDelete(row: any) {
  try {
    await deleteEmployee(row.id)
    message.success('删除成功')
    await loadList()
  } catch (e: any) {
    message.error(e?.message || '删除失败')
  }
}

// ==================== 初始化 ====================
onMounted(() => {
  loadList()
})
</script>

<style scoped>
.employee-roster-page {
  padding: 20px;
  background: #fff;
  border-radius: 8px;
  min-height: calc(100vh - 120px);
}

.roster-table {
  width: 100%;
}
</style>
