<template>
  <div class="file-manager">
    <PageHeader title="文件管理" description="管理上传到服务器的原始数据文件" />

    <!-- 存储统计卡片区 -->
    <a-row :gutter="16" class="stats-row">
      <a-col :span="6">
        <a-card hoverable class="stat-card">
          <div class="stat-item">
            <div class="stat-label">文件总数</div>
            <div class="stat-value">{{ storageStats.totalFiles ?? 0 }}</div>
          </div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card hoverable class="stat-card">
          <div class="stat-item">
            <div class="stat-label">总大小</div>
            <div class="stat-value">{{ formatSize(storageStats.totalSize ?? 0) }}</div>
          </div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card hoverable class="stat-card">
          <div class="stat-item">
            <div class="stat-label">存储使用率</div>
            <a-progress
              :percent="storagePercent"
              :strokeColor="storagePercent > 80 ? 'var(--color-danger)' : storagePercent > 60 ? 'var(--color-warning)' : 'var(--color-info)'"
              :strokeWidth="14"
              style="margin-top: 8px"
            />
          </div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card hoverable class="stat-card chart-card">
          <v-chart :option="monthlyChartOption" autoresize style="height: 100px" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 筛选栏 -->
    <a-card class="filter-card">
      <a-form layout="inline" :model="filters" class="filter-form">
        <a-form-item label="日期范围">
          <a-range-picker
            v-model:value="filters.dateRange"
            valueFormat="YYYY-MM-DD"
            style="width: 260px"
          />
        </a-form-item>
        <a-form-item label="处理状态">
          <a-select v-model:value="filters.status" placeholder="全部" allowClear style="width: 130px">
            <a-select-option value="pending">待处理</a-select-option>
            <a-select-option value="completed">已完成</a-select-option>
            <a-select-option value="failed">失败</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="handleSearch"><SearchOutlined /> 搜索</a-button>
          <a-button style="margin-left: 8px" @click="handleReset"><UndoOutlined /> 重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 操作工具栏 -->
    <div class="toolbar">
      <div class="toolbar__left">
        <a-button
          danger
          :disabled="!selectedRowKeys.length"
          @click="handleBatchDelete"
        >
          <DeleteOutlined /> 批量删除 ({{ selectedRowKeys.length }})
        </a-button>
      </div>
      <div class="toolbar__right">
        <a-button @click="policyDialogVisible = true"><SettingOutlined /> 清理策略</a-button>
      </div>
    </div>

    <!-- 文件表格 -->
    <a-card class="table-card">
      <a-table
        :columns="fileColumns"
        :dataSource="fileList"
        :loading="loading"
        :rowSelection="{ selectedRowKeys, onChange: onSelectChange }"
        rowKey="id"
        bordered
        :pagination="false"
        @change="handleSortChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'fileSize'">{{ formatSize(record.fileSize) }}</template>
          <template v-else-if="column.dataIndex === 'uploadTime'">{{ formatDate(record.uploadTime) }}</template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-tag :color="statusTagColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'fileExists'">
            <a-tag :color="record.fileExists ? 'success' : 'default'">
              {{ record.fileExists ? '存在' : '已清理' }}
            </a-tag>
          </template>
        </template>
      </a-table>

      <div class="pagination-wrapper">
        <a-pagination
          v-model:current="pagination.page"
          v-model:pageSize="pagination.pageSize"
          :total="pagination.total"
          :pageSizeOptions="['20', '50', '100']"
          showSizeChanger
          :showTotal="(total: number) => `共 ${total} 条`"
          @change="fetchFiles"
          @showSizeChange="fetchFiles"
        />
      </div>
    </a-card>

    <!-- 清理策略弹窗 -->
    <a-modal
      v-model:open="policyDialogVisible"
      title="清理策略管理"
      width="720px"
      :destroyOnClose="true"
    >
      <div class="policy-toolbar">
        <a-button type="primary" size="small" @click="openPolicyForm()"><PlusOutlined /> 新增策略</a-button>
        <a-button size="small" style="color: var(--color-warning-text)" @click="handlePreviewCleanup">手动清理</a-button>
      </div>

      <a-table
        :columns="policyColumns"
        :dataSource="policyList"
        :loading="policyLoading"
        rowKey="id"
        bordered
        size="small"
        :pagination="false"
        style="margin-top: 12px"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'enabled'">
            <a-switch v-model:checked="record.enabled" size="small" @change="togglePolicy(record)" />
          </template>
          <template v-else-if="column.dataIndex === 'lastExecuteTime'">
            {{ record.lastExecuteTime ? formatDate(record.lastExecuteTime) : '-' }}
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="openPolicyForm(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDeletePolicy(record)">删除</a-button>
          </template>
        </template>
      </a-table>

      <!-- 策略编辑表单 -->
      <a-modal
        v-model:open="policyFormVisible"
        :title="editingPolicy.id ? '编辑策略' : '新增策略'"
        width="480px"
      >
        <a-form :model="editingPolicy" :rules="policyRules" ref="policyFormRef" :labelCol="{ style: { width: '100px' } }">
          <a-form-item label="策略名称" name="name">
            <a-input v-model:value="editingPolicy.name" placeholder="请输入策略名称" />
          </a-form-item>
          <a-form-item label="保留天数" name="retentionDays">
            <a-input-number v-model:value="editingPolicy.retentionDays" :min="1" :max="3650" style="width: 100%" />
          </a-form-item>
          <a-form-item label="Cron 表达式" name="cronExpression">
            <a-input v-model:value="editingPolicy.cronExpression" placeholder="如: 0 2 * * *">
              <template #addonAfter>
                <a-dropdown :trigger="['click']">
                  <a-button size="small" type="link">快捷</a-button>
                  <template #overlay>
                    <a-menu @click="({ key }: any) => (editingPolicy.cronExpression = key)">
                      <a-menu-item key="0 2 * * *">每天凌晨2点</a-menu-item>
                      <a-menu-item key="0 3 * * 0">每周日凌晨3点</a-menu-item>
                      <a-menu-item key="0 1 1 * *">每月1号凌晨1点</a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </template>
            </a-input>
          </a-form-item>
          <a-form-item label="启用">
            <a-switch v-model:checked="editingPolicy.enabled" />
          </a-form-item>
        </a-form>
        <template #footer>
          <a-button @click="policyFormVisible = false">取消</a-button>
          <a-button type="primary" @click="submitPolicy" :loading="policySaving">保存</a-button>
        </template>
      </a-modal>

      <template #footer>
        <a-button @click="policyDialogVisible = false">关闭</a-button>
      </template>
    </a-modal>

    <!-- 清理预览弹窗 -->
    <a-modal v-model:open="cleanupPreviewVisible" title="清理预览" width="420px">
      <a-spin :spinning="cleanupPreviewing">
        <div class="cleanup-preview">
          <p>即将清理的文件：<strong>{{ cleanupPreview.fileCount ?? 0 }}</strong> 个</p>
          <p>释放空间：<strong>{{ formatSize(cleanupPreview.totalSize ?? 0) }}</strong></p>
        </div>
      </a-spin>
      <template #footer>
        <a-button @click="cleanupPreviewVisible = false">取消</a-button>
        <a-button type="primary" danger @click="handleExecuteCleanup" :loading="cleanupExecuting">确认清理</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import {
  SearchOutlined, UndoOutlined, DeleteOutlined, SettingOutlined, PlusOutlined,
} from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, GridComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import {
  getUploadedFiles,
  deleteFiles,
  getStorageStats,
  getCleanupPolicies,
  saveCleanupPolicy,
  executeCleanup,
  previewCleanup,
} from '@/api/cardflow'

