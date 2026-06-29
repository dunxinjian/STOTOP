<template>
  <div class="treasury-forecast-page page-container">
    <PageHeader title="13周资金预测" description="滚动查看未来13周现金流入、流出和安全现金缺口">
      <template #left>
        <AccountSetSelector style="width: 220px" />
        <a-date-picker v-model:value="query.startDate" value-format="YYYY-MM-DD" size="small" style="width: 140px" @change="loadData" />
        <a-input-number v-model:value="query.orgId" :min="0" size="small" placeholder="组织ID" style="width: 110px" @change="loadData" />
        <a-input-number v-model:value="query.safetyCash" :precision="2" size="small" placeholder="安全现金" style="width: 130px" @change="loadData" />
      </template>
      <template #actions>
        <a-button @click="openLineDialog">
          <template #icon><PlusOutlined /></template>
          新增计划
        </a-button>
        <a-button type="primary" @click="loadData">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
      </template>
    </PageHeader>

    <div class="forecast-metrics">
      <div class="metric-cell">
        <span>期初现金</span>
        <strong>{{ formatMoney(forecast.openingCash) }}</strong>
      </div>
      <div class="metric-cell">
        <span>13周期末</span>
        <strong>{{ formatMoney(lastEndingCash) }}</strong>
      </div>
      <div class="metric-cell">
        <span>最低周余额</span>
        <strong :class="{ danger: minEndingCash < Number(query.safetyCash || 0) }">{{ formatMoney(minEndingCash) }}</strong>
      </div>
      <div class="metric-cell">
        <span>低于安全现金</span>
        <strong>{{ riskWeeks.length }} 周</strong>
      </div>
    </div>

    <a-card :bordered="false" class="forecast-card">
      <a-table
        :columns="weekColumns"
        :data-source="forecast.weeks"
        :loading="loading"
        row-key="weekStartDate"
        bordered
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'weekStartDate'">
            {{ formatDate(record.weekStartDate) }}
          </template>
          <template v-if="['openingCash', 'inflow', 'outflow', 'endingCash'].includes(String(column.dataIndex))">
            <span :class="moneyClass(column.dataIndex as MoneyField, record)">{{ formatMoney(record[column.dataIndex as MoneyField]) }}</span>
          </template>
          <template v-if="column.dataIndex === 'belowSafetyCash'">
            <a-tag :color="record.belowSafetyCash ? 'error' : 'success'">
              {{ record.belowSafetyCash ? '预警' : '正常' }}
            </a-tag>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card :bordered="false" class="plan-card">
      <template #title>手工资金计划</template>
      <a-table
        :columns="planColumns"
        :data-source="planLines"
        :loading="lineLoading"
        row-key="id"
        size="small"
        :pagination="false"
        :scroll="{ x: 1000 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'planDate'">
            {{ formatDate(record.planDate) }}
          </template>
          <template v-if="column.dataIndex === 'direction'">
            <a-tag :color="record.direction === 'inflow' ? 'success' : 'warning'">
              {{ record.direction === 'inflow' ? '流入' : '流出' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'amount'">
            {{ formatMoney(record.amount) }}
          </template>
          <template v-if="column.dataIndex === 'probability'">
            {{ Number(record.probability || 0).toFixed(0) }}%
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" danger @click="deleteLine(record.id)">
              <DeleteOutlined />删除
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="lineVisible" title="新增资金计划" :width="560" :destroy-on-close="true">
      <a-form :model="lineForm" :label-col="{ style: { width: '96px' } }">
        <a-form-item label="计划日期">
          <a-date-picker v-model:value="lineForm.planDate" value-format="YYYY-MM-DD" style="width: 100%" />
        </a-form-item>
        <a-form-item label="方向">
          <a-segmented v-model:value="lineForm.direction" :options="directionOptions" />
        </a-form-item>
        <a-form-item label="资金分类">
          <a-select v-model:value="lineForm.cashCategory" :options="cashCategoryOptions" />
        </a-form-item>
        <a-form-item label="金额">
          <a-input-number v-model:value="lineForm.amount" :min="0" :precision="2" style="width: 100%" />
        </a-form-item>
        <a-form-item label="发生概率">
          <a-input-number v-model:value="lineForm.probability" :min="0" :max="100" :precision="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="往来方">
          <a-input v-model:value="lineForm.counterpartyName" placeholder="客户、供应商或员工" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="lineForm.remark" :rows="2" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="lineVisible = false">取消</a-button>
        <a-button type="primary" :loading="lineSaving" @click="saveLine">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DeleteOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  deleteTreasuryPlanLine,
  getRolling13WeekTreasuryPlan,
  getTreasuryPlanLines,
  saveTreasuryPlanLine,
  type Rolling13WeekTreasuryDto,
  type TreasuryPlanLineDto,
  type TreasuryWeekDto,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()

// 周预测中可格式化为金额的数值列（与模板 v-if 的列保持一致），均为 number 类型
type MoneyField = Extract<keyof TreasuryWeekDto, 'openingCash' | 'inflow' | 'outflow' | 'endingCash'>

const loading = ref(false)
const lineLoading = ref(false)
const lineSaving = ref(false)
const lineVisible = ref(false)
const forecast = ref<Rolling13WeekTreasuryDto>({ openingCash: 0, safetyCash: 0, weeks: [] })
const planLines = ref<TreasuryPlanLineDto[]>([])

const query = reactive({
  startDate: today(),
  orgId: (orgContextStore.currentOrgId || undefined) as number | undefined,
  safetyCash: 0,
})

const lineForm = reactive({
  planDate: today(),
  direction: 'outflow' as 'inflow' | 'outflow',
  cashCategory: 'other',
  amount: 0,
  probability: 100,
  counterpartyName: '',
  remark: '',
})

const directionOptions = [
  { label: '流入', value: 'inflow' },
  { label: '流出', value: 'outflow' },
]

const cashCategoryOptions = [
  { label: '销售回款', value: 'sales_collection' },
  { label: '费用报销', value: 'expense_reimbursement' },
  { label: '采购付款', value: 'purchase_payment' },
  { label: '工资社保', value: 'payroll' },
  { label: '税费', value: 'tax' },
  { label: '其他', value: 'other' },
]

const weekColumns = [
  { title: '周起始日', dataIndex: 'weekStartDate', key: 'weekStartDate', width: 130 },
  { title: '期初现金', dataIndex: 'openingCash', key: 'openingCash', align: 'right' as const },
  { title: '预计流入', dataIndex: 'inflow', key: 'inflow', align: 'right' as const },
  { title: '预计流出', dataIndex: 'outflow', key: 'outflow', align: 'right' as const },
  { title: '期末现金', dataIndex: 'endingCash', key: 'endingCash', align: 'right' as const },
  { title: '安全线', dataIndex: 'belowSafetyCash', key: 'belowSafetyCash', width: 110, align: 'center' as const },
]

const planColumns = [
  { title: '日期', dataIndex: 'planDate', key: 'planDate', width: 120 },
  { title: '方向', dataIndex: 'direction', key: 'direction', width: 90, align: 'center' as const },
  { title: '资金分类', dataIndex: 'cashCategory', key: 'cashCategory', width: 150 },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 130, align: 'right' as const },
  { title: '概率', dataIndex: 'probability', key: 'probability', width: 90, align: 'right' as const },
  { title: '往来方', dataIndex: 'counterpartyName', key: 'counterpartyName', width: 160 },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 220 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 90, align: 'center' as const, fixed: 'right' as const },
]

