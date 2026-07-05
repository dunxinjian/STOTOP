<template>
  <div class="tab-bar">
    <!-- 左：固定页签（发起 / 待办）+ 多页签链 -->
    <div class="tab-bar__left">
      <button type="button" class="fixed-tab" :class="{ active: isInitiate }" @click="goInitiate">
        <PlusCircleOutlined class="fixed-tab__icon" />
        <span>发起</span>
      </button>
      <button type="button" class="fixed-tab" :class="{ active: isTodo }" @click="goTodo">
        <ScheduleOutlined class="fixed-tab__icon" />
        <span>待办</span>
        <span v-if="todoCount > 0" class="fixed-tab__badge">{{ todoCount > 99 ? '99+' : todoCount }}</span>
      </button>
      <span class="tab-bar__divider" />
      <TopBarNavChain class="tab-bar__chain" />
    </div>

    <!-- 右：操作 + 主题 + 用户 -->
    <div class="tab-bar__right">
      <button type="button" class="tb-icon-btn" title="刷新" @click="refreshPage">
        <ReloadOutlined />
      </button>
      <button
        type="button"
        class="tb-icon-btn"
        :title="isFullscreen ? '退出全屏' : '全屏'"
        @click="toggleFullscreen"
      >
        <FullscreenExitOutlined v-if="isFullscreen" />
        <FullscreenOutlined v-else />
      </button>
      <ThemeSwitcher class="tb-theme" />
      <span class="tab-bar__divider tall" />
      <a-dropdown placement="bottomRight" trigger="click">
        <button type="button" class="tb-user" :title="displayName">
          <span class="tb-avatar">{{ userInitial }}</span>
          <span class="tb-user-name">{{ displayName }}</span>
          <DownOutlined class="tb-user-caret" />
        </button>
        <template #overlay>
          <a-menu>
            <a-menu-item key="profile" @click="goPersonalSettings">个人设置</a-menu-item>
            <a-menu-divider />
            <a-menu-item key="logout" @click="logout">退出登录</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  PlusCircleOutlined, ScheduleOutlined, ReloadOutlined,
  FullscreenOutlined, FullscreenExitOutlined, DownOutlined,
} from '@ant-design/icons-vue'
import TopBarNavChain from './TopBarNavChain.vue'
import ThemeSwitcher from '@/components/ThemeSwitcher.vue'
import { useUserStore } from '@/stores/user'
import { usePermissionStore } from '@/stores/permission'
import { useOrgContextStore } from '@/stores/orgContext'
import { useTenantContextStore } from '@/stores/tenantContext'
import { useSidebarStore } from '@/stores/sidebar'
import { useNotificationStore } from '@/stores/notification'
import { resetRouter } from '@/router'
import { markNavSource } from '@/stores/navChain'
import { pinyinInitial } from '@/utils/pinyin'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const permissionStore = usePermissionStore()
const orgContextStore = useOrgContextStore()
const tenantContextStore = useTenantContextStore()
const sidebarStore = useSidebarStore()
const notificationStore = useNotificationStore()

// ── 固定页签：发起 / 待办 ──
const isInitiate = computed(() => route.path.startsWith('/workhub/initiate'))
const isTodo = computed(() => route.path.startsWith('/workhub') && !isInitiate.value)
const todoCount = computed(() => notificationStore.todoCount?.total ?? 0)

function goInitiate() {
  markNavSource('menu')
  router.push('/workhub/initiate')
}
function goTodo() {
  markNavSource('menu')
  router.push('/workhub/todo')
}

// ── 右侧操作 ──
function refreshPage() {
  orgContextStore.triggerPageRefresh()
}
// 浏览器原生全屏
const isFullscreen = ref(false)
function syncFullscreen() {
  isFullscreen.value = !!document.fullscreenElement
}
function toggleFullscreen() {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen?.()
  } else {
    document.exitFullscreen?.()
  }
}

// ── 用户区 ──
const displayName = computed(() => userStore.userInfo?.realName || userStore.userInfo?.username || '用户')
const userInitial = computed(() => pinyinInitial(displayName.value) || 'U')

function goPersonalSettings() {
  router.push('/profile')
}
async function logout() {
  await userStore.logout()
  permissionStore.reset()
  orgContextStore.clearOrgContext()
  tenantContextStore.clearTenantContext()
  sidebarStore.reset?.()
  resetRouter()
  router.push('/login')
}

onMounted(() => {
  notificationStore.startPolling?.()
  document.addEventListener('fullscreenchange', syncFullscreen)
  syncFullscreen()
})
onBeforeUnmount(() => {
  notificationStore.stopPolling?.()
  document.removeEventListener('fullscreenchange', syncFullscreen)
})
</script>

<style scoped lang="scss">
.tab-bar {
  height: 42px;
  flex-shrink: 0;
  display: flex;
  align-items: stretch;
  padding: 0 8px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-strong);
  box-shadow: var(--shadow-sm);
  position: relative;
  z-index: 5;
}

.tab-bar__left {
  display: flex;
  align-items: stretch;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.tab-bar__right {
  display: flex;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
}

.tab-bar__chain {
  flex: 1;        // 撑满固定页签右侧的剩余空间：既用满顶栏，也让 nav-chain 测到稳定的真·可用宽度
  min-width: 0;
}

.tab-bar__divider {
  width: 1px;
  height: 18px;
  background: var(--border);
  align-self: center;
  margin: 0 4px;
  flex-shrink: 0;
}

// —— 固定页签
.fixed-tab {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 100%;
  padding: 0 12px;
  border: none;
  background: transparent;
  border-bottom: 2px solid transparent;
  color: var(--text-2);
  font-size: 13px;
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.15s, color 0.15s, border-color 0.15s;

  &:hover {
    background: var(--bg-muted);
    color: var(--text-1);
  }

  &.active {
    color: var(--color-primary);
    font-weight: 500;
    border-bottom-color: var(--color-primary);
  }
}

.fixed-tab__icon {
  font-size: 15px;
}

.fixed-tab__badge {
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: var(--text-on-accent);
  font-size: 10px;
  line-height: 16px;
  text-align: center;
  font-weight: 500;
}

// —— 右侧图标按钮
.tb-icon-btn {
  width: 30px;
  height: 30px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  color: var(--text-2);
  font-size: 16px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--bg-muted);
    color: var(--text-1);
  }
}

.tb-theme {
  display: inline-flex;
  align-items: center;
}

// —— 用户
.tb-user {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 30px;
  padding: 0 6px 0 4px;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  cursor: pointer;
  color: var(--text-1);
  transition: background 0.15s;

  &:hover {
    background: var(--bg-muted);
  }
}

.tb-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--color-primary-light);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 500;
  flex-shrink: 0;
}

.tb-user-name {
  font-size: 12.5px;
  font-weight: 500;
  max-width: 90px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tb-user-caret {
  font-size: 11px;
  color: var(--text-3);
}

@media (max-width: 960px) {
  .tb-user-name {
    display: none;
  }
}
</style>
