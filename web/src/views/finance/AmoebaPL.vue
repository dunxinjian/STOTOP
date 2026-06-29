<template>
  <div class="page-container amoeba-pl-page">
    <PageHeader title="阿米巴经营报表">
      <template #left>
        <div class="filter-bar">
          <AccountSetSelector style="width: 140px;" />
          <a-select
            v-model:value="templateSelectValue"
            placeholder="选择损益模板"
            :options="templateOptions"
            style="min-width: 160px"
          />
          <a-month-picker
            v-model:value="mainPeriodDayjs"
            placeholder="主期间"
            value-format="YYYYMM"
            :format="'YYYY年MM月'"
            :allow-clear="false"
            style="width: 140px"
          />
          <a-checkbox v-model:checked="includeYoy" class="yoy-checkbox">显示同比</a-checkbox>
        </div>
      </template>
      <template #actions>
        <a-button
          class="toolbar-btn"
          :type="isEditing ? 'primary' : 'default'"
          @click="toggleEditMode"
        >
          <template #icon><EditOutlined /></template>
          {{ isEditing ? '完成编辑' : '编辑手工指标' }}
        </a-button>
        <a-button
          class="toolbar-btn"
          :disabled="!templateId"
          @click="openEstimateDialog"
        >
          <template #icon><DatabaseOutlined /></template>
          暂估数据
        </a-button>
        <a-button class="toolbar-btn" :loading="exportLoading" @click="handleExport">
          <template #icon><DownloadOutlined /></template>
          导出
        </a-button>
      </template>
    </PageHeader>

    <div class="report-body toolbar-adjacent-card">
      <a-alert
        v-if="unmatchedWarnings.length > 0"
        type="warning"
        show-icon
        closable
        class="unmatched-alert"
        :message="`数据完整性提醒：${unmatchedWarnings.join('；')}`"
        description="存在未匹配到任何损益项的数据，报表合计可能少计。可在模板配置页运行科目覆盖率诊断定位缺失科目。"
      />
      <a-spin :spinning="loading" wrapperClassName="report-spin">
        <div class="amoeba-report-layout" :class="{ 'has-indicators': hasIndicators }">
          <!-- 左栏：全局指标分区 -->
          <div v-if="hasIndicators" class="indicator-panel">
            <div class="indicator-panel-header">运营指标</div>
            <table class="indicator-table">
              <thead>
                <tr>
                  <th class="indicator-name-col">指标</th>
                  <th v-for="label in periodLabels" :key="label">{{ label }}</th>
                  <th>环比</th>
                  <th v-if="includeYoy">同比</th>
                </tr>
              </thead>
              <tbody>
                <template v-for="section in indicatorSections" :key="section.sectionName">
                  <tr v-for="item in section.items" :key="item.id"
                      :class="getIndicatorRowClass(item)">
                    <td :style="{ paddingLeft: item.depth * 16 + 'px' }">
                      {{ item.name }}
                      <a-tooltip v-if="item.dataSourceRemark || item.calculationLogic" placement="right">
                        <template #title>
                          <div v-if="item.dataSourceRemark">来源: {{ item.dataSourceRemark }}</div>
                          <div v-if="item.calculationLogic">逻辑: {{ item.calculationLogic }}</div>
                        </template>
                        <InfoCircleOutlined class="info-icon" />
                      </a-tooltip>
                    </td>
                    <template v-if="item.itemCategory !== 'section'">
                      <td v-for="pv in item.periodValues" :key="pv.periodLabel" class="num-cell">
                        {{ formatItemAmount(pv.amount, item) }}
                      </td>
                      <td class="num-cell change-cell">{{ formatPercent(item.momChange) }}</td>
                      <td v-if="includeYoy" class="num-cell change-cell">{{ formatPercent(item.yoyChange) }}</td>
                    </template>
                    <template v-else>
                      <td :colspan="periodLabels.length + 1 + (includeYoy ? 1 : 0)"></td>
                    </template>
                  </tr>
                </template>
              </tbody>
            </table>
          </div>

          <!-- 右栏：损益报表 -->
          <div class="pnl-panel">
            <!-- 报表表格（按方向 Tab 划分） -->
            <div v-if="hasData" class="tab-bar">
          <div class="tab-labels">
            <a-tabs
              v-model:active-key="activeReportTab"
              class="report-direction-tabs"
            >
              <a-tab-pane
                v-for="tab in tabNodes"
                :key="tab.id"
              >
                <template #tab>
                  <span class="dir-tab-label">
                    <span>{{ tab.name }}</span>
                  </span>
                </template>
              </a-tab-pane>
            </a-tabs>
          </div>
          <div class="global-summaries">
            <span v-for="item in globalSummaries" :key="item.id" class="global-summary-item">
              {{ item.name }} {{ formatGlobalSummary(item) }}
            </span>
          </div>
        </div>
        <div v-if="hasData" class="report-table-wrapper">
          <table class="report-table">
            <colgroup>
              <col class="col-name" />
              <col class="col-unit" />
              <col v-for="(_, idx) in periodLabels" :key="`amt-${idx}`" class="col-amount" />
              <col v-for="(_, idx) in periodLabels" :key="`pu-${idx}`" class="col-perunit" />
              <col class="col-mom" />
              <col v-if="includeYoy" class="col-yoy" />
              <col class="col-source" />
              <col class="col-logic" />
            </colgroup>
            <thead>
              <tr class="head-row-1">
                <th rowspan="2" class="th-name">项目</th>
                <th rowspan="2" class="th-unit">单位</th>
                <th
                  v-for="(label, idx) in periodLabels"
                  :key="`ph-${idx}`"
                  colspan="2"
                  class="th-period"
                  :class="{ 'th-period-current': idx === 0 }"
                >
                  {{ label }}
                  <span v-if="idx === 0" class="th-period-tag">当期</span>
                  <span v-else-if="idx === 1" class="th-period-tag">环比期</span>
                  <span v-else class="th-period-tag">同比期</span>
                </th>
                <th rowspan="2" class="th-rate">环比</th>
                <th v-if="includeYoy" rowspan="2" class="th-rate">同比</th>
                <th rowspan="2" class="th-source">来源</th>
                <th rowspan="2" class="th-logic">说明</th>
              </tr>
              <tr class="head-row-2">
                <template v-for="(_, idx) in periodLabels" :key="`sub-${idx}`">
                  <th class="th-amount">金额</th>
                  <th class="th-perunit">单票均</th>
                </template>
              </tr>
            </thead>
            <tbody>
              <template v-if="currentSections.length === 0">
                <tr><td :colspan="totalColumns" style="text-align:center;color:var(--text-3);padding:24px;">当前方向暂无数据</td></tr>
              </template>
              <template v-for="(section, sIdx) in currentSections" :key="`s-${sIdx}`">
                <!-- 利润合计行（边际/合计分区）：不合并列 -->
                <tr v-if="isMarginSection(section)" class="row-margin-total">
                  <td class="cell-name margin-name">{{ section.sectionName }}</td>
                  <td class="cell-unit">元</td>
                  <template v-for="(pv, pIdx) in normalizedPeriodValues(section.sectionTotals)" :key="`mt-${pIdx}`">
                    <td class="cell-amount" :class="amountClass(pv.amount)">
                      {{ formatAmount(pv.amount) }}
                    </td>
                    <td class="cell-perunit">{{ formatPerUnit(pv.perUnitValue) }}</td>
                  </template>
                  <td class="cell-rate" :class="changeClass(computeMomFromTotals(section.sectionTotals))">{{ renderChangeText(computeMomFromTotals(section.sectionTotals)) }}</td>
                  <td v-if="includeYoy" class="cell-rate" :class="changeClass(computeYoyFromTotals(section.sectionTotals))">
                    {{ renderChangeText(computeYoyFromTotals(section.sectionTotals)) }}
                  </td>
                  <td class="cell-source"></td>
                  <td class="cell-logic"></td>
                </tr>

                <!-- 普通分区：标题行 + 明细。depth=0 group（Tab根节点）不渲染标题行，已由Tab栏显示 -->
                <template v-else>
                  <tr v-if="!isTabRootSection(section)" class="row-section-title" @click="toggleSectionCollapse(section)">
                    <td :colspan="totalColumns" class="section-title-cell">
                      <span class="section-toggle-icon" :class="{ expanded: !collapsedSections.has(sectionKey(section)) }">&#9654;</span>
                      {{ section.sectionName }}
                    </td>
                  </tr>
                  <template v-if="isTabRootSection(section) || !collapsedSections.has(sectionKey(section))">
                  <template v-for="item in getVisibleItems(section.items)" :key="item.id">
                    <tr
                      class="row-item"
                      :class="rowItemClasses(item)"
                      @click="onItemRowClick(item)"
                    >
                      <td class="cell-name">
                        <span
                          class="indent"
                          :style="{ paddingLeft: Math.max((item.depth || 1) - 1, 0) * 16 + 'px' }"
                        >
                          <span class="expand-icon" v-if="canDrill(item)" :class="{ expanded: expandedItems.includes(item.id) }">&#9654;</span>
                          <span class="expand-icon" v-else-if="item.nodeRole === 'group' && parentItemIds.has(item.id)" :class="{ expanded: !collapsedItemIds.has(item.id) }">&#9654;</span>
                          <span class="expand-icon placeholder" v-else></span>
                          <span class="item-name-text">{{ item.name }}</span>
                          <a-spin v-if="loadingItems.includes(item.id)" size="small" style="margin-left: 6px" />
                        </span>
                      </td>
                      <td class="cell-unit">{{ item.unit || '元' }}</td>
                      <template
                        v-for="(pv, pIdx) in normalizedPeriodValues(item.periodValues)"
                        :key="`pv-${item.id}-${pIdx}`"
                      >
                        <td class="cell-amount" :class="amountClass(pv.amount)">
                          <a-input-number
                            v-if="isEditing && item.isManualEntry && pIdx === 0"
                            :value="pv.amount"
                            :precision="2"
                            :controls="false"
                            class="cell-input"
                            @change="(v: any) => onAmountChange(item, v)"
                            @blur="onManualSave(item)"
                            @keyup.enter="onManualSave(item)"
                          />
                          <template v-else>{{ formatItemAmount(pv.amount, item) }}</template>
                        </td>
                        <td class="cell-perunit">
                          <a-input-number
                            v-if="isEditing && item.isManualEntry && pIdx === 0"
                            :value="pv.perUnitValue"
                            :precision="4"
                            :controls="false"
                            class="cell-input"
                            @change="(v: any) => onPerUnitChange(item, v)"
                            @blur="onManualSave(item)"
                            @keyup.enter="onManualSave(item)"
                          />
                          <template v-else>{{ formatPerUnit(pv.perUnitValue) }}</template>
                        </td>
                      </template>
                      <td class="cell-rate" :class="changeClass(item.momChange)">{{ renderChangeText(item.momChange) }}</td>
                      <td v-if="includeYoy" class="cell-rate" :class="changeClass(item.yoyChange)">{{ renderChangeText(item.yoyChange) }}</td>
                      <td class="cell-source">
                        <a-tooltip v-if="item.dataSourceRemark" title="查看数据来源">
                          <a-button
                            type="text"
                            size="small"
                            class="detail-icon-btn source-icon-btn"
                            @click.stop="openSourceDrawer(item)"
                          >
                            <template #icon><DatabaseOutlined /></template>
                          </a-button>
                        </a-tooltip>
                        <span v-else class="detail-empty">—</span>
                      </td>
                      <td class="cell-logic">
                        <a-tooltip v-if="item.calculationLogic" title="查看逻辑说明">
                          <a-button
                            type="text"
                            size="small"
                            class="detail-icon-btn"
                            @click.stop="openLogicDrawer(item)"
                          >
                            <template #icon><InfoCircleOutlined /></template>
                          </a-button>
                        </a-tooltip>
                        <span v-else class="detail-empty">—</span>
                      </td>
                    </tr>
                    <!-- 下钻展开行 -->
                    <template v-if="expandedItems.includes(item.id)">
                      <tr
                        v-for="(d, dIdx) in getDrillRows(item.id)"
                        :key="`d-${item.id}-${dIdx}`"
                        class="row-drill"
                      >
                        <td class="cell-name">
                          <span :style="{ paddingLeft: ((item.depth || 1) + 1) * 16 + 'px' }">
                            <span class="drill-tag">└</span>
                            {{ d.name }}
                          </span>
                        </td>
                        <td class="cell-unit"></td>
                        <td class="cell-amount" :class="amountClass(d.amount)">
                          {{ formatAmount(d.amount) }}
                        </td>
                        <td class="cell-perunit"></td>
                        <template v-for="(_, pIdx) in periodLabels.slice(1)" :key="`drill-skip-${pIdx}`">
                          <td class="cell-amount"></td>
                          <td class="cell-perunit"></td>
                        </template>
                        <td class="cell-rate"></td>
                        <td v-if="includeYoy" class="cell-rate"></td>
                        <td class="cell-source">
                          <a-tooltip v-if="d.source" title="查看数据来源">
                            <a-button
                              type="text"
                              size="small"
                              class="detail-icon-btn source-icon-btn"
                              @click.stop="openSourceDrawer(d)"
                            >
                              <template #icon><DatabaseOutlined /></template>
                            </a-button>
                          </a-tooltip>
                          <span v-else class="detail-empty">—</span>
                        </td>
                        <td class="cell-logic">
                          <a-tooltip v-if="d.logic" title="查看逻辑说明">
                            <a-button
                              type="text"
                              size="small"
                              class="detail-icon-btn"
                              @click.stop="openLogicDrawer(d)"
                            >
                              <template #icon><InfoCircleOutlined /></template>
                            </a-button>
                          </a-tooltip>
                          <span v-else class="detail-empty">—</span>
                        </td>
                      </tr>
                    </template>
                  </template>
                  </template>
                </template>
              </template>
            </tbody>
            <!-- Tab利润行：后端 FormulaValue 仅为当期值，只在当期列展示，避免被误读为各期同值 -->
            <tfoot v-if="currentTabFormulaValue != null">
              <tr class="row-tab-formula">
                <td class="cell-name formula-name">★ {{ currentTabLabel }}利润</td>
                <td class="cell-unit">元</td>
                <td class="cell-amount formula-amount" :class="amountClass(currentTabFormulaValue)" colspan="2">
                  {{ formatAmount(currentTabFormulaValue) }}
                </td>
                <template v-for="(_, pIdx) in periodLabels.slice(1)" :key="`tf-${pIdx}`">
                  <td class="cell-amount change-null" colspan="2">—</td>
                </template>
                <td class="cell-rate"></td>
                <td v-if="includeYoy" class="cell-rate"></td>
                <td class="cell-source"></td>
                <td class="cell-logic">
                  <a-tooltip title="查看逻辑说明">
                    <a-button
                      type="text"
                      size="small"
                      class="detail-icon-btn"
                      @click.stop="openLogicDrawer({ name: `${currentTabLabel}利润`, calculationLogic: '公式计算' })"
                    >
                      <template #icon><InfoCircleOutlined /></template>
                    </a-button>
                  </a-tooltip>
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
        <div v-else-if="!loading" class="empty-guide">
          <div class="empty-icon">📊</div>
          <div class="empty-title">暂无报表数据</div>
          <div class="empty-steps">
            <span>1. 选择账套</span>
            <span class="step-arrow">→</span>
            <span>2. 选择损益模板</span>
            <span class="step-arrow">→</span>
            <span>3. 选择期间</span>
          </div>
        </div>
          </div>
        </div>
      </a-spin>
    </div>
    <a-drawer
      v-model:open="logicDrawerOpen"
      :title="activeLogic.drawerTitle"
      placement="right"
      width="360"
      :destroy-on-close="true"
    >
      <div class="logic-drawer">
        <div class="logic-drawer__label">项目</div>
        <div class="logic-drawer__title">{{ activeLogic.title }}</div>
        <template v-if="activeLogic.source">
          <div class="logic-drawer__label">数据来源</div>
          <div class="logic-drawer__meta">{{ activeLogic.source }}</div>
        </template>
        <template v-if="activeLogic.unit">
          <div class="logic-drawer__label">单位</div>
          <div class="logic-drawer__meta">{{ activeLogic.unit }}</div>
        </template>
        <div class="logic-drawer__label">{{ activeLogic.contentLabel }}</div>
        <div class="logic-drawer__content">{{ activeLogic.logic || activeLogic.emptyText }}</div>
      </div>
    </a-drawer>

    <EstimateDataDialog
      v-model:open="estimateDialogOpen"
      :template-id="templateId"
      :org-id="orgContextStore.currentOrgId || 0"
      :account-set-id="accountSetStore.currentAccountSetId || 0"
      :period="currentPeriodStr"
      :template-name="currentTemplateName"
      :period-label="currentPeriodLabel"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { DatabaseOutlined, DownloadOutlined, EditOutlined, InfoCircleOutlined } from '@ant-design/icons-vue'
