<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { previewFlowDraftPath, getSampleCards, previewVoucher } from '@/api/cardflow'
import { normalizeOptionList } from '@/utils/cardflowFieldFormat'
import UserSelect from '@/components/cardflow/fields/UserSelect.vue'
import OrgSelect from '@/components/cardflow/fields/OrgSelect.vue'
import type { CardFlowPathPreviewDto, CardFlowPathPreviewStepDto, SampleCardDto, VoucherPreviewDto, SchemaFieldDefinition } from '@/types/cardflow'

const props = defineProps<{
  flowDefinitionId?: number | null
  previewApi?: typeof previewFlowDraftPath
  disabled?: boolean
  /** 流程卡片字段：样例表单按它动态生成，路由条件才能供到值 */
  fields?: SchemaFieldDefinition[]
}>()

// 步骤点击联动：把节点 stageKey 抛给编辑页切预览视角
const emit = defineEmits<{
  (e: 'step-select', stageKey: string): void
  // 干跑命中路径：把命中的 stage 序列抛给编辑页，用于竖向图路径点亮（M5-4）
  (e: 'path-updated', hitStageKeys: string[]): void
}>()

const STRATEGY_LABELS: Record<string, string> = {
  role: '按角色',
  fixed: '指定人员',
  fixedUsers: '指定人员',
  fieldUsers: '按字段取人',
  initiator: '发起人',
  orgChain: '组织链',
  amountMatrix: '金额矩阵',
  feeTypeBp: '费用类型 BP',
}

// M5-2 失败态类别 → 展示文案 + 底色档（assigneeUnresolved 走兜底黄，其余红）
const FAILURE_LABELS: Record<string, string> = {
  assigneeUnresolved: '处理人解析失败',
  noBranchMatch: '无分支可走',
  autoStageError: '自动节点异常',
}

function failureLabel(kind: string): string {
  return FAILURE_LABELS[kind] || '推演异常'
}

function strategyLabel(strategy?: string | null): string {
  if (!strategy) return '未配置'
  return STRATEGY_LABELS[strategy] || strategy
}

function approverText(approver: NonNullable<CardFlowPathPreviewStepDto['approver']>): string {
  if (approver.error) return approver.error
  if (approver.approverNames.length) return approver.approverNames.join('、')
  if (approver.fallbackReason) return `未解析到处理人，兜底：${approver.fallbackReason === 'flowAdmin' ? '转审批管理员' : approver.fallbackReason}`
  return '未解析到处理人'
}

const sample = reactive({
  initiatorId: undefined as number | undefined,
  orgId: undefined as number | undefined,
})

// 样例卡片数据：按流程真实 cardSchema 动态生成（此前硬编码 5 个字段，
// 条件引用其他字段的流程供不到值，预演永远落默认分支）
const sampleData = reactive<Record<string, any>>({})

function defaultValueOf(field: SchemaFieldDefinition): any {
  if (field.type === 'money' || String(field.type) === 'number') return 1000
  if (field.type === 'enum') {
    const first = field.options?.[0]
    return (first && typeof first === 'object' ? first.value : first) ?? undefined
  }
  if (field.type === 'date') return undefined
  return undefined
}

/** 参与条件求值有意义的字段类型（人/组织/附件走 initiator./source. 等运行时前缀，不在此模拟） */
const previewFields = computed(() =>
  (props.fields || []).filter(field => ['text', 'money', 'number', 'enum', 'date'].includes(String(field.type)))
)

watch(previewFields, (fields) => {
  const keys = new Set(fields.map(field => field.key))
  for (const key of Object.keys(sampleData)) {
    if (!keys.has(key)) delete sampleData[key]
  }
  for (const field of fields) {
    if (!(field.key in sampleData)) sampleData[field.key] = defaultValueOf(field)
  }
}, { immediate: true, deep: true })

// M5-1 样例值三来源：手动填写 / 取历史卡片 / 随机生成
const sampleSource = ref<'manual' | 'history' | 'random'>('manual')

// —— 取历史卡片 ——
const historyKeyword = ref('')
const historyLoading = ref(false)
const historyCards = ref<SampleCardDto[]>([])
const missingFieldKeys = ref<string[]>([])

