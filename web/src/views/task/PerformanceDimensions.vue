<template>
  <div class="performance-dimensions">
    <div class="page-header">
      <h3>考核维度配置</h3>
      <a-button type="primary" @click="openModal()">
        <template #icon><plus-outlined /></template>
        新增维度
      </a-button>
    </div>

    <a-card :bordered="false">
      <!-- 权重总和提示 -->
      <a-alert
        v-if="weightSum !== 100 && dataList.length > 0"
        :message="`当前权重总和为 ${weightSum}%，建议调整为 100%`"
        type="warning"
        show-icon
        style="margin-bottom: 16px"
      />

      <a-table :columns="columns" :data-source="dataList" :loading="loading" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'weight'">
            <a-progress :percent="record.weight" :size="[120, 8]" :stroke-color="record.weight > 0 ? 'var(--color-info)' : '#d9d9d9'" />
            <span style="margin-left: 4px">{{ record.weight }}%</span>
          </template>
          <template v-else-if="column.dataIndex === 'dataSource'">
            <a-tag>{{ dataSourceMap[record.dataSource] ?? '未知' }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'isEnabled'">
            <a-badge :status="record.isEnabled ? 'success' : 'default'" :text="record.isEnabled ? '启用' : '禁用'" />
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-space>
              <a @click="openModal(record as PerformanceDimensionListDto)">编辑</a>
              <a-popconfirm title="确认删除此维度？" @confirm="handleDelete(record.id)">
                <a style="color: var(--color-danger)">删除</a>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>

      <div v-if="dataList.length > 0" class="weight-summary">
        权重合计：
        <span :class="{ 'weight-ok': weightSum === 100, 'weight-bad': weightSum !== 100 }">{{ weightSum }}%</span>
      </div>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="editingRecord ? '编辑维度' : '新增维度'"
      :confirm-loading="submitting"
      @ok="handleSubmit"
    >
      <a-form ref="formRef" :model="formState" :rules="formRules" layout="vertical">
        <a-form-item label="维度名称" name="dimensionName">
          <a-input v-model:value="formState.dimensionName" placeholder="例：工作质量" :maxlength="50" />
        </a-form-item>
        <a-form-item label="维度编码" name="dimensionCode">
          <a-input v-model:value="formState.dimensionCode" placeholder="例：quality" :maxlength="50" />
        </a-form-item>
        <a-form-item label="数据来源" name="dataSource">
          <a-select v-model:value="formState.dataSource" placeholder="请选择">
            <a-select-option :value="0">系统自动</a-select-option>
            <a-select-option :value="1">人工评分</a-select-option>
            <a-select-option :value="2">混合</a-select-option>
          </a-select>
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="权重（%）" name="weight">
              <a-input-number v-model:value="formState.weight" :min="0" :max="100" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="满分" name="maxScore">
              <a-input-number v-model:value="formState.maxScore" :min="1" :max="1000" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="排序" name="sort">
          <a-input-number v-model:value="formState.sort" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="editingRecord" label="状态" name="isEnabled">
          <a-switch v-model:checked="formState.isEnabled" checked-children="启用" un-checked-children="禁用" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { FormInstance } from 'ant-design-vue'
import type {
  PerformanceDimensionListDto,
  CreatePerformanceDimensionRequest,
  UpdatePerformanceDimensionRequest,
} from '@/types/task'
import {
  getPerformanceDimensions,
  createPerformanceDimension,
  updatePerformanceDimension,
  deletePerformanceDimension,
} from '@/api/task'

const dataSourceMap: Record<number, string> = { 0: '系统自动', 1: '人工评分', 2: '混合' }

const columns = [
  { title: '排序', dataIndex: 'sort', width: 70 },
  { title: '维度名称', dataIndex: 'dimensionName' },
  { title: '编码', dataIndex: 'dimensionCode', width: 120 },
  { title: '数据来源', dataIndex: 'dataSource', width: 110 },
  { title: '权重', dataIndex: 'weight', width: 200 },
  { title: '满分', dataIndex: 'maxScore', width: 80 },
  { title: '状态', dataIndex: 'isEnabled', width: 90 },
  { title: '操作', dataIndex: 'action', width: 120 },
]

const loading = ref(false)
const dataList = ref<PerformanceDimensionListDto[]>([])
const weightSum = computed(() => dataList.value.reduce((s, d) => s + (d.weight ?? 0), 0))

async function fetchData() {
  loading.value = true
  try {
    const res = await getPerformanceDimensions()
    const d = (res as any)?.data ?? res
    dataList.value = Array.isArray(d) ? d : (d.items ?? [])
  } finally {
    loading.value = false
  }
}

// ===== Modal =====
const modalVisible = ref(false)
const submitting = ref(false)
const editingRecord = ref<PerformanceDimensionListDto | null>(null)
const formRef = ref<FormInstance>()
const formState = reactive({
  dimensionName: '',
  dimensionCode: '',
  dataSource: 1 as number,
  weight: 0,
  maxScore: 100,
  sort: 0,
  isEnabled: true,
})
const formRules = {
  dimensionName: [{ required: true, message: '请输入维度名称' }],
  dimensionCode: [{ required: true, message: '请输入维度编码' }],
  dataSource: [{ required: true, message: '请选择数据来源' }],
}

function openModal(record?: PerformanceDimensionListDto) {
  editingRecord.value = record ?? null
  if (record) {
    Object.assign(formState, {
      dimensionName: record.dimensionName,
      dimensionCode: record.dimensionCode,
      dataSource: record.dataSource,
      weight: record.weight,
      maxScore: record.maxScore,
      sort: record.sort,
      isEnabled: record.isEnabled,
    })
  } else {
    Object.assign(formState, { dimensionName: '', dimensionCode: '', dataSource: 1, weight: 0, maxScore: 100, sort: 0, isEnabled: true })
  }
  modalVisible.value = true
}

async function handleSubmit() {
  await formRef.value?.validateFields()
  submitting.value = true
  try {
    if (editingRecord.value) {
      const payload: UpdatePerformanceDimensionRequest = { ...formState }
      await updatePerformanceDimension(editingRecord.value.id, payload)
      message.success('更新成功')
    } else {
      const payload: CreatePerformanceDimensionRequest = {
        dimensionName: formState.dimensionName,
        dimensionCode: formState.dimensionCode,
        dataSource: formState.dataSource,
        weight: formState.weight,
        maxScore: formState.maxScore,
        sort: formState.sort,
      }
      await createPerformanceDimension(payload)
      message.success('创建成功')
    }
    modalVisible.value = false
    fetchData()
  } finally {
    submitting.value = false
  }
}

async function handleDelete(id: number) {
  await deletePerformanceDimension(id)
  message.success('已删除')
  fetchData()
}

onMounted(fetchData)
</script>

<style scoped lang="scss">
.performance-dimensions {
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
    h3 { margin: 0; }
  }
  .weight-summary {
    margin-top: 12px;
    text-align: right;
    font-size: 14px;
    .weight-ok { color: var(--color-success); font-weight: 600; }
    .weight-bad { color: var(--color-danger); font-weight: 600; }
  }
}
</style>
