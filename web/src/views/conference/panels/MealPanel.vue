<template>
  <div class="meal-panel">
    <!-- 工具栏 -->
    <SmartActionBar description="管理活动期间的餐食安排">
      <a-button type="primary" @click="handleAutoGenerate" :loading="generating">
        <ThunderboltOutlined />自动生成餐食计划
      </a-button>
      <a-button @click="showAddModal"><PlusOutlined />手动添加</a-button>
    </SmartActionBar>

    <!-- 餐食计划表格 -->
    <a-table
      :columns="columns"
      :data-source="mealPlans"
      row-key="id"
      :loading="loading"
      :pagination="false"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'date'">
          {{ record.date?.substring(0, 10) }}
        </template>
        <template v-else-if="column.dataIndex === 'mealType'">
          <a-tag :color="mealTypeColor(record.mealType)">{{ record.mealType }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'actualCount'">
          <span
            class="editable-cell"
            @click="handleEditActualCount(record as MealPlanListItemDto)"
          >
            {{ record.actualCount ?? '-' }}
            <EditOutlined class="editable-cell__icon" />
          </span>
        </template>
        <template v-else-if="column.dataIndex === 'budget'">
          ¥{{ ((record.expectedCount || 0) * 80).toLocaleString() }}
        </template>
        <template v-else-if="column.dataIndex === 'remark'">
          <a-tooltip v-if="record.remark" :title="record.remark">
            <span class="text-ellipsis">{{ record.remark }}</span>
          </a-tooltip>
          <span v-else>-</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a @click="handleEdit(record as MealPlanListItemDto)">编辑</a>
            <a @click="handleArrangeAttendees(record as MealPlanListItemDto)">安排人员</a>
            <a-popconfirm title="确定删除此餐食计划？" @confirm="handleDelete(record.id)">
              <a class="danger-link">删除</a>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 编辑/新增 Modal -->
    <a-modal
      v-model:open="editModalVisible"
      :title="editingRecord ? '编辑餐食计划' : '新增餐食计划'"
      @ok="handleSaveForm"
      :confirm-loading="saving"
      :width="520"
    >
      <a-form :model="formState" layout="vertical" ref="formRef">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="日期" name="date" :rules="[{ required: true, message: '请选择日期' }]">
              <a-date-picker v-model:value="formState.date" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="餐次" name="mealType" :rules="[{ required: true, message: '请选择餐次' }]">
              <a-select v-model:value="formState.mealType" placeholder="请选择">
                <a-select-option value="早餐">早餐</a-select-option>
                <a-select-option value="午餐">午餐</a-select-option>
                <a-select-option value="晚餐">晚餐</a-select-option>
                <a-select-option value="茶歇">茶歇</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="地点" name="location">
          <a-input v-model:value="formState.location" placeholder="餐厅/地点" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="预计人数" name="expectedCount" :rules="[{ required: true, message: '请输入预计人数' }]">
              <a-input-number v-model:value="formState.expectedCount" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="用餐方式" name="diningMode">
              <a-select v-model:value="formState.diningMode" placeholder="请选择" allow-clear>
                <a-select-option value="桌餐">桌餐</a-select-option>
                <a-select-option value="自助">自助</a-select-option>
                <a-select-option value="盒饭">盒饭</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="菜单备注" name="remark">
          <a-textarea v-model:value="formState.remark" :rows="3" placeholder="菜单或其他备注信息" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 安排人员 Modal -->
    <a-modal
      v-model:open="attendeeModalVisible"
      title="安排用餐人员"
      :width="700"
      @ok="handleSaveAttendees"
      :confirm-loading="savingAttendees"
    >
      <a-transfer
        v-model:target-keys="selectedAttendeeIds"
        :data-source="allAttendees"
        :titles="['未安排', '已安排']"
        :render="(item: any) => `${item.title} - ${item.description || ''}`"
        show-search
        :filter-option="filterAttendee"
        :list-style="{ width: '280px', height: '360px' }"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { ThunderboltOutlined, PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import SmartActionBar from '../components/SmartActionBar.vue'
import {
  getMealPlans, createMealPlan, updateMealPlan, deleteMealPlan,
  setMealAttendees, autoGenerateMealPlans, getAttendees
} from '@/api/conference'
import type {
  MealPlanListItemDto, MealPlanDto, CreateMealPlanRequest,
  AttendeeListItemDto, MealAttendeeInput
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

const loading = ref(false)
const generating = ref(false)
const saving = ref(false)
const savingAttendees = ref(false)
const mealPlans = ref<MealPlanListItemDto[]>([])
const editModalVisible = ref(false)
const attendeeModalVisible = ref(false)
const editingRecord = ref<MealPlanListItemDto | null>(null)
const currentMealId = ref<number>(0)
const formRef = ref()

const formState = ref<CreateMealPlanRequest>({
  date: '',
  mealType: '',
  location: '',
  expectedCount: 0,
  diningMode: '',
  remark: '',
})

const allAttendees = ref<Array<{ key: string; title: string; description: string }>>([])
const selectedAttendeeIds = ref<string[]>([])

const columns = [
  { title: '日期', dataIndex: 'date', width: 120 },
  { title: '餐次', dataIndex: 'mealType', width: 90 },
  { title: '用餐方式', dataIndex: 'diningMode', width: 90 },
  { title: '餐厅/地点', dataIndex: 'location', width: 140, ellipsis: true },
  { title: '预计人数', dataIndex: 'expectedCount', width: 90, align: 'right' as const },
  { title: '实际人数', dataIndex: 'actualCount', width: 90, align: 'right' as const },
  { title: '桌数', dataIndex: 'tableCount', width: 70, align: 'right' as const },
  { title: '菜单备注', dataIndex: 'remark', width: 160, ellipsis: true },
  { title: '预算', dataIndex: 'budget', width: 100, align: 'right' as const },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const },
]

function mealTypeColor(type: string) {
  const map: Record<string, string> = { '早餐': 'cyan', '午餐': 'blue', '晚餐': 'orange', '茶歇': 'green' }
  return map[type] || 'default'
}

async function loadMealPlans() {
  loading.value = true
  try {
    const res: any = await getMealPlans(props.eventId)
    mealPlans.value = res ?? []
  } catch {
    message.error('加载餐食计划失败')
  } finally {
    loading.value = false
  }
}

const handleAutoGenerate = async () => {
  generating.value = true
  try {
    await autoGenerateMealPlans(props.eventId)
    message.success('餐食计划生成成功')
    loadMealPlans()
  } catch {
    message.error('自动生成失败')
  } finally {
    generating.value = false
  }
}

function showAddModal() {
  editingRecord.value = null
  formState.value = { date: '', mealType: '', location: '', expectedCount: 0, diningMode: '', remark: '' }
  editModalVisible.value = true
}

function handleEdit(record: MealPlanListItemDto) {
  editingRecord.value = record
  formState.value = {
    date: record.date?.substring(0, 10) || '',
    mealType: record.mealType,
    location: record.location || '',
    expectedCount: record.expectedCount,
    diningMode: record.diningMode || '',
    remark: '',
  }
  editModalVisible.value = true
}

async function handleSaveForm() {
  try {
    await formRef.value?.validateFields()
  } catch { return }

  saving.value = true
  try {
    if (editingRecord.value) {
      await updateMealPlan(editingRecord.value.id, {
        date: formState.value.date,
        mealType: formState.value.mealType,
        diningMode: formState.value.diningMode,
        location: formState.value.location,
        expectedCount: formState.value.expectedCount,
        remark: formState.value.remark,
      })
      message.success('更新成功')
    } else {
      await createMealPlan(props.eventId, formState.value)
      message.success('创建成功')
    }
    editModalVisible.value = false
    loadMealPlans()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(id: number) {
  try {
    await deleteMealPlan(id)
    message.success('删除成功')
    loadMealPlans()
  } catch {
    message.error('删除失败')
  }
}

function handleEditActualCount(record: MealPlanListItemDto) {
  // Quick inline edit via prompt
  const val = prompt('请输入实际人数', String(record.actualCount || 0))
  if (val === null) return
  const num = parseInt(val)
  if (isNaN(num)) return
  updateMealPlan(record.id, {
    date: record.date?.substring(0, 10) || '',
    mealType: record.mealType,
    diningMode: record.diningMode,
    location: record.location || '',
    expectedCount: record.expectedCount,
    remark: '',
  }).then(() => {
    message.success('更新成功')
    loadMealPlans()
  })
}

async function handleArrangeAttendees(record: MealPlanListItemDto) {
  currentMealId.value = record.id
  // Load all attendees for transfer
  try {
    const res: any = await getAttendees(props.eventId, { pageSize: 9999 })
    const list: AttendeeListItemDto[] = res?.items ?? res ?? []
    allAttendees.value = list.map((a) => ({
      key: String(a.id),
      title: a.name,
      description: a.organization || '',
    }))
    // Load current meal attendees
    const mealDetail: any = await getMealPlans(props.eventId)
    const plan: MealPlanDto | undefined = (mealDetail ?? []).find?.((m: any) => m.id === record.id)
    selectedAttendeeIds.value = (plan as any)?.attendees?.map((a: any) => String(a.attendeeId)) ?? []
  } catch {
    allAttendees.value = []
    selectedAttendeeIds.value = []
  }
  attendeeModalVisible.value = true
}

function filterAttendee(inputValue: string, option: any) {
  return option.title.includes(inputValue) || option.description.includes(inputValue)
}

async function handleSaveAttendees() {
  savingAttendees.value = true
  try {
    const attendees: MealAttendeeInput[] = selectedAttendeeIds.value.map((id) => ({
      attendeeId: Number(id),
    }))
    await setMealAttendees(currentMealId.value, { attendees })
    message.success('人员安排成功')
    attendeeModalVisible.value = false
    loadMealPlans()
  } catch {
    message.error('保存失败')
  } finally {
    savingAttendees.value = false
  }
}

onMounted(() => {
  loadMealPlans()
})
</script>

<style scoped lang="scss">
.meal-panel {
  padding: 0;
}

.editable-cell {
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: background 0.2s;

  &:hover {
    background: #f0f5ff;
  }

  &__icon {
    font-size: 12px;
    color: #bfbfbf;
    margin-left: 4px;
  }
}

.text-ellipsis {
  max-width: 140px;
  display: inline-block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}

.danger-link {
  color: var(--color-danger);
}
</style>
