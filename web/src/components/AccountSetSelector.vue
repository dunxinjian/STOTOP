<template>
  <div class="account-set-selector">
    <!-- 有账套时正常显示选择器 -->
    <a-select
      v-if="accountSetStore.hasAvailableAccountSets"
      v-model:value="selectedId"
      placeholder="选择账套"
      :loading="accountSetStore.loading"
      @change="handleChange"
      style="width: 100%"
      size="middle"
    >
      <a-select-option
        v-for="item in accountSetStore.accountSets"
        :key="item.id"
        :value="item.id"
      >
        <span>{{ item.fName }}</span>
        <a-tag v-if="item.fIsDefault" color="orange" style="margin-left: 6px; font-size: 11px">默认</a-tag>
      </a-select-option>
    </a-select>
    <!-- 无账套时显示提示 -->
    <a-tooltip v-else-if="!accountSetStore.loading" title="当前组织下没有可用的账套，请切换组织或联系管理员配置">
      <a-select disabled placeholder="当前组织无账套" style="width: 100%" size="middle" />
    </a-tooltip>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()
const selectedId = ref<number>(accountSetStore.currentAccountSetId)

function handleChange(val: any) {
  accountSetStore.setCurrentAccountSet(val as number)
}

// 同步 store 变化
watch(() => accountSetStore.currentAccountSetId, (val) => {
  selectedId.value = val
})

// 监听组织切换，自动刷新账套列表
watch(() => orgContextStore.currentOrgId, async (newOrgId, oldOrgId) => {
  if (newOrgId && newOrgId !== oldOrgId) {
    await accountSetStore.fetchAccountSets()
    selectedId.value = accountSetStore.getCurrentAccountSetId()
  }
})

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  selectedId.value = accountSetStore.getCurrentAccountSetId()
})
</script>

<style scoped>
.account-set-selector {
  display: flex;
  align-items: center;
  width: 100%;
  min-width: 220px;
  max-width: 260px;
}

.account-set-selector :deep(.ant-select) {
  width: 100%;
  font-size: 13px;
}

.account-set-selector :deep(.ant-select-selector) {
  background-color: var(--bg-muted) !important;
  border: 1px solid var(--border) !important;
  color: var(--text-1) !important;
  border-radius: 6px;
}

.account-set-selector :deep(.ant-select-selector:hover) {
  border-color: var(--border-strong) !important;
}

.account-set-selector :deep(.ant-select-focused .ant-select-selector) {
  border-color: var(--color-primary) !important;
  box-shadow: 0 0 0 2px var(--color-primary-border) !important;
}

.account-set-selector :deep(.ant-select-selection-item) {
  color: var(--text-1) !important;
}

.account-set-selector :deep(.ant-select-arrow) {
  color: var(--text-3) !important;
}
</style>
