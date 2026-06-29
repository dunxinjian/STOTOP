<template>
  <div class="knowledge-detail">
    <!-- 顶部操作栏 -->
    <div class="knowledge-detail__toolbar">
      <a-button @click="goBack"><ArrowLeftOutlined /> 返回列表</a-button>
      <div class="knowledge-detail__toolbar-right">
        <template v-if="isEditing">
          <a-button @click="isEditing = false">取消</a-button>
          <a-button type="primary" @click="handleSave" :loading="saving">保存</a-button>
        </template>
        <template v-else-if="detail">
          <a-button @click="isEditing = true"><EditOutlined /> 编辑</a-button>
        </template>
      </div>
    </div>

    <a-spin :spinning="loading">
      <!-- 编辑模式 -->
      <template v-if="isEditing">
        <a-form layout="vertical" class="knowledge-detail__form">
          <a-form-item label="标题" required>
            <a-input v-model:value="form.title" placeholder="知识标题" />
          </a-form-item>

          <a-row :gutter="16">
            <a-col :span="8">
              <a-form-item label="分类" required>
                <a-select v-model:value="form.category" placeholder="选择分类">
                  <a-select-option v-for="cat in categoryOptions" :key="cat.value" :value="cat.value">
                    {{ cat.label }}
                  </a-select-option>
                </a-select>
              </a-form-item>
            </a-col>
            <a-col :span="16">
              <a-form-item label="标签">
                <a-select
                  v-model:value="form.tagIds"
                  mode="tags"
                  placeholder="输入或选择标签"
                  :options="tagOptions"
                />
              </a-form-item>
            </a-col>
          </a-row>

          <a-form-item label="内容">
            <a-textarea
              v-model:value="form.content"
              placeholder="知识内容（支持 Markdown 格式）"
              :rows="16"
              show-count
            />
          </a-form-item>

          <a-row :gutter="16">
            <a-col :span="8">
              <a-form-item label="状态">
                <a-select v-model:value="form.status">
                  <a-select-option :value="0">草稿</a-select-option>
                  <a-select-option :value="1">已发布</a-select-option>
                </a-select>
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="置顶">
                <a-switch v-model:checked="form.isPinned" />
              </a-form-item>
            </a-col>
          </a-row>
        </a-form>
      </template>

      <!-- 查看模式 -->
      <template v-else-if="detail">
        <div class="knowledge-detail__view">
          <h1>{{ detail.title }}</h1>

          <!-- 元信息 -->
          <div class="knowledge-detail__meta">
            <a-tag color="blue">{{ categoryMap[detail.category] || '其他' }}</a-tag>
            <a-tag v-if="detail.isPinned" color="red">置顶</a-tag>
            <a-tag :color="detail.status === 1 ? 'green' : 'default'">
              {{ detail.status === 1 ? '已发布' : '草稿' }}
            </a-tag>
            <span>作者：{{ detail.authorName }}</span>
            <span>{{ formatTime(detail.createTime) }}</span>
          </div>

          <!-- 标签 -->
          <div v-if="detail.tags.length > 0" class="knowledge-detail__tags">
            <a-tag v-for="t in detail.tags" :key="t.id" :color="t.color">{{ t.name }}</a-tag>
          </div>

          <!-- 统计 -->
          <div class="knowledge-detail__stats">
            <a-space :size="20">
              <span><EyeOutlined /> {{ detail.viewCount }} 浏览</span>
              <span
                :class="['knowledge-detail__action', { active: detail.hasLiked }]"
                @click="handleLike"
              >
                <LikeOutlined /> {{ detail.likeCount }} 点赞
              </span>
              <span
                :class="['knowledge-detail__action', { active: detail.hasCollected }]"
                @click="handleCollect"
              >
                <StarOutlined /> {{ detail.collectCount }} 收藏
              </span>
            </a-space>
          </div>

          <!-- 正文内容 -->
          <div class="knowledge-detail__content">
            <div v-if="detail.content" v-html="renderContent(detail.content)" />
            <a-empty v-else description="暂无内容" :image="false" />
          </div>

          <!-- 附件 -->
          <div v-if="knowledgeId" class="knowledge-detail__section">
            <h3>附件</h3>
            <AttachmentUpload :related-type="6" :related-id="knowledgeId" />
          </div>

          <!-- 相关知识推荐 -->
          <div v-if="relatedList.length > 0" class="knowledge-detail__section">
            <h3>相关知识</h3>
            <div class="knowledge-detail__related">
              <a
                v-for="item in relatedList"
                :key="item.id"
                class="knowledge-detail__related-item"
                @click="goToKnowledge(item.id)"
              >
                <span class="knowledge-detail__related-category">
                  {{ categoryMap[item.category] || '其他' }}
                </span>
                {{ item.title }}
              </a>
            </div>
          </div>

          <!-- 评论区 -->
          <div class="knowledge-detail__section">
            <h3>评论 ({{ comments.length }})</h3>
            <div class="knowledge-detail__comment-input">
              <a-textarea
                v-model:value="newComment"
                placeholder="发表评论..."
                :rows="3"
              />
              <a-button
                type="primary"
                :disabled="!newComment.trim()"
                :loading="commentLoading"
                @click="handleAddComment"
                style="margin-top: 8px"
              >
                发表评论
              </a-button>
            </div>

            <div class="knowledge-detail__comments">
              <div v-for="c in comments" :key="c.id" class="knowledge-detail__comment-item">
                <div class="knowledge-detail__comment-header">
                  <strong>{{ c.userName }}</strong>
                  <span class="knowledge-detail__comment-time">{{ formatTime(c.createTime) }}</span>
                </div>
                <div class="knowledge-detail__comment-body">{{ c.content }}</div>
                <!-- 子评论 -->
                <div v-if="c.replies && c.replies.length" class="knowledge-detail__comment-replies">
                  <div v-for="r in c.replies" :key="r.id" class="knowledge-detail__comment-item">
                    <div class="knowledge-detail__comment-header">
                      <strong>{{ r.userName }}</strong>
                      <span class="knowledge-detail__comment-time">{{ formatTime(r.createTime) }}</span>
                    </div>
                    <div class="knowledge-detail__comment-body">{{ r.content }}</div>
                  </div>
                </div>
              </div>
              <a-empty v-if="comments.length === 0" description="暂无评论" :image="false" />
            </div>
          </div>
        </div>
      </template>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  ArrowLeftOutlined, EditOutlined,
  EyeOutlined, LikeOutlined, StarOutlined,
} from '@ant-design/icons-vue'
import {
  getKnowledge, updateKnowledge,
  toggleKnowledgeLike, toggleKnowledgeCollect,
  getKnowledgeComments, createKnowledgeComment,
  getHotKnowledge, getTags,
} from '@/api/task'
import type {
  KnowledgeDetailDto, KnowledgeListDto, KnowledgeCommentDto,
  UpdateKnowledgeRequest, TagListDto,
} from '@/types/task'
import AttachmentUpload from './components/AttachmentUpload.vue'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

