<template>
  <div class="issue-page">
    <PageHeader title="异常处理">
      <template #actions>
        <a-button @click="loadIssues">
          <ReloadOutlined />
        </a-button>
      </template>
    </PageHeader>

    <div class="filters">
      <a-input
        v-model:value="query.keyword"
        allow-clear
        placeholder="搜索异常码、消息、原始值"
        class="filter-keyword"
        @pressEnter="handleSearch"
      >
        <template #prefix><SearchOutlined /></template>
      </a-input>
      <a-input-number v-model:value="query.batchId" placeholder="批次ID" :min="1" class="filter-batch" />
      <a-select v-model:value="query.resolutionStatus" allow-clear placeholder="处理状态" class="filter-select">
        <a-select-option value="Pending">待处理</a-select-option>
        <a-select-option value="Processing">处理中</a-select-option>
        <a-select-option value="Resolved">已处理</a-select-option>
        <a-select-option value="Ignored">已忽略</a-select-option>
        <a-select-option value="Failed">失败</a-select-option>
      </a-select>
      <a-select v-model:value="query.severityLevel" allow-clear placeholder="严重级别" class="filter-select">
        <a-select-option value="Error">错误</a-select-option>
        <a-select-option value="Warning">警告</a-select-option>
        <a-select-option value="Info">信息</a-select-option>
      </a-select>
      <a-button type="primary" @click="handleSearch">
        <SearchOutlined />
      </a-button>
      <a-button @click="handleReset">
        <UndoOutlined />
      </a-button>
    </div>

    <div class="summary-strip">
      <button
        v-for="item in summaryItems"
        :key="item.key"
        class="summary-item"
        :class="{ active: query.resolutionStatus === item.key }"
        type="button"
        @click="setStatusFilter(item.key)"
      >
        <span class="summary-label">{{ item.label }}</span>
        <strong>{{ item.value }}</strong>
      </button>
    </div>

    <a-spin :spinning="loading">
      <div v-if="issues.length" class="issue-list">
        <a-card
          v-for="issue in issues"
          :key="issue.id"
          class="issue-card"
          :bordered="false"
        >
          <div class="issue-card-main">
            <div class="issue-head">
              <div class="issue-title">
                <a-tag :color="severityColor(issue.severityLevel)">{{ severityText(issue.severityLevel) }}</a-tag>
                <a-tag :color="statusColor(issue.resolutionStatus)">{{ statusText(issue.resolutionStatus) }}</a-tag>
                <span class="issue-code">{{ issue.errorType }}</span>
                <span v-if="issue.issueName" class="issue-name">{{ issue.issueName }}</span>
              </div>
              <div class="issue-meta">
                批次 {{ issue.batchId }}
                <span v-if="issue.stagingId"> · 暂存 {{ issue.stagingId }}</span>
                <span> · {{ formatDate(issue.createdTime) }}</span>
              </div>
            </div>

            <div class="issue-message">{{ issue.errorMessage || issue.suggestedFix || '无异常描述' }}</div>

            <div class="issue-fields">
              <span v-if="issue.errorField">字段：{{ issue.errorField }}</span>
              <span v-if="issue.originalValue">原始值：{{ issue.originalValue }}</span>
              <span v-if="issue.resolveMode">处理方式：{{ resolveModeText(issue.resolveMode) }}</span>
              <span v-if="issue.retryStatus && issue.retryStatus !== 'None'">重跑：{{ issue.retryStatus }}</span>
            </div>
          </div>

          <div class="issue-actions">
            <a-tooltip title="打开处理页面">
              <a-button :disabled="!issue.detailRoute" @click="openDetail(issue)">
                <ExportOutlined />
              </a-button>
            </a-tooltip>
            <a-tooltip title="标记已处理">
              <a-button
                type="primary"
                :disabled="isClosed(issue)"
                :loading="actingId === issue.id"
                @click="resolveIssue(issue)"
              >
                <CheckOutlined />
              </a-button>
            </a-tooltip>
            <a-tooltip title="忽略">
              <a-button
                :disabled="isClosed(issue)"
                :loading="actingId === issue.id"
                @click="ignoreIssue(issue)"
              >
                <StopOutlined />
              </a-button>
            </a-tooltip>
            <a-tooltip title="请求重跑">
              <a-button
                :disabled="issue.resolutionStatus === 'Ignored'"
                :loading="actingId === issue.id"
                @click="retryIssue(issue)"
              >
                <SyncOutlined />
              </a-button>
            </a-tooltip>
          </div>
        </a-card>
      </div>
      <a-empty v-else description="暂无异常" class="empty-state" />
    </a-spin>

    <div class="pager">
      <a-pagination
        v-model:current="query.page"
        v-model:page-size="query.pageSize"
        :total="total"
        :show-size-changer="true"
        :show-total="(count: number) => `共 ${count} 条`"
        @change="loadIssues"
        @showSizeChange="loadIssues"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  CheckOutlined,
  ExportOutlined,
  ReloadOutlined,
  SearchOutlined,
  StopOutlined,
  SyncOutlined,
  UndoOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getProcessingIssues,
  ignoreProcessingIssue,
  resolveProcessingIssue,
  retryProcessingIssue,
} from '@/api/cardflow'
import type { ProcessingIssueDto, ProcessingIssueQueryRequest } from '@/types/cardflow'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const actingId = ref<number | null>(null)
const issues = ref<ProcessingIssueDto[]>([])
const total = ref(0)

