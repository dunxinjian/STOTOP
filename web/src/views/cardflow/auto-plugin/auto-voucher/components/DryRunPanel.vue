<template>
  <a-drawer
    :open="open"
    title="DryRun 预演分析"
    :width="720"
    placement="right"
    destroy-on-close
    @close="emit('update:open', false)"
  >
    <!-- 操作栏 -->
    <div class="action-bar">
      <span class="label">批次：</span>
      <a-select
        v-model:value="selectedBatchId"
        placeholder="请选择历史批次"
        style="flex: 1;"
        show-search
        :filter-option="filterBatchOption"
        :loading="loadingBatches"
      >
        <a-select-option v-for="b in batchList" :key="b.id" :value="b.id">
          #{{ b.id }} - {{ b.fileName || b.batchNo }} ({{ b.totalRows }}行)
        </a-select-option>
      </a-select>
      <a-button type="primary" :loading="running" :disabled="!selectedBatchId" @click="executeDryRun">
        执行预演
      </a-button>
    </div>

    <!-- 结果展示 -->
    <template v-if="result">
      <!-- 统计卡片 -->
      <div class="stat-cards">
        <div class="stat-card">
          <a-statistic title="总行数" :value="result.totalRows" />
        </div>
        <div class="stat-card">
          <a-statistic title="已匹配" :value="result.matchedRows" :value-style="{ color: 'var(--color-success-text)' }" />
        </div>
        <div class="stat-card">
          <a-statistic
            title="未匹配"
            :value="result.unmatchedRows"
            :value-style="{ color: result.unmatchedRows > 0 ? 'var(--color-danger-text)' : undefined }"
          />
        </div>
        <div class="stat-card">
          <a-statistic title="预计凭证" :value="result.estimatedVouchers" :value-style="{ color: 'var(--color-info)' }" />
        </div>
      </div>

      <!-- 按字段汇总 -->
      <div v-if="result.groupedSummary && result.groupedSummary.length > 0" class="section">
        <div class="section-title">📊 按字段汇总（未匹配行）</div>
        <a-table
          :columns="summaryColumns"
          :data-source="result.groupedSummary"
          size="small"
          :pagination="false"
          bordered
          row-key="fieldValue"
        />
      </div>

      <!-- 未匹配明细 -->
      <div v-if="result.unmatchedDetails && result.unmatchedDetails.length > 0" class="section">
        <div class="section-title">
          📋 未匹配交易明细 ({{ result.unmatchedRows }}条<template v-if="result.hasMoreUnmatched">，仅展示前500条</template>)
        </div>
        <a-alert
          v-if="result.hasMoreUnmatched"
          type="warning"
          message="未匹配行超过 500 条，仅展示前 500 条"
          show-icon
          style="margin-bottom: 8px;"
        />
        <a-table
          :columns="detailColumns"
          :data-source="result.unmatchedDetails"
          size="small"
          :pagination="{ pageSize: 50, size: 'small', showTotal: (t: number) => `共 ${t} 条` }"
          bordered
          :scroll="{ y: 300, x: detailScrollX }"
          row-key="FID"
        />
      </div>
    </template>

    <!-- 错误提示 -->
    <a-alert v-if="errorMsg" type="error" :message="errorMsg" show-icon style="margin-top: 12px;" />
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { getImportBatches, dryRunAutoPluginRule, dryRunAutoPluginRuleNew } from '@/api/cardflow'
import type { DryRunResult } from '@/api/cardflow'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'

