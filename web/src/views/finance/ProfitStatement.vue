<template>
  <div class="page-container">
    <PageHeader title="利润表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select v-model:value="unitType" style="width: 100px"
          :options="[{ label: '单位：元', value: 'yuan' }, { label: '单位：万元', value: 'wan' }]" />
        <a-button @click="chartDrawerVisible = true"><BarChartOutlined />图表分析</a-button>
        <a-button @click="() => {}"><SettingOutlined />设置</a-button>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <!-- 统一期间选择器 -->
            <a-select v-model:value="filterForm.startPeriodId" placeholder="选择期间" style="width: 160px" @change="onPeriodChange"
              :options="periodList.map(item => ({ label: item.name, value: item.id }))" />
            <a-checkbox v-model:checked="enablePeriodRange" class="period-range-checkbox" />
            <span class="to-text">至</span>
            <a-select
              v-model:value="filterForm.endPeriodId"
              placeholder="选择期间"
              style="width: 160px"
              :disabled="!enablePeriodRange"
              @change="onPeriodChange"
              :options="periodList.map(item => ({ label: item.name, value: item.id }))"
            />
            <div class="format-tabs">
              <span
                :class="['format-tab', { active: filterForm.format === 'small' }]"
                @click="switchFormat('small')"
              >
                小番报表
              </span>
              <span
                :class="['format-tab', { active: filterForm.format === 'enterprise' }]"
                @click="switchFormat('enterprise')"
              >
                小企业报表
              </span>
            </div>
            <!-- 取值规则链接 -->
            <a v-if="filterForm.format === 'enterprise'" class="rule-link" @click="showRuleDialog = true">
              利润表取值规则
            </a>
          </div>
          <div></div>
        </div>
      </template>
    </PageHeader>

    <!-- 数据表区域 - 小番报表格式 -->
    <a-card v-if="filterForm.format === 'small'" :bordered="false">
      <a-table
        :columns="smallColumns"
        :dataSource="tableData"
        rowKey="id"
        :loading="loading"
        :pagination="false"
        bordered
        :rowClassName="getRowClassName"
      >
        <template #headerCell="{ column }">
          <template v-if="column.dataIndex === 'itemName'">
            <div class="header-with-btn">
              <span>项目</span>
              <DownOutlined class="sort-icon" />
            </div>
          </template>
          <template v-if="column.dataIndex === 'calcBtn'">
            <a-button type="primary" size="small" class="calc-btn">测算一下</a-button>
          </template>
          <template v-if="column.dataIndex === 'ratioDiff'">
            <div class="header-with-btn">
              <span>占比差值</span>
              <DownOutlined class="sort-icon" />
            </div>
          </template>
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'itemName'">
            <span :style="{ paddingLeft: (record.level - 1) * 20 + 'px', fontWeight: record.isTotal ? 'bold' : 'normal' }">
              <DownOutlined v-if="record.children && record.children.length > 0" class="expand-icon" />
              {{ record.itemName }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'yearAmount'">
            <a class="drill-link" @click="handleDrillDown(record, 'yearAmount')">
              {{ formatAmount(record.yearAmount) }}
            </a>
          </template>
          <template v-if="column.dataIndex === 'yearRatio'">
            {{ record.yearRatio?.toFixed(2) }}%
          </template>
          <template v-if="column.dataIndex === 'periodAmount'">
            <a class="drill-link" @click="handleDrillDown(record, 'periodAmount')">
              {{ formatAmount(record.periodAmount) }}
            </a>
          </template>
          <template v-if="column.dataIndex === 'periodRatio'">
            {{ record.periodRatio?.toFixed(2) }}%
          </template>
          <template v-if="column.dataIndex === 'ratioDiff'">
            <span :class="{ 'negative-value': record.ratioDiff && record.ratioDiff < 0 }">
              {{ record.ratioDiff?.toFixed(2) }}%
            </span>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 数据表区域 - 小企业报表格式 -->
    <a-card v-else :bordered="false">
      <a-table
        :columns="enterpriseColumns"
        :dataSource="enterpriseTableData"
        rowKey="id"
        :loading="loading"
        :pagination="false"
        bordered
        class="enterprise-table"
        :rowClassName="getEnterpriseRowClassName"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'itemName'">
            <span
              class="project-name"
              :class="{
                'main-title': record.isMainTitle,
                'sub-title': record.isSubTitle,
                'indent': record.isIndent
              }"
              :style="{ paddingLeft: record.indentLevel * 20 + 'px' }"
            >
              {{ record.itemName }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'yearAmount'">
            <a class="drill-link" @click="handleEnterpriseDrillDown(record, 'yearAmount')">
              <span :class="{ 'negative-value': record.yearAmount && record.yearAmount < 0 }">
                {{ formatAmount(record.yearAmount) }}
              </span>
            </a>
          </template>
          <template v-if="column.dataIndex === 'periodAmount'">
            <a class="drill-link" @click="handleEnterpriseDrillDown(record, 'periodAmount')">
              <span :class="{ 'negative-value': record.periodAmount && record.periodAmount < 0 }">
                {{ formatAmount(record.periodAmount) }}
              </span>
            </a>
          </template>
          <template v-if="column.dataIndex === 'prevPeriodAmount'">
            <span :class="{ 'negative-value': record.prevPeriodAmount && record.prevPeriodAmount < 0 }">
              {{ formatAmount(record.prevPeriodAmount) }}
            </span>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 利润表取值规则弹窗 -->
    <a-modal
      v-model:open="showRuleDialog"
      title="利润表取值规则"
      :width="700"
      :centered="true"
      :destroyOnClose="true"
      class="rule-dialog"
    >
      <div class="rule-content">
        <div class="rule-list">
          <div class="rule-item">
            <div class="rule-number">1</div>
            <div class="rule-text">营业税金及附加下的其中内的"消费税"取数540301，"城市维护建设税"取数540302，"资源税"取数540310，"土地增值税"取数540305，"城镇土地使用税、房产税、车船税、印花税"取数540306+540307+540308+540309，"教育费附加、矿产资源补偿费、排污费"取数540303+540304+540311+540312；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">2</div>
            <div class="rule-text">销售费用取数5601，销售费用下的其中内的"商品维修费"取数560107，"广告费和业务宣传费"560115；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">3</div>
            <div class="rule-text">管理费用取数5602，管理费用下的其中内的"开办费"取数560221，"研究费用"取数560222，"业务招待费"取数560214；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">4</div>
            <div class="rule-text">财务费用取数5603，财务费用下的其中内的"利息费用"取数560301；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">5</div>
            <div class="rule-text">营业外收入取数5301，营业外收入下的其中内的"政府补助"取数530107；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">6</div>
            <div class="rule-text">营业外支出取数5711，营业外支出下的其中内的"坏账损失"取数571103，"无法收回的长期债券投资损失"取数571104，"无法收回的长期股权投资损失"取数571105，"自然灾害等不可抗力因素造成的损失"取数571106，"税收滞纳金"取数571107；</div>
          </div>
          <div class="rule-item">
            <div class="rule-number">7</div>
            <div class="rule-text">所得税费用取数5801。</div>
          </div>
        </div>
        <div class="rule-notice">
          <p class="notice-text">如您创建账套的模板为"小企业账套（推荐）"，建议对上述科目编码及名称不做修改（添加子科目或辅助核算不受影响）；如确实无需使用，也可删除，但新增其他自定义科目时请避免使用上述代码。</p>
          <p class="notice-text">如创建的模板是空账套，建议按上述科目编码按需要创建相应的科目，如涉及凭证导入，也需相应调整导入凭证涉及上述科目对应的科目编码。</p>
        </div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="showRuleDialog = false">关闭</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 钻取明细弹窗 -->
    <DrillDownModal
      v-model:visible="drillDownVisible"
      :title="drillDownTitle"
      :data="drillDownData"
    />

    <!-- 图表分析抽屉 -->
    <a-drawer
      v-model:open="chartDrawerVisible"
      title="图表分析"
      :width="1200"
      placement="right"
      :destroyOnClose="true"
    >
      <div v-if="tableData.length > 0 || enterpriseTableData.length > 0" class="drawer-charts">
        <div class="drawer-chart-item">
          <FinanceTrendChart :data="trendData" title="收入/成本/利润12月趋势" />
        </div>
        <div class="drawer-chart-item">
          <FinancePieChart :data="expenseCompositionData" title="费用构成" />
        </div>
        <div class="drawer-chart-item">
          <FinanceBarChart :data="yoyComparisonData" title="同比对比" currentLabel="本期" previousLabel="去年同期" />
        </div>
        <div class="drawer-chart-item">
          <FinanceBarChart :data="momComparisonData" title="环比对比" currentLabel="本期" previousLabel="上期" />
        </div>
      </div>
      <EmptyState v-else />
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined, SettingOutlined, DownOutlined, BarChartOutlined } from '@ant-design/icons-vue'
import { getProfitStatement, getSmallEnterpriseProfitStatement, getPeriods, getReportDrillDown, getProfitTrend, getExpenseComposition, getYoYComparison, getMoMComparison } from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import DrillDownModal from '@/components/charts/DrillDownModal.vue'
import FinanceTrendChart from '@/components/charts/FinanceTrendChart.vue'
import FinancePieChart from '@/components/charts/FinancePieChart.vue'
import FinanceBarChart from '@/components/charts/FinanceBarChart.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  startPeriodId: undefined as number | undefined,
  endPeriodId: undefined as number | undefined,
  format: 'small'
})