async function searchHistoryCards() {
  if (!props.flowDefinitionId) {
    message.warning('请先保存流程草稿后再取历史卡片')
    return
  }
  historyLoading.value = true
  try {
    historyCards.value = await getSampleCards(props.flowDefinitionId, historyKeyword.value.trim() || undefined)
    if (!historyCards.value.length) message.info('近 30 天没有匹配的历史卡片')
  } catch (e) {
    console.error('[PathPreview] 取历史卡片失败:', e)
  } finally {
    historyLoading.value = false
  }
}

// 选一张历史卡片 → dataJson 代入样例字段（跨版本缺字段标黄提示）
function applyHistoryCard(card: SampleCardDto) {
  let parsed: Record<string, any> = {}
  try {
    parsed = JSON.parse(card.dataJson) || {}
  } catch {
    message.warning('该卡片数据格式异常，无法代入')
    return
  }
  const missing: string[] = []
  for (const field of previewFields.value) {
    if (field.key in parsed) {
      sampleData[field.key] = parsed[field.key]
    } else {
      missing.push(field.label || field.key)
    }
  }
  missingFieldKeys.value = missing
  message.success(`已代入「${card.title}」的样例数据`)
}

// —— 随机生成（前端按字段类型生成合法值，无需后端） ——
function randomValueOf(field: SchemaFieldDefinition): any {
  const type = String(field.type)
  if (field.type === 'money' || type === 'number') return Math.floor(Math.random() * 10000)
  if (field.type === 'enum') {
    const opts = normalizeOptionList(field.options)
    if (opts.length) return opts[Math.floor(Math.random() * opts.length)].value
    return undefined
  }
  if (field.type === 'date') return new Date().toISOString().slice(0, 10)
  return `样例-${Math.floor(Math.random() * 1000)}`
}

function generateRandomSample() {
  for (const field of previewFields.value) {
    sampleData[field.key] = randomValueOf(field)
  }
  missingFieldKeys.value = []
  message.success('已随机生成样例数据')
}

const loading = ref(false)
const result = ref<CardFlowPathPreviewDto | null>(null)

const pathText = computed(() => {
  if (!result.value?.steps?.length) return ''
  return result.value.steps.map(step => step.stageName || step.stageKey).join(' -> ')
})

async function runPreview() {
  if (props.disabled) {
    message.warning('预演条件未就绪')
    return
  }
  if (!props.flowDefinitionId) {
    message.warning('请先保存流程草稿后再预演路径')
    return
  }
  loading.value = true
  try {
    const payload: Record<string, any> = {}
    for (const [key, value] of Object.entries(sampleData)) {
      if (value !== undefined && value !== null && value !== '') payload[key] = value
    }
    const dataJson = JSON.stringify(payload)
    const api = props.previewApi || previewFlowDraftPath
    result.value = await api(props.flowDefinitionId, {
      dataJson,
      initialDataJson: dataJson,
      initiatorId: sample.initiatorId || null,
      orgId: sample.orgId || null,
      maxSteps: 20,
    })
    // 命中路径点亮（M5-4）：抛出命中的 stage 序列
    const hitKeys = (result.value?.steps || [])
      .filter(s => s.stepType === 'stage' && s.stageKey)
      .map(s => s.stageKey)
    emit('path-updated', hitKeys)
  } catch (e) {
    // 拦截器已弹出后端具体原因（如"没有可预演的流程版本"），此处不重复弹泛化提示
    console.error('[PathPreview] 预演失败:', e)
  } finally {
    loading.value = false
  }
}

// M5-3 凭证试算：自动节点步骤按需展开试算借贷分录（真试算需运行时上下文时后端诚实降级 success=false）
const voucherByStageKey = reactive<Record<string, VoucherPreviewDto>>({})
const voucherLoadingKey = ref('')

// 自动节点（非动态策略步）才可能有凭证试算
function isAutoVoucherStep(step: CardFlowPathPreviewStepDto): boolean {
  return step.stepType === 'stage'
    && (String(step.type) === 'auto' || String(step.type) === 'batchAuto')
}

