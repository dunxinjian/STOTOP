<template>
  <div class="page-container">
    <PageHeader title="迁移映射配置">
      <template #left>
        <a-select
          v-model:value="currentSchemeId"
          placeholder="选择迁移方案"
          style="width: 220px;"
          :loading="schemesLoading"
          allowClear
          @change="handleSchemeChange"
        >
          <a-select-option v-for="s in schemes" :key="s.id" :value="s.id">
            {{ s.name }}
          </a-select-option>
        </a-select>
        <a-button type="primary" size="small" @click="handleAddScheme">
          <PlusOutlined />新建方案
        </a-button>
        <a-button size="small" @click="handleEditScheme" :disabled="!currentSchemeId">
          <EditOutlined />编辑方案
        </a-button>
      </template>
      <template #right>
        <a-button @click="handleOpenWizard" :disabled="!currentSchemeId">
          <ThunderboltOutlined />启动向导
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <!-- Tabs -->
      <a-tabs v-model:activeKey="activeTab" @change="loadMappings">
        <a-tab-pane key="account" tab="科目映射" />
        <a-tab-pane key="auxiliary" tab="辅助映射" />
        <a-tab-pane key="asset" tab="资产映射" />
      </a-tabs>

      <!-- 工具栏 -->
      <div class="table-toolbar">
        <a-space>
          <a-button type="primary" size="small" @click="handleAddMapping" :disabled="!currentSchemeId">
            <PlusOutlined />新增
          </a-button>
          <a-button size="small" :disabled="!selectedRowKeys.length" danger @click="handleBatchDelete">
            <DeleteOutlined />删除选中
          </a-button>
        </a-space>
      </div>

      <!-- 科目映射表格 -->
      <a-table
        v-if="activeTab === 'account'"
        :columns="accountColumns"
        :dataSource="accountMappings"
        :loading="tableLoading"
        rowKey="id"
        :pagination="false"
        bordered
        size="small"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'arrow'">
            <span style="color: var(--color-info); font-weight: bold;">→</span>
          </template>
          <template v-if="column.dataIndex === 'mappingType'">
            <a-tag :color="record.mappingType === 'exact' ? 'blue' : record.mappingType === 'prefix' ? 'green' : 'orange'">
              {{ mappingTypeLabel(record.mappingType) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 'active' ? 'success' : 'default'">
              {{ record.status === 'active' ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEditMapping(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDeleteMapping(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>

      <!-- 辅助映射表格 -->
      <a-table
        v-if="activeTab === 'auxiliary'"
        :columns="auxiliaryColumns"
        :dataSource="auxiliaryMappings"
        :loading="tableLoading"
        rowKey="id"
        :pagination="false"
        bordered
        size="small"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'arrow'">
            <span style="color: var(--color-info); font-weight: bold;">→</span>
          </template>
          <template v-if="column.dataIndex === 'strategy'">
            <a-tag color="blue">{{ strategyLabel(record.strategy) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 'active' ? 'success' : 'default'">
              {{ record.status === 'active' ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEditMapping(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDeleteMapping(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>

      <!-- 资产映射表格 -->
      <a-table
        v-if="activeTab === 'asset'"
        :columns="assetColumns"
        :dataSource="assetMappings"
        :loading="tableLoading"
        rowKey="id"
        :pagination="false"
        bordered
        size="small"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'arrow'">
            <span style="color: var(--color-info); font-weight: bold;">→</span>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 'active' ? 'success' : 'default'">
              {{ record.status === 'active' ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEditMapping(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDeleteMapping(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 方案编辑弹窗 -->
    <a-modal
      v-model:open="schemeDialogVisible"
      :title="isEditScheme ? '编辑迁移方案' : '新建迁移方案'"
      :width="500"
      :centered="true"
      :destroyOnClose="true"
    >
      <a-form ref="schemeFormRef" :model="schemeForm" :rules="schemeRules" :labelCol="{ style: { width: '90px' } }">
        <a-form-item label="方案名称" name="name">
          <a-input v-model:value="schemeForm.name" placeholder="请输入方案名称" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="schemeForm.description" :rows="3" placeholder="请输入方案描述" />
        </a-form-item>
        <a-form-item label="源系统" name="sourceSystem">
          <a-input v-model:value="schemeForm.sourceSystem" placeholder="如：金蝶、用友等" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="schemeDialogVisible = false">取消</a-button>
        <a-button type="primary" @click="handleSubmitScheme" :loading="schemeSubmitting">确定</a-button>
      </template>
    </a-modal>

    <!-- 映射编辑弹窗 -->
    <a-modal
      v-model:open="mappingDialogVisible"
      :title="isEditMapping ? '编辑映射' : '新增映射'"
      :width="600"
      :centered="true"
      :destroyOnClose="true"
    >
      <!-- 科目映射表单 -->
      <a-form v-if="activeTab === 'account'" ref="mappingFormRef" :model="mappingForm" :labelCol="{ style: { width: '110px' } }">
        <a-form-item label="源科目编码" name="sourceCode">
          <a-input v-model:value="mappingForm.sourceCode" placeholder="源系统科目编码" />
        </a-form-item>
        <a-form-item label="源科目名称" name="sourceName">
          <a-input v-model:value="mappingForm.sourceName" placeholder="源系统科目名称" />
        </a-form-item>
        <a-form-item label="目标科目编码" name="targetCode">
          <a-input v-model:value="mappingForm.targetCode" placeholder="本系统科目编码" />
        </a-form-item>
        <a-form-item label="目标科目名称" name="targetName">
          <a-input v-model:value="mappingForm.targetName" placeholder="本系统科目名称" />
        </a-form-item>
        <a-form-item label="映射类型" name="mappingType">
          <a-select v-model:value="mappingForm.mappingType" placeholder="请选择">
            <a-select-option value="exact">精确匹配</a-select-option>
            <a-select-option value="prefix">前缀匹配</a-select-option>
            <a-select-option value="regex">正则匹配</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="优先级" name="priority">
          <a-input-number v-model:value="mappingForm.priority" :min="0" :max="999" style="width: 120px;" />
        </a-form-item>
      </a-form>
      <!-- 辅助映射表单 -->
      <a-form v-if="activeTab === 'auxiliary'" ref="mappingFormRef" :model="mappingForm" :labelCol="{ style: { width: '110px' } }">
        <a-form-item label="辅助类型" name="auxType">
          <a-input v-model:value="mappingForm.auxType" placeholder="如：customer、department" />
        </a-form-item>
        <a-form-item label="源编码" name="sourceCode">
          <a-input v-model:value="mappingForm.sourceCode" placeholder="源系统编码" />
        </a-form-item>
        <a-form-item label="源名称" name="sourceName">
          <a-input v-model:value="mappingForm.sourceName" placeholder="源系统名称" />
        </a-form-item>
        <a-form-item label="目标编码" name="targetCode">
          <a-input v-model:value="mappingForm.targetCode" placeholder="本系统编码" />
        </a-form-item>
        <a-form-item label="目标名称" name="targetName">
          <a-input v-model:value="mappingForm.targetName" placeholder="本系统名称" />
        </a-form-item>
        <a-form-item label="处理策略" name="strategy">
          <a-select v-model:value="mappingForm.strategy" placeholder="请选择">
            <a-select-option value="map">映射转换</a-select-option>
            <a-select-option value="create">自动创建</a-select-option>
            <a-select-option value="ignore">忽略</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
      <!-- 资产映射表单 -->
      <a-form v-if="activeTab === 'asset'" ref="mappingFormRef" :model="mappingForm" :labelCol="{ style: { width: '110px' } }">
        <a-form-item label="源资产编号" name="sourceCode">
          <a-input v-model:value="mappingForm.sourceCode" placeholder="源系统资产编号" />
        </a-form-item>
        <a-form-item label="目标资产编号" name="targetCode">
          <a-input v-model:value="mappingForm.targetCode" placeholder="本系统资产编号" />
        </a-form-item>
        <a-form-item label="目标资产名称" name="targetName">
          <a-input v-model:value="mappingForm.targetName" placeholder="本系统资产名称" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="mappingDialogVisible = false">取消</a-button>
        <a-button type="primary" @click="handleSubmitMapping" :loading="mappingSubmitting">确定</a-button>
      </template>
    </a-modal>

    <!-- 向导弹窗 -->
    <MigrationWizard
      v-if="wizardVisible"
      :visible="wizardVisible"
      :scheme-id="currentSchemeId"
      :account-set-id="accountSetId"
      @close="wizardVisible = false"
      @success="handleWizardSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import MigrationWizard from './MigrationWizard.vue'
import {
  getMigrationSchemes,
  getMigrationScheme,
  createMigrationScheme,
  updateMigrationScheme,
  deleteMigrationScheme,
  getAccountMappings,
  createAccountMappings,
  updateAccountMapping,
  deleteAccountMapping,
  getAuxiliaryMappings,
  createAuxiliaryMappings,
  updateAuxiliaryMapping,
  deleteAuxiliaryMapping,
  getAssetMappings,
  createAssetMappings,
  updateAssetMapping,
  deleteAssetMapping,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()
// 账套是全局上下文，直接派生，保证读取始终为最新值（不再一次性快照）
const accountSetId = computed<number | undefined>(() => accountSetStore.currentAccountSetId || undefined)

// 方案管理
const schemes = ref<any[]>([])
const currentSchemeId = ref<string>('')
const schemesLoading = ref(false)
const schemeDialogVisible = ref(false)
const isEditScheme = ref(false)
const schemeSubmitting = ref(false)
const schemeFormRef = ref<FormInstance>()
const schemeForm = reactive({ id: '', name: '', description: '', sourceSystem: '' })
const schemeRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入方案名称', trigger: 'blur' }],
}

// 映射表格
const activeTab = ref<string>('account')
const tableLoading = ref(false)
const accountMappings = ref<any[]>([])
const auxiliaryMappings = ref<any[]>([])
const assetMappings = ref<any[]>([])
const selectedRowKeys = ref<string[]>([])

// 映射编辑
const mappingDialogVisible = ref(false)
const isEditMapping = ref(false)
const mappingSubmitting = ref(false)
const mappingFormRef = ref<FormInstance>()
const mappingForm = reactive({
  id: '',
  sourceCode: '',
  sourceName: '',
  targetCode: '',
  targetName: '',
  mappingType: 'exact',
  priority: 0,
  auxType: '',
  strategy: 'map',
})

// 向导
const wizardVisible = ref(false)

// 表格列定义
const accountColumns = [
  { title: '源科目编码', dataIndex: 'sourceCode', width: 130 },
  { title: '源科目名称', dataIndex: 'sourceName', minWidth: 150 },
  { title: '', dataIndex: 'arrow', width: 40, align: 'center' as const },
  { title: '目标科目编码', dataIndex: 'targetCode', width: 130 },
  { title: '目标科目名称', dataIndex: 'targetName', minWidth: 150 },
  { title: '映射类型', dataIndex: 'mappingType', width: 100, align: 'center' as const },
  { title: '优先级', dataIndex: 'priority', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

const auxiliaryColumns = [
  { title: '辅助类型', dataIndex: 'auxType', width: 110 },
  { title: '源编码', dataIndex: 'sourceCode', width: 120 },
  { title: '源名称', dataIndex: 'sourceName', minWidth: 140 },
  { title: '', dataIndex: 'arrow', width: 40, align: 'center' as const },
  { title: '目标编码', dataIndex: 'targetCode', width: 120 },
  { title: '目标名称', dataIndex: 'targetName', minWidth: 140 },
  { title: '处理策略', dataIndex: 'strategy', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

const assetColumns = [
  { title: '源资产编号', dataIndex: 'sourceCode', width: 150 },
  { title: '', dataIndex: 'arrow', width: 40, align: 'center' as const },
  { title: '目标资产编号', dataIndex: 'targetCode', width: 150 },
  { title: '目标资产名称', dataIndex: 'targetName', minWidth: 200 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

function mappingTypeLabel(type: string) {
  const m: Record<string, string> = { exact: '精确', prefix: '前缀', regex: '正则' }
  return m[type] || type
}

function strategyLabel(s: string) {
  const m: Record<string, string> = { map: '映射转换', create: '自动创建', ignore: '忽略' }
  return m[s] || s
}

function onSelectChange(keys: Key[]) {
  selectedRowKeys.value = keys.map(String)
}

// 加载方案列表
async function loadSchemes() {
  schemesLoading.value = true
  try {
    schemes.value = await getMigrationSchemes({ accountSetId: accountSetId.value }) || []
  } catch { /* ignore */ } finally {
    schemesLoading.value = false
  }
}

// 方案切换
function handleSchemeChange() {
  selectedRowKeys.value = []
  loadMappings()
}

// 加载映射数据
async function loadMappings() {
  if (!currentSchemeId.value) {
    accountMappings.value = []
    auxiliaryMappings.value = []
    assetMappings.value = []
    return
  }
  tableLoading.value = true
  try {
    if (activeTab.value === 'account') {
      accountMappings.value = await getAccountMappings(currentSchemeId.value) || []
    } else if (activeTab.value === 'auxiliary') {
      auxiliaryMappings.value = await getAuxiliaryMappings(currentSchemeId.value) || []
    } else {
      assetMappings.value = await getAssetMappings(currentSchemeId.value) || []
    }
  } catch { /* ignore */ } finally {
    tableLoading.value = false
  }
  selectedRowKeys.value = []
}

// 方案 CRUD
function handleAddScheme() {
  isEditScheme.value = false
  Object.assign(schemeForm, { id: '', name: '', description: '', sourceSystem: '' })
  schemeDialogVisible.value = true
}

async function handleEditScheme() {
  if (!currentSchemeId.value) return
  isEditScheme.value = true
  try {
    const data = await getMigrationScheme(currentSchemeId.value)
    Object.assign(schemeForm, data)
  } catch { /* ignore */ }
  schemeDialogVisible.value = true
}

async function handleSubmitScheme() {
  const valid = await schemeFormRef.value?.validate().catch(() => false)
  if (!valid) return
  schemeSubmitting.value = true
  try {
    if (isEditScheme.value) {
      await updateMigrationScheme(schemeForm.id, { ...schemeForm, accountSetId: accountSetId.value })
      message.success('更新成功')
    } else {
      const res = await createMigrationScheme({ ...schemeForm, accountSetId: accountSetId.value }) as any
      currentSchemeId.value = res?.id || ''
      message.success('创建成功')
    }
    schemeDialogVisible.value = false
    loadSchemes()
    loadMappings()
  } catch {
    message.error('操作失败')
  } finally {
    schemeSubmitting.value = false
  }
}

// 映射 CRUD
function handleAddMapping() {
  isEditMapping.value = false
  Object.assign(mappingForm, { id: '', sourceCode: '', sourceName: '', targetCode: '', targetName: '', mappingType: 'exact', priority: 0, auxType: '', strategy: 'map' })
  mappingDialogVisible.value = true
}

function handleEditMapping(record: any) {
  isEditMapping.value = true
  Object.assign(mappingForm, record)
  mappingDialogVisible.value = true
}

async function handleSubmitMapping() {
  mappingSubmitting.value = true
  try {
    const payload = { ...mappingForm, schemeId: currentSchemeId.value }
    // 新增走批量端点（后端要求 { items: [...] }）：单条须包进 items，否则 request.Items 为空 → 静默零行插入
    if (activeTab.value === 'account') {
      if (isEditMapping.value) {
        await updateAccountMapping(mappingForm.id, payload)
      } else {
        await createAccountMappings({ items: [payload] })
      }
    } else if (activeTab.value === 'auxiliary') {
      if (isEditMapping.value) {
        await updateAuxiliaryMapping(mappingForm.id, payload)
      } else {
        await createAuxiliaryMappings({ items: [payload] })
      }
    } else {
      if (isEditMapping.value) {
        await updateAssetMapping(mappingForm.id, payload)
      } else {
        await createAssetMappings({ items: [payload] })
      }
    }
    message.success(isEditMapping.value ? '更新成功' : '新增成功')
    mappingDialogVisible.value = false
    loadMappings()
  } catch {
    message.error('操作失败')
  } finally {
    mappingSubmitting.value = false
  }
}

function handleDeleteMapping(record: any) {
  Modal.confirm({
    title: '确认删除',
    content: '确定要删除该映射记录吗？',
    okType: 'danger',
    async onOk() {
      try {
        if (activeTab.value === 'account') await deleteAccountMapping(record.id)
        else if (activeTab.value === 'auxiliary') await deleteAuxiliaryMapping(record.id)
        else await deleteAssetMapping(record.id)
        message.success('删除成功')
        loadMappings()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

function handleBatchDelete() {
  if (!selectedRowKeys.value.length) return
  Modal.confirm({
    title: '批量删除',
    content: `确定要删除选中的 ${selectedRowKeys.value.length} 条记录吗？`,
    okType: 'danger',
    async onOk() {
      try {
        for (const id of selectedRowKeys.value) {
          if (activeTab.value === 'account') await deleteAccountMapping(id)
          else if (activeTab.value === 'auxiliary') await deleteAuxiliaryMapping(id)
          else await deleteAssetMapping(id)
        }
        message.success('批量删除成功')
        loadMappings()
      } catch {
        message.error('删除失败')
      }
    },
  })
}

// 向导
function handleOpenWizard() {
  wizardVisible.value = true
}

function handleWizardSuccess() {
  wizardVisible.value = false
  loadMappings()
}

// 监听账套切换：方案/映射按账套隔离，切换后旧方案 ID 对新账套无效，需重置选择并重新加载
watch(() => accountSetStore.currentAccountSetId, () => {
  currentSchemeId.value = ''
  selectedRowKeys.value = []
  accountMappings.value = []
  auxiliaryMappings.value = []
  assetMappings.value = []
  loadSchemes()
})

onMounted(() => {
  loadSchemes()
})
</script>

<style scoped lang="scss">
.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
