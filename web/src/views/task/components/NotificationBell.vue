<template>
  <a-dropdown
    v-model:open="dropdownOpen"
    trigger="click"
    placement="bottomRight"
    :overlay-style="{ width: '380px' }"
  >
    <div class="notification-bell" @click.stop>
      <a-badge :count="totalBadgeCount" :overflow-count="99" :offset="[-2, 2]">
        <BellOutlined class="bell-icon" />
      </a-badge>
    </div>

    <template #overlay>
      <div class="notification-dropdown">
        <div class="notification-dropdown__header">
          <div class="notification-dropdown__tabs">
            <span
              class="notification-dropdown__tab"
              :class="{ active: activeTab === 'notification' }"
              @click="activeTab = 'notification'"
            >通知<a-badge v-if="unreadCount > 0" :count="unreadCount" :number-style="{ fontSize: '10px', marginLeft: '4px' }" /></span>
            <span
              class="notification-dropdown__tab"
              :class="{ active: activeTab === 'quality' }"
              @click="activeTab = 'quality'"
            >质量预警<a-badge v-if="qualityPendingCount > 0" :count="qualityPendingCount" :number-style="{ fontSize: '10px', marginLeft: '4px' }" /></span>
          </div>
          <a v-if="activeTab === 'notification' && unreadCount > 0" class="notification-dropdown__action" @click="handleMarkAllRead">全部已读</a>
        </div>

        <!-- 通知列表 -->
        <template v-if="activeTab === 'notification'">
          <a-spin :spinning="loading" size="small">
            <div v-if="recentList.length === 0 && !loading" class="notification-dropdown__empty">
              <BellOutlined style="font-size: 32px; color: #d9d9d9" />
              <p>暂无通知</p>
            </div>

            <div v-else class="notification-dropdown__list">
              <div
                v-for="item in recentList"
                :key="item.id"
                class="dropdown-item"
                :class="{ unread: !item.isRead }"
                @click="handleClickItem(item)"
              >
                <div class="dropdown-item__dot" v-if="!item.isRead" />
                <div class="dropdown-item__content">
                  <div class="dropdown-item__title">{{ item.title }}</div>
                  <div class="dropdown-item__desc">{{ item.content }}</div>
                  <div class="dropdown-item__time">{{ formatRelativeTime(item.createTime) }}</div>
                </div>
              </div>
            </div>
          </a-spin>

          <div class="notification-dropdown__footer" @click="goToCenter">
            查看全部通知
          </div>
        </template>

        <!-- 质量预警列表 -->
        <template v-if="activeTab === 'quality'">
          <div v-if="qualityPendingCount === 0 && qualityOverdueCount === 0" class="notification-dropdown__empty">
            <WarningOutlined style="font-size: 32px; color: #d9d9d9" />
            <p>暂无质量预警</p>
          </div>
          <div v-else class="notification-dropdown__list">
            <div class="dropdown-item quality-alert" v-if="qualityOverdueCount > 0" @click="goToQualityExceptions">
              <div class="dropdown-item__content">
                <div class="dropdown-item__title" style="color: var(--color-danger)">
                  <WarningOutlined style="margin-right: 6px" />已超时异常
                </div>
                <div class="dropdown-item__desc">{{ qualityOverdueCount }} 条异常已超时，请尽快处理</div>
              </div>
              <a-badge :count="qualityOverdueCount" :number-style="{ backgroundColor: 'var(--color-danger)' }" />
            </div>
            <div class="dropdown-item quality-alert" v-if="qualityRawPendingCount > 0" @click="goToQualityExceptions">
              <div class="dropdown-item__content">
                <div class="dropdown-item__title" style="color: var(--color-warning)">
                  <ClockCircleOutlined style="margin-right: 6px" />待处理异常
                </div>
                <div class="dropdown-item__desc">{{ qualityRawPendingCount }} 条异常等待处理</div>
              </div>
              <a-badge :count="qualityRawPendingCount" :number-style="{ backgroundColor: 'var(--color-warning)' }" />
            </div>
          </div>
          <div class="notification-dropdown__footer" @click="goToQualityExceptions">
            查看异常管理
          </div>
        </template>
      </div>
    </template>
  </a-dropdown>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { BellOutlined, WarningOutlined, ClockCircleOutlined } from '@ant-design/icons-vue'
import { getNotifications, getUnreadCount, markNotificationRead, markAllNotificationsRead } from '@/api/task'
import { getExceptionCountByStatus } from '@/api/quality'
import type { NotificationListDto } from '@/types/task'
import * as signalR from '@microsoft/signalr'

const router = useRouter()
const dropdownOpen = ref(false)
const loading = ref(false)
const recentList = ref<NotificationListDto[]>([])
const unreadCount = ref(0)
const activeTab = ref<'notification' | 'quality'>('notification')

// 质量预警数据
const qualityRawPendingCount = ref(0)
const qualityOverdueCount = ref(0)
const qualityPendingCount = computed(() => qualityRawPendingCount.value + qualityOverdueCount.value)
const totalBadgeCount = computed(() => unreadCount.value + qualityPendingCount.value)

let pollTimer: ReturnType<typeof setInterval> | null = null
let notificationConnection: signalR.HubConnection | null = null

// 相对时间格式化
function formatRelativeTime(time: string) {
  if (!time) return ''
  const now = Date.now()
  const target = new Date(time).getTime()
  const diff = now - target
  const seconds = Math.floor(diff / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)

  if (seconds < 60) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  if (hours < 24) return `${hours}小时前`
  if (days < 7) return `${days}天前`
  return time.replace('T', ' ').substring(0, 16)
}

