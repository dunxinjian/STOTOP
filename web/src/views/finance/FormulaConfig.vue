<template>
  <div class="page-container">
    <PageHeader title="公式配置">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <PlusOutlined />新增
        </a-button>
        <a-button @click="handleInitDefaults" :loading="initializing">
          <ThunderboltOutlined />初始化默认公式
        </a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select
              v-model:value="reportType"
              style="width: 180px"
              placeholder="报表类型"
              @change="handleReportTypeChange"
            >
              <a-select-option value="ProfitStatement">利润表</a-select-option>
              <a-select-option value="BalanceSheet">资产负债表</a-select-option>
              <a-select-option value="CashFlow">现金流量表</a-select-option>
              <a-select-option value="AmoebaPL">阿米巴损益表</a-select-option>
            </a-select>
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-input
              v-model:value="searchKeyword"
              placeholder="搜索项目名称"
              style="width: 200px"
              allowClear
              @pressEnter="handleSearch"
            >
              <template #suffix>
                <SearchOutlined class="search-icon" @click="handleSearch" />
              </template>
            </a-input>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 公式列表 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :dataSource="filteredData"
        rowKey="id"
        :loading="loading"
        bordered
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'rowIndex'">
            {{ record.rowIndex }}
          </template>
          <template v-if="column.dataIndex === 'itemName'">
            <a class="link-text" @click="handleEdit(record)">{{ record.itemName }}</a>
          </template>
          <template v-if="column.dataIndex === 'formula'">
            <span class="formula-text">{{ record.formula || '-' }}</span>
          </template>
          <template v-if="column.dataIndex === 'formulaType'">
            <a-tag :color="formulaTypeColor(record.formulaType)">{{ formulaTypeLabel(record.formulaType) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'isEnabled'">
            <a-tag :color="record.isEnabled ? 'success' : 'default'">
              {{ record.isEnabled ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-space>
              <a @click="handleEdit(record)">编辑</a>
              <a class="danger-link" @click="handleDelete(record)">删除</a>
            </a-space>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑公式' : '新增公式'"
      :width="640"
      :centered="true"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :labelCol="{ span: 5 }"
        :wrapperCol="{ span: 17 }"
      >
        <a-form-item label="报表类型" name="reportType">
          <a-select v-model:value="formData.reportType" placeholder="请选择" :disabled="isEdit">
            <a-select-option value="ProfitStatement">利润表</a-select-option>
            <a-select-option value="BalanceSheet">资产负债表</a-select-option>
            <a-select-option value="CashFlow">现金流量表</a-select-option>
            <a-select-option value="AmoebaPL">阿米巴损益表</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="行号" name="rowIndex">
          <a-input-number v-model:value="formData.rowIndex" :min="1" :max="999" style="width: 100%" />
        </a-form-item>
        <a-form-item label="项目名称" name="itemName">
          <a-input v-model:value="formData.itemName" placeholder="请输入项目名称" />
        </a-form-item>
        <a-form-item label="公式类型" name="formulaType">
          <a-select v-model:value="formData.formulaType" placeholder="请选择">
            <a-select-option value="account">科目取值</a-select-option>
            <a-select-option value="row">行间运算</a-select-option>
            <a-select-option value="expression">表达式</a-select-option>
            <a-select-option value="fixed">固定值</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="公式表达式" name="formula">
          <div class="formula-editor">
            <div class="formula-toolbar">
              <a-space size="small" wrap>
                <a-tooltip title="插入科目求和函数 SUM(科目1,科目2,...)">
                  <a-button size="small" @click="insertFormula('SUM')">SUM</a-button>
                </a-tooltip>
                <a-tooltip title="插入单科目取值 ACCOUNT(科目编码)">
                  <a-button size="small" @click="insertFormula('ACCOUNT')">ACCOUNT</a-button>
                </a-tooltip>
                <a-tooltip title="插入行引用 ROW(行号)">
                  <a-button size="small" @click="insertFormula('ROW')">ROW</a-button>
                </a-tooltip>
                <a-divider type="vertical" />
                <a-button size="small" @click="insertOperator('+')">+</a-button>
                <a-button size="small" @click="insertOperator('-')">-</a-button>
                <a-button size="small" @click="insertOperator('*')">×</a-button>
                <a-button size="small" @click="insertOperator('/')">÷</a-button>
              </a-space>
            </div>
            <a-textarea
              ref="formulaTextareaRef"
              v-model:value="formData.formula"
              placeholder="点击上方按钮插入函数，或直接输入公式"
              :rows="3"
            />
            <div class="formula-hint">
              支持：SUM(5001,5051) 科目求和 | ACCOUNT(5401) 单科目 | ROW(1)-ROW(2) 行间运算 | + - * / 四则运算
            </div>
          </div>
        </a-form-item>
        <a-form-item label="关联科目编码" :name="formData.formulaType === 'account' ? 'accountCodes' : undefined">
          <a-select
            v-model:value="selectedAccountCodes"
            mode="multiple"
            show-search
            :filter-option="filterAccountOption"
            placeholder="搜索并选择科目"
            allow-clear
            option-filter-prop="label"
            style="width: 100%"
          >
            <a-select-option
              v-for="acc in accountOptions"
              :key="acc.code"
              :value="acc.code"
              :label="`${acc.code} ${acc.name}`"
            >
              {{ acc.code }} - {{ acc.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="排序号">
          <a-input-number v-model:value="formData.sortOrder" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item label="启用">
          <a-switch v-model:checked="formData.isEnabled" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取 消</a-button>
        <a-button type="primary" @click="handleSubmit" :loading="submitting">确 定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined, SearchOutlined, ThunderboltOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getFormulas, createFormula, updateFormula, deleteFormula, initDefaultFormulas, getAccountTree,
  type FormulaDto,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选
const reportType = ref('ProfitStatement')
const searchKeyword = ref('')

// 数据
const loading = ref(false)
const tableData = ref<FormulaDto[]>([])
const initializing = ref(false)
const submitting = ref(false)

// 科目列表
const accountOptions = ref<{ code: string; name: string }[]>([])
const accountOptionsLoaded = ref(false)
const selectedAccountCodes = ref<string[]>([])
const formulaTextareaRef = ref()

// 扁平化科目树
function flattenAccountTree(tree: any[]): { code: string; name: string }[] {
  const result: { code: string; name: string }[] = []
  function walk(nodes: any[]) {
    for (const node of nodes) {
      if (node.code) result.push({ code: node.code, name: node.name || '' })
      if (node.children?.length) walk(node.children)
    }
  }
  walk(tree)
  return result
}

async function loadAccountOptions() {
  if (accountOptionsLoaded.value) return
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const tree = await getAccountTree(undefined, accountSetId)
    accountOptions.value = flattenAccountTree(Array.isArray(tree) ? tree : (tree as any)?.data ?? [])
    accountOptionsLoaded.value = true
  } catch (e) {
    console.error('加载科目列表失败', e)
  }
}

function filterAccountOption(input: string, option: any): boolean {
  const label: string = option?.label ?? ''
  return label.toLowerCase().includes(input.toLowerCase())
}

function insertFormula(type: string) {
  const textarea = formulaTextareaRef.value?.$el?.querySelector('textarea')
    || formulaTextareaRef.value?.resizableTextArea?.textArea
  if (!textarea) {
    const inserts: Record<string, string> = { SUM: 'SUM()', ACCOUNT: 'ACCOUNT()', ROW: 'ROW()' }
    formData.formula = (formData.formula || '') + (inserts[type] || '')
    return
  }
  const start = textarea.selectionStart || 0
  const end = textarea.selectionEnd || 0
  const current = formData.formula || ''
  const inserts: Record<string, string> = { SUM: 'SUM()', ACCOUNT: 'ACCOUNT()', ROW: 'ROW()' }
  const insert = inserts[type] || ''
  formData.formula = current.substring(0, start) + insert + current.substring(end)
  nextTick(() => {
    const pos = start + insert.length - 1
    textarea.setSelectionRange(pos, pos)
    textarea.focus()
  })
}

function insertOperator(op: string) {
  formData.formula = (formData.formula || '') + op
}

// 弹窗
const dialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref()
const formData = reactive({
  id: 0,
  reportType: 'ProfitStatement',
  rowIndex: 1,
  itemName: '',
  formulaType: 'account',
  formula: '',
  accountCodes: '',
  displayConfig: '',
  isEnabled: true,
  sortOrder: 0,
  accountSetId: 0,
})

const formRules: Record<string, any[]> = {
  reportType: [{ required: true, message: '请选择报表类型', trigger: 'change' }],
  rowIndex: [{ required: true, message: '请输入行号', trigger: 'blur' }],
  itemName: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
  formulaType: [{ required: true, message: '请选择公式类型', trigger: 'change' }],
}

const columns = [
  { title: '行号', dataIndex: 'rowIndex', key: 'rowIndex', width: 80, align: 'center' as const, sorter: (a: FormulaDto, b: FormulaDto) => a.rowIndex - b.rowIndex },
  { title: '项目名称', dataIndex: 'itemName', key: 'itemName', width: 200 },
  { title: '公式类型', dataIndex: 'formulaType', key: 'formulaType', width: 100, align: 'center' as const },
  { title: '公式表达式', dataIndex: 'formula', key: 'formula', minWidth: 240, ellipsis: true },
  { title: '关联科目', dataIndex: 'accountCodes', key: 'accountCodes', width: 160, ellipsis: true },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

// 搜索过滤
const filteredData = computed(() => {
  if (!searchKeyword.value) return tableData.value
  const kw = searchKeyword.value.toLowerCase()
  return tableData.value.filter(item =>
    item.itemName.toLowerCase().includes(kw)
  )
})

// 公式类型标签
function formulaTypeLabel(type: string): string {
  const map: Record<string, string> = {
    account: '科目取值',
    row: '行间运算',
    expression: '表达式',
    fixed: '固定值',
  }
  return map[type] || type
}

function formulaTypeColor(type: string): string {
  const map: Record<string, string> = {
    account: 'blue',
    row: 'green',
    expression: 'orange',
    fixed: 'default',
  }
  return map[type] || 'default'
}

// 加载数据
async function loadData() {
  loading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await getFormulas({ reportType: reportType.value, accountSetId }) as any
    // 后端包裹 ApiResult<T>，数据可能在 res.data 或直接返回
    const data = res?.data ?? res
    tableData.value = Array.isArray(data) ? data : []
  } catch (error) {
    console.error('加载公式列表失败', error)
    tableData.value = []
  } finally {
    loading.value = false
  }
}

function handleReportTypeChange() {
  loadData()
}

function handleSearch() {
  // 前端搜索，无需重新加载
}

// 新增
function handleAdd() {
  isEdit.value = false
  resetForm()
  selectedAccountCodes.value = []
  formData.reportType = reportType.value
  formData.accountSetId = accountSetStore.getCurrentAccountSetId() || 0
  dialogVisible.value = true
  loadAccountOptions()
}

// 编辑
function handleEdit(row: any) {
  isEdit.value = true
  Object.assign(formData, {
    id: row.id,
    reportType: row.reportType,
    rowIndex: row.rowIndex,
    itemName: row.itemName,
    formulaType: row.formulaType,
    formula: row.formula || '',
    accountCodes: row.accountCodes || '',
    displayConfig: row.displayConfig || '',
    isEnabled: row.isEnabled,
    sortOrder: row.sortOrder,
    accountSetId: row.accountSetId,
  })
  // 将逗号分隔字符串解析为数组
  selectedAccountCodes.value = row.accountCodes
    ? row.accountCodes.split(',').map((s: string) => s.trim()).filter(Boolean)
    : []
  dialogVisible.value = true
  loadAccountOptions()
}

// 提交
async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    // 将数组转回逗号分隔字符串
    formData.accountCodes = selectedAccountCodes.value.join(',')
    const data = {
      ...formData,
      accountSetId: formData.accountSetId || accountSetStore.getCurrentAccountSetId() || 0,
    }
    if (isEdit.value) {
      await updateFormula(formData.id, data)
      message.success('更新成功')
    } else {
      await createFormula(data)
      message.success('创建成功')
    }
    dialogVisible.value = false
    loadData()
  } catch (error) {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}

// 删除
function handleDelete(row: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除公式「${row.itemName}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteFormula(row.id)
        message.success('删除成功')
        loadData()
      } catch (error) {
        message.error('删除失败')
      }
    },
  })
}

