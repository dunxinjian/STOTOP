<template>
  <div class="notification-center-page">
    <PageHeader title="通知中心">
      <template #right>
        <a-button @click="handleMarkAllRead" :disabled="unreadTotal === 0">
          <CheckOutlined /> 全部已读
        </a-button>
        <a-button @click="handleClearRead" :disabled="readCount === 0">
          <DeleteOutlined /> 清空已读
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false" class="section-card">
      <!-- Tab 分类 -->
      <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
        <a-tab-pane key="all" tab="全部" />
        <a-tab-pane key="unread">
          <template #tab>
            未读
            <a-badge v-if="unreadTotal > 0" :count="unreadTotal" :offset="[6, -2]" />
          </template>
        </a-tab-pane>
        <a-tab-pane key="read" tab="已读" />
      </a-tabs>

      <!-- 通知类型筛选 -->
      <div class="filter-bar">
        <a-select
          v-model:value="filterEventType"
          placeholder="通知类型"
          allow-clear
          style="width: 160px"
          @change="handleSearch"
        >
          <a-select-option v-for="opt in eventTypeOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </a-select-option>
        </a-select>
        <a-input
          v-model:value="keyword"
          placeholder="搜索通知标题/内容"
          allow-clear
          style="width: 220px; margin-left: 8px"
          @keyup.enter="handleSearch"
          @change="(e: any) => { if (!e.target.value) handleSearch() }"
        >
          <template #prefix><SearchOutlined /></template>
        </a-input>
      </div>

      <!-- 通知列表 -->
      <a-spin :spinning="loading">
        <div v-if="notifications.length === 0 && !loading" class="empty-state">
          <a-empty description="暂无通知">
            <template #image>
              <BellOutlined style="font-size: 48px; color: #d9d9d9" />
            </template>
          </a-empty>
        </div>

        <div v-else class="notification-list">
          <div
            v-for="item in notifications"
            :key="item.id"
            class="notification-item"
            :class="{ unread: !item.isRead }"
            @click="handleClickNotification(item)"
          >
            <div class="notification-item__dot" v-if="!item.isRead" />
            <div class="notification-item__icon">
              <component :is="getEventIcon(item.eventType)" :style="{ color: getEventColor(item.eventType), fontSize: '18px' }" />
            </div>
            <div class="notification-item__body">
              <div class="notification-item__title">{{ item.title }}</div>
              <div class="notification-item__desc">{{ item.content }}</div>
            </div>
            <div class="notification-item__time">{{ formatRelativeTime(item.createTime) }}</div>
            <div class="notification-item__status">
              <a-tag v-if="item.isRead" color="default">已读</a-tag>
              <a-tag v-else color="blue">未读</a-tag>
            </div>
          </div>
        </div>
      </a-spin>

      <!-- 分页 -->
      <div class="pagination-wrap" v-if="total > 0">
        <a-pagination
          v-model:current="pageIndex"
          v-model:pageSize="pageSize"
          :total="total"
          :showTotal="(t: number) => `共 ${t} 条`"
          show-size-changer
          show-quick-jumper
          @change="loadData"
        />
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  SearchOutlined, CheckOutlined, DeleteOutlined, BellOutlined,
  SendOutlined, ClockCircleOutlined, CommentOutlined,
  SyncOutlined, TrophyOutlined, FlagOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getNotifications, markNotificationRead, markAllNotificationsRead, getUnreadCount } from '@/api/task'
import type { NotificationListDto, NotificationPagedRequest } from '@/types/task'

const router = useRouter()
const loading = ref(false)
const notifications = ref<NotificationListDto[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(20)
const activeTab = ref('all')
const filterEventType = ref<number | undefined>(undefined)
const keyword = ref('')
const unreadTotal = ref(0)

// 通知事件类型选项
const eventTypeOptions = [
  { value: 1, label: '任务分配' },
  { value: 2, label: '任务到期' },
  { value: 3, label: '评论提及' },
  { value: 4, label: '进度更新' },
  { value: 5, label: '绩效通知' },
  { value: 6, label: '状态变更' },
]

// 已读通知计数
const readCount = computed(() => {
  return notifications.value.filter(n => n.isRead).length
})

// 事件类型图标映射
function getEventIcon(eventType: number) {
  const map: Record<number, any> = {
    1: SendOutlined,
    2: ClockCircleOutlined,
    3: CommentOutlined,
    4: SyncOutlined,
    5: TrophyOutlined,
    6: FlagOutlined,
  }
  return map[eventType] || BellOutlined
}

// 事件类型颜色映射
function getEventColor(eventType: number) {
  const map: Record<number, string> = {
    1: 'var(--color-info)',
    2: 'var(--color-warning)',
    3: 'var(--color-success)',
    4: 'var(--biz-points)',
    5: '#eb2f96',
    6: 'var(--color-info)',
  }
  return map[eventType] || '#999'
}

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
  if (days < 30) return `${Math.floor(days / 7)}周前`
  return time.replace('T', ' ').substring(0, 16)
}

