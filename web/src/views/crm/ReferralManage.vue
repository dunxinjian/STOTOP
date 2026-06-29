<template>
  <div class="page-container">
    <PageHeader title="推荐返佣管理" description="管理客户推荐记录与返佣申请">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <template v-if="activeTab === 'referral'">
            <a-input v-model:value="referralSearch.keyword" placeholder="客户名称" allow-clear size="small" style="width: 160px" @keyup.enter="handleReferralSearch" />
            <a-select v-model:value="referralSearch.referrerType" placeholder="推荐人类型" allow-clear size="small" style="width: 120px" :options="referrerTypeOptions" />
            <a-range-picker v-model:value="referralSearch.dateRange" size="small" style="width: 220px" />
            <a-button type="primary" size="small" @click="handleReferralSearch">查询</a-button>
            <a-button size="small" @click="handleReferralReset">重置</a-button>
          </template>
          <template v-else>
            <a-input v-model:value="commissionSearch.keyword" placeholder="客户名称" allow-clear size="small" style="width: 160px" @keyup.enter="handleCommissionSearch" />
            <a-select v-model:value="commissionSearch.status" placeholder="状态" allow-clear size="small" style="width: 120px" :options="commissionStatusOptions" />
            <a-button type="primary" size="small" @click="handleCommissionSearch">查询</a-button>
            <a-button size="small" @click="handleCommissionReset">重置</a-button>
          </template>
        </div>
      </template>
    </PageHeader>

    <a-tabs v-model:activeKey="activeTab" type="card" class="crm-tabs">
      <!-- Tab 1: 推荐记录 -->
      <a-tab-pane key="referral" tab="推荐记录">
        <a-card :bordered="false">
          <div class="table-toolbar">
            <a-space>
              <a-button v-if="has(CrmPermissions.ReferralCreate)" type="primary" @click="handleAddReferral">
                <template #icon><PlusOutlined /></template>登记推荐
              </a-button>
              <a-button @click="contactDrawerVisible = true">
                <template #icon><TeamOutlined /></template>外部联系人
              </a-button>
            </a-space>
          </div>
          <a-table
            :columns="referralColumns"
            :data-source="referralData"
            :loading="referralLoading"
            :pagination="referralPaginationConfig"
            row-key="id"
            bordered
            :scroll="{ x: 1000 }"
            @change="handleReferralTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (referralPagination.pageIndex - 1) * referralPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'referrerType'">
                <a-tag :color="record.referrerType === 1 ? 'blue' : 'orange'">
                  {{ record.referrerType === 1 ? '内部' : '外部' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'referrerName'">
                {{ record.referrerType === 1 ? record.employeeName : record.externalContactName }}
              </template>
              <template v-if="column.dataIndex === 'commissionRate'">
                {{ record.commissionRate ? `${record.commissionRate}%` : '-' }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleEditReferral(record as ReferralDto)">
                  <EditOutlined />编辑
                </a-button>
                <a-popconfirm title="确定删除该推荐记录吗？" ok-text="确定" cancel-text="取消" @confirm="handleDeleteReferral(record as ReferralDto)">
                  <a-button type="link" size="small" danger>
                    <DeleteOutlined />删除
                  </a-button>
                </a-popconfirm>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无推荐记录" />
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>

      <!-- Tab 2: 返佣申请 -->
      <a-tab-pane key="commission" tab="返佣申请">
        <a-card :bordered="false">
          <div class="table-toolbar">
            <a-button v-if="has(CrmPermissions.CommissionApply)" type="primary" @click="handleAddCommission">
              <template #icon><PlusOutlined /></template>发起返佣
            </a-button>
          </div>
          <a-table
            :columns="commissionColumns"
            :data-source="commissionData"
            :loading="commissionLoading"
            :pagination="commissionPaginationConfig"
            row-key="id"
            bordered
            :scroll="{ x: 1000 }"
            @change="handleCommissionTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (commissionPagination.pageIndex - 1) * commissionPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="commissionStatusColorMap[record.status] || 'default'">
                  {{ commissionStatusTextMap[record.status] || '未知' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'commissionAmount'">
                ¥{{ record.commissionAmount?.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button v-if="record.status === 0" type="link" size="small" @click="handleEditCommission(record as CommissionDto)">
                  <EditOutlined />编辑
                </a-button>
                <a-button v-if="record.status === 0" type="link" size="small" @click="handleSubmitCommission(record as CommissionDto)">
                  <SendOutlined />提交审批
                </a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无返佣申请" />
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>
    </a-tabs>

    <!-- 登记/编辑推荐 Modal -->
    <a-modal
      v-model:open="referralModalVisible"
      :title="referralModalType === 'add' ? '登记推荐' : '编辑推荐'"
      width="600px"
      :destroy-on-close="true"
      @cancel="referralModalVisible = false"
    >
      <a-form ref="referralFormRef" :model="referralForm" :rules="referralFormRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="客户" name="customerId">
          <a-select v-model:value="referralForm.customerId" placeholder="请选择客户" show-search :filter-option="false" style="width: 100%" />
        </a-form-item>
        <a-form-item label="推荐人类型" name="referrerType">
          <a-radio-group v-model:value="referralForm.referrerType">
            <a-radio :value="1">内部员工</a-radio>
            <a-radio :value="2">外部联系人</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item v-if="referralForm.referrerType === 1" label="员工" name="employeeId">
          <a-select v-model:value="referralForm.employeeId" placeholder="请选择员工" show-search :filter-option="false" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="referralForm.referrerType === 2" label="外部联系人" name="externalContactId">
          <a-select v-model:value="referralForm.externalContactId" placeholder="请选择外部联系人" show-search :filter-option="false" style="width: 100%" />
        </a-form-item>
        <a-form-item label="推荐日期" name="referralDate">
          <a-date-picker v-model:value="referralForm.referralDate" style="width: 100%" />
        </a-form-item>
        <a-form-item label="返佣比例">
          <a-input-number v-model:value="referralForm.commissionRate" :min="0" :max="100" :precision="2" addon-after="%" style="width: 100%" />
        </a-form-item>
        <a-form-item label="说明">
          <a-textarea v-model:value="referralForm.description" :rows="3" placeholder="请输入说明" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="referralModalVisible = false">取消</a-button>
        <a-button type="primary" :loading="referralSubmitLoading" @click="handleSubmitReferral">确定</a-button>
      </template>
    </a-modal>

    <!-- 发起返佣 Modal -->
    <a-modal
      v-model:open="commissionModalVisible"
      title="发起返佣"
      width="600px"
      :destroy-on-close="true"
      @cancel="commissionModalVisible = false"
    >
      <a-form ref="commissionFormRef" :model="commissionForm" :rules="commissionFormRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="关联推荐" name="referralId">
          <a-select v-model:value="commissionForm.referralId" placeholder="请选择推荐记录" style="width: 100%" />
        </a-form-item>
        <a-form-item label="客户" name="customerId">
          <a-select v-model:value="commissionForm.customerId" placeholder="请选择客户" style="width: 100%" />
        </a-form-item>
        <a-form-item label="返佣金额" name="commissionAmount">
          <a-input-number v-model:value="commissionForm.commissionAmount" :min="0" :precision="2" prefix="¥" style="width: 100%" />
        </a-form-item>
        <a-form-item label="计算依据">
          <a-textarea v-model:value="commissionForm.calcBasis" :rows="3" placeholder="请输入计算依据" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="commissionModalVisible = false">取消</a-button>
        <a-button type="primary" :loading="commissionSubmitLoading" @click="handleSubmitCommissionForm">确定</a-button>
      </template>
    </a-modal>

    <!-- 外部联系人 Drawer -->
    <a-drawer v-model:open="contactDrawerVisible" title="外部联系人管理" width="700" :destroy-on-close="true">
      <div class="drawer-toolbar">
        <a-button type="primary" size="small" @click="handleAddContact">
          <template #icon><PlusOutlined /></template>新增联系人
        </a-button>
      </div>
      <a-table
        :columns="contactColumns"
        :data-source="contactData"
        :loading="contactLoading"
        :pagination="false"
        row-key="id"
        bordered
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEditContact(record as ExternalContactDto)">编辑</a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无外部联系人" />
        </template>
      </a-table>

      <!-- 新增/编辑联系人 -->
      <a-modal
        v-model:open="contactModalVisible"
        :title="contactModalType === 'add' ? '新增外部联系人' : '编辑外部联系人'"
        width="500px"
        :destroy-on-close="true"
        @cancel="contactModalVisible = false"
      >
        <a-form ref="contactFormRef" :model="contactForm" :rules="contactFormRules" :label-col="{ style: { width: '80px' } }" style="padding: 10px">
          <a-form-item label="姓名" name="name">
            <a-input v-model:value="contactForm.name" placeholder="请输入姓名" :maxlength="50" />
          </a-form-item>
          <a-form-item label="电话">
            <a-input v-model:value="contactForm.phone" placeholder="请输入电话" :maxlength="30" />
          </a-form-item>
          <a-form-item label="公司">
            <a-input v-model:value="contactForm.company" placeholder="请输入公司" :maxlength="200" />
          </a-form-item>
          <a-form-item label="收款账户">
            <a-input v-model:value="contactForm.bankAccount" placeholder="请输入收款账户" :maxlength="50" />
          </a-form-item>
          <a-form-item label="开户行">
            <a-input v-model:value="contactForm.bankName" placeholder="请输入开户行" :maxlength="100" />
          </a-form-item>
        </a-form>
        <template #footer>
          <a-button @click="contactModalVisible = false">取消</a-button>
          <a-button type="primary" :loading="contactSubmitLoading" @click="handleSubmitContact">确定</a-button>
        </template>
      </a-modal>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, SendOutlined, TeamOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { CrmPermissions, usePermission } from '@/utils/permission'
import {
  getReferralList, createReferral, deleteReferral,
  getCommissionList, createCommission, updateCommissionStatus,
  getExternalContactList, createExternalContact, updateExternalContact,
  type ReferralDto, type CommissionDto, type ExternalContactDto,
} from '@/api/crm'

const { has } = usePermission()

// ==================== 常量 ====================
const referrerTypeOptions = [
  { label: '内部', value: 1 },
  { label: '外部', value: 2 },
]

const commissionStatusOptions = [
  { label: '草稿', value: 0 },
  { label: '审批中', value: 1 },
  { label: '已批准', value: 2 },
  { label: '已付款', value: 3 },
  { label: '已驳回', value: 4 },
]

const commissionStatusColorMap: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'cyan', 4: 'error' }
const commissionStatusTextMap: Record<number, string> = { 0: '草稿', 1: '审批中', 2: '已批准', 3: '已付款', 4: '已驳回' }

const activeTab = ref('referral')

// ==================== 推荐记录 ====================
const referralColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '客户名称', dataIndex: 'customerName', width: 150, ellipsis: true },
  { title: '推荐人类型', dataIndex: 'referrerType', width: 100, align: 'center' as const },
  { title: '推荐人名称', dataIndex: 'referrerName', width: 130 },
  { title: '推荐日期', dataIndex: 'referralDate', width: 120 },
  { title: '返佣比例', dataIndex: 'commissionRate', width: 100, align: 'right' as const },
  { title: '说明', dataIndex: 'description', ellipsis: true },
  { title: '操作', dataIndex: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

const referralSearch = reactive({ keyword: '', referrerType: undefined as number | undefined, dateRange: null as any })
const referralLoading = ref(false)
const referralData = ref<ReferralDto[]>([])
const referralPagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const referralPaginationConfig = computed(() => ({
  current: referralPagination.pageIndex, pageSize: referralPagination.pageSize, total: referralPagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

async function fetchReferralList() {
  referralLoading.value = true
  try {
    const params: any = { pageIndex: referralPagination.pageIndex, pageSize: referralPagination.pageSize }
    if (referralSearch.keyword) params.keyword = referralSearch.keyword
    if (referralSearch.referrerType !== undefined) params.referrerType = referralSearch.referrerType
    if (referralSearch.dateRange?.[0]) params.startDate = referralSearch.dateRange[0].format('YYYY-MM-DD')
    if (referralSearch.dateRange?.[1]) params.endDate = referralSearch.dateRange[1].format('YYYY-MM-DD')
    const res = await getReferralList(params) as any
    referralData.value = res?.items || res || []
    referralPagination.total = res?.total || 0
  } finally { referralLoading.value = false }
}

function handleReferralSearch() { referralPagination.pageIndex = 1; fetchReferralList() }
function handleReferralReset() { referralSearch.keyword = ''; referralSearch.referrerType = undefined; referralSearch.dateRange = null; handleReferralSearch() }
function handleReferralTableChange(pag: any) { referralPagination.pageIndex = pag.current; referralPagination.pageSize = pag.pageSize; fetchReferralList() }

// 登记/编辑推荐
const referralModalVisible = ref(false)
const referralModalType = ref<'add' | 'edit'>('add')
const referralFormRef = ref<FormInstance>()
const referralSubmitLoading = ref(false)
const currentReferralId = ref<number | null>(null)

const referralForm = reactive({
  customerId: undefined as number | undefined,
  referrerType: 1,
  employeeId: undefined as number | undefined,
  externalContactId: undefined as number | undefined,
  referralDate: null as any,
  commissionRate: undefined as number | undefined,
  description: '',
})

const referralFormRules: Record<string, Rule[]> = {
  customerId: [{ required: true, message: '请选择客户', trigger: 'change' }],
  referrerType: [{ required: true, message: '请选择推荐人类型', trigger: 'change' }],
  referralDate: [{ required: true, message: '请选择推荐日期', trigger: 'change' }],
}

function handleAddReferral() {
  referralModalType.value = 'add'
  currentReferralId.value = null
  Object.assign(referralForm, { customerId: undefined, referrerType: 1, employeeId: undefined, externalContactId: undefined, referralDate: null, commissionRate: undefined, description: '' })
  referralModalVisible.value = true
}

function handleEditReferral(record: ReferralDto) {
  referralModalType.value = 'edit'
  currentReferralId.value = record.id
  Object.assign(referralForm, { customerId: record.customerId, referrerType: record.referrerType, employeeId: record.employeeId, externalContactId: record.externalContactId, referralDate: record.referralDate, commissionRate: record.commissionRate, description: record.description || '' })
  referralModalVisible.value = true
}

async function handleSubmitReferral() {
  if (!referralFormRef.value) return
  try { await referralFormRef.value.validate() } catch { return }
  referralSubmitLoading.value = true
  try {
    const data = {
      customerId: referralForm.customerId!,
      referrerType: referralForm.referrerType,
      employeeId: referralForm.referrerType === 1 ? referralForm.employeeId : undefined,
      externalContactId: referralForm.referrerType === 2 ? referralForm.externalContactId : undefined,
      referralDate: typeof referralForm.referralDate === 'string' ? referralForm.referralDate : referralForm.referralDate?.format('YYYY-MM-DD'),
      commissionRate: referralForm.commissionRate,
      description: referralForm.description || undefined,
    }
    await createReferral(data)
    message.success(referralModalType.value === 'add' ? '登记成功' : '更新成功')
    referralModalVisible.value = false
    fetchReferralList()
  } finally { referralSubmitLoading.value = false }
}

async function handleDeleteReferral(record: ReferralDto) {
  try { await deleteReferral(record.id); message.success('删除成功'); fetchReferralList() } catch { /* handled */ }
}

// ==================== 返佣申请 ====================
const commissionColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '客户名称', dataIndex: 'customerName', width: 150, ellipsis: true },
  { title: '推荐人', dataIndex: 'creatorName', width: 120 },
  { title: '返佣金额', dataIndex: 'commissionAmount', width: 120, align: 'right' as const },
  { title: '计算依据', dataIndex: 'calcBasis', ellipsis: true },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '申请人', dataIndex: 'creatorName', width: 100 },
  { title: '操作', dataIndex: 'action', width: 160, align: 'center' as const, fixed: 'right' as const },
]

const commissionSearch = reactive({ keyword: '', status: undefined as number | undefined })
const commissionLoading = ref(false)
const commissionData = ref<CommissionDto[]>([])
const commissionPagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const commissionPaginationConfig = computed(() => ({
  current: commissionPagination.pageIndex, pageSize: commissionPagination.pageSize, total: commissionPagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

async function fetchCommissionList() {
  commissionLoading.value = true
  try {
    const params: any = { pageIndex: commissionPagination.pageIndex, pageSize: commissionPagination.pageSize }
    if (commissionSearch.status !== undefined) params.status = commissionSearch.status
    const res = await getCommissionList(params) as any
    commissionData.value = res?.items || res || []
    commissionPagination.total = res?.total || 0
  } finally { commissionLoading.value = false }
}

function handleCommissionSearch() { commissionPagination.pageIndex = 1; fetchCommissionList() }
function handleCommissionReset() { commissionSearch.keyword = ''; commissionSearch.status = undefined; handleCommissionSearch() }
function handleCommissionTableChange(pag: any) { commissionPagination.pageIndex = pag.current; commissionPagination.pageSize = pag.pageSize; fetchCommissionList() }

// 发起返佣
const commissionModalVisible = ref(false)
const commissionFormRef = ref<FormInstance>()
const commissionSubmitLoading = ref(false)

const commissionForm = reactive({
  referralId: undefined as number | undefined,
  customerId: undefined as number | undefined,
  commissionAmount: undefined as number | undefined,
  calcBasis: '',
})

const commissionFormRules: Record<string, Rule[]> = {
  referralId: [{ required: true, message: '请选择推荐记录', trigger: 'change' }],
  customerId: [{ required: true, message: '请选择客户', trigger: 'change' }],
  commissionAmount: [{ required: true, message: '请输入返佣金额', trigger: 'blur' }],
}

function handleAddCommission() {
  Object.assign(commissionForm, { referralId: undefined, customerId: undefined, commissionAmount: undefined, calcBasis: '' })
  commissionModalVisible.value = true
}

function handleEditCommission(record: CommissionDto) {
  Object.assign(commissionForm, { referralId: record.referralId, customerId: record.customerId, commissionAmount: record.commissionAmount, calcBasis: record.calcBasis || '' })
  commissionModalVisible.value = true
}

async function handleSubmitCommissionForm() {
  if (!commissionFormRef.value) return
  try { await commissionFormRef.value.validate() } catch { return }
  commissionSubmitLoading.value = true
  try {
    await createCommission({
      referralId: commissionForm.referralId!,
      customerId: commissionForm.customerId!,
      commissionAmount: commissionForm.commissionAmount!,
      calcBasis: commissionForm.calcBasis || undefined,
      applicantId: 0,
    })
    message.success('创建成功')
    commissionModalVisible.value = false
    fetchCommissionList()
  } finally { commissionSubmitLoading.value = false }
}

async function handleSubmitCommission(record: CommissionDto) {
  try { await updateCommissionStatus(record.id, 1); message.success('已提交审批'); fetchCommissionList() } catch { /* handled */ }
}

// ==================== 外部联系人 ====================
const contactDrawerVisible = ref(false)
const contactLoading = ref(false)
const contactData = ref<ExternalContactDto[]>([])

const contactColumns = [
  { title: '姓名', dataIndex: 'name', width: 100 },
  { title: '电话', dataIndex: 'phone', width: 130 },
  { title: '公司', dataIndex: 'company', width: 150, ellipsis: true },
  { title: '收款账户', dataIndex: 'bankAccount', width: 150 },
  { title: '开户行', dataIndex: 'bankName', width: 130 },
  { title: '操作', dataIndex: 'action', width: 70, align: 'center' as const },
]

const contactModalVisible = ref(false)
const contactModalType = ref<'add' | 'edit'>('add')
const contactFormRef = ref<FormInstance>()
const contactSubmitLoading = ref(false)
const currentContactId = ref<number | null>(null)

const contactForm = reactive({ name: '', phone: '', company: '', bankAccount: '', bankName: '' })
const contactFormRules: Record<string, Rule[]> = { name: [{ required: true, message: '请输入姓名', trigger: 'blur' }] }

async function fetchContactList() {
  contactLoading.value = true
  try {
    const res = await getExternalContactList({ pageSize: 200 }) as any
    contactData.value = res?.items || res || []
  } finally { contactLoading.value = false }
}

function handleAddContact() {
  contactModalType.value = 'add'
  currentContactId.value = null
  Object.assign(contactForm, { name: '', phone: '', company: '', bankAccount: '', bankName: '' })
  contactModalVisible.value = true
}

function handleEditContact(record: ExternalContactDto) {
  contactModalType.value = 'edit'
  currentContactId.value = record.id
  Object.assign(contactForm, { name: record.name, phone: record.phone || '', company: record.company || '', bankAccount: record.bankAccount || '', bankName: record.bankName || '' })
  contactModalVisible.value = true
}

async function handleSubmitContact() {
  if (!contactFormRef.value) return
  try { await contactFormRef.value.validate() } catch { return }
  contactSubmitLoading.value = true
  try {
    const data = { name: contactForm.name, phone: contactForm.phone || undefined, company: contactForm.company || undefined, bankAccount: contactForm.bankAccount || undefined, bankName: contactForm.bankName || undefined }
    if (contactModalType.value === 'add') {
      await createExternalContact(data)
      message.success('新增成功')
    } else {
      await updateExternalContact(currentContactId.value!, { ...data, status: 1 })
      message.success('更新成功')
    }
    contactModalVisible.value = false
    fetchContactList()
  } finally { contactSubmitLoading.value = false }
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchReferralList()
  fetchCommissionList()
  fetchContactList()
})
</script>

<style scoped lang="scss">
.crm-tabs {
  margin-top: 16px;
}

.table-toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16px;
}

.drawer-toolbar {
  margin-bottom: 16px;
}
</style>
