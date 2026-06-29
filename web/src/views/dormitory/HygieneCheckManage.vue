<template>
  <div class="page-container">
    <PageHeader title="卫生检查" description="管理宿舍卫生检查记录">
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增检查记录
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-form-item label="房间" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.roomId"
                placeholder="全部房间"
                allow-clear
                show-search
                :filter-option="filterOption"
                style="width: 200px"
                :options="roomOptions"
              />
            </a-form-item>
            <a-form-item label="检查结果" style="margin-bottom:0">
              <a-select
                v-model:value="searchForm.result"
                placeholder="全部"
                allow-clear
                style="width: 120px"
                :options="resultOptions"
              />
            </a-form-item>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-button type="primary" @click="handleSearch">
              <template #icon><SearchOutlined /></template>查询
            </a-button>
            <a-button @click="handleReset">
              <template #icon><ReloadOutlined /></template>重置
            </a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1100 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'roomInfo'">
            <span>{{ record.buildingName }} - {{ record.roomNumber }}</span>
          </template>
          <template v-if="column.dataIndex === 'score'">
            <div class="score-cell">
              <a-progress
                :percent="record.score"
                :stroke-color="getScoreColor(record.score)"
                size="small"
                :show-info="false"
                style="width: 80px"
              />
              <span class="score-text">{{ record.score }}分</span>
            </div>
          </template>
          <template v-if="column.dataIndex === 'result'">
            <a-tag :color="getResultColor(record.result)">
              {{ record.result }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该检查记录吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无卫生检查数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑检查记录弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogMode === 'add' ? '新增卫生检查记录' : '编辑卫生检查记录'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="房间" name="roomId">
          <a-select
            v-model:value="formData.roomId"
            placeholder="请选择房间"
            show-search
            :filter-option="filterOption"
            :options="roomOptions"
            :disabled="dialogMode === 'edit'"
          />
        </a-form-item>
        <a-form-item label="检查人ID" name="inspectorId">
          <a-input-number
            v-model:value="formData.inspectorId"
            placeholder="请输入检查人ID"
            style="width: 100%"
            :min="1"
          />
        </a-form-item>
        <a-form-item label="检查日期" name="checkDate">
          <a-date-picker
            v-model:value="formData.checkDateValue"
            format="YYYY-MM-DD"
            placeholder="请选择检查日期"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="评分" name="score">
          <div class="score-input">
            <a-slider
              v-model:value="formData.score"
              :min="0"
              :max="100"
              :style="{ width: '300px' }"
            />
            <a-input-number
              v-model:value="formData.score"
              :min="0"
              :max="100"
              style="width: 80px; margin-left: 16px"
            />
          </div>
        </a-form-item>
        <a-form-item label="检查结果" name="result">
          <a-select
            v-model:value="formData.result"
            placeholder="请选择检查结果"
            :options="resultOptions"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea
            v-model:value="formData.remark"
            :rows="3"
            placeholder="请输入备注或存在的问题"
            :maxlength="500"
            show-count
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getHygieneCheckList,
  createHygieneCheck,
  updateHygieneCheck,
  deleteHygieneCheck,
  getAllBuildings,
  getRoomList,
  type HygieneCheckDto,
  type CreateHygieneCheckRequest,
  type UpdateHygieneCheckRequest,
} from '@/api/dormitory'

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '房间信息', dataIndex: 'roomInfo', key: 'roomInfo', width: 180 },
  { title: '检查人', dataIndex: 'inspectorName', key: 'inspectorName', width: 100 },
  { title: '检查日期', dataIndex: 'checkDate', key: 'checkDate', width: 120 },
  { title: '评分', dataIndex: 'score', key: 'score', width: 180 },
  { title: '检查结果', dataIndex: 'result', key: 'result', width: 100, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 200, ellipsis: true },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

// 检查结果选项
const resultOptions = [
  { label: '优秀', value: '优秀' },
  { label: '良好', value: '良好' },
  { label: '合格', value: '合格' },
  { label: '不合格', value: '不合格' },
]

// 房间选项
const roomOptions = ref<{ label: string; value: number }[]>([])

// 搜索表单
const searchForm = reactive({
  roomId: undefined as number | undefined,
  result: undefined as string | undefined,
})

