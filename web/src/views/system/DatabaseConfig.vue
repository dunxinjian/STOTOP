<template>
  <div class="page-container">
    <PageHeader title="数据库配置" description="查看和管理数据库连接配置" />

    <a-card :bordered="false">
      <a-spin :spinning="loading">
        <a-descriptions :column="1" bordered>
          <a-descriptions-item label="数据库类型">
            <div class="db-type-info">
              <DollarCircleOutlined :style="{ fontSize: '20px' }" />
              <span>{{ dbTypeName }}</span>
            </div>
          </a-descriptions-item>
          <a-descriptions-item label="连接状态">
            <div class="status-info">
              <span
                class="status-dot"
                :class="connectionStatus"
              ></span>
              <span>{{ statusText }}</span>
            </div>
          </a-descriptions-item>
          <a-descriptions-item v-if="configInfo.server" label="服务器地址">
            {{ configInfo.server }}
          </a-descriptions-item>
          <a-descriptions-item v-if="configInfo.port" label="端口">
            {{ configInfo.port }}
          </a-descriptions-item>
          <a-descriptions-item v-if="configInfo.database" label="数据库名">
            {{ configInfo.database }}
          </a-descriptions-item>
          <a-descriptions-item v-if="configInfo.username" label="用户名">
            {{ configInfo.username }}
          </a-descriptions-item>
          <a-descriptions-item v-if="configInfo.password" label="密码">
            ****
          </a-descriptions-item>
        </a-descriptions>
      </a-spin>

      <div class="action-buttons">
        <a-button type="primary" :loading="testing" @click="handleTestConnection">
          <template #icon><ApiOutlined /></template>
          测试连接
        </a-button>
        <a-tooltip title="即将推出">
          <a-button disabled>
            <template #icon><DownloadOutlined /></template>
            备份数据库
          </a-button>
        </a-tooltip>
        <a-tooltip title="即将推出">
          <a-button disabled>
            <template #icon><UploadOutlined /></template>
            恢复数据库
          </a-button>
        </a-tooltip>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { DollarCircleOutlined, ApiOutlined, DownloadOutlined, UploadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getDatabaseConfig,
  testDatabaseConnection,
  type DatabaseConfig,
} from '@/api/system'
import { get } from '@/api/request'

interface DatabaseStatus {
  isInitialized: boolean
  needSetup: boolean
  databaseType?: string
  connectionStatus?: 'connected' | 'disconnected' | 'error'
}

// 状态
const loading = ref(true)
const testing = ref(false)
const status = ref<DatabaseStatus | null>(null)
const configInfo = ref<DatabaseConfig>({})

// 计算属性
const dbTypeName = computed(() => 'SQL Server')

const connectionStatus = computed(() => {
  if (!status.value) return 'unknown'
  if (status.value.connectionStatus === 'connected') return 'connected'
  if (status.value.connectionStatus === 'disconnected') return 'disconnected'
  return 'error'
})

const statusText = computed(() => {
  if (!status.value) return '未知'
  switch (status.value.connectionStatus) {
    case 'connected':
      return '已连接'
    case 'disconnected':
      return '未连接'
    case 'error':
      return '连接错误'
    default:
      return '未知'
  }
})

// 获取数据库状态
async function fetchStatus() {
  try {
    const res = await get('/system/database/status', {}, { silent: true } as any) as DatabaseStatus
    status.value = res
  } catch (error) {
    console.error('获取数据库状态失败:', error)
    status.value = {
      isInitialized: false,
      needSetup: true,
      connectionStatus: 'error',
    }
  }
}

// 获取数据库配置
async function fetchConfig() {
  try {
    const res = await getDatabaseConfig() as DatabaseConfig
    if (res) {
      configInfo.value = res
    }
  } catch (error) {
    console.error('获取数据库配置失败:', error)
  }
}

// 测试连接
async function handleTestConnection() {
  testing.value = true
  try {
    const result = await testDatabaseConnection(configInfo.value) as { success: boolean; message?: string }
    if (result.success) {
      message.success('数据库连接成功')
      // 更新状态
      if (status.value) {
        status.value.connectionStatus = 'connected'
      }
    } else {
      message.error(result.message || '连接失败')
    }
  } catch (error: any) {
    message.error(error.message || '连接测试失败')
  } finally {
    testing.value = false
  }
}

// 初始化
onMounted(async () => {
  loading.value = true
  try {
    await Promise.all([fetchStatus(), fetchConfig()])
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.db-type-info {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  color: var(--text-1);
}

.status-info {
  display: flex;
  align-items: center;
  gap: $spacing-sm;

  .status-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;

    &.connected {
      background-color: $color-success;
    }

    &.disconnected {
      background-color: $color-warning;
    }

    &.error, &.unknown {
      background-color: $color-danger;
    }
  }
}

.action-buttons {
  display: flex;
  gap: 12px;
  margin-top: $spacing-lg;
  padding-top: $spacing-lg;
  border-top: 1px solid $border-color-lighter;
}
</style>
