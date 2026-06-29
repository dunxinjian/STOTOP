<template>
  <div class="page-container account-template-manage">
    <PageHeader title="科目模板">
    </PageHeader>

    <div class="template-body">
      <!-- 左侧：模板列表 -->
      <div class="template-list-panel">
        <div class="panel-header">
          <span class="panel-title">模板列表</span>
          <a-button type="primary" size="small" @click="handleAddTemplate">
            <PlusOutlined />新增
          </a-button>
        </div>
        <a-spin :spinning="templatesLoading">
          <div class="template-list" v-if="templates.length > 0">
            <div
              v-for="tpl in templates"
              :key="tpl.id"
              :class="['template-item', { active: selectedTemplateId === tpl.id }]"
              @click="handleSelectTemplate(tpl)"
            >
              <div class="template-item-main">
                <div class="template-item-name">{{ tpl.name }}</div>
                <div class="template-item-meta">
                  <span class="template-item-code">{{ tpl.code }}</span>
                  <a-tag v-if="tpl.isPreset" color="blue" size="small">预置</a-tag>
                </div>
                <div class="template-item-desc" v-if="tpl.description">{{ tpl.description }}</div>
                <div class="template-item-count">科目数量：{{ tpl.itemCount ?? 0 }}</div>
              </div>
              <a-tooltip title="删除" v-if="!tpl.isPreset">
                <span class="template-item-delete" @click.stop="handleDeleteTemplate(tpl)">
                  <DeleteOutlined />
                </span>
              </a-tooltip>
            </div>
          </div>
          <div v-else class="template-list-empty">
            <EmptyState />
          </div>
        </a-spin>
      </div>

      <!-- 右侧：科目项树 -->
      <div class="template-items-panel">
        <div class="panel-header" v-if="selectedTemplate">
          <span class="panel-title">{{ selectedTemplate.name }} - 科目项</span>
          <a-button type="primary" size="small" @click="handleAddItem(null)">
            <PlusOutlined />添加一级科目
          </a-button>
        </div>
        <div class="panel-header" v-else>
          <span class="panel-title">科目项</span>
        </div>
        <a-spin :spinning="itemsLoading">
          <a-table
            v-if="selectedTemplate"
            :columns="itemColumns"
            :dataSource="flattenItems"
            rowKey="id"
            :bordered="false"
            :pagination="false"
            :loading="false"
            :rowClassName="() => 'tree-row'"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'code'">
                <div class="code-cell" :style="{ paddingLeft: ((record._level || 1) - 1) * 24 + 'px' }">
                  <span
                    v-if="record._hasChildren"
                    class="expand-icon"
                    @click.stop="toggleExpand(record)"
                  >
                    <RightOutlined v-if="!isExpanded(record)" />
                    <DownOutlined v-else />
                  </span>
                  <span v-else class="expand-placeholder"></span>
                  <span class="code-text">{{ record.code }}</span>
                </div>
              </template>
              <template v-if="column.dataIndex === 'name'">
                <span>{{ record.name }}</span>
              </template>
              <template v-if="column.dataIndex === 'category'">
                <span>{{ record.category }}</span>
              </template>
              <template v-if="column.dataIndex === 'balanceDirection'">
                {{ record.balanceDirection === '借' ? '借' : '贷' }}
              </template>
              <template v-if="column.dataIndex === 'isLeaf'">
                <a-tag :color="record.isLeaf ? 'green' : 'default'">
                  {{ record.isLeaf ? '是' : '否' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'auxiliary'">
                {{ record.auxiliary || '-' }}
              </template>
              <template v-if="column.dataIndex === 'action'">
                <div class="row-actions">
                  <a-tooltip title="编辑">
                    <span class="action-icon" @click="handleEditItem(record)">
                      <EditOutlined />
                    </span>
                  </a-tooltip>
                  <a-tooltip title="添加子级">
                    <span class="action-icon" @click="handleAddItem(record)">
                      <PlusOutlined />
                    </span>
                  </a-tooltip>
                  <a-tooltip title="删除">
                    <span class="action-icon danger" @click="handleDeleteItem(record)">
                      <DeleteOutlined />
                    </span>
                  </a-tooltip>
                </div>
              </template>
            </template>
            <template #emptyText><EmptyState /></template>
          </a-table>
          <div v-else class="items-empty-tip">
            <EmptyState />
            <p>请从左侧选择一个模板</p>
          </div>
        </a-spin>
      </div>
    </div>

    <!-- 新增/编辑模板弹窗 -->
    <a-modal
      v-model:open="templateDialogVisible"
      :title="isEditTemplate ? '编辑模板' : '新增模板'"
      :width="480"
      :destroyOnClose="true"
    >
      <a-form
        ref="templateFormRef"
        :model="templateFormData"
        :rules="templateFormRules"
        layout="vertical"
        class="custom-form"
      >
        <a-form-item label="模板编码" name="code">
          <a-input v-model:value="templateFormData.code" placeholder="请输入模板编码" :disabled="isEditTemplate" />
        </a-form-item>
        <a-form-item label="模板名称" name="name">
          <a-input v-model:value="templateFormData.name" placeholder="请输入模板名称" />
        </a-form-item>
        <a-form-item label="说明">
          <a-textarea v-model:value="templateFormData.description" :rows="3" placeholder="请输入说明" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="templateDialogVisible = false">取消</a-button>
        <a-button type="primary" @click="handleSubmitTemplate" :loading="templateSubmitting">确定</a-button>
      </template>
    </a-modal>

    <!-- 新增/编辑科目项弹窗 -->
    <a-modal
      v-model:open="itemDialogVisible"
      :title="isEditItem ? '编辑科目项' : '新增科目项'"
      :width="520"
      :destroyOnClose="true"
    >
      <a-form
        ref="itemFormRef"
        :model="itemFormData"
        :rules="itemFormRules"
        layout="vertical"
        class="custom-form"
      >
        <a-form-item label="科目编码" name="code">
          <a-input v-model:value="itemFormData.code" placeholder="请输入科目编码" :disabled="isEditItem" />
        </a-form-item>
        <a-form-item label="科目名称" name="name">
          <a-input v-model:value="itemFormData.name" placeholder="请输入科目名称" />
        </a-form-item>
        <a-form-item label="上级科目">
          <a-input :value="parentItemName" disabled placeholder="无（一级科目）" />
        </a-form-item>
        <a-form-item label="类别" name="category">
          <a-select
            v-model:value="itemFormData.category"
            placeholder="请选择类别"
            :options="categoryOptions"
          />
        </a-form-item>
        <a-form-item label="余额方向" name="balanceDirection">
          <a-select
            v-model:value="itemFormData.balanceDirection"
            placeholder="请选择余额方向"
            :options="balanceDirectionOptions"
          />
        </a-form-item>
        <a-form-item label="辅助核算">
          <a-input v-model:value="itemFormData.auxiliary" placeholder="请输入辅助核算（可留空）" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="itemDialogVisible = false">取消</a-button>
        <a-button type="primary" @click="handleSubmitItem" :loading="itemSubmitting">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, DeleteOutlined, EditOutlined, RightOutlined, DownOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getAccountTemplates,
  getAccountTemplateDetail,
  createAccountTemplate,
  updateAccountTemplate,
  deleteAccountTemplate,
  getAccountTemplateItems,
  addAccountTemplateItem,
  updateAccountTemplateItem,
  deleteAccountTemplateItem,
} from '@/api/finance'

// ========== 模板列表 ==========
const templates = ref<any[]>([])
const templatesLoading = ref(false)
const selectedTemplateId = ref<number | null>(null)
const selectedTemplate = computed(() => templates.value.find(t => t.id === selectedTemplateId.value) || null)

async function loadTemplates() {
  templatesLoading.value = true
  try {
    const res = await getAccountTemplates() as any
    templates.value = res || []
    // 默认选中第一个
    if (templates.value.length > 0 && !selectedTemplateId.value) {
      handleSelectTemplate(templates.value[0])
    } else if (selectedTemplateId.value) {
      // 刷新当前选中模板的科目项
      loadItems(selectedTemplateId.value)
    }
  } catch (error) {
    console.error('加载模板列表失败:', error)
  } finally {
    templatesLoading.value = false
  }
}

function handleSelectTemplate(tpl: any) {
  selectedTemplateId.value = tpl.id
  loadItems(tpl.id)
}

// ========== 模板弹窗 ==========
const templateDialogVisible = ref(false)
const isEditTemplate = ref(false)
const templateSubmitting = ref(false)
const templateFormRef = ref<FormInstance>()
const templateFormData = reactive({
  id: 0,
  code: '',
  name: '',
  description: '',
})
const templateFormRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入模板编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入模板名称', trigger: 'blur' }],
}

