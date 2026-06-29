<template>
  <div class="page-container">
    <PageHeader title="复盘计划">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建复盘
        </a-button>
      </template>
    </PageHeader>

    <!-- 统计卡片 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="复盘总数" :value="stats.totalReviews" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="本月新增" :value="stats.monthNewCount" :value-style="{ color: 'var(--color-info)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="待跟进改进" :value="stats.pendingImprovements" :value-style="{ color: 'var(--color-warning)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false">
          <a-statistic title="改进完成率" :value="stats.completionRate" suffix="%" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 列表 -->
    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'title'">
            <a @click="handleViewDetail(record)" style="cursor: pointer">{{ record.title }}</a>
          </template>
          <template v-if="column.dataIndex === 'exceptionId'">
            {{ record.exceptionId || '-' }}
          </template>
          <template v-if="column.dataIndex === 'improvementCount'">
            <a-tag color="blue">{{ record.improvements?.length ?? 0 }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleViewDetail(record)">查看详情</a-button>
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-popconfirm
              title="确定删除该复盘记录吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="formVisible"
      :title="formType === 'add' ? '新建复盘' : '编辑复盘'"
      width="720px"
      :destroy-on-close="true"
      @cancel="formVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '90px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <a-form-item label="标题" name="title">
          <a-input v-model:value="formData.title" placeholder="请输入复盘标题" :maxlength="200" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="关联异常ID">
              <a-input-number v-model:value="formData.exceptionId" placeholder="选填" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="复盘日期">
              <a-date-picker v-model:value="formData.reviewDate" style="width: 100%" placeholder="选填" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="根因分析" name="rootCause">
          <a-textarea v-model:value="formData.rootCause" placeholder="请输入根因分析" :rows="4" :maxlength="2000" />
        </a-form-item>
        <a-form-item label="影响分析">
          <a-textarea v-model:value="formData.impactAnalysis" placeholder="选填" :rows="3" :maxlength="2000" />
        </a-form-item>
        <a-form-item label="结论">
          <a-textarea v-model:value="formData.conclusion" placeholder="选填" :rows="3" :maxlength="2000" />
        </a-form-item>

        <!-- 改进措施动态表单 -->
        <a-divider orientation="left" style="margin: 12px 0">
          改进措施
          <a-button type="link" size="small" @click="addImprovement">
            <template #icon><PlusOutlined /></template>添加
          </a-button>
        </a-divider>
        <div v-for="(item, idx) in formData.improvements" :key="idx" style="display: flex; gap: 8px; margin-bottom: 8px; align-items: flex-start">
          <a-input v-model:value="item.content" placeholder="改进内容" style="flex: 1" />
          <a-input-number v-model:value="item.assigneeId" placeholder="负责人ID" style="width: 110px" :min="1" />
          <a-date-picker v-model:value="item.deadline" placeholder="截止日期" style="width: 140px" value-format="YYYY-MM-DD" />
          <a-button type="text" danger size="small" @click="removeImprovement(idx)">
            <template #icon><DeleteOutlined /></template>
          </a-button>
        </div>
      </a-form>
      <template #footer>
        <a-button @click="formVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="detailVisible"
      title="复盘详情"
      width="640px"
      :destroy-on-close="true"
    >
      <template v-if="detailData">
        <a-descriptions bordered :column="2" size="small" style="margin-bottom: 24px">
          <a-descriptions-item label="标题" :span="2">{{ detailData.title }}</a-descriptions-item>
          <a-descriptions-item label="关联异常ID">{{ detailData.exceptionId || '-' }}</a-descriptions-item>
          <a-descriptions-item label="复盘日期">{{ detailData.reviewDate || '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建人">{{ detailData.creatorName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ detailData.createTime || '-' }}</a-descriptions-item>
          <a-descriptions-item label="更新时间" :span="2">{{ detailData.updateTime || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div style="font-weight: 600; margin-bottom: 8px">根因分析</div>
        <div style="margin-bottom: 16px; white-space: pre-wrap; color: #555">{{ detailData.rootCause || '-' }}</div>

        <div style="font-weight: 600; margin-bottom: 8px">影响分析</div>
        <div style="margin-bottom: 16px; white-space: pre-wrap; color: #555">{{ detailData.impactAnalysis || '-' }}</div>

        <div style="font-weight: 600; margin-bottom: 8px">结论</div>
        <div style="margin-bottom: 24px; white-space: pre-wrap; color: #555">{{ detailData.conclusion || '-' }}</div>

        <div style="font-weight: 600; margin-bottom: 12px">改进措施</div>
        <a-table
          v-if="detailData.improvements && detailData.improvements.length > 0"
          :columns="improvementColumns"
          :data-source="detailData.improvements"
          :pagination="false"
          row-key="id"
          size="small"
          bordered
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'completed'">
              <a-tag :color="record.completed ? 'success' : 'warning'">
                {{ record.completed ? '已完成' : '待执行' }}
              </a-tag>
            </template>
            <template v-if="column.dataIndex === 'deadline'">
              {{ record.deadline || '-' }}
            </template>
          </template>
        </a-table>
        <EmptyState v-else size="small" title="暂无改进措施" />
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getReviews,
  getReviewDetail,
  createReview,
  updateReview,
  deleteReview,
  getReviewStats,
} from '@/api/quality'

// ========== 统计 ==========
const stats = reactive({
  totalReviews: 0,
  monthNewCount: 0,
  pendingImprovements: 0,
  completionRate: 0,
})

async function fetchStats() {
  try {
    const res = await getReviewStats() as any
    if (res) {
      stats.totalReviews = res.totalReviews ?? 0
      stats.monthNewCount = res.monthNewCount ?? 0
      stats.pendingImprovements = res.pendingImprovements ?? 0
      stats.completionRate = res.completionRate ?? 0
    }
  } catch (e) { console.error(e) }
}

// ========== 列表 ==========
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '标题', dataIndex: 'title', key: 'title', width: 220, ellipsis: true },
  { title: '关联异常', dataIndex: 'exceptionId', key: 'exceptionId', width: 100, align: 'center' as const },
  { title: '复盘日期', dataIndex: 'reviewDate', key: 'reviewDate', width: 120 },
  { title: '改进措施数', dataIndex: 'improvementCount', key: 'improvementCount', width: 100, align: 'center' as const },
  { title: '创建人', dataIndex: 'creatorName', key: 'creatorName', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

const improvementColumns = [
  { title: '改进内容', dataIndex: 'content', key: 'content', ellipsis: true },
  { title: '负责人', dataIndex: 'assigneeName', key: 'assigneeName', width: 90 },
  { title: '截止日期', dataIndex: 'deadline', key: 'deadline', width: 110 },
  { title: '状态', dataIndex: 'completed', key: 'completed', width: 80, align: 'center' as const },
]

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    const res = await getReviews(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total ?? res?.length ?? 0
    }
  } finally {
    loading.value = false
  }
}

// ========== 新增/编辑 ==========
const formVisible = ref(false)
const formType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  title: '',
  exceptionId: undefined as number | undefined,
  reviewDate: undefined as string | undefined,
  rootCause: '',
  impactAnalysis: '',
  conclusion: '',
  improvements: [] as { content: string; assigneeId: number | undefined; deadline: string | undefined; sortOrder: number }[],
})

const formRules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入复盘标题', trigger: 'blur' }],
  rootCause: [{ required: true, message: '请输入根因分析', trigger: 'blur' }],
}

