<template>
  <div class="knowledge-list">
    <!-- 页面标题 & 操作 -->
    <div class="knowledge-list__header">
      <h2>知识库</h2>
      <div class="knowledge-list__header-right">
        <a-radio-group v-model:value="viewMode" size="small">
          <a-radio-button value="list"><UnorderedListOutlined /></a-radio-button>
          <a-radio-button value="card"><AppstoreOutlined /></a-radio-button>
        </a-radio-group>
        <a-button type="primary" @click="handleCreate">
          <PlusOutlined /> 新建知识
        </a-button>
      </div>
    </div>

    <!-- 筛选区 -->
    <div class="knowledge-list__filters">
      <a-select
        v-model:value="query.category"
        placeholder="知识分类"
        allow-clear
        style="width: 160px"
        @change="() => handleSearch()"
      >
        <a-select-option v-for="cat in categoryOptions" :key="cat.value" :value="cat.value">
          {{ cat.label }}
        </a-select-option>
      </a-select>

      <a-select
        v-model:value="query.sortField"
        placeholder="排序"
        style="width: 120px"
        @change="() => handleSearch()"
      >
        <a-select-option value="createTime">最新</a-select-option>
        <a-select-option value="likeCount">最热</a-select-option>
        <a-select-option value="viewCount">最多浏览</a-select-option>
      </a-select>

      <a-input-search
        v-model:value="query.keyword"
        placeholder="搜索知识标题或内容"
        style="width: 280px"
        allow-clear
        @search="() => handleSearch()"
      />
    </div>

    <!-- 标签云 -->
    <div v-if="allTags.length > 0" class="knowledge-list__tags">
      <span class="knowledge-list__tags-label">标签筛选：</span>
      <a-checkable-tag
        v-for="tag in allTags"
        :key="tag.id"
        :checked="selectedTagIds.includes(tag.id)"
        @change="(checked: boolean) => handleTagToggle(tag.id, checked)"
      >
        {{ tag.name }}
      </a-checkable-tag>
    </div>

    <a-spin :spinning="loading">
      <!-- 列表视图 -->
      <template v-if="viewMode === 'list'">
        <a-table
          :columns="columns"
          :data-source="list"
          :pagination="pagination"
          row-key="id"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'title'">
              <a @click="handleView(record as KnowledgeListDto)">{{ record.title }}</a>
            </template>
            <template v-else-if="column.key === 'category'">
              {{ categoryMap[record.category] || '其他' }}
            </template>
            <template v-else-if="column.key === 'tags'">
              <a-tag v-for="t in record.tags" :key="t.id" :color="t.color" size="small">
                {{ t.name }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'stats'">
              <a-space :size="12">
                <span><EyeOutlined /> {{ record.viewCount }}</span>
                <span><LikeOutlined /> {{ record.likeCount }}</span>
              </a-space>
            </template>
            <template v-else-if="column.key === 'createTime'">
              {{ formatTime(record.createTime) }}
            </template>
          </template>
        </a-table>
      </template>

      <!-- 卡片视图 -->
      <template v-else>
        <div class="knowledge-list__cards">
          <div
            v-for="item in list"
            :key="item.id"
            class="knowledge-card"
            @click="handleView(item)"
          >
            <div class="knowledge-card__category">
              {{ categoryMap[item.category] || '其他' }}
            </div>
            <div class="knowledge-card__title">{{ item.title }}</div>
            <div class="knowledge-card__tags">
              <a-tag v-for="t in item.tags" :key="t.id" :color="t.color" size="small">
                {{ t.name }}
              </a-tag>
            </div>
            <div class="knowledge-card__footer">
              <span class="knowledge-card__author">{{ item.authorName }}</span>
              <div class="knowledge-card__stats">
                <span><EyeOutlined /> {{ item.viewCount }}</span>
                <span><LikeOutlined /> {{ item.likeCount }}</span>
              </div>
            </div>
            <div class="knowledge-card__time">{{ formatTime(item.createTime) }}</div>
          </div>
        </div>

        <!-- 卡片视图分页 -->
        <div class="knowledge-list__card-pagination">
          <a-pagination
            v-model:current="pagination.current"
            v-model:pageSize="pagination.pageSize"
            :total="pagination.total"
            show-size-changer
            :show-total="(t: number) => `共 ${t} 条`"
            @change="handlePageChange"
          />
        </div>
      </template>

      <EmptyState v-if="!loading && list.length === 0" title="暂无知识文章" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, UnorderedListOutlined, AppstoreOutlined,
  EyeOutlined, LikeOutlined,
} from '@ant-design/icons-vue'
import { getKnowledgeList, getTags } from '@/api/task'
import type { KnowledgeListDto, KnowledgePagedRequest, TagListDto } from '@/types/task'
import dayjs from 'dayjs'

