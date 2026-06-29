<template>
  <div class="page-container">
    <PageHeader title="返利结算">
      <template #actions>
        <a-button type="primary" @click="executeDialogVisible = true">
          <template #icon><ThunderboltOutlined /></template>执行结算
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.brandCode" size="small" placeholder="品牌编码" allow-clear style="width: 120px" />
          <a-select v-model:value="searchForm.status" size="small" placeholder="状态" allow-clear style="width: 120px"
            :options="statusOptions" />
          <a-range-picker v-model:value="dateRange" size="small" style="width: 240px" />
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
        row-key="id"
        bordered
        :scroll="{ x: 1600 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'period'">
            {{ record.periodStart?.slice(0, 10) }} ~ {{ record.periodEnd?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'totalWeight'">
            {{ (record.totalWeight ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'baseRebateAmount'">
            ¥{{ (record.baseRebateAmount ?? 0).toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'finalRebateAmount'">
            <span style="font-weight: 600; color: var(--color-info)">¥{{ (record.finalRebateAmount ?? 0).toFixed(2) }}</span>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="getStatusColor(record.status)">{{ getStatusText(record.status) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewDetail(record as PolicyRebateSettlementListItemDto)">详情</a-button>
            <a-popconfirm v-if="record.status === 0" title="确定确认此结算记录？" @confirm="handleConfirm(record as PolicyRebateSettlementListItemDto)">
              <a-button type="link" size="small">确认</a-button>
            </a-popconfirm>
            <a-popconfirm v-if="record.status === 1" title="确定核销此结算记录？" @confirm="handleWriteOff(record as PolicyRebateSettlementListItemDto)">
              <a-button type="link" size="small">核销</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 执行结算弹窗 -->
    <a-modal v-model:open="executeDialogVisible" title="执行结算" width="500px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="返利方案ID" required>
          <a-input-number v-model:value="executeForm.policyRebateId" style="width: 100%" placeholder="请输入返利方案ID" />
        </a-form-item>
        <a-form-item label="结算周期" required>
          <a-range-picker v-model:value="executeForm.dateRange" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="executeDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="executeLoading" @click="handleExecute">执行</a-button>
      </template>
    </a-modal>

    <!-- 详情弹窗 -->
    <a-modal v-model:open="detailDialogVisible" title="结算详情" width="700px" :destroy-on-close="true" :footer="null">
      <template v-if="detailData">
        <a-descriptions :column="2" bordered size="small" style="margin-bottom: 16px">
          <a-descriptions-item label="结算周期">{{ detailData.periodStart?.slice(0, 10) }} ~ {{ detailData.periodEnd?.slice(0, 10) }}</a-descriptions-item>
          <a-descriptions-item label="品牌编码">{{ detailData.brandCode }}</a-descriptions-item>
          <a-descriptions-item label="运单数">{{ detailData.totalWaybills ?? 0 }}</a-descriptions-item>
          <a-descriptions-item label="总重量">{{ (detailData.totalWeight ?? 0).toFixed(2) }} kg</a-descriptions-item>
          <a-descriptions-item label="均重">{{ (detailData.avgWeight ?? 0).toFixed(2) }} kg</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="getStatusColor(detailData.status)">{{ getStatusText(detailData.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="基础返利">
            <span style="color: var(--color-info)">¥{{ (detailData.baseRebateAmount ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="奖励金额">
            <span style="color: var(--color-success)">+¥{{ (detailData.totalReward ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="处罚金额">
            <span style="color: var(--color-danger)">-¥{{ (detailData.totalPenalty ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="最终返利">
            <span style="font-weight: 600; font-size: 16px; color: var(--color-info)">¥{{ (detailData.finalRebateAmount ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
        </a-descriptions>
        <h4>调整明细</h4>
        <a-table :columns="detailColumns" :data-source="detailData.details" :pagination="false" size="small" bordered row-key="id">
          <template #bodyCell="{ column, record: detail }">
            <template v-if="column.dataIndex === 'adjustType'">
              <a-tag :color="detail.adjustType === 1 ? 'green' : 'red'">{{ detail.adjustType === 1 ? '奖励' : '处罚' }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'adjustAmount'">
              ¥{{ (detail.adjustAmount ?? 0).toFixed(2) }}
            </template>
          </template>
        </a-table>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import { ThunderboltOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getSettlementList,
  getSettlementDetail,
  executeSettlement,
  confirmSettlement,
  writeOffSettlement,
  type PolicyRebateSettlementListItemDto,
  type PolicyRebateSettlementDetailDto,
} from '@/api/express'

// 搜索
const searchForm = reactive({
  brandCode: undefined as string | undefined,
  status: undefined as number | undefined,
})
const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const statusOptions = [
  { label: '待确认', value: 0 },
  { label: '已确认', value: 1 },
  { label: '已核销', value: 2 },
]

function getStatusText(s: number) { return ['待确认', '已确认', '已核销'][s] ?? '未知' }
function getStatusColor(s: number) { return ['processing', 'success', 'default'][s] ?? 'default' }

// 表格
const loading = ref(false)
const tableData = ref<PolicyRebateSettlementListItemDto[]>([])
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
  { title: '结算周期', dataIndex: 'period', width: 200 },
  { title: '品牌编码', dataIndex: 'brandCode', width: 100, align: 'center' as const },
  { title: '运单数', dataIndex: 'totalWaybills', width: 90, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 110, align: 'right' as const },
  { title: '基础返利', dataIndex: 'baseRebateAmount', width: 110, align: 'right' as const },
  { title: '最终返利', dataIndex: 'finalRebateAmount', width: 120, align: 'right' as const },
  { title: '状态', dataIndex: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

const detailColumns = [
  { title: '规则ID', dataIndex: 'ruleId', width: 80 },
  { title: '实际值', dataIndex: 'actualValue', width: 100 },
  { title: '阈值', dataIndex: 'thresholdValue', width: 100 },
  { title: '调整类型', dataIndex: 'adjustType', width: 90, align: 'center' as const },
  { title: '调整金额', dataIndex: 'adjustAmount', width: 110, align: 'right' as const },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
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
      params.periodStartFrom = dateRange.value[0].format('YYYY-MM-DD')
      params.periodStartTo = dateRange.value[1].format('YYYY-MM-DD')
    }
    const res = await getSettlementList(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取结算列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }

function handleReset() {
  searchForm.brandCode = undefined
  searchForm.status = undefined
  dateRange.value = undefined
  handleSearch()
}

// 确认
async function handleConfirm(row: PolicyRebateSettlementListItemDto) {
  try {
    await confirmSettlement(row.id)
    message.success('确认成功')
    fetchList()
  } catch { /* handled */ }
}

// 核销
async function handleWriteOff(row: PolicyRebateSettlementListItemDto) {
  try {
    await writeOffSettlement(row.id)
    message.success('核销成功')
    fetchList()
  } catch { /* handled */ }
}

// 详情
const detailDialogVisible = ref(false)
const detailData = ref<PolicyRebateSettlementDetailDto | null>(null)

async function handleViewDetail(row: PolicyRebateSettlementListItemDto) {
  try {
    detailData.value = await getSettlementDetail(row.id)
    detailDialogVisible.value = true
  } catch {
    message.error('获取结算详情失败')
  }
}

// 执行结算
const executeDialogVisible = ref(false)
const executeLoading = ref(false)
const executeForm = reactive({
  policyRebateId: undefined as number | undefined,
  dateRange: undefined as [Dayjs, Dayjs] | undefined,
})

async function handleExecute() {
  if (!executeForm.policyRebateId || !executeForm.dateRange) {
    message.warning('请填写完整信息')
    return
  }
  executeLoading.value = true
  try {
    await executeSettlement({
      policyRebateId: executeForm.policyRebateId,
      periodStart: executeForm.dateRange[0].format('YYYY-MM-DD'),
      periodEnd: executeForm.dateRange[1].format('YYYY-MM-DD'),
    })
    message.success('结算执行成功')
    executeDialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    executeLoading.value = false
  }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
