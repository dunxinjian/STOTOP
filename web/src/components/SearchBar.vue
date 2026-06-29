<template>
  <a-card class="search-bar" :bordered="false">
    <a-form :model="modelValue" layout="inline" @submit.prevent="$emit('search')">
      <slot />
      <a-form-item>
        <a-button type="primary" @click="$emit('search')">
          <template #icon><SearchOutlined /></template>查询
        </a-button>
        <a-button style="margin-left: 8px" @click="$emit('reset')">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
        <slot name="extra-buttons" />
      </a-form-item>
    </a-form>
  </a-card>
</template>

<script setup lang="ts">
import { SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'

defineProps<{
  /** 搜索表单对象 */
  modelValue: Record<string, any>
}>()

defineEmits<{
  /** 点击查询按钮 */
  (e: 'search'): void
  /** 点击重置按钮 */
  (e: 'reset'): void
}>()
</script>

<style scoped lang="scss">
.search-bar {
  flex: 0 0 auto;
  margin-bottom: 0;
  border-radius: 4px;

  :deep(.ant-card-body) {
    padding: 8px 16px;
    background: #fff;
  }

  :deep(.ant-form) {
    display: flex;
    flex-wrap: wrap;
    align-items: center;

    .ant-form-item {
      margin-bottom: 0;
      margin-right: 12px;

      &:last-child {
        margin-right: 0;
      }
    }
  }
}
</style>
