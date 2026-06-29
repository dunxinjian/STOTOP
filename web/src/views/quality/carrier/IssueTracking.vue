<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import { getEvents, getPendingCount, exportEvents, type EventQueryParams } from '@/api/carrierQuality'
import { downloadBlob } from '@/utils/download'

const carrier = ref('申通')
const dateRange = ref<[Dayjs, Dayjs]>([dayjs().subtract(29, 'day').startOf('day'), dayjs()])
const filters = ref({ networkCode: '', empNo: '', domain: '', platform: '', severity: undefined as number | undefined })
const multiDomainOnly = ref(false)
const pendingOnly = ref(false)

const pendingCount = ref(0)

const rows = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(50)
const loading = ref(false)

function buildParams(): EventQueryParams {
  return {
    carrier: carrier.value,
    from: dateRange.value[0].format('YYYY-MM-DD'),
    to: dateRange.value[1].format('YYYY-MM-DD'),
    networkCode: filters.value.networkCode || undefined,
    empNo: filters.value.empNo || undefined,
    domain: filters.value.domain || undefined,
    platform: filters.value.platform || undefined,
    severity: filters.value.severity,
    multiDomainOnly: multiDomainOnly.value || undefined,
    pendingOnly: pendingOnly.value || undefined,
    page: page.value,
    size: size.value,
  }
}

async function fetchData() {
  loading.value = true
  try {
    const r = await getEvents(buildParams())
    rows.value = r?.items || []; total.value = r?.total || 0
  } catch { message.error('获取问题件失败') } finally { loading.value = false }
}
function handleSearch() { page.value = 1; fetchData() }
function onTableChange(p: any) { page.value = p.current; size.value = p.pageSize; fetchData() }
async function handleExport() {
  try {
    const blob = await exportEvents({ ...buildParams(), page: 1, size: 100000 })
    downloadBlob(blob, `问题件追踪-${dayjs().format('YYYYMMDD-HHmmss')}.csv`)
  } catch { message.error('导出失败') }
}

const columns = [
  { title: '业务日期', dataIndex: 'date', width: 110, customRender: ({ text }: any) => text ? dayjs(text).format('YYYY-MM-DD') : '' },
  { title: '运单号', dataIndex: 'waybill', width: 150 },
  { title: '网点', dataIndex: 'networkName', width: 120 },
  { title: '工号', dataIndex: 'empNo', width: 100 },
  { title: '姓名', dataIndex: 'empNameRaw', width: 90 },
  { title: '质量域', dataIndex: 'domain', width: 110 },
  { title: '问题类型', dataIndex: 'problemName', width: 160 },
  { title: '严重度', dataIndex: 'severity', width: 80 },
  { title: '考核金额', dataIndex: 'fee', width: 100, customRender: ({ text }: any) => '¥' + (text || 0).toFixed(2) },
  { title: '平台', dataIndex: 'platform', width: 100 },
]

function rowClassName(record: any) {
  if (record.isMultiDomain) return 'row-multi'
  if (record.isPending) return 'row-pending'
  return ''
}

onMounted(async () => {
  fetchData()
  try { pendingCount.value = (await getPendingCount({ carrier: carrier.value })) || 0 } catch { /* 静默 */ }
})
</script>

<template>
  <div class="page">
    <PageHeader title="问题件追踪">
      <template #left><span class="view-title">问题件追踪</span></template>
      <template #actions>
        <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">
          <a-select v-model:value="carrier" size="middle" style="width:90px" :options="[{ value: '申通', label: '申通' }]" />
          <a-range-picker v-model:value="dateRange" size="middle" style="width:240px" />
          <a-input v-model:value="filters.networkCode" size="middle" placeholder="网点编码" allow-clear style="width:110px" />
          <a-input v-model:value="filters.empNo" size="middle" placeholder="工号" allow-clear style="width:100px" />
          <a-input v-model:value="filters.domain" size="middle" placeholder="质量域" allow-clear style="width:110px" />
          <a-input v-model:value="filters.platform" size="middle" placeholder="平台" allow-clear style="width:100px" />
          <a-checkbox v-model:checked="multiDomainOnly">仅看重点件</a-checkbox>
          <a-checkbox v-model:checked="pendingOnly">仅看待认领</a-checkbox>
          <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
          <a-button size="middle" @click="handleExport">导出</a-button>
        </div>
      </template>
    </PageHeader>

    <a-alert
      v-if="pendingCount > 0"
      type="warning"
      show-icon
      style="margin-bottom:12px;"
      :message="`当前有 ${pendingCount} 条待认领事件，建议前往认领页处理`">
      <template #description>
        <router-link to="/quality/carrier/claim">去认领</router-link>
      </template>
    </a-alert>

    <a-card size="small">
      <a-table
        :columns="columns" :data-source="rows" :loading="loading" row-key="id" size="small"
        :row-class-name="rowClassName"
        :scroll="{ x: 1200, y: 'calc(100vh - 280px)' }"
        :pagination="{ current: page, pageSize: size, total, showSizeChanger: true, showQuickJumper: true, showTotal: (t: number) => `共 ${t} 条` }"
        @change="onTableChange">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'domain'">
            <a-tag v-if="record.isMultiDomain" color="red">多域</a-tag>{{ record.domain }}
          </template>
          <template v-else-if="column.dataIndex === 'empNameRaw'">
            <span>{{ record.empNameRaw }}</span><a-tag v-if="record.isPending" color="orange" style="margin-left:4px;">待认领</a-tag>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
.page { padding: 12px; }
.view-title { font-weight: 600; }
:deep(.row-pending) { background: var(--bg-disabled, #f5f5f5); color: var(--text-3); }
:deep(.row-multi) { background: rgba(214, 88, 78, 0.06); }
</style>
