<!-- @deprecated 已被 MegaMenu 取代，此组件不再使用。保留文件以备将来需要。 -->
<template>
  <div class="side-menu">
    <a-menu
      mode="inline"
      :selected-keys="selectedKeys"
      :open-keys="openKeys"
      :inline-collapsed="sidebarStore.collapsed"
      theme="dark"
      @click="handleMenuClick"
      @open-change="handleOpenChange"
    >
      <template v-for="item in filteredMenus" :key="item.id">
        <SideMenuItem :item="item" />
      </template>
    </a-menu>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { useSidebarStore } from '@/stores/sidebar'
import { usePermissionStore } from '@/stores/permission'
import SideMenuItem from './SideMenuItem.vue'
import type { MenuItem } from '@/api/auth'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()
const sidebarStore = useSidebarStore()
const permissionStore = usePermissionStore()

// 当前选中的菜单
const selectedKeys = ref<string[]>([])
// 展开的子菜单
const openKeys = ref<(string | number)[]>([])

// 根据当前模块过滤菜单
const filteredMenus = computed(() => {
  return permissionStore.getCurrentModuleMenus(appStore.currentModule)
})

// 根据路由设置选中的菜单和展开的子菜单
function updateMenuState() {
  const path = route.path
  selectedKeys.value = [path]
  
  // 找到所有需要展开的父菜单
  const parentKeys: string[] = []
  function findParents(items: MenuItem[], parentIds: string[] = []): void {
    for (const item of items) {
      if (item.route === path) {
        parentKeys.push(...parentIds)
        return
      }
      if (item.children && item.children.length > 0) {
        findParents(item.children, [...parentIds, String(item.id)])
      }
    }
  }
  findParents(filteredMenus.value)
  openKeys.value = [...new Set([...openKeys.value, ...parentKeys])]
}

// 点击菜单项
function handleMenuClick({ key }: { key: string | number }) {
  if (typeof key === 'string' && key.startsWith('/')) {
    router.push(key)
  }
}

// 展开/收起子菜单
function handleOpenChange(keys: (string | number)[]) {
  openKeys.value = keys
}

// 监听路由变化
watch(() => route.path, () => {
  updateMenuState()
}, { immediate: true })

// 监听菜单数据变化
watch(() => filteredMenus.value, () => {
  updateMenuState()
}, { immediate: true })

// 折叠时关闭所有展开的菜单
watch(() => sidebarStore.collapsed, (collapsed) => {
  if (collapsed) {
    openKeys.value = []
  }
})

onMounted(() => {
  updateMenuState()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.side-menu {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;

  &::-webkit-scrollbar {
    width: 4px;
  }

  &::-webkit-scrollbar-thumb {
    background: rgba(255,255,255,0.15);
    border-radius: 2px;
  }

  &::-webkit-scrollbar-track {
    background: transparent;
  }

  :deep(.ant-menu) {
    border-right: none !important;
    background: transparent;

    .ant-menu-item,
    .ant-menu-submenu-title {
      height: 40px;
      line-height: 40px;
      margin: 2px 8px;
      border-radius: 4px;
      color: rgba(255,255,255,0.65);
      padding-left: 16px !important;

      .anticon {
        color: rgba(255,255,255,0.45);
        font-size: 15px;
      }

      &:hover:not(.ant-menu-item-selected) {
        background: rgba(255,255,255,0.08);
        color: rgba(255,255,255,0.85);

        .anticon {
          color: rgba(255,255,255,0.85);
        }
      }
    }

    .ant-menu-item-selected {
      background: $color-primary !important;
      color: #ffffff !important;
      border-left: none;
      padding-left: 16px !important;
      font-weight: 500;

      .anticon {
        color: #ffffff !important;
      }

      &::after {
        display: none;
      }
    }

    .ant-menu-submenu-open {
      > .ant-menu-submenu-title {
        background: transparent;
        color: rgba(255,255,255,0.85);
      }
    }

    // 展开的子菜单背景
    .ant-menu-sub {
      background: rgba(0,0,0,0.2) !important;

      .ant-menu-item {
        padding-left: 40px !important;
      }

      .ant-menu-item-selected {
        padding-left: 40px !important;
      }
    }

    // 折叠态tooltip样式
    .ant-menu-item-group-title {
      color: rgba(255,255,255,0.45);
      font-size: 11px;
      padding: 4px 16px 2px;
    }
  }
}
</style>
