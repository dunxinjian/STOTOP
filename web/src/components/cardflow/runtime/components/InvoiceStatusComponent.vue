<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'
import BusinessStatusComponent from './BusinessStatusComponent.vue'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <BusinessStatusComponent :component="component" default-title="发票状态" default-status="未校验" />
</template>
