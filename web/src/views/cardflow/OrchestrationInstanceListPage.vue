<script setup lang="ts">
/**
 * OrchestrationInstanceListPage.vue — 编排实例监控
 *
 * 实例运行状态列表 + 暂停 / 恢复 / 取消
 */
import { ref, reactive, computed, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  SearchOutlined,
  EyeOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  StopOutlined,
  ExclamationCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { isRedirectingToLogin } from '@/api/request'
import {
  getOrchestrationInstances,
  getOrchestrationTemplates,
  pauseOrchestrationInstance,
  resumeOrchestrationInstance,
  cancelOrchestrationInstance,
} from '@/api/orchestration'
import type {
  OrchestrationInstance,
  OrchestrationInstanceStatus,
  OrchestrationTemplate,
} from '@/types/orchestration'

const router = useRouter()

// ==================== 状态 ====================

interface StatusMeta { text: string; color: string }
const STATUS_META: Record<OrchestrationInstanceStatus, StatusMeta> = {
  running: { text: '运行中', color: 'var(--color-info)' },
  completed: { text: '已完成', color: 'var(--color-success)' },
  terminated: { text: '已终止', color: 'var(--text-3)' },
  failed: { text: '失败', color: 'var(--color-danger)' },
  cancelled: { text: '已取消', color: 'var(--text-3)' },
  paused: { text: '已暂停', color: 'var(--color-warning)' },
}
const STATUS_OPTIONS = (Object.keys(STATUS_META) as OrchestrationInstanceStatus[]).map(k => ({
  label: STATUS_META[k].text,
  value: k,
}))

const loading = ref(false)
const dataSource = ref<OrchestrationInstance[]>([])
const templates = ref<OrchestrationTemplate[]>([])
const templateMap = computed(() => {
  const m = new Map<number, OrchestrationTemplate>()
  templates.value.forEach(t => m.set(t.id, t))
  return m
})

const pagination = reactive({ current: 1, pageSize: 20, total: 0 })
const searchParams = reactive({
  templateId: undefined as number | undefined,
  status: undefined as OrchestrationInstanceStatus | undefined,
})

// ==================== 表格列 ====================

const columns: TableColumnsType = [
  { title: '实例 ID', dataIndex: 'id', key: 'id', width: 100 },
  { title: '编排模板', dataIndex: 'templateName', key: 'templateName', width: 220, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, align: 'center' },
  { title: '触发次数', dataIndex: 'triggerCount', key: 'triggerCount', width: 100, align: 'right' },
  { title: '发起人', dataIndex: 'initiatorId', key: 'initiatorId', width: 120 },
  { title: '发起时间', dataIndex: 'initiatedTime', key: 'initiatedTime', width: 170 },
  { title: '完成时间', dataIndex: 'completedTime', key: 'completedTime', width: 170 },
  { title: '操作', key: 'action', width: 260, fixed: 'right' },
]

// ==================== 加载 ====================

async function loadTemplates() {
  try {
    const res = await getOrchestrationTemplates({ pageSize: 200 })
    templates.value = res?.items || []
  } catch {
    /* 静默 */
  }
}

async function loadData() {
  loading.value = true
  try {
    const res = await getOrchestrationInstances({
      templateId: searchParams.templateId,
      status: searchParams.status,
      page: pagination.current,
      pageSize: pagination.pageSize,
    })
    dataSource.value = res?.items || []
    pagination.total = res?.total ?? dataSource.value.length
  } catch {
    if (!isRedirectingToLogin) message.error('加载编排实例列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  loadData()
}

function handleReset() {
  searchParams.templateId = undefined
  searchParams.status = undefined
  pagination.current = 1
  loadData()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  loadData()
}

// ==================== 行操作 ====================

function gotoDetail(record: OrchestrationInstance) {
  router.push({ path: `/cardflow/orchestration-instances/${record.id}` })
}

async function handlePause(record: OrchestrationInstance) {
  try {
    await pauseOrchestrationInstance(record.id)
    message.success('已暂停')
    loadData()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '暂停失败')
  }
}

async function handleResume(record: OrchestrationInstance) {
  try {
    await resumeOrchestrationInstance(record.id)
    message.success('已恢复')
    loadData()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '恢复失败')
  }
}

function handleCancel(record: OrchestrationInstance) {
  Modal.confirm({
    title: `取消实例 #${record.id}？`,
    icon: h(ExclamationCircleOutlined),
    content: '取消后实例不再执行任何派发。',
    okText: '取消实例',
    okType: 'danger',
    async onOk() {
      try {
        await cancelOrchestrationInstance(record.id)
        message.success('已取消')
        loadData()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '取消失败')
      }
    },
  })
}