// 期间区间勾选
const enablePeriodRange = ref(false)

const unitType = ref('yuan')
const periodList = ref<{ id: number; name: string; year?: number; periodNo?: number }[]>([])
const showRuleDialog = ref(false)
const chartDrawerVisible = ref(false)
const loading = ref(false)

// 钻取相关
const drillDownVisible = ref(false)
const drillDownTitle = ref('')
const drillDownData = ref<any[]>([])

// 图表相关
const trendData = ref<any[]>([])
const expenseCompositionData = ref<any[]>([])
const yoyComparisonData = ref<any[]>([])
const momComparisonData = ref<any[]>([])

// 小番报表 columns
const smallColumns = [
  { title: '项目', dataIndex: 'itemName', key: 'itemName', minWidth: 240, fixed: 'left' as const },
  { title: '测算一下', dataIndex: 'calcBtn', key: 'calcBtn', width: 100, align: 'center' as const },
  {
    title: '本年累计数据', children: [
      { title: '参考金额', dataIndex: 'yearAmount', key: 'yearAmount', minWidth: 140, align: 'right' as const },
      { title: '营收占比', dataIndex: 'yearRatio', key: 'yearRatio', width: 100, align: 'right' as const },
    ],
  },
  {
    title: '本期数据', children: [
      { title: '本期金额', dataIndex: 'periodAmount', key: 'periodAmount', minWidth: 140, align: 'right' as const },
      { title: '营收占比', dataIndex: 'periodRatio', key: 'periodRatio', width: 100, align: 'right' as const },
    ],
  },
  { title: '占比差值', dataIndex: 'ratioDiff', key: 'ratioDiff', width: 120, align: 'right' as const },
]

