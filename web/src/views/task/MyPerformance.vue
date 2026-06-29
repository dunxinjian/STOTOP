<template>
  <div class="my-performance">
    <h3>我的绩效</h3>

    <!-- 当前周期概览 -->
    <a-card v-if="currentRecord" :bordered="false" class="current-card">
      <template #title>
        <span>当前考核：{{ currentRecord.periodName }}</span>
        <a-tag v-if="currentRecord.grade" :color="gradeColorMap[currentRecord.grade]" style="margin-left: 8px; font-size: 14px">
          {{ currentRecord.grade }}
        </a-tag>
      </template>
      <template #extra>
        <a-badge :status="statusBadge[currentRecord.status]" :text="statusText[currentRecord.status]" />
      </template>

      <a-row :gutter="24">
        <a-col :span="8">
          <a-statistic title="综合得分" :value="currentRecord.overallScore ?? 0" :precision="1" :value-style="{ color: getScoreColor(currentRecord.overallScore) }" />
        </a-col>
        <a-col :span="8">
          <a-statistic title="完成率" :value="currentRecord.completionRate" suffix="%" :precision="1" />
        </a-col>
        <a-col :span="8">
          <a-statistic title="按时完成率" :value="currentRecord.onTimeRate" suffix="%" :precision="1" />
        </a-col>
      </a-row>

      <!-- 维度得分展示 -->
      <div v-if="currentDetail" class="dimension-scores">
        <h4 style="margin: 20px 0 12px">各维度得分</h4>
        <div class="dim-list">
          <div v-for="ds in currentDetail.dimensionScores" :key="ds.id" class="dim-item">
            <div class="dim-header">
              <span class="dim-name">{{ ds.dimensionName }}</span>
              <span class="dim-weight">权重 {{ ds.weight }}%</span>
            </div>
            <a-progress
              :percent="ds.maxScore > 0 ? ((ds.score ?? 0) / ds.maxScore * 100) : 0"
              :stroke-color="getScoreColor(ds.score)"
              :format="() => `${ds.score ?? '-'} / ${ds.maxScore}`"
            />
            <div v-if="ds.remark" class="dim-remark">{{ ds.remark }}</div>
          </div>
        </div>

        <!-- 评语区域 -->
        <a-descriptions :column="1" bordered size="small" style="margin-top: 16px">
          <a-descriptions-item label="自评评语">{{ currentDetail.selfComment ?? '暂无' }}</a-descriptions-item>
          <a-descriptions-item label="上级评语">{{ currentDetail.comment ?? '暂无' }}</a-descriptions-item>
        </a-descriptions>

        <!-- 自评操作 -->
        <div v-if="currentRecord.status === 0" style="margin-top: 16px">
          <a-button type="primary" @click="openSelfEvaluate">提交自评</a-button>
        </div>
      </div>
    </a-card>

    <a-empty v-if="!loading && !currentRecord && records.length === 0" description="暂无绩效记录" style="margin-top: 40px" />

    <!-- 历史考核记录 -->
    <a-card v-if="historyRecords.length > 0" :bordered="false" title="历史考核记录" style="margin-top: 16px">
      <a-table :columns="historyColumns" :data-source="historyRecords" row-key="id" :pagination="false" size="small">
        <template #bodyCell="{ column, record: row }">
          <template v-if="column.dataIndex === 'completionRate'">
            {{ row.completionRate?.toFixed(1) }}%
          </template>
          <template v-else-if="column.dataIndex === 'overallScore'">
            <span :style="{ fontWeight: 600, color: getScoreColor(row.overallScore) }">
              {{ row.overallScore ?? '-' }}
            </span>
          </template>
          <template v-else-if="column.dataIndex === 'grade'">
            <a-tag v-if="row.grade" :color="gradeColorMap[row.grade]">{{ row.grade }}</a-tag>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-badge :status="statusBadge[row.status]" :text="statusText[row.status]" />
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a @click="viewHistoryDetail(row)">查看详情</a>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 自评弹窗 -->
    <a-modal v-model:open="selfModalVisible" title="提交自评" :confirm-loading="selfSubmitting" width="640" @ok="handleSelfSubmit">
      <a-form layout="vertical">
        <h4>各维度自评打分</h4>
        <a-table
          :columns="selfScoreColumns"
          :data-source="selfScores"
          row-key="dimensionId"
          :pagination="false"
          size="small"
          style="margin-bottom: 16px"
        >
          <template #bodyCell="{ column, record: row, index }">
            <template v-if="column.dataIndex === 'weight'">{{ row.weight }}%</template>
            <template v-else-if="column.dataIndex === 'score'">
              <a-input-number v-model:value="selfScores[index].score" :min="0" :max="row.maxScore" size="small" style="width: 80px" />
            </template>
            <template v-else-if="column.dataIndex === 'remark'">
              <a-input v-model:value="selfScores[index].remark" size="small" placeholder="备注" />
            </template>
          </template>
        </a-table>
        <a-form-item label="自评评语">
          <a-textarea v-model:value="selfComment" :rows="3" placeholder="请输入自评评语" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 历史详情抽屉 -->
    <a-drawer v-model:open="historyDrawerVisible" title="绩效详情" width="600">
      <a-spin :spinning="historyDetailLoading">
        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item label="考核周期">{{ historyDetail?.periodName }}</a-descriptions-item>
          <a-descriptions-item label="综合得分">{{ historyDetail?.overallScore ?? '-' }}</a-descriptions-item>
          <a-descriptions-item label="等级">
            <a-tag v-if="historyDetail?.grade" :color="gradeColorMap[historyDetail.grade]">{{ historyDetail.grade }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="完成率">{{ historyDetail?.completionRate?.toFixed(1) }}%</a-descriptions-item>
        </a-descriptions>

        <h4 style="margin: 16px 0 8px">维度评分</h4>
        <a-table
          :columns="[
            { title: '维度', dataIndex: 'dimensionName' },
            { title: '权重', dataIndex: 'weight', width: 80, customRender: ({ text }: any) => `${text}%` },
            { title: '得分', dataIndex: 'score', width: 80 },
            { title: '满分', dataIndex: 'maxScore', width: 80 },
          ]"
          :data-source="historyDetail?.dimensionScores ?? []"
          row-key="id"
          :pagination="false"
          size="small"
        />

        <a-descriptions :column="1" bordered size="small" style="margin-top: 16px">
          <a-descriptions-item label="自评评语">{{ historyDetail?.selfComment ?? '暂无' }}</a-descriptions-item>
          <a-descriptions-item label="上级评语">{{ historyDetail?.comment ?? '暂无' }}</a-descriptions-item>
        </a-descriptions>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type {
  PerformanceRecordListDto,
  PerformanceRecordDetailDto,
  DimensionScoreInput,
} from '@/types/task'
import {
  getMyPerformance,
  getPerformanceRecordDetail,
  selfEvaluate,
} from '@/api/task'

