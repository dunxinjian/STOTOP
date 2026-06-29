<template>
  <div class="page-container">
    <PageHeader title="承包区管理">
      <template #actions>
        <a-select v-model:value="filters.status" placeholder="状态" allow-clear style="width: 120px" @change="handleFilterChange">
          <a-select-option :value="1">启用</a-select-option>
          <a-select-option :value="0">停用</a-select-option>
        </a-select>
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
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'contractStartDate'">
            {{ record.contractStartDate?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'contractEndDate'">
            {{ record.contractEndDate?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'contractFee'">
            {{ record.contractFee != null ? '¥' + record.contractFee.toFixed(2) : '' }}
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as FranchiseAreaDto)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record as FranchiseAreaDto)">删除</a-button>
            <a-button type="link" size="small" @click="handleQuotation(record)">报价</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal v-model:open="dialogVisible" :title="editingId ? '编辑承包区' : '新增承包区'" width="750px" :destroy-on-close="true" @cancel="dialogVisible = false">
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
            <a-form-item label="承包人" name="contractor">
              <a-input v-model:value="form.contractor" placeholder="请输入承包人" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="承包开始日期" name="contractStartDate">
              <a-date-picker v-model:value="form.contractStartDate" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="承包结束日期" name="contractEndDate">
              <a-date-picker v-model:value="form.contractEndDate" style="width: 100%" value-format="YYYY-MM-DD" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="覆盖片区" name="coverageDistrict">
              <a-input v-model:value="form.coverageDistrict" placeholder="请输入覆盖片区" :maxlength="200" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="承包费" name="contractFee">
              <a-input-number v-model:value="form.contractFee" :min="0" :precision="2" style="width: 100%" placeholder="请输入承包费" />
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
      :client-type="'CB'"
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
  getFranchiseAreaList,
  getFranchiseAreaDetail,
  createFranchiseArea,
  updateFranchiseArea,
  deleteFranchiseArea,
  checkFranchiseAreaCodeExists,
  type FranchiseAreaDto,
  type CreateFranchiseAreaRequest,
  type UpdateFranchiseAreaRequest,
} from '@/api/express'

// 报价抽屉
const quotationDrawerVisible = ref(false)
const currentQuotationClientId = ref('')
const currentQuotationClientName = ref('')

function handleQuotation(record: any) {
  currentQuotationClientId.value = record.code
  currentQuotationClientName.value = record.contractor || ''
  quotationDrawerVisible.value = true
}

const columns = [
  { title: '编号', dataIndex: 'code', width: 130, customRender: ({ text }: any) => text || '-' },
  { title: '所属组织', dataIndex: 'orgId', width: 120 },
  { title: '承包人', dataIndex: 'contractor', width: 100 },
  { title: '覆盖片区', dataIndex: 'coverageDistrict', width: 150 },
  { title: '承包费', dataIndex: 'contractFee', width: 110, align: 'right' as const },
  { title: '承包开始', dataIndex: 'contractStartDate', width: 110 },
  { title: '承包结束', dataIndex: 'contractEndDate', width: 110 },
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
const dataSource = ref<FranchiseAreaDto[]>([])
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
    const res = await getFranchiseAreaList({
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

// ===== 新增/编辑弹窗 =====
const dialogVisible = ref(false)
const editingId = ref<number | undefined>(undefined)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  code: '',
  orgId: undefined as number | undefined,
  contractor: '',
  contractStartDate: undefined as string | undefined,
  contractEndDate: undefined as string | undefined,
  coverageDistrict: '',
  contractFee: undefined as number | undefined,
  contactPhone: '',
  address: '',
  status: 1,
  remark: '',
})

const statusChecked = computed({
  get: () => form.status === 1,
  set: (val: boolean) => { form.status = val ? 1 : 0 },
})

const formRules = computed<Record<string, Rule[]>>(() => ({
  code: editingId.value ? [] : [{ required: true, message: '请输入编号', trigger: 'blur' }],
  orgId: [{ required: true, message: '请选择所属组织', trigger: 'change' }],
}))

function resetForm() {
  form.code = ''
  form.orgId = undefined
  form.contractor = ''
  form.contractStartDate = undefined
  form.contractEndDate = undefined
  form.coverageDistrict = ''
  form.contractFee = undefined
  form.contactPhone = ''
  form.address = ''
  form.status = 1
  form.remark = ''
}

function handleAdd() {
  editingId.value = undefined
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(record: FranchiseAreaDto) {
  editingId.value = record.id
  try {
    const d = await getFranchiseAreaDetail(record.id)
    Object.assign(form, {
      code: d.code || '',
      orgId: d.orgId || undefined,
      contractor: d.contractor || '',
      contractStartDate: d.contractStartDate?.slice(0, 10) || undefined,
      contractEndDate: d.contractEndDate?.slice(0, 10) || undefined,
      coverageDistrict: d.coverageDistrict || '',
      contractFee: d.contractFee,
      contactPhone: d.contactPhone || '',
      address: d.address || '',
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
      const updateData: UpdateFranchiseAreaRequest = {
        orgId: form.orgId!,
        contractor: form.contractor || undefined,
        contractStartDate: form.contractStartDate || undefined,
        contractEndDate: form.contractEndDate || undefined,
        coverageDistrict: form.coverageDistrict || undefined,
        contractFee: form.contractFee,
        contactPhone: form.contactPhone || undefined,
        address: form.address || undefined,
        status: form.status,
        remark: form.remark || undefined,
      }
      await updateFranchiseArea(editingId.value, updateData)
      message.success('更新成功')
    } else {
      // 新建时做编号唯一性校验
      try {
        const exists = await checkFranchiseAreaCodeExists(form.code)
        if (exists) {
          message.error(`承包区编号 '${form.code}' 已存在，请换一个编号`)
          submitting.value = false
          return
        }
      } catch { /* 校验失败时继续提交，由后端校验 */ }
      const createData: CreateFranchiseAreaRequest = {
        code: form.code,
        orgId: form.orgId!,
        contractor: form.contractor || undefined,
        contractStartDate: form.contractStartDate || undefined,
        contractEndDate: form.contractEndDate || undefined,
        coverageDistrict: form.coverageDistrict || undefined,
        contractFee: form.contractFee,
        contactPhone: form.contactPhone || undefined,
        address: form.address || undefined,
        status: form.status,
        remark: form.remark || undefined,
      }
      await createFranchiseArea(createData)
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
function handleDelete(record: FranchiseAreaDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除该承包区吗？`,
    okType: 'danger',
    async onOk() {
      await deleteFranchiseArea(record.id)
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
