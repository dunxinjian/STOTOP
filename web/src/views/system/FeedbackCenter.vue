<template>
  <div class="feedback-page">
    <PageHeader title="反馈中心" description="收集内测用户反馈，并按卡片流程跟进迭代">
      <template #actions>
        <a-button type="primary" @click="openCreate">
          <template #icon><PlusOutlined /></template>
          提交反馈
        </a-button>
      </template>
      <template #toolbar>
        <div class="toolbar">
          <a-input
            v-model:value="filters.keyword"
            placeholder="搜索标题、描述、实际结果"
            allow-clear
            size="small"
            class="keyword"
            @keyup.enter="loadData"
          />
          <a-select v-model:value="filters.module" placeholder="模块" allow-clear size="small" class="filter" :options="moduleOptions" />
          <a-select v-model:value="filters.type" placeholder="类型" allow-clear size="small" class="filter" :options="typeOptions" />
          <a-select v-model:value="filters.severity" placeholder="严重程度" allow-clear size="small" class="filter" :options="severityOptions" />
          <a-switch v-model:checked="filters.mine" size="small" />
          <span class="mine-label">只看我的</span>
          <a-button size="small" type="primary" @click="loadData">查询</a-button>
          <a-button size="small" @click="resetFilters">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <div class="summary-row">
      <button
        v-for="status in statusOptions"
        :key="status.value"
        class="status-chip"
        :class="{ active: selectedStatus === status.value }"
        type="button"
        @click="toggleStatus(status.value)"
      >
        <span class="dot" :style="{ background: status.color }" />
        <span>{{ status.label }}</span>
        <strong>{{ countMap[status.value] || 0 }}</strong>
      </button>
    </div>

    <div class="board" :class="{ loading }">
      <section v-for="status in visibleStatuses" :key="status.value" class="board-column">
        <header class="column-header">
          <span>{{ status.label }}</span>
          <strong>{{ groupedCards[status.value]?.length || 0 }}</strong>
        </header>

        <div class="column-body">
          <button
            v-for="card in groupedCards[status.value] || []"
            :key="card.id"
            type="button"
            class="feedback-card"
            @click="openDetail(card)"
          >
            <div class="card-top">
              <a-tag :color="typeMeta(card.type).color">{{ typeMeta(card.type).label }}</a-tag>
              <a-tag :color="severityMeta(card.severity).color">{{ severityMeta(card.severity).label }}</a-tag>
            </div>
            <div class="card-title">{{ card.title }}</div>
            <div class="card-desc">{{ card.description || card.actualResult || '暂无详细描述' }}</div>
            <div class="card-meta">
              <span>{{ moduleLabel(card.module) }}</span>
              <span>{{ card.submitterName || '未知提交人' }}</span>
            </div>
            <div class="card-meta">
              <span>{{ formatTime(card.updateTime) }}</span>
              <span>{{ card.assigneeName || '未分派' }}</span>
            </div>
          </button>

          <EmptyState v-if="!loading && (groupedCards[status.value]?.length || 0) === 0" description="暂无反馈" />
        </div>
      </section>
    </div>

    <a-modal
      v-model:open="createVisible"
      title="提交反馈"
      width="760px"
      :style="feedbackModalStyle"
      :body-style="feedbackModalBodyStyle"
      :destroy-on-close="true"
    >
      <a-form ref="createFormRef" :model="createForm" :rules="createRules" layout="vertical">
        <div class="form-grid">
          <a-form-item label="反馈标题" name="title" class="span-2">
            <a-input v-model:value="createForm.title" placeholder="一句话说明问题或建议" :maxlength="200" />
          </a-form-item>
          <a-form-item label="类型" name="type">
            <a-select v-model:value="createForm.type" :options="typeOptions" />
          </a-form-item>
          <a-form-item label="所属模块" name="module">
            <a-select v-model:value="createForm.module" show-search :options="moduleOptions" />
          </a-form-item>
          <a-form-item label="严重程度" name="severity">
            <a-select v-model:value="createForm.severity" :options="severityOptions" />
          </a-form-item>
          <a-form-item label="关联版本">
            <a-input v-model:value="createForm.version" placeholder="例如 v2026.04.1" />
          </a-form-item>
          <a-form-item label="描述" class="span-2">
            <a-textarea v-model:value="createForm.description" :rows="3" :maxlength="2000" show-count />
          </a-form-item>
          <a-form-item label="复现步骤" class="span-2">
            <a-textarea v-model:value="createForm.reproduceSteps" :rows="3" :maxlength="2000" show-count />
          </a-form-item>
          <a-form-item label="期望结果">
            <a-textarea v-model:value="createForm.expectedResult" :rows="3" :maxlength="1000" />
          </a-form-item>
          <a-form-item label="实际结果">
            <a-textarea v-model:value="createForm.actualResult" :rows="3" :maxlength="1000" />
          </a-form-item>
          <a-form-item label="附件链接" class="span-2">
            <a-textarea v-model:value="createForm.attachmentLinks" :rows="2" placeholder="截图、录屏或文件链接，可多行填写" />
          </a-form-item>
        </div>
      </a-form>
      <template #footer>
        <a-button @click="createVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitting" @click="submitCreate">提交</a-button>
      </template>
    </a-modal>

    <a-drawer v-model:open="detailVisible" :title="currentDetail?.title || '反馈详情'" width="720px" :destroy-on-close="true">
      <template v-if="currentDetail">
        <div class="detail-head">
          <a-tag :color="statusMeta(currentDetail.status).color">{{ statusMeta(currentDetail.status).label }}</a-tag>
          <a-tag :color="typeMeta(currentDetail.type).color">{{ typeMeta(currentDetail.type).label }}</a-tag>
          <a-tag :color="severityMeta(currentDetail.severity).color">{{ severityMeta(currentDetail.severity).label }}</a-tag>
          <span>{{ moduleLabel(currentDetail.module) }}</span>
        </div>

        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item label="提交人">{{ currentDetail.submitterName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="负责人">{{ currentDetail.assigneeName || '未分派' }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatTime(currentDetail.createTime) }}</a-descriptions-item>
          <a-descriptions-item label="更新时间">{{ formatTime(currentDetail.updateTime) }}</a-descriptions-item>
          <a-descriptions-item label="页面地址" :span="2">{{ currentDetail.pageUrl || '-' }}</a-descriptions-item>
          <a-descriptions-item label="描述" :span="2">{{ currentDetail.description || '-' }}</a-descriptions-item>
          <a-descriptions-item label="复现步骤" :span="2">{{ currentDetail.reproduceSteps || '-' }}</a-descriptions-item>
          <a-descriptions-item label="期望结果">{{ currentDetail.expectedResult || '-' }}</a-descriptions-item>
          <a-descriptions-item label="实际结果">{{ currentDetail.actualResult || '-' }}</a-descriptions-item>
          <a-descriptions-item label="附件链接" :span="2">{{ currentDetail.attachmentLinks || '-' }}</a-descriptions-item>
          <a-descriptions-item label="处理结论" :span="2">{{ currentDetail.conclusion || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div v-if="has(SystemPermissions.FeedbackManage)" class="manage-panel">
          <a-space wrap>
            <a-select
              v-model:value="assignForm.assigneeId"
              show-search
              allow-clear
              placeholder="负责人"
              style="width: 180px"
              :filter-option="false"
              @search="searchUsers"
            >
              <a-select-option v-for="user in userOptions" :key="user.id" :value="user.id">{{ user.name }}</a-select-option>
            </a-select>
            <a-button :loading="assigning" @click="submitAssign">分派</a-button>
            <a-select v-model:value="transitionForm.status" placeholder="下一状态" style="width: 160px" :options="nextStatusOptions" />
            <a-button type="primary" :loading="transitioning" @click="submitTransition">流转</a-button>
          </a-space>
          <a-textarea v-model:value="transitionForm.comment" :rows="2" placeholder="流转说明或分派备注" />
          <a-textarea v-model:value="transitionForm.conclusion" :rows="2" placeholder="处理结论，关闭时建议填写" />
        </div>

        <div class="comment-panel">
          <a-textarea v-model:value="commentText" :rows="3" placeholder="补充说明或验证结果" />
          <a-button type="primary" :loading="commenting" @click="submitComment">添加评论</a-button>
        </div>

        <div class="timeline-title">处理动态</div>
        <a-timeline>
          <a-timeline-item v-for="item in currentDetail.activities" :key="item.id">
            <div class="activity-line">
              <strong>{{ item.actorName || '未知用户' }}</strong>
              <span>{{ actionLabel(item) }}</span>
              <small>{{ formatTime(item.createTime) }}</small>
            </div>
            <div v-if="item.content" class="activity-content">{{ item.content }}</div>
          </a-timeline-item>
        </a-timeline>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { usePermission, SystemPermissions } from '@/utils/permission'
import { getUserList, type UserItem } from '@/api/system'
import {
  addFeedbackComment,
  assignFeedback,
  createFeedback,
  getFeedbackBoard,
  getFeedbackCounts,
  getFeedbackDetail,
  transitionFeedback,
} from '@/api/feedback'
import type { FeedbackActivityDto, FeedbackCardDto, FeedbackDetailDto } from '@/types/feedback'

const { has } = usePermission()

const statusOptions = [
  { value: 0, label: '新反馈', color: '#64748b' },
  { value: 1, label: '待复现', color: 'var(--color-info)' },
  { value: 2, label: '已确认', color: 'var(--color-info-text)' },
  { value: 3, label: '已排期', color: 'var(--biz-approval)' },
  { value: 4, label: '开发中', color: 'var(--color-warning)' },
  { value: 5, label: '待验证', color: 'var(--color-primary)' },
  { value: 6, label: '已关闭', color: 'var(--color-success)' },
]

const typeOptions = [
  { value: 1, label: 'Bug', color: 'red' },
  { value: 2, label: '易用性', color: 'orange' },
  { value: 3, label: '需求', color: 'blue' },
  { value: 4, label: '数据问题', color: 'purple' },
  { value: 5, label: '权限问题', color: 'cyan' },
]

const severityOptions = [
  { value: 1, label: '阻塞', color: 'red' },
  { value: 2, label: '高', color: 'orange' },
  { value: 3, label: '中', color: 'blue' },
  { value: 4, label: '低', color: 'default' },
]

const moduleOptions = [
  { value: 'express', label: '快递' },
  { value: 'finance', label: '财务' },
  { value: 'oa', label: 'OA审批' },
  { value: 'cardflow', label: 'CardFlow' },
  { value: 'system', label: '系统管理' },
  { value: 'workhub', label: '工作台' },
  { value: 'crm', label: '客户关系' },
  { value: 'mobile', label: '移动端' },
  { value: 'other', label: '其他' },
]

const filters = reactive({
  keyword: '',
  module: undefined as string | undefined,
  type: undefined as number | undefined,
  severity: undefined as number | undefined,
  mine: false,
})

const loading = ref(false)
const cards = ref<FeedbackCardDto[]>([])
const counts = ref<Record<number, number>>({})
const selectedStatus = ref<number | null>(null)

const countMap = computed(() => counts.value)
const visibleStatuses = computed(() => selectedStatus.value === null
  ? statusOptions
  : statusOptions.filter(s => s.value === selectedStatus.value))

const groupedCards = computed<Record<number, FeedbackCardDto[]>>(() => {
  const grouped: Record<number, FeedbackCardDto[]> = {}
  for (const status of statusOptions) grouped[status.value] = []
  for (const card of cards.value) {
    if (!grouped[card.status]) grouped[card.status] = []
    grouped[card.status].push(card)
  }
  return grouped
})

const createVisible = ref(false)
const createFormRef = ref<FormInstance>()
const submitting = ref(false)
const feedbackModalStyle = { top: '24px' }
const feedbackModalBodyStyle = {
  maxHeight: 'calc(100vh - 190px)',
  overflowY: 'auto',
} as const
const createForm = reactive({
  title: '',
  type: 1,
  module: 'system',
  severity: 3,
  version: '',
  description: '',
  reproduceSteps: '',
  expectedResult: '',
  actualResult: '',
  attachmentLinks: '',
})

const createRules: any = {
  title: [{ required: true, message: '请输入反馈标题', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }],
  module: [{ required: true, message: '请选择模块', trigger: 'change' }],
  severity: [{ required: true, message: '请选择严重程度', trigger: 'change' }],
}

const detailVisible = ref(false)
const currentDetail = ref<FeedbackDetailDto | null>(null)
const userOptions = ref<UserItem[]>([])
const assignForm = reactive({ assigneeId: undefined as number | undefined })
const transitionForm = reactive({
  status: undefined as number | undefined,
  comment: '',
  conclusion: '',
})
const assigning = ref(false)
const transitioning = ref(false)
const commentText = ref('')
const commenting = ref(false)

const nextStatusOptions = computed(() => {
  if (!currentDetail.value) return []
  const map: Record<number, number[]> = {
    0: [1, 2, 6],
    1: [2, 6],
    2: [3, 4, 6],
    3: [4, 6],
    4: [5, 6],
    5: [4, 6],
    6: [],
  }
  return (map[currentDetail.value.status] || []).map(value => ({ value, label: statusMeta(value).label }))
})

function statusMeta(value: number) {
  return statusOptions.find(item => item.value === value) || statusOptions[0]
}

function typeMeta(value: number) {
  return typeOptions.find(item => item.value === value) || typeOptions[0]
}

function severityMeta(value: number) {
  return severityOptions.find(item => item.value === value) || severityOptions[2]
}

function moduleLabel(value: string) {
  return moduleOptions.find(item => item.value === value)?.label || value
}

function formatTime(value: string | null) {
  if (!value) return '-'
  return value.replace('T', ' ').substring(0, 16)
}

function toggleStatus(value: number) {
  selectedStatus.value = selectedStatus.value === value ? null : value
  loadData()
}

function buildParams() {
  return {
    keyword: filters.keyword || undefined,
    module: filters.module || undefined,
    type: filters.type ?? undefined,
    severity: filters.severity ?? undefined,
    status: selectedStatus.value ?? undefined,
    mine: filters.mine,
  }
}

async function loadData() {
  loading.value = true
  try {
    const params = buildParams()
    const [board, statusCounts] = await Promise.all([
      getFeedbackBoard(params),
      getFeedbackCounts({ ...params, status: undefined }),
    ])
    cards.value = board || []
    counts.value = Object.fromEntries((statusCounts || []).map(item => [item.status, item.count]))
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  filters.keyword = ''
  filters.module = undefined
  filters.type = undefined
  filters.severity = undefined
  filters.mine = false
  selectedStatus.value = null
  loadData()
}

function openCreate() {
  createForm.title = ''
  createForm.type = 1
  createForm.module = inferModule()
  createForm.severity = 3
  createForm.version = ''
  createForm.description = ''
  createForm.reproduceSteps = ''
  createForm.expectedResult = ''
  createForm.actualResult = ''
  createForm.attachmentLinks = ''
  createVisible.value = true
}

function inferModule() {
  const segment = window.location.pathname.split('/')[1]
  return moduleOptions.some(item => item.value === segment) ? segment : 'system'
}

async function submitCreate() {
  if (!createFormRef.value) return
  try { await createFormRef.value.validate() } catch { return }
  submitting.value = true
  try {
    const detail = await createFeedback({
      ...createForm,
      pageUrl: window.location.href,
      clientInfo: navigator.userAgent,
    })
    message.success('反馈已提交')
    createVisible.value = false
    await loadData()
    if (detail?.id) await openDetail(detail)
  } finally {
    submitting.value = false
  }
}

async function openDetail(record: FeedbackCardDto) {
  const detail = await getFeedbackDetail(record.id)
  currentDetail.value = detail
  assignForm.assigneeId = detail.assigneeId || undefined
  transitionForm.status = undefined
  transitionForm.comment = ''
  transitionForm.conclusion = detail.conclusion || ''
  commentText.value = ''
  detailVisible.value = true
  if (detail.assigneeId && detail.assigneeName) {
    userOptions.value = [{ id: detail.assigneeId, name: detail.assigneeName, account: '', status: 1 }]
  }
}

async function searchUsers(keyword: string) {
  const res = await getUserList({ keyword, pageIndex: 1, pageSize: 20 }) as any
  userOptions.value = res?.items || res || []
}

async function submitAssign() {
  if (!currentDetail.value) return
  assigning.value = true
  try {
    const detail = await assignFeedback(currentDetail.value.id, {
      assigneeId: assignForm.assigneeId || null,
      comment: transitionForm.comment || null,
    })
    currentDetail.value = detail
    message.success('已分派')
    await loadData()
  } finally {
    assigning.value = false
  }
}

async function submitTransition() {
  if (!currentDetail.value || transitionForm.status === undefined) {
    message.warning('请选择下一状态')
    return
  }
  transitioning.value = true
  try {
    const detail = await transitionFeedback(currentDetail.value.id, {
      status: transitionForm.status,
      comment: transitionForm.comment || null,
      conclusion: transitionForm.conclusion || null,
    })
    currentDetail.value = detail
    transitionForm.status = undefined
    transitionForm.comment = ''
    message.success('状态已更新')
    await loadData()
  } finally {
    transitioning.value = false
  }
}

async function submitComment() {
  if (!currentDetail.value || !commentText.value.trim()) {
    message.warning('请输入评论内容')
    return
  }
  commenting.value = true
  try {
    await addFeedbackComment(currentDetail.value.id, { content: commentText.value.trim() })
    currentDetail.value = await getFeedbackDetail(currentDetail.value.id)
    commentText.value = ''
    message.success('评论已添加')
    await loadData()
  } finally {
    commenting.value = false
  }
}

function actionLabel(item: FeedbackActivityDto) {
  if (item.action === 'transition') return `将状态从 ${statusMeta(item.fromStatus ?? 0).label} 流转到 ${statusMeta(item.toStatus ?? 0).label}`
  if (item.action === 'assign') return '分派反馈'
  if (item.action === 'comment') return '添加评论'
  if (item.action === 'update') return '更新反馈'
  return '提交反馈'
}

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="scss">
.feedback-page {
  min-height: 100%;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  width: 100%;
  flex-wrap: wrap;

  .keyword { width: 240px; }
  .filter { width: 120px; }
}

.mine-label {
  font-size: 12px;
  color: #667085;
}

.summary-row {
  display: flex;
  gap: 8px;
  padding: 12px 16px;
  overflow-x: auto;
  border-bottom: 1px solid #edf0f5;
  background: #fff;
}

.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 32px;
  padding: 0 12px;
  border: 1px solid #d9dee8;
  border-radius: 6px;
  background: #fff;
  color: #344054;
  white-space: nowrap;
  cursor: pointer;

  &.active {
    border-color: var(--border-strong);
    color: var(--text-1);
    background: var(--bg-muted);
  }

  .dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
  }
}

.board {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(280px, 1fr);
  gap: 12px;
  padding: 16px;
  overflow-x: auto;

  &.loading {
    opacity: 0.65;
  }
}

.board-column {
  min-height: 520px;
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #f8fafc;
}

.column-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 42px;
  padding: 0 12px;
  border-bottom: 1px solid #e6eaf0;
  font-weight: 600;
  color: #1f2937;
}

.column-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 10px;
}