const props = defineProps<{
  open: boolean
  ruleId?: number
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const store = useAutoVoucherRuleStore()

const selectedBatchId = ref<number | undefined>(undefined)
const batchList = ref<any[]>([])
const loadingBatches = ref(false)
const running = ref(false)
const result = ref<DryRunResult | null>(null)
const errorMsg = ref('')

// 打开时加载批次
watch(() => props.open, (val) => {
  if (val) {
    loadBatches()
  }
})

// ==================== 汇总表格列 ====================
const summaryColumns = [
  { title: '字段值', dataIndex: 'fieldValue', ellipsis: true },
  { title: '未匹配数', dataIndex: 'count', width: 100, align: 'right' as const },
  {
    title: '合计金额',
    dataIndex: 'totalAmount',
    width: 130,
    align: 'right' as const,
    customRender: ({ text }: { text: number | null }) =>
      text != null ? `¥${text.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '-',
  },
]

// ==================== 明细表格列（动态生成）====================
const PRIORITY_FIELDS = ['F业务日期', 'F费用摘要', 'F支出金额', 'F费用类别', 'F收入金额', 'F金额']

const detailColumns = computed(() => {
  if (!result.value || !result.value.unmatchedDetails || result.value.unmatchedDetails.length === 0) return []
  const sample = result.value.unmatchedDetails[0]
  const allKeys = Object.keys(sample).filter(k => k !== 'FID' && !k.startsWith('F批次'))

  // 优先展示的字段
  const priorityKeys = PRIORITY_FIELDS.filter(k => allKeys.includes(k))
  const otherKeys = allKeys.filter(k => !priorityKeys.includes(k))
  const orderedKeys = [...priorityKeys, ...otherKeys]

  const cols: any[] = [{ title: 'FID', dataIndex: 'FID', width: 60, fixed: 'left' as const }]
  for (const k of orderedKeys.slice(0, 10)) {
    const isAmount = k.includes('金额') || k.includes('费用')
    cols.push({
      title: k,
      dataIndex: k,
      width: 120,
      ellipsis: true,
      align: isAmount ? ('right' as const) : undefined,
      customRender: isAmount
        ? ({ text }: { text: any }) => {
            const num = Number(text)
            return !isNaN(num) && text != null && text !== ''
              ? `¥${num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
              : text
          }
        : undefined,
    })
  }
  return cols
})

const detailScrollX = computed(() => {
  return detailColumns.value.length * 120 + 60
})

// ==================== 批次过滤 ====================
function filterBatchOption(input: string, option: any) {
  const text = `#${option.value} - ${option.children}`
  return text.toLowerCase().includes(input.toLowerCase())
}

// ==================== 加载批次 ====================
async function loadBatches() {
  loadingBatches.value = true
  try {
    const stagingTable = store.formData.stagingTable
    if (!stagingTable) {
      batchList.value = []
      return
    }
    const end = new Date()
    const start = new Date(end.getTime() - 90 * 24 * 60 * 60 * 1000)
    const res: any = await getImportBatches({
      pageSize: 50,
      page: 1,
      status: 2,
      startDate: start.toISOString().split('T')[0],
      endDate: end.toISOString().split('T')[0],
      targetTable: stagingTable,
    })
    batchList.value = res?.items || res || []
  } catch {
    batchList.value = []
  } finally {
    loadingBatches.value = false
  }
}

// ==================== 执行预演 ====================
async function executeDryRun() {
  if (!selectedBatchId.value) return
  running.value = true
  result.value = null
  errorMsg.value = ''

  try {
    const configJson = store.buildConfigJson()
    let res: any
    if (props.ruleId) {
      res = await dryRunAutoPluginRule(props.ruleId, { batchId: selectedBatchId.value, configJson })
    } else {
      res = await dryRunAutoPluginRuleNew({ batchId: selectedBatchId.value, configJson })
    }
    result.value = res
  } catch (e: any) {
    errorMsg.value = e?.response?.data?.message || e?.message || '预演失败'
  } finally {
    running.value = false
  }
}
</script>

<style lang="scss" scoped>
.action-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;

  .label {
    white-space: nowrap;
    color: var(--text-2);
    font-weight: 500;
  }
}

.stat-cards {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.stat-card {
  flex: 1;
  padding: 12px 16px;
  background: var(--bg-muted);
  border-radius: 8px;
  border: 1px solid var(--border);
  text-align: center;
}

.section {
  margin-bottom: 20px;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
  margin-bottom: 10px;
}
</style>
