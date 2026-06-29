<template>
  <div class="page-container">
    <PageHeader title="末端驿站管理">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex;align-items:center;justify-content:space-between;width:100%;">
          <div style="display:flex;align-items:center;gap:8px;"></div>
          <div style="display:flex;align-items:center;gap:8px;">
            <a-select v-model:value="filters.stationType" size="small" placeholder="驿站类型" allow-clear style="width:120px" @change="handleFilterChange">
              <a-select-option :value="1">直营</a-select-option>
              <a-select-option :value="2">合作</a-select-option>
            </a-select>
            <a-select v-model:value="filters.status" size="small" placeholder="状态" allow-clear style="width:120px" @change="handleFilterChange">
              <a-select-option :value="1">启用</a-select-option>
              <a-select-option :value="0">停用</a-select-option>
            </a-select>
          </div>
        </div>
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
        :scroll="{ x: 1600 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'stationType'">
            <a-tag :color="record.stationType === 1 ? 'blue' : 'orange'">
              {{ stationTypeMap[record.stationType] ?? record.stationType }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'area'">
            {{ record.area != null ? record.area + ' ㎡' : '' }}
          </template>
          <template v-if="column.dataIndex === 'cooperationStartDate'">
            {{ record.cooperationStartDate?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as LastMileStationDto)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record as LastMileStationDto)">删除</a-button>
            <a-button type="link" size="small" @click="handleQuotation(record)">报价</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal v-model:open="dialogVisible" :title="editingId ? '编辑末端驿站' : '新增末端驿站'" width="800px" :destroy-on-close="true" @cancel="dialogVisible = false">
      <a-form ref="formRef" :model="form" :rules="formRules" :label-col="{ style: { width: '110px' } }" style="padding: 10px 20px">
        <a-divider orientation="left" style="margin-top: 0">基本信息</a-divider>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="驿站类型" name="stationType">
              <a-radio-group v-model:value="form.stationType" @change="handleStationTypeChange">
                <a-radio :value="1">直营</a-radio>
                <a-radio :value="2">合作</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>

          <!-- 直营模式：选择组织 -->
          <a-col v-if="form.stationType === 1" :span="12">
            <a-form-item label="所属组织" name="orgId">
              <a-tree-select
                v-model:value="form.orgId"
                :tree-data="orgTree"
                placeholder="请选择组织"
                allow-clear
                show-search
                tree-node-filter-prop="title"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>

          <!-- 合作模式：手填编码和名称 -->
          <a-col v-if="form.stationType === 2" :span="12">
            <a-form-item label="编码" name="code">
              <a-input v-model:value="form.code" placeholder="请输入编码" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col v-if="form.stationType === 2" :span="12">
            <a-form-item label="名称" name="name">
              <a-input v-model:value="form.name" placeholder="请输入名称" :maxlength="100" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">运营信息</a-divider>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="运营时间" name="businessHours">
              <a-input v-model:value="form.businessHours" placeholder="如 08:00-20:00" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="日处理量" name="dailyVolume">
              <a-input-number v-model:value="form.dailyVolume" :min="0" style="width: 100%" placeholder="件/天" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="货架数" name="shelfCount">
              <a-input-number v-model:value="form.shelfCount" :min="0" style="width: 100%" placeholder="请输入货架数" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="面积(㎡)" name="area">
              <a-input-number v-model:value="form.area" :min="0" :precision="2" style="width: 100%" placeholder="平方米" />
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
      :client-type="'YZ'"
      :client-name="currentQuotationClientName"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import QuotationDrawer from '@/views/express/components/QuotationDrawer.vue'
import { getOrganizationTree, type OrgTreeNode } from '@/api/system'
import {
  getLastMileStationList,
  getLastMileStationDetail,
  createLastMileStation,
  updateLastMileStation,
  deleteLastMileStation,
  type LastMileStationDto,
  type CreateLastMileStationRequest,
} from '@/api/express'

const stationTypeMap: Record<number, string> = { 1: '直营', 2: '合作' }

// 报价抽屉
const quotationDrawerVisible = ref(false)
const currentQuotationClientId = ref('')
const currentQuotationClientName = ref('')

function handleQuotation(record: any) {
  if (!record.serviceObjectId) {
    message.warning('该驿站未关联业务对象，无法查看报价')
    return
  }
  currentQuotationClientId.value = String(record.serviceObjectId)
  currentQuotationClientName.value = record.name || record.code || ''
  quotationDrawerVisible.value = true
}

const columns = [
  { title: '类型', dataIndex: 'stationType', width: 80, align: 'center' as const },
  { title: '编码', dataIndex: 'code', width: 120 },
  { title: '名称', dataIndex: 'name', width: 150 },
  { title: '地址', dataIndex: 'address', width: 200 },
  { title: '运营时间', dataIndex: 'businessHours', width: 130 },
  { title: '日处理量', dataIndex: 'dailyVolume', width: 100, align: 'right' as const },
  { title: '货架数', dataIndex: 'shelfCount', width: 80, align: 'right' as const },
  { title: '面积', dataIndex: 'area', width: 100, align: 'right' as const },
  { title: '联系人', dataIndex: 'contactName', width: 100 },
  { title: '联系电话', dataIndex: 'contactPhone', width: 130 },
  { title: '合作日期', dataIndex: 'cooperationStartDate', width: 110 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const, align: 'center' as const },
]

// ===== 组织树 =====
interface TreeSelectNode { value: number; title: string; children?: TreeSelectNode[] }
const orgTree = ref<TreeSelectNode[]>([])

function transformOrgTree(nodes: OrgTreeNode[]): TreeSelectNode[] {
  if (!nodes) return []
  return nodes.map(n => ({
    value: n.id,
    title: n.name,
    children: n.children ? transformOrgTree(n.children) : undefined,
  }))
}

async function loadOrgTree() {
  try {
    const res = await getOrganizationTree() as any
    const nodes = Array.isArray(res) ? res : (res?.items || [])
    orgTree.value = transformOrgTree(nodes)
  } catch { /* ignore */ }
}

// ===== 列表查询 =====
const loading = ref(false)
const dataSource = ref<LastMileStationDto[]>([])
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
  stationType: undefined as number | undefined,
  status: undefined as number | undefined,
})

