<template>
  <div class="contract-status-flow">
    <a-steps :current="currentStep" :status="stepStatus" size="small">
      <a-step title="草稿" :description="getDesc(0)" />
      <a-step title="审批中" :description="getDesc(1)" />
      <a-step title="待签署" :description="getDesc(2)" />
      <a-step title="已生效" :description="getDesc(3)" />
      <a-step :title="terminatedTitle" :description="getDesc(4)" :status="terminatedStatus" />
    </a-steps>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  status: number
  createdAt?: string
  submittedAt?: string
  approvedAt?: string
  signedAt?: string
  effectiveAt?: string
  expiredAt?: string
  terminatedAt?: string
}

const props = withDefaults(defineProps<Props>(), { status: 0 })

const terminatedTitle = computed(() => {
  if (props.status === 5) return '已终止'
  if (props.status === 4) return '已到期'
  return '到期/终止'
})

const terminatedStatus = computed<'wait' | 'finish' | 'error'>(() => {
  if (props.status === 5) return 'error'
  if (props.status === 4) return 'finish'
  return 'wait'
})

const currentStep = computed(() => {
  if (props.status >= 4) return 4
  return props.status
})

const stepStatus = computed<'wait' | 'process' | 'finish' | 'error'>(() => {
  if (props.status === 5) return 'error'
  if (props.status >= 3) return 'finish'
  return 'process'
})

function getDesc(step: number): string {
  const timeMap: Record<number, string | undefined> = {
    0: props.createdAt?.slice(0, 16),
    1: props.submittedAt?.slice(0, 16),
    2: props.approvedAt?.slice(0, 16),
    3: props.effectiveAt?.slice(0, 16) || props.signedAt?.slice(0, 16),
    4: props.terminatedAt?.slice(0, 16) || props.expiredAt?.slice(0, 16),
  }
  return timeMap[step] || ''
}
</script>

<style scoped lang="scss">
.contract-status-flow {
  padding: 8px 16px;
}
</style>
