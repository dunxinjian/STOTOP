<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useSecurityStore } from '@/stores/security'
import { useUserStore } from '@/stores/user'
import { verifyPassword } from '@/api/security'
import { pinyinInitial } from '@/utils/pinyin'

const router = useRouter()
const securityStore = useSecurityStore()
const userStore = useUserStore()

// 状态
const password = ref('')
const loading = ref(false)
const errorMsg = ref('')
const lockStartTime = ref(Date.now())
const elapsedSeconds = ref(0)
const passwordInput = ref<HTMLInputElement | null>(null)

let elapsedTimer: number | null = null
let autoExpireTimer: number | null = null

// 从安全配置计算自动退出的超时秒数
const autoExpireSeconds = computed(() => {
  const config = securityStore.securityConfig
  if (!config) return 30 * 60 // 默认30分钟
  return (config.idleLogoutMinutes - config.idleLockscreenMinutes) * 60
})

// 格式化已锁屏时间
const elapsedDisplay = computed(() => {
  const mins = Math.floor(elapsedSeconds.value / 60)
  const secs = elapsedSeconds.value % 60
  return `${mins} 分 ${secs.toString().padStart(2, '0')} 秒`
})

// 用户信息
const userName = computed(() => userStore.userInfo?.realName || userStore.userInfo?.username || '用户')
const userAvatar = computed(() => userStore.userInfo?.avatar || '')

async function handleUnlock() {
  if (!password.value.trim()) {
    errorMsg.value = '请输入密码'
    return
  }

  loading.value = true
  errorMsg.value = ''

  try {
    const result = await verifyPassword(password.value)
    if (result) {
      securityStore.unlock()
    } else {
      errorMsg.value = '密码错误，请重新输入'
      password.value = ''
      nextTick(() => passwordInput.value?.focus())
    }
  } catch {
    errorMsg.value = '验证失败，请重试'
    password.value = ''
    nextTick(() => passwordInput.value?.focus())
  } finally {
    loading.value = false
  }
}

async function handleSwitchAccount() {
  await userStore.logout()
  router.push('/login')
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !loading.value) {
    handleUnlock()
  }
}

function startTimers() {
  lockStartTime.value = Date.now()
  elapsedSeconds.value = 0

  // 每秒更新锁屏持续时间
  elapsedTimer = window.setInterval(() => {
    elapsedSeconds.value = Math.floor((Date.now() - lockStartTime.value) / 1000)
  }, 1000)

  // 超时自动退出
  autoExpireTimer = window.setTimeout(() => {
    securityStore.expireSession()
  }, autoExpireSeconds.value * 1000)
}

function stopTimers() {
  if (elapsedTimer !== null) {
    clearInterval(elapsedTimer)
    elapsedTimer = null
  }
  if (autoExpireTimer !== null) {
    clearTimeout(autoExpireTimer)
    autoExpireTimer = null
  }
}

onMounted(() => {
  startTimers()
  nextTick(() => passwordInput.value?.focus())
})

onUnmounted(() => {
  stopTimers()
})
</script>

<template>
  <div class="lock-screen">
    <div class="lock-screen__card">
      <!-- 用户头像 -->
      <div class="lock-screen__avatar">
        <img v-if="userAvatar" :src="userAvatar" alt="avatar" class="lock-screen__avatar-img" />
        <div v-else class="lock-screen__avatar-placeholder">
          {{ pinyinInitial(userName) || 'U' }}
        </div>
      </div>

      <!-- 用户名 -->
      <h2 class="lock-screen__name">{{ userName }}</h2>

      <!-- 锁屏时间 -->
      <p class="lock-screen__elapsed">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align: middle; margin-right: 4px;">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
        屏幕已锁定 {{ elapsedDisplay }}
      </p>

      <!-- 密码输入 -->
      <div class="lock-screen__input-wrap">
        <input
          ref="passwordInput"
          v-model="password"
          type="password"
          placeholder="请输入密码解锁"
          class="lock-screen__input"
          :disabled="loading"
          @keydown="handleKeydown"
        />
      </div>

      <!-- 错误提示 -->
      <p v-if="errorMsg" class="lock-screen__error">{{ errorMsg }}</p>

      <!-- 按钮 -->
      <div class="lock-screen__actions">
        <button
          class="lock-screen__btn lock-screen__btn--primary"
          :disabled="loading"
          @click="handleUnlock"
        >
          <span v-if="loading" class="lock-screen__spinner"></span>
          {{ loading ? '验证中...' : '解锁' }}
        </button>
        <button
          class="lock-screen__btn lock-screen__btn--ghost"
          :disabled="loading"
          @click="handleSwitchAccount"
        >
          切换账号
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.lock-screen {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
}

.lock-screen__card {
  width: 360px;
  max-width: 90vw;
  padding: 40px 32px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
  text-align: center;
}

.lock-screen__avatar {
  margin-bottom: 16px;
}

.lock-screen__avatar-img {
  width: 72px;
  height: 72px;
  border-radius: 50%;
  object-fit: cover;
  border: 3px solid #f0f0f0;
}

.lock-screen__avatar-placeholder {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 72px;
  height: 72px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  font-size: 28px;
  font-weight: 600;
}

.lock-screen__name {
  font-size: 20px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
  margin: 0 0 8px;
}

.lock-screen__elapsed {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  margin: 0 0 24px;
}

.lock-screen__input-wrap {
  margin-bottom: 12px;
}

.lock-screen__input {
  width: 100%;
  height: 40px;
  padding: 0 12px;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
}

.lock-screen__input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary-border);
}

.lock-screen__input:disabled {
  background: #f5f5f5;
  cursor: not-allowed;
}

.lock-screen__error {
  font-size: 13px;
  color: var(--color-danger);
  margin: 0 0 12px;
}

.lock-screen__actions {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 16px;
}

.lock-screen__btn {
  width: 100%;
  height: 40px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.lock-screen__btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.lock-screen__btn--primary {
  background: var(--color-primary);
  color: #fff;
}

.lock-screen__btn--primary:hover:not(:disabled) {
  background: var(--color-primary-hover);
}

.lock-screen__btn--ghost {
  background: transparent;
  color: rgba(0, 0, 0, 0.65);
  border: 1px solid #d9d9d9;
}

.lock-screen__btn--ghost:hover:not(:disabled) {
  color: var(--color-primary);
  border-color: var(--color-primary);
}

.lock-screen__spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 480px) {
  .lock-screen__card {
    padding: 32px 20px;
  }
}
</style>
