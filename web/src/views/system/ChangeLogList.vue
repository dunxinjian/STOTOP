<template>
  <div class="page-container">
    <PageHeader title="变更记录" description="查询组织架构相关的变更操作日志">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.businessType" size="small" placeholder="业务类型" style="width: 120px" allowClear
            :options="[
              { label: '全部', value: '' },
              { label: '组织', value: 'Organization' },
              { label: '人员', value: 'User' },
              { label: '岗位', value: 'Position' },
            ]"
          />
          <a-select v-model:value="searchForm.operationType" size="small" placeholder="操作类型" style="width: 120px" allowClear
            :options="[
              { label: '全部', value: '' },
              { label: '新增', value: 'Create' },
              { label: '修改', value: 'Update' },
              { label: '删除', value: 'Delete' },
              { label: '绑定', value: 'Bind' },
              { label: '解绑', value: 'Unbind' },
              { label: '同步', value: 'Sync' },
            ]"
          />
          <a-input v-model:value="searchForm.operatorName" size="small" placeholder="操作人" allowClear style="width: 130px" />
          <a-range-picker
            v-model:value="searchForm.dateRange"
            size="small"
            :placeholder="['开始日期', '结束日期']"
            value-format="YYYY-MM-DD"
            style="width: 240px"
          />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        :expandedRowKeys="expandedRowKeys"
        @expandedRowsChange="(keys: (string | number)[]) => expandedRowKeys = keys as string[]"
        :pagination="{
          current: pagination.pageIndex,
          pageSize: pagination.pageSize,
          total: pagination.total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
          pageSizeOptions: ['10', '20', '50', '100'],
          onChange: handlePageChange,
          onShowSizeChange: (_c: number, s: number) => handleSizeChange(s),
        }"
      >
        <template #expandedRowRender="{ record }">
          <div class="expand-content">
            <div class="expand-title">变更详情</div>
            <a-table
              v-if="getChangeDetails(record.changeContent).length > 0"
              :columns="detailColumns"
              :dataSource="getChangeDetails(record.changeContent)"
              :pagination="false"
              size="small"
              bordered
              rowKey="field"
            >
              <template #bodyCell="{ column, record: detail }">
                <template v-if="column.dataIndex === 'oldValue'">
                  {{ detail.oldValue ?? '-' }}
                </template>
                <template v-if="column.dataIndex === 'newValue'">
                  {{ detail.newValue ?? '-' }}
                </template>
              </template>
            </a-table>
            <div v-else class="expand-raw">{{ record.changeContent || '无详细内容' }}</div>
          </div>
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'businessType'">
            <a-tag v-if="record.businessType === 'Organization'" color="blue">组织</a-tag>
            <a-tag v-else-if="record.businessType === 'User'" color="orange">人员</a-tag>
            <a-tag v-else-if="record.businessType === 'Position'" color="green">岗位</a-tag>
            <a-tag v-else>{{ record.businessType }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'operationType'">
            {{ operationTypeMap[record.operationType] || record.operationType }}
          </template>
          <template v-if="column.dataIndex === 'dingTalkSyncStatus'">
            <a-tag v-if="record.dingTalkSyncStatus === 1" color="success">已同步</a-tag>
            <a-tag v-else-if="record.dingTalkSyncStatus === 2" color="error">同步失败</a-tag>
            <a-tag v-else-if="record.dingTalkSyncStatus === 3">无需同步</a-tag>
            <a-tag v-else color="default">未同步</a-tag>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无变更记录" />
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getChangeLogs,
  type ChangeLogDto,
} from '@/api/system'

interface ChangeDetail {
  field: string
  oldValue: string | null
  newValue: string | null
}

const operationTypeMap: Record<string, string> = {
  Create: '新增',
  Update: '修改',
  Delete: '删除',
  Bind: '绑定',
  Unbind: '解绑',
  Sync: '同步',
}

const columns = [
  { title: '操作时间', dataIndex: 'operationTime', key: 'operationTime', width: 170 },
  { title: '业务类型', dataIndex: 'businessType', key: 'businessType', width: 100, align: 'center' as const },
  { title: '对象名称', dataIndex: 'businessName', key: 'businessName', ellipsis: true },
  { title: '操作类型', dataIndex: 'operationType', key: 'operationType', width: 100, align: 'center' as const },
  { title: '操作人', dataIndex: 'operatorName', key: 'operatorName', width: 100 },
  { title: '钉钉同步', dataIndex: 'dingTalkSyncStatus', key: 'dingTalkSyncStatus', width: 110, align: 'center' as const },
]

const detailColumns = [
  { title: '字段名', dataIndex: 'field', key: 'field', width: 180 },
  { title: '旧值', dataIndex: 'oldValue', key: 'oldValue' },
  { title: '新值', dataIndex: 'newValue', key: 'newValue' },
]

// 搜索表单
const searchForm = reactive({
  businessType: '',
  operationType: '',
  operatorName: '',
  dateRange: undefined as [string, string] | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<ChangeLogDto[]>([])
const expandedRowKeys = ref<string[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

// 获取变更记录列表
async function fetchChangeLogs() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.businessType) params.businessType = searchForm.businessType
    if (searchForm.operationType) params.operationType = searchForm.operationType
    if (searchForm.dateRange && searchForm.dateRange[0]) {
      params.startTime = searchForm.dateRange[0]
      params.endTime = searchForm.dateRange[1]
    }

    const res = await getChangeLogs(params) as any
    if (res) {
      tableData.value = res?.items || []
      pagination.total = res?.total || 0
    }
  } finally {
    loading.value = false
  }
}

// 解析变更内容为详情列表
function getChangeDetails(content: string): ChangeDetail[] {
  if (!content) return []
  try {
    const parsed = JSON.parse(content)
    if (Array.isArray(parsed)) {
      return parsed.map((item: any) => ({
        field: item.field || item.fieldName || item.Field || '',
        oldValue: item.oldValue ?? item.OldValue ?? null,
        newValue: item.newValue ?? item.NewValue ?? null,
      }))
    }
    if (typeof parsed === 'object' && parsed !== null) {
      return Object.entries(parsed).map(([key, value]: [string, any]) => ({
        field: key,
        oldValue: typeof value === 'object' ? (value?.old ?? value?.oldValue ?? null) : null,
        newValue: typeof value === 'object' ? (value?.new ?? value?.newValue ?? null) : String(value),
      }))
    }
    return []
  } catch {
    return []
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchChangeLogs()
}

// 重置搜索
function handleReset() {
  searchForm.businessType = ''
  searchForm.operationType = ''
  searchForm.operatorName = ''
  searchForm.dateRange = undefined
  pagination.pageIndex = 1
  fetchChangeLogs()
}

// 分页
function handleSizeChange(val: number) {
  pagination.pageSize = val
  fetchChangeLogs()
}

function handlePageChange(val: number) {
  pagination.pageIndex = val
  fetchChangeLogs()
}

onMounted(() => {
  fetchChangeLogs()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.expand-content {
  padding: 12px 20px;

  .expand-title {
    font-size: 13px;
    font-weight: 500;
    margin-bottom: 8px;
    color: $text-primary;
  }

  .expand-raw {
    font-size: 13px;
    color: $text-secondary;
    white-space: pre-wrap;
    word-break: break-all;
  }
}
</style>
