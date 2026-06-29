<template>
  <div class="page-container">
    <PageHeader title="租赁收费" description="管理三轮车租赁费用标准和收费记录">
      <template #actions>
        <a-button type="primary" @click="handleAddStandard" v-if="activeTab === 'standard'">
          <template #icon><PlusOutlined /></template>新增标准
        </a-button>
        <a-button type="primary" @click="handleOpenGenerateModal" v-if="activeTab === 'charge'">
          <template #icon><ThunderboltOutlined /></template>批量生成账单
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <template v-if="activeTab === 'standard'">
            <a-input v-model:value="standardSearchForm.keyword" size="small" placeholder="标准名称" allow-clear style="width: 180px" @keyup.enter="handleStandardSearch" />
            <a-select v-model:value="standardSearchForm.status" size="small" placeholder="状态" allow-clear style="width: 100px" :options="[{ label: '启用', value: 1 }, { label: '停用', value: 0 }]" />
            <a-button type="primary" size="small" @click="handleStandardSearch">查询</a-button>
            <a-button size="small" @click="handleStandardReset">重置</a-button>
          </template>
          <template v-if="activeTab === 'charge'">
            <a-input v-model:value="chargeSearchForm.keyword" size="small" placeholder="员工姓名" allow-clear style="width: 160px" @keyup.enter="handleChargeSearch" />
            <a-select v-model:value="chargeSearchForm.chargeStatus" size="small" placeholder="收费状态" allow-clear style="width: 120px" :options="chargeStatusOptions" />
            <a-range-picker v-model:value="chargeSearchForm.dateRange" size="small" style="width: 240px" value-format="YYYY-MM-DD" :placeholder="['开始日期', '结束日期']" />
            <a-button type="primary" size="small" @click="handleChargeSearch">查询</a-button>
            <a-button size="small" @click="handleChargeReset">重置</a-button>
          </template>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab">
        <!-- Tab 1: 费用标准管理 -->
        <a-tab-pane key="standard" tab="费用标准">
          <a-table
            :columns="standardColumns"
            :data-source="standardTableData"
            :loading="standardLoading"
            :pagination="standardPaginationConfig"
            row-key="id"
            bordered
            :scroll="{ x: 1000 }"
            @change="handleStandardTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (standardPagination.pageIndex - 1) * standardPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'amount'">
                <span class="amount-cell">{{ formatAmount(record.amount) }}</span>
              </template>
              <template v-if="column.dataIndex === 'chargeCycle'">
                <a-tag :color="getChargeCycleColor(record.chargeCycle)">
                  {{ getChargeCycleLabel(record.chargeCycle) }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'effectiveDate'">
                {{ record.effectiveDate }}
              </template>
              <template v-if="column.dataIndex === 'expiryDate'">
                {{ record.expiryDate || '-' }}
              </template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="record.status === 1 ? 'success' : 'error'">
                  {{ record.status === 1 ? '启用' : '停用' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleEditStandard(record)">
                  <EditOutlined />编辑
                </a-button>
                <a-button type="link" size="small" @click="handleToggleStandardStatus(record)">
                  {{ record.status === 1 ? '停用' : '启用' }}
                </a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无费用标准数据" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- Tab 2: 收费记录管理 -->
        <a-tab-pane key="charge" tab="收费记录">
          <a-table
            :columns="chargeColumns"
            :data-source="chargeTableData"
            :loading="chargeLoading"
            :pagination="chargePaginationConfig"
            row-key="id"
            bordered
            :scroll="{ x: 1200 }"
            @change="handleChargeTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (chargePagination.pageIndex - 1) * chargePagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'chargePeriod'">
                {{ record.chargePeriodStart }} ~ {{ record.chargePeriodEnd }}
              </template>
              <template v-if="column.dataIndex === 'amountDue'">
                <span class="amount-cell">{{ formatAmount(record.amountDue) }}</span>
              </template>
              <template v-if="column.dataIndex === 'amountPaid'">
                <span class="amount-cell">{{ formatAmount(record.amountPaid || 0) }}</span>
              </template>
              <template v-if="column.dataIndex === 'chargeStatus'">
                <a-tag :color="getChargeStatusColor(record.chargeStatus)">
                  {{ getChargeStatusLabel(record.chargeStatus) }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'chargeDate'">
                {{ record.chargeDate || '-' }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <template v-if="record.chargeStatus === 1 || record.chargeStatus === 3">
                  <a-button type="link" size="small" @click="handleConfirmCharge(record)">
                    <CheckOutlined />确认收费
                  </a-button>
                  <a-button type="link" size="small" @click="handleWaiveCharge(record)">
                    <MinusCircleOutlined />减免
                  </a-button>
                </template>
                <span v-else class="text-gray">-</span>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无收费记录数据" />
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 新增/编辑标准弹窗 -->
    <a-modal
      v-model:open="standardDialogVisible"
      :title="standardDialogType === 'add' ? '新增费用标准' : '编辑费用标准'"
      width="600px"
      :destroy-on-close="true"
      @cancel="standardDialogVisible = false"
    >
      <a-form
        ref="standardFormRef"
        :model="standardFormData"
        :rules="standardFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="名称" name="name">
          <a-input
            v-model:value="standardFormData.name"
            placeholder="请输入标准名称"
            :maxlength="100"
          />
        </a-form-item>
        <a-form-item label="金额" name="amount">
          <a-input-number
            v-model:value="standardFormData.amount"
            placeholder="请输入金额"
            style="width: 100%"
            :min="0"
            :precision="2"
            prefix="¥"
          />
        </a-form-item>
        <a-form-item label="收费周期" name="chargeCycle">
          <a-select
            v-model:value="standardFormData.chargeCycle"
            placeholder="请选择收费周期"
            :options="chargeCycleOptions"
          />
        </a-form-item>
        <a-form-item label="生效日期" name="effectiveDate">
          <a-date-picker
            v-model:value="standardFormData.effectiveDate"
            placeholder="请选择生效日期"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="失效日期">
          <a-date-picker
            v-model:value="standardFormData.expiryDate"
            placeholder="请选择失效日期（可选）"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="standardFormData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="standardDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="standardSubmitLoading" @click="handleStandardSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 批量生成账单弹窗 -->
    <a-modal
      v-model:open="generateDialogVisible"
      title="批量生成账单"
      width="400px"
      :destroy-on-close="true"
      @cancel="generateDialogVisible = false"
    >
      <a-form
        ref="generateFormRef"
        :model="generateFormData"
        :rules="generateFormRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="年份" name="year">
          <a-select
            v-model:value="generateFormData.year"
            placeholder="请选择年份"
            :options="yearOptions"
          />
        </a-form-item>
        <a-form-item label="月份" name="month">
          <a-select
            v-model:value="generateFormData.month"
            placeholder="请选择月份"
            :options="monthOptions"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="generateDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="generateLoading" @click="handleGenerateCharges">确定生成</a-button>
      </template>
    </a-modal>

    <!-- 确认收费弹窗 -->
    <a-modal
      v-model:open="confirmDialogVisible"
      title="确认收费"
      width="500px"
      :destroy-on-close="true"
      @cancel="confirmDialogVisible = false"
    >
      <a-form
        ref="confirmFormRef"
        :model="confirmFormData"
        :rules="confirmFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="应收金额">
          <span class="amount-cell">{{ formatAmount(currentCharge?.amountDue || 0) }}</span>
        </a-form-item>
        <a-form-item label="实收金额" name="amountPaid">
          <a-input-number
            v-model:value="confirmFormData.amountPaid"
            placeholder="请输入实收金额"
            style="width: 100%"
            :min="0"
            :precision="2"
            prefix="¥"
          />
        </a-form-item>
        <a-form-item label="同步财务">
          <a-switch v-model:checked="confirmFormData.syncFinance" />
          <span class="ml-2 text-gray">开启后将同步生成财务凭证</span>
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="confirmFormData.remark"
            :rows="2"
            placeholder="请输入备注"
            :maxlength="500"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="confirmDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="confirmLoading" @click="handleConfirmSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 减免弹窗 -->
    <a-modal
      v-model:open="waiveDialogVisible"
      title="减免收费"
      width="450px"
      :destroy-on-close="true"
      @cancel="waiveDialogVisible = false"
    >
      <a-form
        ref="waiveFormRef"
        :model="waiveFormData"
        :rules="waiveFormRules"
        :label-col="{ style: { width: '80px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="应收金额">
          <span class="amount-cell">{{ formatAmount(currentCharge?.amountDue || 0) }}</span>
        </a-form-item>
        <a-form-item label="减免原因" name="remark">
          <a-textarea
            v-model:value="waiveFormData.remark"
            :rows="3"
            placeholder="请输入减免原因"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="waiveDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="waiveLoading" @click="handleWaiveSubmit">确定减免</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, CheckOutlined, MinusCircleOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getRentalStandardList,
  getRentalStandardDetail,
  createRentalStandard,
  updateRentalStandard,
  updateRentalStandardStatus,
  getRentalChargeList,
  generateRentalCharges,
  confirmRentalCharge,
  waiveRentalCharge,
  type RentalStandardListItemDto,
  type RentalChargeListItemDto,
} from '@/api/vehicle'

// 当前激活的Tab
const activeTab = ref('standard')

// ==================== 费用标准相关 ====================

// 收费周期选项
const chargeCycleOptions = [
  { label: '月', value: 1 },
  { label: '季', value: 2 },
  { label: '年', value: 3 },
]

// 收费状态选项
const chargeStatusOptions = [
  { label: '全部', value: 0 },
  { label: '待收', value: 1 },
  { label: '已收', value: 2 },
  { label: '逾期', value: 3 },
  { label: '减免', value: 4 },
]

// 年份选项
const currentYear = new Date().getFullYear()
const yearOptions = Array.from({ length: 5 }, (_, i) => ({
  label: `${currentYear - 2 + i}年`,
  value: currentYear - 2 + i,
}))

// 月份选项
const monthOptions = Array.from({ length: 12 }, (_, i) => ({
  label: `${i + 1}月`,
  value: i + 1,
}))

// 标准表格列配置
const standardColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '名称', dataIndex: 'name', key: 'name', width: 150, ellipsis: true },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 120, align: 'right' as const },
  { title: '收费周期', dataIndex: 'chargeCycle', key: 'chargeCycle', width: 100, align: 'center' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', key: 'effectiveDate', width: 110 },
  { title: '失效日期', dataIndex: 'expiryDate', key: 'expiryDate', width: 110 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const, fixed: 'right' as const },
]

// 标准搜索表单
const standardSearchForm = reactive({
  keyword: '',
  status: '' as number | '',
})

// 标准表格数据
const standardLoading = ref(false)
const standardTableData = ref<RentalStandardListItemDto[]>([])
const standardPagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const standardPaginationConfig = computed(() => ({
  current: standardPagination.pageIndex,
  pageSize: standardPagination.pageSize,
  total: standardPagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleStandardTableChange(pag: any) {
  standardPagination.pageIndex = pag.current
  standardPagination.pageSize = pag.pageSize
  fetchStandardList()
}

// 标准弹窗相关
const standardDialogVisible = ref(false)
const standardDialogType = ref<'add' | 'edit'>('add')
const standardFormRef = ref<FormInstance>()
const standardSubmitLoading = ref(false)
const currentStandardId = ref<number | null>(null)

const standardFormData = reactive({
  name: '',
  amount: undefined as number | undefined,
  chargeCycle: undefined as number | undefined,
  effectiveDate: '',
  expiryDate: '',
  remark: '',
})

const standardFormRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入标准名称', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入金额', trigger: 'blur' }],
  chargeCycle: [{ required: true, message: '请选择收费周期', trigger: 'change' }],
  effectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
}

// 获取标准列表
async function fetchStandardList() {
  standardLoading.value = true
  try {
    const params: any = {
      pageIndex: standardPagination.pageIndex,
      pageSize: standardPagination.pageSize,
    }
    if (standardSearchForm.keyword) params.keyword = standardSearchForm.keyword
    if (standardSearchForm.status !== '' && standardSearchForm.status !== undefined) {
      params.status = standardSearchForm.status
    }
    const res = await getRentalStandardList(params)
    if (res) {
      standardTableData.value = res.items || []
      standardPagination.total = res.totalCount || 0
    }
  } finally {
    standardLoading.value = false
  }
}

// 搜索
function handleStandardSearch() {
  standardPagination.pageIndex = 1
  fetchStandardList()
}

// 重置搜索
function handleStandardReset() {
  standardSearchForm.keyword = ''
  standardSearchForm.status = ''
  standardPagination.pageIndex = 1
  fetchStandardList()
}

// 重置标准表单
function resetStandardForm() {
  standardFormData.name = ''
  standardFormData.amount = undefined
  standardFormData.chargeCycle = undefined
  standardFormData.effectiveDate = ''
  standardFormData.expiryDate = ''
  standardFormData.remark = ''
  currentStandardId.value = null
}

// 新增标准
function handleAddStandard() {
  standardDialogType.value = 'add'
  resetStandardForm()
  standardDialogVisible.value = true
}

// 编辑标准
async function handleEditStandard(row: any) {
  standardDialogType.value = 'edit'
  currentStandardId.value = row.id
  resetStandardForm()
  standardDialogVisible.value = true

  try {
    const detail = await getRentalStandardDetail(row.id)
    if (detail) {
      standardFormData.name = detail.name
      standardFormData.amount = detail.amount
      standardFormData.chargeCycle = detail.chargeCycle
      standardFormData.effectiveDate = detail.effectiveDate
      standardFormData.expiryDate = detail.expiryDate || ''
      standardFormData.remark = detail.remark || ''
    }
  } catch (error) {
    console.error('获取标准详情失败:', error)
    message.error('获取标准详情失败')
  }
}

// 提交标准表单
async function handleStandardSubmit() {
  if (!standardFormRef.value) return
  try {
    await standardFormRef.value.validate()
  } catch {
    return
  }

  standardSubmitLoading.value = true
  try {
    const data = {
      name: standardFormData.name,
      amount: standardFormData.amount!,
      chargeCycle: standardFormData.chargeCycle!,
      effectiveDate: standardFormData.effectiveDate,
      expiryDate: standardFormData.expiryDate || undefined,
      remark: standardFormData.remark || undefined,
    }

    if (standardDialogType.value === 'add') {
      await createRentalStandard(data)
      message.success('新增成功')
    } else {
      await updateRentalStandard(currentStandardId.value!, data)
      message.success('更新成功')
    }
    standardDialogVisible.value = false
    fetchStandardList()
  } catch (error) {
    console.error('提交失败:', error)
    message.error('提交失败')
  } finally {
    standardSubmitLoading.value = false
  }
}

// 启用/停用标准
async function handleToggleStandardStatus(row: any) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateRentalStandardStatus(row.id, newStatus)
    message.success(`${actionText}成功`)
    fetchStandardList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
    message.error(`${actionText}失败`)
  }
}

