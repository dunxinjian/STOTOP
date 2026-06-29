<script setup lang="ts">
import { ref } from 'vue'
import type { SchemaFieldDefinition } from '@/types/cardflow'
import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'

// ==================== Props & Emits ====================

interface Props {
  fields: SchemaFieldDefinition[]
  modelValue: Record<string, any>
  platform?: 'pc' | 'mobile'
  errors?: Record<string, string>
}

const props = withDefaults(defineProps<Props>(), {
  platform: 'pc',
  errors: () => ({}),
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, any>): void
}>()

// ==================== State ====================

const internalErrors = ref<Record<string, string>>({})

// ==================== Methods ====================

function onModelUpdate(value: Record<string, any>) {
  emit('update:modelValue', value)
}

/**
 * 校验所有 required 字段是否已填写
 */
function validate(): { valid: boolean; errors: Record<string, string> } {
  const errs: Record<string, string> = {}

  for (const field of props.fields) {
    if (!field.required) continue

    const val = props.modelValue?.[field.key]
    const isEmpty =
      val === null ||
      val === undefined ||
      val === '' ||
      (Array.isArray(val) && val.length === 0)

    if (isEmpty) {
      errs[field.key] = `${field.label}为必填项`
    }
  }

  internalErrors.value = errs
  return { valid: Object.keys(errs).length === 0, errors: errs }
}

// ==================== Expose ====================

defineExpose({ validate })
</script>

<template>
  <div v-if="fields && fields.length > 0" class="stage-input-fields">
    <div class="stage-input-fields__header">
      <h3 class="stage-input-fields__title">请填写补充信息</h3>
      <div class="stage-input-fields__divider" />
    </div>

    <SchemaRenderer
      :schema="fields"
      :model-value="modelValue"
      mode="edit"
      :platform="platform"
      :errors="{ ...internalErrors, ...errors }"
      @update:model-value="onModelUpdate"
    />
  </div>
</template>

<style scoped lang="scss">
.stage-input-fields {
  margin-top: 16px;
  background: var(--bg-muted);
  border-radius: 8px;
  padding: 16px;

  &__header {
    margin-bottom: 12px;
  }

  &__title {
    margin: 0 0 8px;
    font-size: 14px;
    font-weight: 500;
    color: var(--text-2);
    line-height: 1.5;
  }

  &__divider {
    height: 1px;
    background: var(--border);
  }
}
</style>
