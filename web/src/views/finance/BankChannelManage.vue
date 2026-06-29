<template>
  <div class="page-container">
    <PageHeader title="交易渠道配置" description="管理银行账户、支付宝等交易渠道及导入模板">
      <template #actions>
        <a-button v-if="has(FinancePermissions.BankChannelManage)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增渠道
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false" class="toolbar-section">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1000 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'type'">
            <a-tag :color="record.type === 1 ? 'blue' : 'cyan'">
              {{ record.type === 1 ? '银行' : '支付宝' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'bankName'">
            {{ record.type === 1 ? record.bankName : '-' }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="has(FinancePermissions.BankChannelManage)" type="link" size="small" @click="handleEdit(record as BankChannelDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button v-if="has(FinancePermissions.BankChannelManage)" type="link" size="small" @click="handleConfigTemplate(record as BankChannelDto)">
              <SettingOutlined />配置模板
            </a-button>
            <a-button v-if="has(FinancePermissions.BankChannelManage)" type="link" size="small" @click="handleToggleStatus(record as BankChannelDto)">
              {{ record.status === 1 ? '停用' : '启用' }}
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无渠道数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑渠道弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增渠道' : '编辑渠道'"
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
        <a-form-item label="名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入渠道名称" :maxlength="100" />
        </a-form-item>
        <a-form-item label="类型" name="type">
          <a-select v-model:value="formData.type" placeholder="请选择类型">
            <a-select-option :value="1">银行</a-select-option>
            <a-select-option :value="2">支付宝</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="账号" name="accountNo">
          <a-input v-model:value="formData.accountNo" placeholder="请输入账号" :maxlength="100" />
        </a-form-item>
        <a-form-item v-if="formData.type === 1" label="开户行" name="bankName">
          <a-input v-model:value="formData.bankName" placeholder="请输入开户行" :maxlength="200" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="formData.remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 配置导入模板弹窗 -->
    <a-modal
      v-model:open="templateDialogVisible"
      title="配置导入模板"
      width="800px"
      :destroy-on-close="true"
      @cancel="templateDialogVisible = false"
    >
      <div class="template-tip">
        <p>配置 Excel 导入时的列映射关系，将 Excel 中的列名映射到系统字段</p>
      </div>
      <a-table
        :columns="mappingColumns"
        :data-source="mappingData"
        :pagination="false"
        bordered
        size="small"
        row-key="field"
        style="margin-bottom: 16px"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'excelColumn'">
            <a-input v-model:value="record.excelColumn" placeholder="Excel列名" size="small" />
          </template>
          <template v-if="column.dataIndex === 'required'">
            <a-checkbox v-model:checked="record.required" />
          </template>
        </template>
      </a-table>

      <div class="preview-section">
        <div class="preview-title">映射预览（JSON）</div>
        <pre class="preview-json">{{ mappingPreviewJson }}</pre>
      </div>

      <template #footer>
        <a-button @click="templateDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="templateSubmitLoading" @click="handleSaveTemplate">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, SettingOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getBankChannelList,
  createBankChannel,
  updateBankChannel,
  getBankChannelById,
  type BankChannelDto,
} from '@/api/finance'
import { usePermission, FinancePermissions } from '@/utils/permission'

const { has } = usePermission()

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '类型', dataIndex: 'type', key: 'type', width: 100, align: 'center' as const },
  { title: '账号', dataIndex: 'accountNo', key: 'accountNo', width: 200 },
  { title: '开户行', dataIndex: 'bankName', key: 'bankName', width: 180 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 240, align: 'center' as const, fixed: 'right' as const },
]

// 表格数据
const loading = ref(false)
const tableData = ref<BankChannelDto[]>([])
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
  fetchList()
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentChannelId = ref<number | null>(null)