async function tryPreviewVoucher(step: CardFlowPathPreviewStepDto) {
  if (!props.flowDefinitionId) {
    message.warning('请先保存流程草稿后再试算凭证')
    return
  }
  voucherLoadingKey.value = step.stageKey
  try {
    const payload: Record<string, any> = {}
    for (const [key, value] of Object.entries(sampleData)) {
      if (value !== undefined && value !== null && value !== '') payload[key] = value
    }
    voucherByStageKey[step.stageKey] = await previewVoucher(props.flowDefinitionId, {
      stageKey: step.stageKey,
      cardDataJson: JSON.stringify(payload),
    })
  } catch (e) {
    console.error('[PathPreview] 凭证试算失败:', e)
  } finally {
    voucherLoadingKey.value = ''
  }
}

function directionLabel(direction: string): string {
  return direction === 'debit' ? '借' : direction === 'credit' ? '贷' : direction
}
</script>

<template>
  <section class="cf-path-preview">
    <header class="cf-path-preview__head">
      <div>
        <strong>模拟运行</strong>
        <span>{{ disabled ? '预演条件未就绪，请先完成左侧检查项' : '输入样例卡片数据，立即预演审批路径' }}</span>
      </div>
      <a-button type="primary" size="small" :loading="loading" :disabled="disabled" @click="runPreview">
        预演路径
      </a-button>
    </header>

    <!-- M5-1 样例值三来源 -->
    <div class="cf-path-preview__source">
      <a-radio-group v-model:value="sampleSource" size="small" button-style="solid" :disabled="disabled">
        <a-radio-button value="manual">手动填写</a-radio-button>
        <a-radio-button value="history">取历史卡片</a-radio-button>
        <a-radio-button value="random">随机生成</a-radio-button>
      </a-radio-group>
      <a-button
        v-if="sampleSource === 'random'"
        size="small"
        :disabled="disabled || !previewFields.length"
        @click="generateRandomSample"
      >生成一组</a-button>
    </div>

    <!-- 取历史卡片：搜索 + 列表选取 -->
    <div v-if="sampleSource === 'history'" class="cf-path-preview__history">
      <a-input-search
        v-model:value="historyKeyword"
        placeholder="按标题 / 单号搜索近 30 天卡片"
        size="small"
        :loading="historyLoading"
        :disabled="disabled"
        @search="searchHistoryCards"
      />
      <div v-if="historyCards.length" class="cf-path-preview__history-list">
        <button
          v-for="card in historyCards"
          :key="card.cardId"
          type="button"
          class="cf-path-preview__history-item"
          @click="applyHistoryCard(card)"
        >{{ card.title }}</button>
      </div>
      <div v-if="missingFieldKeys.length" class="cf-path-preview__history-missing">
        以下字段在该历史卡片中缺失，未代入：{{ missingFieldKeys.join('、') }}
      </div>
    </div>

    <div class="cf-path-preview__form">
      <label>
        <span>发起人</span>
        <UserSelect
          :model-value="sample.initiatorId"
          :disabled="disabled"
          placeholder="选择发起人"
          @update:model-value="(v: any) => sample.initiatorId = v?.id ?? undefined"
        />
      </label>
      <label>
        <span>组织</span>
        <OrgSelect
          :model-value="sample.orgId"
          :disabled="disabled"
          placeholder="选择组织"
          @update:model-value="(v: any) => sample.orgId = v?.id ?? undefined"
        />
      </label>
      <label v-for="field in previewFields" :key="field.key">
        <span>{{ field.label || field.key }}</span>
        <a-input-number
          v-if="field.type === 'money' || String(field.type) === 'number'"
          v-model:value="sampleData[field.key]"
          :disabled="disabled"
          style="width: 100%"
        />
        <a-select
          v-else-if="field.type === 'enum'"
          v-model:value="sampleData[field.key]"
          :options="normalizeOptionList(field.options)"
          :disabled="disabled"
          allow-clear
          style="width: 100%"
        />
        <a-date-picker
          v-else-if="field.type === 'date'"
          v-model:value="sampleData[field.key]"
          value-format="YYYY-MM-DD"
          :disabled="disabled"
          style="width: 100%"
        />
        <a-input
          v-else
          v-model:value="sampleData[field.key]"
          :disabled="disabled"
          :placeholder="field.placeholder || ''"
        />
      </label>
      <div v-if="!previewFields.length" class="cf-path-preview__no-fields">
        流程还没有可模拟的卡片字段（文本 / 金额 / 数字 / 枚举 / 日期）。
      </div>
    </div>

    <div v-if="pathText" class="cf-path-preview__path">
      <span
        v-for="(item, index) in pathText.split(' -> ')"
        :key="`${item}-${index}`"
        class="cf-path-preview__step"
      >
        {{ item }}
      </span>
    </div>

    <div v-if="result?.warnings?.length" class="cf-path-preview__warnings">
      <strong>预演提醒</strong>
      <span v-for="warning in result.warnings" :key="warning">{{ warning }}</span>
    </div>

    <div v-if="result?.steps?.length" class="cf-path-preview__details">
      <article
        v-for="step in result.steps"
        :key="`${step.order}-${step.stageKey}`"
        :class="{ 'is-clickable': step.stepType === 'stage' }"
        @click="step.stepType === 'stage' && emit('step-select', step.stageKey)"
      >
        <div class="cf-path-preview__detail-head">
          <strong>{{ step.stageName }}</strong>
          <span>{{ step.reason || step.selectedRouteName || step.stepType }}</span>
        </div>
        <!-- 人工节点：该节点将派给谁（ApproverResolver 干跑真值） -->
        <div v-if="step.approver" class="cf-path-preview__approver">
          <span class="cf-path-preview__approver-strategy">处理人（{{ strategyLabel(step.approver.strategy) }}）</span>
          <span
            class="cf-path-preview__approver-names"
            :class="{ 'is-error': !!step.approver.error, 'is-fallback': !step.approver.error && !step.approver.approverNames.length }"
          >{{ approverText(step.approver) }}</span>
        </div>
        <!-- M5-2 失败态推演标注：兜底继续为黄底，硬失败为红底；点"去配置"跳该节点 -->
        <div
          v-if="step.failure"
          class="cf-path-preview__failure"
          :class="step.failure.fallbackApplied ? 'is-fallback' : 'is-hard'"
          @click.stop="step.stepType === 'stage' && emit('step-select', step.stageKey)"
        >
          <span class="cf-path-preview__failure-tag">{{ failureLabel(step.failure.kind) }}</span>
          <span class="cf-path-preview__failure-msg">{{ step.failure.message }}</span>
          <a
            v-if="step.stepType === 'stage'"
            class="cf-path-preview__failure-link"
            @click.stop="emit('step-select', step.stageKey)"
          >去配置</a>
        </div>
        <div v-if="step.candidates?.length" class="cf-path-preview__candidates">
          <span
            v-for="candidate in step.candidates"
            :key="candidate.edgeKey"
            :class="{ 'is-hit': candidate.matched }"
          >
            {{ candidate.routeName }}：{{ candidate.explanation }}
          </span>
        </div>
        <!-- M5-3 凭证试算：自动凭证节点按需展开借贷分录预览 -->
        <div v-if="isAutoVoucherStep(step)" class="cf-path-preview__voucher">
          <a-button
            size="small"
            :loading="voucherLoadingKey === step.stageKey"
            @click.stop="tryPreviewVoucher(step)"
          >试算凭证</a-button>
          <div v-if="voucherByStageKey[step.stageKey]" class="cf-path-preview__voucher-body">
            <div
              v-if="voucherByStageKey[step.stageKey].entries.length"
              class="cf-path-preview__voucher-entries"
            >
              <div
                v-for="(entry, ei) in voucherByStageKey[step.stageKey].entries"
                :key="ei"
                class="cf-path-preview__voucher-entry"
              >
                <span class="cf-path-preview__voucher-dir">{{ directionLabel(entry.direction) }}</span>
                <span class="cf-path-preview__voucher-acct">{{ entry.accountName }}</span>
                <span class="cf-path-preview__voucher-amt">{{ entry.amount }}</span>
              </div>
            </div>
            <div v-else class="cf-path-preview__voucher-note">
              {{ voucherByStageKey[step.stageKey].message }}
            </div>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-path-preview {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.cf-path-preview__head {
  display: flex;
  justify-content: space-between;
  gap: 12px;

  strong,
  span {
    display: block;
  }

  strong { color: var(--text-1); font-size: 14px; }
  span { margin-top: 2px; color: var(--text-2); font-size: 12px; }
}

.cf-path-preview__source {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cf-path-preview__history {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);
}

.cf-path-preview__history-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  max-height: 120px;
  overflow-y: auto;
}

