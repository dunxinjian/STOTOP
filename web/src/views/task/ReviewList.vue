<template>
  <div class="review-list">
    <!-- 页面标题 & 新建按钮 -->
    <div class="review-list__header">
      <h2>复盘管理</h2>
      <a-button type="primary" @click="handleCreate">
        <PlusOutlined /> 新建复盘
      </a-button>
    </div>

    <!-- 筛选区 -->
    <div class="review-list__filters">
      <a-select
        v-model:value="query.relationType"
        placeholder="关联类型"
        allow-clear
        style="width: 140px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="1">项目</a-select-option>
        <a-select-option :value="2">目标</a-select-option>
        <a-select-option :value="3">任务</a-select-option>
      </a-select>

      <a-select
        v-model:value="query.status"
        placeholder="状态"
        allow-clear
        style="width: 120px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="0">草稿</a-select-option>
        <a-select-option :value="1">已发布</a-select-option>
      </a-select>

      <a-input-search
        v-model:value="query.keyword"
        placeholder="搜索复盘标题"
        style="width: 240px"
        allow-clear
        @search="() => handleSearch()"
      />
    </div>

    <!-- 列表表格 -->
    <a-table
      :columns="columns"
      :data-source="list"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'title'">
          <a @click="handleView(record)">{{ record.title }}</a>
        </template>

        <template v-else-if="column.key === 'relationType'">
          {{ relationTypeMap[record.relationType] || '未知' }}
        </template>

        <template v-else-if="column.key === 'relationTitle'">
          {{ record.relationTitle || '-' }}
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? '已发布' : '草稿' }}
          </a-tag>
        </template>

        <template v-else-if="column.key === 'createTime'">
          {{ formatTime(record.createTime) }}
        </template>

        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleView(record)">查看</a-button>
            <a-button v-if="record.status === 0" type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-popconfirm title="确定删除此复盘？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { getReviews, deleteReview } from '@/api/task'
import type { ReviewListDto, ReviewPagedRequest } from '@/types/task'
import dayjs from 'dayjs'

const router = useRouter()

const loading = ref(false)
const list = ref<ReviewListDto[]>([])
const total = ref(0)

const query = reactive<ReviewPagedRequest>({
  pageIndex: 1,
  pageSize: 15,
  keyword: undefined,
  relationType: undefined,
  status: undefined,
})

const pagination = reactive({
  current: 1,
  pageSize: 15,
  total: 0,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
})

const relationTypeMap: Record<number, string> = {
  1: '项目',
  2: '目标',
  3: '任务',
}

const columns = [
  { title: '标题', key: 'title', ellipsis: true },
  { title: '关联类型', key: 'relationType', width: 100 },
  { title: '关联对象', key: 'relationTitle', ellipsis: true, width: 180 },
  { title: '创建人', dataIndex: 'reviewerName', width: 100 },
  { title: '创建时间', key: 'createTime', width: 160 },
  { title: '状态', key: 'status', width: 80 },
  { title: '操作', key: 'action', width: 180 },
]

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

async function loadData() {
  loading.value = true
  try {
    const res = await getReviews({
      ...query,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    })
    list.value = res.items
    total.value = res.total
    pagination.total = res.total
  } catch {
    message.error('获取复盘列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  loadData()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadData()
}

function handleCreate() {
  router.push({ name: 'ReviewDetail' })
}

function handleView(record: Record<string, any>) {
  router.push({ name: 'ReviewDetail', params: { reviewId: record.id } })
}

function handleEdit(record: Record<string, any>) {
  router.push({ name: 'ReviewDetail', params: { reviewId: record.id }, query: { edit: '1' } })
}

async function handleDelete(id: number) {
  try {
    await deleteReview(id)
    message.success('删除成功')
    loadData()
  } catch {
    message.error('删除失败')
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.review-list {
  padding: 24px;

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
    }
  }

  &__filters {
    display: flex;
    gap: 12px;
    margin-bottom: 16px;
    flex-wrap: wrap;
  }
}
</style>