type BadgeStatus = 'default' | 'error' | 'warning' | 'success' | 'processing'
const gradeColorMap: Record<string, string> = { S: 'var(--biz-points)', A: 'var(--color-info)', B: 'var(--color-success)', C: 'var(--color-warning)', D: 'var(--color-danger)' }
const statusText: Record<number, string> = { 0: '待自评', 1: '待上级评', 2: '已完成' }
const statusBadge: Record<number, BadgeStatus> = { 0: 'warning', 1: 'processing', 2: 'success' }

const loading = ref(false)
const records = ref<PerformanceRecordListDto[]>([])

// 当前周期 = 状态为 0 或 1 的第一条
const currentRecord = computed(() => records.value.find(r => r.status === 0 || r.status === 1) ?? records.value[0] ?? null)
const historyRecords = computed(() => records.value.filter(r => r !== currentRecord.value))
const currentDetail = ref<PerformanceRecordDetailDto | null>(null)

const historyColumns = [
  { title: '考核周期', dataIndex: 'periodName' },
  { title: '完成率', dataIndex: 'completionRate', width: 100 },
  { title: '综合得分', dataIndex: 'overallScore', width: 100 },
  { title: '等级', dataIndex: 'grade', width: 80 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '操作', dataIndex: 'action', width: 100 },
]

