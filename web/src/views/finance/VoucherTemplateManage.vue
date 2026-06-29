<template>
  <div class="voucher-template-page">
    <PageHeader title="凭证模板" />

    <!-- 工具栏 -->
    <a-card :bordered="false" class="toolbar-section">
      <div class="toolbar-bar">
        <div class="toolbar-left"></div>
        <div class="toolbar-right">
          <a-button type="primary" @click="openCreate">
            <PlusOutlined />新增模板
          </a-button>
        </div>
      </div>
    </a-card>

    <!-- 模板列表 -->
    <a-table :columns="listColumns" :dataSource="templateList" :loading="loading" bordered rowKey="id" :pagination="false" style="width: 100%">
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'createTime'">
          {{ formatDate(record.createTime) }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" @click="openEdit(record)">编辑</a-button>
          <a-button type="link" style="color: var(--color-success)" @click="openGenerate(record)">生成凭证</a-button>
          <a-button type="link" danger @click="handleDelete(record)">删除</a-button>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑凭证模板' : '新增凭证模板'"
      :width="900"
      :destroyOnClose="true"
    >
      <a-form :model="form" :rules="rules" ref="formRef" :labelCol="{ span: 4 }" :wrapperCol="{ span: 20 }">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="模板名称" name="name">
              <a-input v-model:value="form.name" placeholder="如：月末结转、计提折旧" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="排序">
              <a-input-number v-model:value="form.sort" :min="0" :max="9999" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="描述" :labelCol="{ span: 2 }" :wrapperCol="{ span: 22 }">
          <a-textarea v-model:value="form.description" :rows="2" placeholder="模板用途说明（选填）" />
        </a-form-item>

        <!-- 分录表格 -->
        <div class="entry-section">
          <div class="entry-header">
            <span class="entry-title">分录明细</span>
            <a-button type="primary" size="small" @click="addEntry">
              <PlusOutlined />添加行
            </a-button>
          </div>
          <a-table :columns="entryColumns" :dataSource="form.entries" bordered size="small" rowKey="seq" :pagination="false">
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">{{ index + 1 }}</template>
              <template v-if="column.dataIndex === 'summary'">
                <a-input v-model:value="record.summary" size="small" placeholder="摘要" />
              </template>
              <template v-if="column.dataIndex === 'account'">
                <div class="account-select-cell">
                  <span v-if="record.accountId" class="account-tag">{{ record.accountCode }} {{ record.accountName }}</span>
                  <a-button size="small" type="link" @click="openAccountPicker(index)">选择科目</a-button>
                </div>
              </template>
              <template v-if="column.dataIndex === 'debitAmount'">
                <a-input-number
                  v-model:value="record.debitAmount"
                  :min="0"
                  :precision="2"
                  size="small"
                  style="width: 100%"
                  :controls="false"
                  @change="clearCredit(record)"
                />
              </template>
              <template v-if="column.dataIndex === 'creditAmount'">
                <a-input-number
                  v-model:value="record.creditAmount"
                  :min="0"
                  :precision="2"
                  size="small"
                  style="width: 100%"
                  :controls="false"
                  @change="clearDebit(record)"
                />
              </template>
              <template v-if="column.dataIndex === 'entryAction'">
                <a-button type="link" size="small" danger @click="removeEntry(index)">
                  <DeleteOutlined />
                </a-button>
              </template>
            </template>
          </a-table>

          <!-- 合计行 -->
          <div class="entry-totals">
            <span>合计：借方 <strong>{{ totalDebit.toFixed(2) }}</strong>&nbsp;&nbsp;贷方 <strong>{{ totalCredit.toFixed(2) }}</strong></span>
            <a-tag v-if="isBalanced" color="success">借贷平衡</a-tag>
            <a-tag v-else color="error">借贷不平衡</a-tag>
          </div>
        </div>
      </a-form>

      <template #footer>
        <a-button @click="dialogVisible = false">取 消</a-button>
        <a-button type="primary" @click="handleSubmit" :loading="saving">保 存</a-button>
      </template>
    </a-modal>

    <!-- 科目选择弹窗 -->
    <a-modal v-model:open="accountDialogVisible" title="选择科目" :width="600">
      <div class="account-tabs">
        <span
          v-for="cat in accountCategories"
          :key="cat"
          :class="['tab-item', { active: currentCategory === cat }]"
          @click="switchCategory(cat)"
        >{{ cat }}</span>
      </div>
      <div class="account-tree-wrap">
        <a-tree
          :treeData="currentCategoryTree"
          :fieldNames="{ title: 'displayName', children: 'children', key: 'id' }"
          :defaultExpandAll="true"
          @select="handleAccountNodeSelect"
        />
      </div>
      <template #footer>
        <a-button @click="accountDialogVisible = false">取 消</a-button>
        <a-button type="primary" @click="confirmAccountSelect">确 定</a-button>
      </template>
    </a-modal>

    <!-- 生成凭证弹窗 -->
    <a-modal v-model:open="generateDialogVisible" title="从模板生成凭证" :width="400">
      <a-form :labelCol="{ span: 6 }" :wrapperCol="{ span: 16 }">
        <a-form-item label="凭证日期">
          <a-date-picker
            v-model:value="generateDate"
            valueFormat="YYYY-MM-DD"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="generateDialogVisible = false">取 消</a-button>
        <a-button type="primary" @click="handleGenerate" :loading="generating">生 成</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getVoucherTemplates,
  getVoucherTemplateDetail,
  createVoucherTemplate,
  updateVoucherTemplate,
  deleteVoucherTemplate,
  generateVoucherFromTemplate,
  getAccountTree
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useRouter } from 'vue-router'

