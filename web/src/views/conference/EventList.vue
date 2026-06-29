<template>
  <div class="page-container">
    <PageHeader title="会务管理">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建活动
        </a-button>
      </template>
      <template #toolbar>
        <div class="toolbar-search">
          <a-segmented v-model:value="statusFilter" :options="statusSegments" @change="handleSearch" />
          <a-input-search
            v-model:value="keyword"
            placeholder="搜索活动名称..."
            allow-clear
            style="width: 240px; margin-left: auto;"
            @search="handleSearch"
          />
        </div>
      </template>
    </PageHeader>

    <!-- 活动卡片网格 -->
    <div v-if="loading" class="loading-wrap">
      <a-spin size="large" />
    </div>
    <template v-else-if="filteredList.length">
      <a-row :gutter="16">
        <a-col v-for="item in filteredList" :key="item.id" :span="6" style="margin-bottom: 16px;">
          <a-card
            hoverable
            class="event-card"
            @click="handleCardClick(item)"
          >
            <template #title>
              <div class="card-title-row">
                <span class="card-title-text">{{ item.name }}</span>
                <a-tag v-if="item.type === 'wedding'" color="pink" style="margin: 0">婚礼</a-tag>
                <a-tag v-else color="blue" style="margin: 0">会议</a-tag>
                <a-badge :status="statusBadgeMap[item.status]" :text="statusTextMap[item.status]" />
              </div>
            </template>
            <div class="card-body">
              <div class="card-info-row">
                <EnvironmentOutlined class="card-icon" />
                <span>{{ item.location || '未设置地点' }}</span>
              </div>
              <div class="card-info-row">
                <CalendarOutlined class="card-icon" />
                <span>{{ formatDate(item.startDate) }} ~ {{ formatDate(item.endDate) }}</span>
              </div>
              <div v-if="item.type === 'wedding' && (item.groomName || item.brideName)" class="wedding-info">
                💒 {{ item.groomName }} & {{ item.brideName }}
              </div>
            </div>
            <template #actions>
              <span><UserOutlined /> {{ item.attendeeCount ?? 0 }}人</span>
              <span><CalendarOutlined /> {{ item.scheduleCount ?? 0 }}个日程</span>
              <span><CarOutlined /> {{ item.vehicleCount ?? 0 }}辆车</span>
            </template>
          </a-card>
        </a-col>
      </a-row>
    </template>
    <EmptyState v-else title="暂无活动数据">
      <a-button type="primary" @click="handleAdd">创建第一个活动</a-button>
    </EmptyState>

    <!-- 新建活动弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      title="新建活动"
      width="680px"
      :destroy-on-close="true"
      @cancel="modalVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="活动名称" name="Name">
          <a-input v-model:value="formData.Name" placeholder="请输入活动名称" :maxlength="200" />
        </a-form-item>
        <a-form-item label="活动类型">
          <a-radio-group v-model:value="formData.Type">
            <a-radio-button value="conference">会议/培训</a-radio-button>
            <a-radio-button value="wedding">婚礼</a-radio-button>
          </a-radio-group>
        </a-form-item>
        <template v-if="formData.Type === 'wedding'">
          <a-form-item label="新郎姓名">
            <a-input v-model:value="formData.GroomName" placeholder="请输入新郎姓名" :maxlength="50" />
          </a-form-item>
          <a-form-item label="新娘姓名">
            <a-input v-model:value="formData.BrideName" placeholder="请输入新娘姓名" :maxlength="50" />
          </a-form-item>
        </template>
        <a-form-item label="活动日期" name="DateRange">
          <a-range-picker v-model:value="formData.DateRange" style="width: 100%" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="地点" name="Location">
              <a-input v-model:value="formData.Location" placeholder="请输入活动地点" :maxlength="200" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="负责人" name="Manager">
              <a-input v-model:value="formData.Manager" placeholder="请输入负责人" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="预算" name="Budget">
          <a-input-number
            v-model:value="formData.Budget"
            placeholder="请输入预算金额"
            style="width: 100%"
            :min="0"
            :precision="2"
            :formatter="(value: any) => `¥ ${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')"
            :parser="(value: any) => value.replace(/¥\s?|(,*)/g, '')"
          />
        </a-form-item>
        <a-form-item label="描述" name="Description">
          <a-textarea v-model:value="formData.Description" placeholder="请输入活动描述" :rows="3" :maxlength="1000" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="modalVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import {
  PlusOutlined,
  EnvironmentOutlined,
  CalendarOutlined,
  UserOutlined,
  CarOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getEvents, createEvent } from '@/api/conference'
