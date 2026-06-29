<template>
  <div class="point-application-page">
    <PageHeader title="积分申请" />

    <a-card :bordered="false" class="section-card">
      <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
        <a-tab-pane key="submit" tab="提交申请">
          <div class="submit-section">
            <a-button type="primary" @click="showAppForm = true">
              <template #icon><PlusOutlined /></template>
              提交积分申请
            </a-button>
          </div>
        </a-tab-pane>
        <a-tab-pane key="my" tab="我的申请" />
        <a-tab-pane key="pending" tab="待审批" />
      </a-tabs>

      <!-- 我的申请列表 -->
      <template v-if="activeTab === 'my' || activeTab === 'submit'">
        <a-table
          :columns="myColumns"
          :data-source="myList"
          :loading="myLoading"
          :pagination="myPaginationConfig"
          row-key="id"
          bordered
          size="middle"
          @change="handleMyTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'pointValue'">
              <span style="font-weight: 600; color: var(--color-info)">+{{ record.pointValue }}</span>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ formatTime(record.createTime) }}
            </template>
            <template v-if="column.dataIndex === 'approvalTime'">
              {{ formatTime(record.approvalTime) }}
            </template>
          </template>
        </a-table>
      </template>

      <!-- 待审批列表 -->
      <template v-if="activeTab === 'pending'">
        <a-table
          :columns="pendingColumns"
          :data-source="pendingList"
          :loading="pendingLoading"
          :pagination="pendingPaginationConfig"
          row-key="id"
          bordered
          size="middle"
          @change="handlePendingTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'pointValue'">
              <span style="font-weight: 600; color: var(--color-info)">+{{ record.pointValue }}</span>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ formatTime(record.createTime) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-button type="link" size="small" @click="handleApprove(record as PointApplicationListDto)">通过</a-button>
              <a-button type="link" size="small" danger @click="handleReject(record as PointApplicationListDto)">拒绝</a-button>
            </template>
          </template>
        </a-table>
      </template>
    </a-card>

    <!-- 申请弹窗 -->
    <ApplicationForm v-model:open="showAppForm" @success="handleAppSuccess" />

    <!-- 审批弹窗 -->
    <a-modal
      v-model:open="showApproveModal"
      title="审批通过"
      @ok="confirmApprove"
      :confirmLoading="approving"
    >
      <a-form layout="vertical">
        <a-form-item label="审批意见（可选）">
          <a-textarea v-model:value="approvalComment" :rows="3" placeholder="请输入审批意见" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="showRejectModal"
      title="审批拒绝"
      @ok="confirmReject"
      :confirmLoading="rejecting"
    >
      <a-form layout="vertical">
        <a-form-item label="拒绝原因" required>
          <a-textarea v-model:value="rejectReason" :rows="3" placeholder="请输入拒绝原因" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import ApplicationForm from './components/ApplicationForm.vue'
import {
  getMyApplications,
  getPendingApplications,
  approveApplication,
  rejectApplication,
} from '@/api/points'
import type { PointApplicationListDto } from '@/types/points'

const activeTab = ref('submit')
const showAppForm = ref(false)

// 我的申请
const myList = ref<PointApplicationListDto[]>([])
const myLoading = ref(false)
const myTotal = ref(0)
const myPageIndex = ref(1)
const myPageSize = ref(20)

// 待审批
const pendingList = ref<PointApplicationListDto[]>([])
const pendingLoading = ref(false)
const pendingTotal = ref(0)
const pendingPageIndex = ref(1)
const pendingPageSize = ref(20)

// 审批
const showApproveModal = ref(false)
const showRejectModal = ref(false)
const approvalComment = ref('')
const rejectReason = ref('')
const approving = ref(false)
const rejecting = ref(false)
const currentRecord = ref<PointApplicationListDto | null>(null)

const myColumns = [
  { title: '申请时间', dataIndex: 'createTime', width: 160 },
  { title: '规则', dataIndex: 'ruleName', width: 150 },
  { title: '积分', dataIndex: 'pointValue', width: 80 },
  { title: '说明', dataIndex: 'applicationNote', ellipsis: true },
  { title: '状态', dataIndex: 'status', width: 90 },
  { title: '审批人', dataIndex: 'approverName', width: 100 },
  { title: '审批时间', dataIndex: 'approvalTime', width: 160 },
]

