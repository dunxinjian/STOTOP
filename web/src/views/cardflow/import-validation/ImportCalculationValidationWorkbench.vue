<template>
  <div class="validation-workbench">
    <PageHeader back-to="/cardflow/upload-center" back-label="上传中心">
      <template #center>
        <span>导入计算验证</span>
      </template>
      <template #actions>
        <a-button @click="loadSummary" :loading="summaryLoading">
          <ReloadOutlined />
        </a-button>
        <a-button type="primary" :loading="running" @click="runValidation">
          <CalculatorOutlined />
          开始验证
        </a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="summaryLoading">
      <section class="batch-strip">
        <div class="batch-core">
          <span class="batch-id">#{{ batchId || '-' }}</span>
          <div class="batch-title">
            <strong>{{ summary?.flowName || '导入批次' }}</strong>
            <span>{{ summary?.batchNo || summary?.targetTable || '未加载批次信息' }}</span>
          </div>
          <a-tag v-if="summary" :color="batchStatusColor()" class="batch-status-tag">
            {{ summary.batchStatusText || '-' }}
            <template v-if="summary.isBatchRunning && summary.progressPercent != null"> · {{ summary.progressPercent }}%</template>
          </a-tag>
        </div>
        <div class="batch-metrics">
          <div class="metric">
            <span>导入行</span>
            <strong>{{ summary?.totalRows ?? 0 }}</strong>
          </div>
          <div class="metric">
            <span>自动凭证</span>
            <strong>{{ domainCount('Voucher') }}</strong>
          </div>
          <div class="metric">
            <span>价格计算</span>
            <strong>{{ domainCount('Pricing') }}</strong>
          </div>
          <div class="metric">
            <span>成本计算</span>
            <strong>{{ domainCount('Cost') }}</strong>
          </div>
        </div>
      </section>

      <a-alert
        v-if="summary?.isBatchRunning"
        type="warning"
        show-icon
        class="batch-running-alert"
        :message="`批次仍在执行中（${summary.batchStatusText}${summary.currentNodeName ? ' · 当前节点：' + summary.currentNodeName : ''}）`"
        description="计费与成本结果可能尚未写入，此时摘要计数与验证结果不完整。请等待批次完成后点击刷新并重新验证。"
      />
      <a-alert
        v-else-if="summary?.batchErrorMessage"
        type="error"
        show-icon
        class="batch-running-alert"
        message="批次执行存在错误"
        :description="summary.batchErrorMessage"
      />
    </a-spin>

    <section class="control-band">
      <div class="domain-checks">
        <a-checkbox-group v-model:value="form.domains">
          <a-checkbox :value="ValidationDomain.Voucher">自动凭证</a-checkbox>
          <a-checkbox :value="ValidationDomain.Pricing">价格计算</a-checkbox>
          <a-checkbox :value="ValidationDomain.Cost">成本计算</a-checkbox>
        </a-checkbox-group>
      </div>
      <div class="run-options">
        <a-radio-group v-model:value="form.mode" button-style="solid" size="small">
          <a-radio-button value="sample">抽样</a-radio-button>
          <a-radio-button value="errorsOnly">异常优先</a-radio-button>
          <a-radio-button value="allLimited">全量(限5000)</a-radio-button>
        </a-radio-group>
        <a-input-number
          v-model:value="form.sampleSize"
          :min="1"
          :max="5000"
          size="small"
          addon-before="样本"
          :disabled="form.mode === 'allLimited'"
        />
        <a-input-number
          v-model:value="form.tolerance"
          :min="0"
          :step="0.01"
          size="small"
          addon-before="容差"
        />
        <a-checkbox v-model:checked="form.includeEvidence">证据</a-checkbox>
        <a-dropdown :disabled="!report">
          <a-button size="small">
            <DownloadOutlined />
            导出
          </a-button>
          <template #overlay>
            <a-menu @click="onExportMenuClick">
              <a-menu-item key="findings">导出问题清单 CSV</a-menu-item>
              <a-menu-item key="samples">导出抽样核对 CSV</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </section>

    <section class="diagnosis-grid">
      <button
        v-for="item in attributionCards"
        :key="item.key"
        type="button"
        class="diagnosis-card"
        :class="{ active: activeAttribution === item.key }"
        @click="toggleAttribution(item.key)"
      >
        <span class="card-label">{{ item.label }}</span>
        <strong>{{ item.count }}</strong>
        <small>{{ item.hint }}</small>
      </button>
    </section>

    <section class="result-panel sample-panel">
      <div class="result-head">
        <div>
          <span class="result-title">抽样核对</span>
          <span class="result-subtitle">逐条查看原始值、计算结果值和成本项合计</span>
        </div>
        <StatusTag type="info">样本 {{ report?.sampleRows?.length ?? 0 }}</StatusTag>
      </div>

      <a-table
        :columns="sampleColumns"
        :data-source="sampleRowsForTable"
        :loading="running"
        row-key="rowKey"
        size="small"
        :pagination="{ pageSize: 10, showSizeChanger: true, showTotal: (total: number) => `共 ${total} 条` }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'target'">
            <div class="target-cell">
              <strong>{{ record.waybillNo || record.businessKey || record.sourceRowId || '-' }}</strong>
              <span>源行 {{ record.sourceRowId || '-' }}</span>
            </div>
          </template>

          <template v-else-if="column.dataIndex === 'original'">
            <div class="value-stack">
              <span>价格 {{ formatValue(resultFor(record, ValidationDomain.Pricing)?.originalValue) }}</span>
              <span>成本 {{ formatValue(resultFor(record, ValidationDomain.Cost)?.originalValue) }}</span>
            </div>
          </template>

          <template v-else-if="column.dataIndex === 'pricing'">
            <SampleResultCell :result="resultFor(record, ValidationDomain.Pricing)" />
          </template>

          <template v-else-if="column.dataIndex === 'cost'">
            <SampleResultCell :result="resultFor(record, ValidationDomain.Cost)" show-cost-total />
          </template>

          <template v-else-if="column.dataIndex === 'voucher'">
            <SampleResultCell :result="resultFor(record, ValidationDomain.Voucher)" />
          </template>

          <template v-else-if="column.dataIndex === 'findingCount'">
            <a-badge :count="record.findings?.length || 0" :number-style="{ backgroundColor: record.findings?.length ? 'var(--color-danger)' : 'var(--color-success)' }" />
          </template>

          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="openSampleRow(record)">明细</a-button>
          </template>
        </template>
      </a-table>

      <EmptyState
        v-if="!running && report && sampleRowsForTable.length === 0"
        title="暂无抽样数据"
        class="result-empty"
      />
    </section>

    <section class="result-panel">
      <div class="result-head">
        <div>
          <span class="result-title">验证结果</span>
          <span class="result-subtitle">已检查 {{ report?.checkedRows ?? 0 }} 行</span>
        </div>
        <div class="severity-summary">
          <a-tooltip v-for="key in severityKeys" :key="key" :title="activeSeverity === key ? '取消筛选' : `只看${severityText(key)}问题`">
            <a-tag
              :color="severityColor(key)"
              class="severity-filter-tag"
              :class="{ 'severity-filter-active': activeSeverity === key }"
              @click="toggleSeverity(key)"
            >
              {{ severityText(key) }} {{ severityCount(key) }}
            </a-tag>
          </a-tooltip>
        </div>
      </div>

      <a-table
        :columns="columns"
        :data-source="filteredFindings"
        :loading="running"
        row-key="rowKey"
        size="small"
        :pagination="{ pageSize: 12, showSizeChanger: true, showTotal: (total: number) => `共 ${total} 条` }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'domain'">
            <a-tag :color="domainColor(record.domain)">{{ domainText(record.domain) }}</a-tag>
          </template>

          <template v-else-if="column.dataIndex === 'attribution'">
            <a-tag :color="attributionColor(record.attribution)">{{ attributionText(record.attribution) }}</a-tag>
          </template>

          <template v-else-if="column.dataIndex === 'severity'">
            <a-tag :color="severityColor(record.severity)">{{ severityText(record.severity) }}</a-tag>
          </template>

          <template v-else-if="column.dataIndex === 'affectedRows'">
            <StatusTag type="info">{{ record.affectedRows || 1 }} 行</StatusTag>
          </template>

          <template v-else-if="column.dataIndex === 'target'">
            <div class="target-cell">
              <strong>{{ record.waybillNo || record.businessKey || record.sourceRowId || '-' }}</strong>
              <span v-if="record.voucherId">凭证 {{ record.voucherId }}</span>
            </div>
          </template>

          <template v-else-if="column.dataIndex === 'difference'">
            <span :class="{ danger: hasDifference(record) }">{{ formatMoney(record.difference) }}</span>
          </template>

          <template v-else-if="column.dataIndex === 'title'">
            <div class="finding-title">
              <strong>{{ record.title }}</strong>
              <span>{{ record.message }}</span>
            </div>
          </template>

          <template v-else-if="column.dataIndex === 'confidence'">
            <a-progress
              :percent="Math.round((record.confidence || 0) * 100)"
              size="small"
              :show-info="false"
              :stroke-color="confidenceColor(record.confidence)"
            />
          </template>

          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="openFinding(record)">证据</a-button>
          </template>
        </template>
      </a-table>

      <EmptyState
        v-if="!running && report && filteredFindings.length === 0"
        title="当前筛选无异常"
        class="result-empty"
      />
    </section>

    <a-drawer
      v-model:open="drawerOpen"
      title="诊断证据"
      :width="760"
      placement="right"
      :destroy-on-close="true"
    >
      <template v-if="selectedFinding">
        <div class="drawer-title">
          <a-tag :color="domainColor(selectedFinding.domain)">{{ domainText(selectedFinding.domain) }}</a-tag>
          <a-tag :color="attributionColor(selectedFinding.attribution)">{{ attributionText(selectedFinding.attribution) }}</a-tag>
          <strong>{{ selectedFinding.title }}</strong>
        </div>

        <a-alert
          :type="selectedFinding.severity === ValidationSeverity.Blocker || selectedFinding.severity === 'Blocker' ? 'error' : 'warning'"
          :message="selectedFinding.message"
          :description="selectedFinding.suggestedAction"
          show-icon
        />

        <a-descriptions bordered size="small" :column="2" class="evidence-desc">
          <a-descriptions-item label="业务键">{{ selectedFinding.waybillNo || selectedFinding.businessKey || '-' }}</a-descriptions-item>
          <a-descriptions-item label="源行">{{ selectedFinding.sourceRowId || '-' }}</a-descriptions-item>
          <a-descriptions-item label="系统值">{{ formatMoney(selectedFinding.systemValue) }}</a-descriptions-item>
          <a-descriptions-item label="期望值">{{ formatMoney(selectedFinding.expectedValue) }}</a-descriptions-item>
          <a-descriptions-item label="差异">{{ formatMoney(selectedFinding.difference) }}</a-descriptions-item>
          <a-descriptions-item label="影响行数">{{ selectedFinding.affectedRows || 1 }}</a-descriptions-item>
          <a-descriptions-item label="置信度">{{ Math.round((selectedFinding.confidence || 0) * 100) }}%</a-descriptions-item>
        </a-descriptions>

        <a-tabs>
          <a-tab-pane key="persisted" tab="写入结果">
            <EvidenceList :items="selectedFinding.evidence?.persistedResult" />
          </a-tab-pane>
          <a-tab-pane key="config" tab="配置命中">
            <div class="tag-list">
              <StatusTag v-for="item in selectedFinding.evidence?.matchedConfigurations || []" :key="item" type="success">
                {{ item }}
              </StatusTag>
              <StatusTag v-for="item in selectedFinding.evidence?.configurationIssues || []" :key="item" type="warning">
                {{ item }}
              </StatusTag>
              <a-empty
                v-if="!(selectedFinding.evidence?.matchedConfigurations?.length || selectedFinding.evidence?.configurationIssues?.length)"
                description="无配置证据"
              />
            </div>
          </a-tab-pane>
          <a-tab-pane key="trace" tab="计算轨迹">
            <a-timeline v-if="selectedFinding.evidence?.traceSteps?.length">
              <a-timeline-item v-for="step in selectedFinding.evidence.traceSteps" :key="`${step.step}-${step.description}`">
                <div class="trace-step">
                  <strong>{{ step.description || step.step }}</strong>
                  <span v-if="step.formula">{{ step.formula }}</span>
                  <small>
                    输入 {{ formatMoney(step.inputValue) }} · 输出 {{ formatMoney(step.outputValue) }}
                  </small>
                </div>
              </a-timeline-item>
            </a-timeline>
            <a-empty v-else description="无计算轨迹" />
          </a-tab-pane>
          <a-tab-pane key="source" tab="导入字段">
            <EvidenceList :items="selectedFinding.evidence?.sourceFields" />
          </a-tab-pane>
        </a-tabs>
      </template>
    </a-drawer>

    <a-drawer
      v-model:open="sampleDrawerOpen"
      title="抽样核对明细"
      :width="860"
      placement="right"
      :destroy-on-close="true"
    >
      <template v-if="selectedSampleRow">
        <div class="drawer-title">
          <StatusTag type="info">源行 {{ selectedSampleRow.sourceRowId || '-' }}</StatusTag>
          <strong>{{ selectedSampleRow.waybillNo || selectedSampleRow.businessKey || '样本行' }}</strong>
        </div>

        <a-tabs>
          <a-tab-pane key="compare" tab="原始值 / 计算结果值">
            <a-table
              :columns="sampleResultColumns"
              :data-source="selectedSampleRow.results || []"
              row-key="label"
              size="small"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'domain'">
                  <a-tag :color="domainColor(record.domain)">{{ domainText(record.domain) }}</a-tag>
                </template>
                <template v-else-if="column.dataIndex === 'status'">
                  <a-tag :color="sampleStatusColor(record.status)">{{ sampleStatusText(record.status) }}</a-tag>
                </template>
                <template v-else-if="column.dataIndex === 'originalValue'">
                  {{ formatValue(record.originalValue) }}
                </template>
                <template v-else-if="column.dataIndex === 'systemValue'">
                  {{ formatValue(record.systemValue) }}
                </template>
                <template v-else-if="column.dataIndex === 'expectedValue'">
                  {{ formatValue(record.expectedValue) }}
                </template>
                <template v-else-if="column.dataIndex === 'difference'">
                  <span :class="{ danger: hasDifference(record) }">{{ formatMoney(record.difference) }}</span>
                </template>
              </template>
            </a-table>
          </a-tab-pane>

          <a-tab-pane key="costItems" tab="成本项">
            <template v-if="selectedCostResult?.costItems?.length">
              <a-table
                :columns="costItemColumns"
                :data-source="selectedCostResult.costItems"
                row-key="costItemId"
                size="small"
                :pagination="false"
              />
              <div class="cost-total">
                <span>成本项合计</span>
                <strong>{{ formatMoney(costItemTotal(selectedCostResult.costItems)) }}</strong>
                <span>结果表成本合计</span>
                <strong>{{ formatValue(selectedCostResult.systemValue) }}</strong>
              </div>
            </template>
            <a-empty v-else description="无成本项明细" />
          </a-tab-pane>

          <a-tab-pane v-if="selectedVoucherDetail" key="voucher" tab="凭证核验">
            <div class="voucher-verify">
              <a-alert
                :type="voucherVerifyAlert.type"
                :message="voucherVerifyAlert.message"
                show-icon
                class="voucher-verify-alert"
              />

              <h4>① 命中规则</h4>
              <a-descriptions bordered size="small" :column="2">
                <a-descriptions-item label="预筛选">{{ selectedVoucherDetail.passedFilter ? '通过' : '被排除' }}</a-descriptions-item>
                <a-descriptions-item label="规则组">{{ selectedVoucherDetail.ruleGroupName || '未命中' }}</a-descriptions-item>
                <a-descriptions-item label="命中原因" :span="2">{{ selectedVoucherDetail.matchReason || '-' }}</a-descriptions-item>
                <a-descriptions-item label="聚合模式">{{ selectedVoucherDetail.amountAggregation || '-' }}</a-descriptions-item>
                <a-descriptions-item label="业务键">{{ selectedVoucherDetail.businessKey || '-' }}</a-descriptions-item>
              </a-descriptions>

              <h4>② 参与字段（原始值）</h4>
              <EvidenceList :items="selectedVoucherDetail.sourceFieldValues" />

              <template v-if="selectedVoucherDetail.ruleLines.length">
                <h4>③ 规则分录配置</h4>
                <a-table
                  :columns="voucherRuleLineColumns"
                  :data-source="selectedVoucherDetail.ruleLines"
                  row-key="lineNo"
                  size="small"
                  :pagination="false"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'enabled'">
                      <a-tag :color="record.enabled ? 'green' : 'default'">{{ record.enabled ? '启用' : '禁用' }}</a-tag>
                    </template>
                  </template>
                </a-table>
              </template>

              <template v-if="selectedVoucherDetail.draftEntries.length">
                <h4>④ 凭证草案（按当前规则推演）</h4>
                <a-table
                  :columns="voucherEntryColumns"
                  :data-source="selectedVoucherDetail.draftEntries"
                  row-key="lineNo"
                  size="small"
                  :pagination="false"
                />
                <div class="voucher-totals">
                  <span>借合计 <strong>{{ formatMoney(selectedVoucherDetail.draftDebitTotal) }}</strong></span>
                  <span>贷合计 <strong>{{ formatMoney(selectedVoucherDetail.draftCreditTotal) }}</strong></span>
                  <a-tag v-if="selectedVoucherDetail.draftBalanced != null" :color="selectedVoucherDetail.draftBalanced ? 'green' : 'red'">
                    {{ selectedVoucherDetail.draftBalanced ? '草案平衡' : '草案不平' }}
                  </a-tag>
                </div>
              </template>

              <h4>⑤ 实际凭证</h4>
              <template v-if="selectedVoucherDetail.actualVoucherId">
                <div class="voucher-totals">
                  <StatusTag type="info">{{ selectedVoucherDetail.actualVoucherNo }}</StatusTag>
                  <span>借合计 <strong>{{ formatMoney(selectedVoucherDetail.actualDebitTotal) }}</strong></span>
                  <span>贷合计 <strong>{{ formatMoney(selectedVoucherDetail.actualCreditTotal) }}</strong></span>
                  <a-tag v-if="selectedVoucherDetail.actualBalanced != null" :color="selectedVoucherDetail.actualBalanced ? 'green' : 'red'">
                    {{ selectedVoucherDetail.actualBalanced ? '借贷平衡' : '借贷不平' }}
                  </a-tag>
                </div>
                <a-table
                  v-if="selectedVoucherDetail.actualEntries.length"
                  :columns="voucherEntryColumns"
                  :data-source="selectedVoucherDetail.actualEntries"
                  row-key="lineNo"
                  size="small"
                  :pagination="false"
                />
              </template>
              <a-empty v-else :description="selectedVoucherDetail.businessKey ? '按业务键未找到对应实际凭证' : 'SUM 聚合模式下凭证由整组数据生成，请在凭证管理中按批次查看'" />

              <template v-if="selectedVoucherDetail.issues.length">
                <h4>问题</h4>
                <div class="tag-list">
                  <StatusTag v-for="issue in selectedVoucherDetail.issues" :key="issue" type="warning">{{ issue }}</StatusTag>
                </div>
              </template>
            </div>
          </a-tab-pane>

          <a-tab-pane key="trace" tab="计算轨迹">
            <a-timeline v-if="selectedSampleTraceSteps.length">
              <a-timeline-item v-for="step in selectedSampleTraceSteps" :key="`${step.step}-${step.description}`">
                <div class="trace-step">
                  <strong>{{ step.description || step.step }}</strong>
                  <span v-if="step.formula">{{ step.formula }}</span>
                  <small v-if="step.inputValue != null || step.outputValue != null">
                    输入 {{ formatMoney(step.inputValue) }} · 输出 {{ formatMoney(step.outputValue) }}
                  </small>
                </div>
              </a-timeline-item>
            </a-timeline>
            <a-empty v-else description="无计算轨迹（勾选“证据”后重新验证可获取价格六步解释）" />
          </a-tab-pane>

          <a-tab-pane key="sourceFields" tab="原始字段">
            <EvidenceList :items="selectedSampleRow.sourceFields" />
          </a-tab-pane>

          <a-tab-pane key="persisted" tab="写入结果">
            <div class="sample-result-groups">
              <section v-for="result in selectedSampleRow.results || []" :key="result.label">
                <h4>{{ result.label }}</h4>
                <EvidenceList :items="result.persistedResult" />
              </section>
            </div>
          </a-tab-pane>
        </a-tabs>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onMounted, reactive, ref, type PropType } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  CalculatorOutlined,
  DownloadOutlined,
  ReloadOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  ValidationAttribution,
  ValidationDomain,
  ValidationSeverity,
  getImportValidationSummary,
  runImportValidation,
  type CalculationTraceStepDto,
  type ImportValidationFindingDto,
  type ImportValidationMode,
  type ImportValidationReportDto,
  type ImportValidationSampleCostItemDto,
  type ImportValidationSampleResultDto,
  type ImportValidationSampleRowDto,
  type ImportValidationSummaryDto,
} from '@/api/importValidation'

