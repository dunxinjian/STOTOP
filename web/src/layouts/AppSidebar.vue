<template>
  <nav
    class="app-sidebar"
    :class="{ 'is-collapsed': collapsed }"
    :style="{ width: collapsed ? collapsedWidth + 'px' : sidebarStore.sidebarWidth + 'px' }"
    aria-label="主导航"
  >
    <!-- 顶部：租户(多客户时) + 工作组织 + 折叠 -->
    <div class="asb-head" :class="{ collapsed }">
      <TenantSwitcher v-if="!collapsed" mode="dark" class="asb-tenant" />
      <OrgSwitcher v-if="!collapsed" mode="dark" class="asb-org" />
      <button
        type="button"
        class="asb-collapse"
        :title="collapsed ? '展开侧栏' : '收起侧栏'"
        @click="sidebarStore.toggleCollapse()"
      >
        <MenuUnfoldOutlined v-if="collapsed" />
        <MenuFoldOutlined v-else />
      </button>
    </div>

    <!-- 搜索（命令面板触发） -->
    <a-tooltip :title="collapsed ? '搜索 (' + shortcutLabel + ')' : ''" placement="right">
      <button type="button" class="asb-search" :class="{ collapsed }" @click="openSearch">
        <SearchOutlined class="asb-search-icon" />
        <template v-if="!collapsed">
          <span class="asb-search-ph">搜索</span>
          <kbd class="asb-search-kbd">{{ shortcutLabel }}</kbd>
        </template>
      </button>
    </a-tooltip>

    <div class="asb-scroll">
      <!-- 业务模块 → 子菜单 两级 -->
      <template v-for="mod in moduleNav" :key="mod.code">
        <!-- 折叠态：仅图标 -->
        <a-tooltip v-if="collapsed" :title="mod.name" placement="right">
          <button
            type="button"
            class="asb-mod-icon"
            :class="{ 'is-active': mod.code === currentModule }"
            @click="goModuleRoot(mod)"
          >
            <component :is="iconOf(mod.icon)" />
          </button>
        </a-tooltip>

        <!-- 展开态：可折叠分组 -->
        <template v-else>
          <button
            type="button"
            class="asb-mod"
            :class="{ 'is-active': mod.code === currentModule }"
            @click="toggleModule(mod.code)"
          >
            <component :is="iconOf(mod.icon)" class="asb-mod-ico" />
            <span class="asb-mod-name">{{ mod.name }}</span>
            <DownOutlined v-if="expanded.has(mod.code)" class="asb-chevron" />
            <RightOutlined v-else class="asb-chevron" />
          </button>

          <div v-if="expanded.has(mod.code)" class="asb-sub">
            <template v-for="(group, gi) in mod.groups" :key="gi">
              <template v-if="group.items.length > 1">
                <div class="asb-group-label">{{ group.groupName }}</div>
                <div class="asb-group-items">
                  <button
                    v-for="item in group.items"
                    :key="item.route || item.code"
                    type="button"
                    class="asb-item"
                    :class="{ 'is-active': isItemActive(item.route) }"
                    @click="goItem(item.route)"
                  >
                    <span class="asb-item-name">{{ item.name }}</span>
                  </button>
                </div>
              </template>
              <template v-else>
                <button
                  v-for="item in group.items"
                  :key="item.route || item.code"
                  type="button"
                  class="asb-item asb-item--flat"
                  :class="{ 'is-active': isItemActive(item.route) }"
                  @click="goItem(item.route)"
                >
                  <span class="asb-bar" />
                  <span class="asb-item-name">{{ item.name }}</span>
                </button>
              </template>
            </template>
            <div v-if="!mod.groups.length" class="asb-empty">暂无可访问页面</div>
          </div>
        </template>
      </template>
    </div>

    <!-- 拖拽改宽（仅展开态） -->
    <div v-if="!collapsed" class="asb-resizer" @pointerdown="startResize" />
  </nav>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  HomeOutlined, AccountBookOutlined, ApartmentOutlined, UserOutlined,
  TeamOutlined, CarOutlined, FileTextOutlined, ShopOutlined, TrophyOutlined,
  CheckSquareOutlined, SendOutlined, SafetyOutlined, SafetyCertificateOutlined,
  ScheduleOutlined, BarChartOutlined, SettingOutlined, AppstoreOutlined,
  DownOutlined, RightOutlined, MenuFoldOutlined, MenuUnfoldOutlined, SearchOutlined,
} from '@ant-design/icons-vue'
import type { MenuItem } from '@/api/auth'
import { useAppStore, MODULE_TABS, type ModuleTab } from '@/stores/app'
import { usePermissionStore } from '@/stores/permission'
import { useUserStore } from '@/stores/user'
import { useSidebarStore } from '@/stores/sidebar'
import { markNavSource } from '@/stores/navChain'
import { useCommandPalette } from '@/composables/useCommandPalette'
import OrgSwitcher from '@/components/OrgSwitcher.vue'
import TenantSwitcher from '@/components/TenantSwitcher.vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()
const permissionStore = usePermissionStore()
const userStore = useUserStore()
const sidebarStore = useSidebarStore()

