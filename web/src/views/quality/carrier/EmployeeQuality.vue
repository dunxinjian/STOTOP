<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import { getEmployeeRank, getEmployeeMetrics, getEmployeeTimeline } from '@/api/carrierQuality'

const carrier = ref('申通')
const dateRange = ref<[Dayjs, Dayjs]>([dayjs().subtract(29, 'day').startOf('day'), dayjs()])
const networkCode = ref<string | undefined>(undefined)

const dimension = ref('problem')
const dimensionOptions = [
  { value: 'problem', label: '问题件' }, { value: 'complaint', label: '客诉' },
  { value: 'fakesign', label: '虚签' }, { value: 'timeout', label: '超时' }, { value: 'fee', label: '考核金额' },
]
const rank = ref<any>({ worst: [], best: [] })

const metrics = ref<any[]>([])
const metricsTotal = ref(0)
const page = ref(1)
const size = ref(50)
const loading = ref(false)

// 单员工钻取抽屉
const drawerOpen = ref(false)
const drawerEmpNo = ref('')
const timeline = ref<any[]>([])

function baseParams() {
  return {
    carrier: carrier.value,
    from: dateRange.value[0].format('YYYY-MM-DD'),
    to: dateRange.value[1].format('YYYY-MM-DD'),
    networkCode: networkCode.value || undefined,
  }
}

async function fetchRank() {
  try { rank.value = await getEmployeeRank({ ...baseParams(), dimension: dimension.value, topN: 10 }) || { worst: [], best: [] } }
  catch { message.error('获取红黑榜失败') }
}
async function fetchMetrics() {
  loading.value = true
  try {
    const r = await getEmployeeMetrics({ ...baseParams(), page: page.value, size: size.value })
    metrics.value = r?.items || []; metricsTotal.value = r?.total || 0
  } catch { message.error('获取员工指标失败') } finally { loading.value = false }
}
function handleSearch() { page.value = 1; fetchRank(); fetchMetrics() }
function onTableChange(p: any) { page.value = p.current; size.value = p.pageSize; fetchMetrics() }

async function openDrawer(empNo: string) {
  drawerEmpNo.value = empNo; drawerOpen.value = true
  try {
    timeline.value = await getEmployeeTimeline(empNo, {
      carrier: carrier.value,
      from: dateRange.value[0].format('YYYY-MM-DD'),
      to: dateRange.value[1].format('YYYY-MM-DD'),
    }) || []
  } catch { message.error('获取员工时间线失败') }
}

const metricColumns = [
  { title: '工号', dataIndex: 'empNo', width: 100, fixed: 'left' as const },
  { title: '姓名', dataIndex: 'empName', width: 90, fixed: 'left' as const },
  { title: '网点', dataIndex: 'networkCode', width: 90 },
  { title: '派件量', dataIndex: '派件量', width: 80 },
  { title: '当日派签量', dataIndex: '当日派签量', width: 100 },
  { title: '应上门量', dataIndex: '应上门量', width: 90 },
  { title: '未上门量', dataIndex: '未上门量', width: 90 },
  { title: '客诉发起量', dataIndex: '客诉发起量', width: 100 },
  { title: '工单定责量', dataIndex: '工单定责量', width: 100 },
  { title: '虚假签收数', dataIndex: '虚假签收数', width: 100 },
  { title: '照片质检不合格数', dataIndex: '照片质检不合格数', width: 110 },
  { title: '超时T0', dataIndex: '派送超时T0数', width: 80 },
  { title: '超时T1', dataIndex: '派送超时T1数', width: 80 },
  { title: '超时T2', dataIndex: '派送超时T2数', width: 80 },
  { title: '超时T3', dataIndex: '派送超时T3数', width: 80 },
  { title: '揽收不及时数', dataIndex: '揽收不及时数', width: 100 },
  { title: '上传不及时数', dataIndex: '上传不及时数', width: 100 },
  { title: '问题件数', dataIndex: '问题件数', width: 90 },
  { title: '违规虚假电联', dataIndex: '违规虚假电联', width: 100 },
  { title: '违规无效电联', dataIndex: '违规无效电联', width: 100 },
  { title: '违规照片定位虚假', dataIndex: '违规照片定位虚假', width: 110 },
  { title: '违规签收文本不规范', dataIndex: '违规签收文本不规范', width: 120 },
  { title: '违规引导代收', dataIndex: '违规引导代收', width: 100 },
  { title: '违规双签', dataIndex: '违规双签', width: 90 },
  { title: '考核金额合计', dataIndex: '考核金额合计', width: 110 },
]

