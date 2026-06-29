<template>
  <div class="page-container">
    <PageHeader title="数据库连接管理" description="管理系统数据库连接配置" />

    <a-card :bordered="false">
      <a-empty description="功能开发中">
        <template #image>
          <LinkOutlined :style="{ fontSize: '48px', color: '#d9d9d9' }" />
        </template>
        <a-typography-text type="secondary">
          数据库连接管理功能正在开发中，敬请期待。
        </a-typography-text>
      </a-empty>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { LinkOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