const collapsedWidth = 48
const collapsed = computed(() => sidebarStore.collapsed)
const currentModule = computed(() => appStore.currentModule)

// —— 搜索：命令面板
const { open: openCommandPalette } = useCommandPalette()
const isMac = typeof navigator !== 'undefined' && navigator.platform.toUpperCase().includes('MAC')
const shortcutLabel = computed(() => (isMac ? '⌘K' : 'Ctrl K'))
function openSearch() {
  openCommandPalette()
}

const iconMap: Record<string, any> = {
  HomeOutlined, AccountBookOutlined, ApartmentOutlined, UserOutlined,
  TeamOutlined, CarOutlined, FileTextOutlined, ShopOutlined, TrophyOutlined,
  CheckSquareOutlined, SendOutlined, SafetyOutlined, SafetyCertificateOutlined,
  ScheduleOutlined, BarChartOutlined, SettingOutlined,
}
function iconOf(name?: string) {
  return (name && iconMap[name]) || AppstoreOutlined
}

interface ModuleNavEntry {
  code: string
  name: string
  route: string
  icon?: string
  groups: { groupName: string; items: MenuItem[] }[]
}

/** 可见业务模块（排除工作台）+ 其分组菜单 */
const moduleNav = computed<ModuleNavEntry[]>(() => {
  const vis = permissionStore.getModuleVisibility(userStore.permissions) as Record<string, boolean>
  return MODULE_TABS
    .filter((m) => m.code !== 'workhub' && (m.alwaysShow || vis[m.code]))
    .map((m) => ({
      code: m.code,
      name: m.name,
      route: m.route,
      icon: m.icon,
      groups: permissionStore.getModuleMenuGroups(m.code),
    }))
})

// 当前模块自动展开
const expanded = ref<Set<string>>(new Set())
watch(currentModule, (code) => {
  if (code && code !== 'workhub') {
    const next = new Set(expanded.value)
    next.add(code)
    expanded.value = next
  }
}, { immediate: true })

function toggleModule(code: string) {
  const next = new Set(expanded.value)
  if (next.has(code)) next.delete(code)
  else next.add(code)
  expanded.value = next
}

function isItemActive(itemRoute?: string): boolean {
  if (!itemRoute) return false
  return route.path === itemRoute || route.path.startsWith(itemRoute + '/')
}

function goItem(itemRoute?: string) {
  if (!itemRoute) return
  markNavSource('menu')
  router.push(itemRoute)
}

function goModuleRoot(mod: ModuleTab) {
  appStore.setCurrentModule(mod.code)
  markNavSource('menu')
  router.push(mod.route)
}

// 拖拽改宽
function startResize(e: PointerEvent) {
  e.preventDefault()
  const startX = e.clientX
  const startW = sidebarStore.sidebarWidth
  const move = (ev: PointerEvent) => sidebarStore.setSidebarWidth(startW + (ev.clientX - startX))
  const up = () => {
    document.removeEventListener('pointermove', move)
    document.removeEventListener('pointerup', up)
    document.body.style.userSelect = ''
  }
  document.body.style.userSelect = 'none'
  document.addEventListener('pointermove', move)
  document.addEventListener('pointerup', up)
}
</script>

<style scoped lang="scss">
.app-sidebar {
  position: relative;
  flex: 0 0 auto;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--sidebar-bg);
  box-sizing: border-box;
  transition: width 0.18s ease;
}

// —— 顶部：工作组织 + 折叠
.asb-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 9px 8px 7px;
}

.asb-head.collapsed {
  justify-content: center;
  padding: 9px 0 7px;
}

.asb-org {
  flex: 1;
  min-width: 0;
}

