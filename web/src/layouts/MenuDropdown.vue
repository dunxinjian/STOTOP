<!-- @deprecated 已被 MegaMenu 取代，此组件不再使用。保留文件以备将来需要。 -->
<template>
  <template v-for="item in items" :key="item.id">
    <!-- 有子菜单 -->
    <a-sub-menu v-if="hasChildren(item)" :key="String(item.id)">
      <template #title>
        <component v-if="item.icon" :is="item.icon" />
        <span>{{ item.name }}</span>
      </template>
      <MenuDropdown :items="item.children!" :level="level + 1" />
    </a-sub-menu>
    <!-- 无子菜单（叶子节点） -->
    <a-menu-item
      v-else-if="item.type === 'menu' && item.route"
      :key="item.route"
      @click="handleClick(item)"
    >
      <template #icon>
        <component v-if="item.icon" :is="item.icon" />
      </template>
      <span>{{ item.name }}</span>
    </a-menu-item>
  </template>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import type { MenuItem } from '@/api/auth'

interface Props {
  items: MenuItem[]
  level?: number
}

const props = withDefaults(defineProps<Props>(), {
  level: 1,
})

const router = useRouter()

function hasChildren(item: MenuItem): boolean {
  return !!(item.children && item.children.filter((c) => c.type === 'menu').length > 0)
}

function handleClick(item: MenuItem) {
  if (item.route) {
    router.push(item.route)
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// 下拉菜单子项样式优化
:deep(.ant-menu-submenu-popup) {
  min-width: 160px;

  .ant-menu-item {
    height: 36px;
    line-height: 36px;
    margin: 2px $spacing-xs;
    border-radius: $border-radius-sm;
    transition: all $transition-normal;

    &:hover {
      background-color: $bg-page;
      color: $color-primary;
    }
  }

  .ant-menu-submenu-title {
    height: 36px;
    line-height: 36px;
    margin: 2px $spacing-xs;
    border-radius: $border-radius-sm;
    transition: all $transition-normal;

    &:hover {
      background-color: $bg-page;
      color: $color-primary;
    }
  }
}
</style>
