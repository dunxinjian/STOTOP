<template>
  <div class="page-container">
    <PageHeader title="账单详情">
      <template #left>
        <a-button type="link" @click="router.push({ name: 'ExpressInvoiceList' })" style="padding: 0">
          <template #icon><ArrowLeftOutlined /></template>返回
        </a-button>
      </template>
      <template #actions>
        <a-button v-if="detail?.reviewStatus === 0" type="primary" @click="openReviewDialog(true)">审核</a-button>
        <a-button v-if="detail?.reviewStatus === 1" danger @click="openReviewDialog(false)">反审核</a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 基本信息 -->
      <a-card title="基本信息" :bordered="false" style="margin-bottom: 12px">
        <a-descriptions :column="3" bordered size="small">
          <a-descriptions-item label="账单号">{{ detail?.invoiceNo ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="客户">
            <a
              v-if="detail?.clientName"
              class="cross-module-link"
              @click="router.push({ path: '/crm/customers', query: { keyword: detail.clientName } })"
            >{{ detail.clientName }}</a>
            <template v-else>-</template>
          </a-descriptions-item>
          <a-descriptions-item label="品牌">{{ detail?.brandName ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="账期起始">{{ detail?.periodStart?.slice(0, 10) ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="账期结束">{{ detail?.periodEnd?.slice(0, 10) ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ detail?.createdTime?.slice(0, 19)?.replace('T', ' ') ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="审核状态">
            <a-tag v-if="detail" :color="getReviewStatusColor(detail.reviewStatus)">
              {{ getReviewStatusText(detail.reviewStatus) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="业务状态">
            <a-tag v-if="detail" :color="getStatusColor(detail.status)">
              {{ getStatusText(detail.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="审核人">{{ detail?.reviewer ?? '-' }}</a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 金额汇总 -->
      <a-card title="金额汇总" :bordered="false" style="margin-bottom: 12px">
        <a-descriptions :column="3" bordered size="small">
          <a-descriptions-item label="运单数">{{ detail?.totalWaybills ?? 0 }}</a-descriptions-item>
          <a-descriptions-item label="总重量(kg)">{{ (detail?.totalWeight ?? 0).toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="均重(kg)">{{ (detail?.avgWeight ?? 0).toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="应收金额">
            <span style="color: var(--color-info); font-weight: 600">¥{{ (detail?.totalCharge ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="成本">¥{{ (detail?.totalCost ?? 0).toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="毛利">
            <span :style="{ color: (detail?.totalProfit ?? 0) >= 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: '600' }">
              ¥{{ (detail?.totalProfit ?? 0).toFixed(2) }}
            </span>
          </a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 附加费信息 -->
      <a-card v-if="detail?.weightCapSurcharge || detail?.quotaSurcharge" title="附加费信息" :bordered="false" style="margin-bottom: 12px">
        <a-descriptions :column="3" bordered size="small">
          <a-descriptions-item label="均重上限(kg)">{{ detail?.weightCap ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="超标重量(kg)">{{ (detail?.excessWeight ?? 0).toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="均重超标附加费">
            <span style="color: var(--color-danger)">¥{{ (detail?.weightCapSurcharge ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="省份配额附加费">
            <span style="color: var(--color-danger)">¥{{ (detail?.quotaSurcharge ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="含附加费应收">
            <span style="font-weight: 600">¥{{ (detail?.totalChargeWithSurcharge ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 预付款核销 -->
      <a-card title="预付款核销" :bordered="false" style="margin-bottom: 12px">
        <a-descriptions :column="3" bordered size="small">
          <a-descriptions-item label="预付款抵扣">¥{{ (detail?.prepayDeduction ?? 0).toFixed(2) }}</a-descriptions-item>
          <a-descriptions-item label="应付金额">
            <span style="color: var(--color-warning); font-weight: 600">¥{{ (detail?.payableAmount ?? 0).toFixed(2) }}</span>
          </a-descriptions-item>
        </a-descriptions>
      </a-card>

      <!-- 对账明细 -->
      <a-card title="对账明细" :bordered="false" style="margin-bottom: 12px">
        <template #extra>
          <a-space>
            <a-tag :color="reconciliationStatusColor" style="font-size: 13px; padding: 2px 10px">
              {{ reconciliationStatusText }}
            </a-tag>
            <a-popconfirm
              v-if="reconciliation?.reconciliationStatus === 0"
              title="确认对账无误？"
              @confirm="handleConfirmReconciliation"
            >
              <a-button type="primary" size="small" :loading="reconciliationLoading">确认对账</a-button>
            </a-popconfirm>
            <a-button
              v-if="reconciliation?.reconciliationStatus === 0 || reconciliation?.reconciliationStatus === 1"
              size="small"
              danger
              @click="disputeModalVisible = true"
            >提起异议</a-button>
            <a-button
              v-if="reconciliation?.reconciliationStatus === 2"
              size="small"
              type="primary"
              @click="resolveModalVisible = true"
            >处理异议</a-button>
            <a-button size="small" @click="handleExport" :loading="exportLoading">
              <template #icon><DownloadOutlined /></template>导出 Excel
            </a-button>
          </a-space>
        </template>

        <!-- 异议信息 -->
        <a-alert
          v-if="reconciliation?.disputeReason"
          type="warning"
          show-icon
          style="margin-bottom: 12px"
        >
          <template #message>
            <div>
              <strong>异议原因：</strong>{{ reconciliation.disputeReason }}
            </div>
            <div v-if="reconciliation.disputeResolution" style="margin-top: 4px">
              <strong>处理说明：</strong>{{ reconciliation.disputeResolution }}
            </div>
          </template>
        </a-alert>

        <a-table
          :columns="reconciliationColumns"
          :data-source="reconciliation?.lines ?? []"
          :loading="reconciliationLoading"
          :pagination="reconciliationLines.length > 50 ? { pageSize: 50, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` } : false"
          row-key="waybillId"
          bordered
          size="small"
          :scroll="{ x: 900 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'waybillDate'">
              {{ record.waybillDate?.slice(0, 10) }}
            </template>
            <template v-if="column.dataIndex === 'billableWeight'">
              {{ record.billableWeight?.toFixed(2) }}
            </template>
            <template v-if="column.dataIndex === 'freightCharge'">
              ¥{{ record.freightCharge?.toFixed(2) }}
            </template>
            <template v-if="column.dataIndex === 'surchargeAmount'">
              ¥{{ record.surchargeAmount?.toFixed(2) }}
            </template>
            <template v-if="column.dataIndex === 'chargeAmount'">
              <span style="font-weight: 600">¥{{ record.chargeAmount?.toFixed(2) }}</span>
            </template>
          </template>
          <template #summary>
            <a-table-summary fixed>
              <a-table-summary-row>
                <a-table-summary-cell :index="0" :col-span="4">
                  <strong>合计（{{ reconciliation?.totalWaybills ?? 0 }} 单）</strong>
                </a-table-summary-cell>
                <a-table-summary-cell :index="4" align="right" />
                <a-table-summary-cell :index="5" align="right" />
                <a-table-summary-cell :index="6" align="right" />
                <a-table-summary-cell :index="7" align="right">
                  <strong style="color: var(--color-info)">¥{{ (reconciliation?.totalCharge ?? 0).toFixed(2) }}</strong>
                </a-table-summary-cell>
              </a-table-summary-row>
            </a-table-summary>
          </template>
          <template #emptyText>
            <span style="color: #999">暂无对账明细</span>
          </template>
        </a-table>
      </a-card>

      <!-- 审核日志 -->
      <a-card title="审核日志" :bordered="false">
        <a-table
          :columns="reviewLogColumns"
          :data-source="detail?.reviewLogs ?? []"
          :pagination="false"
          row-key="id"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'action'">
              <a-tag :color="record.action === 1 ? 'success' : record.action === 2 ? 'error' : 'default'">
                {{ getActionText(record.action) }}
              </a-tag>
            </template>
            <template v-if="column.dataIndex === 'createdTime'">
              {{ record.createdTime?.slice(0, 19)?.replace('T', ' ') }}
            </template>
          </template>
          <template #emptyText>
            <span style="color: #999">暂无审核日志</span>
          </template>
        </a-table>
      </a-card>
    </a-spin>

    <!-- 审核/反审核弹窗 -->
    <a-modal v-model:open="reviewDialogVisible" :title="isApproveAction ? '审核账单' : '反审核账单'" width="400px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="备注">
          <a-textarea v-model:value="reviewRemark" :rows="3" placeholder="请输入备注（可选）" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="reviewDialogVisible = false">取消</a-button>
        <a-button v-if="isApproveAction" danger @click="handleReview(false)">驳回</a-button>
        <a-button v-if="isApproveAction" type="primary" :loading="reviewLoading" @click="handleReview(true)">通过</a-button>
        <a-button v-if="!isApproveAction" type="primary" danger :loading="reviewLoading" @click="handleReverseReview">确认反审核</a-button>
      </template>
    </a-modal>

    <!-- 提起异议弹窗 -->
    <a-modal v-model:open="disputeModalVisible" title="提起异议" width="480px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="异议原因" required>
          <a-textarea v-model:value="disputeReason" :rows="4" placeholder="请输入异议原因" :maxlength="1000" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="disputeModalVisible = false">取消</a-button>
        <a-button type="primary" danger :loading="reconciliationActionLoading" @click="handleRaiseDispute">提交异议</a-button>
      </template>
    </a-modal>

    <!-- 处理异议弹窗 -->
    <a-modal v-model:open="resolveModalVisible" title="处理异议" width="480px" :destroy-on-close="true">
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="处理说明" required>
          <a-textarea v-model:value="resolveResolution" :rows="4" placeholder="请输入处理说明" :maxlength="1000" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="resolveModalVisible = false">取消</a-button>
        <a-button type="primary" :loading="reconciliationActionLoading" @click="handleResolveDispute">提交处理</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { ArrowLeftOutlined, DownloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { downloadBlob } from '@/utils/download'
import {
  getInvoiceDetail,
  reviewInvoice,
  reverseReview,
  getReconciliationDetail,
  confirmReconciliation,
  raiseDispute,
  resolveDispute,
  exportReconciliation,
  type InvoiceDetailDto,
  type ReconciliationDetailDto,
} from '@/api/express'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const detail = ref<InvoiceDetailDto | null>(null)

const reviewLogColumns = [
  { title: '操作', dataIndex: 'action', width: 100, align: 'center' as const },
  { title: '规则结果', dataIndex: 'ruleResult', ellipsis: true },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '时间', dataIndex: 'createdTime', width: 180 },
]

async function fetchDetail() {
  const id = Number(route.params.id)
  if (!id) return
  loading.value = true
  try {
    detail.value = await getInvoiceDetail(id)
  } catch {
    message.error('获取账单详情失败')
  } finally {
    loading.value = false
  }
}

function getReviewStatusText(s: number) { return ['待审核', '已通过', '已驳回'][s] ?? '未知' }
function getReviewStatusColor(s: number) { return ['processing', 'success', 'error'][s] ?? 'default' }
function getStatusText(s: number) { return ['草稿', '已确认', '已发送', '已收款'][s] ?? '未知' }
function getStatusColor(s: number) { return ['default', 'blue', 'orange', 'green'][s] ?? 'default' }
function getActionText(a: number) { return ['自动审核', '审核通过', '审核驳回', '反审核'][a] ?? '未知' }

// 审核/反审核
const reviewDialogVisible = ref(false)
const isApproveAction = ref(true)
const reviewRemark = ref('')
const reviewLoading = ref(false)

function openReviewDialog(isApprove: boolean) {
  isApproveAction.value = isApprove
  reviewRemark.value = ''
  reviewDialogVisible.value = true
}

async function handleReview(approved: boolean) {
  if (!detail.value) return
  reviewLoading.value = true
  try {
    await reviewInvoice(detail.value.id, { approved, remark: reviewRemark.value || undefined })
    message.success(approved ? '审核通过' : '审核驳回')
    reviewDialogVisible.value = false
    fetchDetail()
  } catch { /* handled */ } finally {
    reviewLoading.value = false
  }
}

async function handleReverseReview() {
  if (!detail.value) return
  reviewLoading.value = true
  try {
    await reverseReview(detail.value.id, { remark: reviewRemark.value || undefined })
    message.success('反审核成功')
    reviewDialogVisible.value = false
    fetchDetail()
  } catch { /* handled */ } finally {
    reviewLoading.value = false
  }
}

// ==================== 对账 ====================
const reconciliation = ref<ReconciliationDetailDto | null>(null)
const reconciliationLoading = ref(false)
const reconciliationActionLoading = ref(false)
const exportLoading = ref(false)

const reconciliationLines = computed(() => reconciliation.value?.lines ?? [])

const reconciliationStatusText = computed(() => {
  const s = reconciliation.value?.reconciliationStatus
  return ['未对账', '已对账', '有异议', '异议已解决'][s ?? 0] ?? '未知'
})

const reconciliationStatusColor = computed(() => {
  const s = reconciliation.value?.reconciliationStatus
  return ['default', 'success', 'error', 'processing'][s ?? 0] ?? 'default'
})

const reconciliationColumns = [
  { title: '运单号', dataIndex: 'waybillNo', width: 150 },
  { title: '运单日期', dataIndex: 'waybillDate', width: 110 },
  { title: '目的地', dataIndex: 'provinceName', width: 100 },
  { title: '计费重量(kg)', dataIndex: 'billableWeight', width: 120, align: 'right' as const },
  { title: '基础运费(元)', dataIndex: 'freightCharge', width: 120, align: 'right' as const },
  { title: '附加费(元)', dataIndex: 'surchargeAmount', width: 110, align: 'right' as const },
  { title: '应收金额(元)', dataIndex: 'chargeAmount', width: 120, align: 'right' as const },
]

async function fetchReconciliation() {
  const id = Number(route.params.id)
  if (!id) return
  reconciliationLoading.value = true
  try {
    reconciliation.value = await getReconciliationDetail(id) as ReconciliationDetailDto
  } catch {
    // 对账数据可能尚未生成，不报错
    reconciliation.value = null
  } finally {
    reconciliationLoading.value = false
  }
}

async function handleConfirmReconciliation() {
  const id = Number(route.params.id)
  if (!id) return
  reconciliationActionLoading.value = true
  try {
    await confirmReconciliation(id)
    message.success('对账确认成功')
    fetchReconciliation()
  } catch { /* handled */ } finally {
    reconciliationActionLoading.value = false
  }
}

// 提起异议
const disputeModalVisible = ref(false)
const disputeReason = ref('')

async function handleRaiseDispute() {
  if (!disputeReason.value?.trim()) {
    message.error('请输入异议原因')
    return
  }
  const id = Number(route.params.id)
  if (!id) return
  reconciliationActionLoading.value = true
  try {
    await raiseDispute(id, { reason: disputeReason.value })
    message.success('异议已提交')
    disputeModalVisible.value = false
    disputeReason.value = ''
    fetchReconciliation()
  } catch { /* handled */ } finally {
    reconciliationActionLoading.value = false
  }
}

// 处理异议
const resolveModalVisible = ref(false)
const resolveResolution = ref('')

async function handleResolveDispute() {
  if (!resolveResolution.value?.trim()) {
    message.error('请输入处理说明')
    return
  }
  const id = Number(route.params.id)
  if (!id) return
  reconciliationActionLoading.value = true
  try {
    await resolveDispute(id, { resolution: resolveResolution.value })
    message.success('异议处理成功')
    resolveModalVisible.value = false
    resolveResolution.value = ''
    fetchReconciliation()
  } catch { /* handled */ } finally {
    reconciliationActionLoading.value = false
  }
}

// 导出
async function handleExport() {
  const id = Number(route.params.id)
  if (!id) return
  exportLoading.value = true
  try {
    const res: any = await exportReconciliation(id)
    const blob = new Blob([res], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    downloadBlob(blob, `对账明细_${detail.value?.invoiceNo ?? id}.xlsx`)
  } catch {
    message.error('导出失败')
  } finally {
    exportLoading.value = false
  }
}

onMounted(() => {
  fetchDetail()
  fetchReconciliation()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.cross-module-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}
</style>