import dayjs, { Dayjs } from 'dayjs'
import {
  getAmoebaMultiPeriodReport,
  getAmoebaManualData,
  saveAmoebaManualData,
  getAmoebaPLItemDetail,
  getBillingDrillDown,
  getDepreciationDrillDown,
  getAmoebaPLTemplates,
  isOutboundRevenueItem,
  isDepreciationItem,
  type AmoebaMultiPeriodResponse,
  type TabNode,
  type GlobalSummaryNode,
  type SectionData,
  type MultiPeriodPLItemData,
  type PeriodValue,
  type SaveManualDataRequest,
} from '@/api/finance'

import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import EstimateDataDialog from './components/EstimateDataDialog.vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()

// ==================== 工具栏状态 ====================
const templateId = ref<number>(0)
const templateOptions = ref<{ value: number; label: string }[]>([])
// 0 表示未选择：映射为 undefined 让下拉显示占位符而非裸数字
const templateSelectValue = computed<any>({
  get: () => templateId.value || undefined,
  set: v => { templateId.value = Number(v) || 0 },
})
const mainPeriodDayjs = ref<Dayjs | string>(dayjs().format('YYYYMM'))

const includeYoy = ref<boolean>(false)
const isEditing = ref<boolean>(false)
const exportLoading = ref<boolean>(false)
const estimateDialogOpen = ref<boolean>(false)
const loading = ref<boolean>(false)