.feedback-card {
  display: block;
  width: 100%;
  min-height: 156px;
  padding: 12px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s;

  &:hover {
    border-color: var(--color-primary);
    box-shadow: 0 6px 18px rgba(15, 23, 42, 0.08);
  }
}

.card-top,
.card-meta,
.detail-head {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.card-title {
  margin-top: 8px;
  font-size: 14px;
  font-weight: 600;
  color: #111827;
  line-height: 1.45;
}

.card-desc {
  height: 42px;
  margin-top: 6px;
  color: #667085;
  font-size: 12px;
  line-height: 1.5;
  overflow: hidden;
}

.card-meta {
  justify-content: space-between;
  margin-top: 8px;
  color: #8a94a6;
  font-size: 12px;
}

.form-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0 16px;
}

.span-2 {
  grid-column: span 2;
}

.detail-head {
  margin-bottom: 14px;
}

.manage-panel,
.comment-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 16px;
  padding: 12px;
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #fbfcfe;
}

.comment-panel {
  align-items: flex-start;
}

.timeline-title {
  margin: 20px 0 12px;
  font-weight: 600;
  color: #1f2937;
}

.activity-line {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;

  small {
    color: #98a2b3;
  }
}

.activity-content {
  margin-top: 4px;
  color: #667085;
  white-space: pre-wrap;
}

@media (max-width: 768px) {
  .toolbar {
    justify-content: flex-start;

    .keyword,
    .filter {
      width: 100%;
    }
  }

  .form-grid {
    grid-template-columns: 1fr;
  }

  .span-2 {
    grid-column: span 1;
  }
}
</style>