function handleAddTemplate() {
  isEditTemplate.value = false
  templateFormData.id = 0
  templateFormData.code = ''
  templateFormData.name = ''
  templateFormData.description = ''
  templateDialogVisible.value = true
}

function handleDeleteTemplate(tpl: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除模板「${tpl.name}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAccountTemplate(tpl.id)
        message.success('删除成功')
        if (selectedTemplateId.value === tpl.id) {
          selectedTemplateId.value = null
          itemTree.value = []
        }
        loadTemplates()
      } catch (error) {
        message.error('删除失败')
      }
    },
  })
}

async function handleSubmitTemplate() {
  const valid = await templateFormRef.value?.validate().catch(() => false)
  if (!valid) return
  templateSubmitting.value = true
  try {
    if (isEditTemplate.value) {
      await updateAccountTemplate(templateFormData.id, { ...templateFormData })
      message.success('更新成功')
    } else {
      await createAccountTemplate({ ...templateFormData })
      message.success('创建成功')
    }
    templateDialogVisible.value = false
    loadTemplates()
  } catch (error) {
    message.error(isEditTemplate.value ? '更新失败' : '创建失败')
  } finally {
    templateSubmitting.value = false
  }
}

// ========== 科目项树 ==========
const itemTree = ref<any[]>([])
const itemsLoading = ref(false)
const expandedIds = ref<Set<number>>(new Set())

