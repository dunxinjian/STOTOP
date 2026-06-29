<template>
  <div
    class="app-layout"
    :style="{
      '--sidebar-width': sidebarStore.collapsed ? '48px' : sidebarStore.sidebarWidth + 'px'
    }"
  >
    <!-- 全高侧栏（深色） -->
    <AppSidebar />

    <!-- 内容区：多页签顶栏 + 内容 -->
    <div class="content-area">
      <TabBar />
      <AppBreadcrumb v-if="!isWorkhub" />
      <div class="content-scroll">
        <router-view v-slot="{ Component, route: viewRoute }">
          <keep-alive :max="20">
            <component :is="Component" :key="`${orgContextStore.orgSwitchVersion}:${orgContextStore.pageRefreshVersion}:${viewRoute.fullPath}`" />
          </keep-alive>
        </router-view>
      </div>
    </div>

    <GlobalSearch />
    <ShortcutHelp />
    <FeedbackQuickSubmit />
  </div>
</template>

<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
import TabBar from './TabBar.vue'
import GlobalSearch from '@/components/GlobalSearch.vue'
import ShortcutHelp from '@/components/ShortcutHelp.vue'
import FeedbackQuickSubmit from '@/components/FeedbackQuickSubmit.vue'
import AppBreadcrumb from '@/components/AppBreadcrumb.vue'
import { useAppStore, MODULE_TABS } from '@/stores/app'
import { useOrgContextStore } from '@/stores/orgContext'
import { useSidebarStore } from '@/stores/sidebar'
import { computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const appStore = useAppStore()
const orgContextStore = useOrgContextStore()
const sidebarStore = useSidebarStore()

// 工作台（发起/待办）页：内容全出血，不显示面包屑
const isWorkhub = computed(() => route.path.startsWith('/workhub'))

// ── 路由 → 模块检测 ────────────────────────────────────────
function updateCurrentModuleFromRoute() {
  const path = route.path

  if (path === '/' || path === '/home' || path.startsWith('/workhub')) {
    appStore.setCurrentModule('workhub')
    return
  }

  for (const mod of MODULE_TABS) {
    const routePrefix = mod.route.split('/').slice(0, 2).join('/')
    if (path.startsWith(routePrefix + '/') || path === mod.route) {
      appStore.setCurrentModule(mod.code)
      return
    }
  }
}

// 路由变化 → 同步当前模块标识
watch(() => route.fullPath, () => {
  updateCurrentModuleFromRoute()
}, { immediate: true })

// ── 初始化 ─────────────────────────────────────────────────────
onMounted(() => {
  appStore.fetchVersion()
  updateCurrentModuleFromRoute()
})
</script>
