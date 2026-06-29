<template>
  <div class="page-container">
    <PageHeader title="账套管理">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <PlusOutlined />新增账套
        </a-button>
      </template>
    </PageHeader>

    <!-- 账套列表 -->
    <a-card :bordered="false">
      <a-table :columns="columns" :dataSource="tableData" :loading="loading" rowKey="id" :pagination="false" bordered>
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'fName'">
            <a class="link-text" @click="handleEdit(record)">{{ record.fName }}</a>
          </template>
          <template v-if="column.dataIndex === 'fIsDefault'">
            <a-tag v-if="record.fIsDefault" color="warning">默认</a-tag>
            <span v-else>-</span>
          </template>
          <template v-if="column.dataIndex === 'fStatus'">
            <a-tag :color="record.fStatus === 1 ? 'success' : 'default'">
              {{ record.fStatus === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'startPeriod'">
            <span v-if="record.fStartYear && record.fStartMonth">
              {{ record.fStartYear }}-{{ String(record.fStartMonth).padStart(2, '0') }}
            </span>
            <span v-else>-</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-button type="link" size="small" @click="handleSetDefault(record)" :disabled="record.fIsDefault" class="btn-warning">设为默认</a-button>
            <a-button type="link" size="small" @click="handleInitialize(record)" class="btn-success">初始化</a-button>
            <a-button type="link" size="small" @click="handleMigration(record)"><SwapOutlined />凭证迁移</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 模板预览弹窗 -->
    <a-modal
      v-model:open="previewVisible"
      title="模板科目预览"
      :width="750"
      :destroyOnClose="true"
      :centered="true"
      :footer="null"
    >
      <a-table
        :columns="previewColumns"
        :dataSource="previewTreeData"
        :loading="previewLoading"
        rowKey="code"
        :pagination="false"
        :expandable="{ childrenColumnName: 'children', defaultExpandAllRows: true }"
        bordered
        size="small"
        :scroll="{ y: 450 }"
      />
    </a-modal>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑账套' : '新增账套'"
      :width="650"
      :destroyOnClose="true"
      :centered="true"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :labelCol="{ style: { width: '90px' } }">
        <a-form-item label="账套编码" name="fCode">
          <a-input v-model:value="formData.fCode" placeholder="请输入账套编码" :disabled="isEdit" />
        </a-form-item>
        <a-form-item label="账套名称" name="fName">
          <a-input v-model:value="formData.fName" placeholder="请输入账套名称" />
        </a-form-item>
        <a-form-item label="法人名称" name="fCompanyName">
          <a-input v-model:value="formData.fCompanyName" placeholder="请输入法人名称" />
        </a-form-item>
        <a-form-item label="起始账期" name="startPeriod">
          <a-date-picker
            v-model:value="formData.startPeriod"
            picker="month"
            placeholder="请选择起始年月"
            format="YYYY-MM"
            valueFormat="YYYY-MM"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="formData.fDescription" :rows="3" placeholder="请输入描述" />
        </a-form-item>
        <a-form-item label="关联组织">
          <a-select
            v-model:value="formData.fOrgId"
            placeholder="请选择关联组织"
            :loading="orgsLoading"
            :options="orgOptions"
            allowClear
            showSearch
            :filterOption="(input: string, option: any) => option.label.toLowerCase().includes(input.toLowerCase())"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="formData.fSortOrder" :min="0" :max="9999" />
        </a-form-item>
        <a-form-item label="状态">
          <a-switch v-model:checked="formData.fStatusBool" checkedChildren="启用" unCheckedChildren="停用" />
        </a-form-item>
        <a-form-item label="默认账套">
          <a-switch v-model:checked="formData.fIsDefault" />
        </a-form-item>

        <!-- 科目模板选择（仅新增时显示） -->
        <template v-if="!isEdit">
          <a-divider orientation="left">科目模板</a-divider>
          <a-form-item label="科目模板">
            <div style="display: flex; gap: 8px; align-items: center; width: 100%">
              <a-select
                v-model:value="formData.templateId"
                placeholder="不使用模板"
                style="flex: 1"
                allowClear
                :loading="templatesLoading"
                :options="accountTemplateOptions"
              />
              <a-button
                v-if="formData.templateId"
                type="link"
                size="small"
                @click="handlePreviewTemplate"
              >
                <EyeOutlined /> 预览
              </a-button>
            </div>
          </a-form-item>
        </template>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取 消</a-button>
        <a-button type="primary" @click="handleSubmit" :loading="submitting">
          {{ isEdit ? '确 定' : '创建并初始化' }}
        </a-button>
      </template>
    </a-modal>

    <!-- 外部凭证迁移向导 -->
    <MigrationWizard
      v-if="migrationVisible"
      :visible="migrationVisible"
      :account-set-id="migrationAccountSetId"
      @close="migrationVisible = false"
      @success="migrationVisible = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EyeOutlined, SwapOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getAccountSets,
  createAccountSet,
  updateAccountSet,
  deleteAccountSet,
  initializeAccountSet,
} from '@/api/finance'
import { getAccountTemplates, getAccountTemplateItems } from '@/api/finance'
import type { AccountSetDto } from '@/api/finance'
import { getOrganizationTree } from '@/api/system'
import type { OrgTreeNode } from '@/api/system'
import { useAccountSetStore } from '@/stores/accountSet'
import MigrationWizard from './MigrationWizard.vue'

const accountSetStore = useAccountSetStore()
const loading = ref(false)
const submitting = ref(false)
const tableData = ref<AccountSetDto[]>([])
const dialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref<FormInstance>()

const columns = [
  { title: '编码', dataIndex: 'fCode', key: 'fCode', width: 120 },
  { title: '名称', dataIndex: 'fName', key: 'fName', minWidth: 180 },
  { title: '法人名称', dataIndex: 'fCompanyName', key: 'fCompanyName', minWidth: 160 },
  { title: '默认账套', dataIndex: 'fIsDefault', key: 'fIsDefault', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'fStatus', key: 'fStatus', width: 100, align: 'center' as const },
  { title: '排序', dataIndex: 'fSortOrder', key: 'fSortOrder', width: 80, align: 'center' as const },
  { title: '起始账期', dataIndex: 'startPeriod', key: 'startPeriod', width: 110, align: 'center' as const },
  { title: '描述', dataIndex: 'fDescription', key: 'fDescription', minWidth: 200, ellipsis: true },
  { title: '操作', dataIndex: 'action', key: 'action', width: 260, align: 'center' as const, fixed: 'right' as const },
]

// 科目模板列表
const accountTemplateOptions = ref<{ label: string; value: number }[]>([])
const templatesLoading = ref(false)

// 组织列表
const orgOptions = ref<{ label: string; value: number }[]>([])
const orgsLoading = ref(false)

// 模板预览
const previewVisible = ref(false)
const previewLoading = ref(false)
const previewTreeData = ref<any[]>([])
const previewColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 150 },
  { title: '名称', dataIndex: 'name', key: 'name', minWidth: 180 },
  { title: '类别', dataIndex: 'category', key: 'category', width: 100 },
  { title: '余额方向', dataIndex: 'balanceDirection', key: 'balanceDirection', width: 100 },
]

