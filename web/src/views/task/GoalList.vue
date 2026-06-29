<template>
  <div class="goal-list-page">
    <PageHeader title="目标管理">
      <template #right>
        <a-radio-group v-model:value="viewMode" size="small" button-style="solid">
          <a-radio-button value="card"><AppstoreOutlined /> 卡片</a-radio-button>
          <a-radio-button value="list"><UnorderedListOutlined /> 列表</a-radio-button>
        </a-radio-group>
        <a-button type="primary" @click="showCreateModal">
          <PlusOutlined /> 新增目标
        </a-button>
      </template>
    </PageHeader>

    <!-- 筛选栏 -->
    <div class="filter-bar">
      <a-select
        v-model:value="filters.level"
        placeholder="周期"
        allow-clear
        style="width: 120px"
        @change="() => loadGoals()"
      >
        <a-select-option value="yearly">年度</a-select-option>
        <a-select-option value="quarterly">季度</a-select-option>
        <a-select-option value="monthly">月度</a-select-option>
      </a-select>
      <a-select
        v-model:value="filters.status"
        placeholder="状态"
        allow-clear
        style="width: 120px"
        @change="() => loadGoals()"
      >
        <a-select-option :value="0">草稿</a-select-option>
        <a-select-option :value="1">进行中</a-select-option>
        <a-select-option :value="2">已完成</a-select-option>
        <a-select-option :value="3">已取消</a-select-option>
      </a-select>
      <a-input-search
        v-model:value="filters.keyword"
        placeholder="搜索目标"
        allow-clear
        style="width: 240px"
        @search="() => loadGoals()"
      />
    </div>

    <a-spin :spinning="loading">
      <!-- 卡片视图 -->
      <template v-if="viewMode === 'card'">
        <a-row :gutter="[16, 16]" v-if="goals.length">
          <a-col :xs="24" :sm="12" :lg="8" :xl="6" v-for="goal in goals" :key="goal.id">
            <a-card hoverable class="goal-card" @click="goDetail(goal.id)">
              <div class="goal-card__header">
                <a-tag :color="levelColor(goal.level)" class="goal-card__level">{{ levelLabel(goal.level) }}</a-tag>
                <a-tag :color="statusColor(goal.status)">{{ statusLabel(goal.status) }}</a-tag>
              </div>
              <h3 class="goal-card__title">{{ goal.title }}</h3>
              <a-progress :percent="goal.progress" :stroke-color="progressColor(goal.progress)" size="small" />
              <div class="goal-card__meta">
                <span v-if="goal.responsibleName"><UserOutlined /> {{ goal.responsibleName }}</span>
                <span><AimOutlined /> KR {{ goal.keyResultCount }}</span>
                <span v-if="goal.childrenCount"><ApartmentOutlined /> 子目标 {{ goal.childrenCount }}</span>
              </div>
              <div class="goal-card__footer">
                <span class="goal-card__date">{{ formatDate(goal.startDate) }} ~ {{ formatDate(goal.endDate) }}</span>
                <a-dropdown :trigger="['click']" @click.stop>
                  <a-button type="text" size="small"><EllipsisOutlined /></a-button>
                  <template #overlay>
                    <a-menu>
                      <a-menu-item @click.stop="openDecompose(goal)">
                        <ApartmentOutlined /> 分解目标
                      </a-menu-item>
                      <a-menu-item danger @click.stop="handleDelete(goal)">
                        <DeleteOutlined /> 删除
                      </a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </div>
            </a-card>
          </a-col>
        </a-row>
        <EmptyState v-else title="暂无目标数据" />
      </template>

      <!-- 列表视图 -->
      <template v-else>
        <a-table
          :columns="columns"
          :data-source="goals"
          :pagination="false"
          row-key="id"
          size="middle"
          :custom-row="(r: any) => ({ onClick: () => goDetail(r.id) })"
          class="goal-table"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'title'">
              <a @click="goDetail(record.id)">{{ record.title }}</a>
            </template>
            <template v-if="column.dataIndex === 'level'">
              <a-tag :color="levelColor(record.level)">{{ levelLabel(record.level) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'progress'">
              <a-progress :percent="record.progress" :stroke-color="progressColor(record.progress)" size="small" style="width: 120px" />
            </template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'responsibleName'">
              {{ record.responsibleName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'period'">
              {{ formatDate(record.startDate) }} ~ {{ formatDate(record.endDate) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-space>
                <a-button type="link" size="small" @click.stop="openDecompose(record as GoalTreeDto)">分解</a-button>
                <a-popconfirm title="确定删除？" @confirm="handleDelete(record as GoalTreeDto)">
                  <a-button type="link" size="small" danger @click.stop>删除</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </template>
    </a-spin>

    <!-- 创建目标弹窗 -->
    <a-modal
      v-model:open="createVisible"
      :title="'新增目标'"
      :confirm-loading="createLoading"
      @ok="handleCreate"
      width="560px"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="标题" required>
          <a-input v-model:value="form.title" placeholder="输入目标标题" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="3" placeholder="目标描述（可选）" />
        </a-form-item>
        <a-form-item label="周期" required>
          <a-select v-model:value="form.level" placeholder="选择周期">
            <a-select-option value="yearly">年度</a-select-option>
            <a-select-option value="quarterly">季度</a-select-option>
            <a-select-option value="monthly">月度</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="起止日期" required>
          <a-range-picker v-model:value="dateRange" style="width: 100%" />
        </a-form-item>
        <a-form-item label="权重">
          <a-input-number v-model:value="form.weight" :min="0" :max="100" :precision="0" addon-after="%" style="width: 140px" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 分解目标弹窗 -->
    <a-modal
      v-model:open="decomposeVisible"
      title="分解目标"
      :confirm-loading="decomposeLoading"
      @ok="handleDecompose"
      width="560px"
    >
      <a-alert type="info" show-icon style="margin-bottom: 16px">
        <template #message>将从「{{ decomposeParent?.title }}」分解出子目标</template>
      </a-alert>
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="子目标标题" required>
          <a-input v-model:value="decomposeForm.title" placeholder="输入子目标标题" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="decomposeForm.description" :rows="3" placeholder="子目标描述（可选）" />
        </a-form-item>
        <a-form-item label="周期" required>
          <a-select v-model:value="decomposeForm.level" placeholder="选择周期">
            <a-select-option value="yearly">年度</a-select-option>
            <a-select-option value="quarterly">季度</a-select-option>
            <a-select-option value="monthly">月度</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="起止日期" required>
          <a-range-picker v-model:value="decomposeDateRange" style="width: 100%" />
        </a-form-item>
        <a-form-item label="权重">
          <a-input-number v-model:value="decomposeForm.weight" :min="0" :max="100" :precision="0" addon-after="%" style="width: 140px" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import {
  PlusOutlined, AppstoreOutlined, UnorderedListOutlined,
  UserOutlined, AimOutlined, ApartmentOutlined,
  EllipsisOutlined, DeleteOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getGoalTree, createGoal, decomposeGoal } from '@/api/task'
import { useOrgContextStore } from '@/stores/orgContext'
import type { GoalListDto, GoalTreeDto, CreateGoalRequest, DecomposeGoalRequest } from '@/types/task'

const router = useRouter()
const orgStore = useOrgContextStore()
const loading = ref(false)
const viewMode = ref<'card' | 'list'>('card')

// 扁平化目标列表（从树结构中提取）
const goals = ref<GoalTreeDto[]>([])

const filters = reactive<{
  level: string | undefined
  status: number | undefined
  keyword: string | undefined
}>({
  level: undefined,
  status: undefined,
  keyword: undefined,
})

// ---------- 列表列 ----------
const columns = [
  { title: '标题', dataIndex: 'title', ellipsis: true },
  { title: '周期', dataIndex: 'level', width: 90 },
  { title: '进度', dataIndex: 'progress', width: 160 },
  { title: '状态', dataIndex: 'status', width: 90 },
  { title: '负责人', dataIndex: 'responsibleName', width: 100 },
  { title: 'KR', dataIndex: 'keyResultCount', width: 60 },
  { title: '操作', dataIndex: 'action', width: 130 },
]

// ---------- 辅助函数 ----------
const levelLabels: Record<string, string> = { yearly: '年度', quarterly: '季度', monthly: '月度' }
const levelColors: Record<string, string> = { yearly: 'blue', quarterly: 'orange', monthly: 'green' }
const statusLabels: Record<number, string> = { 0: '草稿', 1: '进行中', 2: '已完成', 3: '已取消' }
const statusColors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'warning' }

function levelLabel(l: string) { return levelLabels[l] ?? l }
function levelColor(l: string) { return levelColors[l] ?? 'default' }
function statusLabel(s: number) { return statusLabels[s] ?? `${s}` }
function statusColor(s: number) { return statusColors[s] ?? 'default' }
function progressColor(p: number) { return p >= 80 ? 'var(--color-success)' : p >= 40 ? 'var(--color-info)' : 'var(--color-warning)' }
function formatDate(d?: string) { return d ? d.substring(0, 10) : '' }

function flattenTree(nodes: GoalTreeDto[]): GoalTreeDto[] {
  const list: GoalTreeDto[] = []
  for (const n of nodes) {
    list.push(n)
    if (n.children?.length) list.push(...flattenTree(n.children))
  }
  return list
}

// ---------- 加载数据 ----------
async function loadGoals() {
  loading.value = true
  try {
    const tree = await getGoalTree({
      level: filters.level || undefined,
      status: filters.status ?? undefined,
      keyword: filters.keyword || undefined,
    })
    goals.value = flattenTree(tree)
  } catch {
    message.error('加载目标列表失败')
  } finally {
    loading.value = false
  }
}

// ---------- 路由跳转 ----------
function goDetail(id: number) {
  router.push({ name: 'GoalDetail', params: { goalId: String(id) } })
}

// ---------- 创建目标 ----------
const createVisible = ref(false)
const createLoading = ref(false)
const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const form = reactive<Partial<Omit<CreateGoalRequest, 'description'>> & { description?: string }>({
  title: '',
  description: '',
  level: undefined,
  weight: 100,
  goalOrgId: 0,
})

function showCreateModal() {
  form.title = ''
  form.description = ''
  form.level = undefined
  form.weight = 100
  dateRange.value = undefined
  createVisible.value = true
}

async function handleCreate() {
  if (!form.title?.trim()) return message.warning('请输入标题')
  if (!form.level) return message.warning('请选择周期')
  if (!dateRange.value) return message.warning('请选择起止日期')
  createLoading.value = true
  try {
    await createGoal({
      title: form.title!.trim(),
      description: form.description || undefined,
      goalOrgId: form.goalOrgId || orgStore.currentOrgId || 0,
      level: form.level!,
      startDate: dateRange.value[0].format('YYYY-MM-DD'),
      endDate: dateRange.value[1].format('YYYY-MM-DD'),
      weight: form.weight,
    })
    message.success('创建成功')
    createVisible.value = false
    loadGoals()
  } catch {
    message.error('创建失败')
  } finally {
    createLoading.value = false
  }
}

// ---------- 分解目标 ----------
const decomposeVisible = ref(false)
const decomposeLoading = ref(false)
const decomposeParent = ref<GoalTreeDto | null>(null)
const decomposeDateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const decomposeForm = reactive<Partial<Omit<DecomposeGoalRequest, 'description'>> & { description?: string }>({
  title: '',
  description: '',
  level: undefined,
  weight: 100,
  goalOrgId: 0,
})

function openDecompose(goal: GoalTreeDto) {
  decomposeParent.value = goal
  decomposeForm.title = ''
  decomposeForm.description = ''
  decomposeForm.level = undefined
  decomposeForm.weight = 100
  decomposeDateRange.value = undefined
  decomposeVisible.value = true
}

async function handleDecompose() {
  if (!decomposeForm.title?.trim()) return message.warning('请输入子目标标题')
  if (!decomposeForm.level) return message.warning('请选择周期')
  if (!decomposeDateRange.value) return message.warning('请选择起止日期')
  decomposeLoading.value = true
  try {
    await decomposeGoal(decomposeParent.value!.id, {
      title: decomposeForm.title!.trim(),
      description: decomposeForm.description || undefined,
      goalOrgId: decomposeForm.goalOrgId || orgStore.currentOrgId || 0,
      level: decomposeForm.level!,
      startDate: decomposeDateRange.value[0].format('YYYY-MM-DD'),
      endDate: decomposeDateRange.value[1].format('YYYY-MM-DD'),
      weight: decomposeForm.weight,
    })
    message.success('分解成功')
    decomposeVisible.value = false
    loadGoals()
  } catch {
    message.error('分解失败')
  } finally {
    decomposeLoading.value = false
  }
}

// ---------- 删除目标 ----------
function handleDelete(goal: GoalTreeDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定删除目标「${goal.title}」吗？`,
    okText: '删除',
    okType: 'danger',
    async onOk() {
      try {
        const { del } = await import('@/api/request')
        await del(`/task/goals/${goal.id}`)
        message.success('已删除')
        loadGoals()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

onMounted(() => loadGoals())
</script>

<style scoped lang="scss">
.goal-list-page {
  padding: 0 4px;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.goal-card {
  border-radius: 8px;
  transition: box-shadow 0.2s;

  &:hover {
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 8px;
  }

  &__level {
    font-size: 12px;
  }

  &__title {
    font-size: 15px;
    font-weight: 600;
    margin: 0 0 10px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__meta {
    display: flex;
    gap: 12px;
    font-size: 12px;
    color: #8c8c8c;
    margin-top: 10px;
    flex-wrap: wrap;

    span {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
  }

  &__footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 10px;
    border-top: 1px solid #f0f0f0;
    padding-top: 8px;
  }

  &__date {
    font-size: 12px;
    color: #8c8c8c;
  }
}

.goal-table {
  :deep(tr) {
    cursor: pointer;
  }
}
</style>