// 小企业报表 columns
const enterpriseColumns = [
  { title: '项目', dataIndex: 'itemName', key: 'itemName', minWidth: 280, fixed: 'left' as const },
  { title: '行次', dataIndex: 'rowNo', key: 'rowNo', width: 60, align: 'center' as const },
  { title: '本年累计金额', dataIndex: 'yearAmount', key: 'yearAmount', minWidth: 140, align: 'right' as const },
  { title: '本期金额', dataIndex: 'periodAmount', key: 'periodAmount', minWidth: 140, align: 'right' as const },
  { title: '上期金额', dataIndex: 'prevPeriodAmount', key: 'prevPeriodAmount', minWidth: 140, align: 'right' as const },
]

// 期间变化处理
function onPeriodChange() {
  loadData()
}

// 高亮行名称列表
const highlightItems = ['营业税金', '其他损失', '所得税费用']

// 小番报表表格数据
const tableData = ref<any[]>([])

// 小企业报表表格数据（34行）
const enterpriseTableData = ref<any[]>([])

// 切换报表格式
function switchFormat(format: string) {
  filterForm.value.format = format
  loadData()
}

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  const divisor = unitType.value === 'wan' ? 10000 : 1
  const value = val / divisor
  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 小番报表行类名
function getRowClassName(record: any) {
  if (record.highlight || highlightItems.some(item => record.itemName.includes(item))) {
    return 'highlight-row'
  }
  return ''
}