// ==================== 报表数据 ====================
const sections = ref<SectionData[]>([])
const indicatorSections = ref<SectionData[]>([])
const periodLabels = ref<string[]>([])

// ==================== 方向 Tab 化 ====================
const activeReportTab = ref<number>(0)
const tabNodes = ref<TabNode[]>([])
const globalSummaries = ref<GlobalSummaryNode[]>([])
const unmatchedWarnings = ref<string[]>([])

// 当前 Tab 下的 sections（按 tabAncestorId 过滤）
const currentSections = computed(() => {
  return sections.value.filter(s => s.tabAncestorId === activeReportTab.value)
})

// 当前 Tab 的标签
const currentTabLabel = computed(() => {
  const tab = tabNodes.value.find(t => t.id === activeReportTab.value)
  return tab?.name || ''
})

// 当前 Tab 的公式计算值
const currentTabFormulaValue = computed<number | null | undefined>(() => {
  const tab = tabNodes.value.find(t => t.id === activeReportTab.value)
  return tab?.formulaValue
})

// activeReportTab 合法性保护
watch(tabNodes, (tabs) => {
  if (!tabs.find(t => t.id === activeReportTab.value)) {
    activeReportTab.value = tabs[0]?.id || 0
  }
}, { immediate: true })

// 下钻状态
const expandedItems = ref<number[]>([])
const loadingItems = ref<number[]>([])
const drillRowsMap = ref<Record<number, DrillRow[]>>({})
const outboundCache = ref<Record<number, boolean>>({})
const depreciationCache = ref<Record<number, boolean>>({})

// ==================== 折叠/展开状态 ====================
const collapsedSections = ref<Set<string>>(new Set())  // key = sectionName|tabAncestorId
const collapsedItemIds = ref<Set<number>>(new Set())   // key = item.id

function sectionKey(section: SectionData): string {
  return `${section.sectionName}|${section.tabAncestorId ?? ''}`
}

/** 有子项的 item.id 集合（从 sections 数据计算） */
const parentItemIds = computed<Set<number>>(() => {
  const parents = new Set<number>()
  for (const sec of sections.value) {
    const flat = flattenItems(sec.items || [])
    for (let i = 0; i < flat.length - 1; i++) {
      if ((flat[i + 1].depth || 0) > (flat[i].depth || 0)) {
        parents.add(flat[i].id)
      }
    }
  }
  return parents
})

/** 根据折叠状态过滤，返回当前可见条目 */
function getVisibleItems(items: MultiPeriodPLItemData[]): MultiPeriodPLItemData[] {
  const flat = flattenItems(items)
  const result: MultiPeriodPLItemData[] = []
  let hiddenBelow = -1
  for (const item of flat) {
    const d = item.depth || 0
    if (hiddenBelow >= 0 && d <= hiddenBelow) hiddenBelow = -1
    if (hiddenBelow >= 0) continue
    result.push(item)
    if (item.nodeRole === 'group' && collapsedItemIds.value.has(item.id)) {
      hiddenBelow = d
    }
  }
  return result
}

function toggleSectionCollapse(section: SectionData) {
  const key = sectionKey(section)
  const s = new Set(collapsedSections.value)
  if (s.has(key)) s.delete(key)
  else s.add(key)
  collapsedSections.value = s
}

function toggleItemCollapse(itemId: number) {
  const s = new Set(collapsedItemIds.value)
  if (s.has(itemId)) s.delete(itemId)
  else s.add(itemId)
  collapsedItemIds.value = s
}

interface DrillRow {
  name: string
  amount: number
  source?: string
  logic?: string
}

