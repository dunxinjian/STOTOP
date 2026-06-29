<template>
  <a-card
    class="stat-card"
    :bordered="false"
    :hoverable="clickable"
    @click="clickable ? $emit('click') : undefined"
  >
    <a-skeleton :loading="loading" active :paragraph="{ rows: 2 }">
      <div class="stat-card-title">{{ title }}</div>
      <a-statistic
        :value="value"
        :prefix="prefix"
        :suffix="suffix"
        :precision="precision"
        :value-style="{ fontSize: '28px', fontWeight: 600 }"
      />
      <div v-if="change !== undefined" class="stat-card-change" :class="change >= 0 ? 'change-up' : 'change-down'">
        <ArrowUpOutlined v-if="change >= 0" />
        <ArrowDownOutlined v-else />
        {{ Math.abs(change).toFixed(1) }}% 较上月
      </div>
    </a-skeleton>
  </a-card>
</template>

<script setup lang="ts">
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'

withDefaults(defineProps<{
  title: string
  value: number
  prefix?: string
  suffix?: string
  change?: number
  loading?: boolean
  precision?: number
  clickable?: boolean
}>(), {
  precision: 0,
  loading: false,
  clickable: false,
})

defineEmits<{
  click: []
}>()
</script>

<style scoped>
.stat-card {
  height: 120px;
  border-radius: var(--radius-lg);
}

.stat-card-title {
  font-size: var(--font-base);
  color: var(--text-3);
  margin-bottom: var(--space-sm8);
}

.stat-card-change {
  font-size: 12px;
  margin-top: 4px;
}

.change-up {
  color: var(--color-success);
}

.change-down {
  color: var(--color-danger);
}
</style>