async function loadUnreadCount() {
  try {
    const res = await getUnreadCount()
    unreadCount.value = res.total
  } catch {
    // ignore
  }
}

async function loadRecent() {
  loading.value = true
  try {
    const res = await getNotifications({ pageIndex: 1, pageSize: 5 })
    recentList.value = res.items
  } catch {
    // ignore
  } finally {
    loading.value = false
  }
}

async function handleClickItem(item: NotificationListDto) {
  dropdownOpen.value = false
  if (!item.isRead) {
    try {
      await markNotificationRead(item.id)
      item.isRead = true
      unreadCount.value = Math.max(0, unreadCount.value - 1)
    } catch {
      // ignore
    }
  }
  // 跳转到通知中心（或关联页面）
  const routeMap: Record<number, string> = {
    1: '/task/tasks',
    2: '/task/projects',
    3: '/task/goals',
  }
  const base = routeMap[item.relationType]
  if (base && item.relationId) {
    router.push(`${base}/${item.relationId}`)
  } else {
    router.push('/task/notifications')
  }
}

async function handleMarkAllRead() {
  try {
    await markAllNotificationsRead()
    recentList.value.forEach(n => (n.isRead = true))
    unreadCount.value = 0
    message.success('已全部标记为已读')
  } catch {
    message.error('操作失败')
  }
}

function goToCenter() {
  dropdownOpen.value = false
  router.push('/task/notifications')
}

// 质量预警
async function loadQualityAlerts() {
  try {
    const res = await getExceptionCountByStatus()
    if (res) {
      qualityRawPendingCount.value = res.pending || 0
      qualityOverdueCount.value = res.overdue || 0
    }
  } catch {
    // 静默处理，不影响导航栏
  }
}

function goToQualityExceptions() {
  dropdownOpen.value = false
  router.push('/quality/exceptions')
}

// SignalR 实时通知推送
async function setupSignalR() {
  try {
    notificationConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notification', {
        accessTokenFactory: () => localStorage.getItem('stotop_token') || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    notificationConnection.on('ReceiveNotification', (notification: any) => {
      // 更新未读数
      unreadCount.value += 1
      // 将新通知插入列表顶部
      recentList.value.unshift({
        id: notification.id,
        receiverId: 0,
        eventType: notification.eventType,
        title: notification.title,
        content: notification.content,
        relationType: notification.relationType,
        relationId: notification.relationId,
        isRead: false,
        pushedToDingTalk: false,
        createTime: notification.createTime
      })
      // 保持最多5条
      if (recentList.value.length > 5) {
        recentList.value = recentList.value.slice(0, 5)
      }
    })

    await notificationConnection.start()
  } catch (err) {
    console.error('SignalR 通知连接失败:', err)
  }
}

function teardownSignalR() {
  if (notificationConnection) {
    notificationConnection.stop().catch(() => {})
    notificationConnection = null
  }
}

onMounted(() => {
  loadUnreadCount()
  loadRecent()
  loadQualityAlerts()
  // 轮询未读数（每60秒），作为 SignalR 的兑底方案
  pollTimer = setInterval(() => {
    loadUnreadCount()
    loadQualityAlerts()
  }, 60000)
  // 接入 SignalR 实时推送
  setupSignalR()
})

onUnmounted(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
  teardownSignalR()
})
</script>

<style scoped lang="scss">
.notification-bell {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.3s;

  &:hover {
    background: rgba(0, 0, 0, 0.04);
  }

  .bell-icon {
    font-size: 16px;
    color: rgba(0, 0, 0, 0.65);
    transition: color 0.3s;
  }

  &:hover .bell-icon {
    color: var(--color-primary);
  }
}

.notification-dropdown {
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12);
  overflow: hidden;

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 16px;
    border-bottom: 1px solid #f0f0f0;
  }

  &__title {
    font-size: 15px;
    font-weight: 600;
    color: #303133;
  }

  &__tabs {
    display: flex;
    gap: 16px;
  }

  &__tab {
    font-size: 14px;
    color: #909399;
    cursor: pointer;
    padding-bottom: 2px;
    border-bottom: 2px solid transparent;
    transition: all 0.3s;
    display: inline-flex;
    align-items: center;

    &:hover {
      color: #303133;
    }

    &.active {
      color: #303133;
      font-weight: 600;
      border-bottom-color: var(--color-primary);
    }
  }

  &__action {
    font-size: 13px;
    color: var(--text-1);
    cursor: pointer;

    &:hover {
      color: var(--color-primary);
    }
  }

  &__empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 32px 0;
    color: #999;

    p {
      margin: 8px 0 0;
      font-size: 13px;
    }
  }

  &__list {
    max-height: 360px;
    overflow-y: auto;
  }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 10px 0;
    border-top: 1px solid #f0f0f0;
    font-size: 13px;
    color: var(--text-1);
    cursor: pointer;

    &:hover {
      background: #fafafa;
      color: var(--color-primary);
    }
  }
}

.dropdown-item {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 16px;
  cursor: pointer;
  position: relative;
  transition: background 0.2s;

  &:hover {
    background: #fafafa;
  }

  &.unread {
    background: #f0f7ff;

    &:hover {
      background: #e6f0fa;
    }
  }

  &.quality-alert {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  &__dot {
    position: absolute;
    left: 6px;
    top: 18px;
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--color-info);
  }

  &__content {
    flex: 1;
    min-width: 0;
    padding-left: 4px;
  }

  &__title {
    font-size: 13px;
    font-weight: 500;
    color: #303133;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__desc {
    font-size: 12px;
    color: #909399;
    margin-top: 2px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__time {
    font-size: 11px;
    color: #c0c4cc;
    margin-top: 4px;
  }
}
</style>