// 小企业报表行类名
function getEnterpriseRowClassName(record: any) {
  if (record.highlightLand) {
    return 'land-highlight-row'
  }
  return ''
}

// 加载数据
async function loadData() {
  // 检查期间是否已选择
  if (!filterForm.value.startPeriodId) {
    return
  }
  loading.value = true
  try {
    // 计算实际的 startPeriodId / endPeriodId
    const startId = filterForm.value.startPeriodId
    const endId = enablePeriodRange.value ? filterForm.value.endPeriodId : startId

    if (filterForm.value.format === 'small') {
      // 小番报表：使用期间查询
      const res = await getProfitStatement({
        startPeriodId: startId,
        endPeriodId: endId ?? startId,
        format: 'small',
        accountSetId: accountSetStore.getCurrentAccountSetId() || undefined
      })
      if (res && Array.isArray(res)) {
        tableData.value = res.map((item: any) => ({
          id: item.rowIndex?.toString() || '',
          itemName: item.itemName,
          level: 1,
          isTotal: [7, 10, 12].includes(item.rowIndex),
          yearAmount: item.yearAccumulatedAmount,
          yearRatio: item.yearRevenueRatio,
          periodAmount: item.currentAmount,
          periodRatio: item.currentRevenueRatio,
          ratioDiff: item.ratioDifference,
          children: [],
          highlight: highlightItems.some((h: string) => item.itemName?.includes(h))
        }))
      }
    } else {
      // 小企业报表：从选中期间解析年月
      const selectedPeriod = periodList.value.find(p => p.id === startId)
      const year = selectedPeriod?.year || new Date().getFullYear()
      const month = selectedPeriod?.periodNo || (new Date().getMonth() + 1)

      // 计算上期年月
      let prevYear = year
      let prevMonth = month - 1
      if (prevMonth < 1) { prevYear -= 1; prevMonth = 12 }

      const currentAccountSetId = accountSetStore.getCurrentAccountSetId() || undefined
      const [res, prevRes] = await Promise.all([
        getSmallEnterpriseProfitStatement({ year, month, accountSetId: currentAccountSetId }),
        getSmallEnterpriseProfitStatement({ year: prevYear, month: prevMonth, accountSetId: currentAccountSetId }).catch(() => null)
      ])

      if (res && Array.isArray(res)) {
        const prevMap: Record<number, number> = {}
        if (prevRes && Array.isArray(prevRes)) {
          prevRes.forEach((item: any) => { prevMap[item.rowIndex] = item.currentAmount })
        }
        enterpriseTableData.value = res.map((item: any) => ({
          id: `e${item.rowIndex}`,
          itemName: item.itemName,
          rowNo: item.rowIndex,
          yearAmount: item.yearAccumulatedAmount,
          periodAmount: item.currentAmount,
          prevPeriodAmount: prevMap[item.rowIndex] ?? 0,
          isMainTitle: item.isMainTitle,
          isSubTitle: item.isSubTitle,
          isIndent: item.isIndent,
          indentLevel: item.indentLevel,
          highlightLand: item.itemName?.includes('土地增值税')
        }))
      }
    }
  } catch (error) {
    console.error('加载利润表数据失败', error)
  } finally {
    loading.value = false
  }
}

