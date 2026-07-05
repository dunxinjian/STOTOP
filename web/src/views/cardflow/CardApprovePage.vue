<template>
  <div class="card-approve-page">
    <div v-if="loading" class="cap-state">
      <a-spin tip="加载中..." />
    </div>
    <a-result
      v-else-if="loadError"
      status="error"
      title="卡片加载失败"
      sub-title="请检查卡片是否存在或稍后重试"
    >
      <template #extra>
        <a-button type="primary" @click="resolvePanelMode">重试</a-button>
        <a-button @click="goBack">返回</a-button>
      </template>
    </a-result>

    <CardFlowPanel
      v-model:visible="panelVisible"
      :card-id="cardId"
      :mode="panelMode"
      @closed="goBack"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import CardFlowPanel from '@/components/cardflow/CardFlowPanel.vue'
import { getCard } from '@/api/cardflow'
import { useUserStore } from '@/stores/user'
import type { CardDetailDto } from '@/types/cardflow'

type PanelMode = 'approval' | 'readonly' | 'initiator'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const cardId = computed(() => Number(route.params.id))
const loading = ref(true)
const loadError = ref(false)
const panelVisible = ref(false)
const panelMode = ref<PanelMode>('readonly')

/**
 * 模式自适应：
 * - 当前用户在当前节点有 pending 待办 → approval
 * - 是发起人且卡片 returned → initiator（可重新提交/废除）
 * - 其余 → readonly
 */
async function resolvePanelMode() {
  loading.value = true
  loadError.value = false
  try {
    if (!userStore.userInfo) {
      await userStore.fetchUserInfo().catch(() => undefined)
    }
    const card = (await getCard(cardId.value)) as CardDetailDto
    const myId = userStore.userInfo?.id
    const currentStage =
      card.stageInstances?.find((s) => s.id === card.currentStageInstanceId) || null
    const isPendingAssignee =
      !!myId &&
      card.status === 'active' &&
      !!currentStage &&
      currentStage.assignees.some((a) => a.userId === myId && a.status === 'pending')
    const isInitiator = !!myId && card.initiatorId === myId

    if (isPendingAssignee) {
      panelMode.value = 'approval'
    } else if (isInitiator && card.status === 'returned') {
      panelMode.value = 'initiator'
    } else {
      panelMode.value = 'readonly'
    }
    panelVisible.value = true
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
}

function goBack() {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/workhub')
  }
}

onMounted(() => {
  // 移动端仍走独立审批页
  const isMobile =
    /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) || window.innerWidth < 768
  if (isMobile) {
    router.replace(`/m/cardflow/approval/${route.params.id}`)
    return
  }
  resolvePanelMode()
})
</script>

<style scoped>
.card-approve-page {
  width: 100%;
  min-height: calc(100vh - 96px);
  padding: 24px;
}

.cap-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
}
</style>
