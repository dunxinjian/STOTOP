<template>
  <a-select
    :value="modelValue"
    :placeholder="placeholder"
    show-search
    :filter-option="false"
    :options="options"
    :loading="searching"
    :disabled="disabled"
    allow-clear
    @search="handleSearch"
    @change="onChange"
  />
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { getEmployeeList, type EmployeeDto } from '@/api/hr'

const props = withDefaults(defineProps<{
  modelValue: number | undefined
  /** 编辑回填时已选员工的显示名，确保不发起搜索也能显示名字 */
  initialLabel?: string
  placeholder?: string
  disabled?: boolean
}>(), {
  placeholder: '请输入姓名或工号搜索员工',
  disabled: false,
})

const emit = defineEmits<{
  (e: 'update:modelValue', v: number | undefined): void
  (e: 'change', emp: EmployeeDto | undefined): void
}>()

const options = ref<{ label: string; value: number; data?: EmployeeDto }[]>([])
const searching = ref(false)
let timer: ReturnType<typeof setTimeout> | null = null

// 编辑回填：有已选值+初始名时预置一个选项，避免显示成空白 ID
watch(
  () => [props.modelValue, props.initialLabel] as const,
  ([val, label]) => {
    if (val && label && !options.value.some(o => o.value === val)) {
      options.value = [{ label, value: val as number }, ...options.value]
    }
  },
  { immediate: true }
)

function handleSearch(value: string) {
  if (timer) clearTimeout(timer)
  if (!value || value.trim().length < 1) return
  timer = setTimeout(async () => {
    searching.value = true
    try {
      const res = await getEmployeeList({ keyword: value.trim(), pageIndex: 1, pageSize: 20, employeeStatus: 1 })
      options.value = (res?.items || []).map((emp: EmployeeDto) => ({
        label: `${emp.name} (${emp.fuid || emp.id})`,
        value: emp.id,
        data: emp,
      }))
    } catch (e) {
      console.error('搜索员工失败:', e)
      options.value = []
    } finally {
      searching.value = false
    }
  }, 300)
}

function onChange(val: any) {
  emit('update:modelValue', (val ?? undefined) as number | undefined)
  emit('change', options.value.find(o => o.value === val)?.data)
}
</script>
