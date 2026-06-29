<template>
  <div class="knowledge-create">
    <!-- 顶部 -->
    <div class="knowledge-create__toolbar">
      <a-button @click="goBack"><ArrowLeftOutlined /> 返回列表</a-button>
      <div class="knowledge-create__toolbar-right">
        <a-button @click="handleSave(0)" :loading="saving">存为草稿</a-button>
        <a-button type="primary" @click="handleSave(1)" :loading="saving">发布</a-button>
      </div>
    </div>

    <a-form layout="vertical" class="knowledge-create__form">
      <a-form-item label="标题" required>
        <a-input v-model:value="form.title" placeholder="输入知识标题" size="large" />
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

      <a-form-item label="摘要">
        <a-textarea
          v-model:value="form.summary"
          placeholder="简短描述知识要点（可选）"
          :rows="2"
          show-count
          :maxlength="200"
        />
      </a-form-item>

      <a-form-item label="正文内容">
        <a-textarea
          v-model:value="form.content"
          placeholder="知识正文（支持 Markdown 格式）"
          :rows="18"
          show-count
        />
      </a-form-item>

      <!-- 来源关联（可选） -->
      <a-collapse :bordered="false" ghost>
        <a-collapse-panel key="source" header="来源关联（可选）">
          <a-row :gutter="16">
            <a-col :span="8">
              <a-form-item label="来源复盘ID">
                <a-input-number v-model:value="form.sourceReviewId" placeholder="复盘ID" style="width: 100%" />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="来源任务ID">
                <a-input-number v-model:value="form.sourceTaskId" placeholder="任务ID" style="width: 100%" />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="来源项目ID">
                <a-input-number v-model:value="form.sourceProjectId" placeholder="项目ID" style="width: 100%" />
              </a-form-item>
            </a-col>
          </a-row>
        </a-collapse-panel>
      </a-collapse>
    </a-form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { ArrowLeftOutlined } from '@ant-design/icons-vue'
import { createKnowledge, getTags } from '@/api/task'
import type { CreateKnowledgeRequest, TagListDto } from '@/types/task'

const router = useRouter()

const saving = ref(false)
const allTags = ref<TagListDto[]>([])

const categoryOptions = [
  { value: 1, label: '经验总结' },
  { value: 2, label: '最佳实践' },
  { value: 3, label: '问题解决方案' },
  { value: 4, label: '工具方法' },
  { value: 5, label: '其他' },
]

const tagOptions = computed(() =>
  allTags.value.map(t => ({ value: t.id, label: t.name }))
)

const form = reactive({
  title: '',
  content: '',
  summary: '',
  category: undefined as number | undefined,
  tagIds: [] as number[],
  sourceReviewId: undefined as number | undefined,
  sourceTaskId: undefined as number | undefined,
  sourceProjectId: undefined as number | undefined,
})

function goBack() {
  router.push({ name: 'KnowledgeList' })
}

async function loadTags() {
  try {
    allTags.value = await getTags()
  } catch {
    // 不阻塞
  }
}

async function handleSave(status: number) {
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
    // 将摘要拼入正文前部（如有）
    const fullContent = form.summary
      ? `${form.summary}\n\n---\n\n${form.content}`
      : form.content

    const res = await createKnowledge({
      title: form.title,
      content: fullContent || null,
      category: form.category!,
      sourceReviewId: form.sourceReviewId || null,
      sourceTaskId: form.sourceTaskId || null,
      sourceProjectId: form.sourceProjectId || null,
      tagIds: form.tagIds.length ? form.tagIds : null,
    } as CreateKnowledgeRequest)

    message.success(status === 1 ? '知识已发布' : '草稿已保存')
    router.push({ name: 'KnowledgeDetail', params: { knowledgeId: res.id } })
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

onMounted(loadTags)
</script>

<style scoped lang="scss">
.knowledge-create {
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
}
</style>
