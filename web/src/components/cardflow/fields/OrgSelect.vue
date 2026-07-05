<script setup lang="ts">
/**
 * OrgSelect.vue — 组织选择器（客户端过滤组织树，存 {id, name}）
 * 历史裸 ID / 字符串值降级显示为 #id。
 */
import { ref, watch, onMounted } from 'vue'
import type { OrgFieldValue } from '@/types/cardflow'
import { useOrgSearch } from '@/composables/useOrgSearch'

interface Props {
  modelValue?: OrgFieldValue | number | string | null
  disabled?: boolean
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  disabled: false,
  placeholder: '请选择组织',
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: OrgFieldValue | null): void
}>()

const { orgOptions, loading, load, search, pin } = useOrgSearch()
const selectedId = ref<number | null>(null)

function normalize(v: Props['modelValue']): OrgFieldValue | null {
  if (v === null || v === undefined || v === '') return null
  if (typeof v === 'object') {
    const id = Number((v as OrgFieldValue).id)
    return Number.isFinite(id) ? { id, name: (v as OrgFieldValue).name || `#${id}` } : null
  }
  const id = Number(v)
  return Number.isFinite(id) ? { id, name: `#${id}` } : null
}

function syncFromModel() {
  const val = normalize(props.modelValue)
  selectedId.value = val?.id ?? null
  if (val) pin({ label: val.name, value: val.id, name: val.name })
}

watch(() => props.modelValue, syncFromModel)
onMounted(() => {
  syncFromModel()
  load()
})

function onChange(raw: any) {
  const id = raw == null ? null : Number(raw)
  if (id == null || !Number.isFinite(id)) {
    emit('update:modelValue', null)
    return
  }
  const opt = orgOptions.value.find((o) => o.value === id)
  emit('update:modelValue', { id, name: opt?.name || `#${id}` })
}
</script>

<template>
  <a-select
    :value="selectedId ?? undefined"
    :options="orgOptions"
    :disabled="disabled"
    :placeholder="placeholder"
    :loading="loading"
    :filter-option="false"
    show-search
    allow-clear
    style="width: 100%"
    @search="search"
    @change="onChange"
  />
</template>