// 导出Excel(CSV)
function exportToExcel() {
  const currentFormat = filterForm.value.format
  const data = currentFormat === 'small' ? tableData.value : enterpriseTableData.value
  if (!data || !data.length) { message.warning('暂无数据可导出'); return }

  const startPeriod = periodList.value.find(p => p.id === filterForm.value.startPeriodId)
  const periodStr = startPeriod ? startPeriod.name : new Date().toISOString().slice(0, 7)

  let headers: string[]
  let rows: any[][]

  if (currentFormat === 'small') {
    headers = ['项目', '本年累计金额', '本年营收占比(%)', '本期金额', '本期营收占比(%)', '占比差値(%)']
    rows = (data as any[]).map(r => [r.itemName, r.yearAmount ?? '', r.yearRatio?.toFixed(2) ?? '', r.periodAmount ?? '', r.periodRatio?.toFixed(2) ?? '', r.ratioDiff?.toFixed(2) ?? ''])
  } else {
    headers = ['项目', '行次', '本年累计金额', '本期金额', '上期金额']
    rows = (data as any[]).map(r => [r.itemName, r.rowNo ?? '', r.yearAmount ?? '', r.periodAmount ?? '', r.prevPeriodAmount ?? ''])
  }

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `利润表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

// 加载期间列表
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .sort((a: any, b: any) => {
        if (a.year !== b.year) return b.year - a.year
        return b.periodNo - a.periodNo
      })
      .map((p: any) => ({
        id: p.id,
        name: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
        year: p.year,
        periodNo: p.periodNo
      }))
    if (periodList.value.length > 0) {
      const now = new Date()
      const currentYear = now.getFullYear()
      const currentMonth = now.getMonth() + 1
      // startPeriodId 默认当前月或最近期
      let defaultPeriod = periodList.value.find(
        (p: any) => p.year === currentYear && p.periodNo === currentMonth
      )
      if (!defaultPeriod) defaultPeriod = periodList.value[0]
      filterForm.value.startPeriodId = defaultPeriod.id
      // endPeriodId 默认最早期（列表最后一个）
      filterForm.value.endPeriodId = periodList.value[periodList.value.length - 1].id
    }
  } catch (error) {
    console.error('加载期间列表失败', error)
  }
}

onMounted(async () => {
  await loadPeriods()
  loadData()
  loadChartData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  filterForm.value.startPeriodId = undefined
  filterForm.value.endPeriodId = undefined
  await loadPeriods()
  loadData()
  loadChartData()
})

// 钻取处理函数
async function handleDrillDown(record: any, _column: string) {
  if (!record.id) return
  const rowIndex = parseInt(record.id)
  if (isNaN(rowIndex)) return
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.startPeriodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const month = selectedPeriod?.periodNo || (new Date().getMonth() + 1)
  drillDownTitle.value = `${record.itemName} - 凭证明细`
  try {
    const res = await getReportDrillDown({
      reportType: '利润表',
      rowIndex,
      year,
      month,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined
    })
    drillDownData.value = res || []
    drillDownVisible.value = true
  } catch (e) {
    console.error('钻取失败', e)
  }
}

async function handleEnterpriseDrillDown(record: any, _column: string) {
  if (!record.rowNo) return
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.startPeriodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const month = selectedPeriod?.periodNo || (new Date().getMonth() + 1)
  drillDownTitle.value = `${record.itemName} - 凭证明细`
  try {
    const res = await getReportDrillDown({
      reportType: '小企业利润表',
      rowIndex: record.rowNo,
      year,
      month,
      accountSetId: accountSetStore.getCurrentAccountSetId() || undefined
    })
    drillDownData.value = res || []
    drillDownVisible.value = true
  } catch (e) {
    console.error('钻取失败', e)
  }
}

// 加载图表数据
async function loadChartData() {
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.startPeriodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const month = selectedPeriod?.periodNo || (new Date().getMonth() + 1)
  const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined

  try {
    const [trend, expense, yoy, mom] = await Promise.all([
      getProfitTrend({ year, accountSetId }).catch(() => []),
      getExpenseComposition({ year, month, accountSetId }).catch(() => []),
      getYoYComparison({ year, month, accountSetId }).catch(() => []),
      getMoMComparison({ year, month, accountSetId }).catch(() => [])
    ])
    trendData.value = trend || []
    expenseCompositionData.value = expense || []
    yoyComparisonData.value = yoy || []
    momComparisonData.value = mom || []
  } catch (e) {
    console.error('加载图表数据失败', e)
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.to-text {
  color: #606266;
  padding: 0 4px;
}

.period-range-checkbox {
  margin: 0 2px;
}

.format-tabs {
  display: flex;
  margin-left: 16px;
  border-radius: 4px;
  overflow: hidden;
  border: 1px solid #dcdfe6;
  background: var(--bg-muted);

  .format-tab {
    padding: 6px 16px;
    font-size: 14px;
    color: #606266;
    cursor: pointer;
    background: transparent;
    transition: all 0.3s;

    &:hover {
      color: var(--color-primary);
    }

    &.active {
      color: var(--text-1);
      background: var(--bg-card);
      font-weight: 600;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
    }
  }
}

.rule-link {
  margin-left: 16px;
  color: var(--text-1);
  font-size: 14px;
  cursor: pointer;
  text-decoration: none;

  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.data-table {
  .header-with-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 4px;

    .sort-icon {
      font-size: 12px;
      color: #909399;
    }
  }

  .calc-btn {
    padding: 4px 12px;
    font-size: 12px;
  }

  .expand-icon {
    font-size: 12px;
    margin-right: 4px;
    color: #909399;
  }

  .negative-value {
    color: var(--color-danger);
  }

  :deep(.highlight-row) {
    background-color: #FFFDE7 !important;
  }

  :deep(.highlight-row:hover) {
    background-color: #FFF9C4 !important;
  }

  :deep(.land-highlight-row) {
    background-color: #FFFDE7 !important;
  }

  :deep(.land-highlight-row:hover) {
    background-color: #FFF9C4 !important;
  }
}

// 小企业报表样式
.enterprise-table {
  .project-name {
    font-size: 14px;

    &.main-title {
      font-weight: bold;
    }

    &.sub-title {
      font-weight: bold;
    }

    &.indent {
      // 缩进通过内联样式控制
    }
  }
}

// 取值规则弹窗样式
.rule-dialog {
  .rule-content {
    .rule-list {
      margin-bottom: 20px;

      .rule-item {
        display: flex;
        gap: 12px;
        margin-bottom: 12px;
        padding-left: 8px;
        border-left: 3px solid var(--color-info);

        .rule-number {
          flex-shrink: 0;
          width: 20px;
          height: 20px;
          background: var(--color-info);
          color: #fff;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 12px;
          font-weight: bold;
          margin-top: 2px;
        }

        .rule-text {
          flex: 1;
          font-size: 14px;
          line-height: 1.6;
          color: #303133;
        }
      }
    }

    .rule-notice {
      .notice-text {
        font-size: 14px;
        font-weight: bold;
        color: #303133;
        line-height: 1.8;
        margin-bottom: 12px;

        &:last-child {
          margin-bottom: 0;
        }
      }
    }
  }

  .dialog-footer {
    display: flex;
    justify-content: flex-end;
  }
}

.drawer-charts {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.drawer-chart-item {
  min-height: 360px;
  background: #fafafa;
  border-radius: 8px;
  padding: 16px;
}

.drill-link {
  color: var(--text-1);
  cursor: pointer;
  text-decoration: none;

  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}
</style>