type DomainKey = 'Voucher' | 'Pricing' | 'Cost'
type AttributionKey = 'ImportData' | 'Configuration' | 'CalculationLogic' | 'Persistence'
type SeverityKey = 'Low' | 'Medium' | 'High' | 'Blocker'

const route = useRoute()
const summaryLoading = ref(false)
const running = ref(false)
const summary = ref<ImportValidationSummaryDto | null>(null)
const report = ref<ImportValidationReportDto | null>(null)
const activeAttribution = ref<AttributionKey | null>(null)
const activeSeverity = ref<SeverityKey | null>(null)
const severityKeys: SeverityKey[] = ['Blocker', 'High', 'Medium', 'Low']
const drawerOpen = ref(false)
const selectedFinding = ref<ImportValidationFindingDto | null>(null)
const sampleDrawerOpen = ref(false)
const selectedSampleRow = ref<ImportValidationSampleRowDto | null>(null)

const form = reactive({
  domains: [ValidationDomain.Voucher, ValidationDomain.Pricing, ValidationDomain.Cost],
  mode: 'sample' as ImportValidationMode,
  sampleSize: 100,
  includeEvidence: true,
  tolerance: 0.01,
})

const batchId = computed(() => {
  const raw = route.params.batchId
  const value = Array.isArray(raw) ? raw[0] : raw
  return Number(value) || 0
})

