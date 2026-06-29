<template>
  <div class="page-container">
    <PageHeader title="业务代理管理">
      <template #actions>
        <a-select v-model:value="filters.agentLevel" placeholder="代理级别" allow-clear style="width: 130px" @change="handleFilterChange">
          <a-select-option :value="1">一级代理</a-select-option>
          <a-select-option :value="2">二级代理</a-select-option>
        </a-select>
        <a-select v-model:value="filters.status" placeholder="状态" allow-clear style="width: 100px" @change="handleFilterChange">
          <a-select-option :value="1">启用</a-select-option>
          <a-select-option :value="0">停用</a-select-option>
        </a-select>
        <a-input-search v-model:value="filters.keyword" placeholder="搜索编码/名称" style="width: 200px" allow-clear @search="handleFilterChange" @pressEnter="handleFilterChange" />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="dataSource"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无业务代理数据" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'agentLevel'">
            {{ agentLevelMap[record.agentLevel] ?? record.agentLevel }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'cooperationStartDate'">
            {{ record.cooperationStartDate?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as AgentDto)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm title="确定删除该业务代理吗？" ok-text="确定" cancel-text="取消" @confirm="handleDelete(record as AgentDto)">
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
            <a-button type="link" size="small" @click="handleQuotation(record)">
              <DollarOutlined />报价
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal v-model:open="dialogVisible" :title="editingId ? '编辑业务代理' : '新增业务代理'" width="700px" :destroy-on-close="true" @cancel="dialogVisible = false">
      <a-form ref="formRef" :model="form" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="编码" name="code">
              <a-input v-model:value="form.code" placeholder="请输入编码" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="名称" name="name">
              <a-input v-model:value="form.name" placeholder="请输入名称" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="代理级别" name="agentLevel">
              <a-select v-model:value="form.agentLevel" placeholder="请选择代理级别">
                <a-select-option :value="1">一级代理</a-select-option>
                <a-select-option :value="2">二级代理</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="代理区域" name="agentRegion">
              <a-input v-model:value="form.agentRegion" placeholder="请输入代理区域" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系人" name="contactName">
              <a-input v-model:value="form.contactName" placeholder="请输入联系人" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系电话" name="contactPhone">
              <a-input v-model:value="form.contactPhone" placeholder="请输入联系电话" :maxlength="30" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="合作开始日期" name="cooperationStartDate">
              <a-date-picker v-model:value="form.cooperationStartDate" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="状态" name="status">
              <a-switch v-model:checked="statusChecked" checked-children="启用" un-checked-children="停用" />
            </a-form-item>
          </a-col>
          <a-col :span="24">
            <a-form-item label="地址" name="address">
              <a-input v-model:value="form.address" placeholder="请输入地址" :maxlength="200" />
            </a-form-item>
          </a-col>
          <a-col :span="24">
            <a-form-item label="备注" name="remark">
              <a-textarea v-model:value="form.remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitting" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <QuotationDrawer
      v-model:open="quotationDrawerVisible"
      :client-id="currentQuotationClientId"
      :client-type="'DL'"
      :client-name="currentQuotationClientName"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, DollarOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import QuotationDrawer from '@/views/express/components/QuotationDrawer.vue'
import {
  getAgentList,
  getAgentDetail,
  createAgent,
  updateAgent,
  deleteAgent,
  type AgentDto,
  type CreateAgentRequest,
} from '@/api/express'

const agentLevelMap: Record<number, string> = { 1: '一级代理', 2: '二级代理' }

// 报价抽屉
const quotationDrawerVisible = ref(false)
const currentQuotationClientId = ref('')
const currentQuotationClientName = ref('')

function handleQuotation(record: any) {
  currentQuotationClientId.value = record.code
  currentQuotationClientName.value = record.name
  quotationDrawerVisible.value = true
}

const columns = [
  { title: '编码', dataIndex: 'code', width: 120 },
  { title: '名称', dataIndex: 'name', width: 150, ellipsis: true },
  { title: '代理级别', dataIndex: 'agentLevel', width: 100, align: 'center' as const },
  { title: '代理区域', dataIndex: 'agentRegion', width: 120, ellipsis: true },
  { title: '联系人', dataIndex: 'contactName', width: 100 },
  { title: '联系电话', dataIndex: 'contactPhone', width: 130 },
  { title: '合作日期', dataIndex: 'cooperationStartDate', width: 110 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', width: 170 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const, align: 'center' as const },
]

// ===== 列表查询 =====
const loading = ref(false)
const dataSource = ref<AgentDto[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const filters = reactive({
  agentLevel: undefined as number | undefined,
  status: undefined as number | undefined,
  keyword: '',
})

async function fetchList() {
  loading.value = true
  try {
    const res = await getAgentList({
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      status: filters.status,
      agentLevel: filters.agentLevel,
    })
    dataSource.value = res.items
    pagination.total = (res as any).totalCount ?? res.total
  } catch {
    message.error('查询失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleFilterChange() {
  pagination.current = 1
  fetchList()
}

function handleReset() {
  filters.agentLevel = undefined
  filters.status = undefined
  filters.keyword = ''
  pagination.current = 1
  fetchList()
}

// ===== 新增/编辑弹窗 =====
const dialogVisible = ref(false)
const editingId = ref<number | undefined>(undefined)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  code: '',
  name: '',
  agentLevel: 1 as number,
  agentRegion: '',
  contactName: '',
  contactPhone: '',
  address: '',
  cooperationStartDate: undefined as string | undefined,
  status: 1,
  remark: '',
})

const statusChecked = computed({
  get: () => form.status === 1,
  set: (val: boolean) => { form.status = val ? 1 : 0 },
})

const formRules: Record<string, Rule[]> = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  agentLevel: [{ required: true, message: '请选择代理级别', trigger: 'change' }],
}

function resetForm() {
  form.code = ''
  form.name = ''
  form.agentLevel = 1
  form.agentRegion = ''
  form.contactName = ''
  form.contactPhone = ''
  form.address = ''
  form.cooperationStartDate = undefined
  form.status = 1
  form.remark = ''
}

function handleAdd() {
  editingId.value = undefined
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(record: AgentDto) {
  editingId.value = record.id
  try {
    const d = await getAgentDetail(record.id)
    Object.assign(form, {
      code: d.code || '',
      name: d.name || '',
      agentLevel: d.agentLevel,
      agentRegion: d.agentRegion || '',
      contactName: d.contactName || '',
      contactPhone: d.contactPhone || '',
      address: d.address || '',
      cooperationStartDate: d.cooperationStartDate?.slice(0, 10) || undefined,
      status: d.status,
      remark: d.remark || '',
    })
    dialogVisible.value = true
  } catch {
    message.error('获取详情失败')
  }
}

async function handleSubmit() {
  try { await formRef.value?.validate() } catch { return }
  submitting.value = true
  try {
    const data: CreateAgentRequest = {
      code: form.code,
      name: form.name,
      agentLevel: form.agentLevel,
      agentRegion: form.agentRegion || undefined,
      contactName: form.contactName || undefined,
      contactPhone: form.contactPhone || undefined,
      address: form.address || undefined,
      cooperationStartDate: form.cooperationStartDate || undefined,
      status: form.status,
      remark: form.remark || undefined,
    }
    if (editingId.value) {
      await updateAgent(editingId.value, data)
      message.success('更新成功')
    } else {
      await createAgent(data)
      message.success('创建成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch {
    message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

// ===== 删除 =====
async function handleDelete(record: AgentDto) {
  try {
    await deleteAgent(record.id)
    message.success('删除成功')
    fetchList()
  } catch {
    message.error('删除失败')
  }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
