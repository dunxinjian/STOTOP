<template>
  <div class="issue-type-config">
    <PageHeader title="问题类型配置">
      <template #actions>
        <a-button v-if="has(CardFlowPermissions.DispatchRuleManage)" type="primary" size="small" @click="openEditDialog()">
          <PlusOutlined /> 新增自定义类型
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; gap: 8px;">
          <a-select
            v-model:value="query.category"
            placeholder="分类"
            allowClear
            size="small"
            style="width: 140px;"
          >
            <a-select-option v-for="c in categoryOptions" :key="c" :value="c">{{ c }}</a-select-option>
          </a-select>
          <a-select
            v-model:value="query.sourceAgent"
            placeholder="来源Agent"
            allowClear
            size="small"
            style="width: 160px;"
          >
            <a-select-option v-for="a in agentOptions" :key="a" :value="a">{{ a }}</a-select-option>
          </a-select>
          <a-select
            v-model:value="query.status"
            placeholder="状态"
            allowClear
            size="small"
            style="width: 110px;"
          >
            <a-select-option :value="1">启用</a-select-option>
            <a-select-option :value="0">禁用</a-select-option>
          </a-select>
          <a-button type="primary" size="small" @click="onSearch">查询</a-button>
          <a-button size="small" @click="onReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-card class="table-card">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        row-key="id"
        bordered
        size="middle"
        :pagination="pagination"
        :scroll="{ x: 1200 }"
        :row-class-name="rowClassName"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'isBuiltIn'">
            <a-tag :color="record.isBuiltIn ? 'blue' : 'green'">
              {{ record.isBuiltIn ? '内置' : '自定义' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'severityLevel'">
            <a-tag :color="record.severityLevel === 'Error' ? 'red' : 'orange'">
              {{ record.severityLevel === 'Error' ? '错误' : '警告' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'dispatchMode'">
            <template v-if="record.dispatchMode">
              <a-tag :color="dispatchModeColor(record.dispatchMode)">{{ dispatchModeLabel(record.dispatchMode) }}</a-tag>
            </template>
            <template v-else>
              <a-tag color="red">未配置</a-tag>
            </template>
          </template>
          <template v-else-if="column.dataIndex === 'dispatchTarget'">
            <span v-if="record.dispatchTarget">{{ formatDispatchTarget(record.dispatchMode, record.dispatchTarget) }}</span>
            <span v-else style="color: #c0c4cc;">-</span>
          </template>
          <template v-else-if="column.dataIndex === 'timeoutHours'">
            <span v-if="record.timeoutHours != null">{{ record.timeoutHours }}h</span>
            <span v-else style="color: #c0c4cc;">-</span>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-switch
              v-if="has(CardFlowPermissions.DispatchRuleManage)"
              :checked="record.status === 1"
              size="small"
              @change="(val: string | number | boolean) => handleToggleEnabled(record as IssueTypeDto, val as boolean)"
            />
            <a-tag v-else :color="record.status === 1 ? 'green' : 'default'">
              {{ record.status === 1 ? '启用' : '禁用' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button
              v-if="has(CardFlowPermissions.DispatchRuleManage)"
              type="link"
              size="small"
              @click="openEditDialog(record as IssueTypeDto)"
            >编辑</a-button>
            <a-button
              v-if="has(CardFlowPermissions.DispatchRuleManage) && !record.isBuiltIn"
              type="link"
              size="small"
              danger
              @click="handleDelete(record as IssueTypeDto)"
            >删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? (editingRecord?.isBuiltIn ? '编辑内置类型' : '编辑自定义类型') : '新增自定义类型'"
      :confirm-loading="submitting"
      width="640px"
      :destroy-on-close="true"
      :bodyStyle="{ maxHeight: 'calc(80vh - 110px)', overflowY: 'auto' }"
      @ok="handleSubmit"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        layout="vertical"
      >
        <!-- ===== 基本信息 ===== -->
        <a-form-item label="编码" name="code">
          <a-input
            v-model:value="formData.code"
            placeholder="请输入编码（如 ERR_CUSTOM_01）"
            :disabled="isEdit && editingRecord?.isBuiltIn"
          />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input
            v-model:value="formData.name"
            placeholder="请输入类型名称"
            :disabled="isEdit && editingRecord?.isBuiltIn"
          />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="formData.description" placeholder="问题类型描述" :rows="2" :disabled="isEdit && editingRecord?.isBuiltIn" />
        </a-form-item>
        <a-form-item label="所属模块" name="module">
          <a-select v-model:value="formData.module" placeholder="请选择模块" :disabled="isEdit && editingRecord?.isBuiltIn">
            <a-select-option value="Express">快递</a-select-option>
            <a-select-option value="Finance">财务</a-select-option>
            <a-select-option value="Quality">质量</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="来源Agent" name="sourceAgent">
          <a-input
            v-model:value="formData.sourceAgent"
            placeholder="来源Agent标识"
            :disabled="isEdit && editingRecord?.isBuiltIn"
          />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-input
            v-model:value="formData.category"
            placeholder="请输入分类"
            :disabled="isEdit && editingRecord?.isBuiltIn"
          />
        </a-form-item>
        <a-form-item label="严重级别" name="severityLevel">
          <a-select v-model:value="formData.severityLevel" placeholder="请选择严重级别" :disabled="isEdit && editingRecord?.isBuiltIn">
            <a-select-option value="Error">错误（Error）</a-select-option>
            <a-select-option value="Warning">警告（Warning）</a-select-option>
          </a-select>
        </a-form-item>

        <!-- ===== 派发配置 ===== -->
        <a-divider orientation="left" style="margin: 12px 0 8px;">派发配置</a-divider>
        <a-form-item label="派发模式" name="dispatchMode">
          <a-select
            v-model:value="formData.dispatchMode"
            placeholder="请选择派发模式"
            allowClear
          >
            <a-select-option value="Role">按角色派发</a-select-option>
            <a-select-option value="Assignee">指定人派发</a-select-option>
            <a-select-option value="None">不派发</a-select-option>
          </a-select>
        </a-form-item>

        <!-- 派发目标：根据派发模式动态显示 -->
        <a-form-item v-if="formData.dispatchMode === 'Role'" label="派发目标（角色）" name="dispatchTargetRoles">
          <a-select
            v-model:value="dispatchTargetRoles"
            mode="tags"
            placeholder="输入角色编码后回车添加（如 DataOperator）"
            style="width: 100%;"
          />
          <div style="margin-top: 4px; color: #8c8c8c; font-size: 12px;">
            可输入多个角色编码，回车确认每个角色
          </div>
        </a-form-item>
        <a-form-item v-else-if="formData.dispatchMode === 'Assignee'" label="派发目标（用户）" name="dispatchTargetAssignee">
          <a-space direction="vertical" style="width: 100%;">
            <a-input v-model:value="dispatchTargetAssigneeId" placeholder="用户ID" type="number" />
            <a-input v-model:value="dispatchTargetAssigneeName" placeholder="用户名称" />
          </a-space>
        </a-form-item>

        <a-form-item label="超时时效（小时）" name="timeoutHours">
          <a-input-number
            v-model:value="formData.timeoutHours"
            :min="1"
            :max="720"
            placeholder="超时小时数"
            style="width: 100%;"
          />
        </a-form-item>
        <a-form-item label="组织隔离" name="orgScoped">
          <a-switch v-model:checked="formData.orgScoped" checkedChildren="是" unCheckedChildren="否" />
        </a-form-item>

        <!-- ===== 高级配置 ===== -->
        <a-divider orientation="left" style="margin: 12px 0 8px;">高级配置</a-divider>
        <a-form-item label="建议修复" name="suggestedFix">
          <a-textarea v-model:value="formData.suggestedFix" placeholder="建议修复方案" :rows="2" :disabled="isEdit && editingRecord?.isBuiltIn" />
        </a-form-item>
        <a-form-item label="详情路由" name="detailRoute">
          <a-input v-model:value="formData.detailRoute" placeholder="如 /datacenter/quality/xxx" :disabled="isEdit && editingRecord?.isBuiltIn" />
        </a-form-item>
        <a-form-item label="启用状态" name="status">
          <a-switch v-model:checked="formData.enabled" checkedChildren="启用" unCheckedChildren="禁用" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance, TablePaginationConfig } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getIssueTypes,
  createIssueType,
  updateIssueType,
  deleteIssueType,
  type IssueTypeDto,
  type IssueTypeQueryRequest,
  type IssueTypeCreateRequest,
  type IssueTypeUpdateRequest,
} from '@/api/qualityCenter'
import { usePermission, CardFlowPermissions } from '@/utils/permission'

const { has } = usePermission()

// ==================== 筛选条件 ====================
// 本地收窄 status 为 number（去掉共享 DTO 的 null），以匹配 AntD SelectValue
const query = reactive<Omit<IssueTypeQueryRequest, 'status'> & { status?: number }>({
  pageIndex: 1,
  pageSize: 20,
  category: undefined,
  sourceAgent: undefined,
  status: undefined,
})

// 选项数据（从列表中动态提取）
const categoryOptions = ref<string[]>([])
const agentOptions = ref<string[]>([])

// ==================== 表格 ====================
const loading = ref(false)
const tableData = ref<IssueTypeDto[]>([])
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const tableColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 180, ellipsis: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: 160, ellipsis: true },
  { title: '来源Agent', dataIndex: 'sourceAgent', key: 'sourceAgent', width: 140, ellipsis: true },
  { title: '分类', dataIndex: 'category', key: 'category', width: 110 },
  { title: '级别', dataIndex: 'severityLevel', key: 'severityLevel', width: 80, align: 'center' as const },
  { title: '内置', dataIndex: 'isBuiltIn', key: 'isBuiltIn', width: 80, align: 'center' as const },
  { title: '派发模式', dataIndex: 'dispatchMode', key: 'dispatchMode', width: 110, align: 'center' as const },
  { title: '派发目标', dataIndex: 'dispatchTarget', key: 'dispatchTarget', width: 160, ellipsis: true },
  { title: '超时', dataIndex: 'timeoutHours', key: 'timeoutHours', width: 70, align: 'center' as const },
  { title: '启用', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 130, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const params: IssueTypeQueryRequest = {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
    }
    if (query.category) params.category = query.category
    if (query.sourceAgent) params.sourceAgent = query.sourceAgent
    if (query.status != null) params.status = query.status
    const res: any = await getIssueTypes(params)
    if (Array.isArray(res)) {
      tableData.value = res
      pagination.total = res.length
    } else {
      tableData.value = res?.items ?? res?.list ?? []
      pagination.total = res?.total ?? res?.totalCount ?? 0
    }
    // 提取筛选选项
    extractFilterOptions(tableData.value)
  } catch (e: any) {
    message.error('加载问题类型失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function extractFilterOptions(data: IssueTypeDto[]) {
  const cats = new Set<string>()
  const agents = new Set<string>()
  data.forEach(item => {
    if (item.category) cats.add(item.category)
    if (item.sourceAgent) agents.add(item.sourceAgent)
  })
  // 合并已有选项（避免翻页后丢失）
  categoryOptions.value = [...new Set([...categoryOptions.value, ...cats])]
  agentOptions.value = [...new Set([...agentOptions.value, ...agents])]
}

function onSearch() {
  pagination.current = 1
  fetchList()
}

function onReset() {
  query.category = undefined
  query.sourceAgent = undefined
  query.status = undefined
  onSearch()
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  fetchList()
}

function rowClassName(record: IssueTypeDto) {
  return record.dispatchMode == null ? 'row-unconfigured' : ''
}

// ==================== 编辑弹窗 ====================
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingRecord = ref<IssueTypeDto | null>(null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const formData = reactive({
  code: '',
  name: '',
  description: '',
  module: 'Express',
  sourceAgent: '',
  category: '',
  severityLevel: 'Warning',
  dispatchMode: undefined as string | undefined,
  dispatchTarget: null as string | null,
  timeoutHours: undefined as number | undefined,
  suggestedFix: '',
  detailRoute: '',
  orgScoped: false,
  enabled: true, // maps to status: 1=enabled, 0=disabled
})

// 派发目标响应式变量
const dispatchTargetRoles = ref<string[]>([])
const dispatchTargetAssigneeId = ref<string>('')
const dispatchTargetAssigneeName = ref<string>('')

const formRules: Record<string, Rule[]> = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
}

function parseDispatchTarget(mode: string | null | undefined, target: string | null | undefined) {
  dispatchTargetRoles.value = []
  dispatchTargetAssigneeId.value = ''
  dispatchTargetAssigneeName.value = ''
  if (!target) return
  try {
    const parsed = JSON.parse(target)
    if (mode === 'Role') {
      dispatchTargetRoles.value = parsed.roles || (parsed.roleCode ? [parsed.roleCode] : [])
    } else if (mode === 'Assignee') {
      dispatchTargetAssigneeId.value = String(parsed.assigneeId || '')
      dispatchTargetAssigneeName.value = parsed.assigneeName || ''
    }
  } catch { /* 忽略解析失败 */ }
}

function buildDispatchTarget(): string | null {
  if (formData.dispatchMode === 'Role' && dispatchTargetRoles.value.length > 0) {
    return JSON.stringify({ roles: dispatchTargetRoles.value })
  }
  if (formData.dispatchMode === 'Assignee' && dispatchTargetAssigneeId.value) {
    return JSON.stringify({
      assigneeId: Number(dispatchTargetAssigneeId.value),
      assigneeName: dispatchTargetAssigneeName.value,
    })
  }
  return null
}

function openEditDialog(record?: IssueTypeDto) {
  if (record) {
    isEdit.value = true
    editingRecord.value = record
    Object.assign(formData, {
      code: record.code,
      name: record.name,
      description: record.description || '',
      module: record.module || 'Express',
      sourceAgent: record.sourceAgent || '',
      category: record.category || '',
      severityLevel: record.severityLevel || 'Warning',
      dispatchMode: record.dispatchMode || undefined,
      dispatchTarget: record.dispatchTarget || null,
      timeoutHours: record.timeoutHours ?? undefined,
      suggestedFix: record.suggestedFix || '',
      detailRoute: record.detailRoute || '',
      orgScoped: record.orgScoped ?? false,
      enabled: record.status === 1,
    })
    parseDispatchTarget(record.dispatchMode, record.dispatchTarget)
  } else {
    isEdit.value = false
    editingRecord.value = null
    Object.assign(formData, {
      code: '',
      name: '',
      description: '',
      module: 'Express',
      sourceAgent: '',
      category: '',
      severityLevel: 'Warning',
      dispatchMode: undefined,
      dispatchTarget: null,
      timeoutHours: undefined,
      suggestedFix: '',
      detailRoute: '',
      orgScoped: false,
      enabled: true,
    })
    parseDispatchTarget(null, null)
  }
  dialogVisible.value = true
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const dispatchTarget = formData.dispatchMode === 'None' ? null : buildDispatchTarget()
    if (isEdit.value && editingRecord.value) {
      const payload: IssueTypeUpdateRequest = {
        dispatchMode: formData.dispatchMode,
        dispatchTarget: dispatchTarget,
        timeoutHours: formData.timeoutHours ?? undefined,
        orgScoped: formData.orgScoped,
        status: formData.enabled ? 1 : 0,
        severityLevel: formData.severityLevel,
        suggestedFix: formData.suggestedFix,
        detailRoute: formData.detailRoute,
      }
      // 自定义类型可编辑所有字段
      if (!editingRecord.value.isBuiltIn) {
        payload.name = formData.name
        payload.description = formData.description
        payload.module = formData.module
        payload.sourceAgent = formData.sourceAgent
        payload.category = formData.category
      }
      await updateIssueType(editingRecord.value.id, payload)
      message.success('更新成功')
    } else {
      await createIssueType({
        code: formData.code,
        name: formData.name,
        description: formData.description || undefined,
        module: formData.module || undefined,
        sourceAgent: formData.sourceAgent || undefined,
        category: formData.category || undefined,
        severityLevel: formData.severityLevel || undefined,
        dispatchMode: formData.dispatchMode,
        dispatchTarget: dispatchTarget,
        timeoutHours: formData.timeoutHours ?? undefined,
        suggestedFix: formData.suggestedFix || undefined,
        detailRoute: formData.detailRoute || undefined,
        orgScoped: formData.orgScoped,
        status: formData.enabled ? 1 : 0,
      })
      message.success('创建成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch (e: any) {
    message.error((isEdit.value ? '更新' : '创建') + '失败：' + (e?.message || '未知错误'))
  } finally {
    submitting.value = false
  }
}

// ==================== 启用/禁用快捷切换 ====================
async function handleToggleEnabled(record: IssueTypeDto, enabled: boolean) {
  try {
    await updateIssueType(record.id, { status: enabled ? 1 : 0 })
    record.status = enabled ? 1 : 0
    message.success(enabled ? '已启用' : '已禁用')
  } catch (e: any) {
    message.error('操作失败：' + (e?.message || '未知错误'))
  }
}

// ==================== 删除 ====================
function handleDelete(record: IssueTypeDto) {
  Modal.confirm({
    title: '删除确认',
    content: `确认删除自定义类型「${record.name}」？此操作不可恢复。`,
    okText: '删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      try {
        await deleteIssueType(record.id)
        message.success('删除成功')
        fetchList()
      } catch (e: any) {
        message.error('删除失败：' + (e?.message || '未知错误'))
      }
    },
  })
}

// ==================== 辅助函数 ====================
function dispatchModeLabel(mode: string) {
  const map: Record<string, string> = {
    Role: '按角色',
    Assignee: '指定人',
    None: '不派发',
  }
  return map[mode] ?? mode
}

function dispatchModeColor(mode: string) {
  const map: Record<string, string> = {
    Role: 'blue',
    Assignee: 'purple',
    None: 'default',
  }
  return map[mode] ?? 'default'
}

function formatDispatchTarget(mode: string | null | undefined, target: string | null | undefined): string {
  if (!target) return '-'
  try {
    const parsed = JSON.parse(target)
    if (mode === 'Role') {
      const roles = parsed.roles || [parsed.roleCode || parsed.RoleCode]
      return '角色: ' + roles.filter(Boolean).join(', ')
    }
    if (mode === 'Assignee') {
      return '用户: ' + (parsed.assigneeName || parsed.AssigneeName || `ID:${parsed.assigneeId}`)
    }
  } catch {
    // 如果不是JSON，直接返回原文本
  }
  return target
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchList()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.issue-type-config {
  padding: $page-padding;
}

.table-card :deep(.ant-card-body) {
  padding: 0;
}

:deep(.row-unconfigured) {
  background-color: var(--color-warning-light) !important;
}

:deep(.row-unconfigured:hover > td) {
  background-color: #fff1b8 !important;
}
</style>
