<template>
  <div class="voucher-status-flow">
    <template v-for="(step, idx) in steps" :key="step.key">
      <a-tooltip :title="getDesc(step.key)" :open="getDesc(step.key) ? undefined : false">
        <a-tag
          :color="tagColor(step.key)"
          class="status-tag"
        >
          <CheckOutlined v-if="step.key < currentStep" class="tag-icon" />
          <span>{{ step.title }}</span>
        </a-tag>
      </a-tooltip>
      <span v-if="idx < steps.length - 1" class="arrow">→</span>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { CheckOutlined } from '@ant-design/icons-vue'

interface Props {
  status: number          // 0=草稿, 1=待审, 2=已审, 3=已记账
  createdAt?: string
  createdBy?: string
  auditedAt?: string
  auditedBy?: string
  postedAt?: string
}

const props = withDefaults(defineProps<Props>(), {
  status: 0,
})

const steps = [
  { key: 0, title: '草稿' },
  { key: 1, title: '待审核' },
  { key: 2, title: '已审核' },
  { key: 3, title: '已记账' },
]

const currentStep = computed(() => {
  return Math.max(0, Math.min(props.status, 3))
})

function tagColor(key: number): string {
  if (key < currentStep.value) return 'success'
  if (key === currentStep.value) return 'processing'
  return 'default'
}

function getDesc(key: number): string {
  if (key === 0 && props.createdAt) {
    const parts = [props.createdBy, props.createdAt].filter(Boolean)
    return parts.join(' ')
  }
  if (key === 2 && props.auditedAt) {
    const parts = [props.auditedBy, props.auditedAt].filter(Boolean)
    return parts.join(' ')
  }
  if (key === 3 && props.postedAt) {
    return props.postedAt
  }
  return ''
}
</script>

<style scoped lang="scss">
.voucher-status-flow {
  display: flex;
  align-items: center;
  height: 32px;
  padding: 0;
  gap: 4px;

  .status-tag {
    margin: 0;
    font-size: 12px;
    line-height: 22px;
    cursor: default;

    .tag-icon {
      margin-right: 3px;
      font-size: 11px;
    }
  }

  .arrow {
    color: #d0d0d0;
    font-size: 12px;
    user-select: none;
  }
}
</style>