use([BarChart, TitleComponent, TooltipComponent, GridComponent, CanvasRenderer])

// ==================== 文件列表 ====================
const loading = ref(false)
const fileList = ref<any[]>([])
const selectedRowKeys = ref<(string | number)[]>([])

const fileColumns = [
  { title: '文件名', dataIndex: 'fileName', key: 'fileName', ellipsis: true, sorter: true },
  { title: '文件大小', dataIndex: 'fileSize', key: 'fileSize', width: 120, sorter: true },
  { title: '上传时间', dataIndex: 'uploadTime', key: 'uploadTime', width: 170, sorter: true },
  { title: '关联批次号', dataIndex: 'batchNo', key: 'batchNo', width: 140, ellipsis: true },
  { title: '处理状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '物理文件', dataIndex: 'fileExists', key: 'fileExists', width: 100, align: 'center' as const },
]

const policyColumns = [
  { title: '策略名称', dataIndex: 'name', key: 'name' },
  { title: '保留天数', dataIndex: 'retentionDays', key: 'retentionDays', width: 90, align: 'center' as const },
  { title: 'Cron 表达式', dataIndex: 'cronExpression', key: 'cronExpression', width: 140 },
  { title: '状态', dataIndex: 'enabled', key: 'enabled', width: 80, align: 'center' as const },
  { title: '上次执行时间', dataIndex: 'lastExecuteTime', key: 'lastExecuteTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const },
]

const filters = reactive({
  dateRange: undefined as [string, string] | undefined,
  status: undefined as string | undefined,
})

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0,
})