const columns = [
  { title: '域', dataIndex: 'domain', width: 96 },
  { title: '归因', dataIndex: 'attribution', width: 120 },
  { title: '严重度', dataIndex: 'severity', width: 90 },
  { title: '影响', dataIndex: 'affectedRows', width: 78, align: 'center' as const },
  { title: '对象', dataIndex: 'target', width: 140 },
  { title: '问题', dataIndex: 'title' },
  { title: '差异', dataIndex: 'difference', width: 100, align: 'right' as const },
  { title: '置信', dataIndex: 'confidence', width: 90 },
  { title: '操作', dataIndex: 'action', width: 72, align: 'center' as const },
]

const sampleColumns = [
  { title: '样本', dataIndex: 'target', width: 170 },
  { title: '原始值', dataIndex: 'original', width: 190 },
  { title: '价格计算', dataIndex: 'pricing', width: 180 },
  { title: '成本计算', dataIndex: 'cost', width: 220 },
  { title: '自动凭证', dataIndex: 'voucher', width: 180 },
  { title: '异常', dataIndex: 'findingCount', width: 72, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 72, align: 'center' as const },
]

const sampleResultColumns = [
  { title: '域', dataIndex: 'domain', width: 96 },
  { title: '状态', dataIndex: 'status', width: 88 },
  { title: '原始值', dataIndex: 'originalValue', width: 130 },
  { title: '计算结果值', dataIndex: 'systemValue', width: 130 },
  { title: '期望/拆分合计', dataIndex: 'expectedValue', width: 140 },
  { title: '差异', dataIndex: 'difference', width: 100, align: 'right' as const },
  { title: '说明', dataIndex: 'message' },
]