const lastEndingCash = computed(() => {
  const weeks = forecast.value.weeks || []
  return weeks.length ? weeks[weeks.length - 1].endingCash : 0
})

const minEndingCash = computed(() => {
  const values = (forecast.value.weeks || []).map((week) => week.endingCash)
  return values.length ? Math.min(...values) : 0
})

const riskWeeks = computed(() => (forecast.value.weeks || []).filter((week) => week.belowSafetyCash))

watch(
  () => accountSetStore.currentAccountSetId,
  () => loadData(),
)

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  await loadData()
})

async function loadData() {
  await Promise.all([loadForecast(), loadPlanLines()])
}

async function loadForecast() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) return

  loading.value = true
  try {
    forecast.value = await getRolling13WeekTreasuryPlan({
      accountSetId,
      startDate: query.startDate,
      orgId: query.orgId,
      safetyCash: Number(query.safetyCash || 0),
    })
  } catch {
    message.error('加载13周资金预测失败')
  } finally {
    loading.value = false
  }
}

async function loadPlanLines() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) return

  lineLoading.value = true
  try {
    planLines.value = await getTreasuryPlanLines({
      accountSetId,
      startDate: query.startDate,
      endDate: addDays(query.startDate, 90),
      orgId: query.orgId,
    })
  } catch {
    planLines.value = []
  } finally {
    lineLoading.value = false
  }
}

