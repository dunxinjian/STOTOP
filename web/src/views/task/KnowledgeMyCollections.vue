<template>
  <div class="knowledge-collections">
    <div class="knowledge-collections__header">
      <h2>我的收藏</h2>
    </div>

    <!-- 筛选区 -->
    <div class="knowledge-collections__filters">
      <a-select
        v-model:value="filterCategory"
        placeholder="知识分类"
        allow-clear
        style="width: 160px"
        @change="applyFilters"
      >
        <a-select-option v-for="cat in categoryOptions" :key="cat.value" :value="cat.value">
          {{ cat.label }}
        </a-select-option>
      </a-select>

      <a-input-search
        v-model:value="keyword"
        placeholder="搜索收藏的知识"
        style="width: 280px"
        allow-clear
        @search="applyFilters"
      />
    </div>

    <a-spin :spinning="loading">
      <!-- 卡片列表 -->
      <div v-if="filteredList.length" class="knowledge-collections__cards">
        <div
          v-for="item in filteredList"
          :key="item.id"
          class="collection-card"
          @click="handleView(item)"
        >
          <div class="collection-card__category">
            {{ categoryMap[item.category] || '其他' }}
          </div>
          <div class="collection-card__title">{{ item.title }}</div>
          <div class="collection-card__tags">
            <a-tag v-for="t in item.tags" :key="t.id" :color="t.color" size="small">
              {{ t.name }}
            </a-tag>
          </div>
          <div class="collection-card__footer">
            <span class="collection-card__author">{{ item.authorName }}</span>
            <div class="collection-card__stats">
              <span><EyeOutlined /> {{ item.viewCount }}</span>
              <span><LikeOutlined /> {{ item.likeCount }}</span>
            </div>
          </div>
          <div class="collection-card__time">{{ formatTime(item.createTime) }}</div>
          <div class="collection-card__action">
            <a-button
              type="text"
              size="small"
              danger
              @click.stop="handleUncollect(item)"
            >
              <StarFilled style="color: var(--biz-points)" /> 取消收藏
            </a-button>
          </div>
        </div>
      </div>

      <EmptyState v-if="!loading && filteredList.length === 0" title="暂无收藏的知识文章" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { EyeOutlined, LikeOutlined, StarFilled } from '@ant-design/icons-vue'
import { getMyCollections, toggleKnowledgeCollect } from '@/api/task'
import type { KnowledgeListDto } from '@/types/task'
import dayjs from 'dayjs'

const router = useRouter()
const loading = ref(false)
const allList = ref<KnowledgeListDto[]>([])
const filteredList = ref<KnowledgeListDto[]>([])
const filterCategory = ref<number | undefined>(undefined)
const keyword = ref('')

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

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

async function loadData() {
  loading.value = true
  try {
    const res = await getMyCollections()
    allList.value = res.items
    applyFilters()
  } catch {
    message.error('获取收藏列表失败')
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  let result = [...allList.value]
  if (filterCategory.value !== undefined) {
    result = result.filter(item => item.category === filterCategory.value)
  }
  if (keyword.value) {
    const kw = keyword.value.toLowerCase()
    result = result.filter(item => item.title.toLowerCase().includes(kw))
  }
  filteredList.value = result
}

function handleView(item: KnowledgeListDto) {
  router.push({ name: 'KnowledgeDetail', params: { knowledgeId: item.id } })
}

async function handleUncollect(item: KnowledgeListDto) {
  try {
    await toggleKnowledgeCollect(item.id)
    message.success('已取消收藏')
    loadData()
  } catch {
    message.error('操作失败')
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.knowledge-collections {
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

  &__cards {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 16px;
  }
}

.collection-card {
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

  &__action {
    margin-top: 8px;
    border-top: 1px solid #f0f0f0;
    padding-top: 8px;
  }
}
</style>
