<template>
  <div class="page-container">
    <PageHeader title="案例库">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增案例
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="搜索标题" allow-clear style="width: 160px" @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.tags" size="small" placeholder="选择标签" mode="multiple" allow-clear style="width: 200px" :options="tagOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 顶部统计区 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="6">
        <a-card :bordered="false" size="small">
          <a-statistic title="案例总数" :value="stats.totalArticles" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" size="small">
          <a-statistic title="本月新增" :value="stats.monthNewCount" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" size="small">
          <a-statistic title="热门标签数" :value="stats.topTags?.length ?? 0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" size="small">
          <a-statistic title="总浏览量" :value="totalViewCount" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 内容区 -->
    <a-row :gutter="16">
      <!-- 左侧分类导航 -->
      <a-col :span="4">
        <a-card :bordered="false" size="small" title="分类导航">
          <a-menu
            v-model:selectedKeys="selectedCategory"
            mode="inline"
            :style="{ border: 'none' }"
            @click="handleCategoryClick"
          >
            <a-menu-item key="">全部</a-menu-item>
            <a-menu-item v-for="cat in categories" :key="cat">{{ cat }}</a-menu-item>
          </a-menu>
        </a-card>
      </a-col>

      <!-- 右侧文章列表 -->
      <a-col :span="20">
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
              <template v-if="column.dataIndex === 'category'">
                <a-tag color="blue">{{ record.category }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'tags'">
                <template v-if="record.tags">
                  <a-tag
                    v-for="(tag, i) in record.tags.split(',')"
                    :key="tag"
                    :color="tagColors[Number(i) % tagColors.length]"
                    style="margin-bottom: 2px"
                  >{{ tag }}</a-tag>
                </template>
                <span v-else>-</span>
              </template>
              <template v-if="column.dataIndex === 'viewCount'">
                <EyeOutlined style="margin-right: 4px; color: #999" />{{ record.viewCount }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleViewDetail(record)">查看</a-button>
                <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
                <a-popconfirm
                  title="确定删除该文章吗？"
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
      </a-col>
    </a-row>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="formVisible"
      :title="formType === 'add' ? '新增案例' : '编辑案例'"
      width="680px"
      :destroy-on-close="true"
      @cancel="formVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <a-form-item label="标题" name="title">
          <a-input v-model:value="formData.title" placeholder="请输入标题" :maxlength="200" />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-select
            v-model:value="formData.category"
            placeholder="请选择或输入分类"
            mode="tags"
            :max-tag-count="1"
            :options="categorySelectOptions"
          />
        </a-form-item>
        <a-form-item label="标签" name="tags">
          <a-select
            v-model:value="formData.tags"
            placeholder="输入或选择标签"
            mode="tags"
            :options="tagOptions"
          />
        </a-form-item>
        <a-form-item label="内容" name="content">
          <a-textarea v-model:value="formData.content" placeholder="请输入内容" :rows="10" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="关联异常ID">
              <a-input-number v-model:value="formData.relatedExceptionId" placeholder="选填" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="关联复盘ID">
              <a-input-number v-model:value="formData.relatedReviewId" placeholder="选填" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="formVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="detailVisible"
      title="案例详情"
      width="640px"
      :destroy-on-close="true"
    >
      <template v-if="detailData">
        <h2 style="margin-bottom: 16px">{{ detailData.title }}</h2>
        <div style="margin-bottom: 16px">
          <a-tag color="blue">{{ detailData.category }}</a-tag>
        </div>

        <a-descriptions bordered :column="2" size="small" style="margin-bottom: 24px">
          <a-descriptions-item label="浏览量">
            <EyeOutlined style="margin-right: 4px" />{{ detailData.viewCount }}
          </a-descriptions-item>
          <a-descriptions-item label="创建人">{{ detailData.creatorName }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ detailData.createTime }}</a-descriptions-item>
          <a-descriptions-item label="更新时间">{{ detailData.updateTime || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div style="margin-bottom: 12px; font-weight: 600">标签</div>
        <div style="margin-bottom: 24px">
          <template v-if="detailData.tags">
            <a-tag
              v-for="(tag, i) in detailData.tags.split(',')"
              :key="tag"
              :color="tagColors[Number(i) % tagColors.length]"
            >{{ tag }}</a-tag>
          </template>
          <span v-else style="color: #999">无标签</span>
        </div>

        <div style="margin-bottom: 12px; font-weight: 600">内容</div>
        <div style="background: #fafafa; padding: 16px; border-radius: 6px; white-space: pre-wrap; margin-bottom: 24px">{{ detailData.content }}</div>

        <template v-if="detailData.relatedExceptionId || detailData.relatedReviewId">
          <div style="margin-bottom: 12px; font-weight: 600">关联信息</div>
          <a-descriptions bordered :column="2" size="small">
            <a-descriptions-item label="关联异常ID">{{ detailData.relatedExceptionId || '-' }}</a-descriptions-item>
            <a-descriptions-item label="关联复盘ID">{{ detailData.relatedReviewId || '-' }}</a-descriptions-item>
          </a-descriptions>
        </template>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EyeOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getKnowledgeArticles,
  getKnowledgeDetail,
  createKnowledgeArticle,
  updateKnowledgeArticle,
  deleteKnowledgeArticle,
  getKnowledgeCategories,
  getKnowledgeTags,
  getKnowledgeStats,
} from '@/api/quality'

const tagColors = ['blue', 'green', 'orange', 'purple', 'cyan', 'magenta', 'red', 'geekblue', 'volcano', 'gold']

// ========== 统计 ==========
const stats = reactive({ totalArticles: 0, monthNewCount: 0, topTags: [] as any[] })
const totalViewCount = ref(0)

async function fetchStats() {
  try {
    const res = await getKnowledgeStats() as any
    if (res) {
      stats.totalArticles = res.totalArticles ?? 0
      stats.monthNewCount = res.monthNewCount ?? 0
      stats.topTags = res.topTags ?? []
      // 从分类分布中计算总浏览量
      totalViewCount.value = res.totalViewCount ?? 0
    }
  } catch (e) { console.error(e) }
}

// ========== 分类导航 ==========
const categories = ref<string[]>([])
const selectedCategory = ref<string[]>([''])

async function fetchCategories() {
  try {
    const res = await getKnowledgeCategories() as any
    categories.value = res ?? []
  } catch (e) { console.error(e) }
}

function handleCategoryClick({ key }: { key: string | number }) {
  selectedCategory.value = [String(key)]
  pagination.pageIndex = 1
  fetchList()
}

const categorySelectOptions = computed(() =>
  categories.value.map(c => ({ label: c, value: c }))
)

// ========== 标签 ==========
const allTags = ref<string[]>([])
const tagOptions = computed(() => allTags.value.map(t => ({ label: t, value: t })))

async function fetchTags() {
  try {
    const res = await getKnowledgeTags() as any
    allTags.value = res ?? []
  } catch (e) { console.error(e) }
}

// ========== 搜索 ==========
const searchForm = reactive({
  keyword: '',
  tags: [] as string[],
})

function handleSearch() { pagination.pageIndex = 1; fetchList() }
function handleReset() {
  searchForm.keyword = ''
  searchForm.tags = []
  pagination.pageIndex = 1
  fetchList()
}

// ========== 表格 ==========
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
  { title: '分类', dataIndex: 'category', key: 'category', width: 100, align: 'center' as const },
  { title: '标签', dataIndex: 'tags', key: 'tags', width: 200 },
  { title: '浏览量', dataIndex: 'viewCount', key: 'viewCount', width: 90, align: 'center' as const },
  { title: '创建人', dataIndex: 'creatorName', key: 'creatorName', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
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
    const cat = selectedCategory.value[0]
    if (cat) params.category = cat
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.tags.length > 0) params.tags = searchForm.tags.join(',')
    const res = await getKnowledgeArticles(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total ?? res?.length ?? 0
    }
  } finally {
    loading.value = false
  }
}