// 关联实体路由映射
function getRelationRoute(relationType: number, relationId: number): string | null {
  const map: Record<number, string> = {
    1: `/task/tasks`,      // 任务
    2: `/task/projects`,   // 项目
    3: `/task/goals`,      // 目标
  }
  const base = map[relationType]
  if (base) return `${base}/${relationId}`
  return null
}

function handleSearch() {
  pageIndex.value = 1
  loadData()
}

function handleTabChange() {
  pageIndex.value = 1
  loadData()
}

async function loadData() {
  loading.value = true
  try {
    const params: NotificationPagedRequest = {
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      eventType: filterEventType.value ?? undefined,
    }
    if (activeTab.value === 'unread') params.isRead = false
    else if (activeTab.value === 'read') params.isRead = true

    const res = await getNotifications(params)
    notifications.value = res.items
    total.value = res.total
  } catch {
    message.error('加载通知列表失败')
  } finally {
    loading.value = false
  }
}

async function loadUnreadCount() {
  try {
    const res = await getUnreadCount()
    unreadTotal.value = res.total
  } catch {
    // ignore
  }
}

async function handleClickNotification(item: NotificationListDto) {
  // 标记已读
  if (!item.isRead) {
    try {
      await markNotificationRead(item.id)
      item.isRead = true
      unreadTotal.value = Math.max(0, unreadTotal.value - 1)
    } catch {
      // ignore
    }
  }
  // 跳转
  const route = getRelationRoute(item.relationType, item.relationId)
  if (route) {
    router.push(route)
  }
}

async function handleMarkAllRead() {
  try {
    await markAllNotificationsRead()
    notifications.value.forEach(n => (n.isRead = true))
    unreadTotal.value = 0
    message.success('已全部标记为已读')
  } catch {
    message.error('操作失败')
  }
}

function handleClearRead() {
  Modal.confirm({
    title: '确认清空',
    content: '确定要清空所有已读通知吗？',
    okText: '确定',
    cancelText: '取消',
    onOk: () => {
      // 前端移除已读条目（后端如需删除可扩展API）
      notifications.value = notifications.value.filter(n => !n.isRead)
      total.value = notifications.value.length
      message.success('已清空已读通知')
    },
  })
}

onMounted(() => {
  loadData()
  loadUnreadCount()
})
</script>

<style scoped lang="scss">
.notification-center-page {
  padding: 0 4px;
}

.section-card {
  border-radius: 8px;
}

.filter-bar {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

.empty-state {
  padding: 60px 0;
  text-align: center;
}

.notification-list {
  display: flex;
  flex-direction: column;
}

.notification-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid #f0f0f0;
  cursor: pointer;
  transition: background 0.2s;
  position: relative;

  &:last-child {
    border-bottom: none;
  }

  &:hover {
    background: #fafafa;
  }

  &.unread {
    background: #f0f7ff;

    &:hover {
      background: #e6f0fa;
    }
  }

  &__dot {
    position: absolute;
    left: 4px;
    top: 50%;
    transform: translateY(-50%);
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--color-info);
  }

  &__icon {
    flex-shrink: 0;
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    background: #f5f5f5;
  }

  &__body {
    flex: 1;
    min-width: 0;
  }

  &__title {
    font-size: 14px;
    font-weight: 500;
    color: #303133;
    margin-bottom: 4px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__desc {
    font-size: 13px;
    color: #909399;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__time {
    flex-shrink: 0;
    font-size: 12px;
    color: #c0c4cc;
    white-space: nowrap;
  }

  &__status {
    flex-shrink: 0;
  }
}

.pagination-wrap {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