const knowledgeId = computed(() => Number(route.params.knowledgeId))

const loading = ref(false)
const saving = ref(false)
const isEditing = ref(false)
const detail = ref<KnowledgeDetailDto | null>(null)
const comments = ref<KnowledgeCommentDto[]>([])
const relatedList = ref<KnowledgeListDto[]>([])
const allTags = ref<TagListDto[]>([])
const newComment = ref('')
const commentLoading = ref(false)

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

const tagOptions = computed(() =>
  allTags.value.map(t => ({ value: t.id, label: t.name }))
)

const form = reactive({
  title: '',
  content: '',
  category: undefined as number | undefined,
  status: 0,
  isPinned: false,
  tagIds: [] as number[],
})

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

function renderContent(content: string): string {
  // 简单的换行 → <br> 渲染，如需完整 Markdown 可集成 marked
  return content
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br>')
}

function goBack() {
  router.push({ name: 'KnowledgeList' })
}

function goToKnowledge(id: number) {
  router.push({ name: 'KnowledgeDetail', params: { knowledgeId: id } })
}

function fillForm(d: KnowledgeDetailDto) {
  form.title = d.title
  form.content = d.content || ''
  form.category = d.category
  form.status = d.status
  form.isPinned = d.isPinned
  form.tagIds = d.tags.map(t => t.id)
}

