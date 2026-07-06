<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'
import BusinessStatusComponent from './BusinessStatusComponent.vue'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <BusinessStatusComponent :component="component" default-title="借款冲抵" default-status="未冲抵" />
</template>
