<!-- @deprecated 已被 MegaMenu 取代，此组件不再使用。保留文件以备将来需要。 -->
<template>
  <!-- 有子菜单 -->
  <a-sub-menu v-if="hasChildren" :key="String(item.id)">
    <template #title>
      <component v-if="iconComponent" :is="iconComponent" />
      <span>{{ item.name }}</span>
    </template>
    <SideMenuItem
      v-for="child in visibleChildren"
      :key="child.id"
      :item="child"
    />
  </a-sub-menu>
  
  <!-- 无子菜单（叶子节点）：只有在有组件路径（即已注册路由）或被 redirect 覆盖时才显示 -->
  <a-menu-item v-else-if="item.type === 'menu' && item.route && item.componentPath" :key="item.route">
    <template #icon>
      <component v-if="iconComponent" :is="iconComponent" />
    </template>
    <span>{{ item.name }}</span>
    <a-badge
      v-if="isQualityException && qualityPendingCount > 0"
      :count="qualityPendingCount"
      :number-style="{ fontSize: '10px', boxShadow: 'none' }"
      style="margin-left: 8px"
    />
  </a-menu-item>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import type { MenuItem } from '@/api/auth'
import * as Icons from '@ant-design/icons-vue'
import { getExceptionCountByStatus } from '@/api/quality'

interface Props {
  item: MenuItem
}

const props = defineProps<Props>()

// 质量异常badge
const isQualityException = computed(() => {
  return props.item.code === 'quality:exception' || props.item.route === '/quality/exceptions'
})

const qualityPendingCount = ref(0)

onMounted(async () => {
  if (isQualityException.value) {
    try {
      const res = await getExceptionCountByStatus()
      if (res.data) {
        qualityPendingCount.value = (res.data.pending || 0) + (res.data.overdue || 0)
      }
    } catch {
      // 静默处理
    }
  }
})

// 过滤出可见的子菜单（type === 'menu'）
const visibleChildren = computed(() => {
  if (!props.item.children) return []
  return props.item.children.filter(c => c.type === 'menu')
})

// 是否有子菜单
const hasChildren = computed(() => visibleChildren.value.length > 0)

// 动态解析图标组件
const iconComponent = computed(() => {
  if (!props.item.icon) return null

  const iconName = props.item.icon

  // 尝试从 @ant-design/icons-vue 中查找图标
  // 1. 直接匹配
  if ((Icons as Record<string, unknown>)[iconName]) {
    return (Icons as Record<string, unknown>)[iconName]
  }

  // 2. 处理 kebab-case: home-outlined -> HomeOutlined
  const pascalCase = iconName
    .split(/[-_]/)
    .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join('')

  // 3. 尝试多种格式匹配
  const tryNames = [
    iconName,
    pascalCase,
    `${pascalCase}Outlined`,
    `${iconName}Outlined`,
  ]

  for (const name of tryNames) {
    if ((Icons as Record<string, unknown>)[name]) {
      return (Icons as Record<string, unknown>)[name]
    }
  }

  // 4. 尝试首字母大写后直接匹配
  const capitalizedName = iconName.charAt(0).toUpperCase() + iconName.slice(1)
  if ((Icons as Record<string, unknown>)[capitalizedName]) {
    return (Icons as Record<string, unknown>)[capitalizedName]
  }
  if ((Icons as Record<string, unknown>)[`${capitalizedName}Outlined`]) {
    return (Icons as Record<string, unknown>)[`${capitalizedName}Outlined`]
  }

  return null
})

// 判断是否有有效的图标组件
const hasIcon = computed(() => !!iconComponent.value)
</script>
