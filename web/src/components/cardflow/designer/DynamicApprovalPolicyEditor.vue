<script setup lang="ts">
import { computed } from 'vue'
import ConditionBuilder from '../ConditionBuilder.vue'
import type { ConditionGroup, FieldOption } from '../ConditionBuilder.vue'
import type { DynamicStagePolicyRequest, SchemaFieldDefinition } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'

const props = defineProps<{
  modelValue: DynamicStagePolicyRequest[]
  sourceStageKey?: string | null
  stages: StageDefinition[]
  fields: SchemaFieldDefinition[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: DynamicStagePolicyRequest[]): void
}>()

const STRATEGY_OPTIONS = [
  { value: 'amountMatrix', label: '按金额矩阵加签', hint: '按金额区间插入不同层级审批人' },
  { value: 'orgChain', label: '按组织链逐级审批', hint: '从发起组织逐级寻找负责人' },
  { value: 'role', label: '按发起人角色决定审批人', hint: '按角色解析处理人' },
  { value: 'feeTypeBp', label: '按费用类型指定财务 BP', hint: '不同费用类型匹配不同 BP' },
  { value: 'fieldUsers', label: '按字段人员作为审批人', hint: '从卡片人员字段读取审批人' },
  { value: 'fixedUsers', label: '指定人员', hint: '固定人员列表' },
]

const TRIGGER_OPTIONS = [
  { value: 'afterSourceBeforeRoute', label: '源节点完成后，路由前' },
  { value: 'afterRouteBeforeTarget', label: '条件路由命中后，目标节点前' },
  { value: 'afterTarget', label: '目标节点后' },
  { value: 'replaceTargetHandlers', label: '替换目标节点处理人' },
]

const INSERT_POSITIONS = [
  { value: 'afterSource', label: '插在来源节点后' },
  { value: 'beforeTarget', label: '插在目标节点前' },
  { value: 'afterTarget', label: '插在目标节点后' },
]

const FALLBACK_OPTIONS = [
  { value: 'failSubmit', label: '提交失败' },
  { value: 'flowAdmin', label: '转审批管理员' },
]

const fieldOptions = computed<FieldOption[]>(() =>
  props.fields.map(field => ({
    key: field.key,
    label: field.label || field.key,
    type: field.type,
  })),
)

const userFieldOptions = computed(() =>
  props.fields
    .filter(field => field.type === 'user')
    .map(field => ({ value: field.key, label: field.label || field.key })),
)

const amountFieldOptions = computed(() =>
  props.fields
    .filter(field => field.type === 'money' || (field.type as string) === 'number')
    .map(field => ({ value: field.key, label: field.label || field.key })),
)

const feeTypeFieldOptions = computed(() =>
  props.fields
    .filter(field => field.type === 'enum' || field.type === 'text')
    .map(field => ({ value: field.key, label: field.label || field.key })),
)

const stageOptions = computed(() =>
  props.stages.map(stage => ({ value: stage.id, label: stage.name || stage.id })),
)

const currentPolicies = computed(() =>
  props.modelValue.filter(policy => policy.sourceStageKey === props.sourceStageKey),
)

function emitPolicies(nextPolicy: DynamicStagePolicyRequest) {
  const next = props.modelValue.some(policy => policy.policyKey === nextPolicy.policyKey)
    ? props.modelValue.map(policy => policy.policyKey === nextPolicy.policyKey ? nextPolicy : policy)
    : [...props.modelValue, nextPolicy]
  emit('update:modelValue', next)
}

function removePolicy(policyKey: string) {
  emit('update:modelValue', props.modelValue.filter(policy => policy.policyKey !== policyKey))
}

function addPolicy() {
  if (!props.sourceStageKey) return
  const suffix = Math.random().toString(36).slice(2, 8)
  emit('update:modelValue', [
    ...props.modelValue,
    {
      policyKey: `pol_${props.sourceStageKey}_${suffix}`,
      sourceStageKey: props.sourceStageKey,
      policyName: '动态审批策略',
      strategyType: 'amountMatrix',
      strategyConfigJson: JSON.stringify({ amountField: amountFieldOptions.value[0]?.value || 'amount', ranges: [] }),
      conditionJson: null,
      triggerTiming: 'afterRouteBeforeTarget',
      insertPosition: 'beforeTarget',
      continuationStageKey: null,
      priority: currentPolicies.value.length + 1,
      maxInsertCount: 20,
      fallbackJson: JSON.stringify({ type: 'flowAdmin' }),
      status: 'active',
    },
  ])
}