const costItemColumns = [
  { title: '成本项ID', dataIndex: 'costItemId', width: 96 },
  { title: '成本项', dataIndex: 'costItemName' },
  {
    title: '计算结果',
    dataIndex: 'amount',
    width: 130,
    align: 'right' as const,
    customRender: ({ text }: { text?: number }) => formatMoney(text),
  },
]

const attributionCards = computed(() => [
  {
    key: 'ImportData' as const,
    label: '导入数据问题',
    count: attributionCount('ImportData'),
    hint: '字段缺失、值异常',
  },
  {
    key: 'Configuration' as const,
    label: '配置问题',
    count: attributionCount('Configuration'),
    hint: '报价、规则、映射未命中',
  },
  {
    key: 'CalculationLogic' as const,
    label: '计算逻辑问题',
    count: attributionCount('CalculationLogic'),
    hint: '解释值与系统值不一致',
  },
  {
    key: 'Persistence' as const,
    label: '写入链路问题',
    count: attributionCount('Persistence'),
    hint: '计算存在但结果缺失',
  },
])

const filteredFindings = computed(() => {
  let items = (report.value?.findings || []).map((item, index) => ({
    ...item,
    rowKey: `${normalizeDomain(item.domain)}-${item.sourceRowId ?? index}-${item.title}`,
  }))
  if (activeAttribution.value)
    items = items.filter(item => normalizeAttribution(item.attribution) === activeAttribution.value)
  if (activeSeverity.value)
    items = items.filter(item => normalizeSeverity(item.severity) === activeSeverity.value)
  return items
})

