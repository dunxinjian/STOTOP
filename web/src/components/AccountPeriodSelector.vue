<template>
  <div class="account-period-selector">
    <a-select
      v-model:value="selectedId"
      placeholder="选择账期"
      :loading="loading"
      @change="handleChange"
      style="width: 100%"
      size="middle"
    >
      <a-select-option
        v-for="item in periods"
        :key="item.id"
        :value="item.id"
      >
        {{ formatPeriod(item) }}
      </a-select-option>
    </a-select>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { getPeriods, getCurrentPeriod } from '@/api/finance'

// 期间数据类型
interface PeriodItem {
  id: number
  year: number
  periodNo: number
  periodName?: string
}

const props = defineProps<{
  accountSetId: number
}>()

const emit = defineEmits<{
  (e: 'change', periodId: number, period: { year: number; periodNo: number }): void
}>()

const periods = ref<PeriodItem[]>([])
const selectedId = ref<number>(0)
const loading = ref(false)

// 格式化期间显示
function formatPeriod(item: PeriodItem): string {
  return `${item.year}年${item.periodNo}月`
}

// 加载期间列表
async function loadPeriods() {
  if (!props.accountSetId) return

  loading.value = true
  try {
    const res = await getPeriods(props.accountSetId) as any[]
    periods.value = (res || []).map((item: any) => ({
      id: item.id,
      year: item.year ?? extractYear(item.periodName),
      periodNo: item.periodNo ?? extractPeriodNo(item.periodName),
      periodName: item.periodName
    }))
  } catch (error) {
    console.error('加载期间列表失败:', error)
    periods.value = []
  } finally {
    loading.value = false
  }
}

// 加载当前期间并设置为默认选中
async function loadCurrentPeriod() {
  try {
    const res = await getCurrentPeriod(props.accountSetId) as any
    if (res) {
      const currentPeriod: PeriodItem = {
        id: res.id,
        year: res.year ?? extractYear(res.periodName),
        periodNo: res.periodNo ?? extractPeriodNo(res.periodName),
        periodName: res.periodName
      }
      selectedId.value = currentPeriod.id
      emit('change', currentPeriod.id, { year: currentPeriod.year, periodNo: currentPeriod.periodNo })
    }
  } catch (error) {
    console.error('获取当前期间失败:', error)
    // 如果获取当前期间失败，选择列表中的第一个
    if (periods.value.length > 0) {
      const first = periods.value[0]
      selectedId.value = first.id
      emit('change', first.id, { year: first.year, periodNo: first.periodNo })
    }
  }
}

// 从 periodName 中解析年份（格式如 "2026-04" 或 "2026年4月"）
function extractYear(periodName?: string): number {
  if (!periodName) return new Date().getFullYear()
  const match = periodName.match(/(\d{4})/)
  return match ? parseInt(match[1]) : new Date().getFullYear()
}

// 从 periodName 中解析月份
function extractPeriodNo(periodName?: string): number {
  if (!periodName) return new Date().getMonth() + 1
  const match = periodName.match(/[年\-](\d{1,2})/)
  return match ? parseInt(match[1]) : new Date().getMonth() + 1
}

// 处理选择变化
function handleChange(val: SelectValue) {
  const id = val as number
  const period = periods.value.find(p => p.id === id)
  if (period) {
    emit('change', period.id, { year: period.year, periodNo: period.periodNo })
  }
}

// 监听账套ID变化
watch(() => props.accountSetId, async (newId) => {
  if (newId) {
    await loadPeriods()
    await loadCurrentPeriod()
  }
}, { immediate: true })

onMounted(async () => {
  if (props.accountSetId) {
    await loadPeriods()
    await loadCurrentPeriod()
  }
})
</script>

<style scoped>
.account-period-selector {
  display: flex;
  align-items: center;
  width: 100%;
  min-width: 120px;
  max-width: 160px;
}

.account-period-selector :deep(.ant-select) {
  width: 100%;
  font-size: 13px;
}

.account-period-selector :deep(.ant-select-selector) {
  background-color: var(--bg-muted) !important;
  border: 1px solid var(--border) !important;
  color: var(--text-1) !important;
  border-radius: 6px;
}

.account-period-selector :deep(.ant-select-selector:hover) {
  border-color: var(--border-strong) !important;
}

.account-period-selector :deep(.ant-select-focused .ant-select-selector) {
  border-color: var(--color-primary) !important;
  box-shadow: 0 0 0 2px var(--color-primary-border) !important;
}

.account-period-selector :deep(.ant-select-selection-item) {
  color: var(--text-1) !important;
}

.account-period-selector :deep(.ant-select-arrow) {
  color: var(--text-3) !important;
}
</style>