const query = reactive<ProcessingIssueQueryRequest>({
  batchId: Number(route.query.batchId) || undefined,
  errorType: typeof route.query.issueType === 'string' ? route.query.issueType : undefined,
  resolutionStatus: typeof route.query.status === 'string' ? route.query.status : undefined,
  severityLevel: undefined,
  keyword: undefined,
  page: 1,
  pageSize: 20,
})

const counts = computed(() => {
  const base = { Pending: 0, Processing: 0, Resolved: 0, Ignored: 0, Failed: 0 }
  for (const issue of issues.value) {
    const key = issue.resolutionStatus as keyof typeof base
    if (key in base) base[key] += 1
  }
  return base
})

const summaryItems = computed(() => [
  { key: 'Pending', label: '待处理', value: counts.value.Pending },
  { key: 'Processing', label: '处理中', value: counts.value.Processing },
  { key: 'Resolved', label: '已处理', value: counts.value.Resolved },
  { key: 'Ignored', label: '已忽略', value: counts.value.Ignored },
])

watch(
  () => route.query,
  () => {
    query.batchId = Number(route.query.batchId) || undefined
    query.errorType = typeof route.query.issueType === 'string' ? route.query.issueType : undefined
    query.resolutionStatus = typeof route.query.status === 'string' ? route.query.status : query.resolutionStatus
    query.page = 1
    loadIssues()
  }
)

