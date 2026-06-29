<script setup lang="ts">
import { computed } from 'vue'
import type { CardComponentRuntime, SchemaFieldDefinition, SchemaFieldType } from '@/types/cardflow'
import CardDetailTable, { type DetailRow } from '../../CardDetailTable.vue'

const props = withDefaults(defineProps<{
  component: CardComponentRuntime
  modelValue?: DetailRow[]
  mode: 'edit' | 'view'
  platform?: 'pc' | 'mobile'
  accountSetId?: number | null
  orgId?: number | null
}>(), {
  modelValue: () => [],
  platform: 'pc',
  accountSetId: null,
  orgId: null,
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: DetailRow[]): void
}>()

const tableSchema = computed<SchemaFieldDefinition[]>(() =>
  props.component.columns.map((column) => ({
    key: column.key,
    label: column.label || column.key,
    type: (column.type || 'text') as SchemaFieldType,
    readonly: !column.editable || !props.component.editable,
    required: column.required,
  })),
)

const runtimeRows = computed<DetailRow[]>(() =>
  props.component.rows.map((row, index) => ({
    _id: row.id != null ? String(row.id) : `${props.component.id}_${index}`,
    ...row.values,
  })),
)

const rows = computed({
  get: () => props.modelValue.length > 0 ? props.modelValue : runtimeRows.value,
  set: (value: DetailRow[]) => emit('update:modelValue', value),
})

const effectiveMode = computed<'edit' | 'view'>(() =>
  props.mode === 'edit' && props.component.editable ? 'edit' : 'view',
)
</script>

<template>
  <section class="cf-detail-component">
    <div class="cf-detail-component__head">
      <span>{{ component.title || '明细' }}</span>
      <b v-if="component.required">必填</b>
    </div>
    <CardDetailTable
      v-if="tableSchema.length > 0"
      v-model="rows"
      :schema="tableSchema"
      :mode="effectiveMode"
      :platform="platform"
      :account-set-id="accountSetId"
      :org-id="orgId"
      compact
    />
    <div v-else class="cf-detail-component__empty">明细组件未配置列</div>
    <p v-for="warning in component.warnings" :key="warning" class="cf-detail-component__warning">
      {{ warning }}
    </p>
  </section>
</template>

<style scoped lang="scss">
.cf-detail-component {
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 12px;
  background: var(--bg-card);

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin-bottom: 10px;
    color: var(--text-1);
    font-size: 14px;
    font-weight: 600;

    b {
      color: var(--color-danger);
      font-size: 12px;
      font-weight: 500;
    }
  }

  &__empty,
  &__warning {
    color: var(--color-warning);
    font-size: 12px;
    line-height: 18px;
    margin: 8px 0 0;
  }
}
</style>
