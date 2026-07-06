<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'
import BusinessStatusComponent from './BusinessStatusComponent.vue'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <BusinessStatusComponent :component="component" default-title="付款信息" default-status="待付款" />
</template>