const sortState = reactive({
  sortBy: '',
  sortDesc: false,
})

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys
}

async function fetchFiles() {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize,
    }
    if (filters.status) params.status = filters.status
    if (filters.dateRange?.length === 2) {
      params.startDate = filters.dateRange[0]
      params.endDate = filters.dateRange[1]
    }
    if (sortState.sortBy) {
      params.sortBy = sortState.sortBy
      params.sortDesc = sortState.sortDesc
    }
    const res: any = await getUploadedFiles(params)
    fileList.value = res.data?.items ?? res.items ?? []
    pagination.total = res.data?.total ?? res.total ?? 0
  } catch {
    message.error('获取文件列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.page = 1
  fetchFiles()
}

function handleReset() {
  filters.dateRange = undefined
  filters.status = undefined
  sortState.sortBy = ''
  sortState.sortDesc = false
  pagination.page = 1
  fetchFiles()
}

function handleSortChange(_pagination: any, _filters: any, sorter: any) {
  sortState.sortBy = sorter.order ? sorter.field : ''
  sortState.sortDesc = sorter.order === 'descend'
  fetchFiles()
}

async function handleBatchDelete() {
  if (!selectedRowKeys.value.length) return
  Modal.confirm({
    title: '批量删除确认',
    content: `确认删除选中的 ${selectedRowKeys.value.length} 个文件？此操作不可恢复。`,
    okText: '删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      await deleteFiles(selectedRowKeys.value as number[])
      message.success('删除成功')
      fetchFiles()
      fetchStorageStats()
    },
  })
}

// ==================== 存储统计 ====================
const storageStats = ref<any>({})

const storagePercent = computed(() => {
  const { usedSpace, totalSpace } = storageStats.value
  if (!totalSpace) return 0
  return Math.round((usedSpace / totalSpace) * 100)
})

const monthlyChartOption = computed(() => {
  const monthly = storageStats.value.monthlyStats ?? []
  return {
    tooltip: { trigger: 'axis' },
    grid: { top: 20, right: 10, bottom: 20, left: 50 },
    xAxis: {
      type: 'category',
      data: monthly.map((m: any) => m.month),
      axisLabel: { fontSize: 10 },
    },
    yAxis: {
      type: 'value',
      axisLabel: {
        fontSize: 10,
        formatter: (v: number) => formatSize(v),
      },
    },
    series: [
      {
        type: 'bar',
        data: monthly.map((m: any) => m.size),
        itemStyle: { color: '#3A6FB0', borderRadius: [3, 3, 0, 0] },
        barMaxWidth: 28,
      },
    ],
  }
})

async function fetchStorageStats() {
  try {
    const res: any = await getStorageStats()
    storageStats.value = res.data ?? res ?? {}
  } catch {
    // silent
  }
}

// ==================== 清理策略 ====================
const policyDialogVisible = ref(false)
const policyFormVisible = ref(false)
const policyLoading = ref(false)
const policySaving = ref(false)
const policyList = ref<any[]>([])
const policyFormRef = ref<FormInstance>()

const defaultPolicy = () => ({
  id: null as number | null,
  name: '',
  retentionDays: 30,
  cronExpression: '0 2 * * *',
  enabled: true,
})

const editingPolicy = reactive(defaultPolicy())

const policyRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入策略名称', trigger: 'blur' }],
  retentionDays: [{ required: true, message: '请输入保留天数', trigger: 'blur' }],
  cronExpression: [{ required: true, message: '请输入Cron表达式', trigger: 'blur' }],
}

