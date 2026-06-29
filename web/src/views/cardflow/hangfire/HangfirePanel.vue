<template>
  <div class="page-container">
    <PageHeader title="任务调度">
      <template #right>
        <a-button @click="openInNewWindow">
          <template #icon><ExportOutlined /></template>
          新窗口打开
        </a-button>
      </template>
    </PageHeader>

    <div class="card">
      <!-- 加载状态 -->
      <div v-if="loading" class="loading-state">
        <a-spin size="large" />
        <p class="loading-text">正在连接任务调度服务...</p>
      </div>

      <!-- 错误状态 -->
      <div v-else-if="loadError" class="error-state">
        <a-result
          status="error"
          title="连接失败"
          sub-title="无法连接到 Hangfire 任务调度面板，请检查后端服务是否运行正常"
        >
          <template #extra>
            <a-button type="primary" @click="reload">重新加载</a-button>
            <a-button @click="openInNewWindow">新窗口尝试</a-button>
          </template>
        </a-result>
      </div>

      <!-- iframe 嵌入 -->
      <iframe
        v-show="!loadError"
        ref="iframeRef"
        :src="hangfireUrl"
        class="hangfire-iframe"
        frameborder="0"
        @load="onIframeLoad"
        @error="onIframeError"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import { ExportOutlined } from '@ant-design/icons-vue'

const hangfireUrl = '/hangfire'
const loading = ref(true)
const loadError = ref(false)
const iframeRef = ref<HTMLIFrameElement>()

const onIframeLoad = () => {
  loading.value = false
  loadError.value = false
}

const onIframeError = () => {
  loading.value = false
  loadError.value = true
}

const reload = () => {
  loading.value = true
  loadError.value = false
  if (iframeRef.value) {
    iframeRef.value.src = hangfireUrl
  }
}

const openInNewWindow = () => {
  window.open(hangfireUrl, '_blank')
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.card {
  background: $bg-card;
  border-radius: $border-radius-lg;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.05);
  padding: 0;
  overflow: hidden;
  position: relative;
  height: calc(100vh - 130px);
  display: flex;
  flex-direction: column;
}

.hangfire-iframe {
  width: 100%;
  flex: 1;
  border: none;
  border-radius: $border-radius-lg;
  display: block;
}

.loading-state {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: $spacing-md;
  background: $bg-card;
  border-radius: $border-radius-lg;
  z-index: 1;
}

.loading-text {
  font-size: $font-size-base;
  color: $text-secondary;
  margin: 0;
}

.error-state {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: $bg-card;
  border-radius: $border-radius-lg;
  z-index: 1;
}
</style>
