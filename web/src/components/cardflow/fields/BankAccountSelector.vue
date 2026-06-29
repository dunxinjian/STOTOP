<script setup lang="ts">
/**
 * BankAccountSelector.vue — 银行账户选择器
 *
 * 选择后自动带出对应财务科目信息（accountId/accountCode）
 */
import { ref, watch, onMounted, computed } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { BankAccountFieldValue } from '@/types/cardflow'
import { getBankAccounts } from '@/api/cardflow'

interface Props {
  modelValue?: BankAccountFieldValue | null
  orgId?: number | null
  disabled?: boolean
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  orgId: null,
  disabled: false,
  placeholder: '请选择银行账户',
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: BankAccountFieldValue | null): void
}>()

const loading = ref(false)
const items = ref<
  Array<{
    id: number
    accountNo: string
    accountName?: string
    bankName?: string
    accountId?: number
    accountCode?: string
  }>
>([])
const selectedId = ref<number | undefined>(props.modelValue?.id ?? undefined)

watch(
  () => props.modelValue,
  (v) => { selectedId.value = v?.id ?? undefined },
)

watch(() => props.orgId, () => load())

onMounted(load)

async function load() {
  if (!props.orgId) return
  loading.value = true
  try {
    items.value = await getBankAccounts({ orgId: props.orgId })
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
    emit('update:modelValue', {
      id: it.id,
      accountNo: it.accountNo,
      accountName: it.accountName,
      bankName: it.bankName,
      accountId: it.accountId,
      accountCode: it.accountCode,
    })
  }
}

const options = computed(() =>
  items.value.map((i) => ({
    label: `${i.accountNo}${i.bankName ? ' · ' + i.bankName : ''}${
      i.accountName ? ' · ' + i.accountName : ''
    }`,
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
    :placeholder="placeholder"
    show-search
    allow-clear
    :filter-option="(input: string, opt: any) => opt.label.toLowerCase().includes(input.toLowerCase())"
    style="width: 100%"
    @change="onChange"
  />
</template>
