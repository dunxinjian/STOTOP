<template>
  <div class="login-page">
    <!-- 物流线性纹理（低透明、稀疏、克制） -->
    <div class="login-texture" aria-hidden="true">
      <svg class="login-map" viewBox="0 0 1440 800" preserveAspectRatio="xMidYMid slice">
        <defs>
          <pattern id="map-fine" width="48" height="48" patternUnits="userSpaceOnUse">
            <path d="M48 0 V48 M0 48 H48" fill="none" stroke="rgba(255,255,255,0.025)" stroke-width="1"/>
          </pattern>
          <pattern id="map-block" width="192" height="192" patternUnits="userSpaceOnUse">
            <path d="M192 0 V192 M0 192 H192" fill="none" stroke="rgba(255,255,255,0.05)" stroke-width="1"/>
          </pattern>
        </defs>
        <rect width="1440" height="800" fill="url(#map-fine)"/>
        <rect width="1440" height="800" fill="url(#map-block)"/>
        <!-- 主干道 -->
        <path d="M-80 250 L1520 590" fill="none" stroke="rgba(255,255,255,0.06)" stroke-width="2"/>
        <path d="M300 -60 L840 860" fill="none" stroke="rgba(255,255,255,0.05)" stroke-width="2"/>
        <path d="M1200 -60 L600 860" fill="none" stroke="rgba(255,255,255,0.035)" stroke-width="1.5"/>
        <!-- 环路 -->
        <path d="M100 120 C 520 50, 1000 120, 1340 370" fill="none" stroke="rgba(255,255,255,0.04)" stroke-width="1.5"/>
        <!-- 环岛 + 街区 -->
        <circle cx="840" cy="430" r="26" fill="none" stroke="rgba(255,255,255,0.05)" stroke-width="1.5"/>
        <rect x="150" y="170" width="120" height="92" fill="none" stroke="rgba(255,255,255,0.03)" stroke-width="1"/>
        <rect x="1160" y="520" width="150" height="104" fill="none" stroke="rgba(255,255,255,0.03)" stroke-width="1"/>
        <!-- 投递路线（高亮 A→B） -->
        <path d="M150 660 C 430 540, 560 720, 820 560 S 1250 430, 1330 210" fill="none" stroke="rgba(255,255,255,0.13)" stroke-width="1.5" stroke-dasharray="1 9" stroke-linecap="round"/>
        <circle cx="150" cy="660" r="5" fill="none" stroke="rgba(255,255,255,0.40)" stroke-width="2"/>
        <circle cx="820" cy="560" r="3.5" fill="rgba(255,255,255,0.22)"/>
        <path d="M1330 196 c -7 0 -12 5 -12 12 c 0 9 12 22 12 22 s 12 -13 12 -22 c 0 -7 -5 -12 -12 -12 z" fill="none" stroke="rgba(255,255,255,0.34)" stroke-width="1.5"/>
      </svg>
    </div>

    <div class="login-center">
      <!-- 品牌锁号 -->
      <div class="brand-lockup">
        <div class="brand-row">
          <img
            v-if="enterpriseInfoStore.hasLogo"
            :src="enterpriseInfoStore.logoUrl"
            :alt="enterpriseInfoStore.displayName"
            class="brand-logo-img"
          />
          <template v-else>
            <span class="brand-mark">{{ brandInitial }}</span>
            <span class="brand-name">{{ enterpriseInfoStore.displayName }}</span>
          </template>
        </div>
        <div class="brand-tagline">把快递物流、仓储作业与经营数据放进同一个工作现场</div>
      </div>

      <!-- 登录卡 -->
      <div class="login-card">
        <!-- 登录态 -->
        <template v-if="!orgSelectVisible">
          <div class="form-head">
            <h2 class="form-title">登录</h2>
            <p class="form-subtitle">使用企业账号或钉钉进入工作台</p>
          </div>

          <a-alert
            v-if="!dbConfigured"
            message="系统主数据库未配置，请联系管理员进行配置"
            type="warning"
            :closable="false"
            show-icon
            class="db-alert"
          />

          <!-- 登录方式 -->
          <div class="method-tabs" role="tablist">
            <button
              type="button"
              class="method-tab"
              :class="{ active: loginMethod === 'password' }"
              @click="loginMethod = 'password'"
            >账号密码</button>
            <button
              type="button"
              class="method-tab"
              :class="{ active: loginMethod === 'dingtalk' }"
              @click="loginMethod = 'dingtalk'"
            >钉钉扫码</button>
          </div>

          <!-- 账号密码 -->
          <a-form
            v-if="loginMethod === 'password'"
            ref="formRef"
            :model="form"
            :rules="rules"
            class="login-form"
            size="large"
            @keyup.enter="handleLogin"
          >
            <a-form-item name="username">
              <a-input
                v-model:value="form.username"
                placeholder="账号 / 手机号"
                autocomplete="username"
                aria-label="账号"
                allow-clear
                :disabled="loading"
              >
                <template #prefix><UserOutlined /></template>
              </a-input>
            </a-form-item>

            <a-form-item name="password">
              <a-input-password
                v-model:value="form.password"
                placeholder="密码"
                autocomplete="current-password"
                aria-label="密码"
                :disabled="loading"
              >
                <template #prefix><LockOutlined /></template>
              </a-input-password>
            </a-form-item>

            <div class="remember-row">
              <a-checkbox v-model:checked="rememberAccount" :disabled="loading">记住账号</a-checkbox>
              <a class="forgot-password" @click="handleForgotPassword">忘记密码？</a>
            </div>

            <a-button
              type="primary"
              class="login-btn"
              :loading="loading"
              :disabled="!dbConfigured || loading"
              html-type="button"
              @click="handleLogin"
            >
              {{ loading ? '登录中...' : '登录' }}
            </a-button>
          </a-form>

          <!-- 钉钉扫码 -->
          <div v-else class="dingtalk-panel">
            <p class="dingtalk-hint">使用钉钉扫码或授权进入</p>
            <a-button
              class="dingtalk-btn"
              :loading="dingtalkCallbackLoading"
              @click="handleDingtalkLogin"
            >
              <template v-if="!dingtalkCallbackLoading">
                <svg viewBox="0 0 1024 1024" width="1em" height="1em" class="dingtalk-icon"><path d="M512 64C264.6 64 64 264.6 64 512s200.6 448 448 448 448-200.6 448-448S759.4 64 512 64zm227 385.3c-1 4.2-3.5 10.4-7 17.8h.1l-.4.7c-20.3 43.1-73.1 127.7-73.1 127.7s-.1-.2-.3-.5l-15.5 26.8h74.5L575.1 810l32.3-128h-58.6l20.4-84.7c-16.5 3.9-35.9 9.4-59 16.8 0 0-31.2 18.2-89.9-35 0 0-39.6-34.7-16.6-43.4 9.8-3.7 47.4-8.4 77-12.3 40-5.4 64.6-8.2 64.6-8.2S422 517 392.7 512.5c-29.3-4.6-65.3-15.8-84-44.9 0 0-34.1-37.8 12.4-29.4 0 0 63.1 12.3 107.5 3.7 44.4-8.6 72.1-26.3 72.1-26.3s-45.7 2.5-92.9-8.6c-47.2-11.1-75.6-39.8-75.6-39.8s-35.7-31.3-14.2-29.4c21.5 1.9 63.1 16.8 100.2 18.4 37.1 1.5 79.6-7.6 79.6-7.6s-68.5-21.2-95.4-39.8c-26.8-18.6-48.6-49.4-48.6-49.4s-31.3-41.5 13-24.5c0 0 78.4 38 131.2 41.5 18.3 1.2 30.4-4.2 39.2-9.5z" fill="currentColor"/></svg>
                钉钉扫码登录
              </template>
              <template v-else>正在处理钉钉登录...</template>
            </a-button>
          </div>

          <div class="form-foot-note">
            <InfoCircleOutlined class="foot-note-icon" />
            多组织账号将在登录后选择组织与账套
          </div>
        </template>

        <!-- 选择组织态（多组织登录后） -->
        <div v-else class="org-select">
          <div class="org-select-head">
            <h2 class="form-title">选择组织</h2>
            <p class="form-subtitle">你在多个组织任职，选择一个进入</p>
          </div>
          <div class="org-list">
            <button
              v-for="org in orgList"
              :key="org.orgId"
              type="button"
              class="org-item"
              :class="{ 'is-primary': org.isPrimaryOrg === 1 }"
              @click="handleOrgSelected(org.orgId)"
            >
              <span class="org-icon"><BankOutlined /></span>
              <span class="org-main">
                <span class="org-name-row">
                  <span class="org-name">{{ org.orgName }}</span>
                  <span v-if="org.isPrimaryOrg === 1" class="org-badge">主组织</span>
                </span>
                <span v-if="orgMeta(org)" class="org-meta">{{ orgMeta(org) }}</span>
              </span>
              <ArrowRightOutlined class="org-arrow" />
            </button>
          </div>
        </div>

        <div v-if="loading" class="login-overlay">
          <a-spin size="large" />
          <p class="login-overlay-text">正在验证...</p>
        </div>
      </div>

      <div class="brand-footer">{{ enterpriseInfoStore.name }} Enterprise System</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import {
  UserOutlined, LockOutlined, BankOutlined, ArrowRightOutlined, InfoCircleOutlined,
} from '@ant-design/icons-vue'
import { useUserStore } from '@/stores/user'
import { useOrgContextStore } from '@/stores/orgContext'
import { useEnterpriseInfoStore } from '@/stores/enterpriseInfo'
import { useSecurityStore } from '@/stores/security'
import { checkDbConnectionStatus } from '@/api/system'
import { getDingtalkConfig } from '@/api/auth'
import type { UserOrganizationDto } from '@/types/organization'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const orgContextStore = useOrgContextStore()
const enterpriseInfoStore = useEnterpriseInfoStore()
const securityStore = useSecurityStore()