/** 抽样明细抽屉：聚合该样本行所有域的计算轨迹（价格六步解释由后端在勾选证据时返回） */
const selectedSampleTraceSteps = computed<CalculationTraceStepDto[]>(() =>
  (selectedSampleRow.value?.results || []).flatMap(result => result.traceSteps || []))

/** 抽样明细抽屉：凭证行级核验明细（原始字段→规则配置→凭证结果） */
const selectedVoucherDetail = computed(() => selectedSampleRow.value?.results
  ?.find(result => normalizeDomain(result.domain) === 'Voucher')?.voucherDetail || null)

const voucherVerifyAlert = computed<{ type: 'success' | 'warning' | 'error'; message: string }>(() => {
  const detail = selectedVoucherDetail.value
  const result = selectedSampleRow.value?.results?.find(r => normalizeDomain(r.domain) === 'Voucher')
  if (!detail || !result) return { type: 'warning', message: '无凭证核验数据' }
  const type = result.status === 'ok' ? 'success' : result.status === 'different' || result.status === 'error' ? 'error' : 'warning'
  return { type, message: result.message }
})

const voucherRuleLineColumns = [
  { title: '行号', dataIndex: 'lineNo', width: 56 },
  { title: '方向', dataIndex: 'direction', width: 56 },
  { title: '科目配置', dataIndex: 'accountText', width: 200 },
  { title: '金额字段', dataIndex: 'amountField', width: 110 },
  { title: '摘要模板', dataIndex: 'summaryTemplate', width: 150 },
  { title: '条件', dataIndex: 'conditionText' },
  { title: '辅助核算', dataIndex: 'auxiliaryText', width: 150 },
  { title: '状态', dataIndex: 'enabled', width: 64 },
]

const voucherEntryColumns = [
  { title: '行号', dataIndex: 'lineNo', width: 56 },
  { title: '方向', dataIndex: 'direction', width: 56 },
  { title: '科目', dataIndex: 'accountCode', width: 100 },
  { title: '科目名称', dataIndex: 'accountName', width: 140 },
  {
    title: '金额',
    dataIndex: 'amount',
    width: 110,
    align: 'right' as const,
    customRender: ({ text }: { text?: number }) => formatMoney(text),
  },
  { title: '摘要', dataIndex: 'summary' },
  { title: '问题', dataIndex: 'issue', width: 160 },
]

const sampleRowsForTable = computed(() => (report.value?.sampleRows || []).map((row, index) => ({
  ...row,
  rowKey: `${row.sourceRowId ?? index}-${row.waybillNo || row.businessKey || index}`,
})))

const selectedCostResult = computed(() => selectedSampleRow.value?.results
  ?.find(result => normalizeDomain(result.domain) === 'Cost') || null)

async function loadSummary() {
  if (!batchId.value) return
  summaryLoading.value = true
  try {
    summary.value = await getImportValidationSummary(batchId.value)
  } finally {
    summaryLoading.value = false
  }
}

async function runValidation() {
  if (!batchId.value) {
    message.warning('缺少批次ID')
    return
  }
  if (form.domains.length === 0) {
    message.warning('请选择验证范围')
    return
  }
  if (summary.value?.isBatchRunning) {
    message.warning('批次仍在执行中，本次验证结果可能不完整，批次完成后请重新验证')
  }
  running.value = true
  try {
    report.value = await runImportValidation(batchId.value, {
      domains: form.domains,
      mode: form.mode,
      sampleSize: form.sampleSize,
      includeEvidence: form.includeEvidence,
      tolerance: form.tolerance,
    })
    if (report.value?.isBatchRunning) {
      message.warning(`验证完成，但批次状态为「${report.value.batchStatusText}」，结果可能不完整`)
      void loadSummary()
    } else {
      message.success('验证完成')
    }
  } finally {
    running.value = false
  }
}

function onExportMenuClick({ key }: { key: string | number }) {
  if (key === 'findings') exportFindingsCsv()
  else if (key === 'samples') exportSamplesCsv()
}

