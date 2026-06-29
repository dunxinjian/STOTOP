<template>
  <div class="page-card-detail">
    <van-nav-bar title="卡片详情" left-arrow @click-left="goBack" />

    <!-- 加载中 -->
    <div v-if="pageLoading" class="loading-container">
      <van-loading size="36px" vertical>加载中...</van-loading>
    </div>

    <!-- 加载失败 -->
    <div v-else-if="loadError" class="error-container">
      <van-empty image="error" description="加载失败">
        <van-button type="primary" size="small" @click="loadDetail">重试</van-button>
      </van-empty>
    </div>

    <!-- 正文 -->
    <div v-else-if="detail" class="detail-body">
      <!-- 基本信息 -->
      <van-cell-group inset class="info-section">
        <van-cell :title="detail.title" :label="detail.flowName" />
        <van-cell title="发起人" :value="detail.applicant" />
        <van-cell title="发起时间" :value="formatDateTime(detail.createdAt)" />
      </van-cell-group>

      <!-- 流程进度 -->
      <div class="steps-section">
        <div class="section-title">流程进度</div>
        <van-steps :active="activeStep" active-color="var(--color-success)">
          <van-step v-for="(node, idx) in detail.steps" :key="idx">
            {{ node.name }}
          </van-step>
        </van-steps>
      </div>

      <!-- 表单内容 -->
      <div class="fields-section">
        <div class="section-title">表单内容</div>
        <CardFields :fields="detail.fields || []" />
      </div>

      <!-- 审批意见 -->
      <div class="comment-section">
        <div class="section-title">审批意见</div>
        <van-field
          v-model="comment"
          type="textarea"
          placeholder="请输入审批意见"
          rows="3"
          autosize
          show-word-limit
          maxlength="200"
        />
      </div>

      <!-- 底部占位（防止被固定栏遮挡） -->
      <div class="bottom-spacer" />
    </div>

    <!-- 审批操作栏 -->
    <ApprovalBar
      v-if="detail && !loadError"
      :loading="actionLoading"
      :can-sign="detail.canSign ?? false"
      @approve="handleApprove"
      @reject="handleReject"
      @sign="handleSign"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  Empty as VanEmpty,
  Button as VanButton,
  Cell as VanCell,
  CellGroup as VanCellGroup,
  Steps as VanSteps,
  Step as VanStep,
  Field as VanField,
  showToast,
  showDialog,
} from 'vant'
import { getCardDetail, approveCard, rejectCard, signCard } from '@shared/api/cardflow'
import { useAuthStore } from '../stores/auth'
import CardFields from '../components/CardFields.vue'
import ApprovalBar from '../components/ApprovalBar.vue'

defineOptions({ name: 'MobileCardDetail' })

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

// 状态
const pageLoading = ref(true)
const loadError = ref(false)
const actionLoading = ref(false)
const comment = ref('')
const detail = ref<any>(null)
const activeStep = ref(0)

// 切换组织上下文
if (route.query.orgId) {
  authStore.setCurrentOrg(Number(route.query.orgId))
}

onMounted(() => {
  loadDetail()
})

async function loadDetail() {
  const id = Number(route.params.id)
  if (!id) {
    loadError.value = true
    pageLoading.value = false
    return
  }

  pageLoading.value = true
  loadError.value = false

  try {
    const res = await getCardDetail(id)
    detail.value = res
    // 计算当前步骤
    if (res.steps?.length) {
      const idx = res.steps.findIndex((s: any) => s.status === 'active' || s.status === 'processing')
      activeStep.value = idx >= 0 ? idx : res.steps.length - 1
    }
  } catch (e) {
    console.error('[CardDetail] 加载失败:', e)
    loadError.value = true
  } finally {
    pageLoading.value = false
  }
}

async function handleApprove() {
  const id = Number(route.params.id)
  actionLoading.value = true
  try {
    await approveCard(id, comment.value)
    showToast({ message: '审批通过', type: 'success' })
    setTimeout(() => router.back(), 800)
  } catch (e: any) {
    showToast({ message: e?.message || '操作失败', type: 'fail' })
  } finally {
    actionLoading.value = false
  }
}

async function handleReject() {
  if (!comment.value.trim()) {
    showToast({ message: '退回时请填写审批意见', type: 'fail' })
    return
  }
  const id = Number(route.params.id)
  actionLoading.value = true
  try {
    await rejectCard(id, comment.value)
    showToast({ message: '已退回', type: 'success' })
    setTimeout(() => router.back(), 800)
  } catch (e: any) {
    showToast({ message: e?.message || '操作失败', type: 'fail' })
  } finally {
    actionLoading.value = false
  }
}

async function handleSign() {
  await showDialog({
    title: '加签',
    message: '加签功能正在开发中',
  })
}

function goBack() {
  router.back()
}

function formatDateTime(value: string): string {
  if (!value) return ''
  const d = new Date(value)
  if (isNaN(d.getTime())) return value
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
</script>

<style scoped lang="scss">
.page-card-detail {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 70px;
}

.loading-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 60vh;
}

.error-container {
  padding-top: 80px;
}

.detail-body {
  padding: 12px 0;
}

.info-section {
  margin-bottom: 12px;
}

.section-title {
  padding: 12px 16px 8px;
  font-size: 14px;
  font-weight: 500;
  color: #323233;
}

.steps-section {
  background: #fff;
  margin: 0 12px 12px;
  border-radius: 8px;
  padding-bottom: 16px;
}

.fields-section {
  margin: 0 12px 12px;
  background: #fff;
  border-radius: 8px;
}

.comment-section {
  margin: 0 12px 12px;
  background: #fff;
  border-radius: 8px;
  padding: 0 0 8px;
}

.bottom-spacer {
  height: 60px;
}
</style>