type LoginNextAction =
  | { type: 'redirect'; redirect: string }
  | { type: 'org-selection' }

const formRef = ref<FormInstance>()
const loading = ref(false)
const rememberAccount = ref(false)
const dbConfigured = ref(true)
const orgSelectVisible = ref(false)
const orgList = ref<UserOrganizationDto[]>([])
const loginMethod = ref<'password' | 'dingtalk'>('password')
const dingtalkEnabled = ref(false)
const dingtalkCallbackLoading = ref(false)
const dingtalkAppKey = ref('')
const dingtalkRedirectUri = ref('')

const form = reactive({
  username: 'admin',
  password: 'admin123',
})

/** 品牌首字（无 logo 时显示在橙色标记块内） */
const brandInitial = computed(() => (enterpriseInfoStore.displayName || 'S').charAt(0))

const REMEMBER_ACCOUNT_KEY = 'stotop_remember_account'

onMounted(async () => {
  const savedAccount = localStorage.getItem(REMEMBER_ACCOUNT_KEY)
  if (savedAccount) {
    form.username = savedAccount
    rememberAccount.value = true
  }

  // 非阻塞加载企业信息，API返回后自动更新显示
  enterpriseInfoStore.fetchEnterpriseInfo()

  const [, dingtalkConfig] = await Promise.allSettled([
    checkDbConnectionStatus().then((res: any) => {
      dbConfigured.value = res?.hasSystemConnection ?? true
    }).catch(() => {
      dbConfigured.value = true
    }),
    getDingtalkConfig().then(cfg => {
      dingtalkEnabled.value = cfg.enabled
      dingtalkAppKey.value = cfg.appKey || ''
      dingtalkRedirectUri.value = cfg.redirectUri || window.location.origin + '/login'
    }).catch(() => {
      dingtalkEnabled.value = false
    }),
  ])
  void dingtalkConfig

  const authCode = route.query.authCode as string | undefined
  if (authCode) {
    const savedState = sessionStorage.getItem('dingtalk_oauth_state')
    const returnedState = route.query.state as string | undefined
    if (savedState && returnedState && savedState !== returnedState) {
      message.error('钉钉登录失败：state 校验不通过，请重试')
      router.replace({ path: '/login' })
      return
    }
    sessionStorage.removeItem('dingtalk_oauth_state')

    loginMethod.value = 'dingtalk'
    dingtalkCallbackLoading.value = true
    try {
      await userStore.dingtalkLogin(authCode)
      await router.replace({ path: '/login' })
      await runPostLoginTransition()
    } catch {
      message.error('钉钉登录失败，请重试')
      router.replace({ path: '/login' })
    } finally {
      dingtalkCallbackLoading.value = false
    }
  }
})

