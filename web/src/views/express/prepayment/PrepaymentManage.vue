<template>
  <div class="page-container">
    <PageHeader title="预付款管理">
      <template #actions>
        <a-button type="primary" @click="rechargeDialogVisible = true">
          <template #icon><PlusOutlined /></template>充值
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input-number v-model:value="searchForm.clientId" size="small" placeholder="客户ID" style="width: 120px" />
          <a-select v-model:value="searchForm.transactionType" size="small" placeholder="类型" allow-clear style="width: 120px"
            :options="transTypeOptions" />
          <a-range-picker v-model:value="dateRange" size="small" style="width: 240px" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 余额信息 -->
    <a-card :bordered="false" style="margin-bottom: 12px">
      <a-row :gutter="16" align="middle">
        <a-col :span="6">
          <a-form layout="inline">
            <a-form-item label="客户ID">
              <a-input-number v-model:value="balanceClientId" placeholder="输入客户ID" style="width: 140px" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" @click="fetchBalance" :loading="balanceLoading">查询余额</a-button>
            </a-form-item>
          </a-form>
        </a-col>
        <a-col :span="18">
          <a-row v-if="balanceInfo" :gutter="24">
            <a-col :span="8">
              <a-statistic title="当前余额" :value="balanceInfo.balance" :precision="2" prefix="¥"
                :value-style="{ color: balanceInfo.balance > 0 ? 'var(--color-success)' : 'var(--color-danger)' }" />
            </a-col>
            <a-col :span="8">
              <a-statistic title="累计充值" :value="balanceInfo.totalRecharge" :precision="2" prefix="¥"
                :value-style="{ color: 'var(--color-info)' }" />
            </a-col>
            <a-col :span="8">
              <a-statistic title="累计消费" :value="balanceInfo.totalConsume" :precision="2" prefix="¥" />
            </a-col>
          </a-row>
        </a-col>
      </a-row>
    </a-card>

    <!-- 流水查询 -->
    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'transactionType'">
            <a-tag :color="record.transactionType === 1 ? 'green' : 'orange'">
              {{ record.transactionType === 1 ? '充值' : '核销' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'amount'">
            <span :style="{ color: record.transactionType === 1 ? 'var(--color-success)' : 'var(--color-danger)' }">
              {{ record.transactionType === 1 ? '+' : '-' }}¥{{ record.amount.toFixed(2) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'balanceAfter'">
            ¥{{ (record.balanceAfter ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'invoiceId'">
            <a-button v-if="record.invoiceId" type="link" size="small" @click="router.push({ name: 'ExpressInvoiceDetail', params: { id: record.invoiceId } })">
              #{{ record.invoiceId }}
            </a-button>
            <span v-else>-</span>
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 19)?.replace('T', ' ') }}
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 充值弹窗 -->
    <a-modal v-model:open="rechargeDialogVisible" title="预付款充值" width="500px" :destroy-on-close="true" @cancel="rechargeDialogVisible = false">
      <a-form ref="rechargeFormRef" :model="rechargeForm" :rules="rechargeRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="客户ID" name="clientId">
          <a-input-number v-model:value="rechargeForm.clientId" style="width: 100%" placeholder="请输入客户ID" />
        </a-form-item>
        <a-form-item label="充值金额" name="amount">
          <a-input-number v-model:value="rechargeForm.amount" :min="0.01" :precision="2" style="width: 100%" placeholder="请输入充值金额" prefix="¥" />
        </a-form-item>
        <a-form-item label="付款日期">
          <a-date-picker v-model:value="rechargeForm.paymentDate" style="width: 100%" />
        </a-form-item>
        <a-form-item label="付款方式">
          <a-select v-model:value="rechargeForm.paymentMethod" placeholder="请选择" allow-clear
            :options="[{ label: '银行转账', value: '银行转账' }, { label: '现金', value: '现金' }, { label: '支票', value: '支票' }]" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="rechargeForm.remark" :rows="2" placeholder="请输入备注" :maxlength="200" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="rechargeDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="rechargeLoading" @click="handleRecharge">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { Dayjs } from 'dayjs'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPrepaymentBalance,
  getPrepaymentTransactions,
  recharge,
  type PrepaymentBalanceDto,
  type PrepaymentTransactionDto,
} from '@/api/express'

const router = useRouter()

// 余额查询
const balanceClientId = ref<number | undefined>(undefined)
const balanceLoading = ref(false)
const balanceInfo = ref<PrepaymentBalanceDto | null>(null)

async function fetchBalance() {
  if (!balanceClientId.value) {
    message.warning('请输入客户ID')
    return
  }
  balanceLoading.value = true
  try {
    balanceInfo.value = await getPrepaymentBalance(String(balanceClientId.value))
  } catch {
    message.error('查询余额失败')
  } finally {
    balanceLoading.value = false
  }
}

// 流水查询
const searchForm = reactive({
  clientId: undefined as number | undefined,
  transactionType: undefined as number | undefined,
})
const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const transTypeOptions = [
  { label: '充值', value: 1 },
  { label: '核销', value: 2 },
]

const loading = ref(false)
const tableData = ref<PrepaymentTransactionDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '类型', dataIndex: 'transactionType', width: 80, align: 'center' as const },
  { title: '金额', dataIndex: 'amount', width: 120, align: 'right' as const },
  { title: '操作后余额', dataIndex: 'balanceAfter', width: 120, align: 'right' as const },
  { title: '关联账单', dataIndex: 'invoiceId', width: 100, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '时间', dataIndex: 'createdTime', width: 170 },
]

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      ...searchForm,
    }
    if (dateRange.value) {
      params.startDate = dateRange.value[0].format('YYYY-MM-DD')
      params.endDate = dateRange.value[1].format('YYYY-MM-DD')
    }
    const res = await getPrepaymentTransactions(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取流水列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.clientId = undefined
  searchForm.transactionType = undefined
  dateRange.value = undefined
  handleSearch()
}

// 充值弹窗
const rechargeDialogVisible = ref(false)
const rechargeFormRef = ref<FormInstance>()
const rechargeLoading = ref(false)
const rechargeForm = reactive({
  clientId: undefined as number | undefined,
  amount: undefined as number | undefined,
  paymentDate: undefined as Dayjs | undefined,
  paymentMethod: undefined as string | undefined,
  remark: '',
})

const rechargeRules: Record<string, Rule[]> = {
  clientId: [{ required: true, message: '请输入客户ID', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入充值金额', trigger: 'blur' }],
}

async function handleRecharge() {
  if (!rechargeFormRef.value) return
  try { await rechargeFormRef.value.validate() } catch { return }

  rechargeLoading.value = true
  try {
    await recharge({
      clientId: String(rechargeForm.clientId),
      amount: rechargeForm.amount!,
      paymentDate: rechargeForm.paymentDate?.format('YYYY-MM-DD'),
      paymentMethod: rechargeForm.paymentMethod,
      remark: rechargeForm.remark || undefined,
    })
    message.success('充值成功')
    rechargeDialogVisible.value = false
    // 刷新余额和流水
    if (balanceClientId.value) fetchBalance()
    fetchList()
  } catch { /* handled */ } finally {
    rechargeLoading.value = false
  }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
