<template>
  <div class="voucher-generations">
    <!-- 筛选栏 -->
    <div class="vg-filter">
      <a-space>
        <a-input-number v-model:value="filterBatchId" placeholder="批次ID" :min="1" style="width: 120px" allow-clear />
      </a-space>
    </div>

    <!-- 列表 -->
    <a-table
      :columns="columns"
      :data-source="list"
      :loading="loading"
      row-key="id"
      size="small"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'batchId'">
          {{ record.batchId }}
        </template>

        <template v-else-if="column.dataIndex === 'targetTable'">
          {{ record.targetTable || '-' }}
        </template>

        <template v-else-if="column.dataIndex === 'rows'">
          <span>{{ record.totalRows }}</span>
          <span style="color: var(--color-success-text)"> / {{ record.matchedRows }}</span>
          <span style="color: var(--color-danger-text)" :class="{ 'unmatched-link': record.unmatchedRows > 0 }" @click="record.unmatchedRows > 0 && showUnmatched(record)"> / {{ record.unmatchedRows }}</span>
        </template>

        <template v-else-if="column.dataIndex === 'generatedVoucherCount'">
          <span v-if="record.voucherIds && record.voucherIds.length > 0" class="voucher-count-link">
            {{ record.generatedVoucherCount }}
          </span>
          <span v-else>{{ record.generatedVoucherCount }}</span>
        </template>

        <template v-else-if="column.dataIndex === 'status'">
          <span :style="{ color: statusColor(record.status) }">{{ statusText(record.status) }}</span>
        </template>

        <template v-else-if="column.dataIndex === 'createTime'">
          {{ formatDate(record.createTime) }}
        </template>

        <template v-else-if="column.dataIndex === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="showUnmatched(record)" :disabled="!record.unmatchedRows">详情</a-button>
            <a-button type="link" size="small" @click="openValidation(record)">验证</a-button>
            <a-button
              v-if="record.status === 2 || record.status === 3"
              type="link"
              size="small"
              :loading="retryingId === record.id"
              @click="handleRetry(record)"
            >重试</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 未匹配明细抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="未匹配明细"
      :width="640"
      :destroyOnClose="true"
    >
      <a-spin :spinning="detailLoading">
        <a-table
          v-if="unmatchedDetails.length > 0"
          :columns="unmatchedColumns"
          :data-source="unmatchedDetails"
          row-key="rowId"
          size="small"
          :pagination="{ pageSize: 20, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` }"
        />
        <a-empty v-else description="无未匹配数据" />
      </a-spin>

      <template v-if="currentRecord?.errorMessage">
        <a-divider />
        <a-alert type="error" :message="currentRecord.errorMessage" show-icon />
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { getVoucherGenerations, getVoucherGeneration, retryVoucherGeneration } from '@/api/cardflow'

interface UnmatchedDetail {
  rowId: number
  fieldName: string
  fieldValue: string
  reason: string
}

interface VoucherGenerationRecord {
  id: number
  batchId: number
  targetTable: string
  totalRows: number
  matchedRows: number
  unmatchedRows: number
  unmatchedDetails?: UnmatchedDetail[]
  generatedVoucherCount: number
  voucherIds?: number[]
  status: number
  errorMessage?: string
  createTime: string
}

// 状态
const router = useRouter()
const loading = ref(false)
const list = ref<VoucherGenerationRecord[]>([])
const filterBatchId = ref<number | undefined>(undefined)

// 抽屉
const drawerVisible = ref(false)
const detailLoading = ref(false)
const unmatchedDetails = ref<UnmatchedDetail[]>([])
const currentRecord = ref<VoucherGenerationRecord | null>(null)

// 重试
const retryingId = ref<number | null>(null)

// 列定义
const columns = [
  { title: '批次ID', dataIndex: 'batchId', width: 80, align: 'center' as const },
  { title: '暂存表', dataIndex: 'targetTable', width: 160, ellipsis: true },
  { title: '总行数 / 已匹配 / 未匹配', dataIndex: 'rows', width: 180, align: 'center' as const },
  { title: '生成凭证数', dataIndex: 'generatedVoucherCount', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createTime', width: 160, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const },
]

const unmatchedColumns = [
  { title: '行号', dataIndex: 'rowId', width: 80, align: 'center' as const },
  { title: '字段名', dataIndex: 'fieldName', width: 140 },
  { title: '字段值', dataIndex: 'fieldValue', width: 160, ellipsis: true },
  { title: '原因', dataIndex: 'reason', ellipsis: true },
]

// 状态映射
function statusText(status: unknown): string {
  const map: Record<number, string> = { 0: '进行中', 1: '全部成功', 2: '部分成功', 3: '全部失败' }
  return map[Number(status)] ?? '未知'
}

function statusColor(status: unknown): string {
  const map: Record<number, string> = { 0: 'var(--color-info)', 1: 'var(--color-success)', 2: 'var(--color-warning)', 3: 'var(--color-danger)' }
  return map[Number(status)] ?? 'var(--text-3)'
}

function formatDate(s: string): string {
  if (!s) return '-'
  const d = new Date(s)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

// 加载列表
async function loadList() {
  loading.value = true
  try {
    const params: any = {}
    if (filterBatchId.value) params.batchId = filterBatchId.value
    const res: any = await getVoucherGenerations(params)
    list.value = Array.isArray(res) ? res : (res?.data ?? res?.items ?? [])
  } catch (e: any) {
    console.error('加载凭证生成记录失败', e)
    message.error('加载凭证生成记录失败')
  } finally {
    loading.value = false
  }
}

// 查看未匹配明细
async function showUnmatched(record: Record<string, any>) {
  currentRecord.value = record as VoucherGenerationRecord
  drawerVisible.value = true
  detailLoading.value = true
  try {
    const res: any = await getVoucherGeneration(record.id)
    const detail = res?.data ?? res
    unmatchedDetails.value = detail?.unmatchedDetails ?? []
  } catch (e: any) {
    console.error('加载未匹配明细失败', e)
    message.error('加载未匹配明细失败')
    unmatchedDetails.value = []
  } finally {
    detailLoading.value = false
  }
}

function openValidation(record: Record<string, any>) {
  router.push({ path: `/cardflow/import-validation/${Number(record.batchId)}` })
}

// 重试
async function handleRetry(record: Record<string, any>) {
  retryingId.value = Number(record.id)
  try {
    await retryVoucherGeneration(Number(record.id))
    message.success('已提交重试')
    await loadList()
  } catch (e: any) {
    console.error('重试失败', e)
    message.error(e?.message || '重试失败')
  } finally {
    retryingId.value = null
  }
}

// 暴露刷新方法供父组件调用
defineExpose({ loadList })

onMounted(() => {
  loadList()
})
</script>

<style scoped>
.voucher-generations {
  padding: 0;
}
.vg-filter {
  margin-bottom: 12px;
}
.unmatched-link {
  cursor: pointer;
  text-decoration: underline;
}
.unmatched-link:hover {
  opacity: 0.8;
}
.voucher-count-link {
  color: var(--color-info);
  cursor: default;
}
</style>
