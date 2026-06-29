<template>
  <div class="hangfire-panel">
    <a-spin :spinning="loading" tip="正在加载任务调度面板...">
      <iframe
        ref="iframeRef"
        :src="hangfireUrl"
        class="hangfire-iframe"
        frameborder="0"
        @load="onIframeLoad"
        @error="onIframeError"
      />
    </a-spin>
    <div v-if="loadError" class="error-state">
      <a-empty description="Hangfire 面板加载失败">
        <a-button type="primary" @click="reload">重新加载</a-button>
      </a-empty>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

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
</script>

<style lang="scss" scoped>
.hangfire-panel {
  height: calc(100vh - 120px);
  position: relative;

  .hangfire-iframe {
    width: 100%;
    height: 100%;
    border: none;
  }

  .error-state {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
  }
}
</style>