async function fetchData() {
  loading.value = true
  try {
    const res = await getMyPerformance()
    const d = (res as any)?.data ?? res
    records.value = Array.isArray(d) ? d : (d.items ?? [])
    // 加载当前周期详情
    if (currentRecord.value) {
      const detRes = await getPerformanceRecordDetail(currentRecord.value.id)
      currentDetail.value = (detRes as any)?.data ?? detRes
    }
  } finally {
    loading.value = false
  }
}

// ===== 自评 =====
const selfModalVisible = ref(false)
const selfSubmitting = ref(false)
const selfComment = ref('')
const selfScores = ref<{ dimensionId: number; dimensionName: string; weight: number; maxScore: number; score: number | undefined; remark: string }[]>([])

const selfScoreColumns = [
  { title: '维度', dataIndex: 'dimensionName' },
  { title: '权重', dataIndex: 'weight', width: 80 },
  { title: '满分', dataIndex: 'maxScore', width: 70 },
  { title: '自评分', dataIndex: 'score', width: 120 },
  { title: '备注', dataIndex: 'remark' },
]

function openSelfEvaluate() {
  if (!currentDetail.value) return
  selfComment.value = currentDetail.value.selfComment ?? ''
  selfScores.value = currentDetail.value.dimensionScores.map(ds => ({
    dimensionId: ds.dimensionId,
    dimensionName: ds.dimensionName ?? '',
    weight: ds.weight,
    maxScore: ds.maxScore,
    score: ds.score ?? undefined,
    remark: ds.remark ?? '',
  }))
  selfModalVisible.value = true
}

async function handleSelfSubmit() {
  const scores: DimensionScoreInput[] = selfScores.value.map(s => ({
    dimensionId: s.dimensionId,
    score: s.score ?? 0,
    remark: s.remark,
  }))
  selfSubmitting.value = true
  try {
    await selfEvaluate(currentDetail.value!.id, { selfComment: selfComment.value, dimensionScores: scores })
    message.success('自评提交成功')
    selfModalVisible.value = false
    fetchData()
  } finally {
    selfSubmitting.value = false
  }
}

// ===== 历史详情 =====
const historyDrawerVisible = ref(false)
const historyDetailLoading = ref(false)
const historyDetail = ref<PerformanceRecordDetailDto | null>(null)

async function viewHistoryDetail(record: Record<string, any>) {
  historyDrawerVisible.value = true
  historyDetailLoading.value = true
  try {
    const res = await getPerformanceRecordDetail(record.id)
    historyDetail.value = (res as any)?.data ?? res
  } finally {
    historyDetailLoading.value = false
  }
}

function getScoreColor(score: number | null | undefined): string {
  if (score == null) return '#999'
  if (score >= 90) return 'var(--biz-points)'
  if (score >= 80) return 'var(--color-info)'
  if (score >= 70) return 'var(--color-success)'
  if (score >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

onMounted(fetchData)
</script>

<style scoped lang="scss">
.my-performance {
  > h3 { margin-bottom: 16px; }

  .dimension-scores {
    .dim-list {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
      gap: 16px;
    }
    .dim-item {
      padding: 12px;
      background: #fafafa;
      border-radius: 6px;
      .dim-header {
        display: flex;
        justify-content: space-between;
        margin-bottom: 6px;
        .dim-name { font-weight: 500; }
        .dim-weight { color: #999; font-size: 12px; }
      }
      .dim-remark { color: #666; font-size: 12px; margin-top: 4px; }
    }
  }
}
</style>