function parseObject(json?: string | null): Record<string, any> {
  if (!json) return {}
  try {
    const parsed = JSON.parse(json)
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {}
  } catch {
    return {}
  }
}

function parseCondition(json?: string | null): ConditionGroup {
  const parsed = parseObject(json)
  return Array.isArray(parsed.conditions) ? parsed as ConditionGroup : { logic: 'and', conditions: [] }
}

function patch(policy: DynamicStagePolicyRequest, partial: Partial<DynamicStagePolicyRequest>) {
  emitPolicies({ ...policy, ...partial })
}

function getConfig(policy: DynamicStagePolicyRequest) {
  return parseObject(policy.strategyConfigJson)
}

function setConfig(policy: DynamicStagePolicyRequest, patchValue: Record<string, any>) {
  const next = { ...getConfig(policy), ...patchValue }
  patch(policy, { strategyConfigJson: JSON.stringify(next) })
}

function setFallback(policy: DynamicStagePolicyRequest, type: string) {
  patch(policy, { fallbackJson: JSON.stringify({ type }) })
}

function fallbackType(policy: DynamicStagePolicyRequest) {
  return parseObject(policy.fallbackJson).type || 'failSubmit'
}

function setCondition(policy: DynamicStagePolicyRequest, condition: ConditionGroup) {
  patch(policy, {
    conditionJson: condition.conditions.length ? JSON.stringify(condition) : null,
  })
}

function getRanges(policy: DynamicStagePolicyRequest) {
  const ranges = getConfig(policy).ranges
  return Array.isArray(ranges) ? ranges : []
}

function setRange(policy: DynamicStagePolicyRequest, index: number, partial: Record<string, any>) {
  const ranges = [...getRanges(policy)]
  ranges[index] = { ...ranges[index], ...partial }
  setConfig(policy, { ranges })
}

function addRange(policy: DynamicStagePolicyRequest) {
  const ranges = [...getRanges(policy), { min: null, max: null, roleCode: '' }]
  setConfig(policy, { ranges })
}

function removeRange(policy: DynamicStagePolicyRequest, index: number) {
  const ranges = getRanges(policy).filter((_, idx) => idx !== index)
  setConfig(policy, { ranges })
}

function getMapping(policy: DynamicStagePolicyRequest) {
  const mapping = getConfig(policy).mapping
  return Array.isArray(mapping) ? mapping : []
}

function setMapping(policy: DynamicStagePolicyRequest, index: number, partial: Record<string, any>) {
  const mapping = [...getMapping(policy)]
  mapping[index] = { ...mapping[index], ...partial }
  setConfig(policy, { mapping })
}

function addMapping(policy: DynamicStagePolicyRequest) {
  const mapping = [...getMapping(policy), { feeType: '', roleCode: '' }]
  setConfig(policy, { mapping })
}

function removeMapping(policy: DynamicStagePolicyRequest, index: number) {
  const mapping = getMapping(policy).filter((_, idx) => idx !== index)
  setConfig(policy, { mapping })
}

function numericTagsToArray(value: string[] | number[] | undefined) {
  return (value || [])
    .map(item => Number(item))
    .filter(item => Number.isFinite(item) && item > 0)
}
</script>