const rules: Record<string, Rule[]> = {
  username: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码不少于6位', trigger: 'blur' },
  ],
}

function getRedirectTarget() {
  return (route.query.redirect as string) || '/'
}

/** 选组织行的元信息：组织类型 · 岗位 */
function orgMeta(org: UserOrganizationDto): string {
  return [org.orgType, org.position].filter(Boolean).join(' · ')
}

async function loadOrgsAndRedirect(): Promise<LoginNextAction> {
  await orgContextStore.fetchOrganizations()
  const orgs = orgContextStore.organizations
  const redirect = getRedirectTarget()

  if (orgs.length === 0) {
    return { type: 'redirect', redirect }
  }

  // 单组织：直接切换并进入主页面
  if (orgs.length === 1) {
    await orgContextStore.doSwitchOrganization(orgs[0].orgId)
    return { type: 'redirect', redirect }
  }

  // 多组织：一律进入「选择组织」屏（按组织数量判定，不再按是否有主组织自动选择）
  orgList.value = orgs
  orgSelectVisible.value = true
  return { type: 'org-selection' }
}

async function runPostLoginTransition() {
  loading.value = true
  try {
    const nextAction = await loadOrgsAndRedirect()
    if (nextAction.type === 'redirect') {
      // 登录成功后启动空闲检测（进入主界面前初始化）
      await securityStore.fetchSecurityConfig()
      securityStore.initIdleDetection()
      await router.push(nextAction.redirect)
      return
    }
    // org-selection：选择组织屏由 orgSelectVisible 触发，留在本页
  } finally {
    loading.value = false
  }
}

