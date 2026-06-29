<template>
  <div class="review-detail">
    <!-- 顶部操作栏 -->
    <div class="review-detail__toolbar">
      <a-button @click="goBack"><ArrowLeftOutlined /> 返回列表</a-button>
      <div class="review-detail__toolbar-right">
        <template v-if="isEditing">
          <a-button @click="handleSaveDraft" :loading="saving">保存草稿</a-button>
          <a-button type="primary" @click="handlePublish" :loading="saving">发布</a-button>
        </template>
        <template v-else-if="detail && detail.status === 0">
          <a-button type="primary" @click="isEditing = true"><EditOutlined /> 编辑</a-button>
        </template>
      </div>
    </div>

    <a-spin :spinning="loading">
      <!-- 编辑模式 -->
      <template v-if="isEditing">
        <a-form layout="vertical" class="review-detail__form">
          <a-form-item label="复盘标题" required>
            <a-input v-model:value="form.title" placeholder="请输入复盘标题" />
          </a-form-item>

          <a-row :gutter="16">
            <a-col :span="8">
              <a-form-item label="关联类型" required>
                <a-select v-model:value="form.relationType" placeholder="选择关联类型">
                  <a-select-option :value="1">项目</a-select-option>
                  <a-select-option :value="2">目标</a-select-option>
                  <a-select-option :value="3">任务</a-select-option>
                </a-select>
              </a-form-item>
            </a-col>
            <a-col :span="16">
              <a-form-item label="关联对象ID" required>
                <a-input-number v-model:value="form.relationId" placeholder="请输入关联ID" style="width: 100%" />
              </a-form-item>
            </a-col>
          </a-row>

          <!-- 四象限复盘模型 -->
          <div class="review-detail__quadrant-title">四象限复盘</div>
          <a-row :gutter="16">
            <a-col :span="12">
              <div class="quadrant quadrant--continue">
                <div class="quadrant__header">
                  <CheckCircleOutlined /> 做得好的（Continue）
                </div>
                <a-textarea
                  v-model:value="form.wentWell"
                  placeholder="哪些做法效果不错，值得继续保持？"
                  :rows="6"
                  :bordered="false"
                />
              </div>
            </a-col>
            <a-col :span="12">
              <div class="quadrant quadrant--stop">
                <div class="quadrant__header">
                  <CloseCircleOutlined /> 需改进的（Stop）
                </div>
                <a-textarea
                  v-model:value="form.toImprove"
                  placeholder="哪些做法效果不好，需要停止或改进？"
                  :rows="6"
                  :bordered="false"
                />
              </div>
            </a-col>
          </a-row>
          <a-row :gutter="16" style="margin-top: 16px">
            <a-col :span="12">
              <div class="quadrant quadrant--start">
                <div class="quadrant__header">
                  <BulbOutlined /> 可以尝试的（Start）
                </div>
                <a-textarea
                  v-model:value="form.lessonsLearned"
                  placeholder="有哪些新想法或经验值得尝试？"
                  :rows="6"
                  :bordered="false"
                />
              </div>
            </a-col>
            <a-col :span="12">
              <div class="quadrant quadrant--ask">
                <div class="quadrant__header">
                  <QuestionCircleOutlined /> 困惑/疑问（Ask）
                </div>
                <a-textarea
                  v-model:value="form.actionPlan"
                  placeholder="还有哪些疑问或困惑需要解决？"
                  :rows="6"
                  :bordered="false"
                />
              </div>
            </a-col>
          </a-row>
        </a-form>
      </template>

      <!-- 查看模式 -->
      <template v-else-if="detail">
        <div class="review-detail__view">
          <h2>{{ detail.title }}</h2>
          <div class="review-detail__meta">
            <a-tag :color="detail.status === 1 ? 'green' : 'default'">
              {{ detail.status === 1 ? '已发布' : '草稿' }}
            </a-tag>
            <span>{{ relationTypeMap[detail.relationType] }}：{{ detail.relationTitle || '-' }}</span>
            <span>创建人：{{ detail.reviewerName }}</span>
            <span>{{ formatTime(detail.createTime) }}</span>
          </div>

          <!-- 四象限查看 -->
          <a-row :gutter="16" class="review-detail__quadrants">
            <a-col :span="12">
              <div class="quadrant quadrant--continue">
                <div class="quadrant__header">
                  <CheckCircleOutlined /> 做得好的（Continue）
                </div>
                <div class="quadrant__content">{{ detail.wentWell || '暂无内容' }}</div>
              </div>
            </a-col>
            <a-col :span="12">
              <div class="quadrant quadrant--stop">
                <div class="quadrant__header">
                  <CloseCircleOutlined /> 需改进的（Stop）
                </div>
                <div class="quadrant__content">{{ detail.toImprove || '暂无内容' }}</div>
              </div>
            </a-col>
          </a-row>
          <a-row :gutter="16" style="margin-top: 16px">
            <a-col :span="12">
              <div class="quadrant quadrant--start">
                <div class="quadrant__header">
                  <BulbOutlined /> 可以尝试的（Start）
                </div>
                <div class="quadrant__content">{{ detail.lessonsLearned || '暂无内容' }}</div>
              </div>
            </a-col>
            <a-col :span="12">
              <div class="quadrant quadrant--ask">
                <div class="quadrant__header">
                  <QuestionCircleOutlined /> 困惑/疑问（Ask）
                </div>
                <div class="quadrant__content">{{ detail.actionPlan || '暂无内容' }}</div>
              </div>
            </a-col>
          </a-row>

          <!-- 参与人 -->
          <div v-if="detail.participants && detail.participants.length > 0" class="review-detail__section">
            <h3>参与人</h3>
            <a-space>
              <a-tag v-for="p in detail.participants" :key="p.userId">{{ p.userName }}</a-tag>
            </a-space>
          </div>
        </div>
      </template>
    </a-spin>

    <!-- 附件区 -->
    <div v-if="reviewId" class="review-detail__section">
      <h3>附件</h3>
      <AttachmentUpload :related-type="5" :related-id="reviewId" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  ArrowLeftOutlined, EditOutlined,
  CheckCircleOutlined, CloseCircleOutlined,
  BulbOutlined, QuestionCircleOutlined,
} from '@ant-design/icons-vue'
import { getReview, createReview, updateReview, publishReview } from '@/api/task'
import type { ReviewDetailDto, CreateReviewRequest, UpdateReviewRequest } from '@/types/task'
import AttachmentUpload from './components/AttachmentUpload.vue'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