<template>
  <section class="cf-dynamic-policy-editor">
    <header class="cf-dynamic-policy-editor__head">
      <div>
        <strong>动态人工节点</strong>
        <span>运行时按策略插入，不修改流程定义本身</span>
      </div>
      <a-button size="small" type="primary" ghost :disabled="!sourceStageKey" @click="addPolicy">
        添加策略
      </a-button>
    </header>

    <div v-if="!sourceStageKey" class="cf-dynamic-policy-editor__empty">
      请先选择一个人工节点
    </div>
    <div v-else-if="currentPolicies.length === 0" class="cf-dynamic-policy-editor__empty">
      当前节点暂未配置动态审批策略
    </div>

    <article
      v-for="policy in currentPolicies"
      :key="policy.policyKey"
      class="cf-dynamic-policy-card"
    >
      <div class="cf-dynamic-policy-card__top">
        <a-input
          :value="policy.policyName"
          placeholder="策略名称"
          @update:value="(value: string) => patch(policy, { policyName: value })"
        />
        <a-button size="small" danger type="text" @click="removePolicy(policy.policyKey)">删除</a-button>
      </div>

      <div class="cf-dynamic-policy-card__grid">
        <label>
          <span>策略模板</span>
          <a-select
            :value="policy.strategyType"
            :options="STRATEGY_OPTIONS"
            style="width: 100%"
            @change="(value: any) => patch(policy, { strategyType: value })"
          />
        </label>
        <label>
          <span>触发时机</span>
          <a-select
            :value="policy.triggerTiming"
            :options="TRIGGER_OPTIONS"
            style="width: 100%"
            @change="(value: any) => patch(policy, { triggerTiming: value })"
          />
        </label>
        <label>
          <span>插入位置</span>
          <a-select
            :value="policy.insertPosition"
            :options="INSERT_POSITIONS"
            style="width: 100%"
            @change="(value: any) => patch(policy, { insertPosition: value })"
          />
        </label>
        <label>
          <span>继续节点</span>
          <a-select
            :value="policy.continuationStageKey ?? undefined"
            :options="stageOptions"
            allow-clear
            style="width: 100%"
            @change="(value: any) => patch(policy, { continuationStageKey: value || null })"
          />
        </label>
        <label>
          <span>优先级</span>
          <a-input-number
            :value="policy.priority"
            :min="1"
            style="width: 100%"
            @update:value="(value: string | number) => patch(policy, { priority: Number(value) || 1 })"
          />
        </label>
        <label>
          <span>最大插入数</span>
          <a-input-number
            :value="policy.maxInsertCount ?? undefined"
            :min="1"
            :max="20"
            style="width: 100%"
            @update:value="(value: string | number) => patch(policy, { maxInsertCount: Number(value) || 20 })"
          />
        </label>
      </div>

      <section class="cf-dynamic-policy-card__strategy">
        <div class="cf-dynamic-policy-card__title">
          {{ STRATEGY_OPTIONS.find(item => item.value === policy.strategyType)?.label }}
          <span>{{ STRATEGY_OPTIONS.find(item => item.value === policy.strategyType)?.hint }}</span>
        </div>

        <template v-if="policy.strategyType === 'amountMatrix'">
          <label class="cf-policy-field">
            <span>金额字段 amountField</span>
            <a-select
              :value="getConfig(policy).amountField"
              :options="amountFieldOptions"
              style="width: 100%"
              @change="(value: any) => setConfig(policy, { amountField: value })"
            />
          </label>
          <div class="cf-policy-rows">
            <div v-for="(range, index) in getRanges(policy)" :key="index" class="cf-policy-row">
              <a-input-number :value="range.min" placeholder="最小金额" @update:value="(value: string | number) => setRange(policy, index, { min: value })" />
              <a-input-number :value="range.max" placeholder="最大金额" @update:value="(value: string | number) => setRange(policy, index, { max: value })" />
              <a-input :value="range.roleCode" placeholder="角色编码 roleCode" @update:value="(value: string) => setRange(policy, index, { roleCode: value })" />
              <a-button size="small" danger type="text" @click="removeRange(policy, index)">移除</a-button>
            </div>
            <a-button size="small" type="dashed" block @click="addRange(policy)">添加金额区间</a-button>
          </div>
        </template>

        <template v-else-if="policy.strategyType === 'orgChain'">
          <div class="cf-dynamic-policy-card__grid">
            <label>
              <span>起始组织 ID</span>
              <a-input-number :value="getConfig(policy).startOrgId" style="width: 100%" @update:value="(value: string | number) => setConfig(policy, { startOrgId: value })" />
            </label>
            <label>
              <span>截止组织编码</span>
              <a-input :value="getConfig(policy).stopOrgCode" @update:value="(value: string) => setConfig(policy, { stopOrgCode: value })" />
            </label>
            <label>
              <span>最大层级</span>
              <a-input-number :value="getConfig(policy).maxLevels || 20" :min="1" :max="20" style="width: 100%" @update:value="(value: string | number) => setConfig(policy, { maxLevels: Number(value) || 20 })" />
            </label>
          </div>
        </template>

        <template v-else-if="policy.strategyType === 'role'">
          <label class="cf-policy-field">
            <span>角色编码 roleCode</span>
            <a-input :value="getConfig(policy).roleCode" placeholder="如 finance_manager" @update:value="(value: string) => setConfig(policy, { roleCode: value })" />
          </label>
        </template>

        <template v-else-if="policy.strategyType === 'feeTypeBp'">
          <label class="cf-policy-field">
            <span>费用类型字段 fieldKey</span>
            <a-select
              :value="getConfig(policy).fieldKey"
              :options="feeTypeFieldOptions"
              style="width: 100%"
              @change="(value: any) => setConfig(policy, { fieldKey: value })"
            />
          </label>
          <div class="cf-policy-rows">
            <div v-for="(item, index) in getMapping(policy)" :key="index" class="cf-policy-row">
              <a-input :value="item.feeType" placeholder="费用类型值" @update:value="(value: string) => setMapping(policy, index, { feeType: value })" />
              <a-input :value="item.roleCode" placeholder="BP角色编码" @update:value="(value: string) => setMapping(policy, index, { roleCode: value })" />
              <a-button size="small" danger type="text" @click="removeMapping(policy, index)">移除</a-button>
            </div>
            <a-button size="small" type="dashed" block @click="addMapping(policy)">添加费用类型映射</a-button>
          </div>
        </template>

        <template v-else-if="policy.strategyType === 'fieldUsers'">
          <label class="cf-policy-field">
            <span>人员字段 fieldKey</span>
            <a-select
              :value="getConfig(policy).fieldKey"
              :options="userFieldOptions"
              style="width: 100%"
              @change="(value: any) => setConfig(policy, { fieldKey: value })"
            />
          </label>
        </template>

        <template v-else>
          <label class="cf-policy-field">
            <span>人员 ID 列表 userIds</span>
            <a-select
              mode="tags"
              :value="(getConfig(policy).userIds || []).map(String)"
              style="width: 100%"
              placeholder="输入用户 ID 后回车"
              @change="(value: any) => setConfig(policy, { userIds: numericTagsToArray(value) })"
            />
          </label>
        </template>
      </section>

      <section class="cf-dynamic-policy-card__condition">
        <div class="cf-dynamic-policy-card__title">适用条件</div>
        <ConditionBuilder
          :model-value="parseCondition(policy.conditionJson)"
          :fields="fieldOptions"
          @update:model-value="(value: ConditionGroup) => setCondition(policy, value)"
        />
      </section>

      <div class="cf-dynamic-policy-card__fallback">
        <span>安全兜底 fallbackJson</span>
        <a-radio-group
          :value="fallbackType(policy)"
          button-style="solid"
          size="small"
          @change="(event: any) => setFallback(policy, event.target.value)"
        >
          <a-radio-button v-for="option in FALLBACK_OPTIONS" :key="option.value" :value="option.value">
            {{ option.label }}
          </a-radio-button>
        </a-radio-group>
      </div>
    </article>
  </section>
