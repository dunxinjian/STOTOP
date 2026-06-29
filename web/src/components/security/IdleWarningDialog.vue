<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useSecurityStore } from '@/stores/security'

const securityStore = useSecurityStore()

// 从安全配置中计算倒计时总秒数
const totalSeconds = computed(() => {
  const config = securityStore.securityConfig
  if (!config) return 120
  return (config.idleLockscreenMinutes - config.idleWarningMinutes) * 60
})

const countdown = ref(totalSeconds.value)
let timer: number | null = null

function startCountdown() {
  countdown.value = totalSeconds.value
  timer = window.setInterval(() => {
    countdown.value--
    if (countdown.value <= 0) {
      stopCountdown()
    }
  }, 1000)
}

function stopCountdown() {
  if (timer !== null) {
    clearInterval(timer)
    timer = null
  }
}

function handleContinue() {
  stopCountdown()
  securityStore.resetIdle()
}

onMounted(() => {
  startCountdown()
})

onUnmounted(() => {
  stopCountdown()
})
</script>

<template>
  <a-modal
    :open="true"
    :closable="false"
    :maskClosable="false"
    :keyboard="false"
    :footer="null"
    centered
    width="420px"
  >
    <div class="idle-warning">
      <div class="idle-warning__icon">
        <svg viewBox="0 0 24 24" width="48" height="48" fill="none" stroke="var(--color-warning)" stroke-width="2">
          <path d="M12 2L2 22h20L12 2z" fill="var(--color-warning-light)" stroke="var(--color-warning)" />
          <line x1="12" y1="9" x2="12" y2="14" stroke="var(--color-warning)" stroke-linecap="round" />
          <circle cx="12" cy="17" r="0.5" fill="var(--color-warning)" stroke="none" />
        </svg>
      </div>
      <h3 class="idle-warning__title">会话即将过期</h3>
      <p class="idle-warning__message">
        由于您长时间未操作，系统将在
        <span class="idle-warning__countdown">{{ countdown }}</span>
        秒后锁定屏幕
      </p>
      <a-button type="primary" block size="large" @click="handleContinue">
        继续工作
      </a-button>
    </div>
  </a-modal>
</template>

<style scoped>
.idle-warning {
  text-align: center;
  padding: 16px 0;
}

.idle-warning__icon {
  margin-bottom: 16px;
}

.idle-warning__title {
  font-size: 18px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 12px;
}

.idle-warning__message {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.65);
  margin-bottom: 24px;
  line-height: 1.6;
}

.idle-warning__countdown {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-warning);
  padding: 0 4px;
}
</style>
