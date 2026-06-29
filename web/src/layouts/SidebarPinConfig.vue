<template>
  <a-popover
    v-model:open="popoverOpen"
    placement="topLeft"
    trigger="click"
    overlay-class-name="sidebar-pin-config"
  >
    <slot />
    <template #content>
      <div class="pin-config-title">添加固定入口</div>
      <a-input
        v-model:value="searchKeyword"
        placeholder="搜索页面"
        size="small"
        allow-clear
        style="margin-bottom: 8px;"
      >
        <template #prefix><SearchOutlined style="color: rgba(0,0,0,0.45);" /></template>
      </a-input>
      <div style="max-height: 340px; overflow-y: auto;">
        <template v-for="mod in filteredModules" :key="mod.code">
          <div class="pin-module-group">
            <div class="pin-module-group__title">{{ mod.name }}</div>
            <template v-for="group in mod.groups" :key="group.groupName">
              <div
                v-for="item in group.items"
                :key="item.route || item.code"
                class="pin-module-group__item"
                :class="{ 'pin-module-group__item--pinned': isPinned(item.route) }"
                @click="togglePin(item)"
              >
                <component :is="getIconComponent(item.icon)" v-if="getIconComponent(item.icon)" />
                <span style="flex: 1;">{{ item.name }}</span>
                <CheckOutlined v-if="isPinned(item.route)" style="color: var(--color-primary); font-size: 12px;" />
              </div>
            </template>
          </div>
        </template>
        <div v-if="!filteredModules.length" style="padding: 16px; text-align: center; color: rgba(0,0,0,0.45); font-size: 13px;">
          {{ searchKeyword ? '无匹配结果' : '暂无可用页面' }}
        </div>
      </div>
    </template>
  </a-popover>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { CheckOutlined, SearchOutlined } from '@ant-design/icons-vue'
import * as AntIcons from '@ant-design/icons-vue'
import { usePermissionStore } from '@/stores/permission'
import { useSidebarStore } from '@/stores/sidebar'
import { useUserStore } from '@/stores/user'
import { MODULE_TABS } from '@/stores/app'
import type { MenuItem } from '@/api/auth'

const permissionStore = usePermissionStore()
const sidebarStore = useSidebarStore()
const userStore = useUserStore()

const popoverOpen = ref(false)
const searchKeyword = ref('')

function getIconComponent(iconName?: string) {
  if (!iconName) return null
  return (AntIcons as Record<string, any>)[iconName] || null
}

// 获取所有有权限的模块及其菜单
const availableModules = computed(() => {
  const visibility = permissionStore.getModuleVisibility(userStore.permissions)
  const result: { code: string; name: string; groups: { groupName: string; items: MenuItem[] }[] }[] = []

  for (const mod of MODULE_TABS) {
    if (mod.code === 'workhub') continue // 工作台始终固定
    const moduleKey = mod.code as keyof typeof visibility
    if (!visibility[moduleKey]) continue

    const groups = permissionStore.getModuleMenuGroups(mod.code)
    if (groups.length > 0) {
      result.push({ code: mod.code, name: mod.name, groups })
    }
  }
  return result
})

// 根据搜索关键字过滤模块列表
const filteredModules = computed(() => {
  const keyword = searchKeyword.value.trim().toLowerCase()
  if (!keyword) return availableModules.value

  return availableModules.value
    .map(mod => {
      const filteredGroups = mod.groups
        .map(group => ({
          ...group,
          items: group.items.filter(item => item.name.toLowerCase().includes(keyword)),
        }))
        .filter(group => group.items.length > 0)
      return { ...mod, groups: filteredGroups }
    })
    .filter(mod => mod.groups.length > 0)
})

function isPinned(route?: string): boolean {
  if (!route) return false
  return sidebarStore.isFavorite(route)
}

function togglePin(item: MenuItem) {
  if (!item.route) return
  if (sidebarStore.isFavorite(item.route)) {
    sidebarStore.removeFavorite(item.route)
  } else {
    sidebarStore.addFavorite(item.route)
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.pin-config-title {
  font-size: 13px;
  font-weight: 500;
  margin-bottom: 8px;
  color: rgba(0, 0, 0, 0.85);
}

.pin-module-group {
  & + .pin-module-group {
    margin-top: 8px;
    padding-top: 8px;
    border-top: 1px dashed #f0f0f0;
  }

  &__title {
    font-size: 11px;
    color: #999;
    letter-spacing: 0.4px;
    padding: 4px 6px;
    user-select: none;
  }

  &__item {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 5px 8px;
    border-radius: 4px;
    font-size: 13px;
    color: rgba(0, 0, 0, 0.75);
    cursor: pointer;
    transition: background 0.15s ease, color 0.15s ease;

    &:hover {
      background: rgba(0, 0, 0, 0.04);
      color: $color-primary;
    }

    &--pinned {
      color: $color-primary;
      background: var(--color-primary-light);
    }

    .anticon {
      font-size: 13px;
    }
  }
}
</style>