</template>

<style scoped lang="scss">
.cf-dynamic-policy-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.cf-dynamic-policy-editor__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;

  strong,
  span {
    display: block;
  }

  strong { color: var(--text-1); font-size: 14px; }
  span { margin-top: 2px; color: var(--text-2); font-size: 12px; }
}

.cf-dynamic-policy-editor__empty {
  display: grid;
  place-items: center;
  min-height: 120px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  color: var(--text-3);
  font-size: 13px;
}

.cf-dynamic-policy-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);
}

.cf-dynamic-policy-card__top {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 8px;
}

.cf-dynamic-policy-card__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;

  label {
    display: flex;
    flex-direction: column;
    gap: 5px;
    min-width: 0;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-dynamic-policy-card__strategy,
.cf-dynamic-policy-card__condition {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);
}

.cf-dynamic-policy-card__title {
  color: var(--text-1);
  font-size: 13px;
  font-weight: 700;

  span {
    margin-left: 8px;
    color: var(--text-2);
    font-size: 12px;
    font-weight: 400;
  }
}

.cf-policy-field {
  display: flex;
  flex-direction: column;
  gap: 6px;

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-policy-rows {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cf-policy-row {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr auto;
  gap: 8px;
  align-items: center;
}

.cf-dynamic-policy-card__fallback {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}
</style>