.app-sidebar .asb-org :deep(.org-current) {
  width: 100%;
  max-width: none;
  height: 30px;
  border: 1px solid var(--sidebar-border);
  border-radius: var(--radius-md);
}

.asb-collapse {
  width: 28px;
  height: 28px;
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  color: var(--sidebar-text-muted);
  font-size: 16px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--sidebar-item-hover);
    color: var(--sidebar-text-strong);
  }
}

// —— 搜索
.asb-search {
  margin: 1px 8px 10px;
  height: 30px;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 8px;
  border: 1px solid var(--sidebar-border);
  border-radius: var(--radius-md);
  background: var(--sidebar-search-bg);
  color: var(--sidebar-text-muted);
  font-size: 13px;
  cursor: pointer;
  text-align: left;
  transition: background 0.15s;

  &:hover {
    background: var(--sidebar-item-hover);
  }
}

.asb-search.collapsed {
  margin: 1px auto 10px;
  width: 32px;
  justify-content: center;
  padding: 0;
}

.asb-search-icon {
  font-size: 14px;
  flex-shrink: 0;
}

.asb-search-ph {
  flex: 1;
}

.asb-search-kbd {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: var(--radius-sm);
  background: var(--sidebar-border);
  color: var(--sidebar-text);
  font-family: inherit;
}

// —— 滚动区
.asb-scroll {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 0 8px 16px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.is-collapsed .asb-scroll {
  padding: 0 0 16px;
  align-items: center;
}

// —— 通用激活左条
.asb-bar {
  position: absolute;
  left: 0;
  top: 6px;
  bottom: 6px;
  width: 2.5px;
  border-radius: 2px;
  background: transparent;
}

// —— 一级模块（按图1，字号偏大、行更舒展）
.asb-mod {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  flex-shrink: 0;
  padding: 8px 10px;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  color: var(--sidebar-text);
  font-size: 14.5px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--sidebar-item-hover);
    color: var(--sidebar-text-strong);
  }

  &.is-active {
    color: var(--sidebar-text-strong);
    font-weight: 500;
  }
}

.asb-mod-ico {
  font-size: 17px;
  flex-shrink: 0;
}

.asb-mod-name {
  flex: 1;
  min-width: 0;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.asb-chevron {
  font-size: 11px;
  color: var(--sidebar-text-muted);
  flex-shrink: 0;
}

// —— 折叠态模块图标
.asb-mod-icon {
  width: 38px;
  height: 38px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--sidebar-text);
  font-size: 18px;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--sidebar-item-hover);
    color: var(--sidebar-text-strong);
  }

  &.is-active {
    color: var(--sidebar-item-active-text);
    background: var(--sidebar-item-active-bg);
  }
}

// —— 二级子菜单
.asb-sub {
  display: flex;
  flex-direction: column;
  gap: 1px;
  padding-bottom: 2px;
  flex-shrink: 0;
}

.asb-group-label {
  font-size: 11px;
  font-weight: 500;
  color: var(--sidebar-text-muted);
  letter-spacing: 0.5px;
  padding: 9px 10px 4px 16px;
  margin-top: 6px;
  border-top: 1px solid var(--sidebar-border);
}

.asb-sub > .asb-group-label:first-child {
  margin-top: 0;
  border-top: none;
  padding-top: 4px;
}

.asb-group-items {
  display: flex;
  flex-direction: column;
  gap: 1px;
  margin: 1px 0 4px 22px;
  border-left: 1px solid var(--sidebar-border);
}

.asb-item {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
  padding: 7px 10px 7px 14px;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  color: var(--sidebar-text);
  font-size: 13px;
  text-align: left;
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--sidebar-item-hover);
    color: var(--sidebar-text-strong);
  }

  &.is-active {
    background: var(--sidebar-item-active-bg);
    color: var(--sidebar-item-active-text);
    font-weight: 500;

    .asb-bar {
      background: var(--sidebar-active-indicator);
    }
  }
}

.asb-item--flat {
  padding-left: 30px;
}

.asb-item-name {
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.asb-empty {
  font-size: 12px;
  color: var(--sidebar-text-muted);
  padding: 6px 10px 6px 33px;
}

// —— 拖拽改宽手柄
.asb-resizer {
  position: absolute;
  top: 0;
  right: -2px;
  width: 5px;
  height: 100%;
  cursor: col-resize;
  z-index: 2;
}

.asb-resizer:hover {
  background: var(--sidebar-border);
}
</style>
