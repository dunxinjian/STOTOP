<template>
  <div class="module-placeholder">
    <PageHeader title="会务管理" />
    <div class="placeholder-content">
      <div class="placeholder-hero">
        <BuildOutlined class="hero-icon" />
        <h2>功能建设中</h2>
        <p class="hero-desc">该模块正在紧锣密鼓地开发中，敬请期待</p>
      </div>
      <div class="feature-grid">
        <div v-for="feature in features" :key="feature.title" class="feature-card">
          <component :is="feature.icon" class="feature-icon" />
          <h4>{{ feature.title }}</h4>
          <p>{{ feature.desc }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@/components/PageHeader.vue'
import {
  BuildOutlined,
  CalendarOutlined,
  TeamOutlined,
  TableOutlined,
  CarOutlined,
} from '@ant-design/icons-vue'

const features = [
  { title: '活动管理', desc: '创建和管理各类活动', icon: CalendarOutlined },
  { title: '参会人员', desc: '参会人员登记和分组', icon: TeamOutlined },
  { title: '桌次安排', desc: '智能桌次和座位编排', icon: TableOutlined },
  { title: '接送管理', desc: '车辆调度和乘客接送', icon: CarOutlined },
]
</script>

<style scoped>
.module-placeholder {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0 16px 16px;
  background: var(--bg-page);
}
.placeholder-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
}
.placeholder-hero {
  text-align: center;
  margin-bottom: 48px;
}
.hero-icon {
  font-size: 64px;
  color: #bfbfbf;
  margin-bottom: 16px;
}
.placeholder-hero h2 {
  font-size: 24px;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 8px;
}
.hero-desc {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.45);
}
.feature-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  max-width: 900px;
  width: 100%;
}
.feature-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 24px 16px;
  text-align: center;
  transition: all 0.3s;
}
.feature-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  transform: translateY(-2px);
}
.feature-icon {
  font-size: 28px;
  color: var(--color-info);
  margin-bottom: 12px;
}
.feature-card h4 {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 4px;
}
.feature-card p {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  margin: 0;
}
</style>
