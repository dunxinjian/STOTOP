<template>
  <div class="module-placeholder">
    <PageHeader title="人资管理" />
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
  IdcardOutlined,
  ClockCircleOutlined,
  DollarOutlined,
  ApartmentOutlined,
} from '@ant-design/icons-vue'

const features = [
  { title: '员工花名册', desc: '全员档案统一管理', icon: IdcardOutlined },
  { title: '考勤管理', desc: '考勤记录和统计分析', icon: ClockCircleOutlined },
  { title: '薪资核算', desc: '自动化薪资计算和发放', icon: DollarOutlined },
  { title: '组织架构', desc: '部门和岗位体系管理', icon: ApartmentOutlined },
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
