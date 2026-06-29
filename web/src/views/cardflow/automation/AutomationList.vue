<template>
  <div class="automation-list">
    <PageHeader title="自动化下载" description="管理自动下载任务，配置定时抓取流程">
      <template #actions>
        <a-button type="primary" @click="handleCreate"><PlusOutlined /> 新建任务</a-button>
      </template>
    </PageHeader>

    <div class="page-content">
      <a-table
        :columns="columns"
        :dataSource="taskList"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'targetUrl'">
            <a v-if="record.targetUrl" :href="record.targetUrl" target="_blank" class="ant-btn-link">
              {{ truncateUrl(record.targetUrl) }}
            </a>
            <span v-else class="text-secondary">—</span>
          </template>
          <template v-else-if="column.dataIndex === 'cronExpression'">
            <a-tag v-if="record.cronExpression" color="default">{{ record.cronExpression }}</a-tag>
            <span v-else class="text-secondary">未设置</span>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-switch
              v-model:checked="record.status"
              :checkedValue="1"
              :unCheckedValue="0"
              @change="(val: any) => handleToggleStatus(record, val)"
            />
          </template>
          <template v-else-if="column.dataIndex === 'updateTime'">
            <span>{{ record.updateTime || '—' }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)"><EditOutlined /> 编辑</a-button>
            <a-button type="link" size="small" style="color: var(--color-warning-text)" @click="handleTrigger(record)"><CaretRightOutlined /> 触发</a-button>
            <a-button type="link" size="small" @click="handleViewLogs(record)"><FileOutlined /> 日志</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record)"><DeleteOutlined /> 删除</a-button>
          </template>
        </template>
      </a-table>
    </div>

    <!-- 日志弹窗 -->
    <a-modal
      v-model:open="logDialogVisible"
      :title="`执行日志 - ${currentTask?.taskName || ''}`"
      width="800px"
      centered
      :destroyOnClose="true"
    >
      <a-table
        :columns="logColumns"
        :dataSource="logList"
        :loading="logLoading"
        rowKey="startTime"
        bordered
        :pagination="false"
        :scroll="{ y: 400 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : record.status === 2 ? 'error' : 'processing'">
              {{ record.status === 1 ? '成功' : record.status === 2 ? '失败' : '运行中' }}
            </a-tag>
          </template>
        </template>
      </a-table>
      <template #footer>
        <a-button @click="logDialogVisible = false">关闭</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined, EditOutlined, DeleteOutlined,
  CaretRightOutlined, FileOutlined,
} from '@ant-design/icons-vue'
import {
  getDownloadTasks,
  deleteDownloadTask,
  triggerDownloadTask,
  updateDownloadTask,
  getDownloadLogs,
} from '@/api/cardflow'

const router = useRouter()
const loading = ref(false)
const taskList = ref<any[]>([])

// 日志相关
const logDialogVisible = ref(false)
const logLoading = ref(false)
const logList = ref<any[]>([])
const currentTask = ref<any>(null)

const columns = [
  { title: '任务名称', dataIndex: 'taskName', key: 'taskName', ellipsis: true },
  { title: '目标网站URL', dataIndex: 'targetUrl', key: 'targetUrl', ellipsis: true },
  { title: 'Cron 表达式', dataIndex: 'cronExpression', key: 'cronExpression', width: 150, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '上次执行时间', dataIndex: 'updateTime', key: 'updateTime', width: 180, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 280, align: 'center' as const, fixed: 'right' as const },
]

const logColumns = [
  { title: '执行时间', dataIndex: 'startTime', key: 'startTime', width: 180 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '下载文件数', dataIndex: 'downloadFileCount', key: 'downloadFileCount', width: 110, align: 'center' as const },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
]

function truncateUrl(url: string, max = 40) {
  if (!url) return ''
  return url.length > max ? url.slice(0, max) + '...' : url
}

async function loadTasks() {
  loading.value = true
  try {
    const res = await getDownloadTasks()
    taskList.value = res.data ?? res ?? []
  } catch {
    message.error('加载任务列表失败')
  } finally {
    loading.value = false
  }
}

function handleCreate() {
  router.push({ name: 'FlowDesigner' })
}

function handleEdit(row: any) {
  router.push({ name: 'FlowDesigner', params: { id: row.id } })
}

async function handleToggleStatus(row: any, val: number) {
  try {
    await updateDownloadTask(row.id, { ...row, status: val })
    message.success(val === 1 ? '已启用' : '已禁用')
  } catch {
    row.status = val === 1 ? 0 : 1
    message.error('状态更新失败')
  }
}

async function handleTrigger(row: any) {
  Modal.confirm({
    title: '手动触发',
    content: `确认手动触发任务「${row.taskName}」？`,
    okText: '确认',
    cancelText: '取消',
    async onOk() {
      await triggerDownloadTask(row.id)
      message.success('任务已触发')
    },
  })
}

async function handleViewLogs(row: any) {
  currentTask.value = row
  logDialogVisible.value = true
  logLoading.value = true
  try {
    const res = await getDownloadLogs(row.id, { pageSize: 50 })
    logList.value = res.data ?? res ?? []
  } catch {
    message.error('加载日志失败')
  } finally {
    logLoading.value = false
  }
}

async function handleDelete(row: any) {
  Modal.confirm({
    title: '删除确认',
    content: `确认删除任务「${row.taskName}」？此操作不可恢复。`,
    okText: '删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      await deleteDownloadTask(row.id)
      message.success('删除成功')
      loadTasks()
    },
  })
}

onMounted(() => {
  loadTasks()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.automation-list {
  padding: $page-padding;

  .page-content {
    margin-top: $section-gap;
  }

  .text-secondary {
    color: $text-secondary;
    font-size: $font-size-sm;
  }
}
</style>