// ========== 新增/编辑表单 ==========
const formVisible = ref(false)
const formType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  title: '',
  category: [] as string[],
  tags: [] as string[],
  content: '',
  relatedExceptionId: undefined as number | undefined,
  relatedReviewId: undefined as number | undefined,
})

const formRules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  category: [{ required: true, message: '请选择或输入分类', trigger: 'change' }],
  content: [{ required: true, message: '请输入内容', trigger: 'blur' }],
}

function resetForm() {
  formData.title = ''
  formData.category = []
  formData.tags = []
  formData.content = ''
  formData.relatedExceptionId = undefined
  formData.relatedReviewId = undefined
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
  formData.title = record.title
  formData.category = record.category ? [record.category] : []
  formData.tags = record.tags ? record.tags.split(',') : []
  formData.content = record.content || ''
  formData.relatedExceptionId = record.relatedExceptionId || undefined
  formData.relatedReviewId = record.relatedReviewId || undefined
  formVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload: any = {
      title: formData.title,
      content: formData.content,
      category: formData.category[0] || '',
      tags: formData.tags.join(',') || undefined,
      relatedExceptionId: formData.relatedExceptionId || undefined,
      relatedReviewId: formData.relatedReviewId || undefined,
    }
    if (formType.value === 'add') {
      await createKnowledgeArticle(payload)
      message.success('新增成功')
    } else {
      await updateKnowledgeArticle(currentId.value!, payload)
      message.success('更新成功')
    }
    formVisible.value = false
    fetchList()
    fetchStats()
    fetchCategories()
    fetchTags()
  } finally { submitLoading.value = false }
}

// ========== 删除 ==========
async function handleDelete(record: any) {
  try {
    await deleteKnowledgeArticle(record.id)
    message.success('删除成功')
    fetchList()
    fetchStats()
  } catch (e) { console.error('删除失败:', e) }
}

// ========== 详情抽屉 ==========
const detailVisible = ref(false)
const detailData = ref<any>(null)

async function handleViewDetail(record: any) {
  detailVisible.value = true
  detailData.value = null
  try {
    const res = await getKnowledgeDetail(record.id) as any
    detailData.value = res
  } catch (e) { console.error('获取详情失败:', e) }
}

// ========== 初始化 ==========
onMounted(() => {
  fetchList()
  fetchStats()
  fetchCategories()
  fetchTags()
})
</script>