function resetForm() {
  formData.title = ''
  formData.exceptionId = undefined
  formData.reviewDate = undefined
  formData.rootCause = ''
  formData.impactAnalysis = ''
  formData.conclusion = ''
  formData.improvements = []
}

function addImprovement() {
  formData.improvements.push({ content: '', assigneeId: undefined, deadline: undefined, sortOrder: formData.improvements.length })
}

function removeImprovement(idx: number) {
  formData.improvements.splice(idx, 1)
}

function handleAdd() {
  formType.value = 'add'
  currentId.value = null
  resetForm()
  formVisible.value = true
}

function handleEdit(record: any) {
  formType.value = 'edit'
  currentId.value = record.id
  formData.title = record.title || ''
  formData.exceptionId = record.exceptionId || undefined
  formData.reviewDate = record.reviewDate || undefined
  formData.rootCause = record.rootCause || ''
  formData.impactAnalysis = record.impactAnalysis || ''
  formData.conclusion = record.conclusion || ''
  formData.improvements = (record.improvements || []).map((item: any, idx: number) => ({
    content: item.content || '',
    assigneeId: item.assigneeId || undefined,
    deadline: item.deadline || undefined,
    sortOrder: item.sortOrder ?? idx,
  }))
  formVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload: any = {
      title: formData.title,
      exceptionId: formData.exceptionId || undefined,
      reviewDate: formData.reviewDate || undefined,
      rootCause: formData.rootCause,
      impactAnalysis: formData.impactAnalysis || undefined,
      conclusion: formData.conclusion || undefined,
      improvements: formData.improvements
        .filter(i => i.content)
        .map((i, idx) => ({
          content: i.content,
          assigneeId: i.assigneeId || undefined,
          deadline: i.deadline || undefined,
          sortOrder: idx,
        })),
    }
    if (formType.value === 'add') {
      await createReview(payload)
      message.success('新建成功')
    } else {
      await updateReview(currentId.value!, payload)
      message.success('更新成功')
    }
    formVisible.value = false
    fetchList()
    fetchStats()
  } finally { submitLoading.value = false }
}

// ========== 删除 ==========
async function handleDelete(record: any) {
  try {
    await deleteReview(record.id)
    message.success('删除成功')
    fetchList()
    fetchStats()
  } catch (e) { console.error('删除失败:', e) }
}

// ========== 详情 ==========
const detailVisible = ref(false)
const detailData = ref<any>(null)

async function handleViewDetail(record: any) {
  detailVisible.value = true
  detailData.value = null
  try {
    const res = await getReviewDetail(record.id) as any
    detailData.value = res
  } catch (e) { console.error('获取详情失败:', e) }
}

// ========== 初始化 ==========
onMounted(() => {
  fetchList()
  fetchStats()
})
</script>