interface LogicDrawerPayload {
  name?: string
  title?: string
  calculationLogic?: string | null
  logic?: string | null
  dataSourceRemark?: string | null
  source?: string | null
  unit?: string | null
}

const logicDrawerOpen = ref(false)
const activeLogic = ref({
  drawerTitle: '逻辑说明',
  title: '',
  logic: '',
  source: '',
  unit: '',
  contentLabel: '说明内容',
  emptyText: '暂无逻辑说明',
})

function openLogicDrawer(payload: LogicDrawerPayload) {
  const logic = payload.calculationLogic ?? payload.logic ?? ''
  if (!logic) return
  activeLogic.value = {
    drawerTitle: '逻辑说明',
    title: payload.name ?? payload.title ?? '未命名项目',
    logic,
    source: payload.dataSourceRemark ?? payload.source ?? '',
    unit: payload.unit ?? '',
    contentLabel: '说明内容',
    emptyText: '暂无逻辑说明',
  }
  logicDrawerOpen.value = true
}

function openSourceDrawer(payload: LogicDrawerPayload) {
  const source = payload.dataSourceRemark ?? payload.source ?? ''
  if (!source) return
  activeLogic.value = {
    drawerTitle: '数据来源',
    title: payload.name ?? payload.title ?? '未命名项目',
    logic: source,
    source: '',
    unit: payload.unit ?? '',
    contentLabel: '来源内容',
    emptyText: '暂无数据来源',
  }
  logicDrawerOpen.value = true
}

// 计算属性
const hasData = computed(() => sections.value.length > 0)

const hasIndicators = computed(() => {
  return indicatorSections.value && indicatorSections.value.length > 0
})

const totalColumns = computed(() => {
  // name + unit + 2*periods + mom + (yoy?) + source + logic
  return 2 + periodLabels.value.length * 2 + 1 + (includeYoy.value ? 1 : 0) + 2
})

const currentPeriodStr = computed<string>(() => {
  const v = mainPeriodDayjs.value
  if (!v) return dayjs().format('YYYYMM')
  if (typeof v === 'string') return v
  return (v as Dayjs).format('YYYYMM')
})

const currentTemplateName = computed<string>(() => {
  const opt = templateOptions.value.find((o) => o.value === templateId.value)
  return opt?.label || ''
})

const currentPeriodLabel = computed<string>(() => {
  const p = currentPeriodStr.value
  if (p.length === 6) return `${p.slice(0, 4)}年${p.slice(4, 6)}月`
  return p
})

function openEstimateDialog() {
  if (!templateId.value) {
    message.warning('请先选择损益模板')
    return
  }
  if (!orgContextStore.currentOrgId) {
    message.warning('请先选择组织')
    return
  }
  estimateDialogOpen.value = true
}

// 暂估数据参与报表计算（AggregateEstimateData），关闭管理弹窗后刷新报表以反映改动
watch(estimateDialogOpen, (open, wasOpen) => {
  if (!open && wasOpen && templateId.value) {
    fetchReport()
  }
})

// ==================== 生命周期 ====================
onMounted(async () => {
  // loadTemplates 选中默认模板后，templateId watch 会触发首次 fetchReport，无需在此重复请求
  await loadTemplates()
})

watch(
  () => accountSetStore.currentAccountSetId,
  async () => {
    resetDrillState()
    // 模板按账套隔离：切换账套后旧模板 ID 对新账套无效，必须重置再加载
    templateId.value = 0
    templateOptions.value = []
    await loadTemplates()
  }
)

watch(
  () => orgContextStore.currentOrgId,
  () => {
    resetDrillState()
    if (templateId.value) fetchReport()
  }
)

watch(
  [templateId, mainPeriodDayjs, includeYoy],
  () => {
    resetDrillState()
    if (templateId.value) {
      fetchReport()
    } else {
      // 置空时同时作废在途请求，防止旧响应迟到后把已清空的数据写回
      fetchSeq++
      clearReportData()
      loading.value = false
    }
  }
)

// ==================== 选项加载 ====================
async function loadTemplates() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) return
  try {
    const res: any = await getAmoebaPLTemplates({ accountSetId })
    // 防御：响应返回时账套可能已再次切换，丢弃过期结果
    if (accountSetId !== accountSetStore.currentAccountSetId) return
    const templates = Array.isArray(res) ? res : res?.items || []
    templateOptions.value = templates.map((t: any) => ({
      value: t.id || t.fid,
      label: t.name || t.fName,
    }))
    if (templates.length > 0 && !templateId.value) {
      templateId.value = templates[0].id || templates[0].fid
    }
  } catch (e) {
    console.error('加载损益模板失败', e)
  }
}

// ==================== 取数 ====================
// 请求序号守卫：切换期间/模板/组织/账套时并发请求乱序返回，旧响应不得覆盖新数据
let fetchSeq = 0

function clearReportData() {
  sections.value = []
  indicatorSections.value = []
  periodLabels.value = []
  tabNodes.value = []
  globalSummaries.value = []
  unmatchedWarnings.value = []
}

async function fetchReport() {
  if (!accountSetStore.currentAccountSetId || !templateId.value) return
  if (!orgContextStore.currentOrgId) return
  const seq = ++fetchSeq
  loading.value = true
  try {
    const res: AmoebaMultiPeriodResponse = await getAmoebaMultiPeriodReport({
      templateId: templateId.value,
      orgId: orgContextStore.currentOrgId,
      accountSetId: accountSetStore.currentAccountSetId,
      mainPeriod: currentPeriodStr.value,
      includeYoy: includeYoy.value,
    })
    if (seq !== fetchSeq) return // 已有更新的请求，丢弃过期响应
    sections.value = res?.sections || []
    indicatorSections.value = res?.indicatorSections || []
    periodLabels.value = res?.periodLabels || []
    tabNodes.value = res?.tabNodes || []
    globalSummaries.value = res?.globalSummaries || []
    unmatchedWarnings.value = res?.unmatchedWarnings || []
    // 初始化折叠状态：后端 depth 从 1 起（Tab 直接子级=1）——
    // 顶层板块（depth=1）保持展开，仅 depth >= 2 的嵌套子板块默认折叠
    collapsedSections.value = new Set()
    const defaultCollapsed = new Set<number>()
    for (const sec of sections.value) {
      for (const it of flattenItems(sec.items || [])) {
        if (it.nodeRole === 'group' && (it.depth || 0) >= 2) {
          defaultCollapsed.add(it.id)
        }
      }
    }
    collapsedItemIds.value = defaultCollapsed
  } catch (e: any) {
    if (seq !== fetchSeq) return
    console.error('加载多期对比报表失败', e)
    message.error(e?.message || '加载报表失败')
    // 整体清空，避免左栏指标/Tab/全局汇总残留上一组织或期间的数据
    clearReportData()
  } finally {
    if (seq === fetchSeq) loading.value = false
  }
}

function resetDrillState() {
  expandedItems.value = []
  drillRowsMap.value = {}
}

// ==================== 行渲染辅助 ====================
function isMarginSection(section: SectionData): boolean {
  const name = section.sectionName || ''
  return /合计|利润|边际/.test(name) && (!section.items || section.items.length === 0)
}

/**
 * 判断 section 是否对应 Tab 根节点（depth=0 group）
 * 设计规则：depth=0 group 已作为 Tab 显示，列表中不再渲染其标题行，直接展示子节点
 */