const router = useRouter()

const loading = ref(false)
const list = ref<KnowledgeListDto[]>([])
const allTags = ref<TagListDto[]>([])
const selectedTagIds = ref<number[]>([])
const viewMode = ref<'list' | 'card'>('list')

const categoryOptions = [
  { value: 1, label: '经验总结' },
  { value: 2, label: '最佳实践' },
  { value: 3, label: '问题解决方案' },
  { value: 4, label: '工具方法' },
  { value: 5, label: '其他' },
]

const categoryMap: Record<number, string> = {
  1: '经验总结',
  2: '最佳实践',
  3: '问题解决方案',
  4: '工具方法',
  5: '其他',
}

const query = reactive<KnowledgePagedRequest>({
  pageIndex: 1,
  pageSize: 12,
  keyword: undefined,
  category: undefined,
  sortField: 'createTime',
  sortOrder: 'desc',
  tagIds: undefined,
})

const pagination = reactive({
  current: 1,
  pageSize: 12,
  total: 0,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
})

const columns = [
  { title: '标题', key: 'title', ellipsis: true },
  { title: '分类', key: 'category', width: 120 },
  { title: '标签', key: 'tags', width: 200 },
  { title: '作者', dataIndex: 'authorName', width: 100 },
  { title: '统计', key: 'stats', width: 140 },
  { title: '创建时间', key: 'createTime', width: 160 },
]

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

async function loadTags() {
  try {
    allTags.value = await getTags()
  } catch {
    // 标签加载失败不阻塞页面
  }
}

async function loadData() {
  loading.value = true
  try {
    const res = await getKnowledgeList({
      ...query,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      tagIds: selectedTagIds.value.length ? selectedTagIds.value : undefined,
    })
    list.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取知识列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  loadData()
}

function handleTagToggle(tagId: number, checked: boolean) {
  if (checked) {
    selectedTagIds.value.push(tagId)
  } else {
    selectedTagIds.value = selectedTagIds.value.filter(id => id !== tagId)
  }
  handleSearch()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadData()
}

function handlePageChange(page: number, pageSize: number) {
  pagination.current = page
  pagination.pageSize = pageSize
  loadData()
}

function handleCreate() {
  router.push({ name: 'KnowledgeCreate' })
}

function handleView(record: KnowledgeListDto) {
  router.push({ name: 'KnowledgeDetail', params: { knowledgeId: record.id } })
}

onMounted(() => {
  loadTags()
  loadData()
})
</script>

<style scoped lang="scss">
.knowledge-list {
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

    &-right {
      display: flex;
      align-items: center;
      gap: 12px;
    }
  }

  &__filters {
    display: flex;
    gap: 12px;
    margin-bottom: 12px;
    flex-wrap: wrap;
  }

  &__tags {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 4px;
    margin-bottom: 16px;

    &-label {
      font-size: 13px;
      color: #8c8c8c;
      margin-right: 4px;
    }
  }

  &__cards {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 16px;
  }

  &__card-pagination {
    display: flex;
    justify-content: flex-end;
    margin-top: 20px;
  }
}

.knowledge-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
    border-color: #d9d9d9;
  }

  &__category {
    font-size: 12px;
    color: #8c8c8c;
    margin-bottom: 8px;
  }

  &__title {
    font-size: 16px;
    font-weight: 600;
    color: #262626;
    margin-bottom: 10px;
    overflow: hidden;
    text-overflow: ellipsis;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
  }

  &__tags {
    margin-bottom: 12px;
    min-height: 22px;
  }

  &__footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 13px;
    color: #8c8c8c;
  }

  &__stats {
    display: flex;
    gap: 12px;
  }

  &__time {
    font-size: 12px;
    color: #bfbfbf;
    margin-top: 8px;
  }
}
</style>