const pendingColumns = [
  { title: '申请人', dataIndex: 'applicantName', width: 100 },
  { title: '申请时间', dataIndex: 'createTime', width: 160 },
  { title: '规则', dataIndex: 'ruleName', width: 150 },
  { title: '积分', dataIndex: 'pointValue', width: 80 },
  { title: '说明', dataIndex: 'applicationNote', ellipsis: true },
  { title: '操作', dataIndex: 'action', width: 140, fixed: 'right' as const },
]

const myPaginationConfig = computed(() => ({
  current: myPageIndex.value,
  pageSize: myPageSize.value,
  total: myTotal.value,
  showTotal: (t: number) => `共 ${t} 条`,
  showSizeChanger: true,
}))

const pendingPaginationConfig = computed(() => ({
  current: pendingPageIndex.value,
  pageSize: pendingPageSize.value,
  total: pendingTotal.value,
  showTotal: (t: number) => `共 ${t} 条`,
  showSizeChanger: true,
}))

function statusColor(status: number) {
  return { 0: 'blue', 1: 'green', 2: 'red' }[status] || 'default'
}

function statusText(status: number) {
  return { 0: '待审批', 1: '已通过', 2: '已拒绝' }[status] || '未知'
}

function formatTime(t: string | null) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 16)
}

function handleTabChange() {
  if (activeTab.value === 'my' || activeTab.value === 'submit') {
    loadMyApplications()
  } else if (activeTab.value === 'pending') {
    loadPending()
  }
}

function handleMyTableChange(pagination: any) {
  myPageIndex.value = pagination.current
  myPageSize.value = pagination.pageSize
  loadMyApplications()
}

function handlePendingTableChange(pagination: any) {
  pendingPageIndex.value = pagination.current
  pendingPageSize.value = pagination.pageSize
  loadPending()
}

async function loadMyApplications() {
  myLoading.value = true
  try {
    const res = await getMyApplications({
      pageIndex: myPageIndex.value,
      pageSize: myPageSize.value,
    })
    myList.value = res.items
    myTotal.value = res.total
  } catch {
    message.error('加载我的申请失败')
  } finally {
    myLoading.value = false
  }
}

async function loadPending() {
  pendingLoading.value = true
  try {
    const res = await getPendingApplications({
      pageIndex: pendingPageIndex.value,
      pageSize: pendingPageSize.value,
    })
    pendingList.value = res.items
    pendingTotal.value = res.total
  } catch {
    message.error('加载待审批列表失败')
  } finally {
    pendingLoading.value = false
  }
}

function handleApprove(record: PointApplicationListDto) {
  currentRecord.value = record
  approvalComment.value = ''
  showApproveModal.value = true
}

function handleReject(record: PointApplicationListDto) {
  currentRecord.value = record
  rejectReason.value = ''
  showRejectModal.value = true
}

async function confirmApprove() {
  if (!currentRecord.value) return
  approving.value = true
  try {
    await approveApplication(currentRecord.value.id, {
      approvalComment: approvalComment.value || null,
    })
    message.success('审批通过')
    showApproveModal.value = false
    loadPending()
  } catch {
    message.error('审批失败')
  } finally {
    approving.value = false
  }
}

async function confirmReject() {
  if (!currentRecord.value) return
  if (!rejectReason.value.trim()) {
    message.warning('请填写拒绝原因')
    return
  }
  rejecting.value = true
  try {
    await rejectApplication(currentRecord.value.id, rejectReason.value)
    message.success('已拒绝')
    showRejectModal.value = false
    loadPending()
  } catch {
    message.error('操作失败')
  } finally {
    rejecting.value = false
  }
}

function handleAppSuccess() {
  loadMyApplications()
}

onMounted(() => {
  loadMyApplications()
})
</script>

<style scoped lang="scss">
.point-application-page {
  padding: 0 4px;
}

.section-card {
  border-radius: 8px;
}

.submit-section {
  margin-bottom: 16px;
}
</style>
