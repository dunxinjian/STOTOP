<template>
  <a-form
    ref="formRef"
    :model="formState"
    :rules="rules"
    layout="vertical"
    class="task-form"
  >
    <a-form-item label="任务标题" name="title">
      <a-input v-model:value="formState.title" placeholder="请输入任务标题" :maxlength="200" />
    </a-form-item>

    <a-form-item label="任务描述" name="description">
      <a-textarea
        v-model:value="formState.description"
        placeholder="请输入任务描述"
        :rows="4"
        :maxlength="2000"
        show-count
      />
    </a-form-item>

    <a-row :gutter="16">
      <a-col :span="12">
        <a-form-item label="优先级" name="priority">
          <a-select v-model:value="formState.priority" placeholder="请选择优先级">
            <a-select-option :value="0">低</a-select-option>
            <a-select-option :value="1">中</a-select-option>
            <a-select-option :value="2">高</a-select-option>
            <a-select-option :value="3">紧急</a-select-option>
          </a-select>
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="任务类型" name="type">
          <a-select v-model:value="formState.type" placeholder="请选择类型">
            <a-select-option :value="0">常规任务</a-select-option>
            <a-select-option :value="1">里程碑</a-select-option>
            <a-select-option :value="2">缺陷</a-select-option>
          </a-select>
        </a-form-item>
      </a-col>
    </a-row>

    <a-row :gutter="16">
      <a-col :span="12">
        <a-form-item label="负责人" name="assigneeId">
          <a-select
            v-model:value="formState.assigneeId"
            placeholder="请选择负责人"
            show-search
            allow-clear
            :filter-option="false"
            @search="handleUserSearch"
          >
            <a-select-option v-for="u in userOptions" :key="u.id" :value="u.id">
              {{ u.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="所属项目" name="projectId">
          <a-select
            v-model:value="formState.projectId"
            placeholder="请选择项目（可选）"
            allow-clear
            :options="projectOptions"
            :field-names="{ label: 'name', value: 'id' }"
          />
        </a-form-item>
      </a-col>
    </a-row>

    <a-row :gutter="16">
      <a-col :span="12">
        <a-form-item label="开始日期" name="planStart">
          <a-date-picker v-model:value="formState.planStart" style="width: 100%" value-format="YYYY-MM-DD" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="截止日期" name="planEnd">
          <a-date-picker v-model:value="formState.planEnd" style="width: 100%" value-format="YYYY-MM-DD" />
        </a-form-item>
      </a-col>
    </a-row>

    <a-form-item label="标签" name="tagIds">
      <a-select
        v-model:value="formState.tagIds"
        mode="multiple"
        placeholder="选择标签"
        :options="tagOptions"
        :field-names="{ label: 'name', value: 'id' }"
      />
    </a-form-item>

    <a-form-item label="可见范围" name="visibility">
      <VisibilityConfig v-model="formState.visibility" />
    </a-form-item>

    <a-form-item label="预估工时（小时）" name="estimatedHours">
      <a-input-number v-model:value="formState.estimatedHours" :min="0" :max="9999" placeholder="预估工时" style="width: 160px" />
    </a-form-item>

    <div class="task-form__footer">
      <a-button @click="emit('cancel')">取消</a-button>
      <a-button type="primary" :loading="submitting" @click="handleSubmit">
        {{ isEdit ? '保存修改' : '创建任务' }}
      </a-button>
    </div>
  </a-form>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { createTask, updateTask, getTags, getProjects } from '@/api/task'
import { get } from '@/api/request'
import type { TaskDetailDto, TagListDto, ProjectListDto } from '@/types/task'
import VisibilityConfig from './VisibilityConfig.vue'

interface UserOption {
  id: number
  name: string
}

const props = defineProps<{
  task?: TaskDetailDto
  projectId?: number
  parentId?: number
}>()

const emit = defineEmits<{
  (e: 'submit', task: TaskDetailDto): void
  (e: 'cancel'): void
}>()

const isEdit = computed(() => !!props.task)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const userOptions = ref<UserOption[]>([])
const tagOptions = ref<TagListDto[]>([])
const projectOptions = ref<ProjectListDto[]>([])

const formState = reactive({
  title: '',
  description: undefined as string | undefined,
  priority: 1,
  type: 0,
  assigneeId: undefined as number | undefined,
  projectId: undefined as number | undefined,
  planStart: undefined as string | undefined,
  planEnd: undefined as string | undefined,
  tagIds: [] as number[],
  visibility: 0,
  estimatedHours: undefined as number | undefined,
})

const rules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入任务标题', trigger: 'blur' }],
}

// Initialize form from task prop (edit mode)
watch(
  () => props.task,
  (task) => {
    if (task) {
      formState.title = task.title
      formState.description = task.description ?? undefined
      formState.priority = task.priority
      formState.type = task.type
      formState.assigneeId = task.assigneeId ?? undefined
      formState.projectId = task.projectId ?? undefined
      formState.planStart = task.planStart ?? undefined
      formState.planEnd = task.planEnd ?? undefined
      formState.tagIds = task.tags.map((t) => t.id)
      formState.visibility = task.visibility
      formState.estimatedHours = task.estimatedHours ?? undefined
      // Pre-fill user option for the assignee
      if (task.assigneeId && task.assigneeName) {
        userOptions.value = [{ id: task.assigneeId, name: task.assigneeName }]
      }
    }
  },
  { immediate: true },
)

// Set default project from prop
watch(
  () => props.projectId,
  (pid) => {
    if (pid && !formState.projectId) {
      formState.projectId = pid
    }
  },
  { immediate: true },
)

let searchTimer: ReturnType<typeof setTimeout> | null = null

function handleUserSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      userOptions.value = await get<UserOption[]>('/system/users/search', { keyword })
    } catch {
      // ignore
    }
  }, 300)
}

async function loadOptions() {
  try {
    const [tags, projects] = await Promise.all([
      getTags(),
      getProjects({ pageSize: 200 }),
    ])
    tagOptions.value = tags
    projectOptions.value = projects.items
  } catch {
    // ignore
  }
}

async function handleSubmit() {
  try {
    await formRef.value?.validateFields()
  } catch {
    return
  }
  submitting.value = true
  try {
    let result: TaskDetailDto
    if (isEdit.value && props.task) {
      result = await updateTask(props.task.id, {
        title: formState.title,
        description: formState.description,
        projectId: formState.projectId ?? null,
        type: formState.type,
        priority: formState.priority,
        assigneeId: formState.assigneeId ?? null,
        planStart: formState.planStart,
        planEnd: formState.planEnd,
        estimatedHours: formState.estimatedHours,
        visibility: formState.visibility,
        tagIds: formState.tagIds.length > 0 ? formState.tagIds : null,
      })
      message.success('任务已更新')
    } else {
      result = await createTask({
        title: formState.title,
        description: formState.description,
        projectId: formState.projectId ?? null,
        parentTaskId: props.parentId,
        type: formState.type,
        priority: formState.priority,
        assigneeId: formState.assigneeId ?? null,
        planStart: formState.planStart,
        planEnd: formState.planEnd,
        estimatedHours: formState.estimatedHours,
        visibility: formState.visibility,
        tagIds: formState.tagIds.length > 0 ? formState.tagIds : null,
      })
      message.success('任务已创建')
    }
    emit('submit', result)
  } catch {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}

onMounted(loadOptions)
</script>

<style scoped lang="scss">
.task-form {
  &__footer {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-top: 16px;
    border-top: 1px solid #f0f0f0;
  }
}
</style>
