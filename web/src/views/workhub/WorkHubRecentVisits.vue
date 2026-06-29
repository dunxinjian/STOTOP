<template>
  <div class="recent-visits-panel">
    <!-- 头部 -->
    <div class="panel-header">
      <span class="panel-header__title">快速导航</span>
    </div>

    <!-- 内容区 -->
    <div class="panel-body">
      <!-- 最近访问 -->
      <div class="section-label">最近访问</div>
      <div class="frequent-list" v-if="recommendationStore.recentPages.length > 0">
        <div
          v-for="item in recommendationStore.recentPages"
          :key="item.path"
          class="frequent-item"
          @click="router.push(item.path)"
        >
          <component :is="getPageIcon(item.path)" class="frequent-icon" />
          <span class="frequent-name">{{ getDisplayTitle(item.title) }}</span>
          <span class="frequent-meta">{{ formatRelativeTime(item.lastVisitTime) }}</span>
        </div>
      </div>
      <div class="pages-empty" v-else>
        <span>浏览页面后将自动记录</span>
      </div>

      <!-- 高频功能列表 -->
      <template v-if="recommendationStore.frequentPages.length > 0">
        <div class="section-label section-label--top">常用功能</div>
        <div class="frequent-list">
          <div
            v-for="item in recommendationStore.frequentPages.slice(0, 8)"
            :key="item.path"
            class="frequent-item"
            @click="router.push(item.path)"
          >
            <component :is="getPageIcon(item.path)" class="frequent-icon" />
            <span class="frequent-name">{{ getDisplayTitle(item.title) }}</span>
            <span class="visit-count">{{ item.visitCount }} 次</span>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useRecommendationStore } from '@/stores/recommendation'
import {
  LineChartOutlined, FileOutlined, AuditOutlined, TeamOutlined,
  BarChartOutlined, HomeOutlined, ShopOutlined, SettingOutlined,
} from '@ant-design/icons-vue'

const router = useRouter()
const recommendationStore = useRecommendationStore()

function getPageIcon(path: string) {
  if (path.includes('finance')) return LineChartOutlined
  if (path.includes('oa')) return AuditOutlined
  if (path.includes('hr')) return TeamOutlined
  if (path.includes('quality')) return BarChartOutlined
  if (path.includes('dashboard')) return HomeOutlined
  if (path.includes('shop') || path.includes('crm')) return ShopOutlined
  if (path.includes('setting')) return SettingOutlined
  return FileOutlined
}

function getDisplayTitle(title: string) {
  if (title === '数据质量中心') return '运单质量中心'
  if (title === '快速成本方案') return '快递成本方案'
  return title
}

function formatRelativeTime(timestamp: number): string {
  const diff = Date.now() - timestamp
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}小时前`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}天前`
  return new Date(timestamp).toLocaleDateString('zh-CN')
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.recent-visits-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-card);
  border-left: 0;
  overflow: hidden;
}

.panel-header {
  display: flex;
  align-items: center;
  height: 54px;
  padding: 0 20px;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
  background: var(--bg-card);
}

.panel-header__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-1);
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 18px 20px 24px;

  &::-webkit-scrollbar {
    width: 4px;
  }

  &::-webkit-scrollbar-thumb {
    background: rgba(0, 0, 0, 0.12);
    border-radius: 2px;
  }
}

.section-label {
  font-size: 12px;
  color: var(--text-2);
  font-weight: 500;
  margin-bottom: 12px;
  letter-spacing: 0;

  &--top {
    margin-top: 18px;
    padding-top: 18px;
    border-top: 1px solid var(--border);
  }
}

.pages-empty {
  font-size: 13px;
  color: $text-secondary;
  padding: 12px 0;
  text-align: center;
}

.frequent-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.frequent-item {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  min-height: 38px;
  padding: var(--space-sm8) var(--space-md12);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    background: var(--color-primary-light);
  }
}

.frequent-icon {
  font-size: 14px;
  color: var(--text-3);
  flex-shrink: 0;
}

.frequent-name {
  font-size: 13px;
  color: var(--text-1);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.visit-count {
  font-size: var(--font-xs);
  background: var(--bg-muted);
  color: var(--text-2);
  padding: 2px var(--space-sm8);
  border-radius: var(--radius-pill);
  font-weight: 500;
  flex-shrink: 0;
}

.frequent-meta {
  font-size: var(--font-xs);
  color: var(--text-3);
  flex-shrink: 0;
}
</style>
