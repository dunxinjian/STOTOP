<template>
  <div class="schedule-manage">
    <div class="schedule-manage__header">
      <h2>调度管理</h2>
      <a-button type="primary" @click="handleCreate">
        <PlusOutlined /> 新建调度
      </a-button>
    </div>

    <!-- 筛选区 -->
    <div class="schedule-manage__filters">
      <a-select
        v-model:value="query.scheduleType"
        placeholder="调度类型"
        allow-clear
        style="width: 140px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="1">一次性</a-select-option>
        <a-select-option :value="2">每日</a-select-option>
        <a-select-option :value="3">每周</a-select-option>
        <a-select-option :value="4">Cron</a-select-option>
      </a-select>

      <a-select
        v-model:value="isEnabledFilter"
        placeholder="启用状态"
        allow-clear
        style="width: 120px"
        @change="() => handleSearch()"
      >
        <a-select-option :value="1">已启用</a-select-option>
        <a-select-option :value="0">已禁用</a-select-option>
      </a-select>

      <a-input-search
        v-model:value="query.keyword"
        placeholder="搜索调度名称"
        style="width: 240px"
        allow-clear
        @search="() => handleSearch()"
      />
    </div>

    <!-- 表格 -->
    <a-table
      :columns="columns"
      :data-source="list"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'templateTaskTitle'">
          {{ record.templateTaskTitle || '-' }}
        </template>

        <template v-else-if="column.key === 'scheduleType'">
          <a-tag :color="scheduleTypeColor(record.scheduleType)">
            {{ scheduleTypeLabel(record.scheduleType) }}
          </a-tag>
        </template>

        <template v-else-if="column.key === 'cronExpression'">
          {{ record.cronExpression || record.scheduledTime || '-' }}
        </template>

        <template v-else-if="column.key === 'nextExecution'">
          {{ record.nextExecution ? formatTime(record.nextExecution) : '-' }}
        </template>

        <template v-else-if="column.key === 'lastExecution'">
          {{ record.lastExecution ? formatTime(record.lastExecution) : '-' }}
        </template>

        <template v-else-if="column.key === 'isEnabled'">
          <a-switch
            :checked="record.isEnabled"
            checked-children="启用"
            un-checked-children="禁用"
            @change="handleToggle(record)"
          />
        </template>

        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="isEdit ? '编辑调度' : '新建调度'"
      :confirm-loading="submitLoading"
      @ok="handleSubmit"
      width="520px"
    >
      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
        <a-form-item v-if="!isEdit" label="模板任务ID" required>
          <a-input-number v-model:value="form.templateTaskId" :min="1" style="width: 100%" placeholder="输入模板任务ID" />
        </a-form-item>
        <a-form-item label="调度类型" required>
          <a-select v-model:value="form.scheduleType" placeholder="选择调度类型">
            <a-select-option :value="1">一次性</a-select-option>
            <a-select-option :value="2">每日</a-select-option>
            <a-select-option :value="3">每周</a-select-option>
            <a-select-option :value="4">Cron表达式</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item v-if="form.scheduleType === 4" label="Cron表达式">
          <a-input v-model:value="form.cronExpression" placeholder="例: 0 9 * * 1-5" />
        </a-form-item>
        <a-form-item v-if="form.scheduleType === 1" label="计划执行时间">
          <a-date-picker
            v-model:value="scheduledTimeValue"
            show-time
            style="width: 100%"
            placeholder="选择执行时间"
          />
        </a-form-item>
        <a-form-item v-if="isEdit" label="启用状态">
          <a-switch v-model:checked="form.isEnabled" checked-children="启用" un-checked-children="禁用" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { getSchedules, createSchedule, updateSchedule, toggleSchedule } from '@/api/task'
import type { TaskScheduleListDto, SchedulePagedRequest } from '@/types/task'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'

const loading = ref(false)
const list = ref<TaskScheduleListDto[]>([])

const query = reactive<SchedulePagedRequest>({
  pageIndex: 1,
  pageSize: 15,
  keyword: undefined,
  scheduleType: undefined,
  isEnabled: undefined,
})