const formData = reactive({
  id: 0,
  fCode: '',
  fName: '',
  fCompanyName: '',
  fDescription: '',
  fIsDefault: false,
  fStatus: 1,
  fStatusBool: true,
  fSortOrder: 0,
  fStartYear: 0,
  fStartMonth: 0,
  startPeriod: '' as string,
  fOrgId: undefined as number | undefined,
  templateId: undefined as number | undefined,
})

const formRules: Record<string, any[]> = {
  fCode: [{ required: true, message: '请输入账套编码', trigger: 'blur' }],
  fName: [{ required: true, message: '请输入账套名称', trigger: 'blur' }],
  fCompanyName: [{ required: true, message: '请输入法人名称', trigger: 'blur' }],
  startPeriod: [{ required: true, message: '请选择起始账期', trigger: 'change' }],
}

async function loadData() {
  loading.value = true
  try {
    const res = await getAccountSets() as any
    tableData.value = res || []
  } catch (error) {
    console.error('加载账套列表失败:', error)
  } finally {
    loading.value = false
  }
}

function resetForm() {
  formData.id = 0
  formData.fCode = ''
  formData.fName = ''
  formData.fCompanyName = ''
  formData.fDescription = ''
  formData.fIsDefault = false
  formData.fStatus = 1
  formData.fStatusBool = true
  formData.fSortOrder = 0
  formData.fStartYear = 0
  formData.fStartMonth = 0
  formData.startPeriod = ''
  formData.fOrgId = undefined
  formData.templateId = undefined
}