function isTabRootSection(section: SectionData): boolean {
  if (section.tabAncestorId == null) return false
  const tab = tabNodes.value.find(t => t.id === section.tabAncestorId)
  if (!tab) return false
  return tab.name === section.sectionName
}

function flattenItems(items: MultiPeriodPLItemData[]): MultiPeriodPLItemData[] {
  // 后端返回扁平数组，前端按 depth 缩进；如有 children 字段，递归展平
  const out: MultiPeriodPLItemData[] = []
  for (const it of items) {
    out.push(it)
    const children = (it as any).children as MultiPeriodPLItemData[] | undefined
    if (children && children.length > 0) {
      out.push(...flattenItems(children))
    }
  }
  return out
}

/**
 * 是否可下钻：优先使用后端契约字段 canDrillDown
 * （revenue/cost + 系统来源 + 非手工才可下钻），缺失时回退旧推断
 */
function canDrill(item: MultiPeriodPLItemData): boolean {
  if (item.canDrillDown != null) return item.canDrillDown
  return item.nodeRole === 'data' && !item.isManualEntry
}

function rowItemClasses(item: MultiPeriodPLItemData): Record<string, boolean> {
  return {
    // 后端 depth 从 1 起（Tab 直接子级=1），depth>1 才是嵌套子分组
    'row-section-subtotal': item.nodeRole === 'group' && (item.depth || 0) > 1,
    'row-detail': item.nodeRole === 'data',
    'row-formula': item.nodeRole === 'formula',
    'row-indicator': item.nodeRole === 'indicator',
    'row-clickable': canDrill(item),
    'row-section-collapsible': item.nodeRole === 'group' && parentItemIds.value.has(item.id),
    'row-manual': item.isManualEntry,
  }
}

function normalizedPeriodValues(periodVals?: PeriodValue[]): PeriodValue[] {
  const labels = periodLabels.value
  if (!labels.length) return periodVals || []
  const out: PeriodValue[] = []
  for (let i = 0; i < labels.length; i++) {
    const found = periodVals?.find(p => p.periodLabel === labels[i])
    out.push(
      found || {
        periodLabel: labels[i],
        amount: 0,
        perUnitValue: undefined,
      }
    )
  }
  return out
}

function computeMomFromTotals(totals?: PeriodValue[]): number | undefined {
  if (!totals || totals.length < 2) return undefined
  const norm = normalizedPeriodValues(totals)
  const cur = norm[0]?.amount
  const prev = norm[1]?.amount
  if (cur == null || prev == null || prev === 0) return undefined
  return ((cur - prev) / Math.abs(prev)) * 100
}

function computeYoyFromTotals(totals?: PeriodValue[]): number | undefined {
  if (!totals || totals.length < 3) return undefined
  const norm = normalizedPeriodValues(totals)
  const cur = norm[0]?.amount
  const yoy = norm[2]?.amount
  if (cur == null || yoy == null || yoy === 0) return undefined
  return ((cur - yoy) / Math.abs(yoy)) * 100
}

// ==================== 格式化 ====================
// 整数型计数单位（精确匹配，避免"件·人·日"等复合单位误判）
const INTEGER_UNITS = ['票', '件', '单', '人', '次', '个']
function isIntegerUnit(unit: string | null | undefined): boolean {
  if (!unit) return false
  return INTEGER_UNITS.includes(unit.trim())
}

// 获取有效小数位数：显式配置（DecimalPlaces）优先于单位推断；
// 未配置时按契约"按单位自动判断，默认 2 位"，仅计数类单位取整
function getEffectiveDecimals(item: { unit?: string | null; decimalPlaces?: number | null }): number {
  if (item.decimalPlaces != null) return item.decimalPlaces
  return isIntegerUnit(item.unit) ? 0 : 2
}

// 根据项目单位格式化数值
function formatItemAmount(val: number | null | undefined, item: { unit?: string | null; decimalPlaces?: number | null }): string {
  if (val === null || val === undefined) return '—'
  const decimals = getEffectiveDecimals(item)
  return val.toLocaleString('zh-CN', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
}

function formatAmount(val: number | null | undefined): string {
  if (val === null || val === undefined) return '—'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatPerUnit(val: number | null | undefined): string {
  if (val === null || val === undefined) return '—'
  return Number(val).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 4 })
}

function amountClass(val: number | null | undefined): string {
  if (val === null || val === undefined) return 'zero-amount'
  if (val === 0) return 'zero-amount'
  return val > 0 ? 'positive' : 'negative'
}

function renderChangeText(val: number | null | undefined): string {
  if (val === null || val === undefined) return '—'
  if (val === 0) return '0.00%'
  const arrow = val > 0 ? '↑' : '↓'
  return `${arrow} ${Math.abs(val).toFixed(2)}%`
}

// 小写工具：根据百分比值生成 class，用于颜色
function changeClass(val: number | null | undefined): string {
  if (val === null || val === undefined) return 'change-null'
  if (val > 0) return 'change-up'
  if (val < 0) return 'change-down'
  return 'change-zero'
}

// 在模板中通过函数调用更直接：由于已用 cell-rate，我们再追加内联计算的 class
// 直接在 td 上挂 :class — 改为：
function rateCellClass(val: number | null | undefined): string {
  return changeClass(val)
}

function getIndicatorRowClass(item: MultiPeriodPLItemData): string {
  if (item.itemCategory === 'section' || item.nodeRole === 'group') {
    return 'indicator-group-row'
  }
  return 'indicator-item-row'
}

