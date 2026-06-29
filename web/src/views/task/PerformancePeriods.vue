<template>
  <div class="performance-periods">
    <div class="page-header">
      <h3>考核周期管理</h3>
      <a-button type="primary" @click="openModal()">
        <template #icon><plus-outlined /></template>
        新建考核周期
      </a-button>
    </div>

    <a-card :bordered="false">
      <div class="filter-bar">
        <a-space>
          <a-select v-model:value="query.type" placeholder="周期类型" allow-clear style="width: 140px" @change="() => fetchData()">
            <a-select-option :value="0">月度</a-select-option>
            <a-select-option :value="1">季度</a-select-option>
            <a-select-option :value="2">年度</a-select-option>
          </a-select>
          <a-select v-model:value="query.status" placeholder="状态" allow-clear style="width: 120px" @change="() => fetchData()">
            <a-select-option :value="0">草稿</a-select-option>
            <a-select-option :value="1">进行中</a-select-option>
            <a-select-option :value="2">已结束</a-select-option>
          </a-select>
          <a-input-search v-model:value="query.keyword" placeholder="搜索周期名称" style="width: 200px" @search="() => fetchData()" />
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'type'">
            <a-tag :color="typeColorMap[record.type]">{{ typeTextMap[record.type] }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'dateRange'">
            {{ record.startDate?.substring(0, 10) }} ~ {{ record.endDate?.substring(0, 10) }}
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-badge :status="statusBadgeMap[record.status]" :text="statusTextMap[record.status]" />
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-space>
              <a v-if="record.status === 0" @click="openModal(record as PerformancePeriodListDto)">编辑</a>
              <a v-if="record.status === 0" @click="startPeriod(record as PerformancePeriodListDto)">启动考核</a>
              <a v-if="record.status === 1" @click="calculatePeriod(record as PerformancePeriodListDto)">计算绩效</a>
              <a v-if="record.status === 1" @click="endPeriod(record as PerformancePeriodListDto)">结束考核</a>
              <router-link v-if="record.status !== 0" :to="`/task/performance/evaluation/${record.id}`">
                查看记录
              </router-link>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="editingRecord ? '编辑考核周期' : '新建考核周期'"
      :confirm-loading="submitting"
      @ok="handleSubmit"
    >
      <a-form ref="formRef" :model="formState" :rules="formRules" layout="vertical">
        <a-form-item label="周期名称" name="name">
          <a-input v-model:value="formState.name" placeholder="例：2026年Q1季度考核" :maxlength="100" />
        </a-form-item>
        <a-form-item label="周期类型" name="type">
          <a-select v-model:value="formState.type" placeholder="请选择">
            <a-select-option :value="0">月度</a-select-option>
            <a-select-option :value="1">季度</a-select-option>
            <a-select-option :value="2">年度</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="考核起止日期" name="dateRange">
          <a-range-picker v-model:value="formState.dateRange" style="width: 100%" value-format="YYYY-MM-DD" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { FormInstance, TablePaginationConfig } from 'ant-design-vue'
import type {
  PerformancePeriodListDto,
  PerformancePeriodPagedRequest,
  CreatePerformancePeriodRequest,
  UpdatePerformancePeriodRequest,
} from '@/types/task'
import {
  getPerformancePeriods,
  createPerformancePeriod,
  updatePerformancePeriod,
  calculatePerformance,
} from '@/api/task'

type BadgeStatus = 'default' | 'error' | 'warning' | 'success' | 'processing'
const typeTextMap: Record<number, string> = { 0: '月度', 1: '季度', 2: '年度' }
const typeColorMap: Record<number, string> = { 0: 'blue', 1: 'green', 2: 'orange' }
const statusTextMap: Record<number, string> = { 0: '草稿', 1: '进行中', 2: '已结束' }
const statusBadgeMap: Record<number, BadgeStatus> = { 0: 'default', 1: 'processing', 2: 'success' }

const columns = [
  { title: '周期名称', dataIndex: 'name', ellipsis: true },
  { title: '类型', dataIndex: 'type', width: 100 },
  { title: '考核期间', dataIndex: 'dateRange', width: 220 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '考核人数', dataIndex: 'recordCount', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', width: 170, customRender: ({ text }: any) => text?.substring(0, 16)?.replace('T', ' ') },
  { title: '操作', dataIndex: 'action', width: 240 },
]

const loading = ref(false)
const dataList = ref<PerformancePeriodListDto[]>([])
const query = reactive<PerformancePeriodPagedRequest>({ pageIndex: 1, pageSize: 15, type: undefined, status: undefined, keyword: undefined })
const pagination = reactive({ current: 1, pageSize: 15, total: 0, showSizeChanger: true, showTotal: (t: number) => `共 ${t} 条` })

async function fetchData() {
  loading.value = true
  try {
    const res = await getPerformancePeriods(query)
    const d = (res as any)?.data ?? res
    dataList.value = d.items ?? []
    pagination.total = d.total ?? 0
  } finally {
    loading.value = false
  }
}

function onTableChange(pag: TablePaginationConfig) {
  query.pageIndex = pag.current
  query.pageSize = pag.pageSize
  pagination.current = pag.current!
  pagination.pageSize = pag.pageSize!
  fetchData()
}

// ===== Modal =====
const modalVisible = ref(false)
const submitting = ref(false)
const editingRecord = ref<PerformancePeriodListDto | null>(null)
const formRef = ref<FormInstance>()
const formState = reactive({ name: '', type: 0 as number, dateRange: undefined as [string, string] | undefined })
const formRules = {
  name: [{ required: true, message: '请输入周期名称' }],
  type: [{ required: true, message: '请选择类型' }],
  dateRange: [{ required: true, message: '请选择起止日期' }],
}

function openModal(record?: PerformancePeriodListDto) {
  editingRecord.value = record ?? null
  if (record) {
    formState.name = record.name
    formState.type = record.type
    formState.dateRange = [record.startDate.substring(0, 10), record.endDate.substring(0, 10)]
  } else {
    formState.name = ''
    formState.type = 0
    formState.dateRange = undefined
  }
  modalVisible.value = true
}

async function handleSubmit() {
  await formRef.value?.validateFields()
  const dateRange = formState.dateRange
  if (!dateRange) return
  submitting.value = true
  try {
    if (editingRecord.value) {
      const payload: UpdatePerformancePeriodRequest = {
        name: formState.name,
        type: formState.type,
        startDate: dateRange[0],
        endDate: dateRange[1],
        status: editingRecord.value.status,
      }
      await updatePerformancePeriod(editingRecord.value.id, payload)
      message.success('更新成功')
    } else {
      const payload: CreatePerformancePeriodRequest = {
        name: formState.name,
        type: formState.type,
        startDate: dateRange[0],
        endDate: dateRange[1],
      }
      await createPerformancePeriod(payload)
      message.success('创建成功')
    }
    modalVisible.value = false
    fetchData()
  } finally {
    submitting.value = false
  }
}

function startPeriod(record: PerformancePeriodListDto) {
  Modal.confirm({
    title: '启动考核',
    content: `确认启动「${record.name}」考核周期？启动后将生成考核记录。`,
    async onOk() {
      await updatePerformancePeriod(record.id, { name: record.name, type: record.type, startDate: record.startDate, endDate: record.endDate, status: 1 })
      message.success('已启动')
      fetchData()
    },
  })
}

function endPeriod(record: PerformancePeriodListDto) {
  Modal.confirm({
    title: '结束考核',
    content: `确认结束「${record.name}」考核周期？`,
    async onOk() {
      await updatePerformancePeriod(record.id, { name: record.name, type: record.type, startDate: record.startDate, endDate: record.endDate, status: 2 })
      message.success('已结束')
      fetchData()
    },
  })
}

async function calculatePeriod(record: PerformancePeriodListDto) {
  Modal.confirm({
    title: '计算绩效',
    content: `确认为「${record.name}」触发绩效自动计算？`,
    async onOk() {
      await calculatePerformance(record.id)
      message.success('计算完成')
      fetchData()
    },
  })
}

onMounted(fetchData)
</script>

<style scoped lang="scss">
.performance-periods {
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
    h3 { margin: 0; }
  }
  .filter-bar { margin-bottom: 16px; }
}
</style>