const reviewId = computed(() => {
  const id = route.params.reviewId
  return id ? Number(id) : 0
})

const isNew = computed(() => !reviewId.value)
const loading = ref(false)
const saving = ref(false)
const isEditing = ref(false)
const detail = ref<ReviewDetailDto | null>(null)

const relationTypeMap: Record<number, string> = {
  1: '项目',
  2: '目标',
  3: '任务',
}

const form = reactive({
  title: '',
  relationType: undefined as number | undefined,
  relationId: undefined as number | undefined,
  wentWell: '',
  toImprove: '',
  lessonsLearned: '',
  actionPlan: '',
  participantIds: [] as number[],
})

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

function goBack() {
  router.push({ name: 'ReviewList' })
}

function fillForm(d: ReviewDetailDto) {
  form.title = d.title
  form.relationType = d.relationType
  form.relationId = d.relationId
  form.wentWell = d.wentWell || ''
  form.toImprove = d.toImprove || ''
  form.lessonsLearned = d.lessonsLearned || ''
  form.actionPlan = d.actionPlan || ''
  form.participantIds = d.participants?.map(p => p.userId) || []
}

async function loadDetail() {
  if (isNew.value) {
    isEditing.value = true
    return
  }
  loading.value = true
  try {
    detail.value = await getReview(reviewId.value)
    fillForm(detail.value)
    // 如果带了 edit query 且是草稿，自动进入编辑模式
    if (route.query.edit === '1' && detail.value.status === 0) {
      isEditing.value = true
    }
  } catch {
    message.error('获取复盘详情失败')
  } finally {
    loading.value = false
  }
}

function validateForm(): boolean {
  if (!form.title?.trim()) {
    message.warning('请输入复盘标题')
    return false
  }
  if (!form.relationType || !form.relationId) {
    message.warning('请选择关联类型和关联对象')
    return false
  }
  return true
}

