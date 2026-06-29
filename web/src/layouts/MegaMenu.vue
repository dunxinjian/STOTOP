<!--
  @deprecated 此组件已弃用，不再被任何组件引用。
  Mega Menu 的悬停弹出行为已移除，菜单内容现在通过 ModuleMenuPanel.vue 嵌入到模块首页中。
  保留此文件仅供参考，请勿在新代码中使用。
-->
<template>
  <Teleport to="body">
    <!-- 遮罩层 -->
    <Transition name="mega-fade">
      <div
        v-if="visible"
        class="mega-overlay"
        @click="emit('close')"
      />
    </Transition>

    <!-- 面板 -->
    <Transition name="mega-slide">
      <div
        v-if="visible"
        class="mega-panel"
        @keydown.esc="emit('close')"
        @mouseenter="emit('panel-enter')"
        @mouseleave="emit('panel-leave')"
        tabindex="-1"
        ref="panelRef"
      >
        <!-- 无菜单提示（不应出现，有 hasGroups 保护） -->
        <div v-if="!groups.length" class="mega-empty">暂无菜单项</div>

        <!-- 分栏网格 -->
        <div v-else class="mega-grid">
          <div
            v-for="group in groups"
            :key="group.groupName"
            class="mega-col"
          >
            <!-- 只有当分组有多个item或groupName与item.name不同时才显示标题 -->
            <div
              v-if="shouldShowGroupTitle(group)"
              class="mega-col-title"
            >{{ group.groupName }}</div>
            <ul class="mega-col-items" :class="{ 'no-title': !shouldShowGroupTitle(group) }">
              <li
                v-for="item in group.items"
                :key="item.id"
                class="mega-item"
                :class="{ 'is-active': isCurrentRoute(item.route) }"
                @click="handleItemClick(item)"
              >
                <component
                  v-if="getIconComponent(item.icon)"
                  :is="getIconComponent(item.icon)"
                  class="mega-item-icon"
                />
                <span>{{ item.name }}</span>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as AntIcons from '@ant-design/icons-vue'
import { usePermissionStore } from '@/stores/permission'
import type { MenuItem } from '@/api/auth'

interface Props {
  visible: boolean
  moduleCode: string
  triggerRect?: DOMRect | null
}

const props = withDefaults(defineProps<Props>(), {
  triggerRect: null,
})

const emit = defineEmits<{
  close: []
  navigate: [route: string]
  'panel-enter': []
  'panel-leave': []
}>()

const route = useRoute()
const router = useRouter()
const permissionStore = usePermissionStore()
const panelRef = ref<HTMLElement | null>(null)

// 获取分组数据
const groups = computed(() => {
  if (!props.moduleCode) return []
  return permissionStore.getModuleMenuGroups(props.moduleCode)
})

// 动态图标
function getIconComponent(iconName?: string) {
  if (!iconName) return null
  return (AntIcons as Record<string, any>)[iconName] || null
}

// 当前路由高亮
function isCurrentRoute(itemRoute?: string): boolean {
  if (!itemRoute) return false
  return route.path === itemRoute || route.path.startsWith(itemRoute + '/')
}

// 判断是否需要显示分组标题
// 如果分组只有1个item且groupName === item.name，则不显示标题（避免冗余）
function shouldShowGroupTitle(group: { groupName: string; items: MenuItem[] }): boolean {
  if (group.items.length === 1) {
    const item = group.items[0]
    if (item.name === group.groupName) {
      return false
    }
  }
  return true
}

/**
 * 从菜单编码推断路由路径
 * 例: "cardflow:upload-center" => "/cardflow/upload-center"
 */
function inferRouteFromCode(menuCode: string): string | undefined {
  if (!menuCode) return undefined
  const parts = menuCode.split(':')
  if (parts.length >= 2) {
    // 取前两段作为路由：module/page
    return '/' + parts.slice(0, 2).join('/')
  }
  return undefined
}

// 菜单项点击——通过 router.push 导航
function handleItemClick(item: MenuItem) {
  let targetRoute = item.route

  // fallback：route 为空时从 code 推断
  if (!targetRoute && item.code) {
    targetRoute = inferRouteFromCode(item.code)
    if (targetRoute) {
      console.warn(`[MegaMenu] 菜单 "${item.name}" route 为空，从 code 推断为 "${targetRoute}"`)
    }
  }

  if (targetRoute) {
    router.push(targetRoute)
    emit('navigate', targetRoute)
  } else {
    console.error(`[MegaMenu] 无法确定菜单 "${item.name}" 的路由`, item)
  }
  emit('close')
}

// 面板打开时聚焦，使 ESC 键生效
watch(
  () => props.visible,
  async (val) => {
    if (val) {
      await nextTick()
      panelRef.value?.focus()
    }
  }
)
</script>

<style scoped lang="scss">
.mega-overlay {
  position: fixed;
  inset: 0;
  top: 48px;
  background: rgba(0, 0, 0, 0.25);
  z-index: 98;
}

.mega-panel {
  position: fixed;
  top: 48px;
  left: 0;
  right: 0;
  background: #ffffff;
  border-radius: 0 0 8px 8px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
  z-index: 99;
  outline: none;
  overflow-y: auto;
  max-height: calc(100vh - 48px - 40px);
}

.mega-empty {
  padding: 24px;
  color: rgba(0, 0, 0, 0.45);
  text-align: center;
  font-size: 14px;
}

.mega-grid {
  display: flex;
  overflow-x: auto;
  gap: 0;
  padding: 12px 8px;
}

.mega-col {
  padding: 12px 16px;
  min-width: 140px;
  max-width: 200px;
  flex-shrink: 0;
  border-right: 1px solid #f0f0f0;

  &:last-child {
    border-right: none;
  }
}

.mega-col-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-1);
  letter-spacing: 0.3px;
  padding: 4px 8px 10px;
  margin-bottom: 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  background: linear-gradient(to right, var(--bg-muted), transparent);
  border-radius: 4px;
  margin-left: -8px;
  margin-right: -8px;
}

.mega-col-items {
  list-style: none;
  margin: 0;
  padding: 0;

  &.no-title {
    padding-top: 0;
  }
}

.mega-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 8px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
  transition: all 0.15s ease;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;

  &:hover {
    background: var(--color-primary-light);
    color: var(--color-primary);
  }

  &.is-active {
    color: var(--color-primary);
    font-weight: 500;
    background: var(--color-primary-light);
  }

  .mega-item-icon {
    font-size: 14px;
    flex-shrink: 0;
  }
}

// 遮罩动画
.mega-fade-enter-active,
.mega-fade-leave-active {
  transition: opacity 0.2s ease;
}
.mega-fade-enter-from,
.mega-fade-leave-to {
  opacity: 0;
}

// 面板滑入动画
.mega-slide-enter-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.mega-slide-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.mega-slide-enter-from {
  opacity: 0;
  transform: translateY(-8px);
}
.mega-slide-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