async function loadIssues() {
  loading.value = true
  try {
    const result = await getProcessingIssues(query)
    issues.value = result.items || []
    total.value = result.total || 0
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  query.page = 1
  loadIssues()
}

function handleReset() {
  query.batchId = undefined
  query.errorType = undefined
  query.resolutionStatus = undefined
  query.severityLevel = undefined
  query.keyword = undefined
  query.page = 1
  loadIssues()
}

function setStatusFilter(status: string) {
  query.resolutionStatus = query.resolutionStatus === status ? undefined : status
  query.page = 1
  loadIssues()
}

function openDetail(issue: ProcessingIssueDto) {
  if (!issue.detailRoute) return
  router.push(issue.detailRoute)
}

function resolveIssue(issue: ProcessingIssueDto) {
  Modal.confirm({
    title: '标记为已处理',
    content: issue.errorMessage || issue.errorType,
    okText: '确认',
    cancelText: '取消',
    async onOk() {
      actingId.value = issue.id
      try {
        await resolveProcessingIssue(issue.id, { message: '用户确认已处理' })
        message.success('已处理')
        await loadIssues()
      } finally {
        actingId.value = null
      }
    },
  })
}

function ignoreIssue(issue: ProcessingIssueDto) {
  Modal.confirm({
    title: '忽略异常',
    content: issue.errorMessage || issue.errorType,
    okText: '忽略',
    cancelText: '取消',
    async onOk() {
      actingId.value = issue.id
      try {
        await ignoreProcessingIssue(issue.id, { message: '用户忽略' })
        message.success('已忽略')
        await loadIssues()
      } finally {
        actingId.value = null
      }
    },
  })
}

function retryIssue(issue: ProcessingIssueDto) {
  Modal.confirm({
    title: '请求重跑',
    content: `批次 ${issue.batchId} · ${issue.errorType}`,
    okText: '请求重跑',
    cancelText: '取消',
    async onOk() {
      actingId.value = issue.id
      try {
        await retryProcessingIssue(issue.id, {
          retryAction: issue.afterResolvedAction || 'RetryPlugin',
          message: '用户请求重跑',
        })
        message.success('已记录重跑请求')
        await loadIssues()
      } finally {
        actingId.value = null
      }
    },
  })
}

function isClosed(issue: ProcessingIssueDto) {
  return issue.resolutionStatus === 'Resolved' || issue.resolutionStatus === 'Ignored'
}

function severityColor(value?: string | null) {
  if (value === 'Error' || value === 'Critical') return 'red'
  if (value === 'Warning') return 'orange'
  return 'blue'
}

function severityText(value?: string | null) {
  if (value === 'Error') return '错误'
  if (value === 'Critical') return '严重'
  if (value === 'Warning') return '警告'
  return value || '信息'
}

function statusColor(value?: string | null) {
  if (value === 'Resolved') return 'green'
  if (value === 'Ignored') return 'default'
  if (value === 'Processing') return 'blue'
  if (value === 'Failed') return 'red'
  return 'gold'
}

function statusText(value?: string | null) {
  const map: Record<string, string> = {
    Pending: '待处理',
    Processing: '处理中',
    Resolved: '已处理',
    Ignored: '已忽略',
    Failed: '失败',
  }
  return value ? map[value] || value : '待处理'
}

function resolveModeText(value?: string | null) {
  const map: Record<string, string> = {
    None: '仅记录',
    WorkItem: '工作项',
    InlineCard: '卡片内处理',
    GuideToPage: '功能页处理',
    DedicatedFlow: '专用流程',
  }
  return value ? map[value] || value : '未配置'
}

function formatDate(value?: string | null) {
  if (!value) return ''
  return value.replace('T', ' ').slice(0, 16)
}

onMounted(loadIssues)
</script>

<style scoped>
.issue-page {
  padding: 16px;
  background: var(--bg-page);
  min-height: 100%;
}

.filters {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
  margin: 12px 0;
}

.filter-keyword {
  width: 300px;
}

.filter-batch {
  width: 140px;
}

.filter-select {
  width: 140px;
}

.summary-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 12px;
}

.summary-item {
  height: 64px;
  border: 1px solid var(--border);
  background: var(--bg-card);
  border-radius: 6px;
  text-align: left;
  padding: 10px 12px;
  cursor: pointer;
}

.summary-item.active {
  border-color: var(--border-strong);
  background: color-mix(in srgb, var(--color-info) 8%, transparent);
}

.summary-label {
  display: block;
  color: var(--text-2);
  font-size: 12px;
}

.summary-item strong {
  display: block;
  margin-top: 4px;
  font-size: 22px;
  line-height: 1;
}

.issue-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.issue-card {
  border-radius: 6px;
}

.issue-card :deep(.ant-card-body) {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
}

.issue-card-main {
  min-width: 0;
  flex: 1;
}

.issue-head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
}

.issue-title {
  display: flex;
  gap: 6px;
  align-items: center;
  flex-wrap: wrap;
}

.issue-code {
  font-weight: 600;
  color: var(--text-1);
}

.issue-name {
  color: var(--text-2);
}

.issue-meta {
  color: var(--text-2);
  font-size: 12px;
  white-space: nowrap;
}

.issue-message {
  margin-top: 8px;
  color: var(--text-1);
  line-height: 1.5;
}

.issue-fields {
  display: flex;
  gap: 14px;
  flex-wrap: wrap;
  margin-top: 8px;
  color: var(--text-2);
  font-size: 12px;
}

.issue-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.empty-state {
  padding: 64px 0;
  background: var(--bg-card);
  border-radius: 6px;
}

.pager {
  display: flex;
  justify-content: flex-end;
  margin-top: 12px;
}

@media (max-width: 760px) {
  .summary-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .filter-keyword,
  .filter-batch,
  .filter-select {
    width: 100%;
  }

  .issue-card :deep(.ant-card-body),
  .issue-head {
    flex-direction: column;
  }

  .issue-meta {
    white-space: normal;
  }
}
</style>