function handleAdd() {
  isEdit.value = false
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: any) {
  isEdit.value = true
  const sp = (row.fStartYear && row.fStartMonth)
    ? `${row.fStartYear}-${String(row.fStartMonth).padStart(2, '0')}`
    : ''
  Object.assign(formData, {
    id: row.id,
    fCode: row.fCode,
    fName: row.fName,
    fCompanyName: row.fCompanyName,
    fDescription: row.fDescription || '',
    fIsDefault: row.fIsDefault,
    fStatus: row.fStatus,
    fStatusBool: row.fStatus === 1,
    fSortOrder: row.fSortOrder,
    fStartYear: row.fStartYear || 0,
    fStartMonth: row.fStartMonth || 0,
    startPeriod: sp,
    fOrgId: row.fOrgId || undefined,
  })
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  // 同步 fStatus
  formData.fStatus = formData.fStatusBool ? 1 : 0

  // 将 startPeriod (YYYY-MM) 拆分为年、月
  if (formData.startPeriod) {
    const parts = formData.startPeriod.split('-')
    formData.fStartYear = parseInt(parts[0])
    formData.fStartMonth = parseInt(parts[1])
  } else {
    formData.fStartYear = 0
    formData.fStartMonth = 0
  }

  submitting.value = true
  try {
    if (isEdit.value) {
      await updateAccountSet(formData.id, { ...formData })
      message.success('更新成功')
    } else {
      const params: any = { ...formData }
      // 新的模板选择方式：传递 templateId
      if (formData.templateId) {
        params.templateId = formData.templateId
      }
      const res = await createAccountSet(params) as any
      const extraMsg = res?.message || ''
      message.success(extraMsg || '创建并初始化成功')
    }
    dialogVisible.value = false
    loadData()
    accountSetStore.fetchAccountSets()
  } catch (error) {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}

function handleDelete(row: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除账套"${row.fName}"吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAccountSet(row.id)
        message.success('删除成功')
        loadData()
        accountSetStore.fetchAccountSets()
      } catch (error: any) {
        message.error(error?.message || '删除失败')
      }
    },
  })
}

async function handleSetDefault(row: any) {
  try {
    await updateAccountSet(row.id, { ...row, fIsDefault: true })
    message.success(`已将"${row.fName}"设为默认账套`)
    loadData()
    accountSetStore.fetchAccountSets()
  } catch (error) {
    message.error('设置失败')
  }
}

function handleInitialize(row: any) {
  Modal.confirm({
    title: '确认初始化',
    content: `将为账套"${row.fName}"初始化科目表和会计期间，是否继续？`,
    async onOk() {
      try {
        const res = await initializeAccountSet(row.id) as any
        if (res && res.accountCount !== undefined) {
          message.success(`初始化成功：复制了${res.accountCount}个科目，创建了${res.periodCount}个会计期间`)
        } else {
          message.success('初始化成功')
        }
        loadData()
      } catch (error: any) {
        const msg = error?.response?.data?.message || error?.message || ''
        if (msg.includes('已初始化')) {
          Modal.confirm({
            title: '重新初始化',
            content: `${msg}，是否强制重新初始化？（会清除原有科目、期间和余额数据）`,
            okText: '强制初始化',
            cancelText: '取消',
            okType: 'danger',
            async onOk() {
              try {
                const res2 = await initializeAccountSet(row.id, true) as any
                if (res2 && res2.accountCount !== undefined) {
                  message.success(`重新初始化成功：复制了${res2.accountCount}个科目，创建了${res2.periodCount}个会计期间`)
                } else {
                  message.success('重新初始化成功')
                }
                loadData()
              } catch (e2: any) {
                message.error(e2?.message || '重新初始化失败')
              }
            },
          })
        } else {
          message.error(msg || '初始化失败')
        }
      }
    },
  })
}

onMounted(() => {
  loadData()
  loadTemplates()
  loadOrgs()
})

async function loadTemplates() {
  templatesLoading.value = true
  try {
    const res = await getAccountTemplates() as any[]
    accountTemplateOptions.value = (res || []).map((t: any) => ({
      label: t.name + (t.accountCount ? `（${t.accountCount}个科目）` : ''),
      value: t.id,
    }))
  } catch (error) {
    console.error('加载科目模板失败:', error)
  } finally {
    templatesLoading.value = false
  }
}

async function loadOrgs() {
  orgsLoading.value = true
  try {
    const tree = await getOrganizationTree() as OrgTreeNode[]
    // 展平树形组织列表
    const flatten = (nodes: OrgTreeNode[]): { label: string; value: number }[] => {
      const result: { label: string; value: number }[] = []
      for (const node of nodes) {
        if (node.canBindAccountSet === true) {
          result.push({ label: node.name, value: node.id })
        }
        if (node.children?.length) result.push(...flatten(node.children))
      }
      return result
    }
    orgOptions.value = flatten(tree || [])
  } catch (error) {
    console.error('加载组织列表失败:', error)
  } finally {
    orgsLoading.value = false
  }
}

async function handlePreviewTemplate() {
  if (!formData.templateId) return
  previewLoading.value = true
  previewVisible.value = true
  try {
    const res = await getAccountTemplateItems(formData.templateId) as any[]
    previewTreeData.value = res || []
  } catch (error) {
    console.error('加载模板科目失败:', error)
    previewTreeData.value = []
  } finally {
    previewLoading.value = false
  }
}

// 外部凭证迁移
const migrationVisible = ref(false)
const migrationAccountSetId = ref<number>()

function handleMigration(row: any) {
  migrationAccountSetId.value = row.id
  migrationVisible.value = true
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.link-text {
  color: var(--text-1);
  cursor: pointer;
  text-decoration: none;
}

.link-text:hover {
  color: var(--color-primary);
  text-decoration: underline;
}
</style>