onMounted(() => { fetchRank(); fetchMetrics() })
</script>

<template>
  <div class="page">
    <PageHeader title="员工质量">
      <template #left><span class="view-title">员工质量</span></template>
      <template #actions>
        <div style="display:flex;align-items:center;gap:8px;">
          <a-select v-model:value="carrier" size="middle" style="width:100px" :options="[{ value: '申通', label: '申通' }]" />
          <a-range-picker v-model:value="dateRange" size="middle" style="width:260px" />
          <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card size="small" style="margin-bottom:12px;">
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px;">
        <span>红黑榜维度：</span>
        <a-radio-group v-model:value="dimension" button-style="solid" size="small" @change="fetchRank">
          <a-radio-button v-for="o in dimensionOptions" :key="o.value" :value="o.value">{{ o.label }}</a-radio-button>
        </a-radio-group>
      </div>
      <a-row :gutter="12">
        <a-col :span="12">
          <div class="rank-title">红榜（数值高/表现差 TOP10）</div>
          <a-list size="small" :data-source="rank.worst" :bordered="true">
            <template #renderItem="{ item, index }">
              <a-list-item>
                <span>{{ index + 1 }}. <a @click="openDrawer(item.empNo)">{{ item.empName || item.empNo }}</a>（{{ item.networkCode }}）</span>
                <span style="font-weight:600;">{{ item.value }}</span>
              </a-list-item>
            </template>
          </a-list>
        </a-col>
        <a-col :span="12">
          <div class="rank-title">黑榜（数值低/表现好 TOP10）</div>
          <a-list size="small" :data-source="rank.best" :bordered="true">
            <template #renderItem="{ item, index }">
              <a-list-item>
                <span>{{ index + 1 }}. <a @click="openDrawer(item.empNo)">{{ item.empName || item.empNo }}</a>（{{ item.networkCode }}）</span>
                <span style="font-weight:600;">{{ item.value }}</span>
              </a-list-item>
            </template>
          </a-list>
        </a-col>
      </a-row>
    </a-card>

    <a-card title="员工21指标明细" size="small">
      <a-table
        :columns="metricColumns" :data-source="metrics" :loading="loading" row-key="empNo" size="small"
        :scroll="{ x: 2600, y: 'calc(100vh - 460px)' }"
        :pagination="{ current: page, pageSize: size, total: metricsTotal, showSizeChanger: true, showQuickJumper: true, showTotal: (t: number) => `共 ${t} 条` }"
        @change="onTableChange">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'empNo'"><a @click="openDrawer(record.empNo)">{{ record.empNo }}</a></template>
        </template>
      </a-table>
    </a-card>

    <a-drawer v-model:open="drawerOpen" :title="`员工事件时间线 · ${drawerEmpNo}`" width="520">
      <a-timeline>
        <a-timeline-item v-for="(e, i) in timeline" :key="i">
          <div><b>{{ e.date ? dayjs(e.date).format('YYYY-MM-DD') : '' }}</b> · {{ e.domain }} · {{ e.problemName }}</div>
          <div style="color:var(--text-3);font-size:12px;">运单 {{ e.waybill }} · {{ e.networkName }} · 严重度 {{ e.severity }} · ¥{{ (e.fee || 0).toFixed(2) }}</div>
        </a-timeline-item>
      </a-timeline>
      <a-empty v-if="!timeline.length" description="该员工期内无质量事件" />
    </a-drawer>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
.page { padding: 12px; }
.view-title { font-weight: 600; }
.rank-title { font-weight: 600; margin-bottom: 6px; }
</style>