async function handleSaveDraft() {
  if (!validateForm()) return
  saving.value = true
  try {
    if (isNew.value) {
      const res = await createReview({
        title: form.title,
        relationType: form.relationType!,
        relationId: form.relationId!,
        wentWell: form.wentWell || null,
        toImprove: form.toImprove || null,
        lessonsLearned: form.lessonsLearned || null,
        actionPlan: form.actionPlan || null,
        participantIds: form.participantIds.length ? form.participantIds : null,
      } as CreateReviewRequest)
      message.success('草稿已保存')
      router.replace({ name: 'ReviewDetail', params: { reviewId: res.id } })
    } else {
      await updateReview(reviewId.value, {
        title: form.title,
        wentWell: form.wentWell || null,
        toImprove: form.toImprove || null,
        lessonsLearned: form.lessonsLearned || null,
        actionPlan: form.actionPlan || null,
        participantIds: form.participantIds.length ? form.participantIds : null,
      } as UpdateReviewRequest)
      message.success('草稿已保存')
      await loadDetail()
    }
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function handlePublish() {
  if (!validateForm()) return
  saving.value = true
  try {
    // 先保存再发布
    if (isNew.value) {
      const res = await createReview({
        title: form.title,
        relationType: form.relationType!,
        relationId: form.relationId!,
        wentWell: form.wentWell || null,
        toImprove: form.toImprove || null,
        lessonsLearned: form.lessonsLearned || null,
        actionPlan: form.actionPlan || null,
        participantIds: form.participantIds.length ? form.participantIds : null,
      } as CreateReviewRequest)
      await publishReview(res.id)
      message.success('复盘已发布')
      router.replace({ name: 'ReviewDetail', params: { reviewId: res.id } })
    } else {
      await updateReview(reviewId.value, {
        title: form.title,
        wentWell: form.wentWell || null,
        toImprove: form.toImprove || null,
        lessonsLearned: form.lessonsLearned || null,
        actionPlan: form.actionPlan || null,
        participantIds: form.participantIds.length ? form.participantIds : null,
      } as UpdateReviewRequest)
      await publishReview(reviewId.value)
      message.success('复盘已发布')
    }
    isEditing.value = false
    await loadDetail()
  } catch {
    message.error('发布失败')
  } finally {
    saving.value = false
  }
}

onMounted(loadDetail)
</script>

<style scoped lang="scss">
.review-detail {
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
    max-width: 960px;
  }

  &__quadrant-title {
    font-size: 16px;
    font-weight: 600;
    margin-bottom: 12px;
    color: #262626;
  }

  &__view {
    h2 {
      margin: 0 0 12px;
      font-size: 22px;
      font-weight: 600;
    }
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-bottom: 24px;
    color: #8c8c8c;
    font-size: 13px;
  }

  &__quadrants {
    margin-top: 20px;
  }

  &__section {
    margin-top: 32px;

    h3 {
      font-size: 16px;
      font-weight: 600;
      margin-bottom: 12px;
    }
  }
}

.quadrant {
  border-radius: 8px;
  padding: 16px;
  min-height: 180px;

  &__header {
    font-size: 15px;
    font-weight: 600;
    margin-bottom: 10px;
    display: flex;
    align-items: center;
    gap: 6px;
  }

  &__content {
    font-size: 14px;
    line-height: 1.8;
    white-space: pre-wrap;
    color: #434343;
  }

  &--continue {
    background: var(--color-success-light);
    border: 1px solid #b7eb8f;

    .quadrant__header { color: var(--color-success-text); }
  }

  &--stop {
    background: var(--color-danger-light);
    border: 1px solid #ffccc7;

    .quadrant__header { color: var(--color-danger); }
  }

  &--start {
    background: var(--color-info-light);
    border: 1px solid var(--color-info);

    .quadrant__header { color: var(--color-info); }
  }

  &--ask {
    background: var(--color-warning-light);
    border: 1px solid #ffe58f;

    .quadrant__header { color: var(--color-warning); }
  }

  :deep(.ant-input) {
    background: transparent;
    resize: none;
  }
}
</style>