async function handleLogin() {
  if (!formRef.value || loading.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  loading.value = true
  try {
    await userStore.login({ account: form.username, password: form.password })
    if (rememberAccount.value) {
      localStorage.setItem(REMEMBER_ACCOUNT_KEY, form.username)
    } else {
      localStorage.removeItem(REMEMBER_ACCOUNT_KEY)
    }

    await runPostLoginTransition()
  } catch {
    // 错误已在 request.ts 中处理
  } finally {
    loading.value = false
  }
}

async function handleOrgSelected(orgId: number) {
  loading.value = true
  try {
    const data = await orgContextStore.doSwitchOrganization(orgId)
    if (data) {
      await securityStore.fetchSecurityConfig()
      securityStore.initIdleDetection()
      await router.push(getRedirectTarget())
    } else {
      message.error('组织切换失败，请重试')
    }
  } catch {
    message.error('登录过程出错，请重试')
  } finally {
    loading.value = false
    orgSelectVisible.value = false
  }
}

function handleForgotPassword() {
  message.info('请联系系统管理员重置密码')
}

async function handleDingtalkLogin() {
  if (!dingtalkEnabled.value) {
    message.warning('钉钉登录尚未配置，请联系管理员')
    return
  }
  if (!dingtalkAppKey.value) {
    message.warning('钉钉登录配置未就绪，请稍后重试')
    return
  }
  const state = Math.random().toString(36).substring(2) + Date.now().toString(36)
  sessionStorage.setItem('dingtalk_oauth_state', state)

  const redirectUri = dingtalkRedirectUri.value || window.location.origin + '/login'
  const authUrl =
    `https://login.dingtalk.com/oauth2/auth` +
    `?client_id=${encodeURIComponent(dingtalkAppKey.value)}` +
    `&response_type=code` +
    `&scope=openid` +
    `&redirect_uri=${encodeURIComponent(redirectUri)}` +
    `&state=${encodeURIComponent(state)}` +
    `&prompt=consent`
  window.location.href = authUrl
}
</script>

<style scoped lang="scss">
.login-page {
  position: relative;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  padding: 24px;
  background: var(--login-ink);
}

// —— 物流线性纹理层
.login-texture {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.login-map {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

// —— 居中列
.login-center {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
}

// —— 品牌锁号
.brand-lockup {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 9px;
  margin-bottom: 20px;
}

.brand-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.brand-mark {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-lg);
  background: var(--color-primary);
  color: var(--text-on-accent);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 500;
}

.brand-logo-img {
  max-height: 40px;
  max-width: 220px;
  object-fit: contain;
}

.brand-name {
  font-size: 18px;
  font-weight: 500;
  letter-spacing: 0.5px;
  color: rgba(255, 255, 255, 0.95);
}

.brand-tagline {
  font-size: 12px;
  color: rgba(255, 255, 255, 0.5);
}

// —— 登录卡（浅色浮起）
.login-card {
  position: relative;
  width: 360px;
  max-width: 90vw;
  padding: 26px 30px;
  border-radius: 12px;
  background: var(--bg-card);
  border: 1px solid rgba(255, 255, 255, 0.07);
  box-shadow: 0 10px 34px rgba(0, 0, 0, 0.30);
}

.form-head {
  margin-bottom: 18px;
}

.form-title {
  font-size: 18px;
  font-weight: 500;
  color: var(--text-1);
  margin: 0;
}

.form-subtitle {
  font-size: 12.5px;
  color: var(--text-3);
  margin: 6px 0 0;
}

.db-alert {
  margin-bottom: 16px;
}

// 登录方式 tab
.method-tabs {
  display: flex;
  gap: 24px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 18px;
}

.method-tab {
  background: transparent;
  border: none;
  padding: 0 0 9px;
  margin-bottom: -1px;
  font-size: 14px;
  color: var(--text-2);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: color 0.15s ease, border-color 0.15s ease;

  &:hover {
    color: var(--text-1);
  }

  &.active {
    color: var(--color-primary);
    font-weight: 500;
    border-bottom-color: var(--color-primary);
  }
}

.login-form {
  :deep(.ant-form-item) {
    margin-bottom: 14px;
  }

  :deep(.ant-input-affix-wrapper) {
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    transition: border-color 0.2s ease, box-shadow 0.2s ease;
  }

  :deep(.ant-input-affix-wrapper:focus),
  :deep(.ant-input-affix-wrapper-focused) {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px var(--color-primary-border);
  }
}

.remember-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 18px;
  font-size: 13px;
}

.forgot-password {
  color: var(--color-primary);
  cursor: pointer;
}

.login-btn {
  width: 100%;
  height: 40px;
  font-size: 14px;
  font-weight: 500;
  background: var(--color-primary);
  border: none;
  border-radius: var(--radius-md);
  letter-spacing: 1px;

  &:hover,
  &:focus {
    background: var(--color-primary-hover);
  }
}

// 钉钉面板
.dingtalk-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 14px;
  padding: 16px 0 8px;
}

