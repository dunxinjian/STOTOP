<template>
  <div class="changelog-panel">
    <a-table
      :columns="columns"
      :data-source="logs"
      :loading="loading"
      :pagination="logs.length > 20 ? { pageSize: 20, showSizeChanger: true } : false"
      row-key="fId"
      bordered
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'fChangedTime'">
          {{ formatTime(record.fChangedTime) }}
        </template>
        <template v-if="column.dataIndex === 'change'">
          <span class="old-value">{{ record.fOldValue || '(空)' }}</span>
          <span style="margin: 0 6px; color: var(--text-3);">→</span>
          <span class="new-value">{{ record.fNewValue || '(空)' }}</span>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  getQuotationChangeLogs,
  type QuotationChangeLogDto,
} from '@/api/express'

const props = defineProps<{
  quotationId: number
}>()

const loading = ref(false)
const logs = ref<QuotationChangeLogDto[]>([])

const columns = [
  { title: '变更时间', dataIndex: 'fChangedTime', width: 170 },
  { title: '变更人', dataIndex: 'fChangedBy', width: 100 },
  { title: '变更字段', dataIndex: 'fFieldName', width: 140, ellipsis: true },
  { title: '变更内容', dataIndex: 'change' },
]

function formatTime(time: string) {
  if (!time) return ''
  return time.replace('T', ' ').substring(0, 19)
}

async function loadData() {
  loading.value = true
  try {
    logs.value = await getQuotationChangeLogs(props.quotationId)
  } catch {
    message.error('加载变更日志失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (props.quotationId) {
    loadData()
  }
})
</script>

<style scoped lang="scss">
.changelog-panel {
  padding: 4px 0;
}

.old-value {
  color: var(--color-danger);
  text-decoration: line-through;
}

.new-value {
  color: var(--color-success);
  font-weight: 500;
}
</style>