async function fetchPolicies() {
  policyLoading.value = true
  try {
    const res: any = await getCleanupPolicies()
    policyList.value = res.data ?? res ?? []
  } catch {
    message.error('获取清理策略失败')
  } finally {
    policyLoading.value = false
  }
}

function openPolicyForm(row?: any) {
  if (row) {
    Object.assign(editingPolicy, { ...row })
  } else {
    Object.assign(editingPolicy, defaultPolicy())
  }
  policyFormVisible.value = true
}

async function submitPolicy() {
  if (!policyFormRef.value) return
  await policyFormRef.value.validate()
  policySaving.value = true
  try {
    await saveCleanupPolicy({ ...editingPolicy })
    message.success('策略保存成功')
    policyFormVisible.value = false
    fetchPolicies()
  } catch {
    message.error('策略保存失败')
  } finally {
    policySaving.value = false
  }
}

async function togglePolicy(row: any) {
  try {
    await saveCleanupPolicy({ ...row })
  } catch {
    row.enabled = !row.enabled
    message.error('更新策略状态失败')
  }
}

async function handleDeletePolicy(row: any) {
  Modal.confirm({
    title: '删除确认',
    content: `确认删除策略「${row.name}」？`,
    okType: 'danger',
    async onOk() {
      await saveCleanupPolicy({ ...row, deleted: true })
      message.success('删除成功')
      fetchPolicies()
    },
  })
}

// ==================== 清理预览与执行 ====================
const cleanupPreviewVisible = ref(false)
const cleanupPreviewing = ref(false)
const cleanupExecuting = ref(false)
const cleanupPreview = ref<any>({})

async function handlePreviewCleanup() {
  cleanupPreviewVisible.value = true
  cleanupPreviewing.value = true
  try {
    const res: any = await previewCleanup()
    cleanupPreview.value = res.data ?? res ?? {}
  } catch {
    message.error('获取清理预览失败')
  } finally {
    cleanupPreviewing.value = false
  }
}

async function handleExecuteCleanup() {
  Modal.confirm({
    title: '清理确认',
    content: '确认执行清理？清理后文件将不可恢复。',
    okText: '确认',
    okType: 'danger',
    async onOk() {
      cleanupExecuting.value = true
      try {
        await executeCleanup()
        message.success('清理完成')
        cleanupPreviewVisible.value = false
        fetchFiles()
        fetchStorageStats()
      } finally {
        cleanupExecuting.value = false
      }
    },
  })
}

// ==================== 工具函数 ====================
function formatSize(bytes: number): string {
  if (!bytes || bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

function formatDate(val: string): string {
  if (!val) return ''
  const d = new Date(val)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function statusTagColor(status: string) {
  const map: Record<string, string> = { pending: 'processing', completed: 'success', failed: 'error' }
  return map[status] ?? 'default'
}

function statusLabel(status: string) {
  const map: Record<string, string> = { pending: '待处理', completed: '已完成', failed: '失败' }
  return map[status] ?? status
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchFiles()
  fetchStorageStats()
  fetchPolicies()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.file-manager {
  padding: $page-padding;
}

.stats-row {
  margin-bottom: $section-gap;
}

.stat-card {
  height: 120px;

  :deep(.ant-card-body) {
    padding: $spacing-md;
    height: 100%;
    display: flex;
    align-items: center;
  }
}

.stat-item {
  width: 100%;

  .stat-label {
    font-size: $font-size-sm;
    color: $text-secondary;
    margin-bottom: $spacing-xs;
  }

  .stat-value {
    font-size: 24px;
    font-weight: 600;
    color: $text-primary;
  }
}

.chart-card :deep(.ant-card-body) {
  padding: $spacing-sm;
}

.filter-card {
  margin-bottom: $section-gap;

  :deep(.ant-card-body) {
    padding: $spacing-md $spacing-md 0;
  }

  .filter-form {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
  }
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: $spacing-md;
}

.table-card :deep(.ant-card-body) {
  padding: 0;
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  padding: $spacing-md;
}

.policy-toolbar {
  display: flex;
  justify-content: space-between;
}

.cleanup-preview {
  text-align: center;
  padding: $spacing-lg 0;

  p {
    font-size: $font-size-lg;
    margin: $spacing-sm 0;
    color: $text-regular;
  }

  strong {
    color: $color-danger;
    font-size: 20px;
  }
}
</style>
