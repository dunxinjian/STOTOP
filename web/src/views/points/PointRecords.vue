<template>
  <div class="point-records-page">
    <PageHeader title="积分流水明细">
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px; flex-wrap:wrap;">
            <a-select
              v-model:value="filters.sourceId"
              placeholder="来源类型"
              allow-clear
              style="width: 140px"
              @change="handleSearch"
            >
              <a-select-option v-for="src in sourceOptions" :key="src.id" :value="src.id">
                {{ src.sourceName }}
              </a-select-option>
            </a-select>
            <a-select
              v-model:value="filters.type"
              placeholder="变动类型"
              allow-clear
              style="width: 120px"
              @change="handleSearch"
            >
              <a-select-option :value="1">奖分</a-select-option>
              <a-select-option :value="2">扣分</a-select-option>
            </a-select>
            <a-select
              v-model:value="filters.accountType"
              placeholder="账户类型"
              allow-clear
              style="width: 120px"
              @change="handleSearch"
            >
              <a-select-option :value="1">A 分</a-select-option>
              <a-select-option :value="2">B 分</a-select-option>
            </a-select>
            <a-range-picker
              v-model:value="dateRange"
              @change="handleSearch"
            />
            <a-input
              v-model:value="filters.keyword"
              placeholder="搜索备注/规则名"
              allow-clear
              style="width: 200px"
              @keyup.enter="handleSearch"
              @change="(e: any) => { if (!e.target.value) handleSearch() }"
            >
              <template #prefix><SearchOutlined /></template>
            </a-input>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-table
      :columns="columns"
      :data-source="tableData"
      :loading="loading"
      :pagination="paginationConfig"
      row-key="id"
      bordered
      :scroll="{ x: 1100 }"
      class="records-table"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.dataIndex === 'index'">
          {{ (pageIndex - 1) * pageSize + index + 1 }}
        </template>
        <template v-if="column.dataIndex === 'pointValue'">
          <span :style="{ color: record.pointValue > 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }">
            {{ record.pointValue > 0 ? '+' : '' }}{{ record.pointValue }}
          </span>
        </template>
        <template v-if="column.dataIndex === 'type'">
          <a-tag :color="record.type === 1 ? 'green' : 'red'">
            {{ record.type === 1 ? '奖分' : '扣分' }}
          </a-tag>
        </template>
        <template v-if="column.dataIndex === 'accountType'">
          <a-tag :color="record.accountType === 1 ? 'purple' : 'orange'">
            {{ record.accountType === 1 ? 'A 分' : 'B 分' }}
          </a-tag>
        </template>
        <template v-if="column.dataIndex === 'createTime'">
          {{ formatTime(record.createTime) }}
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { SearchOutlined } from '@ant-design/icons-vue'
import type { Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import { getPointRecords, getPointSources } from '@/api/points'
import type { PointRecordListDto, PointSourceDto } from '@/types/points'

const loading = ref(false)
const tableData = ref<PointRecordListDto[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(20)

const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)

const filters = reactive({
  keyword: '',
  sourceId: undefined as number | undefined,
  type: undefined as number | undefined,
  accountType: undefined as number | undefined,
})

const sourceOptions = ref<PointSourceDto[]>([])

const columns = [
  { title: '#', dataIndex: 'index', width: 50 },
  { title: '时间', dataIndex: 'createTime', width: 160 },
  { title: '来源', dataIndex: 'sourceName', width: 120 },
  { title: '账户类型', dataIndex: 'accountType', width: 100 },
  { title: '类型', dataIndex: 'type', width: 80 },
  { title: '规则名', dataIndex: 'ruleName', width: 150 },
  { title: '积分值', dataIndex: 'pointValue', width: 100 },
  { title: '余额', dataIndex: 'balance', width: 100 },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '操作人', dataIndex: 'operatorName', width: 100 },
]

const paginationConfig = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showTotal: (t: number) => `共 ${t} 条`,
  showSizeChanger: true,
  showQuickJumper: true,
}))

function formatTime(t: string) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 16)
}

function handleSearch() {
  pageIndex.value = 1
  loadData()
}

function handleTableChange(pagination: any) {
  pageIndex.value = pagination.current
  pageSize.value = pagination.pageSize
  loadData()
}

async function loadSources() {
  try {
    sourceOptions.value = await getPointSources()
  } catch {
    // ignore
  }
}

async function loadData() {
  loading.value = true
  try {
    const res = await getPointRecords({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: filters.keyword || undefined,
      sourceId: filters.sourceId ?? undefined,
      type: filters.type ?? undefined,
      accountType: filters.accountType ?? undefined,
      startTime: dateRange.value?.[0]?.format('YYYY-MM-DD') ?? undefined,
      endTime: dateRange.value?.[1]?.format('YYYY-MM-DD') ?? undefined,
    })
    tableData.value = res.items
    total.value = res.total
  } catch {
    message.error('加载积分流水失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadSources()
  loadData()
})
</script>

<style scoped lang="scss">
.point-records-page {
  padding: 0 4px;
}

.records-table {
  border-radius: 8px;
}
</style>