// ==================== 收费记录相关 ====================

// 收费表格列配置
const chargeColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '车辆编码', dataIndex: 'vehicleCode', key: 'vehicleCode', width: 100 },
  { title: '员工姓名', dataIndex: 'employeeName', key: 'employeeName', width: 100 },
  { title: '收费周期', dataIndex: 'chargePeriod', key: 'chargePeriod', width: 200 },
  { title: '应收金额', dataIndex: 'amountDue', key: 'amountDue', width: 120, align: 'right' as const },
  { title: '实收金额', dataIndex: 'amountPaid', key: 'amountPaid', width: 120, align: 'right' as const },
  { title: '收费状态', dataIndex: 'chargeStatus', key: 'chargeStatus', width: 90, align: 'center' as const },
  { title: '收费日期', dataIndex: 'chargeDate', key: 'chargeDate', width: 110 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

// 收费搜索表单
const chargeSearchForm = reactive({
  keyword: '',
  chargeStatus: undefined as number | undefined,
  dateRange: undefined as [string, string] | undefined,
})

// 收费表格数据
const chargeLoading = ref(false)
const chargeTableData = ref<RentalChargeListItemDto[]>([])
const chargePagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const chargePaginationConfig = computed(() => ({
  current: chargePagination.pageIndex,
  pageSize: chargePagination.pageSize,
  total: chargePagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleChargeTableChange(pag: any) {
  chargePagination.pageIndex = pag.current
  chargePagination.pageSize = pag.pageSize
  fetchChargeList()
}

// 获取收费列表
async function fetchChargeList() {
  chargeLoading.value = true
  try {
    const params: any = {
      pageIndex: chargePagination.pageIndex,
      pageSize: chargePagination.pageSize,
    }
    if (chargeSearchForm.keyword) params.keyword = chargeSearchForm.keyword
    if (chargeSearchForm.chargeStatus && chargeSearchForm.chargeStatus !== 0) {
      params.chargeStatus = chargeSearchForm.chargeStatus
    }
    if (chargeSearchForm.dateRange && chargeSearchForm.dateRange.length === 2) {
      params.startDate = chargeSearchForm.dateRange[0]
      params.endDate = chargeSearchForm.dateRange[1]
    }
    const res = await getRentalChargeList(params)
    if (res) {
      chargeTableData.value = res.items || []
      chargePagination.total = res.totalCount || 0
    }
  } finally {
    chargeLoading.value = false
  }
}

// 搜索
function handleChargeSearch() {
  chargePagination.pageIndex = 1
  fetchChargeList()
}

// 重置搜索
function handleChargeReset() {
  chargeSearchForm.keyword = ''
  chargeSearchForm.chargeStatus = undefined
  chargeSearchForm.dateRange = undefined
  chargePagination.pageIndex = 1
  fetchChargeList()
}

// ==================== 批量生成账单 ====================

const generateDialogVisible = ref(false)
const generateFormRef = ref<FormInstance>()
const generateLoading = ref(false)

const generateFormData = reactive({
  year: currentYear,
  month: new Date().getMonth() + 1,
})

const generateFormRules: Record<string, Rule[]> = {
  year: [{ required: true, message: '请选择年份', trigger: 'change' }],
  month: [{ required: true, message: '请选择月份', trigger: 'change' }],
}

function handleOpenGenerateModal() {
  generateFormData.year = currentYear
  generateFormData.month = new Date().getMonth() + 1
  generateDialogVisible.value = true
}

async function handleGenerateCharges() {
  if (!generateFormRef.value) return
  try {
    await generateFormRef.value.validate()
  } catch {
    return
  }

  generateLoading.value = true
  try {
    const count = await generateRentalCharges({
      year: generateFormData.year,
      month: generateFormData.month,
    })
    message.success(`成功生成 ${count} 条账单`)
    generateDialogVisible.value = false
    fetchChargeList()
  } catch (error) {
    console.error('生成账单失败:', error)
    message.error('生成账单失败')
  } finally {
    generateLoading.value = false
  }
}

// ==================== 确认收费 ====================

const confirmDialogVisible = ref(false)
const confirmFormRef = ref<FormInstance>()
const confirmLoading = ref(false)
const currentCharge = ref<RentalChargeListItemDto | null>(null)

const confirmFormData = reactive({
  amountPaid: undefined as number | undefined,
  syncFinance: false,
  remark: '',
})

const confirmFormRules: Record<string, Rule[]> = {
  amountPaid: [{ required: true, message: '请输入实收金额', trigger: 'blur' }],
}

function handleConfirmCharge(row: any) {
  currentCharge.value = row
  confirmFormData.amountPaid = row.amountDue
  confirmFormData.syncFinance = false
  confirmFormData.remark = ''
  confirmDialogVisible.value = true
}

async function handleConfirmSubmit() {
  if (!confirmFormRef.value || !currentCharge.value) return
  try {
    await confirmFormRef.value.validate()
  } catch {
    return
  }

  confirmLoading.value = true
  try {
    await confirmRentalCharge(currentCharge.value.id, {
      amountPaid: confirmFormData.amountPaid,
      syncFinance: confirmFormData.syncFinance,
      remark: confirmFormData.remark || undefined,
    })
    message.success('确认收费成功')
    confirmDialogVisible.value = false
    fetchChargeList()
  } catch (error) {
    console.error('确认收费失败:', error)
    message.error('确认收费失败')
  } finally {
    confirmLoading.value = false
  }
}

// ==================== 减免收费 ====================

const waiveDialogVisible = ref(false)
const waiveFormRef = ref<FormInstance>()
const waiveLoading = ref(false)

const waiveFormData = reactive({
  remark: '',
})

const waiveFormRules: Record<string, Rule[]> = {
  remark: [{ required: true, message: '请输入减免原因', trigger: 'blur' }],
}

function handleWaiveCharge(row: any) {
  currentCharge.value = row
  waiveFormData.remark = ''
  waiveDialogVisible.value = true
}

async function handleWaiveSubmit() {
  if (!waiveFormRef.value || !currentCharge.value) return
  try {
    await waiveFormRef.value.validate()
  } catch {
    return
  }

  waiveLoading.value = true
  try {
    await waiveRentalCharge(currentCharge.value.id, {
      remark: waiveFormData.remark,
    })
    message.success('减免成功')
    waiveDialogVisible.value = false
    fetchChargeList()
  } catch (error) {
    console.error('减免失败:', error)
    message.error('减免失败')
  } finally {
    waiveLoading.value = false
  }
}

// ==================== 工具函数 ====================

// 格式化金额
function formatAmount(amount: number) {
  return `¥${(amount || 0).toFixed(2)}`
}

// 获取收费周期标签
function getChargeCycleLabel(cycle: number) {
  const map: Record<number, string> = {
    1: '月',
    2: '季',
    3: '年',
  }
  return map[cycle] || '未知'
}

// 获取收费周期颜色
function getChargeCycleColor(cycle: number) {
  const map: Record<number, string> = {
    1: 'blue',
    2: 'green',
    3: 'orange',
  }
  return map[cycle] || 'default'
}

// 获取收费状态标签
function getChargeStatusLabel(status: number) {
  const map: Record<number, string> = {
    1: '待收',
    2: '已收',
    3: '逾期',
    4: '减免',
  }
  return map[status] || '未知'
}

// 获取收费状态颜色
function getChargeStatusColor(status: number) {
  const map: Record<number, string> = {
    1: 'blue',
    2: 'green',
    3: 'red',
    4: 'default',
  }
  return map[status] || 'default'
}

onMounted(() => {
  fetchStandardList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.amount-cell {
  font-family: monospace;
  font-weight: 500;
}

.text-gray {
  color: #999;
}

.ml-2 {
  margin-left: 8px;
}
</style>
