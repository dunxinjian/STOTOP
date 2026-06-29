<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'
import { useThemeStore } from '@/stores/theme'
import { useEnterpriseInfoStore } from '@/stores/enterpriseInfo'
import { useSecurityStore } from '@/stores/security'
import { keyboardManager } from '@/utils/keyboardManager'
import { getToken } from '@/utils/auth'
import IdleWarningDialog from '@/components/security/IdleWarningDialog.vue'
import LockScreen from '@/components/security/LockScreen.vue'

const securityStore = useSecurityStore()

const themeStore = useThemeStore()
const enterpriseInfoStore = useEnterpriseInfoStore()

onMounted(() => {
  // 初始化全局快捷键管理器
  keyboardManager.init()
  // 加载主题配置
  themeStore.loadTheme().catch((error: unknown) => {
    console.warn('Theme loading failed, using defaults:', error)
  })
  // 加载企业信息（无需认证）
  enterpriseInfoStore.fetchEnterpriseInfo()
  // 页面刷新时若已有 Token，重新初始化空闲检测（防止刷新后超时无提示直接退出）
  if (getToken()) {
    securityStore.fetchSecurityConfig().then(() => {
      securityStore.initIdleDetection()
    })
  }
})

onUnmounted(() => {
  keyboardManager.destroy()
})

// 监听企业名称变化，更新页面标题
watch(() => enterpriseInfoStore.displayName, (name) => {
  if (name) {
    document.title = name
  }
}, { immediate: true })
</script>

<template>
  <!-- antdTheme 已启用 cssVar(prefix='sto') 与 hashed:false，组件样式以 CSS 变量输出，与 :root 设计令牌协同 -->
  <a-config-provider :locale="zhCN" :theme="themeStore.antdTheme" :component-size="'small'">
    <router-view />
    <IdleWarningDialog v-if="securityStore.idleState === 'warning'" />
    <LockScreen v-if="securityStore.idleState === 'locked'" />
  </a-config-provider>
</template>
