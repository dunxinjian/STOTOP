<template>
  <div class="page-mine">
    <!-- 头部：头像 + 姓名 + 当前组织 -->
    <div class="profile">
      <van-image
        round
        width="56"
        height="56"
        :src="avatarUrl"
        :error-icon="''"
      >
        <template #error>
          <div class="avatar-fallback">{{ avatarLetter }}</div>
        </template>
      </van-image>
      <div class="profile-info">
        <div class="profile-name">{{ user?.name || '未登录' }}</div>
        <div class="profile-org">{{ currentOrg?.name || '未选择组织' }}</div>
      </div>
    </div>

    <!-- 第一组：组织 / 消息 / 操作记录 -->
    <van-cell-group inset class="group">
      <van-cell title="切换组织" is-link :value="currentOrg?.name || '选择组织'" @click="showOrgSheet = true" />
      <van-cell title="我的消息" is-link :value="unreadText" @click="goMessages" />
      <van-cell title="操作记录" is-link @click="goAuditLog" />
    </van-cell-group>

    <!-- 第二组：缓存 / 帮助 / 关于 -->
    <van-cell-group inset class="group">
      <van-cell title="清除缓存" is-link :value="cacheSize" @click="clearCache" />
      <van-cell title="帮助与反馈" is-link @click="goHelp" />
      <van-cell title="关于" is-link :value="versionText" @click="onCheckVersion" />
    </van-cell-group>

    <!-- 退出登录 -->
    <div class="logout-wrap">
      <van-button block type="default" @click="onLogout">退出登录</van-button>
    </div>

    <!-- 组织切换 ActionSheet -->
    <van-action-sheet
      v-model:show="showOrgSheet"
      :actions="orgActions"
      cancel-text="取消"
      close-on-click-action
      @select="onOrgSelect"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  CellGroup as VanCellGroup,
  Cell as VanCell,
  ActionSheet as VanActionSheet,
  Image as VanImage,
  Button as VanButton,
  showDialog,
  showToast,
  showLoadingToast,
  closeToast,
} from 'vant'
import { useAuthStore } from '../stores/auth'
import { checkVersion } from '@shared/api/mobile'
import { pinyinInitial } from '@/utils/pinyin'

defineOptions({ name: 'MobileMine' })

const APP_VERSION = '1.0.0'
const CACHE_PREFIX = 'stotop_'
// token / refreshToken / 当前组织 等关键键不能清除
const PROTECTED_KEYS = new Set([
  'stotop_mobile_token',
  'stotop_mobile_refresh_token',
  'stotop_current_org_id',
])

const authStore = useAuthStore()

const user = computed(() => authStore.user)
const currentOrg = computed(() => authStore.currentOrg)

const avatarUrl = computed(() => user.value?.avatar || '')
const avatarLetter = computed(() => pinyinInitial(user.value?.name) || '?')

// 组织切换
const showOrgSheet = ref(false)
const orgActions = computed(() =>
  authStore.organizations.map(org => ({
    name: org.name,
    value: org.id,
    color: org.id === authStore.currentOrgId ? 'var(--text-1)' : undefined,
  }))
)

function onOrgSelect(action: { value: number }) {
  authStore.setCurrentOrg(action.value)
  showOrgSheet.value = false
  showToast('已切换组织')
}

// 我的消息（占位）
const unreadCount = ref(0)
const unreadText = computed(() => (unreadCount.value > 0 ? `${unreadCount.value}条未读` : ''))

function goMessages() {
  showToast('消息列表即将上线')
}

function goAuditLog() {
  showToast('操作记录即将上线')
}

function goHelp() {
  showToast('帮助与反馈即将上线')
}

// 缓存大小估算
const cacheSize = ref('')

function calcCacheSize() {
  let bytes = 0
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key && key.startsWith(CACHE_PREFIX) && !PROTECTED_KEYS.has(key)) {
      const v = localStorage.getItem(key) || ''
      bytes += key.length + v.length
    }
  }
  cacheSize.value = formatBytes(bytes)
}

function formatBytes(b: number): string {
  if (b < 1024) return `${b}B`
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)}KB`
  return `${(b / 1024 / 1024).toFixed(2)}MB`
}

async function clearCache() {
  try {
    await showDialog({
      title: '清除缓存',
      message: '将清除本地缓存数据（保留登录状态），是否继续？',
      showCancelButton: true,
    })
  } catch {
    return
  }

  const removeKeys: string[] = []
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key && key.startsWith(CACHE_PREFIX) && !PROTECTED_KEYS.has(key)) {
      removeKeys.push(key)
    }
  }
  removeKeys.forEach(k => localStorage.removeItem(k))
  calcCacheSize()
  showToast(`已清除 ${removeKeys.length} 项缓存`)
}

// 版本检查
const versionText = computed(() => `v${APP_VERSION}`)

async function onCheckVersion() {
  showLoadingToast({ message: '检查中...', forbidClick: true })
  try {
    const res = await checkVersion()
    closeToast()
    if (res?.version && res.version !== APP_VERSION) {
      showDialog({
        title: '发现新版本',
        message: `当前 v${APP_VERSION}\n最新 v${res.version}${res.forceUpdate ? '\n（建议立即更新）' : ''}`,
      })
    } else {
      showToast('已是最新版本')
    }
  } catch {
    closeToast()
    showToast('检查失败')
  }
}

// 退出登录
async function onLogout() {
  try {
    await showDialog({
      title: '退出登录',
      message: '确定要退出当前账号吗？',
      showCancelButton: true,
    })
  } catch {
    return
  }
  authStore.clearToken()
  // 重新走免登流程
  try {
    await authStore.loginByDingTalk()
    showToast('已重新登录')
  } catch {
    showToast('请重新打开应用')
  }
}

onMounted(() => {
  calcCacheSize()
})
</script>

<style scoped lang="scss">
.page-mine {
  min-height: 100vh;
  background: #f5f6f8;
  padding-bottom: 32px;
}

.profile {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 24px 20px 28px;
  background: linear-gradient(180deg, #ffffff 0%, #f5f6f8 100%);
}

.avatar-fallback {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  font-size: 22px;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
}

.profile-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.profile-name {
  font-size: 18px;
  font-weight: 600;
  color: #323233;
}

.profile-org {
  font-size: 13px;
  color: #969799;
}

.group {
  margin-top: 12px;
  border-radius: 10px;
  overflow: hidden;
}

.logout-wrap {
  padding: 24px 16px 0;

  :deep(.van-button) {
    color: #ee0a24;
    border-radius: 10px;
  }
}
</style>
