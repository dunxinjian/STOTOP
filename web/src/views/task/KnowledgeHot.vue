<template>
  <div class="knowledge-hot">
    <div class="knowledge-hot__header">
      <h2>热门知识</h2>
    </div>

    <!-- 筛选区 -->
    <div class="knowledge-hot__filters">
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
        placeholder="搜索知识标题"
        style="width: 280px"
        allow-clear
        @search="applyFilters"
      />
    </div>

    <a-spin :spinning="loading">
      <!-- 热门排行列表 -->
      <div v-if="filteredList.length" class="knowledge-hot__list">
        <div
          v-for="(item, index) in filteredList"
          :key="item.id"
          class="hot-item"
          @click="handleView(item)"
        >
          <div class="hot-item__rank" :class="{ 'top-3': index < 3 }">
            {{ index + 1 }}
          </div>
          <div class="hot-item__content">
            <div class="hot-item__title">{{ item.title }}</div>
            <div class="hot-item__meta">
              <span class="hot-item__category">{{ categoryMap[item.category] || '其他' }}</span>
              <span class="hot-item__author">{{ item.authorName }}</span>
              <div class="hot-item__tags">
                <a-tag v-for="t in item.tags" :key="t.id" :color="t.color" size="small">
                  {{ t.name }}
                </a-tag>
              </div>
            </div>
          </div>
          <div class="hot-item__stats">
            <div class="hot-item__stat">
              <EyeOutlined /> <span>{{ item.viewCount }}</span>
            </div>
            <div class="hot-item__stat">
              <LikeOutlined /> <span>{{ item.likeCount }}</span>
            </div>
          </div>
          <div class="hot-item__actions">
            <a-tooltip title="收藏">
              <a-button type="text" size="small" @click.stop="handleCollect(item)">
                <StarOutlined />
              </a-button>
            </a-tooltip>
            <a-tooltip title="点赞">
              <a-button type="text" size="small" @click.stop="handleLike(item)">
                <LikeOutlined />
              </a-button>
            </a-tooltip>
          </div>
        </div>
      </div>

      <EmptyState v-if="!loading && filteredList.length === 0" title="暂无热门知识" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { EyeOutlined, LikeOutlined, StarOutlined } from '@ant-design/icons-vue'
import { getHotKnowledge, toggleKnowledgeLike, toggleKnowledgeCollect } from '@/api/task'
import type { KnowledgeListDto } from '@/types/task'

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

async function loadData() {
  loading.value = true
  try {
    allList.value = await getHotKnowledge()
    applyFilters()
  } catch {
    message.error('获取热门知识失败')
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

async function handleLike(item: KnowledgeListDto) {
  try {
    await toggleKnowledgeLike(item.id)
    message.success('操作成功')
    loadData()
  } catch {
    message.error('操作失败')
  }
}

async function handleCollect(item: KnowledgeListDto) {
  try {
    await toggleKnowledgeCollect(item.id)
    message.success('操作成功')
    loadData()
  } catch {
    message.error('操作失败')
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.knowledge-hot {
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

  &__list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
}

.hot-item {
  display: flex;
  align-items: center;
  gap: 16px;
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px 20px;
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
    border-color: #d9d9d9;
  }

  &__rank {
    flex-shrink: 0;
    width: 32px;
    height: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    font-size: 14px;
    font-weight: 700;
    background: #f5f5f5;
    color: #8c8c8c;

    &.top-3 {
      background: linear-gradient(135deg, var(--biz-points), var(--color-warning));
      color: #fff;
    }
  }

  &__content {
    flex: 1;
    min-width: 0;
  }

  &__title {
    font-size: 15px;
    font-weight: 600;
    color: #262626;
    margin-bottom: 6px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 13px;
    color: #8c8c8c;
    flex-wrap: wrap;
  }

  &__tags {
    display: flex;
    gap: 4px;
  }

  &__stats {
    flex-shrink: 0;
    display: flex;
    gap: 16px;
  }

  &__stat {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 13px;
    color: #8c8c8c;
  }

  &__actions {
    flex-shrink: 0;
    display: flex;
    gap: 4px;
  }
}
</style>