function formatNumber(val: number | null | undefined): string {
  if (val === null || val === undefined) return '—'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatPercent(val: number | null | undefined): string {
  if (val === null || val === undefined) return '—'
  return `${val > 0 ? '+' : ''}${val.toFixed(2)}%`
}

// 全局汇总行：按 Unit 格式化——比率类显示 %，金额类带千分位与 ¥，其他单位原样后缀
function formatGlobalSummary(item: GlobalSummaryNode): string {
  if (item.value == null) return '--'
  const unit = (item.unit || '').trim()
  if (unit === '%' || unit.includes('率')) {
    return `${item.value.toFixed(2)}%`
  }
  const text = item.value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  if (!unit || unit === '元') return `¥${text}`
  return `${text} ${unit}`
}

// ==================== 编辑模式 ====================
function toggleEditMode() {
  if (isEditing.value) {
    isEditing.value = false
    message.success('已退出编辑模式')
    // 小计/公式行/单票均/全局汇总均由服务端计算：退出编辑后整体刷新，
    // 避免界面合计与刚填报的明细自相矛盾（编辑期间不刷新，防止覆盖正在输入的单元格）
    fetchReport()
  } else {
    isEditing.value = true
    message.info('已进入编辑模式：可填报手工指标，失焦或回车自动保存')
  }
}

function onAmountChange(item: MultiPeriodPLItemData, v: any) {
  const norm = normalizedPeriodValues(item.periodValues)
  if (norm[0]) {
    norm[0].amount = Number(v) || 0
    item.periodValues = norm
  }
}

function onPerUnitChange(item: MultiPeriodPLItemData, v: any) {
  const norm = normalizedPeriodValues(item.periodValues)
  if (norm[0]) {
    norm[0].perUnitValue = v == null ? undefined : Number(v)
    item.periodValues = norm
  }
}

// 去重：blur 与回车会对同一行触发两次保存；按"项目+期间+值"判断是否真有变化
const lastSavedManualPayload = new Map<number, string>()

async function onManualSave(item: MultiPeriodPLItemData) {
  if (!item.isManualEntry) return
  if (!orgContextStore.currentOrgId) return
  const norm = normalizedPeriodValues(item.periodValues)
  const current = norm[0]
  if (!current) return
  const payload: SaveManualDataRequest = {
    templateId: templateId.value,
    orgId: orgContextStore.currentOrgId,
    period: currentPeriodStr.value,
    items: [
      {
        plItemId: item.id,
        amount: Number(current.amount) || 0,
        perUnitValue: current.perUnitValue,
      },
    ],
  }
  const payloadKey = `${currentPeriodStr.value}|${payload.items[0].amount}|${payload.items[0].perUnitValue ?? ''}`
  if (lastSavedManualPayload.get(item.id) === payloadKey) return
  try {
    await saveAmoebaManualData(payload)
    lastSavedManualPayload.set(item.id, payloadKey)
    message.success(`【${item.name}】已保存`)
  } catch (e: any) {
    console.error('保存手工填报失败', e)
    message.error(e?.message || '保存失败')
  }
}

// 静默触发拉取手工填报数据（暂未在UI单独使用，保留以备需要）
async function _loadManualData() {
  if (!orgContextStore.currentOrgId || !templateId.value) return
  try {
    await getAmoebaManualData({
      templateId: templateId.value,
      orgId: orgContextStore.currentOrgId,
      period: currentPeriodStr.value,
    })
  } catch {
    /* 忽略 */
  }
}
void _loadManualData

// ==================== 下钻 ====================
async function onItemRowClick(item: MultiPeriodPLItemData) {
  // 板块项：点击切换折叠/展开
  if (item.nodeRole === 'group') {
    if (parentItemIds.value.has(item.id)) toggleItemCollapse(item.id)
    return
  }
  // 可下钻判定以后端 canDrillDown 契约为准
  if (!canDrill(item)) return
  if (isEditing.value) return
  const id = item.id
  // 加载中再次点击不重复发起请求
  if (loadingItems.value.includes(id)) return
  if (expandedItems.value.includes(id)) {
    expandedItems.value = expandedItems.value.filter(x => x !== id)
    return
  }
  // 已加载过的复用缓存
  if (drillRowsMap.value[id]) {
    expandedItems.value.push(id)
    return
  }
  loadingItems.value.push(id)
  try {
    const ym = currentPeriodStr.value
    const startDate = `${ym.slice(0, 4)}-${ym.slice(4, 6)}-01`
    const endDate = dayjs(startDate).endOf('month').format('YYYY-MM-DD')
    const accountSetId = accountSetStore.currentAccountSetId
    if (outboundCache.value[id] === undefined) {
      try {
        outboundCache.value[id] = await isOutboundRevenueItem(id)
      } catch {
        outboundCache.value[id] = false
      }
    }
    if (depreciationCache.value[id] === undefined) {
      try {
        depreciationCache.value[id] = await isDepreciationItem(id)
      } catch {
        depreciationCache.value[id] = false
      }
    }
    let rows: DrillRow[] = []
    if (outboundCache.value[id]) {
      const res: any = await getBillingDrillDown({
        plItemId: id,
        unitIds: undefined,
        startDate,
        endDate,
        accountSetId,
      })
      const groups = res?.groups || []
      for (const g of groups) {
        rows.push({ name: `${g.typeName} 小计`, amount: g.subTotal, source: '出港计费' })
        for (const c of g.clients || []) {
          rows.push({
            name: `  ${c.clientId || ''} ${c.clientName || ''}`.trim(),
            amount: c.amount,
            source: '出港计费',
          })
        }
      }
    } else if (depreciationCache.value[id]) {
      const res: any = await getDepreciationDrillDown({
        plItemId: id,
        startDate,
        endDate,
        accountSetId,
      })
      for (const a of res?.assets || []) {
        rows.push({
          name: `${a.assetName} (${a.assetCode})`,
          amount: a.periodDepreciation,
          source: a.department || '资产折旧',
        })
      }
    } else {
      const res: any = await getAmoebaPLItemDetail({
        templateId: templateId.value,
        accountSetId,
        plItemId: id,
        startDate,
        endDate,
        unitIds: undefined,
      })
      for (const acc of res?.accounts || []) {
        rows.push({
          name: `${acc.accountCode || ''} ${acc.accountName || ''}`.trim(),
          amount: acc.amount,
          source: '科目汇总',
          logic: `共 ${acc.entries?.length || 0} 条分录`,
        })
      }
    }
    drillRowsMap.value[id] = rows
    expandedItems.value.push(id)
  } catch (e: any) {
    console.error('下钻失败', e)
    message.error(e?.message || '下钻失败')
  } finally {
    loadingItems.value = loadingItems.value.filter(x => x !== id)
  }
}

function getDrillRows(itemId: number): DrillRow[] {
  return drillRowsMap.value[itemId] || []
}

// ==================== 导出 ====================
function handleExport() {
  if (!hasData.value) {
    message.warning('暂无数据可导出')
    return
  }
  exportLoading.value = true
  try {
    const html = buildExportHtml()
    const blob = new Blob(
      ['\uFEFF<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40"><head><meta charset="UTF-8" /></head><body>' +
        html +
        '</body></html>'],
      { type: 'application/vnd.ms-excel;charset=utf-8;' }
    )
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `阿米巴经营报表_${currentPeriodStr.value}.xls`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(link.href)
    message.success('导出成功')
  } catch (e: any) {
    console.error('导出失败', e)
    message.error('导出失败')
  } finally {
    exportLoading.value = false
  }
}

function buildExportHtml(): string {
  const cols = totalColumns.value
  const ph = periodLabels.value
  let h = '<table border="1" style="border-collapse:collapse;font-family:Arial,微软雅黑;font-size:12px;">'
  // 第一行表头
  // 以下导出文档内联样式使用字面量 hex：CSS 变量在独立 XLS 文件中无法解析，属导出格式豁免色
  h += '<thead><tr style="background:#f0f0f0;font-weight:bold;">' // 导出文档内联样式豁免色
  h += '<th rowspan="2">项目</th><th rowspan="2">单位</th>'
  for (let i = 0; i < ph.length; i++) {
    const tag = i === 0 ? '当期' : i === 1 ? '环比期' : '同比期'
    h += `<th colspan="2">${ph[i]}（${tag}）</th>`
  }
  h += '<th rowspan="2">环比</th>'
  if (includeYoy.value) h += '<th rowspan="2">同比</th>'
  h += '<th rowspan="2">数据来源</th><th rowspan="2">逻辑说明</th>'
  h += '</tr><tr style="background:#f0f0f0;font-weight:bold;">' // 导出文档内联样式豁免色
  for (let i = 0; i < ph.length; i++) {
    h += '<th>金额</th><th>单票均</th>'
  }
  h += '</tr></thead><tbody>'
  for (const section of sections.value) {
    if (isMarginSection(section)) {
      const norm = normalizedPeriodValues(section.sectionTotals)
      h += '<tr style="background:#d9edf7;font-weight:bold;">' // 导出文档内联样式豁免色
      h += `<td>${escapeHtml(section.sectionName)}</td><td>元</td>`
      for (const pv of norm) {
        h += `<td style="text-align:right;">${formatAmount(pv.amount)}</td><td style="text-align:right;">${formatPerUnit(pv.perUnitValue)}</td>`
      }
      h += `<td>${renderChangeText(computeMomFromTotals(section.sectionTotals))}</td>`
      if (includeYoy.value) h += `<td>${renderChangeText(computeYoyFromTotals(section.sectionTotals))}</td>`
      h += '<td></td><td></td></tr>'
    } else {
      h += `<tr style="background:#f0ad4e;color:#fff;font-weight:bold;"><td colspan="${cols}">${escapeHtml(section.sectionName)}</td></tr>` // 导出文档内联样式豁免色
      for (const item of flattenItems(section.items)) {
        const isSub = item.nodeRole === 'group' && (item.depth || 0) > 0
        const rowStyle = isSub ? 'font-weight:bold;background:#fafafa;' : '' // 导出文档内联样式豁免色
        const indent = '&nbsp;'.repeat((item.depth || 0) * 4)
        h += `<tr style="${rowStyle}">`
        h += `<td>${indent}${escapeHtml(item.name)}</td><td>${escapeHtml(item.unit || '元')}</td>`
        const norm = normalizedPeriodValues(item.periodValues)
        for (const pv of norm) {
          h += `<td style="text-align:right;">${formatItemAmount(pv.amount, item)}</td><td style="text-align:right;">${formatPerUnit(pv.perUnitValue)}</td>`
        }
        h += `<td>${renderChangeText(item.momChange)}</td>`
        if (includeYoy.value) h += `<td>${renderChangeText(item.yoyChange)}</td>`
        h += `<td>${escapeHtml(item.dataSourceRemark || '')}</td><td>${escapeHtml(item.calculationLogic || '')}</td>`
        h += '</tr>'
      }
    }
  }
  h += '</tbody></table>'
  return h
}

function escapeHtml(s: string): string {
  if (!s) return ''
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}
</script>

<style scoped lang="scss">
.page-container.amoeba-pl-page {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  height: 100%;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;

  :deep(.ant-select),
  :deep(.ant-picker),
  :deep(.ant-btn) {
    height: 32px;
  }

  :deep(.ant-select-selector) {
    height: 32px !important;
    line-height: 30px;
  }

  .yoy-checkbox {
    margin-left: 4px;
    color: var(--text-2);
    font-size: 13px;
  }
}

.report-body {
  flex: 1;
  min-height: 0;
  background: var(--bg-card);
  padding: 4px 16px 16px;
  box-shadow: var(--shadow-sm);
  overflow: auto;
  display: flex;
  flex-direction: column;
}

.unmatched-alert {
  margin: 8px 0 4px;
  flex-shrink: 0;
}

.report-spin {
  flex: 1;
  min-height: 0;
}

// ==================== 空状态引导 ====================
.empty-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  color: var(--text-3);

  .empty-icon {
    font-size: 48px;
    margin-bottom: 12px;
    opacity: 0.6;
  }

  .empty-title {
    font-size: 15px;
    color: var(--text-2);
    margin-bottom: 16px;
  }

  .empty-steps {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 13px;
    color: var(--text-3);

    .step-arrow {
      color: var(--text-3);
    }
  }
}

