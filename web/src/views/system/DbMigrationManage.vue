<template>
  <div class="page-container">
    <PageHeader title="数据库迁移管理" description="Schema Auto-Sync 可视化管理" />

    <!-- A. 当前状态区 -->
    <a-row :gutter="16" class="status-cards">
      <a-col :span="8">
        <a-card :bordered="false" :loading="statusLoading">
          <a-statistic
            title="Schema 同步状态"
            :value="syncStatus.hasPendingChanges ? `有${syncStatus.pendingCount}项待执行变更` : '已同步'"
            :value-style="{ color: syncStatus.hasPendingChanges ? 'var(--color-warning)' : 'var(--color-success)', fontSize: '16px' }"
          />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card :bordered="false" :loading="statusLoading">
          <a-statistic
            title="Seeder 版本状态"
            :value="syncStatus.seederStatus || '加载中...'"
            :value-style="{ color: syncStatus.seederStatus === '全部已执行' ? 'var(--color-success)' : 'var(--color-warning)', fontSize: '16px' }"
          />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card :bordered="false" :loading="statusLoading">
          <a-statistic
            title="最近同步时间"
            :value="syncStatus.lastSyncTime || '暂无记录'"
            :value-style="{ fontSize: '16px' }"
          />
        </a-card>
      </a-col>
    </a-row>

    <!-- B. 待执行变更区 -->
    <a-card v-if="pendingChanges.length > 0" :bordered="false" title="待执行变更" class="section-card">
      <a-table
        :dataSource="pendingChanges"
        :columns="pendingColumns"
        :row-selection="{ selectedRowKeys: selectedChangeIds, onChange: onSelectChange }"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'changeType'">
            <a-tag :color="record.changeType === 'AddColumn' ? 'green' : 'orange'">
              {{ record.changeType === 'AddColumn' ? '新增列' : '列类型变更' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'sqlStatement'">
            <a-typography-text code class="sql-preview">{{ record.sqlStatement }}</a-typography-text>
          </template>
        </template>
      </a-table>
      <div class="pending-actions">
        <a-button @click="showFullSql">查看完整SQL</a-button>
        <a-button
          type="primary"
          :disabled="selectedChangeIds.length === 0"
          :loading="executing"
          @click="handleExecuteSelected"
        >
          执行选中 ({{ selectedChangeIds.length }})
        </a-button>
        <a-button type="primary" :loading="executing" @click="handleExecuteAll">全部执行</a-button>
        <a-button danger :loading="executing" @click="handleSkipAll">全部跳过</a-button>
      </div>
    </a-card>

    <!-- D. 警告区 -->
    <a-card v-if="warnings.length > 0" :bordered="false" class="section-card">
      <a-collapse>
        <a-collapse-panel key="1" :header="`警告信息 (${warnings.length}项)`">
          <a-alert
            v-for="(warn, idx) in warnings"
            :key="idx"
            type="warning"
            show-icon
            :message="`${warn.tableName}.${warn.columnName}`"
            :description="warn.message"
            class="warning-item"
          />
        </a-collapse-panel>
      </a-collapse>
    </a-card>

    <!-- C. 执行历史区 -->
    <a-card :bordered="false" title="执行历史" class="section-card">
      <a-table
        :dataSource="historyData"
        :columns="historyColumns"
        :loading="historyLoading"
        :pagination="historyPagination"
        row-key="id"
        size="small"
        @change="handleHistoryTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 'Success' ? 'green' : 'red'">
              {{ record.status === 'Success' ? '成功' : '失败' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'durationMs'">
            {{ record.durationMs != null ? `${record.durationMs}ms` : '-' }}
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- SQL预览弹窗 -->
    <a-modal v-model:open="sqlModalVisible" title="完整SQL预览" width="720px" :footer="null">
      <pre class="sql-full-preview">{{ fullSqlText }}</pre>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import PageHeader from '@/components/PageHeader.vue'
import {
  getSchemaSyncStatus,
  getPendingChanges,
  executeSchemaSyncChanges,
  skipSchemaSyncChanges,
  getSchemaWarnings,
  getMigrationHistory,
  type SchemaSyncStatus,
  type SchemaChangeItem,
  type SchemaWarningItem,
  type MigrationHistoryItem,
} from '@/api/system'

// === 状态区 ===
const statusLoading = ref(false)
const syncStatus = reactive<SchemaSyncStatus>({
  hasPendingChanges: false,
  pendingCount: 0,
  seederStatus: '加载中...',
  lastSyncTime: null,
})

// === 待执行变更 ===
const pendingChanges = ref<SchemaChangeItem[]>([])
const selectedChangeIds = ref<number[]>([])
const executing = ref(false)

const pendingColumns = [
  { title: '表名', dataIndex: 'tableName', width: 160 },
  { title: '列名', dataIndex: 'columnName', width: 160 },
  { title: '变更类型', dataIndex: 'changeType', width: 100 },
  { title: 'SQL预览', dataIndex: 'sqlStatement', ellipsis: true },
  { title: '检测时间', dataIndex: 'detectedAt', width: 170 },
]

// === 警告区 ===
const warnings = ref<SchemaWarningItem[]>([])

// === 执行历史 ===
const historyData = ref<MigrationHistoryItem[]>([])
const historyLoading = ref(false)
const historyPagination = reactive({
  current: 1,
  pageSize: 15,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const historyColumns = [
  { title: '序号', dataIndex: 'id', width: 70 },
  { title: '时间', dataIndex: 'executedTime', width: 170 },
  { title: '模块', dataIndex: 'module', width: 120 },
  { title: '变更描述', dataIndex: 'description', ellipsis: true },
  { title: '状态', dataIndex: 'status', width: 80 },
  { title: '耗时', dataIndex: 'durationMs', width: 100 },
]

// === SQL预览弹窗 ===
const sqlModalVisible = ref(false)
const fullSqlText = ref('')

// === 方法 ===

async function loadStatus() {
  statusLoading.value = true
  try {
    const res = await getSchemaSyncStatus() as any
    const data = res?.data ?? res
    Object.assign(syncStatus, data)
  } catch (e) {
    console.error('加载状态失败', e)
  } finally {
    statusLoading.value = false
  }
}

async function loadPendingChanges() {
  try {
    const res = await getPendingChanges() as any
    const data = res?.data ?? res
    pendingChanges.value = Array.isArray(data) ? data : []
  } catch (e) {
    console.error('加载待执行变更失败', e)
  }
}

async function loadWarnings() {
  try {
    const res = await getSchemaWarnings() as any
    const data = res?.data ?? res
    warnings.value = Array.isArray(data) ? data : []
  } catch (e) {
    console.error('加载警告失败', e)
  }
}

async function loadHistory() {
  historyLoading.value = true
  try {
    const res = await getMigrationHistory(historyPagination.current, historyPagination.pageSize) as any
    const data = res?.data ?? res
    historyData.value = data?.items ?? []
    historyPagination.total = data?.total ?? 0
  } catch (e) {
    console.error('加载执行历史失败', e)
  } finally {
    historyLoading.value = false
  }
}

function onSelectChange(keys: Key[]) {
  selectedChangeIds.value = keys.map(Number)
}

function showFullSql() {
  const items = selectedChangeIds.value.length > 0
    ? pendingChanges.value.filter(c => selectedChangeIds.value.includes(c.id))
    : pendingChanges.value
  fullSqlText.value = items.map(c => `-- [${c.tableName}.${c.columnName}] ${c.changeType}\n${c.sqlStatement}`).join('\n\n')
  sqlModalVisible.value = true
}

function handleExecuteSelected() {
  if (selectedChangeIds.value.length === 0) return
  Modal.confirm({
    title: '确认执行',
    content: `确定要执行选中的 ${selectedChangeIds.value.length} 项变更吗？`,
    onOk: async () => {
      executing.value = true
      try {
        await executeSchemaSyncChanges(selectedChangeIds.value)
        message.success('执行成功')
        selectedChangeIds.value = []
        await refreshAll()
      } catch (e: any) {
        message.error(e?.message || '执行失败')
      } finally {
        executing.value = false
      }
    },
  })
}

function handleExecuteAll() {
  const allIds = pendingChanges.value.map(c => c.id)
  Modal.confirm({
    title: '确认全部执行',
    content: `确定要执行全部 ${allIds.length} 项变更吗？`,
    onOk: async () => {
      executing.value = true
      try {
        await executeSchemaSyncChanges(allIds)
        message.success('全部执行成功')
        selectedChangeIds.value = []
        await refreshAll()
      } catch (e: any) {
        message.error(e?.message || '执行失败')
      } finally {
        executing.value = false
      }
    },
  })
}

function handleSkipAll() {
  const allIds = pendingChanges.value.map(c => c.id)
  Modal.confirm({
    title: '确认跳过',
    content: `确定要跳过全部 ${allIds.length} 项变更吗？跳过后不再提示。`,
    onOk: async () => {
      executing.value = true
      try {
        await skipSchemaSyncChanges(allIds)
        message.success('已全部跳过')
        selectedChangeIds.value = []
        await refreshAll()
      } catch (e: any) {
        message.error(e?.message || '操作失败')
      } finally {
        executing.value = false
      }
    },
  })
}

function handleHistoryTableChange(pagination: any) {
  historyPagination.current = pagination.current
  historyPagination.pageSize = pagination.pageSize
  loadHistory()
}

async function refreshAll() {
  await Promise.all([loadStatus(), loadPendingChanges(), loadWarnings(), loadHistory()])
}

onMounted(() => {
  refreshAll()
})
</script>

<style scoped lang="scss">
.page-container {
  padding: 0;
}

.status-cards {
  margin-bottom: 16px;
}

.section-card {
  margin-bottom: 16px;
}

.pending-actions {
  margin-top: 12px;
  display: flex;
  gap: 8px;
}

.sql-preview {
  font-size: 12px;
  max-width: 400px;
  display: inline-block;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sql-full-preview {
  max-height: 500px;
  overflow: auto;
  background: #f5f5f5;
  padding: 12px;
  border-radius: 4px;
  font-size: 13px;
  white-space: pre-wrap;
  word-break: break-all;
}

.warning-item {
  margin-bottom: 8px;
}
</style>
