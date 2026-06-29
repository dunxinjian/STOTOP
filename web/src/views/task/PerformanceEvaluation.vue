<template>
  <div class="performance-evaluation">
    <!-- 顶部统计 -->
    <a-card :bordered="false" class="stat-card" :loading="dashLoading">
      <a-row :gutter="24">
        <a-col :span="6">
          <a-statistic title="参评人数" :value="dashboard?.totalEmployees ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="已评完" :value="dashboard?.evaluatedCount ?? 0" suffix="人" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="平均分" :value="dashboard?.avgOverallScore ?? 0" :precision="1" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="平均完成率" :value="dashboard?.avgCompletionRate ?? 0" suffix="%" :precision="1" />
        </a-col>
      </a-row>
    </a-card>

    <!-- 等级分布 -->
    <a-card v-if="dashboard && dashboard.gradeDistribution?.length" :bordered="false" title="等级分布" style="margin-top: 16px">
      <div class="grade-distribution">
        <div v-for="item in dashboard.gradeDistribution" :key="item.grade" class="grade-item">
          <div class="grade-label">
            <a-tag :color="gradeColorMap[item.grade] ?? 'default'">{{ item.grade }}</a-tag>
          </div>
          <a-progress
            :percent="item.percentage"
            :stroke-color="gradeColorMap[item.grade]"
            :format="() => `${item.count}人 (${item.percentage.toFixed(1)}%)`"
            style="flex: 1"
          />
        </div>
      </div>
    </a-card>

    <!-- 人员列表 -->
    <a-card :bordered="false" title="考核记录" style="margin-top: 16px">
      <a-table
        :columns="recordColumns"
        :data-source="records"
        :loading="recordLoading"
        row-key="id"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'completionRate'">
            <a-progress :percent="record.completionRate" :size="[80, 6]" />
          </template>
          <template v-else-if="column.dataIndex === 'overallScore'">
            <span :style="{ fontWeight: 600, color: getScoreColor(record.overallScore) }">
              {{ record.overallScore ?? '-' }}
            </span>
          </template>
          <template v-else-if="column.dataIndex === 'grade'">
            <a-tag v-if="record.grade" :color="gradeColorMap[record.grade]">{{ record.grade }}</a-tag>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-badge :status="recordStatusBadge[record.status]" :text="recordStatusText[record.status]" />
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a @click="openDetail(record)">评分详情</a>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 评分详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="currentRecord ? `${currentRecord.employeeName} - 绩效评分` : '评分详情'"
      width="720"
      :body-style="{ paddingBottom: '80px' }"
    >
      <a-spin :spinning="detailLoading">
        <!-- 基本数据概览 -->
        <a-descriptions :column="2" bordered size="small" style="margin-bottom: 20px">
          <a-descriptions-item label="员工">{{ detail?.employeeName }}</a-descriptions-item>
          <a-descriptions-item label="考核周期">{{ detail?.periodName }}</a-descriptions-item>
          <a-descriptions-item label="任务总数">{{ detail?.taskTotal }}</a-descriptions-item>
          <a-descriptions-item label="已完成">{{ detail?.completedCount }}</a-descriptions-item>
          <a-descriptions-item label="按时完成率">{{ detail?.onTimeRate?.toFixed(1) }}%</a-descriptions-item>
          <a-descriptions-item label="目标达成率">{{ detail?.goalAchievementRate?.toFixed(1) }}%</a-descriptions-item>
          <a-descriptions-item label="综合得分">
            <span style="font-size: 18px; font-weight: 700; color: var(--color-info)">{{ detail?.overallScore ?? '-' }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="等级">
            <a-tag v-if="detail?.grade" :color="gradeColorMap[detail.grade]" style="font-size: 16px">{{ detail.grade }}</a-tag>
            <span v-else>-</span>
          </a-descriptions-item>
        </a-descriptions>

        <!-- 维度评分表 -->
        <h4 style="margin-bottom: 12px">各维度评分</h4>
        <a-table
          :columns="scoreColumns"
          :data-source="detail?.dimensionScores ?? []"
          row-key="id"
          :pagination="false"
          size="small"
        >
          <template #bodyCell="{ column, record: scoreRow, index }">
            <template v-if="column.dataIndex === 'weight'">
              {{ scoreRow.weight }}%
            </template>
            <template v-else-if="column.dataIndex === 'score'">
              <span v-if="!isEditing">{{ scoreRow.score ?? '-' }}</span>
              <a-input-number
                v-else
                v-model:value="editScores[index].score"
                :min="0"
                :max="scoreRow.maxScore"
                size="small"
                style="width: 80px"
              />
            </template>
            <template v-else-if="column.dataIndex === 'remark'">
              <span v-if="!isEditing">{{ scoreRow.remark ?? '-' }}</span>
              <a-input
                v-else
                v-model:value="editScores[index].remark"
                size="small"
                placeholder="备注"
              />
            </template>
          </template>
        </a-table>

        <!-- 评语区域 -->
        <div style="margin-top: 20px">
          <template v-if="!isEditing">
            <a-descriptions :column="1" bordered size="small">
              <a-descriptions-item label="自评评语">{{ detail?.selfComment ?? '暂无' }}</a-descriptions-item>
              <a-descriptions-item label="上级评语">{{ detail?.comment ?? '暂无' }}</a-descriptions-item>
            </a-descriptions>
          </template>
          <template v-else>
            <a-form-item v-if="editMode === 'self'" label="自评评语">
              <a-textarea v-model:value="editComment" :rows="3" placeholder="请输入自评评语" />
            </a-form-item>
            <template v-if="editMode === 'review'">
              <a-form-item label="上级评语">
                <a-textarea v-model:value="editComment" :rows="3" placeholder="请输入评语" />
              </a-form-item>
              <a-form-item label="考核等级">
                <a-select v-model:value="editGrade" style="width: 120px">
                  <a-select-option value="S">S（卓越）</a-select-option>
                  <a-select-option value="A">A（优秀）</a-select-option>
                  <a-select-option value="B">B（良好）</a-select-option>
                  <a-select-option value="C">C（合格）</a-select-option>
                  <a-select-option value="D">D（待改进）</a-select-option>
                </a-select>
              </a-form-item>
            </template>
          </template>
        </div>
      </a-spin>

      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px">
          <template v-if="!isEditing">
            <a-button v-if="canSelfEvaluate" @click="startEdit('self')">提交自评</a-button>
            <a-button v-if="canReview" type="primary" @click="startEdit('review')">上级评分</a-button>
          </template>
          <template v-else>
            <a-button @click="cancelEdit">取消</a-button>
            <a-button type="primary" :loading="submitLoading" @click="submitEvaluation">提交</a-button>
          </template>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import type {
  PerformanceRecordListDto,
  PerformanceRecordDetailDto,
  PerformanceDashboardDto,
  DimensionScoreInput,
} from '@/types/task'
import {
  getPerformanceRecords,
  getPerformanceRecordDetail,
  selfEvaluate,
  superiorReview,
  getPerformanceDashboard,
} from '@/api/task'

const route = useRoute()
const periodId = computed(() => Number(route.params.periodId))

type BadgeStatus = 'default' | 'error' | 'warning' | 'success' | 'processing'
const gradeColorMap: Record<string, string> = { S: 'var(--biz-points)', A: 'var(--color-info)', B: 'var(--color-success)', C: 'var(--color-warning)', D: 'var(--color-danger)' }
const recordStatusText: Record<number, string> = { 0: '待自评', 1: '待上级评', 2: '已完成' }
const recordStatusBadge: Record<number, BadgeStatus> = { 0: 'warning', 1: 'processing', 2: 'success' }

// ===== 统计 =====
const dashLoading = ref(false)
const dashboard = ref<PerformanceDashboardDto | null>(null)

async function fetchDashboard() {
  dashLoading.value = true
  try {
    const res = await getPerformanceDashboard(periodId.value)
    dashboard.value = (res as any)?.data ?? res
  } finally {
    dashLoading.value = false
  }
}

// ===== 记录列表 =====
const recordLoading = ref(false)
const records = ref<PerformanceRecordListDto[]>([])
const recordColumns = [
  { title: '姓名', dataIndex: 'employeeName', width: 100 },
  { title: '任务数', dataIndex: 'taskTotal', width: 80 },
  { title: '完成率', dataIndex: 'completionRate', width: 160 },
  { title: '按时率', dataIndex: 'onTimeRate', width: 80, customRender: ({ text }: any) => text != null ? `${text.toFixed(1)}%` : '-' },
  { title: '综合得分', dataIndex: 'overallScore', width: 100 },
  { title: '等级', dataIndex: 'grade', width: 80 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '操作', dataIndex: 'action', width: 100 },
]

async function fetchRecords() {
  recordLoading.value = true
  try {
    const res = await getPerformanceRecords(periodId.value)
    const d = (res as any)?.data ?? res
    records.value = Array.isArray(d) ? d : (d.items ?? [])
  } finally {
    recordLoading.value = false
  }
}

// ===== 评分详情 =====
const drawerVisible = ref(false)
const detailLoading = ref(false)
const currentRecord = ref<PerformanceRecordListDto | null>(null)
const detail = ref<PerformanceRecordDetailDto | null>(null)

const scoreColumns = [
  { title: '维度', dataIndex: 'dimensionName' },
  { title: '编码', dataIndex: 'dimensionCode', width: 100 },
  { title: '权重', dataIndex: 'weight', width: 80 },
  { title: '满分', dataIndex: 'maxScore', width: 70 },
  { title: '得分', dataIndex: 'score', width: 120 },
  { title: '备注', dataIndex: 'remark' },
]

async function openDetail(record: Record<string, any>) {
  currentRecord.value = record as PerformanceRecordListDto
  drawerVisible.value = true
  detailLoading.value = true
  isEditing.value = false
  try {
    const res = await getPerformanceRecordDetail(record.id)
    detail.value = (res as any)?.data ?? res
  } finally {
    detailLoading.value = false
  }
}

// ===== 编辑评分 =====
const isEditing = ref(false)
const editMode = ref<'self' | 'review'>('self')
const editScores = ref<{ dimensionId: number; score: number | undefined; remark: string }[]>([])
const editComment = ref('')
const editGrade = ref('B')
const submitLoading = ref(false)

const canSelfEvaluate = computed(() => detail.value && detail.value.status === 0)
const canReview = computed(() => detail.value && detail.value.status <= 1)

function startEdit(mode: 'self' | 'review') {
  editMode.value = mode
  isEditing.value = true
  editComment.value = mode === 'self' ? (detail.value?.selfComment ?? '') : (detail.value?.comment ?? '')
  editGrade.value = detail.value?.grade ?? 'B'
  editScores.value = (detail.value?.dimensionScores ?? []).map(ds => ({
    dimensionId: ds.dimensionId,
    score: ds.score ?? undefined,
    remark: ds.remark ?? '',
  }))
}

function cancelEdit() {
  isEditing.value = false
}

async function submitEvaluation() {
  const scores: DimensionScoreInput[] = editScores.value.map(s => ({
    dimensionId: s.dimensionId,
    score: s.score ?? 0,
    remark: s.remark,
  }))

  submitLoading.value = true
  try {
    if (editMode.value === 'self') {
      await selfEvaluate(detail.value!.id, { selfComment: editComment.value, dimensionScores: scores })
    } else {
      await superiorReview(detail.value!.id, { comment: editComment.value, grade: editGrade.value, dimensionScores: scores })
    }
    message.success('提交成功')
    isEditing.value = false
    // 刷新详情和列表
    await openDetail(currentRecord.value!)
    fetchRecords()
    fetchDashboard()
  } finally {
    submitLoading.value = false
  }
}

function getScoreColor(score: number | null): string {
  if (score == null) return '#999'
  if (score >= 90) return 'var(--biz-points)'
  if (score >= 80) return 'var(--color-info)'
  if (score >= 70) return 'var(--color-success)'
  if (score >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

onMounted(() => {
  fetchDashboard()
  fetchRecords()
})
</script>

<style scoped lang="scss">
.performance-evaluation {
  .grade-distribution {
    display: flex;
    flex-direction: column;
    gap: 8px;
    .grade-item {
      display: flex;
      align-items: center;
      gap: 12px;
      .grade-label { width: 40px; text-align: center; }
    }
  }
}
</style>