.dingtalk-hint {
  font-size: 13px;
  color: var(--text-3);
  margin: 0;
}

.dingtalk-btn {
  height: 38px;
  border-radius: var(--radius-md);
}

.dingtalk-icon {
  vertical-align: -0.125em;
  margin-right: 6px;
}

.form-foot-note {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 18px;
  font-size: 11px;
  color: var(--text-3);
  justify-content: center;
}

.foot-note-icon {
  font-size: 13px;
}

// —— 选择组织态
.org-select-head {
  margin-bottom: 16px;
}

.org-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.org-item {
  display: flex;
  align-items: center;
  gap: 11px;
  padding: 11px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  background: transparent;
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s ease, background 0.15s ease;

  &:hover {
    border-color: var(--color-primary);
    background: var(--color-primary-light);
  }

  &.is-primary {
    border-color: var(--color-primary);
  }
}

.org-icon {
  width: 34px;
  height: 34px;
  border-radius: var(--radius-lg);
  background: var(--bg-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  color: var(--text-2);
  flex-shrink: 0;
}

.org-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
}

.org-name-row {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.org-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.org-badge {
  flex-shrink: 0;
  font-size: 10px;
  color: var(--color-primary);
  background: var(--color-primary-light);
  border-radius: var(--radius-sm);
  padding: 1px 6px;
}

.org-meta {
  font-size: 11.5px;
  color: var(--text-3);
}

.org-arrow {
  font-size: 15px;
  color: var(--text-3);
  flex-shrink: 0;
}

// —— 品牌署名
.brand-footer {
  margin-top: 20px;
  font-size: 11px;
  letter-spacing: 1px;
  color: rgba(255, 255, 255, 0.4);
}

// —— 加载遮罩
.login-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  background: color-mix(in srgb, var(--bg-card) 82%, transparent);
  border-radius: 12px;
  z-index: 3;

  :deep(.ant-spin-dot-item) {
    background-color: var(--color-primary);
  }
}

.login-overlay-text {
  margin: 0;
  color: var(--text-1);
  font-size: 14px;
  font-weight: 500;
}
</style>

<style lang="scss">
// 消除登录输入框内部 input 边框与外层 wrapper 重复
.login-card .ant-input-affix-wrapper .ant-input,
.login-card .ant-input-affix-wrapper .ant-input:focus {
  border: none !important;
  box-shadow: none !important;
  outline: none !important;
}

.login-card .login-btn.ant-btn-primary {
  background: var(--color-primary) !important;
  border: none;

  &:hover,
  &:focus {
    background: var(--color-primary-hover) !important;
  }

  &:active {
    background: var(--color-primary-active) !important;
  }
}
</style>