import type { EventListItemDto, CreateEventRequest } from '@/api/conference'

interface EventCardItem extends EventListItemDto {
  scheduleCount?: number
  vehicleCount?: number
}

const router = useRouter()

// 状态筛选
const statusSegments = [
  { label: '全部', value: '' },
  { label: '筹备中', value: 'Preparing' },
  { label: '进行中', value: 'InProgress' },
  { label: '已结束', value: 'Finished' },
]

const statusBadgeMap: Record<string, 'processing' | 'success' | 'default' | 'error'> = {
  Preparing: 'processing',
  InProgress: 'success',
  Finished: 'default',
  Cancelled: 'error',
}

const statusTextMap: Record<string, string> = {
  Preparing: '筹备中',
  InProgress: '进行中',
  Finished: '已结束',
  Cancelled: '已取消',
}

// 数据
const loading = ref(false)
const eventList = ref<EventCardItem[]>([])
const keyword = ref('')
const statusFilter = ref<string>('')

const filteredList = computed(() => eventList.value)

// 加载列表
const loadEvents = async () => {
  loading.value = true
  try {
    const params: any = { pageIndex: 1, pageSize: 100 }
    if (keyword.value) params.keyword = keyword.value
    if (statusFilter.value) params.status = statusFilter.value
    const res = await getEvents(params) as any
    eventList.value = res?.items ?? res ?? []
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  loadEvents()
}

function formatDate(date?: string) {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

function handleCardClick(item: EventListItemDto) {
  router.push(`/conference/events/${item.id}`)
}

// 新建弹窗
const modalVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)

const formData = ref<{
  Name: string
  Type: string
  GroomName: string
  BrideName: string
  DateRange: [Dayjs, Dayjs] | undefined
  Location: string
  Manager: string
  Budget: number | undefined
  Description: string
}>({
  Name: '',
  Type: 'conference',
  GroomName: '',
  BrideName: '',
  DateRange: undefined,
  Location: '',
  Manager: '',
  Budget: undefined,
  Description: '',
})

const formRules: Record<string, Rule[]> = {
  Name: [{ required: true, message: '请输入活动名称', trigger: 'blur' }],
  DateRange: [{ required: true, message: '请选择活动日期', trigger: 'change' }],
}

function handleAdd() {
  formData.value = {
    Name: '',
    Type: 'conference',
    GroomName: '',
    BrideName: '',
    DateRange: undefined,
    Location: '',
    Manager: '',
    Budget: undefined,
    Description: '',
  }
  modalVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    const data: CreateEventRequest = {
      name: formData.value.Name,
      type: formData.value.Type,
      groomName: formData.value.Type === 'wedding' ? (formData.value.GroomName || undefined) : undefined,
      brideName: formData.value.Type === 'wedding' ? (formData.value.BrideName || undefined) : undefined,
      startDate: formData.value.DateRange![0].format('YYYY-MM-DD'),
      endDate: formData.value.DateRange![1].format('YYYY-MM-DD'),
      location: formData.value.Location || undefined,
      manager: formData.value.Manager || undefined,
      budget: formData.value.Budget ?? 0,
      description: formData.value.Description || undefined,
    }
    await createEvent(data)
    message.success('创建成功')
    modalVisible.value = false
    loadEvents()
  } finally {
    submitLoading.value = false
  }
}

onMounted(() => {
  loadEvents()
})
</script>

<style scoped lang="scss">
.toolbar-search {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
}

.loading-wrap {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 300px;
}

.event-card {
  transition: transform 0.2s, box-shadow 0.2s;
  cursor: pointer;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
  }
}

.card-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.card-title-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
}

.card-body {
  min-height: 60px;
}

.card-info-row {
  display: flex;
  align-items: center;
  gap: 6px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 13px;
  margin-bottom: 4px;
}

.card-icon {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.35);
}

.wedding-info {
  margin-top: 6px;
  font-size: 13px;
  color: #eb2f96;
}
</style>
