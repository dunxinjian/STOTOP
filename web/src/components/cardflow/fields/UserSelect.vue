<script setup lang="ts">
/**
 * UserSelect.vue — 人员选择器（远端搜索，存 {id, name}）
 * 历史裸 ID / 字符串值降级显示为 #id，不报错。
 */
import { ref, watch, onMounted } from 'vue'
import type { UserFieldValue } from '@/types/cardflow'
import { useUserSearch } from '@/composables/useUserSearch'

interface Props {
  modelValue?: UserFieldValue | number | string | null
  disabled?: boolean
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  disabled: false,
  placeholder: '请选择人员',
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: UserFieldValue | null): void
}>()

const { userOptions, loading, load, search, pin } = useUserSearch()
const selectedId = ref<number | null>(null)

function normalize(v: Props['modelValue']): UserFieldValue | null {
  if (v === null || v === undefined || v === '') return null
  if (typeof v === 'object') {
    const id = Number((v as UserFieldValue).id)
    return Number.isFinite(id) ? { id, name: (v as UserFieldValue).name || `#${id}`, orgName: (v as UserFieldValue).orgName } : null
  }
  const id = Number(v)
  return Number.isFinite(id) ? { id, name: `#${id}` } : null
}

function syncFromModel() {
  const val = normalize(props.modelValue)
  selectedId.value = val?.id ?? null
  if (val) {
    pin({
      label: [val.name, val.orgName].filter(Boolean).join(' / '),
      value: val.id,
      name: val.name,
      orgName: val.orgName,
    })
  }
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
  const opt = userOptions.value.find((o) => o.value === id)
  emit('update:modelValue', { id, name: opt?.name || `#${id}`, orgName: opt?.orgName })
}
</script>

<template>
  <a-select
    :value="selectedId ?? undefined"
    :options="userOptions"
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
