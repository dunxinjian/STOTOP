<template>
  <div class="classification-results">
    <a-spin :spinning="loading">
      <a-empty v-if="!loading && results.length === 0" description="暂无分类结果" />
      <a-table
        v-else
        :columns="columns"
        :data-source="results"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'classificationLabel'">
            <a-tag :color="record.tagColor || 'default'">{{ record.classificationLabel }}</a-tag>
          </template>

          <template v-else-if="column.key === 'severityLevel'">
            <a-tag :color="severityColor(record.severityLevel)">{{ severityText(record.severityLevel) }}</a-tag>
          </template>

          <template v-else-if="column.key === 'matchedRowCount'">
            {{ record.matchedRowCount ?? 0 }}
          </template>

          <template v-else-if="column.key === 'ruleName'">
            {{ record.ruleName || '-' }}
          </template>

          <template v-else-if="column.key === 'processingStatus'">
            <a-tag :color="statusColor(record.processingStatus)">{{ statusText(record.processingStatus) }}</a-tag>
          </template>

          <template v-else-if="column.key === 'resultSummary'">
            <span v-if="record.resultSummary" class="result-summary">{{ record.resultSummary }}</span>
            <span v-else style="color: var(--text-3)">-</span>
          </template>

          <template v-else-if="column.key === 'createTime'">
            {{ formatDate(record.createTime) }}
          </template>
        </template>

        <template #expandedRowRender="{ record }">
          <div class="expanded-detail">
            <div v-if="record.context" class="detail-section">
              <div class="detail-label">上下文信息</div>
              <pre class="detail-json">{{ formatJson(record.context) }}</pre>
            </div>
            <div v-if="record.processingResult" class="detail-section">
              <div class="detail-label">处理结果详情</div>
              <pre class="detail-json">{{ formatJson(record.processingResult) }}</pre>
            </div>
            <a-empty v-if="!record.context && !record.processingResult" description="无详细信息" :image="false" />
          </div>
        </template>
      </a-table>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { get } from '@/api/request'

const props = defineProps<{
  batchId: number
}>()

interface ClassificationResult {
  id: number
  classificationLabel: string
  tagColor?: string
  severityLevel: string
  matchedRowCount: number
  ruleName?: string
  processingStatus: number
  resultSummary?: string
  context?: string
  processingResult?: string
  createTime: string
}

const loading = ref(false)
const results = ref<ClassificationResult[]>([])

const columns = [
  { title: '分类标签', key: 'classificationLabel', dataIndex: 'classificationLabel', width: 120 },
  { title: '严重级别', key: 'severityLevel', dataIndex: 'severityLevel', width: 100 },
  { title: '命中行数', key: 'matchedRowCount', dataIndex: 'matchedRowCount', width: 80, align: 'right' as const },
  { title: '触发规则', key: 'ruleName', dataIndex: 'ruleName', width: 130 },
  { title: '处理状态', key: 'processingStatus', dataIndex: 'processingStatus', width: 100 },
  { title: '处理结果', key: 'resultSummary', dataIndex: 'resultSummary', ellipsis: true },
  { title: '创建时间', key: 'createTime', dataIndex: 'createTime', width: 150 },
]

function severityColor(level: string): string {
  const map: Record<string, string> = {
    Info: 'blue',
    Warning: 'orange',
    Error: 'red',
    Critical: 'purple',
  }
  return map[level] || 'default'
}

function severityText(level: string): string {
  const map: Record<string, string> = {
    Info: '信息',
    Warning: '警告',
    Error: '错误',
    Critical: '严重',
  }
  return map[level] || level
}

function statusColor(status: number): string {
  const map: Record<number, string> = {
    0: 'default',
    1: 'processing',
    2: 'success',
    3: 'error',
  }
  return map[status] ?? 'default'
}

function statusText(status: number): string {
  const map: Record<number, string> = {
    0: '待处理',
    1: '处理中',
    2: '已处理',
    3: '处理失败',
  }
  return map[status] ?? '未知'
}

function formatDate(dt: string): string {
  if (!dt) return '-'
  return dt.replace('T', ' ').slice(0, 19)
}

function formatJson(val: string | object | null | undefined): string {
  if (!val) return ''
  try {
    const obj = typeof val === 'string' ? JSON.parse(val) : val
    return JSON.stringify(obj, null, 2)
  } catch {
    return String(val)
  }
}

async function loadResults() {
  if (!props.batchId) return
  loading.value = true
  try {
    const data = await get<ClassificationResult[]>(
      `/cardflow/dispatch-rules/results/batch/${props.batchId}`
    )
    results.value = Array.isArray(data) ? data : []
  } catch {
    results.value = []
  } finally {
    loading.value = false
  }
}

watch(() => props.batchId, (val) => {
  if (val) loadResults()
}, { immediate: true })
</script>

<style scoped lang="scss">
.classification-results {
  margin-top: 12px;
}

.expanded-detail {
  padding: 8px 12px;
}

.detail-section {
  margin-bottom: 12px;
  &:last-child { margin-bottom: 0; }
}

.detail-label {
  font-weight: 600;
  font-size: 13px;
  margin-bottom: 4px;
  color: var(--text-1);
}

.detail-json {
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 8px 12px;
  font-size: 12px;
  line-height: 1.6;
  max-height: 240px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 0;
}

.result-summary {
  color: var(--text-2);
  font-size: 12px;
}
</style>