// ==================== 表格 ====================
.report-table-wrapper {
  width: 100%;
  overflow-x: auto;
}

// ==================== 方向 Tab ====================
.tab-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid var(--border);
  margin-bottom: 0;
}

.tab-labels {
  flex: 1;
  min-width: 0;
}

.global-summaries {
  font-weight: 700;
  white-space: nowrap;
  padding-right: 8px;
  display: flex;
  align-items: center;
  gap: 16px;
  color: var(--text-1);
  font-size: 14px;

  .global-summary-item {
    display: inline-flex;
    align-items: center;
  }
}

.report-direction-tabs {
  margin-top: 0;
  margin-bottom: 0;

  :deep(.ant-tabs-nav) {
    margin: 0;
    padding: 0;
    min-height: 40px;

    &::before {
      // 取消 Ant Design 默认底部分割线，避免与四重视觉强化冲突
      border-bottom: none;
    }
  }
  // 未激活态：浅灰底 + 中性文字
  :deep(.ant-tabs-tab) {
    padding: 6px 16px;
    margin: 0 4px 0 0 !important;
    font-size: 14px;
    font-weight: 500;
    background: var(--bg-muted);
    color: var(--text-2);
    border: 1px solid var(--border);
    border-bottom: none;
    border-radius: 4px 4px 0 0;
    transition: background 0.2s ease, color 0.2s ease,
      border-color 0.2s ease, box-shadow 0.2s ease;
  }
  // Hover 态：淡蓝底 + 主题文字
  :deep(.ant-tabs-tab:hover) {
    background: var(--color-info-light);
    color: var(--color-primary);
  }
  // 激活态：四重视觉强化（浅蓝底 + 蓝边 + 主题文字加粗 + 顶部 3px 蓝色内阴影）
  :deep(.ant-tabs-tab.ant-tabs-tab-active) {
    background: var(--color-primary-light);
    border-color: var(--color-primary-border);
    box-shadow: inset 0 3px 0 0 var(--color-primary);
  }
  :deep(.ant-tabs-tab.ant-tabs-tab-active .ant-tabs-tab-btn) {
    color: var(--color-primary);
    font-weight: 600;
    font-size: 14px;
    text-shadow: none;
  }
  :deep(.ant-tabs-ink-bar) {
    // 已通过顶部内阴影做强标识，隐藏默认底部 ink-bar 避免视觉重复
    display: none;
  }
  :deep(.ant-tabs-content-holder) {
    display: none;
  }
}