// 初始化默认公式
function handleInitDefaults() {
  const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }
  Modal.confirm({
    title: '初始化默认公式',
    content: `确定要为当前报表类型初始化默认公式吗？如果已存在公式，将不会覆盖。`,
    async onOk() {
      initializing.value = true
      try {
        const res = await initDefaultFormulas({
          reportType: reportType.value,
          accountSetId,
        }) as any
        const count = res?.data ?? res ?? 0
        if (count > 0) {
          message.success(`成功初始化 ${count} 条公式`)
        } else {
          message.info('该报表类型已存在公式，无需初始化')
        }
        loadData()
      } catch (error) {
        message.error('初始化失败')
      } finally {
        initializing.value = false
      }
    },
  })
}

// 重置表单
function resetForm() {
  formData.id = 0
  formData.reportType = 'ProfitStatement'
  formData.rowIndex = 1
  formData.itemName = ''
  formData.formulaType = 'account'
  formData.formula = ''
  formData.accountCodes = ''
  formData.displayConfig = ''
  formData.isEnabled = true
  formData.sortOrder = 0
  formData.accountSetId = 0
  selectedAccountCodes.value = []
}

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, () => {
  accountOptionsLoaded.value = false
  accountOptions.value = []
  loadData()
})

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.formula-editor {
  .formula-toolbar {
    margin-bottom: 0;
    padding: 6px 8px;
    background: #fafafa;
    border: 1px solid #d9d9d9;
    border-bottom: none;
    border-radius: 6px 6px 0 0;

    :deep(.ant-btn) {
      font-family: 'Consolas', 'Monaco', monospace;
      min-width: 32px;
    }
  }

  :deep(.ant-input) {
    border-radius: 0 0 6px 6px;
    font-family: 'Consolas', 'Monaco', monospace;
  }

  .formula-hint {
    margin-top: 4px;
    font-size: 12px;
    color: #999;
  }
}

.toolbar-left .search-icon {
  cursor: pointer;
  color: #909399;

  &:hover {
    color: var(--color-primary);
  }
}

.link-text {
  color: var(--text-1);
  cursor: pointer;
  text-decoration: none;
}

.link-text:hover {
  color: var(--color-primary);
  text-decoration: underline;
}

.danger-link {
  color: var(--color-danger);
}

.formula-text {
  font-family: 'Courier New', monospace;
  font-size: 13px;
  color: #333;
}

/* 表格卡片充满剩余空间 */
.table-card {
  flex: 1;
  overflow: hidden;
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.ant-card-body) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 12px;
  }

  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    min-height: 0;
  }

  :deep(.ant-table) {
    height: 100%;
  }

  :deep(.ant-table-container) {
    height: 100%;
    display: flex;
    flex-direction: column;
  }

  :deep(.ant-table-body) {
    flex: 1;
    overflow-y: auto !important;
  }
}
</style>
