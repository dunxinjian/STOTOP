<template>
  <div class="page-container">
    <div class="page-card">
      <div class="ti-header">
        <h3 class="ti-title">租户邀请</h3>
        <a-button size="small" :loading="loading" @click="load">刷新</a-button>
      </div>
      <p class="ti-desc">加入其他租户须由该租户成员邀请、并经你在此确认接受后生效。</p>

      <a-spin :spinning="loading">
        <a-empty v-if="!loading && invites.length === 0" description="暂无待确认邀请" />
        <a-list v-else :data-source="invites" :split="true">
          <template #renderItem="{ item }">
            <a-list-item>
              <a-list-item-meta :title="item.tenantName">
                <template #description>
                  <span>邀请人 ID：{{ item.invitedBy ?? '—' }}</span>
                  <span v-if="item.createdAt" class="ti-time">· {{ formatTime(item.createdAt) }}</span>
                </template>
              </a-list-item-meta>
              <template #actions>
                <a-button type="primary" size="small" :loading="acting === item.tenantId" @click="onAccept(item)">接受</a-button>
                <a-button danger size="small" :loading="acting === item.tenantId" @click="onReject(item)">拒绝</a-button>
              </template>
            </a-list-item>
          </template>
        </a-list>
      </a-spin>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { getPendingTenantInvites, acceptTenantInvite, rejectTenantInvite } from '@/api/system'
import type { TenantInviteDto } from '@/types/organization'
import { useTenantContextStore } from '@/stores/tenantContext'

const invites = ref<TenantInviteDto[]>([])
const loading = ref(false)
const acting = ref<number | null>(null)
const tenantStore = useTenantContextStore()

function formatTime(t: string): string {
  const d = new Date(t)
  return Number.isNaN(d.getTime()) ? t : d.toLocaleString()
}

async function load() {
  loading.value = true
  try {
    const res = await getPendingTenantInvites() as any
    invites.value = Array.isArray(res) ? res : (res?.items || [])
  } catch {
    // 静默：拦截器已提示
  } finally {
    loading.value = false
  }
}

async function onAccept(item: TenantInviteDto) {
  acting.value = item.tenantId
  try {
    await acceptTenantInvite(item.tenantId)
    message.success(`已加入租户：${item.tenantName}`)
    await Promise.all([load(), tenantStore.fetchTenants()]) // 刷新邀请与可切换租户列表
  } catch {
    // 拦截器已提示
  } finally {
    acting.value = null
  }
}

async function onReject(item: TenantInviteDto) {
  acting.value = item.tenantId
  try {
    await rejectTenantInvite(item.tenantId)
    message.info(`已拒绝邀请：${item.tenantName}`)
    await load()
  } catch {
    // 拦截器已提示
  } finally {
    acting.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.ti-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.ti-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}

.ti-desc {
  margin: 8px 0 16px;
  font-size: 13px;
  color: var(--text-3);
}

.ti-time {
  margin-left: 8px;
  color: var(--text-3);
}
</style>