const accountSetStore = useAccountSetStore()
const router = useRouter()

// 列表
const templateList = ref<any[]>([])
const loading = ref(false)

const listColumns = [
  { title: '模板名称', dataIndex: 'name', key: 'name', width: 150 },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '分录数', dataIndex: 'entryCount', key: 'entryCount', width: 80, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 220, align: 'center' as const, fixed: 'right' as const },
]

const entryColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '摘要', dataIndex: 'summary', key: 'summary', width: 130 },
  { title: '科目', dataIndex: 'account', key: 'account', width: 200 },
  { title: '借方金额', dataIndex: 'debitAmount', key: 'debitAmount', width: 130 },
  { title: '贷方金额', dataIndex: 'creditAmount', key: 'creditAmount', width: 130 },
  { title: '操作', dataIndex: 'entryAction', key: 'entryAction', width: 60, align: 'center' as const },
]

// 弹窗
const dialogVisible = ref(false)
const isEdit = ref(false)
const saving = ref(false)
const formRef = ref()
const form = reactive({
  id: 0 as number,
  name: '',
  description: '',
  sort: 0,
  entries: [] as any[]
})

const rules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入模板名称', trigger: 'blur' }]
}

// 合计
const totalDebit = computed(() => form.entries.reduce((s, e) => s + (e.debitAmount || 0), 0))
const totalCredit = computed(() => form.entries.reduce((s, e) => s + (e.creditAmount || 0), 0))
const isBalanced = computed(() => Math.abs(totalDebit.value - totalCredit.value) < 0.01)

// 科目选择
const accountDialogVisible = ref(false)
const accountCategories = ['资产', '负债', '权益', '成本', '损益']
const currentCategory = ref('资产')
const categoryTreeMap = ref<Record<string, any[]>>({})
const selectedAccountNode = ref<any>(null)
const editingEntryIndex = ref(0)

const currentCategoryTree = computed(() => addDisplayName(categoryTreeMap.value[currentCategory.value] || []))

function addDisplayName(nodes: any[]): any[] {
  return nodes.map(n => ({
    ...n,
    displayName: `${n.code} ${n.name}`,
    children: n.children ? addDisplayName(n.children) : []
  }))
}

// 生成凭证弹窗
const generateDialogVisible = ref(false)
const generateDate = ref(new Date().toISOString().split('T')[0])
const generating = ref(false)
const currentTemplateId = ref(0)

function formatDate(str: string) {
  if (!str) return ''
  return str.replace('T', ' ').slice(0, 16)
}

async function loadList() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) return
  loading.value = true
  try {
    const res = await getVoucherTemplates(accountSetId) as any[]
    templateList.value = res || []
  } catch {
    message.error('加载失败')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  isEdit.value = false
  form.id = 0
  form.name = ''
  form.description = ''
  form.sort = 0
  form.entries = []
  addEntry()
  dialogVisible.value = true
}

async function openEdit(row: any) {
  isEdit.value = true
  form.id = row.id
  try {
    const detail = await getVoucherTemplateDetail(row.id) as any
    form.name = detail.name
    form.description = detail.description || ''
    form.sort = detail.sort || 0
    form.entries = (detail.entries || []).map((e: any) => ({
      id: e.id,
      summary: e.summary || '',
      accountId: e.accountId,
      accountCode: e.accountCode,
      accountName: e.accountName,
      debitAmount: e.debitAmount || 0,
      creditAmount: e.creditAmount || 0,
      seq: e.seq,
      auxiliaryJson: e.auxiliaryJson
    }))
    if (form.entries.length === 0) addEntry()
  } catch {
    message.error('加载模板详情失败')
    return
  }
  dialogVisible.value = true
}