function exportFindingsCsv() {
  const findings = report.value?.findings || []
  if (findings.length === 0) {
    message.info('当前验证结果没有问题记录')
    return
  }
  const header = ['域', '归因', '严重度', '影响行数', '运单号/业务键', '问题', '说明', '系统值', '期望值', '差异', '置信度', '修复建议']
  const rows = findings.map(item => [
    domainText(item.domain),
    attributionText(item.attribution),
    severityText(item.severity),
    item.affectedRows ?? 1,
    item.waybillNo || item.businessKey || item.sourceRowId || '',
    item.title,
    item.message,
    item.systemValue ?? '',
    item.expectedValue ?? '',
    item.difference ?? '',
    `${Math.round((item.confidence || 0) * 100)}%`,
    item.suggestedAction || '',
  ])
  downloadCsv(`批次${batchId.value}-验证问题清单`, header, rows)
}

function exportSamplesCsv() {
  const samples = report.value?.sampleRows || []
  if (samples.length === 0) {
    message.info('当前验证结果没有抽样数据')
    return
  }
  const header = ['运单号/业务键', '源行', '价格状态', '价格系统值', '价格期望值', '价格差异', '成本状态', '成本合计', '成本明细合计', '凭证状态', '异常数']
  const rows = samples.map(row => {
    const pricing = (row.results || []).find(result => normalizeDomain(result.domain) === 'Pricing')
    const cost = (row.results || []).find(result => normalizeDomain(result.domain) === 'Cost')
    const voucher = (row.results || []).find(result => normalizeDomain(result.domain) === 'Voucher')
    return [
      row.waybillNo || row.businessKey || '',
      row.sourceRowId ?? '',
      sampleStatusText(pricing?.status),
      formatCsvValue(pricing?.systemValue),
      formatCsvValue(pricing?.expectedValue),
      pricing?.difference ?? '',
      sampleStatusText(cost?.status),
      formatCsvValue(cost?.systemValue),
      formatCsvValue(cost?.expectedValue),
      sampleStatusText(voucher?.status),
      row.findings?.length || 0,
    ]
  })
  downloadCsv(`批次${batchId.value}-抽样核对`, header, rows)
}

function formatCsvValue(value: unknown) {
  if (value === null || value === undefined) return ''
  return String(value)
}

