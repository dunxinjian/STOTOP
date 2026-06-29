<template>
  <div :class="rootClass">
    <!-- 用户没有关联任何组织且未选择组织时，显示企业简称 -->
    <div v-if="orgContextStore.organizations.length === 0 && !orgContextStore.currentOrgId" class="org-current enterprise-name">
      <span class="org-icon-circle">
        <BankOutlined :style="{ fontSize: '13px' }" />
      </span>
      <span class="org-name">{{ enterpriseInfoStore.displayName }}</span>
    </div>

    <!-- 用户有关联组织时，显示组织切换器 -->
    <template v-else>
      <a-popover
        v-if="orgContextStore.hasMultipleOrgs"
        v-model:open="popoverOpen"
        placement="bottomLeft"
        trigger="click"
        overlay-class-name="org-switcher-popover"
      >
        <template #content>
          <div class="org-list">
            <div class="org-list-header">切换组织</div>
            <div
              v-for="org in orgContextStore.organizations"
              :key="org.id"
              class="org-item"
              :class="{ active: org.orgId === orgContextStore.currentOrgId }"
              @click="handleSwitch(org)"
            >
              <div class="org-item-main">
                <BankOutlined :style="{ fontSize: '14px' }" />
                <span class="org-item-name">{{ org.orgName }}</span>
              </div>
              <div class="org-item-tags">
                <a-tag v-if="org.isPrimaryOrg === 1" color="blue" size="small">主组织</a-tag>
              </div>
              <CheckOutlined v-if="org.orgId === orgContextStore.currentOrgId" class="check-icon" />
            </div>
          </div>
        </template>

        <div
          class="org-current"
          role="button"
          tabindex="0"
          aria-label="切换组织"
          aria-haspopup="menu"
          :aria-expanded="popoverOpen"
          @keydown.enter="togglePopover"
          @keydown.space.prevent="togglePopover"
        >
          <span class="org-icon-circle">
            <BankOutlined :style="{ fontSize: '13px' }" />
          </span>
          <span class="org-name">{{ orgContextStore.currentOrgName || '选择组织' }}</span>
          <CaretDownOutlined class="org-arrow" />
        </div>
      </a-popover>

      <!-- 只有一个组织时不显示下拉箭头 -->
      <div v-else class="org-current single">
        <span class="org-icon-circle">
          <BankOutlined :style="{ fontSize: '13px' }" />
        </span>
        <span class="org-name">{{ orgContextStore.currentOrgName }}</span>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { BankOutlined, CheckOutlined, CaretDownOutlined } from '@ant-design/icons-vue'
import { useOrgContextStore } from '@/stores/orgContext'
import { usePermissionStore } from '@/stores/permission'
import { useAccountSetStore } from '@/stores/accountSet'
import { useEnterpriseInfoStore } from '@/stores/enterpriseInfo'
import { useSidebarStore } from '@/stores/sidebar'
import { MODULE_TABS } from '@/stores/app'
import { useRouter, useRoute } from 'vue-router'
import { replaceDynamicRoutes } from '@/router'
import type { UserOrganizationDto } from '@/types/organization'

const props = withDefaults(defineProps<{
  mode?: 'dark' | 'light'
}>(), {
  mode: 'dark'
})

const orgContextStore = useOrgContextStore()
const permissionStore = usePermissionStore()
const accountSetStore = useAccountSetStore()
const enterpriseInfoStore = useEnterpriseInfoStore()
const sidebarStore = useSidebarStore()
const router = useRouter()
const route = useRoute()

const popoverOpen = ref(false)

/** 切换 Popover 显示状态 */
function togglePopover() {
  popoverOpen.value = !popoverOpen.value
}

// 根元素动态 class
const rootClass = computed(() => ({
  'org-switcher': true,
  'org-switcher--dark': props.mode === 'dark',
}))

const emit = defineEmits<{
  (e: 'switched'): void
}>()

