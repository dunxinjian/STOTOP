<template>
  <div class="page-container">
    <PageHeader title="安全审计日志" description="查看系统登录及安全相关事件记录">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-range-picker
            v-model:value="searchForm.timeRange"
            size="small"
            style="width: 240px"
            :placeholder="['开始时间', '结束时间']"
            show-time
          />
          <a-select
            v-model:value="searchForm.eventType"
            size="small"
            placeholder="事件类型"
            allowClear
            style="width: 120px"
          >
            <a-select-option value="">全部</a-select-option>
            <a-select-option value="Login">登录</a-select-option>
            <a-select-option value="Logout">登出</a-select-option>
            <a-select-option value="TokenRefresh">Token刷新</a-select-option>
            <a-select-option value="Timeout">超时</a-select-option>
            <a-select-option value="Kicked">强制下线</a-select-option>
            <a-select-option value="AnomalyDetected">异常检测</a-select-option>
          </a-select>
          <a-select
            v-model:value="searchForm.eventResult"
            size="small"
            placeholder="结果"
            allowClear
            style="width: 100px"
          >
            <a-select-option value="">全部</a-select-option>
            <a-select-option value="Success">成功</a-select-option>
            <a-select-option value="Failed">失败</a-select-option>
          </a-select>
          <a-input-search
            v-model:value="searchForm.account"
            size="small"
            placeholder="账号搜索"
            style="width: 180px"
            @search="handleSearch"
            allowClear
          />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        row-key="id"
        :bordered="false"
        :scroll="{ x: 1200 }"
        :pagination="{
          current: pagination.page,
          pageSize: pagination.pageSize,
          total: pagination.total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
          onChange: handlePageChange,
          onShowSizeChange: handleSizeChange,
        }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'eventType'">
            <a-tag :color="eventTypeColor(record.eventType)">
              {{ eventTypeLabel(record.eventType) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'eventResult'">
            <a-tag :color="record.eventResult === 'Success' ? 'green' : 'red'">
              {{ record.eventResult === 'Success' ? '成功' : '失败' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'ipAddress'">
            {{ formatIpAddress(record.ipAddress) }}
          </template>
          <template v-if="column.dataIndex === 'deviceInfo'">
            <a-tooltip :title="record.deviceInfo">
              <span class="text-ellipsis">{{ formatDeviceInfo(record.deviceInfo) }}</span>
            </a-tooltip>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import { getAuditLogs, type AuditLogItem } from '@/api/security'

const loading = ref(false)
const tableData = ref<AuditLogItem[]>([])

const searchForm = reactive({
  timeRange: undefined as [Dayjs, Dayjs] | undefined,
  eventType: '' as string,
  eventResult: '' as string,
  account: '' as string,
})

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0,
})

const columns = [
  { title: '时间', dataIndex: 'createTime', width: 170 },
  { title: '账号', dataIndex: 'account', width: 120 },
  { title: '事件类型', dataIndex: 'eventType', width: 120 },
  { title: '结果', dataIndex: 'eventResult', width: 80 },
  { title: 'IP地址', dataIndex: 'ipAddress', width: 140 },
  { title: '设备信息', dataIndex: 'deviceInfo', width: 200, ellipsis: true },
  { title: '失败原因', dataIndex: 'failReason', width: 180 },
]

const eventTypeColorMap: Record<string, string> = {
  Login: 'blue',
  Logout: 'green',
  TokenRefresh: 'cyan',
  Timeout: 'orange',
  Kicked: 'red',
  AnomalyDetected: 'purple',
}

const eventTypeLabelMap: Record<string, string> = {
  Login: '登录',
  Logout: '登出',
  TokenRefresh: 'Token刷新',
  Timeout: '超时',
  Kicked: '强制下线',
  AnomalyDetected: '异常检测',
}

function eventTypeColor(type: string): string {
  return eventTypeColorMap[type] || 'default'
}

function eventTypeLabel(type: string): string {
  return eventTypeLabelMap[type] || type
}

/**
 * IP 地址友好化显示：
 *  - IPv6 本机 ::1 或 0:0:0:0:0:0:0:1 → 127.0.0.1 (本机)
 *  - IPv4 映射地址 ::ffff:x.x.x.x → x.x.x.x
 */
function formatIpAddress(ip: string | null | undefined): string {
  if (!ip) return '-'
  const v = String(ip).trim()
  if (v === '::1' || v === '0:0:0:0:0:0:0:1') return '127.0.0.1 (本机)'
  if (v.toLowerCase().startsWith('::ffff:')) return v.substring(7)
  return v
}

/**
 * User-Agent 解析为浏览器 + 操作系统的简短描述
 */
function formatDeviceInfo(ua: string | null | undefined): string {
  if (!ua) return '-'
  const s = String(ua)
  // 操作系统
  let os = '未知系统'
  if (/Windows NT 10\.0/i.test(s)) os = 'Windows 10/11'
  else if (/Windows NT 6\.3/i.test(s)) os = 'Windows 8.1'
  else if (/Windows NT 6\.1/i.test(s)) os = 'Windows 7'
  else if (/Windows/i.test(s)) os = 'Windows'
  else if (/Android\s+([\d.]+)/i.test(s)) os = 'Android ' + RegExp.$1
  else if (/(iPhone|iPad).*OS\s+([\d_]+)/i.test(s)) os = 'iOS ' + RegExp.$2.replace(/_/g, '.')
  else if (/Mac OS X\s+([\d_]+)/i.test(s)) os = 'macOS ' + RegExp.$1.replace(/_/g, '.')
  else if (/Linux/i.test(s)) os = 'Linux'

  // 浏览器（顺序有要求：Edge 优先于 Chrome，Opera 优先于 Chrome）
  let browser = '未知浏览器'
  let m: RegExpMatchArray | null
  if ((m = s.match(/Edg\/([\d.]+)/))) browser = 'Edge ' + m[1].split('.')[0]
  else if ((m = s.match(/OPR\/([\d.]+)/))) browser = 'Opera ' + m[1].split('.')[0]
  else if ((m = s.match(/Firefox\/([\d.]+)/))) browser = 'Firefox ' + m[1].split('.')[0]
  else if ((m = s.match(/Chrome\/([\d.]+)/))) browser = 'Chrome ' + m[1].split('.')[0]
  else if ((m = s.match(/Version\/([\d.]+).*Safari/))) browser = 'Safari ' + m[1].split('.')[0]
  else if (/MSIE|Trident/.test(s)) browser = 'IE'

  return `${browser} / ${os}`
}

async function fetchData() {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize,
    }
    if (searchForm.timeRange && searchForm.timeRange.length === 2) {
      params.startTime = searchForm.timeRange[0].format('YYYY-MM-DD HH:mm:ss')
      params.endTime = searchForm.timeRange[1].format('YYYY-MM-DD HH:mm:ss')
    }
    if (searchForm.eventType) params.eventType = searchForm.eventType
    if (searchForm.eventResult) params.eventResult = searchForm.eventResult
    if (searchForm.account) params.account = searchForm.account

    const res = await getAuditLogs(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch (e: any) {
    message.error(e.message || '获取审计日志失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.page = 1
  fetchData()
}

function handleReset() {
  searchForm.timeRange = undefined
  searchForm.eventType = ''
  searchForm.eventResult = ''
  searchForm.account = ''
  pagination.page = 1
  fetchData()
}

function handlePageChange(page: number, pageSize: number) {
  pagination.page = page
  pagination.pageSize = pageSize
  fetchData()
}

function handleSizeChange(_current: number, size: number) {
  pagination.pageSize = size
  pagination.page = 1
  fetchData()
}

onMounted(() => {
  fetchData()
})
</script>

<style scoped lang="scss">
.text-ellipsis {
  display: inline-block;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}
</style>