function downloadCsv(fileName: string, header: string[], rows: (string | number)[][]) {
  const escapeCell = (cell: string | number) => {
    const text = String(cell ?? '')
    return /[",\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
  }
  const lines = [header, ...rows].map(row => row.map(escapeCell).join(','))
  // BOM：Excel 直接打开不乱码
  const blob = new Blob(['\uFEFF' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = `${fileName}.csv`
  link.click()
  URL.revokeObjectURL(link.href)
}

function openFinding(record: Record<string, unknown>) {
  selectedFinding.value = record as unknown as ImportValidationFindingDto
  drawerOpen.value = true
}

function openSampleRow(record: Record<string, unknown>) {
  selectedSampleRow.value = record as unknown as ImportValidationSampleRowDto
  sampleDrawerOpen.value = true
}

function toggleAttribution(key: AttributionKey) {
  activeAttribution.value = activeAttribution.value === key ? null : key
}

function toggleSeverity(key: SeverityKey) {
  activeSeverity.value = activeSeverity.value === key ? null : key
}

function batchStatusColor() {
  if (!summary.value) return 'default'
  if (summary.value.isRevoked) return 'default'
  if (summary.value.batchErrorMessage) return 'red'
  return summary.value.isBatchRunning ? 'processing' : 'green'
}

function domainCount(key: DomainKey) {
  return getDictionaryCount(summary.value?.existingResultCounts, key)
}

function attributionCount(key: AttributionKey) {
  return getDictionaryCount(report.value?.attributionCounts, key)
}

function severityCount(key: SeverityKey) {
  return getDictionaryCount(report.value?.severityCounts, key)
}

function getDictionaryCount(source: Record<string, number> | undefined, key: string) {
  if (!source) return 0
  const enumValue = (ValidationDomain as unknown as Record<string, number>)[key]
    ?? (ValidationAttribution as unknown as Record<string, number>)[key]
    ?? (ValidationSeverity as unknown as Record<string, number>)[key]
  return source[key] ?? source[String(enumValue)] ?? 0
}

function normalizeDomain(value: ImportValidationFindingDto['domain'] | ImportValidationSampleResultDto['domain']): DomainKey {
  if (value === ValidationDomain.Voucher || value === '1' || value === 'Voucher') return 'Voucher'
  if (value === ValidationDomain.Pricing || value === '2' || value === 'Pricing') return 'Pricing'
  return 'Cost'
}

function normalizeAttribution(value: ImportValidationFindingDto['attribution']): AttributionKey | 'None' {
  if (value === ValidationAttribution.ImportData || value === '1' || value === 'ImportData') return 'ImportData'
  if (value === ValidationAttribution.Configuration || value === '2' || value === 'Configuration') return 'Configuration'
  if (value === ValidationAttribution.CalculationLogic || value === '3' || value === 'CalculationLogic') return 'CalculationLogic'
  if (value === ValidationAttribution.Persistence || value === '4' || value === 'Persistence') return 'Persistence'
  return 'None'
}

function normalizeSeverity(value: ImportValidationFindingDto['severity'] | SeverityKey): SeverityKey {
  if (value === ValidationSeverity.Blocker || value === '4' || value === 'Blocker') return 'Blocker'
  if (value === ValidationSeverity.High || value === '3' || value === 'High') return 'High'
  if (value === ValidationSeverity.Medium || value === '2' || value === 'Medium') return 'Medium'
  return 'Low'
}

function domainText(value: ImportValidationFindingDto['domain']) {
  const map: Record<DomainKey, string> = {
    Voucher: '自动凭证',
    Pricing: '价格计算',
    Cost: '成本计算',
  }
  return map[normalizeDomain(value)]
}

function domainColor(value: ImportValidationFindingDto['domain']) {
  const map: Record<DomainKey, string> = {
    Voucher: 'blue',
    Pricing: 'green',
    Cost: 'orange',
  }
  return map[normalizeDomain(value)]
}

function attributionText(value: ImportValidationFindingDto['attribution']) {
  const map: Record<string, string> = {
    ImportData: '导入数据问题',
    Configuration: '配置问题',
    CalculationLogic: '计算逻辑问题',
    Persistence: '写入链路问题',
    None: '正常',
  }
  return map[normalizeAttribution(value)]
}

function attributionColor(value: ImportValidationFindingDto['attribution']) {
  const map: Record<string, string> = {
    ImportData: 'red',
    Configuration: 'orange',
    CalculationLogic: 'purple',
    Persistence: 'geekblue',
    None: 'green',
  }
  return map[normalizeAttribution(value)]
}

function severityText(value: ImportValidationFindingDto['severity'] | SeverityKey) {
  const map: Record<SeverityKey, string> = {
    Low: '低',
    Medium: '中',
    High: '高',
    Blocker: '阻断',
  }
  return map[normalizeSeverity(value)]
}

function severityColor(value: ImportValidationFindingDto['severity'] | SeverityKey) {
  const map: Record<SeverityKey, string> = {
    Low: 'green',
    Medium: 'gold',
    High: 'orange',
    Blocker: 'red',
  }
  return map[normalizeSeverity(value)]
}

function confidenceColor(value: number) {
  if (value >= 0.9) return 'var(--color-success)'
  if (value >= 0.75) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

function resultFor(
  row: Partial<ImportValidationSampleRowDto> | Record<string, unknown>,
  domain: ValidationDomain,
) {
  const normalized = normalizeDomain(domain)
  const results = (row as ImportValidationSampleRowDto).results || []
  return results.find(result => normalizeDomain(result.domain) === normalized) || null
}

function hasDifference(record: Record<string, unknown>) {
  return Math.abs(Number(record.difference || 0)) > 0
}

function formatMoney(value?: number) {
  if (value === null || value === undefined) return '-'
  return Number(value).toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

function formatValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '-'
  if (typeof value === 'number') return formatMoney(value)
  if (typeof value === 'boolean') return value ? '是' : '否'
  if (value instanceof Date) return value.toLocaleString('zh-CN')
  if (Array.isArray(value)) return value.length ? JSON.stringify(value) : '[]'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

function sampleStatusText(status?: string) {
  const map: Record<string, string> = {
    ok: '一致',
    different: '不一致',
    missing: '缺失',
    error: '异常',
    unknown: '未知',
  }
  return map[status || 'unknown'] || status || '未知'
}

function sampleStatusColor(status?: string) {
  const map: Record<string, string> = {
    ok: 'green',
    different: 'orange',
    missing: 'default',
    error: 'red',
    unknown: 'default',
  }
  return map[status || 'unknown'] || 'default'
}

function costItemTotal(items: ImportValidationSampleCostItemDto[] = []) {
  return items.reduce((sum, item) => sum + Number(item.amount || 0), 0)
}

function formatEvidenceLabel(key: string) {
  const map: Record<string, string> = {
    batchId: '批次ID',
    billingResultId: '计费结果ID',
    calcStatus: '计算状态',
    chargeAmount: '应收金额',
    billingWeight: '计费重量',
    quotationCode: '报价编号',
    shopName: '店铺账号',
    totalCost: '成本合计',
    breakdownTotal: '成本明细合计',
    breakdownCount: '成本明细数',
    costItems: '成本项明细',
    costItemId: '成本项ID',
    costItemName: '成本项',
    amount: '金额',
    voucherRecordCount: '凭证生成记录数',
    matchedRows: '已匹配行数',
    unmatchedRows: '未匹配行数',
    generatedVoucherCount: '生成凭证数',
  }
  return map[key] || key
}

const SampleResultCell = defineComponent({
  name: 'SampleResultCell',
  props: {
    result: {
      type: Object as PropType<ImportValidationSampleResultDto | null>,
      default: null,
    },
    showCostTotal: {
      type: Boolean,
      default: false,
    },
  },
  setup(props) {
    return () => {
      if (!props.result) {
        return h('div', { class: 'sample-result-cell empty' }, '-')
      }

      const result = props.result
      const rows = [
        h('span', { key: 'system' }, `计算结果值 ${formatValue(result.systemValue)}`),
      ]

      if (props.showCostTotal) {
        rows.push(h('span', { key: 'cost-total' }, `成本项合计 ${formatMoney(costItemTotal(result.costItems || []))}`))
      }

      return h('div', { class: 'sample-result-cell' }, [
        h('div', { class: 'sample-status-line' }, [
          h('span', { class: 'sample-status-dot', 'data-status': result.status || 'unknown' }),
          h('strong', sampleStatusText(result.status)),
        ]),
        ...rows,
      ])
    }
  },
})

const EvidenceList = defineComponent({
  name: 'EvidenceList',
  props: {
    items: {
      type: Object,
      default: () => ({}),
    },
  },
  setup(props) {
    return () => {
      const entries = Object.entries(props.items || {})
      if (entries.length === 0) {
        return h('div', { class: 'evidence-empty' }, '无数据')
      }

      return h('div', { class: 'evidence-list' }, entries.map(([key, value]) => h('div', { class: 'evidence-row', key }, [
        h('span', formatEvidenceLabel(key)),
        h('strong', formatEvidenceValue(value)),
      ])))
    }
  },
})

function formatEvidenceValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '-'
  if (Array.isArray(value)) {
    if (value.length === 0) return '无'
    return value.map(item => formatEvidenceObject(item)).join('；')
  }
  if (typeof value === 'object') return formatEvidenceObject(value)
  return formatValue(value)
}

function formatEvidenceObject(value: unknown) {
  if (!value || typeof value !== 'object') return formatValue(value)
  return Object.entries(value as Record<string, unknown>)
    .map(([key, itemValue]) => `${formatEvidenceLabel(key)}：${formatValue(itemValue)}`)
    .join('，')
}

onMounted(async () => {
  await loadSummary()
  await runValidation()
})
</script>

<style scoped lang="scss">
.validation-workbench {
  padding: 16px;
  background: var(--bg-muted);
  min-height: calc(100vh - 88px);
}

.batch-strip,
.control-band,
.result-panel {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
}

.batch-strip {
  display: flex;
  justify-content: space-between;
  align-items: stretch;
  gap: 16px;
  padding: 16px 18px;
  box-shadow: var(--shadow-sm);
}

.batch-core {
  display: flex;
  align-items: center;
  gap: 14px;
  min-width: 0;
}

.batch-id {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 20px;
  color: var(--color-info);
  white-space: nowrap;
}

.batch-title {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;

  strong {
    font-size: 17px;
    color: var(--text-1);
  }

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.batch-status-tag {
  flex-shrink: 0;
  margin-left: 4px;
}

.voucher-verify {
  display: flex;
  flex-direction: column;
  gap: 10px;

  h4 {
    margin: 6px 0 0;
    font-size: 13px;
    color: var(--text-1);
  }
}

.voucher-verify-alert {
  margin-bottom: 2px;
}

.voucher-totals {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 6px 2px;
  color: var(--text-2);
  font-size: 12px;

  strong {
    color: var(--text-1);
  }
}

.batch-running-alert {
  margin: 0 0 12px;
}

.severity-filter-tag {
  cursor: pointer;
  user-select: none;
}

.severity-filter-active {
  outline: 2px solid currentcolor;
  outline-offset: 1px;
}

.batch-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(90px, 1fr));
  gap: 10px;
  min-width: 430px;
}

.metric {
  border-left: 1px solid var(--border);
  padding-left: 14px;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 2px;

  span {
    color: var(--text-2);
    font-size: 12px;
  }

  strong {
    font-size: 20px;
    color: var(--text-1);
    line-height: 1.15;
  }
}

.control-band {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  margin-top: 12px;
  padding: 12px 16px;
}

.domain-checks :deep(.ant-checkbox-wrapper) {
  margin-right: 18px;
}

.run-options {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.diagnosis-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-top: 12px;
}

.diagnosis-card {
  height: 94px;
  text-align: left;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  padding: 14px 16px;
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s, transform 0.2s;

  &:hover {
    border-color: var(--color-primary);
    box-shadow: 0 10px 24px var(--color-primary-border);
    transform: translateY(-1px);
  }

  &.active {
    border-color: var(--border-strong);
    box-shadow: var(--shadow-md);
    transform: translateY(-1px);
  }

  .card-label,
  small {
    display: block;
    color: var(--text-2);
    font-size: 12px;
  }

  strong {
    display: block;
    margin: 4px 0;
    font-size: 24px;
    line-height: 1.1;
    color: var(--text-1);
  }
}

.result-panel {
  margin-top: 12px;
  padding: 14px 16px 18px;
}

.sample-panel {
  border-color: color-mix(in srgb, var(--color-info) 15%, transparent);
  box-shadow: 0 8px 22px color-mix(in srgb, var(--color-info) 5%, transparent);
}

.result-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.result-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}

.result-subtitle {
  margin-left: 10px;
  color: var(--text-2);
  font-size: 12px;
}

.severity-summary {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.target-cell,
.finding-title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;

  span {
    color: var(--text-2);
    font-size: 12px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}

.finding-title strong {
  color: var(--text-1);
}

.value-stack,
.sample-result-cell {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
  font-size: 12px;
  color: var(--text-2);
}

.value-stack span,
.sample-result-cell span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sample-result-cell.empty {
  color: var(--text-3);
}

.sample-status-line {
  display: flex;
  align-items: center;
  gap: 6px;

  strong {
    color: var(--text-1);
    font-size: 12px;
    font-weight: 600;
  }
}

.sample-status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--text-3);
  flex: 0 0 auto;

  &[data-status='ok'] {
    background: var(--color-success);
  }

  &[data-status='different'] {
    background: var(--color-warning);
  }

  &[data-status='error'] {
    background: var(--color-danger);
  }
}

.cost-total {
  display: grid;
  grid-template-columns: auto minmax(90px, auto) auto minmax(90px, auto);
  gap: 8px 12px;
  justify-content: flex-end;
  align-items: center;
  margin-top: 12px;
  padding: 10px 12px;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 8px;

  span {
    color: var(--text-2);
    font-size: 12px;
  }

  strong {
    color: var(--text-1);
    text-align: right;
  }
}

.sample-result-groups {
  display: flex;
  flex-direction: column;
  gap: 12px;

  section {
    min-width: 0;
  }

  h4 {
    margin: 0 0 6px;
    color: var(--text-1);
    font-size: 13px;
    font-weight: 600;
  }
}

.danger {
  color: var(--color-danger-text);
}

.result-empty {
  margin-top: 16px;
}

.drawer-title {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;

  strong {
    color: var(--text-1);
  }
}

.evidence-desc {
  margin: 14px 0;
}

.tag-list {
  min-height: 96px;
}

.trace-step {
  display: flex;
  flex-direction: column;
  gap: 3px;

  span {
    color: var(--text-2);
  }

  small {
    color: var(--text-2);
  }
}

:deep(.evidence-list) {
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}

:deep(.evidence-row) {
  display: grid;
  grid-template-columns: 180px minmax(0, 1fr);
  border-bottom: 1px solid var(--border);
  min-height: 38px;

  &:last-child {
    border-bottom: 0;
  }

  span,
  strong {
    padding: 9px 12px;
    word-break: break-all;
  }

  span {
    background: var(--bg-muted);
    color: var(--text-2);
    font-weight: 500;
  }

  strong {
    color: var(--text-1);
    font-weight: 500;
  }
}

:deep(.evidence-empty) {
  color: var(--text-3);
  padding: 18px;
  text-align: center;
  border: 1px dashed var(--border);
  border-radius: 8px;
}

@media (max-width: 960px) {
  .batch-strip,
  .control-band,
  .result-head {
    flex-direction: column;
    align-items: stretch;
  }

  .batch-metrics,
  .diagnosis-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    min-width: 0;
  }

  .run-options {
    justify-content: flex-start;
  }
}

@media (max-width: 640px) {
  .validation-workbench {
    padding: 10px;
  }

  .batch-metrics,
  .diagnosis-grid {
    grid-template-columns: 1fr;
  }
}
</style>