async function loadItems(templateId: number) {
  itemsLoading.value = true
  try {
    const res = await getAccountTemplateItems(templateId) as any
    itemTree.value = res || []
    // 默认展开第一级
    expandedIds.value = new Set()
    for (const node of itemTree.value) {
      if (node.children?.length > 0) {
        expandedIds.value.add(node.id)
      }
    }
  } catch (error) {
    console.error('加载科目项失败:', error)
  } finally {
    itemsLoading.value = false
  }
}

const toggleExpand = (row: any) => {
  const id = row.id
  if (expandedIds.value.has(id)) {
    expandedIds.value.delete(id)
  } else {
    expandedIds.value.add(id)
  }
  expandedIds.value = new Set(expandedIds.value)
}

const isExpanded = (row: any) => expandedIds.value.has(row.id)

const flattenItems = computed(() => {
  const result: any[] = []
  const flatten = (nodes: any[], level: number) => {
    for (const node of nodes) {
      const { children, ...rest } = node
      const flatNode = {
        ...rest,
        _level: level,
        _hasChildren: children?.length > 0,
      }
      result.push(flatNode)
      if (children?.length && expandedIds.value.has(node.id)) {
        flatten(children, level + 1)
      }
    }
  }
  flatten(itemTree.value || [], 1)
  return result
})

