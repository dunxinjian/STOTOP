<template>
  <div class="quality-rules">
    <PageHeader title="质量规则配置">
      <template #actions>
        <a-button type="primary" size="small" @click="openEditor()"><PlusOutlined /> 新建规则</a-button>
      </template>
      <template #toolbar>
        <a-select
          v-model:value="filterPipelineId"
          placeholder="管道筛选"
          allowClear
          showSearch
          :filterOption="filterOption"
          size="small"
          style="width: 180px;"
        >
          <a-select-option v-for="p in pipelines" :key="p.id" :value="p.id">{{ p.name }}</a-select-option>
        </a-select>
        <a-select
          v-model:value="filterTargetTable"
          placeholder="目标表筛选"
          allowClear
          showSearch
          :filterOption="filterOption"
          size="small"
          style="width: 180px;"
        >
          <a-select-option v-for="t in stagingTables" :key="t.tableName" :value="t.tableName">{{ t.tableName }}</a-select-option>
        </a-select>
        <a-radio-group v-model:value="filterEnabled" button-style="solid" size="small">
          <a-radio-button value="">全部</a-radio-button>
          <a-radio-button value="true">启用</a-radio-button>
          <a-radio-button value="false">禁用</a-radio-button>
        </a-radio-group>
        <a-button type="primary" size="small" @click="handleSearch"><SearchOutlined /> 搜索</a-button>
        <a-button size="small" @click="handleReset">重置</a-button>
      </template>
    </PageHeader>

    <!-- 列表表格 -->
    <a-card class="table-card">
      <a-table
        :columns="tableColumns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        size="middle"
        :pagination="false"
        :scroll="{ x: 1200 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'ruleLevel'">
            <a-tag :color="ruleLevelColor(record.ruleLevel)">{{ ruleLevelLabel(record.ruleLevel) }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'checkType'">
            <a-tag>{{ checkTypeLabel(record.checkType) }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'severityLevel'">
            <a-tag :color="record.severityLevel === 'Error' ? 'red' : 'orange'">
              {{ record.severityLevel === 'Error' ? '错误' : '警告' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'isBlocking'">
            <a-tag :color="record.isBlocking ? 'red' : 'default'">
              {{ record.isBlocking ? '阻断' : '放行' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'isEnabled'">
            <a-switch
              :checked="record.isEnabled"
              checkedChildren="启用"
              unCheckedChildren="禁用"
              @change="(val: string | number | boolean) => handleToggle(record as QualityRuleDto, val as boolean)"
            />
          </template>
          <template v-else-if="column.dataIndex === 'pipelineName'">
            <span>{{ record.pipelineName || '-' }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'targetTable'">
            <a-tag v-if="record.targetTable" style="margin: 0;">{{ record.targetTable }}</a-tag>
            <span v-else style="color: #c0c4cc;">-</span>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="openEditor(record as QualityRuleDto)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record as QualityRuleDto)">删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 编辑弹窗 -->
    <QualityRuleEditor
      v-model:open="editorVisible"
      :rule="editingRule"
      @saved="onEditorSaved"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined, SearchOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import QualityRuleEditor from './QualityRuleEditor.vue'
import {
  getQualityRules,
  deleteQualityRule,
  updateQualityRule,
} from '@/api/qualityRule'
import type { QualityRuleDto } from '@/api/qualityRule'
import { getPipelines, getStagingTables } from '@/api/cardflow'
import type { PipelineDto, StagingTableInfo } from '@/api/cardflow'

// ==================== 筛选 ====================
const filterPipelineId = ref<number | undefined>(undefined)
const filterTargetTable = ref<string | undefined>(undefined)
const filterEnabled = ref('')

// ==================== 下拉选项 ====================
const pipelines = ref<PipelineDto[]>([])
const stagingTables = ref<StagingTableInfo[]>([])

async function fetchPipelines() {
  try {
    const res: any = await getPipelines()
    pipelines.value = res.data ?? res ?? []
  } catch { /* silent */ }
}

async function fetchStagingTables() {
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res.data ?? res ?? []
  } catch { /* silent */ }
}

function filterOption(input: string, option: any) {
  const label = option?.children?.[0]?.children ?? option?.label ?? ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}

// ==================== 列表 ====================
const loading = ref(false)
const tableData = ref<QualityRuleDto[]>([])

const tableColumns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', ellipsis: true, width: 160 },
  { title: '规则编码', dataIndex: 'ruleCode', key: 'ruleCode', ellipsis: true, width: 140 },
  { title: '关联管道', dataIndex: 'pipelineName', key: 'pipelineName', width: 130 },
  { title: '目标表', dataIndex: 'targetTable', key: 'targetTable', width: 160 },
  { title: '规则级别', dataIndex: 'ruleLevel', key: 'ruleLevel', width: 100, align: 'center' as const },
  { title: '检查类型', dataIndex: 'checkType', key: 'checkType', width: 120, align: 'center' as const },
  { title: '目标字段', dataIndex: 'targetField', key: 'targetField', width: 110, ellipsis: true },
  { title: '严重级别', dataIndex: 'severityLevel', key: 'severityLevel', width: 90, align: 'center' as const },
  { title: '阻断', dataIndex: 'isBlocking', key: 'isBlocking', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const params: any = {}
    if (filterPipelineId.value != null) params.pipelineId = filterPipelineId.value
    if (filterTargetTable.value) params.targetTable = filterTargetTable.value
    if (filterEnabled.value !== '') params.isEnabled = filterEnabled.value === 'true'
    const res: any = await getQualityRules(params)
    const data = res.data ?? res
    if (Array.isArray(data)) {
      tableData.value = data
    } else {
      tableData.value = data.items ?? data.list ?? []
    }
  } catch {
    message.error('获取质量规则列表失败')
  } finally {
    loading.value = false
  }
}

// ==================== 启用/禁用 ====================
async function handleToggle(record: QualityRuleDto, val: boolean) {
  try {
    await updateQualityRule(record.id, { isEnabled: val } as any)
    record.isEnabled = val
    message.success(val ? '已启用' : '已禁用')
  } catch {
    message.error('操作失败')
  }
}

// ==================== 删除 ====================
function handleDelete(record: QualityRuleDto) {
  Modal.confirm({
    title: '删除确认',
    content: `确认删除规则「${record.ruleName}」？此操作不可恢复。`,
    okText: '删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      await deleteQualityRule(record.id)
      message.success('删除成功')
      fetchList()
    },
  })
}

// ==================== 编辑弹窗 ====================
const editorVisible = ref(false)
const editingRule = ref<QualityRuleDto | null>(null)

function openEditor(rule?: QualityRuleDto) {
  editingRule.value = rule ?? null
  editorVisible.value = true
}

function onEditorSaved() {
  fetchList()
}

// ==================== 搜索/重置 ====================
function handleSearch() {
  fetchList()
}

function handleReset() {
  filterPipelineId.value = undefined
  filterTargetTable.value = undefined
  filterEnabled.value = ''
  fetchList()
}

// ==================== 辅助函数 ====================
function ruleLevelLabel(level: string) {
  const map: Record<string, string> = { Field: '字段级', Row: '行级', Batch: '批次级' }
  return map[level] ?? level
}

function ruleLevelColor(level: string) {
  const map: Record<string, string> = { Field: 'blue', Row: 'green', Batch: 'purple' }
  return map[level] ?? 'default'
}

function checkTypeLabel(type: string) {
  const map: Record<string, string> = {
    NotNull: '非空', Format: '格式', Range: '范围',
    Expression: '表达式', SqlCondition: 'SQL条件',
  }
  return map[type] ?? type
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchList()
  fetchPipelines()
  fetchStagingTables()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.quality-rules {
  padding: $page-padding;
}

.table-card :deep(.ant-card-body) {
  padding: 0;
}
</style>
