<template>
  <div class="page-container">
    <PageHeader title="检测规则">
      <template #actions>
        <a-input-search v-model:value="searchForm.businessLine" placeholder="业务线" style="width: 180px" @search="handleSearch" allowClear />
        <a-select v-model:value="searchForm.status" placeholder="状态" allow-clear style="width: 100px" :options="statusOptions" @change="handleSearch" />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增规则
        </a-button>
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
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'name'">
            <a-tooltip :title="record.name">
              <span>{{ record.name }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'dispatchMethod'">
            <a-tag :color="dispatchMethodColor(record.dispatchMethod)">
              {{ dispatchMethodText(record.dispatchMethod) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'defaultPriority'">
            <a-tag :color="priorityColor(record.defaultPriority)">
              {{ priorityText(record.defaultPriority) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'timeoutHours'">
            {{ record.timeoutHours }}小时
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-switch
              :checked="record.status === 0"
              checked-children="启用"
              un-checked-children="禁用"
              @change="handleToggle(record)"
            />
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime || '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText>
          <a-empty description="暂无检测规则" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增规则' : '编辑规则'"
      width="720px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '90px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <div class="section-title">基本信息</div>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="规则名称" name="name">
              <a-input v-model:value="formData.name" placeholder="请输入规则名称" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="业务线" name="businessLine">
              <a-input v-model:value="formData.businessLine" placeholder="请输入业务线" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="派发方式" name="dispatchMethod">
              <a-select v-model:value="formData.dispatchMethod" placeholder="请选择派发方式">
                <a-select-option :value="0">手动</a-select-option>
                <a-select-option :value="1">自动</a-select-option>
                <a-select-option :value="2">工作流</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="派发目标">
              <a-input v-model:value="formData.dispatchTarget" placeholder="请输入派发目标" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="默认优先级" name="defaultPriority">
              <a-select v-model:value="formData.defaultPriority" placeholder="请选择优先级">
                <a-select-option :value="0">低</a-select-option>
                <a-select-option :value="1">中</a-select-option>
                <a-select-option :value="2">高</a-select-option>
                <a-select-option :value="3">紧急</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="超时时长" name="timeoutHours">
              <a-input-number
                v-model:value="formData.timeoutHours"
                placeholder="请输入超时小时数"
                :min="1"
                :max="9999"
                style="width: 100%"
                addon-after="小时"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="描述" :label-col="{ style: { width: '90px' } }">
              <a-textarea v-model:value="formData.description" placeholder="请输入描述" :rows="3" :maxlength="500" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 条件配置 -->
        <div class="section-title">
          触发条件
          <a-button type="link" @click="handleAddCondition"><PlusOutlined />添加条件</a-button>
        </div>
        <div v-for="(cond, idx) in formData.conditions" :key="idx" class="condition-item">
          <a-row :gutter="8" align="middle">
            <a-col :span="4">
              <a-select
                v-if="idx > 0"
                v-model:value="cond.logicRelation"
                size="small"
                style="width: 100%"
              >
                <a-select-option value="AND">AND</a-select-option>
                <a-select-option value="OR">OR</a-select-option>
              </a-select>
              <span v-else class="condition-label">条件</span>
            </a-col>
            <a-col :span="6">
              <a-input v-model:value="cond.fieldName" size="small" placeholder="字段名" />
            </a-col>
            <a-col :span="4">
              <a-select v-model:value="cond.operator" size="small" style="width: 100%">
                <a-select-option value="=">=</a-select-option>
                <a-select-option value=">">></a-select-option>
                <a-select-option value="<">&lt;</a-select-option>
                <a-select-option value=">=">&gt;=</a-select-option>
                <a-select-option value="<=">&lt;=</a-select-option>
                <a-select-option value="!=">!=</a-select-option>
                <a-select-option value="contains">contains</a-select-option>
              </a-select>
            </a-col>
            <a-col :span="7">
              <a-input v-model:value="cond.threshold" size="small" placeholder="阈值" />
            </a-col>
            <a-col :span="3" style="text-align: center">
              <a-button type="link" size="small" danger @click="formData.conditions.splice(idx, 1)">
                <DeleteOutlined />
              </a-button>
            </a-col>
          </a-row>
        </div>
        <a-empty v-if="formData.conditions.length === 0" description="暂无条件，点击上方按钮添加" :image="simpleImage" />
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
import { message, Modal } from 'ant-design-vue'
import { Empty } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getQualityRules,
  getQualityRuleDetail,
  createQualityRule,
  updateQualityRule,
  deleteQualityRule,
  toggleQualityRule,
} from '@/api/quality'

const simpleImage = Empty.PRESENTED_IMAGE_SIMPLE

// ====== 枚举映射 ======
const statusOptions = [
  { label: '启用', value: 0 },
  { label: '禁用', value: 1 },
]

function dispatchMethodText(v: number) { return ['手动', '自动', '工作流'][v] || '未知' }
function dispatchMethodColor(v: number) { return ['default', 'blue', 'green'][v] || 'default' }
function priorityText(v: number) { return ['低', '中', '高', '紧急'][v] || '未知' }
function priorityColor(v: number) { return ['default', 'blue', 'orange', 'red'][v] || 'default' }

// ====== 表格列 ======
const tableColumns = [
  { title: '规则名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: '业务线', dataIndex: 'businessLine', key: 'businessLine', width: 120 },
  { title: '派发方式', dataIndex: 'dispatchMethod', key: 'dispatchMethod', width: 100, align: 'center' as const },
  { title: '默认优先级', dataIndex: 'defaultPriority', key: 'defaultPriority', width: 100, align: 'center' as const },
  { title: '超时时长', dataIndex: 'timeoutHours', key: 'timeoutHours', width: 100, align: 'center' as const },
  { title: '条件数', dataIndex: 'conditionCount', key: 'conditionCount', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 170 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

// ====== 搜索 ======
const searchForm = reactive({
  businessLine: '',
  status: undefined as number | undefined,
})

// ====== 表格数据 ======
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

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
  fetchList()
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.businessLine) params.businessLine = searchForm.businessLine
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getQualityRules(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }

function handleReset() {
  searchForm.businessLine = ''
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchList()
}

// ====== 启用/禁用 ======
async function handleToggle(record: any) {
  try {
    await toggleQualityRule(record.id)
    message.success(record.status === 0 ? '已禁用' : '已启用')
    fetchList()
  } catch (e) {
    console.error('切换状态失败:', e)
  }
}

// ====== 弹窗 ======
interface ConditionRow {
  id?: number
  fieldName: string
  operator: string
  threshold: string
  logicRelation: string
  sort: number
}

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  name: '',
  businessLine: '',
  dispatchMethod: undefined as number | undefined,
  dispatchTarget: '',
  defaultPriority: undefined as number | undefined,
  timeoutHours: undefined as number | undefined,
  description: '',
  conditions: [] as ConditionRow[],
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  businessLine: [{ required: true, message: '请输入业务线', trigger: 'blur' }],
  dispatchMethod: [{ required: true, message: '请选择派发方式', trigger: 'change' }],
  defaultPriority: [{ required: true, message: '请选择默认优先级', trigger: 'change' }],
  timeoutHours: [{ required: true, message: '请输入超时时长', trigger: 'blur' }],
}

function resetForm() {
  formData.name = ''
  formData.businessLine = ''
  formData.dispatchMethod = undefined
  formData.dispatchTarget = ''
  formData.defaultPriority = undefined
  formData.timeoutHours = undefined
  formData.description = ''
  formData.conditions = []
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getQualityRuleDetail(row.id) as any
    if (detail) {
      formData.name = detail.name || ''
      formData.businessLine = detail.businessLine || ''
      formData.dispatchMethod = detail.dispatchMethod
      formData.dispatchTarget = detail.dispatchTarget || ''
      formData.defaultPriority = detail.defaultPriority
      formData.timeoutHours = detail.timeoutHours
      formData.description = detail.description || ''
      formData.conditions = (detail.conditions || []).map((c: any) => ({
        id: c.id,
        fieldName: c.fieldName || '',
        operator: c.operator || '=',
        threshold: c.threshold || '',
        logicRelation: c.logicRelation || 'AND',
        sort: c.sort || 0,
      }))
    }
  } catch (e) {
    console.error('获取规则详情失败:', e)
  }
}

function handleAddCondition() {
  formData.conditions.push({
    fieldName: '',
    operator: '=',
    threshold: '',
    logicRelation: 'AND',
    sort: formData.conditions.length + 1,
  })
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload: any = {
      name: formData.name,
      businessLine: formData.businessLine,
      dispatchMethod: formData.dispatchMethod,
      dispatchTarget: formData.dispatchTarget || undefined,
      defaultPriority: formData.defaultPriority,
      timeoutHours: formData.timeoutHours,
      description: formData.description || undefined,
      conditions: formData.conditions.map((c, idx) => ({
        id: c.id,
        fieldName: c.fieldName,
        operator: c.operator,
        threshold: c.threshold,
        logicRelation: c.logicRelation,
        sort: idx + 1,
      })),
    }
    if (dialogType.value === 'add') {
      await createQualityRule(payload)
      message.success('新增成功')
    } else {
      await updateQualityRule(currentId.value!, payload)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

// ====== 删除 ======
function handleDelete(record: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除规则「${record.name}」吗？`,
    okText: '确定',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      try {
        await deleteQualityRule(record.id)
        message.success('删除成功')
        fetchList()
      } catch (e) {
        console.error('删除失败:', e)
      }
    },
  })
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.condition-item {
  margin-bottom: 8px;
  padding: 8px;
  background: $bg-page;
  border-radius: $border-radius-sm;
}

.condition-label {
  font-size: 13px;
  color: $text-secondary;
  padding-left: 8px;
}
</style>
