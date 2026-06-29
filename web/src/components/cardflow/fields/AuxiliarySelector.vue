<script setup lang="ts">
/**
 * AuxiliarySelector.vue — 辅助核算选择器
 *
 * 根据 auxType 加载不同类型数据，存储为 {id, code, name, auxType}
 */
import { ref, watch, onMounted, computed } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { AuxiliaryFieldValue, AuxiliaryType } from '@/types/cardflow'
import { getAuxiliaryItems } from '@/api/cardflow'

interface Props {
  modelValue?: AuxiliaryFieldValue | null
  auxType: AuxiliaryType
  accountSetId?: number | null
  disabled?: boolean
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  accountSetId: null,
  disabled: false,
  placeholder: '请选择',
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: AuxiliaryFieldValue | null): void
}>()

const loading = ref(false)
const items = ref<Array<{ id: number; code?: string; name: string }>>([])
const selectedId = ref<number | undefined>(props.modelValue?.id ?? undefined)

const typeLabel = computed<Record<AuxiliaryType, string>>(() => ({
  employee: '员工',
  supplier: '供应商',
  customer: '客户',
  department: '部门',
  project: '项目',
}))

watch(
  () => props.modelValue,
  (v) => { selectedId.value = v?.id ?? undefined },
)

watch(() => [props.auxType, props.accountSetId], () => load())

onMounted(load)

async function load() {
  if (!props.accountSetId || !props.auxType) return
  loading.value = true
  try {
    items.value = await getAuxiliaryItems({
      accountSetId: props.accountSetId,
      auxType: props.auxType,
    })
  } catch {
    items.value = []
  } finally {
    loading.value = false
  }
}

function onChange(value: SelectValue) {
  const id = value as number | undefined
  if (id == null) {
    emit('update:modelValue', null)
    return
  }
  const it = items.value.find((i) => i.id === id)
  if (it) {
    emit('update:modelValue', { id: it.id, code: it.code, name: it.name, auxType: props.auxType })
  }
}

const options = computed(() =>
  items.value.map((i) => ({
    label: i.code ? `${i.code} ${i.name}` : i.name,
    value: i.id,
  })),
)
</script>

<template>
  <a-select
    :value="selectedId"
    :options="options"
    :loading="loading"
    :disabled="disabled"
    :placeholder="placeholder || `请选择${typeLabel[auxType] || ''}`"
    show-search
    allow-clear
    :filter-option="(input: string, opt: any) => opt.label.toLowerCase().includes(input.toLowerCase())"
    style="width: 100%"
    @change="onChange"
  />
</template>
