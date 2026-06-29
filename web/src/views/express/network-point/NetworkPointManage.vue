<template>
  <div class="page-container">
    <PageHeader title="快递网点管理">
      <template #actions>
        <a-select v-model:value="filters.status" placeholder="状态" allow-clear
          style="width: 100px" @change="handleFilterChange">
          <a-select-option :value="1">启用</a-select-option>
          <a-select-option :value="0">停用</a-select-option>
        </a-select>
        <a-button @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
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
        :scroll="{ x: 1520 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'pointLevel'">
            {{ pointLevelMap[record.pointLevel] ?? record.pointLevel }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'storageArea'">
            {{ record.storageArea != null ? record.storageArea + ' ㎡' : '' }}
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as NetworkPointDto)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record as NetworkPointDto)">删除</a-button>
            <a-button type="link" size="small" @click="handleQuotation(record)">报价</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal v-model:open="dialogVisible" :title="editingId ? '编辑快递网点' : '新增快递网点'" width="750px" :destroy-on-close="true" @cancel="dialogVisible = false">
      <a-form ref="formRef" :model="form" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-row :gutter="16">
          <a-col :span="12">
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
          <a-col :span="12">
            <a-form-item label="编号" name="code">
              <a-input v-model:value="form.code" :placeholder="editingId ? '' : '请输入编号'" :maxlength="50" :disabled="!!editingId" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="网点简称" name="shortName">
              <a-input v-model:value="form.shortName" placeholder="请输入网点简称" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="网点级别" name="pointLevel">
              <a-select v-model:value="form.pointLevel" placeholder="请选择网点级别">
                <a-select-option :value="1">一级网点</a-select-option>
                <a-select-option :value="2">二级网点</a-select-option>
                <a-select-option :value="3">三级网点</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="上级网点编号" name="parentPointCode">
              <a-input v-model:value="form.parentPointCode" placeholder="请输入上级网点编号" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="一级网点" name="isPrimaryPoint">
              <a-switch v-model:checked="isPrimaryPointChecked" checked-children="是" un-checked-children="否" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="覆盖区域" name="coverageArea">
              <a-input v-model:value="form.coverageArea" placeholder="请输入覆盖区域" :maxlength="200" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="日处理能力" name="dailyCapacity">
              <a-input-number v-model:value="form.dailyCapacity" :min="0" style="width: 100%" placeholder="件/天" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="仓储面积" name="storageArea">
              <a-input-number v-model:value="form.storageArea" :min="0" :precision="2" style="width: 100%" placeholder="平方米" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="营业时间" name="businessHours">
              <a-input v-model:value="form.businessHours" placeholder="如 08:00-20:00" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="负责人" name="manager">
              <a-input v-model:value="form.manager" placeholder="请输入负责人" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系电话" name="contactPhone">
              <a-input v-model:value="form.contactPhone" placeholder="请输入联系电话" :maxlength="30" />
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
      :client-type="'WD'"
      :client-name="currentQuotationClientName"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import QuotationDrawer from '@/views/express/components/QuotationDrawer.vue'
import { getOrganizationTree, type OrgTreeNode } from '@/api/system'
import {
  getNetworkPointList,
  getNetworkPointDetail,
  createNetworkPoint,
  updateNetworkPoint,
  deleteNetworkPoint,
  checkNetworkPointCodeExists,
  type NetworkPointDto,
  type CreateNetworkPointRequest,
  type UpdateNetworkPointRequest,
} from '@/api/express'

const pointLevelMap: Record<number, string> = { 1: '一级网点', 2: '二级网点', 3: '三级网点' }

// 报价抽屉
const quotationDrawerVisible = ref(false)
const currentQuotationClientId = ref('')
const currentQuotationClientName = ref('')

function handleQuotation(record: any) {
  currentQuotationClientId.value = record.code
  currentQuotationClientName.value = record.manager || record.coverageArea || ''
  quotationDrawerVisible.value = true
}

