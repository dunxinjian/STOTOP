<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ConditionBuilder from '../ConditionBuilder.vue'
import type { ConditionGroup, FieldOption } from '../ConditionBuilder.vue'
import type { SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'

const props = defineProps<{
  modelValue: StageRouteRuleRequest | null
  stages: StageDefinition[]
  fields: SchemaFieldDefinition[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: StageRouteRuleRequest): void
  (e: 'delete', edgeKey: string): void
}>()

const condition = ref<ConditionGroup>({ logic: 'and', conditions: [] })

const fieldOptions = computed<FieldOption[]>(() =>
  props.fields.map(field => ({
    key: field.key,
    label: field.label || field.key,
    type: field.type,
  })),
)

const stageOptions = computed(() =>
  props.stages.map(stage => ({
    value: stage.id,
    label: stage.name || stage.id,
  })),
)

watch(
  () => props.modelValue?.edgeKey,
  () => {
    condition.value = parseCondition(props.modelValue?.conditionJson)
  },
  { immediate: true },
)

watch(condition, value => {
  if (!props.modelValue || props.modelValue.isDefault) return
  patch({
    conditionJson: value.conditions.length ? JSON.stringify(value) : null,
  })
}, { deep: true })

function parseCondition(json?: string | null): ConditionGroup {
  if (!json) return { logic: 'and', conditions: [] }
  try {
    const parsed = JSON.parse(json)
    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.conditions)) {
      return parsed
    }
  } catch {
    // ignore invalid drafts and let users rebuild visually
  }
  return { logic: 'and', conditions: [] }
}

function patch(partial: Partial<StageRouteRuleRequest>) {
  if (!props.modelValue) return
  const next = { ...props.modelValue, ...partial }
  if (next.isDefault) {
    next.conditionJson = null
  }
  emit('update:modelValue', next)
}
</script>

<template>
  <section class="cf-route-editor">
    <div v-if="!modelValue" class="cf-route-editor__empty">
      请选择流程图中的一条条件边
    </div>

    <template v-else>
      <header class="cf-route-editor__head">
        <div>
          <strong>流转规则</strong>
          <span>{{ modelValue.edgeKey }}</span>
        </div>
        <a-button size="small" danger type="text" @click="emit('delete', modelValue.edgeKey)">
          删除
        </a-button>
      </header>

      <div class="cf-route-editor__grid">
        <label>
          <span>条件名称</span>
          <a-input
            :value="modelValue.routeName"
            placeholder="如：大额报销"
            @update:value="(value: string) => patch({ routeName: value })"
          />
        </label>

        <label>
          <span>来源节点</span>
          <a-select
            :value="modelValue.fromStageKey"
            :options="stageOptions"
            style="width: 100%"
            @change="(value: any) => patch({ fromStageKey: value })"
          />
        </label>

        <label>
          <span>目标节点</span>
          <a-select
            :value="modelValue.toStageKey"
            :options="stageOptions"
            style="width: 100%"
            @change="(value: any) => patch({ toStageKey: value })"
          />
        </label>

        <label>
          <span>优先级</span>
          <a-input-number
            :value="modelValue.priority"
            :min="1"
            style="width: 100%"
            @update:value="(value: string | number) => patch({ priority: Number(value) || 1 })"
          />
        </label>
      </div>

      <div class="cf-route-editor__default">
        <div>
          <strong>默认分支</strong>
          <span>每个来源节点必须有一个“其他情况”分支</span>
        </div>
        <a-switch
          :checked="modelValue.isDefault"
          @update:checked="(checked: boolean | string | number) => patch({ isDefault: Boolean(checked) })"
        />
      </div>

      <div class="cf-route-editor__condition">
        <div class="cf-route-editor__section-title">
          <strong>流转条件</strong>
          <span v-if="modelValue.isDefault">默认分支不需要配置条件</span>
          <span v-else>使用字段、操作符和值配置规则卡片</span>
        </div>
        <ConditionBuilder
          v-model="condition"
          :fields="fieldOptions"
          :disabled="modelValue.isDefault"
        />
      </div>
    </template>
  </section>
</template>

<style scoped lang="scss">
.cf-route-editor {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.cf-route-editor__empty {
  display: grid;
  place-items: center;
  min-height: 180px;
  color: var(--text-2);
  font-size: 13px;
}

.cf-route-editor__head,
.cf-route-editor__default,
.cf-route-editor__section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.cf-route-editor__head {
  padding-bottom: 10px;
  border-bottom: 1px solid var(--border);

  strong {
    display: block;
    color: var(--text-1);
    font-size: 14px;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-route-editor__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;

  label {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;
  }

  label > span,
  .cf-route-editor__section-title span {
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-route-editor__default {
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);

  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-route-editor__condition {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cf-route-editor__section-title {
  align-items: baseline;

  strong {
    color: var(--text-1);
    font-size: 13px;
  }
}
</style>