async function handleSwitch(org: UserOrganizationDto) {
  if (org.orgId === orgContextStore.currentOrgId) {
    popoverOpen.value = false
    return
  }

  const data = await orgContextStore.doSwitchOrganization(org.orgId)
  if (data) {
    // 1. generateRoutes 已在 doSwitchOrganization 中于 orgSwitchVersion 递增前调用
    // 直接使用已生成好的路由刷新 Vue Router 动态路由表
    replaceDynamicRoutes(permissionStore.routes)

    // 2. 刷新账套列表并自动选中默认账套
    await accountSetStore.fetchAccountSets()

    // 3. 计算新组织下有效的路由路径白名单
    const validRoutes = new Set<string>()
    for (const m of permissionStore.menus) {
      if (m.route) validRoutes.add(m.route)
    }
    // alwaysShow 模块首页也视为有效
    const alwaysRoutes = new Set<string>()
    for (const mod of MODULE_TABS) {
      if (mod.alwaysShow || mod.code === 'workhub') alwaysRoutes.add(mod.route)
    }

    // 4. 清理无权限的收藏（最近访问属于组织上下文，直接清空）
    const validPaths: string[] = []
    for (const fav of sidebarStore.favorites) {
      if (!fav) continue
      if (fav === '/' || fav === '/workhub') { validPaths.push(fav); continue }
      if (alwaysRoutes.has(fav)) { validPaths.push(fav); continue }
      if (validRoutes.has(fav)) { validPaths.push(fav); continue }
      // 前缀匹配：支持带路径参数的详情页（如 /finance/voucher/123）
      let matched = false
      for (const rp of validRoutes) {
        if (rp && fav.startsWith(rp + '/')) { matched = true; break }
      }
      if (matched) validPaths.push(fav)
    }
    sidebarStore.cleanupInvalidFavorites(validPaths)

    // 5. 清空最近访问（属于组织上下文，跨组织切换时不应保留）
    sidebarStore.recentPages = []

    popoverOpen.value = false
    emit('switched')

    // 6. 留在当前页面；若新组织下无访问权限才跳转工作台
    const currentPath = route.path
    const isAlways = [...alwaysRoutes].some((r) => currentPath === r || currentPath.startsWith(r + '/'))
    const isValid = isAlways || [...validRoutes].some((r) => currentPath === r || currentPath.startsWith(r + '/'))
    if (!isValid) {
      router.push('/workhub')
    }
    // 页面正常有权限时： orgSwitchVersion 已递增，MainLayout :key 会自动触发重挂载（即刷新）
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.org-switcher {
  display: flex;
  align-items: center;
}

// 触发按钮基础样式（浅色 / 默认）
.org-switcher .org-current,
.org-current.org-current {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
  color: rgba(0, 0, 0, 0.88);
  background: transparent;
  position: relative;
  max-width: 200px;

  &:hover {
    color: rgba(0, 0, 0, 0.95);
    background: rgba($color-primary, 0.04);
  }

  // 方案二：去掉圆圈底色，图标直接内联
  .org-icon-circle {
    width: auto;
    height: auto;
    border-radius: 0;
    background: none;
    color: var(--text-2);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  .org-name {
    font-size: 13px;
    font-weight: 600;
    max-width: 160px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: rgba(0, 0, 0, 0.88);
  }

  .org-arrow {
    font-size: 10px;
    color: rgba(0, 0, 0, 0.45);
    transition: transform 0.3s;
    flex-shrink: 0;
  }
}

// dark 模式样式 —— 适配顶栏深色背景(#1E1F26)
.org-switcher--dark {
  background: transparent;
  border-radius: 0;
  padding: 0;

  .org-current {
    color: rgba(255, 255, 255, 0.85);
    background: transparent;
    border: 1px solid transparent;

    &:hover {
      color: #fff;
      background: rgba(255, 255, 255, 0.08);
      border-color: rgba(255, 255, 255, 0.1);
    }

    .org-icon-circle {
      background: none;
      color: rgba(255, 255, 255, 0.55);
    }

    &:hover .org-icon-circle {
      background: none;
      color: rgba(255, 255, 255, 0.85);
    }

    .org-name {
      color: rgba(255, 255, 255, 0.85);
    }

    .org-arrow {
      color: rgba(255, 255, 255, 0.5);
    }
  }

  // 覆盖深穿透样式（防止外部 :deep() 干扰）
  :deep(.org-name),
  .org-name {
    color: rgba(255, 255, 255, 0.85) !important;
  }

  :deep(.org-arrow),
  .org-arrow,
  :deep(.anticon) {
    color: rgba(255, 255, 255, 0.6) !important;
  }
}

.org-list {
  min-width: 220px;

  .org-list-header {
    font-size: 12px;
    color: $text-secondary;
    padding: 4px 8px 8px;
    border-bottom: 1px solid $border-color-lighter;
    margin-bottom: 4px;
  }

  .org-item {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    padding: 8px 10px;
    border-radius: 4px;
    cursor: pointer;
    transition: all 0.2s;
    position: relative;

    &:hover {
      background-color: $bg-page;
    }

    &.active {
      background-color: var(--bg-muted);
      color: var(--text-1);
    }

    .org-item-main {
      display: flex;
      align-items: center;
      gap: 6px;
      flex: 1;
      min-width: 0;

      .org-item-name {
        font-size: 14px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
    }

    .org-item-tags {
      display: flex;
      gap: 4px;
    }

    .check-icon {
      color: var(--text-1);
      margin-left: auto;
    }
  }
}
</style>
