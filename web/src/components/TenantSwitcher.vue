<template>
  <!-- 多租户（阶段4F·M9）：仅当用户属多个租户时显示；单客户下隐藏（此为休眠能力）。 -->
  <div v-if="tenantStore.hasMultipleTenants" :class="rootClass">
    <a-popover
      v-model:open="popoverOpen"
      placement="bottomLeft"
      trigger="click"
      overlay-class-name="tenant-switcher-popover"
    >
      <template #content>
        <div class="tenant-list">
          <div class="tenant-list-header">切换租户</div>
          <div
            v-for="t in tenantStore.tenants"
            :key="t.tenantId"
            class="tenant-item"
            :class="{ active: t.tenantId === tenantStore.currentTenantId }"
            @click="handleSwitch(t)"
          >
            <div class="tenant-item-main">
              <ClusterOutlined :style="{ fontSize: '14px' }" />
              <span class="tenant-item-name">{{ t.tenantName }}</span>
            </div>
            <div class="tenant-item-tags">
              <a-tag v-if="t.isPrimary" color="blue" size="small">主租户</a-tag>
            </div>
            <CheckOutlined v-if="t.tenantId === tenantStore.currentTenantId" class="check-icon" />
          </div>
        </div>
      </template>

      <div
        class="tenant-current"
        role="button"
        tabindex="0"
        aria-label="切换租户"
        aria-haspopup="menu"
        :aria-expanded="popoverOpen"
        @keydown.enter="popoverOpen = !popoverOpen"
        @keydown.space.prevent="popoverOpen = !popoverOpen"
      >
        <span class="tenant-icon-circle">
          <ClusterOutlined :style="{ fontSize: '13px' }" />
        </span>
        <span class="tenant-name">{{ tenantStore.currentTenantName || '选择租户' }}</span>
        <CaretDownOutlined class="tenant-arrow" />
      </div>
    </a-popover>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ClusterOutlined, CheckOutlined, CaretDownOutlined } from '@ant-design/icons-vue'
import { useTenantContextStore } from '@/stores/tenantContext'
import { useOrgContextStore } from '@/stores/orgContext'
import type { TenantMembershipDto } from '@/types/organization'

const props = withDefaults(defineProps<{
  mode?: 'dark' | 'light'
}>(), {
  mode: 'dark',
})

const tenantStore = useTenantContextStore()
const orgContextStore = useOrgContextStore()
const popoverOpen = ref(false)

const rootClass = computed(() => ({
  'tenant-switcher': true,
  'tenant-switcher--dark': props.mode === 'dark',
}))

async function handleSwitch(t: TenantMembershipDto) {
  if (t.tenantId === tenantStore.currentTenantId) {
    popoverOpen.value = false
    return
  }
  const data = await tenantStore.doSwitchTenant(t.tenantId)
  if (data) {
    // 组织归属属旧租户，清空组织上下文 → reload 后由中间件在新租户内重选组织。
    // （switch-tenant 已返回本租户自动选定组织 data.context，可后续精修为直接套用免一次重选。）
    orgContextStore.clearOrgContext()
    popoverOpen.value = false
    // 租户切换 = 整体上下文重置，硬重载最稳（以新 X-Tenant-Context 重新引导应用）。
    window.location.reload()
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.tenant-switcher {
  display: flex;
  align-items: center;
}

.tenant-switcher .tenant-current {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
  max-width: 200px;

  .tenant-icon-circle {
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    color: var(--text-2);
  }

  .tenant-name {
    font-size: 13px;
    font-weight: 600;
    max-width: 160px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tenant-arrow {
    font-size: 10px;
    transition: transform 0.3s;
    flex-shrink: 0;
  }
}

.tenant-switcher--dark {
  .tenant-current {
    color: rgba(255, 255, 255, 0.85);

    &:hover {
      color: rgba(255, 255, 255, 1);
      background: rgba(255, 255, 255, 0.08);
    }

    .tenant-icon-circle {
      color: rgba(255, 255, 255, 0.55);
    }

    .tenant-name {
      color: rgba(255, 255, 255, 0.85);
    }

    .tenant-arrow {
      color: rgba(255, 255, 255, 0.5);
    }
  }
}

.tenant-list {
  min-width: 220px;

  .tenant-list-header {
    font-size: 12px;
    color: $text-secondary;
    padding: 4px 8px 8px;
    border-bottom: 1px solid $border-color-lighter;
    margin-bottom: 4px;
  }

  .tenant-item {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    padding: 8px 10px;
    border-radius: 4px;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      background-color: $bg-page;
    }

    &.active {
      background-color: var(--bg-muted);
      color: var(--text-1);
    }

    .tenant-item-main {
      display: flex;
      align-items: center;
      gap: 6px;
      flex: 1;
      min-width: 0;

      .tenant-item-name {
        font-size: 14px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
    }

    .tenant-item-tags {
      display: flex;
      gap: 4px;
    }

    .check-icon {
      color: var(--text-1);
      margin-left: auto;
    }
  }
}
</style>