function addEntry() {
  form.entries.push({
    id: 0,
    summary: '',
    accountId: 0,
    accountCode: '',
    accountName: '',
    debitAmount: 0,
    creditAmount: 0,
    seq: form.entries.length + 1,
    auxiliaryJson: null
  })
}

function removeEntry(index: number) {
  form.entries.splice(index, 1)
}

function clearCredit(row: any) {
  if (row.debitAmount && row.debitAmount > 0) row.creditAmount = 0
}

function clearDebit(row: any) {
  if (row.creditAmount && row.creditAmount > 0) row.debitAmount = 0
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  const payload = {
    accountSetId,
    name: form.name,
    description: form.description,
    sort: form.sort,
    entries: form.entries.map((e, idx) => ({
      ...e,
      seq: idx + 1
    }))
  }

  saving.value = true
  try {
    if (isEdit.value) {
      await updateVoucherTemplate(form.id, payload)
      message.success('更新成功')
    } else {
      await createVoucherTemplate(payload)
      message.success('创建成功')
    }
    dialogVisible.value = false
    await loadList()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: any) {
  Modal.confirm({
    title: '提示',
    content: `确定删除模板"${row.name}"吗？`,
    onOk: async () => {
      await deleteVoucherTemplate(row.id)
      message.success('删除成功')
      await loadList()
    }
  })
}

function openGenerate(row: any) {
  currentTemplateId.value = row.id
  generateDate.value = new Date().toISOString().split('T')[0]
  generateDialogVisible.value = true
}

async function handleGenerate() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }
  generating.value = true
  try {
    const res = await generateVoucherFromTemplate(currentTemplateId.value, {
      date: generateDate.value,
      accountSetId
    }) as any
    message.success('凭证生成成功')
    generateDialogVisible.value = false
    if (res) {
      router.push(`/finance/voucher/entry/${res}`)
    }
  } catch {
    message.error('生成失败')
  } finally {
    generating.value = false
  }
}

// 科目选择
async function openAccountPicker(index: number) {
  editingEntryIndex.value = index
  selectedAccountNode.value = null
  await loadCategoryTree(currentCategory.value)
  accountDialogVisible.value = true
}

async function switchCategory(cat: string) {
  currentCategory.value = cat
  await loadCategoryTree(cat)
}

async function loadCategoryTree(cat: string) {
  if (!categoryTreeMap.value[cat]) {
    try {
      const res = await getAccountTree(cat) as any[]
      categoryTreeMap.value[cat] = res || []
    } catch {
      categoryTreeMap.value[cat] = []
    }
  }
}

function handleAccountNodeSelect(_selectedKeys: any[], info: any) {
  selectedAccountNode.value = info.node
}

function confirmAccountSelect() {
  if (!selectedAccountNode.value) {
    message.warning('请选择科目')
    return
  }
  const entry = form.entries[editingEntryIndex.value]
  if (entry) {
    entry.accountId = selectedAccountNode.value.id
    entry.accountCode = selectedAccountNode.value.code
    entry.accountName = selectedAccountNode.value.name
  }
  accountDialogVisible.value = false
}

onMounted(() => {
  loadList()
})

// 监听账套变化，自动重新加载列表
watch(() => accountSetStore.currentAccountSetId, async (newId) => {
  if (newId) {
    templateList.value = []
    await loadList()
  }
})
</script>

<style scoped lang="scss">
.voucher-template-page {
  padding: 16px;
  background: #fff;
}

.toolbar-section {
  margin-bottom: 8px;
  :deep(.ant-card-body) { padding: 12px 16px; }
}

.toolbar-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: nowrap;
  gap: 8px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.entry-section {
  margin-top: 8px;

  .entry-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;

    .entry-title {
      font-size: 14px;
      font-weight: 600;
      color: #333;
    }
  }

  .entry-totals {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    background: #f5f7fa;
    border: 1px solid #e4e7ed;
    border-top: none;
    font-size: 13px;
    color: #666;
  }
}

.account-select-cell {
  display: flex;
  align-items: center;
  gap: 6px;

  .account-tag {
    font-size: 12px;
    color: var(--color-info);
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}

.account-tabs {
  display: flex;
  gap: 24px;
  padding-bottom: 10px;
  border-bottom: 1px solid #eee;
  margin-bottom: 12px;

  .tab-item {
    font-size: 14px;
    color: #999;
    cursor: pointer;
    padding-bottom: 6px;
    position: relative;

    &.active {
      color: var(--text-1);
      &::after {
        content: '';
        position: absolute;
        bottom: -1px;
        left: 0;
        right: 0;
        height: 2px;
        background: var(--color-primary);
      }
    }

    &:hover { color: var(--color-primary); }
  }
}

.account-tree-wrap {
  height: 380px;
  overflow-y: auto;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  padding: 8px;
}
</style>
