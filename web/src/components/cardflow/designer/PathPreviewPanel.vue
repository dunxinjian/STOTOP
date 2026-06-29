<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { previewFlowDraftPath } from '@/api/cardflow'
import type { CardFlowPathPreviewDto } from '@/types/cardflow'

const props = defineProps<{
  flowDefinitionId?: number | null
  previewApi?: typeof previewFlowDraftPath
  disabled?: boolean
}>()

const sample = reactive({
  initiatorId: undefined as number | undefined,
  orgId: undefined as number | undefined,
  amount: 5000,
  feeType: '差旅费',
  hasExpenseRequest: false,
  hasLoan: false,
  cardStatus: 'draft',
})

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
    const dataJson = JSON.stringify({
      amount: sample.amount,
      feeType: sample.feeType,
      hasExpenseRequest: sample.hasExpenseRequest,
      hasLoan: sample.hasLoan,
      cardStatus: sample.cardStatus,
    })
    const api = props.previewApi || previewFlowDraftPath
    result.value = await api(props.flowDefinitionId, {
      dataJson,
      initialDataJson: dataJson,
      initiatorId: sample.initiatorId || null,
      orgId: sample.orgId || null,
      maxSteps: 20,
    })
  } catch {
    message.error('路径预演失败')
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
        <a-input-number v-model:value="sample.initiatorId" :disabled="disabled" placeholder="用户ID" style="width: 100%" />
      </label>
      <label>
        <span>组织</span>
        <a-input-number v-model:value="sample.orgId" :disabled="disabled" placeholder="组织ID" style="width: 100%" />
      </label>
      <label>
        <span>金额</span>
        <a-input-number v-model:value="sample.amount" :disabled="disabled" :min="0" style="width: 100%" />
      </label>
      <label>
        <span>费用类型</span>
        <a-select v-model:value="sample.feeType" :disabled="disabled" style="width: 100%">
          <a-select-option value="差旅费">差旅费</a-select-option>
          <a-select-option value="招待费">招待费</a-select-option>
          <a-select-option value="办公费">办公费</a-select-option>
          <a-select-option value="其他">其他</a-select-option>
        </a-select>
      </label>
      <label>
        <span>卡片状态</span>
        <a-select v-model:value="sample.cardStatus" :disabled="disabled" style="width: 100%">
          <a-select-option value="draft">草稿</a-select-option>
          <a-select-option value="active">审批中</a-select-option>
          <a-select-option value="completed">已完成</a-select-option>
        </a-select>
      </label>
      <div class="cf-path-preview__checks">
        <a-checkbox v-model:checked="sample.hasExpenseRequest" :disabled="disabled">引用请款</a-checkbox>
        <a-checkbox v-model:checked="sample.hasLoan" :disabled="disabled">引用借款</a-checkbox>
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
      <article v-for="step in result.steps" :key="`${step.order}-${step.stageKey}`">
        <div class="cf-path-preview__detail-head">
          <strong>{{ step.stageName }}</strong>
          <span>{{ step.reason || step.selectedRouteName || step.stepType }}</span>
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

.cf-path-preview__checks {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-top: 22px;
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