.dir-tab-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.report-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  background: var(--bg-card);

  th,
  td {
    border: 1px solid var(--border);
    padding: 6px 10px;
    height: 32px;
    box-sizing: border-box;
    line-height: 1.3;
    vertical-align: middle;
  }

  thead {
    th {
      background: var(--bg-muted);
      color: var(--text-1);
      font-weight: 600;
      text-align: center;
      position: sticky;
      top: 0;
      z-index: 3;
      // 滚动时表头底部阴影
      box-shadow: var(--shadow-sm);
    }

    // 第二行表头需要粘在第一行（高度 32px）之下，
    // 否则两行都 top:0 时第二行会覆盖第一行（期间标签丢失 Bug）
    .head-row-2 th {
      top: 32px;
      z-index: 2;
    }

    .th-period {
      .th-period-tag {
        margin-left: 4px;
        padding: 1px 5px;
        border-radius: 3px;
        background: var(--color-info-light);
        color: var(--color-info);
        font-size: 11px;
        font-weight: normal;
      }

      &-current .th-period-tag {
        background: var(--color-warning-light);
        color: var(--color-warning);
      }
    }

    .th-amount,
    .th-perunit {
      font-weight: 500;
      background: var(--bg-muted);
    }
  }

  // 列宽
  .col-name {
    width: 240px;
  }
  .col-unit {
    width: 60px;
  }
  .col-amount {
    width: 110px;
  }
  .col-perunit {
    width: 90px;
  }
  .col-mom,
  .col-yoy {
    width: 90px;
  }
  .col-source {
    width: 44px;
  }
  .col-logic {
    width: 44px;
  }

  .cell-name {
    text-align: left;
    color: var(--text-1);

    .indent {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }

    .expand-icon {
      width: 12px;
      font-size: 10px;
      color: var(--text-3);
      display: inline-block;
      transition: transform 0.2s ease;

      &.placeholder {
        visibility: hidden;
      }

      &.expanded {
        transform: rotate(90deg);
      }
    }

    .item-name-text {
      flex: 1;
    }
  }

  .cell-unit {
    text-align: center;
    color: var(--text-2);
    font-size: 12px;
  }

  .cell-amount {
    text-align: right;
    color: var(--text-1);

    // 零值灰化
    &.zero-amount {
      color: var(--text-3);
    }
  }

  .cell-perunit {
    text-align: right;
    color: var(--text-2);
  }

  .cell-rate {
    text-align: center;
    color: var(--text-2);
    font-size: 12px;

    // 趋势 badge 样式
    &.change-up {
      color: var(--color-success-text);
      background: var(--color-success-light);
      border-radius: 3px;
    }
    &.change-down {
      color: var(--color-danger-text);
      background: var(--color-danger-light);
      border-radius: 3px;
    }
    &.change-zero {
      color: var(--text-3);
    }
    &.change-null {
      color: var(--text-3);
    }
  }

  .cell-source,
  .cell-logic {
    text-align: center;
    color: var(--text-2);
    font-size: 12px;
    padding-left: 4px;
    padding-right: 4px;
    white-space: nowrap;
  }

  .detail-icon-btn {
    width: 24px;
    height: 24px;
    padding: 0;
    color: var(--text-2);

    &:hover {
      color: var(--color-primary-hover);
      background: var(--color-primary-light);
    }
  }

  .source-icon-btn {
    color: var(--color-info);

    &:hover {
      color: var(--color-info);
      background: var(--color-info-light);
    }
  }

  .detail-empty {
    color: var(--text-3);
  }

  // 行类型
  .row-section-title {
    cursor: pointer;
    user-select: none;

    .section-title-cell {
      background: var(--color-warning-light);
      color: var(--color-warning-text);
      font-weight: 600;
      letter-spacing: 0.5px;
      padding: 8px 12px;
      text-align: left;
      border-left: 3px solid var(--color-primary);

      .section-toggle-icon {
        display: inline-block;
        font-size: 10px;
        color: var(--color-primary);
        transition: transform 0.2s ease;
        margin-right: 6px;

        &.expanded {
          transform: rotate(90deg);
        }
      }
    }

    &:hover .section-title-cell {
      background: var(--color-warning-light);
    }
  }

  .row-margin-total {
    background: var(--color-info-light);
    font-weight: 700;
    border-top: 2px solid var(--color-info);

    .margin-name {
      color: var(--text-1);
    }

    .cell-amount {
      color: var(--text-1);
    }
  }

  .row-tab-formula {
    background: var(--color-info-light);
    font-weight: 700;
    border-top: 2px solid var(--color-info);

    .formula-name {
      color: var(--text-1);
      font-size: 14px;
    }

    .formula-amount {
      color: var(--text-1);
      font-size: 14px;
    }

    td {
      padding: 10px 8px;
    }
  }

  // 斑马纹：明细行奇偶交替
  .row-item:nth-child(even) {
    background: var(--bg-muted);
  }

  .row-section-subtotal {
    background: var(--bg-muted);
    font-weight: 600;

    .cell-name {
      color: var(--text-1);
    }
  }

  .row-formula {
    background: var(--color-info-light);
    font-weight: 700;

    .cell-name {
      color: var(--text-1);
    }

    .cell-amount {
      color: var(--text-1);
    }
  }

  .row-indicator {
    .cell-name {
      color: var(--text-3);
      font-style: italic;
    }

    td {
      color: var(--text-3);
      font-style: italic;
    }
  }

  .row-section-collapsible {
    cursor: pointer;
    user-select: none;

    &:hover td {
      background: var(--color-warning-light) !important;
    }
  }

  .row-detail {
    .cell-name {
      color: var(--text-2);
    }
  }

  .row-clickable {
    cursor: pointer;

    td:first-child {
      border-left: 3px solid transparent;
      transition: border-color 0.2s;
    }

    &:hover {
      background: var(--color-primary-light);

      td:first-child {
        border-left-color: var(--color-primary);
      }
    }
  }

  .row-manual {
    .item-name-text::after {
      content: '手工';
      margin-left: 6px;
      padding: 1px 5px;
      background: var(--color-warning-light);
      color: var(--color-warning);
      border: 1px solid var(--color-warning-border);
      border-radius: 3px;
      font-size: 11px;
      font-weight: normal;
      vertical-align: middle;
    }
  }

  .row-drill {
    background: var(--bg-muted);

    .cell-name {
      color: var(--text-3);
      font-size: 12px;

      .drill-tag {
        color: var(--text-3);
        margin-right: 4px;
      }
    }

    .cell-amount {
      color: var(--text-2);
    }
  }

  // 输入框
  .cell-input {
    width: 100%;

    :deep(.ant-input-number-input) {
      text-align: right;
      padding-right: 4px;
      padding-left: 4px;
      height: 24px;
      font-size: 13px;
    }
  }
}

// ==================== 颜色 ====================
.positive {
  color: var(--color-success-text);
}

.negative {
  color: var(--color-danger-text);
}

// ==================== 双栏布局 ====================
.amoeba-report-layout {
  display: flex;
  gap: 16px;
  width: 100%;
  height: 100%;
}

.indicator-panel {
  flex: 0 0 360px;
  min-width: 300px;
  border-right: 1px solid var(--border);
  padding-right: 16px;
  overflow-y: auto;
}

.pnl-panel {
  flex: 1;
  min-width: 0;
  overflow-x: auto;
  display: flex;
  flex-direction: column;
}

.indicator-panel-header {
  font-size: 14px;
  font-weight: 600;
  padding: 8px 0;
  margin-bottom: 8px;
  border-bottom: 2px solid var(--color-info);
}

.indicator-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;

  th {
    padding: 6px 8px;
    text-align: right;
    font-weight: 500;
    border-bottom: 1px solid var(--border);
    white-space: nowrap;
    background: var(--bg-muted);
    color: var(--text-1);
  }

  th.indicator-name-col {
    text-align: left;
  }

  td {
    padding: 5px 8px;
  }
}

.indicator-group-row {
  font-weight: 600;
  background-color: var(--bg-muted);
}

.indicator-item-row td {
  border-bottom: 1px solid var(--border);
}

.num-cell {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.change-cell {
  font-size: 12px;
  color: var(--text-2);
}

.info-icon {
  margin-left: 4px;
  font-size: 12px;
  color: var(--text-3);
  cursor: help;
}

.logic-drawer {
  display: flex;
  flex-direction: column;
  gap: 8px;
  color: var(--text-1);

  &__label {
    margin-top: 8px;
    color: var(--text-3);
    font-size: 12px;
  }

  &__title {
    color: var(--text-1);
    font-size: 16px;
    font-weight: 600;
    line-height: 1.4;
  }

  &__meta {
    color: var(--text-2);
    line-height: 1.5;
  }

  &__content {
    padding: 12px;
    color: var(--text-1);
    line-height: 1.7;
    white-space: pre-wrap;
    word-break: break-word;
    background: var(--bg-muted);
    border: 1px solid var(--border);
    border-radius: 4px;
  }
}

/* 无指标时全宽显示 */
.amoeba-report-layout:not(.has-indicators) .pnl-panel {
  flex: 1;
}
</style>