.cf-path-preview__history-item {
  padding: 4px 9px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--bg-card);
  color: var(--text-1);
  font-size: 12px;
  cursor: pointer;
  transition: border-color 0.15s;

  &:hover {
    border-color: var(--color-primary);
    color: var(--color-primary);
  }
}

.cf-path-preview__history-missing {
  font-size: 12px;
  color: var(--color-warning-text);
}

.cf-path-preview__form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;

  label {
    display: flex;
    flex-direction: column;
    gap: 5px;
  }

  label > span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-path-preview__no-fields {
  grid-column: 1 / -1;
  padding: 10px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  color: var(--text-3);
  font-size: 12px;
  text-align: center;
}

.cf-path-preview__path {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);
}

.cf-path-preview__step {
  position: relative;
  padding: 5px 9px;
  border-radius: 999px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  color: var(--text-1);
  font-size: 12px;

  &:not(:last-child)::after {
    content: '→';
    position: absolute;
    right: -13px;
    color: var(--text-3);
  }
}

.cf-path-preview__warnings,
.cf-path-preview__details {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cf-path-preview__warnings {
  padding: 9px 10px;
  border: 1px solid var(--color-warning);
  border-radius: 6px;
  background: var(--color-warning-light);

  strong,
  span {
    color: var(--color-warning-text);
    font-size: 12px;
  }
}

.cf-path-preview__details article {
  padding: 9px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);

  &.is-clickable {
    cursor: pointer;
    transition: border-color 0.15s;

    &:hover {
      border-color: var(--color-primary);
    }
  }
}

