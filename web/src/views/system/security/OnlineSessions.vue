<template>
  <div class="page-container">
    <PageHeader title="在线会话管理" description="查看当前在线用户并管理会话">
      <template #actions>
        <span style="display: inline-flex; align-items: center; gap: 8px; margin-right: 24px; font-size: 14px;">
          <span>当前在线</span>
          <span style="font-weight: 600;">{{ tableData.length }}</span>
        </span>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        row-key="sessionId"
        :bordered="false"
        :scroll="{ x: 1000 }"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'deviceInfo'">
            <a-tooltip :title="record.deviceInfo">
              <span class="text-ellipsis">{{ record.deviceInfo }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-popconfirm
              title="确定要强制下线该用户吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleKick(record as OnlineUser)"
            >
              <a-button type="link" danger size="small">强制下线</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getOnlineUsers, adminTerminateSession, type OnlineUser } from '@/api/security'

const loading = ref(false)
const tableData = ref<OnlineUser[]>([])
let refreshTimer: ReturnType<typeof setInterval> | null = null

const columns = [
  { title: '用户名', dataIndex: 'userName', width: 120 },
  { title: '账号', dataIndex: 'account', width: 120 },
  { title: '登录时间', dataIndex: 'loginTime', width: 170 },
  { title: '最后活跃', dataIndex: 'lastActiveTime', width: 170 },
  { title: 'IP地址', dataIndex: 'ipAddress', width: 140 },
  { title: '设备信息', dataIndex: 'deviceInfo', width: 200, ellipsis: true },
  { title: '操作', dataIndex: 'action', width: 100, fixed: 'right' as const },
]

async function fetchData() {
  loading.value = true
  try {
    tableData.value = await getOnlineUsers()
  } catch (e: any) {
    message.error(e.message || '获取在线用户列表失败')
  } finally {
    loading.value = false
  }
}

async function handleKick(record: OnlineUser) {
  try {
    await adminTerminateSession(record.sessionId)
    message.success(`已将用户 ${record.userName || record.account} 强制下线`)
    fetchData()
  } catch (e: any) {
    message.error(e.message || '强制下线失败')
  }
}

onMounted(() => {
  fetchData()
  // 每30秒自动刷新
  refreshTimer = setInterval(fetchData, 30000)
})

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
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
