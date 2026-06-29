<template>
  <div class="page-container">
    <PageHeader title="共保基金" description="管理共保基金和缴费记录">
      <template #actions>
        <a-button type="primary" @click="handleAddFund">
          <template #icon><PlusOutlined /></template>新增基金
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="基金名称/编码" style="width: 200px" allowClear @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.businessType" size="small" placeholder="业务类型" style="width: 120px" allowClear :options="businessTypeOptions" />
          <a-select v-model:value="searchForm.fundStatus" size="small" placeholder="基金状态" style="width: 120px" allowClear :options="fundStatusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 基金余额卡片 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col v-for="fund in fundCards" :key="fund.Id" :span="6">
        <a-card :bordered="false" class="stat-card">
          <a-statistic :title="fund.FundName" :value="fund.FundBalance" :precision="2" prefix="¥" :value-style="{ color: fund.FundBalance > 0 ? 'var(--color-success)' : 'var(--color-danger)' }" />
          <div style="margin-top: 4px; font-size: 12px; color: #999">
            缴入: ¥{{ fund.TotalContributions.toFixed(2) }} | 赔出: ¥{{ fund.TotalPayouts.toFixed(2) }}
          </div>
        </a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab">
        <a-tab-pane key="funds" tab="基金列表">
          <a-table
            :columns="fundColumns"
            :data-source="tableData"
            :loading="loading"
            :pagination="paginationConfig"
            row-key="Id"
            bordered
            :scroll="{ x: 1100 }"
            @change="handleTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'BusinessType'">
                <a-tag>{{ businessTypeMap[record.BusinessType] || '未知' }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'FundBalance'">
                <span :style="{ color: record.FundBalance >= 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 500 }">
                  ¥{{ record.FundBalance.toFixed(2) }}
                </span>
              </template>
              <template v-if="column.dataIndex === 'TotalContributions'">
                ¥{{ record.TotalContributions.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'TotalPayouts'">
                ¥{{ record.TotalPayouts.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'EffectiveDate'">
                {{ formatDate(record.EffectiveDate) }}
              </template>
              <template v-if="column.dataIndex === 'FundStatus'">
                <a-tag :color="fundStatusColorMap[record.FundStatus] || 'default'">
                  {{ fundStatusMap[record.FundStatus] || '未知' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleEditFund(record as InsCoInsuranceFundListItemDto)">编辑</a-button>
                <a-button type="link" size="small" @click="handleViewContributions(record as InsCoInsuranceFundListItemDto)">缴费记录</a-button>
                <a-button type="link" size="small" @click="handleGenerate(record as InsCoInsuranceFundListItemDto)">生成缴费单</a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无基金数据" />
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="contributions" tab="缴费记录">
          <a-table
            :columns="contributionColumns"
            :data-source="contributionData"
            :loading="contribLoading"
            :pagination="contribPaginationConfig"
            row-key="Id"
            bordered
            :scroll="{ x: 1000 }"
            @change="handleContribTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (contribPagination.pageIndex - 1) * contribPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'ContributionAmount'">
                ¥{{ record.ContributionAmount.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'PeriodStart'">
                {{ formatDate(record.PeriodStart) }}
              </template>
              <template v-if="column.dataIndex === 'PeriodEnd'">
                {{ formatDate(record.PeriodEnd) }}
              </template>
              <template v-if="column.dataIndex === 'PaymentDate'">
                {{ record.PaymentDate ? formatDate(record.PaymentDate) : '-' }}
              </template>
              <template v-if="column.dataIndex === 'PaymentStatus'">
                <a-tag :color="paymentStatusColorMap[record.PaymentStatus] || 'default'">
                  {{ paymentStatusMap[record.PaymentStatus] || '未知' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button v-if="record.PaymentStatus === 1" type="link" size="small" @click="handleConfirmPayment(record as InsFundContributionListItemDto)">确认缴费</a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无缴费记录" />
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 新增/编辑基金弹窗 -->
    <a-modal
      v-model:open="fundDialogVisible"
      :title="fundDialogType === 'add' ? '新增基金' : '编辑基金'"
      width="650px"
      :destroy-on-close="true"
      @cancel="fundDialogVisible = false"
    >
      <a-form ref="fundFormRef" :model="fundFormData" :rules="fundFormRules" :label-col="{ style: { width: '110px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="基金名称" name="FundName">
              <a-input v-model:value="fundFormData.FundName" placeholder="请输入" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="基金编码" name="FundCode">
              <a-input v-model:value="fundFormData.FundCode" placeholder="请输入" :maxlength="50" :disabled="fundDialogType === 'edit'" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="业务类型" name="BusinessType">
              <a-select v-model:value="fundFormData.BusinessType" placeholder="请选择" :options="businessTypeEditOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="缴费标准">
              <a-input-number v-model:value="fundFormData.ContributionStandard" placeholder="每期标准金额" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="缴费周期">
              <a-select v-model:value="fundFormData.PaymentCycle" placeholder="请选择" allow-clear :options="paymentCycleOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="免赔额" name="Deductible">
              <a-input-number v-model:value="fundFormData.Deductible" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="单次赔付上限">
              <a-input-number v-model:value="fundFormData.SinglePayoutLimit" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="年度赔付上限">
              <a-input-number v-model:value="fundFormData.AnnualPayoutLimit" placeholder="请输入" style="width: 100%" :min="0" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="生效日期" name="EffectiveDate">
              <a-date-picker v-model:value="fundFormData.EffectiveDate" placeholder="请选择" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="描述">
              <a-textarea v-model:value="fundFormData.FundDescription" :rows="2" placeholder="请输入" :maxlength="500" show-count />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="fundDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="fundSubmitLoading" @click="handleFundSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getFundList,
  createFund,
  updateFund,
  generateContributions,
  confirmContribution,
  getFundContributions,
  type InsCoInsuranceFundListItemDto,
  type InsFundContributionListItemDto,
} from '@/api/insurance'

const businessTypeMap: Record<number, string> = { 1: '三轮车', 2: '员工', 3: '资产' }
const businessTypeOptions = [{ label: '三轮车', value: 1 }, { label: '员工', value: 2 }, { label: '资产', value: 3 }]
const businessTypeEditOptions = [...businessTypeOptions]
const fundStatusMap: Record<number, string> = { 1: '正常', 2: '冻结', 3: '已关闭' }
const fundStatusColorMap: Record<number, string> = { 1: 'success', 2: 'warning', 3: 'default' }
const fundStatusOptions = [{ label: '正常', value: 1 }, { label: '冻结', value: 2 }, { label: '已关闭', value: 3 }]
const paymentStatusMap: Record<number, string> = { 1: '待缴', 2: '已缴', 3: '逾期' }
const paymentStatusColorMap: Record<number, string> = { 1: 'blue', 2: 'success', 3: 'error' }
const paymentCycleOptions = [{ label: '月', value: 1 }, { label: '季', value: 2 }, { label: '年', value: 3 }]

const fundColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '基金名称', dataIndex: 'FundName', width: 150 },
  { title: '基金编码', dataIndex: 'FundCode', width: 100 },
  { title: '业务类型', dataIndex: 'BusinessType', width: 90, align: 'center' as const },
  { title: '基金余额', dataIndex: 'FundBalance', width: 120, align: 'right' as const },
  { title: '累计缴入', dataIndex: 'TotalContributions', width: 120, align: 'right' as const },
  { title: '累计赔出', dataIndex: 'TotalPayouts', width: 120, align: 'right' as const },
  { title: '生效日期', dataIndex: 'EffectiveDate', width: 110 },
  { title: '状态', dataIndex: 'FundStatus', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

const contributionColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '缴费编号', dataIndex: 'ContributionNumber', width: 140 },
  { title: '基金名称', dataIndex: 'FundName', width: 120 },
  { title: '关联对象', dataIndex: 'RelatedObjectName', width: 120 },
  { title: '缴费金额', dataIndex: 'ContributionAmount', width: 110, align: 'right' as const },
  { title: '期间开始', dataIndex: 'PeriodStart', width: 110 },
  { title: '期间结束', dataIndex: 'PeriodEnd', width: 110 },
  { title: '缴费日期', dataIndex: 'PaymentDate', width: 110 },
  { title: '状态', dataIndex: 'PaymentStatus', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

const activeTab = ref('funds')
const searchForm = reactive({ keyword: '', businessType: undefined as number | undefined, fundStatus: undefined as number | undefined })
const loading = ref(false)
const tableData = ref<InsCoInsuranceFundListItemDto[]>([])
const fundCards = ref<InsCoInsuranceFundListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const contribLoading = ref(false)
const contributionData = ref<InsFundContributionListItemDto[]>([])
const contribPagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const contribPaginationConfig = computed(() => ({
  current: contribPagination.pageIndex, pageSize: contribPagination.pageSize, total: contribPagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))
const currentFundId = ref<number | null>(null)

const fundDialogVisible = ref(false)
const fundDialogType = ref<'add' | 'edit'>('add')
const fundFormRef = ref<FormInstance>()
const fundSubmitLoading = ref(false)
const editFundId = ref<number | null>(null)
const fundFormData = reactive({
  FundName: '', FundCode: '', BusinessType: 1, FundDescription: '',
  ContributionStandard: undefined as number | undefined,
  PaymentCycle: undefined as number | undefined,
  Deductible: 0,
  SinglePayoutLimit: undefined as number | undefined,
  AnnualPayoutLimit: undefined as number | undefined,
  EffectiveDate: '',
})
const fundFormRules: Record<string, Rule[]> = {
  FundName: [{ required: true, message: '请输入基金名称', trigger: 'blur' }],
  FundCode: [{ required: true, message: '请输入基金编码', trigger: 'blur' }],
  BusinessType: [{ required: true, message: '请选择业务类型', trigger: 'change' }],
  EffectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
}

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('zh-CN')
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.businessType != null) params.businessType = searchForm.businessType
    if (searchForm.fundStatus != null) params.fundStatus = searchForm.fundStatus
    const res = await getFundList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.total || 0
      fundCards.value = (res.items || []).slice(0, 4)
    }
  } finally { loading.value = false }
}

async function fetchContributions(fundId: number) {
  contribLoading.value = true
  try {
    const res = await getFundContributions(fundId, { pageIndex: contribPagination.pageIndex, pageSize: contribPagination.pageSize })
    if (res) { contributionData.value = res.items || []; contribPagination.total = res.total || 0 }
  } finally { contribLoading.value = false }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() { searchForm.keyword = ''; searchForm.businessType = undefined; searchForm.fundStatus = undefined; pagination.pageIndex = 1; fetchList() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }
function handleContribTableChange(pag: any) {
  contribPagination.pageIndex = pag.current; contribPagination.pageSize = pag.pageSize
  if (currentFundId.value) fetchContributions(currentFundId.value)
}

function handleAddFund() {
  fundDialogType.value = 'add'; editFundId.value = null
  fundFormData.FundName = ''; fundFormData.FundCode = ''; fundFormData.BusinessType = 1
  fundFormData.FundDescription = ''; fundFormData.ContributionStandard = undefined
  fundFormData.PaymentCycle = undefined; fundFormData.Deductible = 0
  fundFormData.SinglePayoutLimit = undefined; fundFormData.AnnualPayoutLimit = undefined
  fundFormData.EffectiveDate = ''
  fundDialogVisible.value = true
}

function handleEditFund(row: InsCoInsuranceFundListItemDto) {
  fundDialogType.value = 'edit'; editFundId.value = row.Id
  fundFormData.FundName = row.FundName; fundFormData.FundCode = row.FundCode
  fundFormData.BusinessType = row.BusinessType
  fundFormData.EffectiveDate = row.EffectiveDate ? row.EffectiveDate.substring(0, 10) : ''
  fundDialogVisible.value = true
}

async function handleFundSubmit() {
  if (!fundFormRef.value) return
  try { await fundFormRef.value.validate() } catch { return }
  fundSubmitLoading.value = true
  try {
    if (fundDialogType.value === 'add') { await createFund({ ...fundFormData }); message.success('新增成功') }
    else { await updateFund(editFundId.value!, { ...fundFormData }); message.success('更新成功') }
    fundDialogVisible.value = false; fetchList()
  } catch { message.error('提交失败') } finally { fundSubmitLoading.value = false }
}

function handleViewContributions(row: InsCoInsuranceFundListItemDto) {
  currentFundId.value = row.Id; contribPagination.pageIndex = 1; activeTab.value = 'contributions'
  fetchContributions(row.Id)
}

async function handleGenerate(row: InsCoInsuranceFundListItemDto) {
  try {
    const count = await generateContributions(row.Id)
    message.success(`已生成 ${count} 条缴费单`); fetchList()
  } catch { message.error('生成失败') }
}

async function handleConfirmPayment(row: InsFundContributionListItemDto) {
  try {
    await confirmContribution(row.Id)
    message.success('缴费确认成功')
    if (currentFundId.value) fetchContributions(currentFundId.value)
    fetchList()
  } catch { message.error('确认失败') }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.stat-card {
  text-align: center;
}
</style>