// 表格数据
const loading = ref(false)
const tableData = ref<HygieneCheckDto[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchHygieneCheckList()
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentCheckId = ref<number | null>(null)

const formData = reactive({
  roomId: undefined as number | undefined,
  inspectorId: undefined as number | undefined,
  checkDateValue: dayjs() as any,
  score: 80,
  result: undefined as string | undefined,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  roomId: [{ required: true, message: '请选择房间', trigger: 'change' }],
  inspectorId: [{ required: true, message: '请输入检查人ID', trigger: 'blur' }],
  checkDate: [{ required: true, message: '请选择检查日期', trigger: 'change' }],
  score: [{ required: true, message: '请输入评分', trigger: 'blur' }],
  result: [{ required: true, message: '请选择检查结果', trigger: 'change' }],
}

// 获取分数颜色
function getScoreColor(score: number): string {
  if (score >= 90) return 'var(--color-success)'
  if (score >= 80) return 'var(--color-info)'
  if (score >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

// 获取检查结果颜色
function getResultColor(result: string): string {
  const colors: Record<string, string> = {
    '优秀': 'green',
    '良好': 'blue',
    '合格': 'default',
    '不合格': 'red',
  }
  return colors[result] || 'default'
}

// 筛选选项
function filterOption(input: string, option: any) {
  return option.label.toLowerCase().indexOf(input.toLowerCase()) >= 0
}

// 加载房间选项
async function loadRoomOptions() {
  try {
    const buildings = await getAllBuildings()
    const options: { label: string; value: number }[] = []
    for (const building of buildings) {
      const rooms = await getRoomList(building.id)
      for (const room of rooms) {
        options.push({
          label: `${building.name} - ${room.roomNumber}`,
          value: room.id,
        })
      }
    }
    roomOptions.value = options
  } catch (error) {
    console.error('加载房间选项失败:', error)
  }
}

// 获取卫生检查列表
async function fetchHygieneCheckList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.roomId) params.roomId = searchForm.roomId
    if (searchForm.result) params.result = searchForm.result
    const res = await getHygieneCheckList(params)
    if (res) {
      tableData.value = res.items || []
      pagination.total = res.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  pagination.pageIndex = 1
  fetchHygieneCheckList()
}

// 重置搜索
function handleReset() {
  searchForm.roomId = undefined
  searchForm.result = undefined
  pagination.pageIndex = 1
  fetchHygieneCheckList()
}

// 重置表单
function resetForm() {
  formData.roomId = undefined
  formData.inspectorId = undefined
  formData.checkDateValue = dayjs()
  formData.score = 80
  formData.result = undefined
  formData.remark = ''
  currentCheckId.value = null
}

// 新增
function handleAdd() {
  dialogMode.value = 'add'
  resetForm()
  dialogVisible.value = true
}

// 编辑
function handleEdit(record: any) {
  dialogMode.value = 'edit'
  currentCheckId.value = record.id
  formData.roomId = record.roomId
  formData.inspectorId = record.inspectorId
  formData.checkDateValue = record.checkDate ? dayjs(record.checkDate) : dayjs()
  formData.score = record.score ?? 80
  formData.result = record.result
  formData.remark = record.remark || ''
  dialogVisible.value = true
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const checkDate = formData.checkDateValue
      ? formData.checkDateValue.format('YYYY-MM-DD')
      : dayjs().format('YYYY-MM-DD')

    if (dialogMode.value === 'add') {
      const data: CreateHygieneCheckRequest = {
        roomId: formData.roomId!,
        inspectorId: formData.inspectorId!,
        checkDate,
        score: formData.score,
        result: formData.result!,
        remark: formData.remark || undefined,
      }
      await createHygieneCheck(data)
      message.success('新增成功')
    } else {
      const data: UpdateHygieneCheckRequest = {
        inspectorId: formData.inspectorId!,
        checkDate,
        score: formData.score,
        result: formData.result!,
        remark: formData.remark || undefined,
      }
      await updateHygieneCheck(currentCheckId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchHygieneCheckList()
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(record: any) {
  try {
    await deleteHygieneCheck(record.id)
    message.success('删除成功')
    fetchHygieneCheckList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

onMounted(() => {
  loadRoomOptions()
  fetchHygieneCheckList()
})
</script>

<style scoped lang="scss">
.score-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.score-text {
  font-weight: 500;
  min-width: 40px;
}

.score-input {
  display: flex;
  align-items: center;
}
</style>
