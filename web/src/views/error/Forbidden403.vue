<template>
  <div class="forbidden-page">
    <div class="forbidden-content">
      <div class="forbidden-icon-wrapper">
        <LockOutlined class="forbidden-icon" />
      </div>
      <h1 class="forbidden-title">权限不足</h1>
      <p class="forbidden-desc">您没有权限访问此页面。如需访问请联系系统管理员。</p>
      <div class="forbidden-actions">
        <a-button size="large" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
          返回上一页
        </a-button>
        <a-button size="large" type="primary" @click="goHome">
          <template #icon><HomeOutlined /></template>
          回到首页
        </a-button>
      </div>
      <p class="forbidden-hint">如有疑问，请联系管理员</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { LockOutlined, ArrowLeftOutlined, HomeOutlined } from '@ant-design/icons-vue'

const router = useRouter()

function goBack() {
  router.back()
}

function goHome() {
  router.push('/workhub')
}
</script>

<style scoped lang="scss">
.forbidden-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #f5f7fa 0%, #e4e9f0 100%);
  padding: 24px;
}

.forbidden-content {
  text-align: center;
  max-width: 480px;
  padding: 60px 40px;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 8px 32px rgba(0, 21, 41, 0.08);
}

.forbidden-icon-wrapper {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  background: linear-gradient(135deg, #fff1f0 0%, #ffccc7 100%);
  margin-bottom: 28px;
}

.forbidden-icon {
  font-size: 48px;
  color: #cf1322;
}

.forbidden-title {
  font-size: 24px;
  font-weight: 600;
  color: #333;
  margin-bottom: 12px;
}

.forbidden-desc {
  font-size: 14px;
  color: #999;
  line-height: 1.6;
  margin-bottom: 32px;
}

.forbidden-actions {
  display: flex;
  justify-content: center;
  gap: 16px;
  margin-bottom: 32px;
}

.forbidden-hint {
  font-size: 12px;
  color: #bbb;
  margin: 0;
}
</style>