// 启用状态筛选用数值编码(1/0)适配 a-select 的 SelectValue 类型，读写时与 query.isEnabled 的布尔值互转
const isEnabledFilter = computed<number | undefined>({
  get: () => (query.isEnabled === undefined ? undefined : query.isEnabled ? 1 : 0),
  set: (val) => { query.isEnabled = val === undefined ? undefined : val === 1 },
})

const pagination = reactive({
  current: 1,
  pageSize: 15,
  total: 0,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
})

const columns = [
  { title: '模板任务', key: 'templateTaskTitle', ellipsis: true },
  { title: '调度类型', key: 'scheduleType', width: 100 },
  { title: '调度规则', key: 'cronExpression', width: 180 },
  { title: '下次执行', key: 'nextExecution', width: 160 },
  { title: '上次执行', key: 'lastExecution', width: 160 },
  { title: '状态', key: 'isEnabled', width: 100 },
  { title: '操作', key: 'action', width: 80 },
]

const scheduleTypeLabels: Record<number, string> = { 1: '一次性', 2: '每日', 3: '每周', 4: 'Cron' }
const scheduleTypeColors: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'orange', 4: 'purple' }

function scheduleTypeLabel(t: number) { return scheduleTypeLabels[t] ?? `${t}` }
function scheduleTypeColor(t: number) { return scheduleTypeColors[t] ?? 'default' }
function formatTime(time: string) { return dayjs(time).format('YYYY-MM-DD HH:mm') }

async function loadData() {
  loading.value = true
  try {
    const res = await getSchedules({
      ...query,
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    })
    list.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取调度列表失败')
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

async function handleToggle(record: Record<string, any>) {
  try {
    await toggleSchedule(record.id)
    message.success(record.isEnabled ? '已禁用' : '已启用')
    loadData()
  } catch {
    message.error('操作失败')
  }
}

// ---------- 新建/编辑 ----------
const modalVisible = ref(false)
const submitLoading = ref(false)
const isEdit = ref(false)
const editId = ref(0)
const scheduledTimeValue = ref<Dayjs | undefined>(undefined)

const form = reactive({
  templateTaskId: undefined as number | undefined,
  scheduleType: undefined as number | undefined,
  cronExpression: '',
  scheduledTime: '',
  isEnabled: true,
})

function resetForm() {
  form.templateTaskId = undefined
  form.scheduleType = undefined
  form.cronExpression = ''
  form.scheduledTime = ''
  form.isEnabled = true
  scheduledTimeValue.value = undefined
}

function handleCreate() {
  isEdit.value = false
  editId.value = 0
  resetForm()
  modalVisible.value = true
}

function handleEdit(record: Record<string, any>) {
  const r = record as TaskScheduleListDto
  isEdit.value = true
  editId.value = r.id
  form.templateTaskId = r.templateTaskId
  form.scheduleType = r.scheduleType
  form.cronExpression = r.cronExpression || ''
  form.scheduledTime = r.scheduledTime || ''
  form.isEnabled = r.isEnabled
  scheduledTimeValue.value = r.scheduledTime ? dayjs(r.scheduledTime) : undefined
  modalVisible.value = true
}

async function handleSubmit() {
  if (!form.scheduleType) return message.warning('请选择调度类型')
  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updateSchedule(editId.value, {
        scheduleType: form.scheduleType,
        cronExpression: form.cronExpression || null,
        scheduledTime: scheduledTimeValue.value ? scheduledTimeValue.value.format('YYYY-MM-DD HH:mm:ss') : null,
        isEnabled: form.isEnabled,
      })
      message.success('更新成功')
    } else {
      if (!form.templateTaskId) return message.warning('请输入模板任务ID')
      await createSchedule({
        templateTaskId: form.templateTaskId,
        scheduleType: form.scheduleType,
        cronExpression: form.cronExpression || null,
        scheduledTime: scheduledTimeValue.value ? scheduledTimeValue.value.format('YYYY-MM-DD HH:mm:ss') : null,
      })
      message.success('创建成功')
    }
    modalVisible.value = false
    loadData()
  } catch {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitLoading.value = false
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.schedule-manage {
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