const formData = reactive({
  name: '',
  type: 1 as number,
  accountNo: '',
  bankName: '',
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入渠道名称', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }],
}

// 模板配置弹窗
const templateDialogVisible = ref(false)
const templateSubmitLoading = ref(false)
const currentTemplateChannel = ref<BankChannelDto | null>(null)

interface MappingRow {
  field: string
  label: string
  excelColumn: string
  required: boolean
}

const systemFields = [
  { field: 'transactionDate', label: '交易日期' },
  { field: 'transactionNo', label: '交易流水号' },
  { field: 'counterpartAccount', label: '对方账号' },
  { field: 'counterpartName', label: '对方户名' },
  { field: 'direction', label: '收支方向' },
  { field: 'amount', label: '金额' },
  { field: 'balance', label: '余额' },
  { field: 'summary', label: '摘要' },
  { field: 'remark', label: '备注' },
]

const mappingData = ref<MappingRow[]>([])

const mappingColumns = [
  { title: '系统字段', dataIndex: 'label', key: 'label', width: 140 },
  { title: 'Excel列名', dataIndex: 'excelColumn', key: 'excelColumn', width: 200 },
  { title: '是否必填', dataIndex: 'required', key: 'required', width: 100, align: 'center' as const },
]

const mappingPreviewJson = computed(() => {
  const obj: Record<string, { excelColumn: string; required: boolean }> = {}
  mappingData.value.forEach((row) => {
    if (row.excelColumn) {
      obj[row.field] = { excelColumn: row.excelColumn, required: row.required }
    }
  })
  return JSON.stringify(obj, null, 2)
})

// 获取列表
async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    const res = await getBankChannelList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function resetForm() {
  formData.name = ''
  formData.type = 1
  formData.accountNo = ''
  formData.bankName = ''
  formData.remark = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentChannelId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: BankChannelDto) {
  dialogType.value = 'edit'
  currentChannelId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getBankChannelById(row.id) as any
    if (detail) {
      formData.name = detail.name || ''
      formData.type = detail.type ?? 1
      formData.accountNo = detail.accountNo || ''
      formData.bankName = detail.bankName || ''
      formData.remark = detail.remark || ''
    }
  } catch (error) {
    console.error('获取渠道详情失败:', error)
  }
}

async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }
  submitLoading.value = true
  try {
    const data: any = {
      name: formData.name,
      type: formData.type,
      accountNo: formData.accountNo || undefined,
      bankName: formData.type === 1 ? formData.bankName || undefined : undefined,
    }
    if (dialogType.value === 'add') {
      await createBankChannel(data)
      message.success('新增成功')
    } else {
      await updateBankChannel(currentChannelId.value!, { ...data, status: 1 })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handleToggleStatus(row: BankChannelDto) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateBankChannel(row.id, {
      name: row.name,
      type: row.type,
      accountNo: row.accountNo,
      bankName: row.bankName,
      status: newStatus,
    })
    message.success(`${actionText}成功`)
    fetchList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
  }
}

function handleConfigTemplate(row: BankChannelDto) {
  currentTemplateChannel.value = row
  // 解析已有模板
  let existing: Record<string, { excelColumn: string; required: boolean }> = {}
  if (row.importTemplate) {
    try {
      existing = JSON.parse(row.importTemplate)
    } catch {
      existing = {}
    }
  }
  mappingData.value = systemFields.map((f) => ({
    field: f.field,
    label: f.label,
    excelColumn: existing[f.field]?.excelColumn || '',
    required: existing[f.field]?.required ?? false,
  }))
  templateDialogVisible.value = true
}

async function handleSaveTemplate() {
  if (!currentTemplateChannel.value) return
  templateSubmitLoading.value = true
  try {
    const templateJson: Record<string, { excelColumn: string; required: boolean }> = {}
    mappingData.value.forEach((row) => {
      if (row.excelColumn) {
        templateJson[row.field] = { excelColumn: row.excelColumn, required: row.required }
      }
    })
    const ch = currentTemplateChannel.value
    await updateBankChannel(ch.id, {
      name: ch.name,
      type: ch.type,
      accountNo: ch.accountNo,
      bankName: ch.bankName,
      importTemplate: JSON.stringify(templateJson),
      status: ch.status,
    })
    message.success('模板保存成功')
    templateDialogVisible.value = false
    fetchList()
  } finally {
    templateSubmitLoading.value = false
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.template-tip {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
  padding: 12px 16px;
  margin-bottom: 16px;
  color: #666;
  font-size: 13px;
}

.preview-section {
  .preview-title {
    font-size: 14px;
    font-weight: 500;
    margin-bottom: 8px;
    color: #333;
  }

  .preview-json {
    background: #f5f5f5;
    border: 1px solid #e8e8e8;
    border-radius: 4px;
    padding: 12px;
    font-size: 12px;
    max-height: 200px;
    overflow-y: auto;
    white-space: pre-wrap;
    word-break: break-all;
  }
}
</style>