const columns = [
  { title: '编号', dataIndex: 'code', width: 130, customRender: ({ text }: any) => text || '-' },
  { title: '网点简称', dataIndex: 'shortName', width: 120 },
  { title: '所属组织', dataIndex: 'orgId', width: 120 },
  { title: '网点级别', dataIndex: 'pointLevel', width: 100, align: 'center' as const },
  { title: '上级网点编号', dataIndex: 'parentPointCode', width: 130, customRender: ({ text }: any) => text || '-' },
  { title: '一级网点', dataIndex: 'isPrimaryPoint', width: 90, align: 'center' as const, customRender: ({ text }: any) => text === 1 ? '是' : '否' },
  { title: '覆盖区域', dataIndex: 'coverageArea', width: 150 },
  { title: '日处理能力', dataIndex: 'dailyCapacity', width: 110, align: 'right' as const },
  { title: '仓储面积', dataIndex: 'storageArea', width: 110, align: 'right' as const },
  { title: '营业时间', dataIndex: 'businessHours', width: 130 },
  { title: '负责人', dataIndex: 'manager', width: 100 },
  { title: '联系电话', dataIndex: 'contactPhone', width: 130 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', width: 170 },
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
const dataSource = ref<NetworkPointDto[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const filters = reactive({ status: undefined as number | undefined })

async function fetchList() {
  loading.value = true
  try {
    const res = await getNetworkPointList({
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      status: filters.status,
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
  filters.status = undefined
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
  shortName: '',
  parentPointCode: '',
  orgId: undefined as number | undefined,
  pointLevel: 1 as number,
  isPrimaryPoint: 1 as number,
  coverageArea: '',
  dailyCapacity: undefined as number | undefined,
  storageArea: undefined as number | undefined,
  businessHours: '',
  address: '',
  manager: '',
  contactPhone: '',
  status: 1,
  remark: '',
})

const statusChecked = computed({
  get: () => form.status === 1,
  set: (val: boolean) => { form.status = val ? 1 : 0 },
})

const isPrimaryPointChecked = computed({
  get: () => form.isPrimaryPoint === 1,
  set: (val: boolean) => { form.isPrimaryPoint = val ? 1 : 0 },
})

const formRules = computed<Record<string, Rule[]>>(() => ({
  code: editingId.value ? [] : [{ required: true, message: '请输入编号', trigger: 'blur' }],
  orgId: [{ required: true, message: '请选择所属组织', trigger: 'change' }],
  pointLevel: [{ required: true, message: '请选择网点级别', trigger: 'change' }],
}))

function resetForm() {
  form.code = ''
  form.shortName = ''
  form.parentPointCode = ''
  form.orgId = undefined
  form.pointLevel = 1
  form.isPrimaryPoint = 1
  form.coverageArea = ''
  form.dailyCapacity = undefined
  form.storageArea = undefined
  form.businessHours = ''
  form.address = ''
  form.manager = ''
  form.contactPhone = ''
  form.status = 1
  form.remark = ''
}

function handleAdd() {
  editingId.value = undefined
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(record: NetworkPointDto) {
  editingId.value = record.id
  try {
    const d = await getNetworkPointDetail(record.id)
    Object.assign(form, {
      code: d.code || '',
      shortName: d.shortName || '',
      parentPointCode: d.parentPointCode || '',
      orgId: d.orgId || undefined,
      pointLevel: d.pointLevel,
      isPrimaryPoint: d.isPrimaryPoint ?? 1,
      coverageArea: d.coverageArea || '',
      dailyCapacity: d.dailyCapacity,
      storageArea: d.storageArea,
      businessHours: d.businessHours || '',
      address: d.address || '',
      manager: d.manager || '',
      contactPhone: d.contactPhone || '',
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
    if (editingId.value) {
      const updateData: UpdateNetworkPointRequest = {
        orgId: form.orgId!,
        shortName: form.shortName || undefined,
        parentPointCode: form.parentPointCode || undefined,
        pointLevel: form.pointLevel,
        isPrimaryPoint: form.isPrimaryPoint,
        coverageArea: form.coverageArea || undefined,
        dailyCapacity: form.dailyCapacity,
        storageArea: form.storageArea,
        businessHours: form.businessHours || undefined,
        address: form.address || undefined,
        manager: form.manager || undefined,
        contactPhone: form.contactPhone || undefined,
        status: form.status,
        remark: form.remark || undefined,
      }
      await updateNetworkPoint(editingId.value, updateData)
      message.success('更新成功')
    } else {
      // 新建时做编号唯一性校验
      try {
        const exists = await checkNetworkPointCodeExists(form.code)
        if (exists) {
          message.error(`网点编号 '${form.code}' 已存在，请换一个编号`)
          submitting.value = false
          return
        }
      } catch { /* 校验失败时继续提交，由后端校验 */ }
      const createData: CreateNetworkPointRequest = {
        code: form.code,
        shortName: form.shortName || undefined,
        parentPointCode: form.parentPointCode || undefined,
        orgId: form.orgId!,
        pointLevel: form.pointLevel,
        isPrimaryPoint: form.isPrimaryPoint,
        coverageArea: form.coverageArea || undefined,
        dailyCapacity: form.dailyCapacity,
        storageArea: form.storageArea,
        businessHours: form.businessHours || undefined,
        address: form.address || undefined,
        manager: form.manager || undefined,
        contactPhone: form.contactPhone || undefined,
        status: form.status,
        remark: form.remark || undefined,
      }
      await createNetworkPoint(createData)
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
function handleDelete(record: NetworkPointDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除该快递网点吗？`,
    okType: 'danger',
    async onOk() {
      await deleteNetworkPoint(record.id)
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
