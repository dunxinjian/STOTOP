<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'
import BusinessStatusComponent from './BusinessStatusComponent.vue'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <BusinessStatusComponent :component="component" default-title="预算状态" default-status="待确认" />
</template>