const itemColumns = [
  { title: '科目编码', dataIndex: 'code', key: 'code', width: 160 },
  { title: '科目名称', dataIndex: 'name', key: 'name', width: 180 },
  { title: '类别', dataIndex: 'category', key: 'category', width: 100 },
  { title: '余额方向', dataIndex: 'balanceDirection', key: 'balanceDirection', width: 90, align: 'center' as const },
  { title: '是否末级', dataIndex: 'isLeaf', key: 'isLeaf', width: 90, align: 'center' as const },
  { title: '辅助核算', dataIndex: 'auxiliary', key: 'auxiliary', width: 120 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 110, align: 'center' as const, fixed: 'right' as const },
]

// ========== 科目项弹窗 ==========
const itemDialogVisible = ref(false)
const isEditItem = ref(false)
const itemSubmitting = ref(false)
const itemFormRef = ref<FormInstance>()
const itemFormData = reactive({
  id: 0,
  code: '',
  name: '',
  parentId: null as number | null,
  category: '',
  balanceDirection: '借',
  auxiliary: '',
})
const itemFormRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入科目编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入科目名称', trigger: 'blur' }],
  category: [{ required: true, message: '请选择类别', trigger: 'change' }],
  balanceDirection: [{ required: true, message: '请选择余额方向', trigger: 'change' }],
}

const categoryOptions = [
  { label: '资产', value: '资产' },
  { label: '负债', value: '负债' },
  { label: '权益', value: '权益' },
  { label: '成本', value: '成本' },
  { label: '损益', value: '损益' },
]

const balanceDirectionOptions = [
  { label: '借', value: '借' },
  { label: '贷', value: '贷' },
]

const parentItemName = computed(() => {
  if (!itemFormData.parentId) return ''
  const find = (nodes: any[]): any => {
    for (const n of nodes) {
      if (n.id === itemFormData.parentId) return n
      if (n.children) {
        const found = find(n.children)
        if (found) return found
      }
    }
    return null
  }
  const parent = find(itemTree.value)
  return parent ? `${parent.code} ${parent.name}` : ''
})

function handleAddItem(parent: any | null) {
  isEditItem.value = false
  itemFormData.id = 0
  itemFormData.code = ''
  itemFormData.name = ''
  itemFormData.parentId = parent ? parent.id : null
  itemFormData.category = parent ? (parent.category || '') : ''
  itemFormData.balanceDirection = parent ? (parent.balanceDirection || '借') : '借'
  itemFormData.auxiliary = ''
  itemDialogVisible.value = true
}

function handleEditItem(record: any) {
  isEditItem.value = true
  itemFormData.id = record.id
  itemFormData.code = record.code
  itemFormData.name = record.name
  itemFormData.parentId = record.parentId || null
  itemFormData.category = record.category || ''
  itemFormData.balanceDirection = record.balanceDirection || '借'
  itemFormData.auxiliary = record.auxiliary || ''
  itemDialogVisible.value = true
}

function handleDeleteItem(record: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除科目项「${record.name}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAccountTemplateItem(selectedTemplateId.value!, record.id)
        message.success('删除成功')
        loadItems(selectedTemplateId.value!)
      } catch (error) {
        message.error('删除失败')
      }
    },
  })
}