function openLineDialog() {
  Object.assign(lineForm, {
    planDate: query.startDate || today(),
    direction: 'outflow',
    cashCategory: 'other',
    amount: 0,
    probability: 100,
    counterpartyName: '',
    remark: '',
  })
  lineVisible.value = true
}

async function saveLine() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }
  if (!lineForm.planDate || Number(lineForm.amount || 0) <= 0) {
    message.warning('请填写计划日期和金额')
    return
  }

  lineSaving.value = true
  try {
    await saveTreasuryPlanLine({
      accountSetId,
      orgId: query.orgId || orgContextStore.currentOrgId || null,
      planDate: lineForm.planDate,
      weekStartDate: weekStart(lineForm.planDate),
      direction: lineForm.direction,
      cashCategory: lineForm.cashCategory,
      amount: Number(lineForm.amount || 0),
      probability: Number(lineForm.probability || 100),
      sourceType: 'manual',
      counterpartyName: lineForm.counterpartyName || null,
      remark: lineForm.remark || null,
    })
    message.success('资金计划已保存')
    lineVisible.value = false
    await loadData()
  } catch {
    message.error('保存资金计划失败')
  } finally {
    lineSaving.value = false
  }
}

async function deleteLine(id?: number) {
  if (!id) return
  await deleteTreasuryPlanLine(id)
  message.success('资金计划已删除')
  await loadData()
}

function moneyClass(field: string | number | symbol, record: any) {
  if (field === 'endingCash' && record.belowSafetyCash) return 'money danger'
  if (field === 'inflow') return 'money income'
  if (field === 'outflow') return 'money outcome'
  return 'money'
}

function formatMoney(value: number | string | undefined) {
  return `¥${Number(value || 0).toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

function formatDate(value: string) {
  return value ? String(value).slice(0, 10) : '-'
}

function today() {
  return new Date().toISOString().slice(0, 10)
}

function addDays(value: string, days: number) {
  const date = new Date(value)
  date.setDate(date.getDate() + days)
  return date.toISOString().slice(0, 10)
}

function weekStart(value: string) {
  const date = new Date(value)
  const day = date.getDay() || 7
  date.setDate(date.getDate() - day + 1)
  return date.toISOString().slice(0, 10)
}
</script>

<style scoped>
.forecast-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 12px;
}

.metric-cell {
  min-height: 72px;
  padding: 14px 16px;
  border: 1px solid #e7edf3;
  border-radius: 8px;
  background: #fff;
}

.metric-cell span {
  display: block;
  color: #64748b;
  font-size: 12px;
}

.metric-cell strong {
  display: block;
  margin-top: 8px;
  color: #1f2937;
  font-size: 21px;
  line-height: 1.1;
  overflow-wrap: anywhere;
}

.metric-cell strong.danger,
.money.danger {
  color: var(--color-danger-text);
}

.money {
  font-variant-numeric: tabular-nums;
}

.money.income {
  color: var(--color-success-text);
}

.money.outcome {
  color: var(--color-warning-text);
}

.plan-card {
  margin-top: 12px;
}
</style>