async function loadDetail() {
  loading.value = true
  try {
    detail.value = await getKnowledge(knowledgeId.value)
    fillForm(detail.value)
  } catch {
    message.error('获取知识详情失败')
  } finally {
    loading.value = false
  }
}

async function loadComments() {
  try {
    comments.value = await getKnowledgeComments(knowledgeId.value)
  } catch {
    // 评论加载失败不阻塞
  }
}

async function loadRelated() {
  try {
    // 使用热门知识作为相关推荐
    const hot = await getHotKnowledge()
    relatedList.value = hot.filter(k => k.id !== knowledgeId.value).slice(0, 5)
  } catch {
    // 不阻塞
  }
}

async function loadTags() {
  try {
    allTags.value = await getTags()
  } catch {
    // 不阻塞
  }
}

async function handleSave() {
  if (!form.title?.trim()) {
    message.warning('请输入标题')
    return
  }
  if (!form.category) {
    message.warning('请选择分类')
    return
  }
  saving.value = true
  try {
    await updateKnowledge(knowledgeId.value, {
      title: form.title,
      content: form.content || null,
      category: form.category!,
      status: form.status,
      isPinned: form.isPinned,
      tagIds: form.tagIds.length ? form.tagIds : null,
    } as UpdateKnowledgeRequest)
    message.success('保存成功')
    isEditing.value = false
    await loadDetail()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function handleLike() {
  try {
    await toggleKnowledgeLike(knowledgeId.value)
    await loadDetail()
  } catch {
    message.error('操作失败')
  }
}

async function handleCollect() {
  try {
    await toggleKnowledgeCollect(knowledgeId.value)
    await loadDetail()
  } catch {
    message.error('操作失败')
  }
}

async function handleAddComment() {
  if (!newComment.value.trim()) return
  commentLoading.value = true
  try {
    await createKnowledgeComment(knowledgeId.value, { content: newComment.value })
    message.success('评论发表成功')
    newComment.value = ''
    await loadComments()
  } catch {
    message.error('评论失败')
  } finally {
    commentLoading.value = false
  }
}

onMounted(() => {
  loadDetail()
  loadComments()
  loadRelated()
  loadTags()
})
</script>

<style scoped lang="scss">
.knowledge-detail {
  padding: 24px;

  &__toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;

    &-right {
      display: flex;
      gap: 8px;
    }
  }

  &__form {
    max-width: 800px;
  }

  &__view {
    max-width: 800px;

    h1 {
      font-size: 24px;
      font-weight: 700;
      margin: 0 0 12px;
    }
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
    color: #8c8c8c;
    font-size: 13px;
    flex-wrap: wrap;
  }

  &__tags {
    margin-bottom: 12px;
  }

  &__stats {
    padding: 12px 0;
    border-bottom: 1px solid #f0f0f0;
    margin-bottom: 24px;
    color: #595959;
    font-size: 14px;
  }

  &__action {
    cursor: pointer;
    transition: color 0.2s;

    &:hover, &.active {
      color: var(--color-primary);
    }
  }

  &__content {
    line-height: 1.8;
    font-size: 15px;
    color: #262626;
    min-height: 200px;
  }

  &__section {
    margin-top: 32px;

    h3 {
      font-size: 16px;
      font-weight: 600;
      margin-bottom: 16px;
    }
  }

  &__related {
    display: flex;
    flex-direction: column;
    gap: 8px;

    &-item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      background: #fafafa;
      border-radius: 6px;
      cursor: pointer;
      transition: background 0.2s;

      &:hover {
        background: #f0f0f0;
      }
    }

    &-category {
      font-size: 12px;
      color: #8c8c8c;
      flex-shrink: 0;
    }
  }

  &__comment-input {
    margin-bottom: 20px;
  }

  &__comments {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  &__comment-item {
    padding: 12px 0;
    border-bottom: 1px solid #f5f5f5;
  }

  &__comment-header {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 6px;
    font-size: 14px;
  }

  &__comment-time {
    font-size: 12px;
    color: #bfbfbf;
  }

  &__comment-body {
    font-size: 14px;
    line-height: 1.6;
    color: #434343;
  }

  &__comment-replies {
    margin-left: 24px;
    border-left: 2px solid #f0f0f0;
    padding-left: 16px;
    margin-top: 8px;
  }
}
</style>
