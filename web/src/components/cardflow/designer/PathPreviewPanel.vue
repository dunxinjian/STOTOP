<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { previewFlowDraftPath } from '@/api/cardflow'
import { normalizeOptionList } from '@/utils/cardflowFieldFormat'
import UserSelect from '@/components/cardflow/fields/UserSelect.vue'
import OrgSelect from '@/components/cardflow/fields/OrgSelect.vue'
import type { CardFlowPathPreviewDto, CardFlowPathPreviewStepDto, SchemaFieldDefinition } from '@/types/cardflow'

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
  } catch (e) {
    // 拦截器已弹出后端具体原因（如"没有可预演的流程版本"），此处不重复弹泛化提示
    console.error('[PathPreview] 预演失败:', e)
  } finally {
    loading.value = false
  }
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
        <div v-if="step.candidates?.length" class="cf-path-preview__candidates">
          <span
            v-for="candidate in step.candidates"
            :key="candidate.edgeKey"
            :class="{ 'is-hit': candidate.matched }"
          >
            {{ candidate.routeName }}：{{ candidate.explanation }}
          </span>
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
</style>
