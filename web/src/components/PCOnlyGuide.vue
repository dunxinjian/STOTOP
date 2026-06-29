<script setup lang="ts">
import { message } from 'ant-design-vue'
import { LaptopOutlined, CopyOutlined } from '@ant-design/icons-vue'

async function copyLink() {
  try {
    await navigator.clipboard.writeText(window.location.href)
    message.success('链接已复制')
  } catch {
    message.error('复制失败，请手动复制地址栏链接')
  }
}
</script>

<template>
  <div class="pc-only-guide">
    <div class="pc-only-guide__icon">
      <LaptopOutlined />
    </div>
    <h3 class="pc-only-guide__title">该表单仅支持在电脑端查看</h3>
    <p class="pc-only-guide__desc">请使用电脑浏览器打开此页面</p>
    <a-button size="small" @click="copyLink">
      <template #icon><CopyOutlined /></template>
      复制链接
    </a-button>
  </div>
</template>

<style scoped>
.pc-only-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 320px;
  padding: 48px 24px;
  text-align: center;
}

.pc-only-guide__icon {
  font-size: 56px;
  color: #bfbfbf;
  margin-bottom: 24px;
}

.pc-only-guide__title {
  font-size: 17px;
  font-weight: 600;
  color: #595959;
  margin: 0 0 8px;
}

.pc-only-guide__desc {
  font-size: 14px;
  color: #8c8c8c;
  margin: 0 0 24px;
}
</style>
