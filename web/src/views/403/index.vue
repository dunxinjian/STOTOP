<template>
  <div class="forbidden-page">
    <a-result status="403" title="403" sub-title="抱歉，您没有权限访问此页面">
      <template #extra>
        <a-button type="primary" @click="router.push({ name: 'Layout' })">返回首页</a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<style scoped lang="scss">
.forbidden-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
}
</style>