async function fetchList() {
  loading.value = true
  try {
    const res = await getLastMileStationList({
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      status: filters.status,
      stationType: filters.stationType,
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

// ===== 新增/编辑弹窗 =====
const dialogVisible = ref(false)
const editingId = ref<string | undefined>(undefined)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  stationType: 1 as number,
  orgId: undefined as number | undefined,
  code: '',
  name: '',
  address: '',
  businessHours: '',
  dailyVolume: undefined as number | undefined,
  shelfCount: undefined as number | undefined,
  area: undefined as number | undefined,
  contactName: '',
  contactPhone: '',
  cooperationStartDate: undefined as string | undefined,
  status: 1,
  remark: '',
})

const statusChecked = computed({
  get: () => form.status === 1,
  set: (val: boolean) => { form.status = val ? 1 : 0 },
})

const formRules = computed<Record<string, Rule[]>>(() => {
  const rules: Record<string, Rule[]> = {
    stationType: [{ required: true, message: '请选择驿站类型', trigger: 'change' }],
  }
  if (form.stationType === 1) {
    rules.orgId = [{ required: true, message: '请选择所属组织', trigger: 'change' }]
  } else {
    rules.code = [{ required: true, message: '请输入编码', trigger: 'blur' }]
    rules.name = [{ required: true, message: '请输入名称', trigger: 'blur' }]
  }
  return rules
})

function handleStationTypeChange() {
  // 切换类型时清除对方模式的字段
  if (form.stationType === 1) {
    form.code = ''
    form.name = ''
  } else {
    form.orgId = undefined
  }
  // 清除校验
  formRef.value?.clearValidate()
}

function resetForm() {
  form.stationType = 1
  form.orgId = undefined
  form.code = ''
  form.name = ''
  form.address = ''
  form.businessHours = ''
  form.dailyVolume = undefined
  form.shelfCount = undefined
  form.area = undefined
  form.contactName = ''
  form.contactPhone = ''
  form.cooperationStartDate = undefined
  form.status = 1
  form.remark = ''
}

function handleAdd() {
  editingId.value = undefined
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(record: LastMileStationDto) {
  editingId.value = record.id
  try {
    const d = await getLastMileStationDetail(record.id)
    Object.assign(form, {
      stationType: d.stationType,
      orgId: d.orgId || undefined,
      code: d.code || '',
      name: d.name || '',
      address: d.address || '',
      businessHours: d.businessHours || '',
      dailyVolume: d.dailyVolume,
      shelfCount: d.shelfCount,
      area: d.area,
      contactName: d.contactName || '',
      contactPhone: d.contactPhone || '',
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
    const data: CreateLastMileStationRequest = {
      stationType: form.stationType,
      orgId: form.stationType === 1 ? form.orgId : undefined,
      code: form.stationType === 2 ? (form.code || undefined) : undefined,
      name: form.stationType === 2 ? (form.name || undefined) : undefined,
      address: form.address || undefined,
      businessHours: form.businessHours || undefined,
      dailyVolume: form.dailyVolume,
      shelfCount: form.shelfCount,
      area: form.area,
      contactName: form.contactName || undefined,
      contactPhone: form.contactPhone || undefined,
      cooperationStartDate: form.cooperationStartDate || undefined,
      status: form.status,
      remark: form.remark || undefined,
    }
    if (editingId.value) {
      await updateLastMileStation(editingId.value, data)
      message.success('更新成功')
    } else {
      await createLastMileStation(data)
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
function handleDelete(record: LastMileStationDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除末端驿站"${record.name || record.code}"吗？`,
    okType: 'danger',
    async onOk() {
      await deleteLastMileStation(record.id)
      message.success('删除成功')
      fetchList()
    },
  })
}

onMounted(() => {
  loadOrgTree()
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
