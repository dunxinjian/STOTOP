<template>
  <a-popover
    v-model:open="popoverOpen"
    placement="topLeft"
    trigger="click"
    overlay-class-name="sidebar-settings-popover"
  >
    <slot />
    <template #content>
      <div style="max-height: 360px; overflow-y: auto; min-width: 200px;">
        <template v-for="group in systemMenuGroups" :key="group.groupName">
          <div class="settings-group">
            <div
              v-if="shouldShowGroupTitle(group)"
              class="settings-group__title"
            >
              {{ group.groupName }}
            </div>
            <div
              v-for="item in group.items"
              :key="item.route || item.code"
              class="settings-group__item"
              :class="{ 'settings-group__item--active': isItemActive(item.route) }"
              @click="handleItemClick(item)"
            >
              <component :is="getIconComponent(item.icon)" v-if="getIconComponent(item.icon)" />
              <span>{{ item.name }}</span>
            </div>
          </div>
        </template>
        <div v-if="!systemMenuGroups.length" style="padding: 16px; text-align: center; color: rgba(0,0,0,0.45); font-size: 13px;">
          暂无可用设置
        </div>
      </div>
    </template>
  </a-popover>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as AntIcons from '@ant-design/icons-vue'
import { usePermissionStore } from '@/stores/permission'
import type { MenuItem } from '@/api/auth'

const route = useRoute()
const router = useRouter()
const permissionStore = usePermissionStore()

const popoverOpen = ref(false)

const systemMenuGroups = computed(() => {
  return permissionStore.getModuleMenuGroups('system')
})

function getIconComponent(iconName?: string) {
  if (!iconName) return null
  return (AntIcons as Record<string, any>)[iconName] || null
}

function shouldShowGroupTitle(group: { groupName: string; items: MenuItem[] }): boolean {
  if (group.items.length === 1 && group.items[0].name === group.groupName) {
    return false
  }
  return true
}

function isItemActive(itemRoute?: string): boolean {
  if (!itemRoute) return false
  return route.path === itemRoute || route.path.startsWith(itemRoute + '/')
}

function handleItemClick(item: MenuItem) {
  if (!item.route) return
  router.push(item.route)
  popoverOpen.value = false
}
</script>

<style scoped lang="scss">
@use '@/styles/sidebar.scss';
</style>