.cf-path-preview__approver {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-top: 6px;
  font-size: 12px;

  &-strategy {
    flex-shrink: 0;
    color: var(--text-3);
  }

  &-names {
    color: var(--text-1);
    word-break: break-all;

    &.is-error {
      color: var(--color-danger);
    }

    &.is-fallback {
      color: var(--color-warning);
    }
  }
}

.cf-path-preview__failure {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
  padding: 6px 9px;
  border-radius: 6px;
  cursor: pointer;

  &.is-hard {
    border: 1px solid var(--color-danger-border);
    background: var(--color-danger-light);
  }

  &.is-fallback {
    border: 1px solid var(--color-warning);
    background: var(--color-warning-light);
  }

  &-tag {
    flex-shrink: 0;
    font-size: 12px;
    font-weight: 600;

    .is-hard & { color: var(--color-danger-text); }
    .is-fallback & { color: var(--color-warning-text); }
  }

  &-msg {
    flex: 1;
    min-width: 0;
    font-size: 12px;
    word-break: break-all;

    .is-hard & { color: var(--color-danger-text); }
    .is-fallback & { color: var(--color-warning-text); }
  }

  &-link {
    flex-shrink: 0;
    font-size: 12px;
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.cf-path-preview__detail-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-path-preview__candidates {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-top: 8px;

  span {
    color: var(--text-2);
    font-size: 12px;
  }

  .is-hit {
    color: var(--color-primary);
    font-weight: 600;
  }
}

.cf-path-preview__voucher {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 8px;
}

.cf-path-preview__voucher-body {
  padding: 8px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);
}

.cf-path-preview__voucher-entries {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cf-path-preview__voucher-entry {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 12px;
}

.cf-path-preview__voucher-dir {
  flex-shrink: 0;
  width: 16px;
  color: var(--text-3);
}

.cf-path-preview__voucher-acct {
  flex: 1;
  min-width: 0;
  color: var(--text-1);
  word-break: break-all;
}

.cf-path-preview__voucher-amt {
  flex-shrink: 0;
  color: var(--text-1);
  font-variant-numeric: tabular-nums;
}

.cf-path-preview__voucher-note {
  font-size: 12px;
  color: var(--text-2);
}
</style>
