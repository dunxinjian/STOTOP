<script setup lang="ts">
/**
 * CardFlow 模块首页 / 入口
 *
 * 快捷入口卡片 + 最近活动列表
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  PlusCircleOutlined,
  UnorderedListOutlined,
  SettingOutlined,
  BarChartOutlined,
  BellOutlined,
  ClockCircleOutlined,
  RightOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { useCardFlowStore } from '@/stores/cardflow'
import { getCards } from '@/api/cardflow'
import type { CardListDto } from '@/types/cardflow'

const router = useRouter()
const cardFlowStore = useCardFlowStore()

// ===== 快捷入口 =====
const shortcuts = [
  {
    title: '发起流程',
    description: '创建新的审批卡片',
    icon: PlusCircleOutlined,
    color: 'var(--color-primary)',
    path: '/cardflow/create',
  },
  {
    title: '我的待办',
    description: '处理待审批的卡片',
    icon: ClockCircleOutlined,
    color: 'var(--color-warning)',
    path: '/workhub',
  },
  {
    title: '流程管理',
    description: '管理流程定义与版本',
    icon: SettingOutlined,
    color: 'var(--biz-waybill)',
    path: '/cardflow/definitions',
  },
  {
    title: '监控看板',
    description: '查看流程运行状况',
    icon: BarChartOutlined,
    color: 'var(--color-success)',
    path: '/cardflow/monitor',
  },
]

// ===== 最近活动 =====
const recentCards = ref<CardListDto[]>([])
const loadingRecent = ref(false)

const statusConfig: Record<string, { text: string; color: string }> = {
  draft: { text: '草稿', color: 'default' },
  active: { text: '审批中', color: 'processing' },
  completed: { text: '已完成', color: 'success' },
  returned: { text: '已退回', color: 'error' },
  voided: { text: '已作废', color: 'default' },
}

async function loadRecentCards() {
  loadingRecent.value = true
  try {
    const res = await getCards({ page: 1, pageSize: 10 })
    recentCards.value = res.items
  } catch {
    message.error('加载最近活动失败')
  } finally {
    loadingRecent.value = false
  }
}

function handleShortcut(path: string) {
  router.push(path)
}

function handleCardClick(card: CardListDto) {
  router.push(`/cardflow/cards/${card.id}`)
}

function formatTime(val: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(() => {
  loadRecentCards()
  cardFlowStore.loadTodoCount()
})
</script>

<template>
  <div class="page-container">
    <PageHeader title="卡片流程" />

    <!-- 快捷入口 -->
    <a-row :gutter="16" style="margin-bottom: 24px;">
      <a-col :span="6" v-for="item in shortcuts" :key="item.title">
        <a-card hoverable class="shortcut-card" @click="handleShortcut(item.path)">
          <div class="shortcut-content">
            <div class="shortcut-icon" :style="{ background: item.color + '15', color: item.color }">
              <component :is="item.icon" style="font-size: 24px;" />
            </div>
            <div class="shortcut-info">
              <div class="shortcut-title">{{ item.title }}</div>
              <div class="shortcut-desc">{{ item.description }}</div>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 待办概览 -->
    <a-row :gutter="16" style="margin-bottom: 24px;">
      <a-col :span="8">
        <a-card size="small">
          <a-statistic title="待处理" :value="cardFlowStore.todoCount.todo" :value-style="{ color: 'var(--color-info)' }" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card size="small">
          <a-statistic title="已发起" :value="cardFlowStore.todoCount.initiated" :value-style="{ color: 'var(--biz-waybill)' }" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card size="small">
          <a-statistic title="抄送我的" :value="cardFlowStore.todoCount.cc" :value-style="{ color: 'var(--color-warning)' }" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 最近活动 -->
    <a-card title="最近活动">
      <template #extra>
        <a-button type="link" size="small" @click="router.push('/cardflow/monitor')">
          查看全部 <RightOutlined />
        </a-button>
      </template>
      <a-spin :spinning="loadingRecent">
        <a-list
          :data-source="recentCards"
          item-layout="horizontal"
          :locale="{ emptyText: '暂无最近活动' }"
        >
          <template #renderItem="{ item }">
            <a-list-item class="activity-item" @click="handleCardClick(item)">
              <a-list-item-meta>
                <template #title>
                  <div class="activity-title">
                    <span>{{ item.title || item.cardNumber || '未命名卡片' }}</span>
                    <a-tag :color="statusConfig[item.status]?.color || 'default'" size="small">
                      {{ statusConfig[item.status]?.text || item.status }}
                    </a-tag>
                  </div>
                </template>
                <template #description>
                  <span>{{ item.flowName }} · {{ item.initiatorName }} · {{ formatTime(item.createdTime) }}</span>
                </template>
              </a-list-item-meta>
              <template #extra>
                <RightOutlined style="color: var(--text-3);" />
              </template>
            </a-list-item>
          </template>
        </a-list>
      </a-spin>
    </a-card>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 16px;
}

.shortcut-card {
  cursor: pointer;
  transition: all 0.2s;
  &:hover {
    transform: translateY(-2px);
    box-shadow: var(--shadow-md);
  }
}

.shortcut-content {
  display: flex;
  align-items: center;
  gap: 12px;
}

.shortcut-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.shortcut-info {
  .shortcut-title {
    font-size: 15px;
    font-weight: 600;
    color: var(--text-1);
    margin-bottom: 2px;
  }

  .shortcut-desc {
    font-size: 12px;
    color: var(--text-3);
  }
}

.activity-item {
  cursor: pointer;
  transition: background 0.2s;
  &:hover {
    background: var(--bg-muted);
  }
}

.activity-title {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