async function handleSubmitItem() {
  const valid = await itemFormRef.value?.validate().catch(() => false)
  if (!valid) return
  if (!selectedTemplateId.value) return

  itemSubmitting.value = true
  try {
    const data = { ...itemFormData }
    if (isEditItem.value) {
      await updateAccountTemplateItem(selectedTemplateId.value, itemFormData.id, data)
      message.success('更新成功')
    } else {
      await addAccountTemplateItem(selectedTemplateId.value, data)
      message.success('添加成功')
    }
    itemDialogVisible.value = false
    loadItems(selectedTemplateId.value)
    // 刷新模板列表（科目数量可能变了）
    loadTemplates()
  } catch (error) {
    message.error(isEditItem.value ? '更新失败' : '添加失败')
  } finally {
    itemSubmitting.value = false
  }
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.account-template-manage {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.template-body {
  flex: 1;
  display: flex;
  gap: 0;
  min-height: 0;
  overflow: hidden;
}

// ===== 左侧面板 =====
.template-list-panel {
  width: 300px;
  min-width: 300px;
  border-right: 1px solid $border-color-lighter;
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid $border-color-lighter;
  flex-shrink: 0;
}

.panel-title {
  font-size: 14px;
  font-weight: 600;
  color: $text-primary;
}

.template-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.template-item {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 12px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 4px;

  &:hover {
    background: var(--color-primary-light);
  }

  &.active {
    background: var(--bg-muted);
    border-left: 3px solid var(--color-primary);
  }
}

.template-item-main {
  flex: 1;
  min-width: 0;
}

.template-item-name {
  font-size: 14px;
  font-weight: 500;
  color: $text-primary;
  margin-bottom: 4px;
}

.template-item-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.template-item-code {
  font-size: 12px;
  color: $text-secondary;
  font-family: 'SF Mono', Consolas, monospace;
}

.template-item-desc {
  font-size: 12px;
  color: $text-secondary;
  margin-bottom: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.template-item-count {
  font-size: 12px;
  color: $text-secondary;
}

.template-item-delete {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  cursor: pointer;
  color: $text-secondary;
  transition: all 0.2s;
  flex-shrink: 0;

  &:hover {
    background: rgba(255, 77, 79, 0.08);
    color: var(--color-danger);
  }
}

.template-list-empty {
  padding: 40px 16px;
  text-align: center;
}

// ===== 右侧面板 =====
.template-items-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;
  min-width: 0;
}

.items-empty-tip {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  color: $text-secondary;

  p {
    margin-top: 12px;
    font-size: 14px;
  }
}

// ===== 树形表格 =====
.template-items-panel {
  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    min-height: 0;
  }

  :deep(.ant-table-body) {
    overflow-y: auto !important;
  }

  :deep(.ant-table-thead > tr > th) {
    background: $bg-page;
    border-bottom: none;
    font-weight: 600;
    color: $text-primary;
    padding: 12px 16px;

    &::before {
      display: none;
    }
  }

  :deep(.tree-row) {
    height: 40px;
    transition: all 0.15s;

    &:hover {
      background: var(--color-primary-light);

      .row-actions {
        opacity: 1;
        visibility: visible;
      }
    }

    > td {
      border-bottom: 1px solid $border-color-lighter;
      padding: 0 16px;
    }
  }
}

// 编码列
.code-cell {
  display: flex;
  align-items: center;
  gap: 8px;

  .expand-placeholder {
    width: 16px;
    height: 16px;
  }

  .expand-icon {
    cursor: pointer;
    color: $text-secondary;
    font-size: 12px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    border-radius: 4px;
    transition: all 0.15s;

    &:hover {
      color: var(--color-primary);
      background: var(--color-primary-light);
    }
  }

  .code-text {
    font-size: 14px;
    color: $text-primary;
    font-family: 'SF Mono', Consolas, monospace;
  }
}

// 行快捷操作
.row-actions {
  display: flex;
  gap: 4px;
  opacity: 0;
  visibility: hidden;
  transition: all 0.15s;

  .action-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border-radius: 6px;
    cursor: pointer;
    color: $text-secondary;
    transition: all 0.15s;

    &:hover {
      background: var(--color-primary-light);
      color: var(--color-primary);
    }

    &.danger:hover {
      background: var(--color-danger-light);
      color: var(--color-danger);
    }
  }
}

// ===== 弹窗表单 =====
.custom-form {
  :deep(.ant-form-item) {
    margin-bottom: 20px;

    .ant-form-item-label > label {
      color: $text-primary;
      font-weight: 500;
    }
  }

  :deep(.ant-input),
  :deep(.ant-select-selector) {
    height: 40px;
    border-radius: 8px;
  }

  :deep(.ant-input) {
    padding: 0 12px;
  }

  :deep(.ant-select-selector) {
    padding: 0 12px !important;
  }
}
</style>