function formatTime(val?: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

function statusOf(record: OrchestrationInstance): OrchestrationInstanceStatus {
  return record.status || 'running'
}

function templateName(record: OrchestrationInstance) {
  return record.templateName || templateMap.value.get(record.templateId)?.name || `#${record.templateId}`
}

onMounted(async () => {
  await loadTemplates()
  await loadData()
})
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #actions>
        <a-space :size="8">
          <a-button @click="loadData">
            <template #icon><ReloadOutlined /></template>
            刷新
          </a-button>
        </a-space>
      </template>

      <template #toolbar>
        <div class="filter-bar">
          <a-select
            v-model:value="searchParams.templateId"
            placeholder="编排模板"
            allow-clear
            show-search
            :filter-option="(input: string, opt: any) => String(opt?.label ?? '').toLowerCase().includes(input.toLowerCase())"
            :options="templates.map(t => ({ label: t.name, value: t.id }))"
            style="width: 220px"
          />
          <a-select
            v-model:value="searchParams.status"
            placeholder="状态"
            allow-clear
            :options="STATUS_OPTIONS"
            style="width: 160px"
          />
          <a-button type="primary" @click="handleSearch">
            <template #icon><SearchOutlined /></template>
            查询
          </a-button>
          <a-button @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-table
      class="orch-table"
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      size="middle"
      :pagination="{
        current: pagination.current,
        pageSize: pagination.pageSize,
        total: pagination.total,
        showSizeChanger: true,
        showQuickJumper: true,
        pageSizeOptions: ['10', '20', '50', '100'],
        showTotal: (t: number) => `共 ${t} 条`,
      }"
      :scroll="{ x: 1280 }"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record: r }">
        <template v-if="column.key === 'id'">
          <a class="link-name" @click="gotoDetail(r as OrchestrationInstance)">#{{ r.id }}</a>
        </template>

        <template v-else-if="column.key === 'templateName'">
          {{ templateName(r as OrchestrationInstance) }}
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag class="status-tag" :color="STATUS_META[statusOf(r as OrchestrationInstance)]?.color">
            {{ STATUS_META[statusOf(r as OrchestrationInstance)]?.text || r.status }}
          </a-tag>
        </template>

        <template v-else-if="column.key === 'initiatedTime'">
          {{ formatTime(r.initiatedTime) }}
        </template>

        <template v-else-if="column.key === 'completedTime'">
          {{ formatTime(r.completedTime) }}
        </template>

        <template v-else-if="column.key === 'action'">
          <div class="row-actions">
            <a-button type="link" size="small" @click="gotoDetail(r as OrchestrationInstance)">
              <EyeOutlined /> 详情
            </a-button>
            <a-button
              v-if="statusOf(r as OrchestrationInstance) === 'running'"
              type="link"
              size="small"
              @click="handlePause(r as OrchestrationInstance)"
            >
              <PauseCircleOutlined /> 暂停
            </a-button>
            <a-button
              v-if="statusOf(r as OrchestrationInstance) === 'paused'"
              type="link"
              size="small"
              @click="handleResume(r as OrchestrationInstance)"
            >
              <PlayCircleOutlined /> 恢复
            </a-button>
            <a-button
              type="link"
              size="small"
              danger
              :disabled="['completed','cancelled','terminated','failed'].includes(statusOf(r as OrchestrationInstance))"
              @click="handleCancel(r as OrchestrationInstance)"
            >
              <StopOutlined /> 取消
            </a-button>
          </div>
        </template>
      </template>
    </a-table>
  </div>
</template>

<style scoped lang="scss">
.page-container { padding: 0; }

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}

.orch-table {
  :deep(.row-actions) {
    opacity: 0.7;
    transition: opacity 0.15s ease;
  }
  :deep(tr:hover .row-actions) {
    opacity: 1;
  }
}

.row-actions {
  display: inline-flex;
  align-items: center;
  flex-wrap: nowrap;

  :deep(.ant-btn-link) {
    padding-inline: 6px;
  }
}

.link-name {
  color: var(--text-1);
  cursor: pointer;
  font-weight: 500;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}

.status-tag {
  color: var(--text-on-accent);
  border: none;
  font-weight: 500;
}
</style>
