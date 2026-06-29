<template>
  <div class="goal-detail-page">
    <PageHeader :title="goal?.title || '目标详情'">
      <template #left>
        <a-button type="text" @click="router.back()"><ArrowLeftOutlined /></a-button>
      </template>
      <template #right>
        <a-button @click="editVisible = true"><EditOutlined /> 编辑</a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <template v-if="goal">
        <!-- 基本信息卡片 -->
        <a-card :bordered="false" class="section-card">
          <a-descriptions :column="3" size="small">
            <a-descriptions-item label="周期">
              <a-tag :color="levelColor(goal.level)">{{ levelLabel(goal.level) }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="statusColor(goal.status)">{{ statusLabel(goal.status) }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="负责人">{{ goal.responsibleName || '-' }}</a-descriptions-item>
            <a-descriptions-item label="起止日期">{{ formatDate(goal.startDate) }} ~ {{ formatDate(goal.endDate) }}</a-descriptions-item>
            <a-descriptions-item label="权重">{{ goal.weight }}%</a-descriptions-item>
            <a-descriptions-item label="创建人">{{ goal.creatorName || '-' }}</a-descriptions-item>
          </a-descriptions>
          <div v-if="goal.description" class="goal-description">
            <span class="label">描述：</span>{{ goal.description }}
          </div>
          <div class="goal-progress-summary">
            <span>总进度</span>
            <a-progress :percent="goal.progress" :stroke-color="progressColor(goal.progress)" style="width: 300px" />
          </div>
        </a-card>

        <!-- KR 列表 -->
        <a-card :bordered="false" class="section-card">
          <template #title>
            <span class="section-title">关键成果 ({{ goal.keyResults?.length || 0 }})</span>
          </template>
          <template #extra>
            <a-button type="primary" size="small" @click="showKrModal()"><PlusOutlined /> 添加 KR</a-button>
          </template>

          <div v-if="goal.keyResults?.length" class="kr-list">
            <div v-for="kr in goal.keyResults" :key="kr.id" class="kr-item">
              <div class="kr-item__header">
                <div class="kr-item__title">
                  <span class="kr-item__sort">KR{{ kr.sort || '' }}</span>
                  {{ kr.title }}
                </div>
                <div class="kr-item__actions">
                  <a-button type="text" size="small" @click="showKrModal(kr)"><EditOutlined /></a-button>
                  <a-popconfirm title="确定删除？" @confirm="handleDeleteKr(kr.id)">
                    <a-button type="text" size="small" danger><DeleteOutlined /></a-button>
                  </a-popconfirm>
                </div>
              </div>
              <div class="kr-item__body">
                <div class="kr-item__progress-row">
                  <span class="kr-item__values">{{ kr.currentValue }} / {{ kr.targetValue }} {{ kr.unit || '' }}</span>
                  <span class="kr-item__weight">权重 {{ kr.weight }}%</span>
                  <span v-if="kr.responsibleName" class="kr-item__owner"><UserOutlined /> {{ kr.responsibleName }}</span>
                </div>
                <div class="kr-item__slider-row">
                  <a-slider
                    :value="kr.currentValue"
                    :min="kr.startValue || 0"
                    :max="kr.targetValue"
                    :tip-formatter="(v?: number) => `${v ?? 0} / ${kr.targetValue}`"
                    style="flex: 1"
                    @afterChange="(v: number | [number, number]) => typeof v === 'number' && handleUpdateKrProgress(kr.id, v)"
                  />
                  <a-input-number
                    :value="kr.currentValue"
                    :min="kr.startValue || 0"
                    :max="kr.targetValue"
                    size="small"
                    style="width: 80px; margin-left: 12px"
                    @change="(v: string | number) => typeof v === 'number' && handleUpdateKrProgress(kr.id, v)"
                  />
                </div>
                <a-progress :percent="kr.progress" :stroke-color="progressColor(kr.progress)" size="small" />
              </div>
            </div>
          </div>
          <a-empty v-else description="暂无关键成果" />
        </a-card>

        <!-- 关联任务 -->
        <a-card :bordered="false" class="section-card">
          <template #title>
            <span class="section-title">关联任务 ({{ relatedTasks.length }})</span>
          </template>
          <a-table
            v-if="relatedTasks.length"
            :columns="taskColumns"
            :data-source="relatedTasks"
            :pagination="false"
            row-key="id"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'priority'">
                <PriorityTag :priority="record.priority" />
              </template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="taskStatusColor(record.status)">{{ taskStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'progress'">
                <a-progress :percent="record.progress" size="small" style="width: 100px" />
              </template>
            </template>
          </a-table>
          <a-empty v-else description="暂无关联任务" />
        </a-card>

        <!-- 目标树 -->
        <a-card :bordered="false" class="section-card">
          <template #title>
            <span class="section-title">目标层级</span>
          </template>
          <template #extra>
          </template>
          <GoalTree v-if="treeData.length" :data="treeData" />
          <a-empty v-else description="暂无层级数据" />
        </a-card>
      </template>
      <EmptyState v-else-if="!loading" title="目标不存在" />
    </a-spin>

    <!-- 编辑目标弹窗 -->
    <a-modal
      v-model:open="editVisible"
      title="编辑目标"
      :confirm-loading="editLoading"
      @ok="handleUpdate"
      width="560px"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="标题" required>
          <a-input v-model:value="editForm.title" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="editForm.description" :rows="3" />
        </a-form-item>
        <a-form-item label="周期" required>
          <a-select v-model:value="editForm.level">
            <a-select-option value="yearly">年度</a-select-option>
            <a-select-option value="quarterly">季度</a-select-option>
            <a-select-option value="monthly">月度</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="起止日期" required>
          <a-range-picker v-model:value="editDateRange" style="width: 100%" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="editForm.status">
            <a-select-option :value="0">草稿</a-select-option>
            <a-select-option :value="1">进行中</a-select-option>
            <a-select-option :value="2">已完成</a-select-option>
            <a-select-option :value="3">已取消</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="权重">
          <a-input-number v-model:value="editForm.weight" :min="0" :max="100" addon-after="%" style="width: 140px" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- KR 弹窗 -->
    <a-modal
      v-model:open="krVisible"
      :title="editingKr ? '编辑关键成果' : '添加关键成果'"
      :confirm-loading="krLoading"
      @ok="handleSaveKr"
      width="520px"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="标题" required>
          <a-input v-model:value="krForm.title" placeholder="KR 标题" />
        </a-form-item>
        <a-form-item label="度量方式">
          <a-select v-model:value="krForm.measureType">
            <a-select-option :value="0">百分比</a-select-option>
            <a-select-option :value="1">数值</a-select-option>
            <a-select-option :value="2">里程碑</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="目标值" required>
          <a-input-number v-model:value="krForm.targetValue" :min="0" style="width: 160px" />
        </a-form-item>
        <a-form-item label="起始值">
          <a-input-number v-model:value="krForm.startValue" :min="0" style="width: 160px" />
        </a-form-item>
        <a-form-item label="单位">
          <a-input v-model:value="krForm.unit" placeholder="如：个、%、万元" style="width: 160px" />
        </a-form-item>
        <a-form-item label="权重">
          <a-input-number v-model:value="krForm.weight" :min="0" :max="100" addon-after="%" style="width: 140px" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="krForm.sort" :min="0" style="width: 100px" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import {
  ArrowLeftOutlined, EditOutlined, PlusOutlined, DeleteOutlined,
  UserOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import PriorityTag from './components/PriorityTag.vue'
import GoalTree from './GoalTree.vue'
import {
  getGoal, updateGoal, getGoalTree, getGoalTasks,
  createKeyResult, updateKeyResult, updateKeyResultProgress, deleteKeyResult,
} from '@/api/task'
import type {
  GoalDetailDto, GoalTreeDto, UpdateGoalRequest,
  KeyResultListDto, CreateKeyResultRequest, UpdateKeyResultRequest,
  TaskListDto,
} from '@/types/task'

const route = useRoute()
const router = useRouter()
const goalId = Number(route.params.goalId)

const loading = ref(false)
const goal = ref<GoalDetailDto | null>(null)
const relatedTasks = ref<TaskListDto[]>([])
const treeData = ref<GoalTreeDto[]>([])

// ---------- 辅助函数 ----------
const levelLabels: Record<string, string> = { yearly: '年度', quarterly: '季度', monthly: '月度' }
const levelColors: Record<string, string> = { yearly: 'blue', quarterly: 'orange', monthly: 'green' }
const statusLabels: Record<number, string> = { 0: '草稿', 1: '进行中', 2: '已完成', 3: '已取消' }
const statusColors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }
const taskStatusLabels: Record<number, string> = { 0: '待处理', 1: '进行中', 2: '已完成', 3: '已取消' }
const taskStatusColors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }

function levelLabel(l: string) { return levelLabels[l] ?? l }
function levelColor(l: string) { return levelColors[l] ?? 'default' }
function statusLabel(s: number) { return statusLabels[s] ?? `${s}` }
function statusColor(s: number) { return statusColors[s] ?? 'default' }
function taskStatusLabel(s: number) { return taskStatusLabels[s] ?? `${s}` }
function taskStatusColor(s: number) { return taskStatusColors[s] ?? 'default' }
function progressColor(p: number) { return p >= 80 ? 'var(--color-success)' : p >= 40 ? 'var(--color-info)' : 'var(--color-warning)' }
function formatDate(d: string) { return d ? d.substring(0, 10) : '' }

const taskColumns = [
  { title: '标题', dataIndex: 'title', ellipsis: true },
  { title: '优先级', dataIndex: 'priority', width: 90 },
  { title: '状态', dataIndex: 'status', width: 90 },
  { title: '进度', dataIndex: 'progress', width: 140 },
  { title: '执行人', dataIndex: 'assigneeName', width: 100 },
]

// ---------- 加载数据 ----------
async function loadGoal() {
  loading.value = true
  try {
    goal.value = await getGoal(goalId)
  } catch {
    message.error('加载目标详情失败')
  } finally {
    loading.value = false
  }
}

async function loadTasks() {
  try {
    relatedTasks.value = await getGoalTasks(goalId)
  } catch { /* ignore */ }
}

async function loadTree() {
  try {
    treeData.value = await getGoalTree()
  } catch { /* ignore */ }
}

// ---------- 编辑目标 ----------
const editVisible = ref(false)
const editLoading = ref(false)
const editDateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const editForm = reactive<Partial<Omit<UpdateGoalRequest, 'description'>> & { description?: string }>({
  title: '',
  description: '',
  level: undefined,
  status: 0,
  weight: 100,
  goalOrgId: 0,
})

function openEdit() {
  if (!goal.value) return
  const g = goal.value
  editForm.title = g.title
  editForm.description = g.description || ''
  editForm.level = g.level
  editForm.status = g.status
  editForm.weight = g.weight
  editForm.goalOrgId = g.goalOrgId
  editDateRange.value = [dayjs(g.startDate), dayjs(g.endDate)]
  editVisible.value = true
}

async function handleUpdate() {
  if (!editForm.title?.trim()) return message.warning('请输入标题')
  if (!editDateRange.value) return message.warning('请选择起止日期')
  editLoading.value = true
  try {
    goal.value = await updateGoal(goalId, {
      title: editForm.title!.trim(),
      description: editForm.description || undefined,
      goalOrgId: editForm.goalOrgId ?? 0,
      level: editForm.level!,
      startDate: editDateRange.value[0].format('YYYY-MM-DD'),
      endDate: editDateRange.value[1].format('YYYY-MM-DD'),
      weight: editForm.weight,
      status: editForm.status!,
    })
    message.success('更新成功')
    editVisible.value = false
  } catch {
    message.error('更新失败')
  } finally {
    editLoading.value = false
  }
}

// ---------- KR 操作 ----------
const krVisible = ref(false)
const krLoading = ref(false)
const editingKr = ref<KeyResultListDto | null>(null)
const krForm = reactive<Partial<Omit<CreateKeyResultRequest, 'unit'> & { sort: number }> & { unit?: string }>({
  title: '',
  measureType: 0,
  targetValue: 100,
  startValue: 0,
  unit: '',
  weight: 100,
  sort: 0,
})

function showKrModal(kr?: KeyResultListDto) {
  editingKr.value = kr ?? null
  if (kr) {
    krForm.title = kr.title
    krForm.measureType = kr.measureType
    krForm.targetValue = kr.targetValue
    krForm.startValue = kr.startValue
    krForm.unit = kr.unit || ''
    krForm.weight = kr.weight
    krForm.sort = kr.sort
  } else {
    krForm.title = ''
    krForm.measureType = 0
    krForm.targetValue = 100
    krForm.startValue = 0
    krForm.unit = ''
    krForm.weight = 100
    krForm.sort = (goal.value?.keyResults?.length ?? 0) + 1
  }
  krVisible.value = true
}

async function handleSaveKr() {
  if (!krForm.title?.trim()) return message.warning('请输入 KR 标题')
  krLoading.value = true
  try {
    if (editingKr.value) {
      await updateKeyResult(editingKr.value.id, {
        title: krForm.title!.trim(),
        measureType: krForm.measureType!,
        targetValue: krForm.targetValue!,
        startValue: krForm.startValue,
        unit: krForm.unit || undefined,
        weight: krForm.weight,
        sort: krForm.sort,
        status: editingKr.value.status,
      } as UpdateKeyResultRequest)
    } else {
      await createKeyResult(goalId, {
        title: krForm.title!.trim(),
        measureType: krForm.measureType!,
        targetValue: krForm.targetValue!,
        startValue: krForm.startValue,
        unit: krForm.unit || undefined,
        weight: krForm.weight,
        sort: krForm.sort,
      })
    }
    message.success(editingKr.value ? '已更新' : '已添加')
    krVisible.value = false
    loadGoal()
  } catch {
    message.error('操作失败')
  } finally {
    krLoading.value = false
  }
}

async function handleUpdateKrProgress(krId: number, value: number) {
  try {
    await updateKeyResultProgress(krId, { currentValue: value })
    loadGoal()
  } catch {
    message.error('更新进度失败')
  }
}

async function handleDeleteKr(krId: number) {
  try {
    await deleteKeyResult(krId)
    message.success('已删除')
    loadGoal()
  } catch {
    message.error('删除失败')
  }
}

// 监听编辑按钮点击（editVisible watch）
import { watch } from 'vue'
watch(editVisible, (v) => { if (v) openEdit() })

onMounted(() => {
  loadGoal()
  loadTasks()
  loadTree()
})
</script>

<style scoped lang="scss">
.goal-detail-page {
  padding: 0 4px;
}

.section-card {
  border-radius: 8px;
  margin-bottom: 16px;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
}

.goal-description {
  margin-top: 12px;
  color: #595959;
  font-size: 14px;

  .label {
    font-weight: 500;
    color: #1a1a1a;
  }
}

.goal-progress-summary {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-top: 12px;
  font-weight: 600;
}

.kr-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.kr-item {
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 12px 16px;
  transition: border-color 0.2s;

  &:hover {
    border-color: var(--color-primary);
  }

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
  }

  &__title {
    font-weight: 600;
    font-size: 14px;
  }

  &__sort {
    display: inline-block;
    background: var(--color-info-light);
    color: var(--color-info);
    font-size: 12px;
    padding: 1px 6px;
    border-radius: 4px;
    margin-right: 6px;
  }

  &__actions {
    flex-shrink: 0;
  }

  &__body {
    margin-top: 8px;
  }

  &__progress-row {
    display: flex;
    gap: 16px;
    font-size: 12px;
    color: #8c8c8c;
    margin-bottom: 4px;

    span {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
  }

  &__values {
    font-weight: 500;
    color: #1a1a1a;
  }

  &__slider-row {
    display: flex;
    align-items: center;
    margin-bottom: 4px;
  }
}
</style>
