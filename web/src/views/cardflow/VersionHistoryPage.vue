<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  ArrowLeftOutlined,
  EyeOutlined,
  RollbackOutlined,
  DiffOutlined,
  DeleteOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getFlowDefinition,
  getFlowVersions,
  getFlowVersionDetail,
  saveFlowDraftVersion,
  publishFlowDefinition,
} from '@/api/cardflow'
import type {
  FlowVersionDto,
  FlowVersionDetailDto,
  SchemaFieldDefinition,
  StageDefinitionDto,
  StageDefinitionRequest,
  StageRouteRuleRequest,
  StageRouteRuleDto,
  DynamicStagePolicyRequest,
  DynamicStagePolicyDto,
} from '@/types/cardflow'
import { parseCardSchemaFields } from '@/utils/cardflowSchema'

const route = useRoute()
const router = useRouter()

const flowId = computed(() => Number(route.params.id || 0))
const flowName = ref('')
const loading = ref(false)
const dataSource = ref<FlowVersionDto[]>([])
const selectedRowKeys = ref<number[]>([])

const selectedVersion = computed<FlowVersionDto | undefined>(
  () => dataSource.value.find(v => v.id === selectedRowKeys.value[0]),
)

// ---- 详情 Drawer ----
const detailVisible = ref(false)
const detailLoading = ref(false)
const versionDetail = ref<FlowVersionDetailDto | null>(null)

// ---- 对比 Drawer ----
const diffVisible = ref(false)
const diffLoading = ref(false)
const leftDetail = ref<FlowVersionDetailDto | null>(null)
const rightDetail = ref<FlowVersionDetailDto | null>(null)

const statusConfig: Record<string, { text: string; color: string }> = {
  draft: { text: '草稿', color: 'default' },
  published: { text: '已发布', color: 'success' },
  archived: { text: '已归档', color: 'warning' },
}

const columns: TableColumnsType = [
  { title: '版本号', dataIndex: 'versionNumber', key: 'versionNumber', width: 100, align: 'center' },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' },
  { title: '当前发布', dataIndex: 'isCurrentVersion', key: 'isCurrentVersion', width: 100, align: 'center' },
  { title: '创建人', dataIndex: 'createdBy', key: 'createdBy', width: 140 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 180 },
  { title: '发布时间', dataIndex: 'publishTime', key: 'publishTime', width: 180 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' },
]

// ============== 数据加载 ==============
async function loadFlow() {
  if (!flowId.value) return
  try {
    const res: any = await getFlowDefinition(flowId.value)
    flowName.value = res?.flowName || '流程'
  } catch {
    flowName.value = '流程'
  }
}

async function loadVersions() {
  if (!flowId.value) return
  loading.value = true
  try {
    const res = await getFlowVersions(flowId.value)
    const list = ((res as FlowVersionDto[]) || []).slice()
    list.sort((a, b) => b.versionNumber - a.versionNumber)
    dataSource.value = list
  } catch {
    message.error('加载版本列表失败')
  } finally {
    loading.value = false
  }
}

// ============== 行交互 ==============
function rowClassName(record: FlowVersionDto) {
  return record.isCurrentVersion ? 'row-current' : ''
}

function customRow(record: FlowVersionDto) {
  return {
    onClick: () => {
      selectedRowKeys.value = [record.id]
    },
  }
}

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys.map(k => Number(k))
}

// ============== 查看详情 ==============
async function handleViewDetail(record: FlowVersionDto) {
  detailLoading.value = true
  detailVisible.value = true
  versionDetail.value = null
  try {
    const res = await getFlowVersionDetail(flowId.value, record.id)
    versionDetail.value = res as FlowVersionDetailDto
  } catch {
    message.error('加载版本详情失败')
  } finally {
    detailLoading.value = false
  }
}

// ============== 对比 ==============
async function handleCompare(record: FlowVersionDto) {
  // 默认对比选中版本与版本号小一位的版本
  const list = dataSource.value
  const target = list.find(v => v.versionNumber === record.versionNumber - 1)
  if (!target) {
    message.info('没有可用于对比的前一版本')
    return
  }
  diffVisible.value = true
  diffLoading.value = true
  leftDetail.value = null
  rightDetail.value = null
  try {
    const [left, right] = await Promise.all([
      getFlowVersionDetail(flowId.value, target.id),
      getFlowVersionDetail(flowId.value, record.id),
    ])
    leftDetail.value = left as FlowVersionDetailDto
    rightDetail.value = right as FlowVersionDetailDto
  } catch {
    message.error('加载版本对比数据失败')
  } finally {
    diffLoading.value = false
  }
}

// ============== 回滚 ==============
function handleRollback(record: FlowVersionDto) {
  Modal.confirm({
    title: '确认回滚',
    content: `确认将 V${record.versionNumber} 内容复制为新版本并发布？在途卡片不受影响。`,
    okText: '确认回滚',
    cancelText: '取消',
    async onOk() {
      try {
        const detail = (await getFlowVersionDetail(flowId.value, record.id)) as FlowVersionDetailDto
        const stages: StageDefinitionRequest[] = (detail.stages || []).map((s: StageDefinitionDto) => ({
          name: s.stageName,
          type: s.type,
          sortOrder: s.sortOrder,
          approvalMode: s.approvalMode,
          assigneeStrategy: s.assigneeStrategy,
          assigneeConfigJson: s.assigneeConfigJson,
          conditionJson: s.conditionJson,
          inputFieldsJson: s.inputFieldsJson,
          // 废除 autoPluginName / autoPluginConfigJson，改为插件注册+规则引用
          pluginRegistryId: s.pluginRegistryId ?? null,
          pluginRuleId: s.pluginRuleId ?? null,
          failurePolicyJson: s.failurePolicyJson,
          ccConfigJson: s.ccConfigJson,
          timeoutHours: s.timeoutHours,
          priorityTemplate: s.priorityTemplate,
        }))
        const routes: StageRouteRuleRequest[] = (detail.routes || []).map((r: StageRouteRuleDto) => ({
          edgeKey: r.edgeKey,
          fromStageKey: r.fromStageKey,
          toStageKey: r.toStageKey,
          routeName: r.routeName,
          conditionJson: r.conditionJson,
          priority: r.priority,
          isDefault: r.isDefault,
          status: r.status,
          failurePolicyJson: r.failurePolicyJson,
        }))
        const dynamicPolicies: DynamicStagePolicyRequest[] = (detail.dynamicPolicies || []).map((p: DynamicStagePolicyDto) => ({
          policyKey: p.policyKey,
          sourceStageKey: p.sourceStageKey,
          policyName: p.policyName,
          strategyType: p.strategyType,
          strategyConfigJson: p.strategyConfigJson,
          conditionJson: p.conditionJson,
          triggerTiming: p.triggerTiming,
          insertPosition: p.insertPosition,
          continuationStageKey: p.continuationStageKey,
          priority: p.priority,
          maxInsertCount: p.maxInsertCount,
          fallbackJson: p.fallbackJson,
          status: p.status,
        }))
        await saveFlowDraftVersion(flowId.value, {
          cardSchemaJson: detail.cardSchemaJson,
          detailSchemaJson: detail.detailSchemaJson,
          flowSettingsJson: detail.flowSettingsJson,
          stages,
          routes,
          dynamicPolicies,
        })
        await publishFlowDefinition(flowId.value)
        message.success(`已回滚到 V${record.versionNumber} 并发布为新版本`)
        selectedRowKeys.value = []
        loadVersions()
      } catch {
        message.error('回滚失败')
      }
    },
  })
}

function handleRollbackSelected() {
  if (!selectedVersion.value) {
    message.info('请先选中一个版本')
    return
  }
  if (selectedVersion.value.status !== 'published') {
    message.warning('仅已发布的版本可被回滚')
    return
  }
  if (selectedVersion.value.isCurrentVersion) {
    message.info('该版本即为当前发布版本，无需回滚')
    return
  }
  handleRollback(selectedVersion.value)
}

function handleDeleteDraft(record: FlowVersionDto) {
  Modal.confirm({
    title: '删除草稿版本',
    content: `确认删除草稿 V${record.versionNumber}？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk() {
      // 草稿版本由后端编辑器自动管理，未提供独立删除接口
      message.info('草稿版本由编辑器统一管理，请进入流程编辑页清空草稿后保存')
    },
  })
}

// ============== 工具方法 ==============
function formatTime(val: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

function formatJson(jsonStr: string | null) {
  if (!jsonStr) return '(空)'
  try {
    return JSON.stringify(JSON.parse(jsonStr), null, 2)
  } catch {
    return jsonStr
  }
}

function safeParseFields(json: string | null | undefined): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

function safeParseObject(json: string | null | undefined): Record<string, any> {
  if (!json) return {}
  try {
    const v = JSON.parse(json)
    return v && typeof v === 'object' && !Array.isArray(v) ? v : {}
  } catch {
    return {}
  }
}

function isEqual(a: any, b: any): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

function diffFieldChanges(a: SchemaFieldDefinition, b: SchemaFieldDefinition): string[] {
  const keys = new Set<string>([...Object.keys(a || {}), ...Object.keys(b || {})])
  const changes: string[] = []
  for (const k of keys) {
    const av = (a as any)?.[k]
    const bv = (b as any)?.[k]
    if (JSON.stringify(av) !== JSON.stringify(bv)) {
      changes.push(`${k}: ${JSON.stringify(av) ?? '∅'} → ${JSON.stringify(bv) ?? '∅'}`)
    }
  }
  return changes
}

type DiffStatus = 'unchanged' | 'added' | 'removed' | 'modified'

interface FieldDiffRow { field: SchemaFieldDefinition; status: DiffStatus; changes: string[] }
interface StageDiffRow { stage: StageDefinitionDto; status: DiffStatus; changes: string[] }

const fieldDiff = computed<{ left: FieldDiffRow[]; right: FieldDiffRow[] }>(() => {
  if (!leftDetail.value || !rightDetail.value) return { left: [], right: [] }
  const leftFields = safeParseFields(leftDetail.value.cardSchemaJson)
  const rightFields = safeParseFields(rightDetail.value.cardSchemaJson)
  const leftMap = new Map(leftFields.map(f => [f.key, f]))
  const rightMap = new Map(rightFields.map(f => [f.key, f]))
  const left: FieldDiffRow[] = leftFields.map(f => {
    if (!rightMap.has(f.key)) return { field: f, status: 'removed', changes: [] }
    const rb = rightMap.get(f.key)!
    if (!isEqual(f, rb)) return { field: f, status: 'modified', changes: diffFieldChanges(f, rb) }
    return { field: f, status: 'unchanged', changes: [] }
  })
  const right: FieldDiffRow[] = rightFields.map(f => {
    if (!leftMap.has(f.key)) return { field: f, status: 'added', changes: [] }
    const lb = leftMap.get(f.key)!
    if (!isEqual(lb, f)) return { field: f, status: 'modified', changes: diffFieldChanges(lb, f) }
    return { field: f, status: 'unchanged', changes: [] }
  })
  return { left, right }
})

const stageDiff = computed<{ left: StageDiffRow[]; right: StageDiffRow[] }>(() => {
  if (!leftDetail.value || !rightDetail.value) return { left: [], right: [] }
  const ls = leftDetail.value.stages || []
  const rs = rightDetail.value.stages || []
  const keyOf = (s: StageDefinitionDto) => `${s.stageName}#${s.sortOrder}`
  const stageBody = (s: StageDefinitionDto) => {
    const { id: _id, ...rest } = s
    return rest
  }
  const lm = new Map(ls.map(s => [keyOf(s), s]))
  const rm = new Map(rs.map(s => [keyOf(s), s]))
  const left: StageDiffRow[] = ls.map(s => {
    const k = keyOf(s)
    if (!rm.has(k)) return { stage: s, status: 'removed', changes: [] }
    const rb = rm.get(k)!
    if (!isEqual(stageBody(s), stageBody(rb))) return { stage: s, status: 'modified', changes: stageChanges(s, rb) }
    return { stage: s, status: 'unchanged', changes: [] }
  })
  const right: StageDiffRow[] = rs.map(s => {
    const k = keyOf(s)
    if (!lm.has(k)) return { stage: s, status: 'added', changes: [] }
    const lb = lm.get(k)!
    if (!isEqual(stageBody(lb), stageBody(s))) return { stage: s, status: 'modified', changes: stageChanges(lb, s) }
    return { stage: s, status: 'unchanged', changes: [] }
  })
  return { left, right }
})

function stageChanges(a: StageDefinitionDto, b: StageDefinitionDto): string[] {
  const keys = new Set<string>([...Object.keys(a || {}), ...Object.keys(b || {})])
  const changes: string[] = []
  for (const k of keys) {
    if (k === 'id') continue
    const av = (a as any)?.[k]
    const bv = (b as any)?.[k]
    if (JSON.stringify(av) !== JSON.stringify(bv)) {
      changes.push(`${k}: ${formatVal(av)} → ${formatVal(bv)}`)
    }
  }
  return changes
}

function formatVal(v: any): string {
  if (v === null || v === undefined) return '∅'
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

interface SettingsDiffRow { key: string; leftValue: any; rightValue: any; status: DiffStatus }

const settingsDiff = computed<SettingsDiffRow[]>(() => {
  const left = safeParseObject(leftDetail.value?.flowSettingsJson)
  const right = safeParseObject(rightDetail.value?.flowSettingsJson)
  const keys = new Set<string>([...Object.keys(left), ...Object.keys(right)])
  return Array.from(keys).map(k => {
    const lv = left[k]
    const rv = right[k]
    let status: DiffStatus = 'unchanged'
    if (!(k in left)) status = 'added'
    else if (!(k in right)) status = 'removed'
    else if (JSON.stringify(lv) !== JSON.stringify(rv)) status = 'modified'
    return { key: k, leftValue: lv, rightValue: rv, status }
  })
})

const detailFields = computed<SchemaFieldDefinition[]>(
  () => safeParseFields(versionDetail.value?.cardSchemaJson),
)
const detailSettings = computed<Record<string, any>>(
  () => safeParseObject(versionDetail.value?.flowSettingsJson),
)
const detailSettingEntries = computed(() => Object.entries(detailSettings.value))

function statusBadge(status: DiffStatus): { text: string; cls: string } | null {
  if (status === 'added') return { text: '新增', cls: 'badge-added' }
  if (status === 'removed') return { text: '删除', cls: 'badge-removed' }
  if (status === 'modified') return { text: '修改', cls: 'badge-modified' }
  return null
}

const canRollback = computed(() => {
  const v = selectedVersion.value
  return !!v && v.status === 'published' && !v.isCurrentVersion
})

onMounted(async () => {
  await Promise.all([loadFlow(), loadVersions()])
})
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #left>
        <a-button @click="router.push('/cardflow/definitions')">
          <template #icon><ArrowLeftOutlined /></template>
          返回定义列表
        </a-button>
      </template>
      <template #center>
        <span class="header-title">{{ flowName }} · 版本历史</span>
      </template>
      <template #actions>
        <a-space>
          <a-button type="primary" :disabled="!canRollback" @click="handleRollbackSelected">
            <template #icon><RollbackOutlined /></template>
            回滚到选中版本
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      :pagination="false"
      :scroll="{ x: 1080 }"
      :row-class-name="rowClassName"
      :custom-row="customRow"
      :row-selection="{ type: 'radio', selectedRowKeys, onChange: onSelectChange, columnWidth: 48 }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'versionNumber'">
          <span class="version-tag">V{{ record.versionNumber }}</span>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="statusConfig[record.status]?.color || 'default'">
            {{ statusConfig[record.status]?.text || record.status }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'isCurrentVersion'">
          <StatusTag v-if="record.isCurrentVersion" type="info">当前</StatusTag>
          <span v-else style="color: var(--text-3)">-</span>
        </template>
        <template v-else-if="column.key === 'createdBy'">
          {{ (record as any).createdBy || (record as any).creatorName || '-' }}
        </template>
        <template v-else-if="column.key === 'createdTime'">
          {{ formatTime(record.createdTime) }}
        </template>
        <template v-else-if="column.key === 'publishTime'">
          {{ formatTime(record.publishTime) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click.stop="handleViewDetail(record as FlowVersionDto)">
              <EyeOutlined />查看详情
            </a-button>
            <template v-if="record.status === 'draft'">
              <a-button type="link" size="small" danger @click.stop="handleDeleteDraft(record as FlowVersionDto)">
                <DeleteOutlined />删除
              </a-button>
            </template>
            <template v-else>
              <a-button type="link" size="small" @click.stop="handleCompare(record as FlowVersionDto)">
                <DiffOutlined />对比
              </a-button>
              <a-button
                type="link" size="small"
                :disabled="record.isCurrentVersion"
                @click.stop="handleRollback(record as FlowVersionDto)"
              >
                <RollbackOutlined />回滚
              </a-button>
            </template>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- ===== 详情 Drawer ===== -->
    <a-drawer
      v-model:open="detailVisible"
      :title="`版本 V${versionDetail?.versionNumber || ''} 详情`"
      width="720"
      :body-style="{ padding: '16px' }"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="versionDetail">
          <a-descriptions :column="2" bordered size="small" style="margin-bottom: 16px">
            <a-descriptions-item label="版本号">V{{ versionDetail.versionNumber }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="statusConfig[versionDetail.status]?.color">
                {{ statusConfig[versionDetail.status]?.text }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="创建时间">{{ formatTime(versionDetail.createdTime) }}</a-descriptions-item>
            <a-descriptions-item label="发布时间">{{ formatTime(versionDetail.publishTime) }}</a-descriptions-item>
          </a-descriptions>

          <a-collapse :default-active-key="['fields', 'stages', 'settings']">
            <a-collapse-panel key="fields" :header="`Schema 字段（${detailFields.length}）`">
              <a-table
                v-if="detailFields.length"
                size="small"
                :pagination="false"
                :data-source="detailFields"
                row-key="key"
                :columns="[
                  { title: '字段Key', dataIndex: 'key', width: 140 },
                  { title: '名称', dataIndex: 'label', width: 140 },
                  { title: '类型', dataIndex: 'type', width: 90 },
                  { title: '必填', dataIndex: 'required', width: 70, customRender: ({ text }: any) => text ? '是' : '否' },
                  { title: '只读', dataIndex: 'readonly', width: 70, customRender: ({ text }: any) => text ? '是' : '否' },
                ]"
              />
              <a-empty v-else description="无字段" />
            </a-collapse-panel>

            <a-collapse-panel key="stages" :header="`节点链（${versionDetail.stages?.length || 0}）`">
              <div v-if="versionDetail.stages?.length">
                <div v-for="stage in versionDetail.stages" :key="stage.id" class="stage-row">
                  <span class="stage-order">#{{ stage.sortOrder }}</span>
                  <StatusTag :type="stage.type === 'cc' ? 'success' : 'info'">
                    {{ stage.type }}
                  </StatusTag>
                  <span class="stage-name">{{ stage.stageName }}</span>
                  <span v-if="stage.approvalMode" class="stage-meta">模式: {{ stage.approvalMode }}</span>
                  <span v-if="stage.assigneeStrategy" class="stage-meta">指派: {{ stage.assigneeStrategy }}</span>
                </div>
              </div>
              <a-empty v-else description="无节点" />
            </a-collapse-panel>

            <a-collapse-panel key="settings" header="流程设置">
              <a-descriptions
                v-if="detailSettingEntries.length"
                :column="1" bordered size="small"
              >
                <a-descriptions-item
                  v-for="[k, v] in detailSettingEntries" :key="k" :label="k"
                >
                  <pre class="kv-value">{{ formatVal(v) }}</pre>
                </a-descriptions-item>
              </a-descriptions>
              <a-empty v-else description="无配置" />
            </a-collapse-panel>

            <a-collapse-panel key="raw" header="原始 JSON">
              <pre class="json-preview">{{ formatJson(versionDetail.cardSchemaJson) }}</pre>
            </a-collapse-panel>
          </a-collapse>
        </template>
        <a-empty v-else-if="!detailLoading" description="无数据" />
      </a-spin>
    </a-drawer>

    <!-- ===== 对比 Drawer ===== -->
    <a-drawer
      v-model:open="diffVisible"
      title="版本对比"
      placement="right"
      width="100%"
      :body-style="{ padding: '16px', background: 'var(--bg-muted)' }"
      :header-style="{ padding: '12px 16px' }"
    >
      <a-spin :spinning="diffLoading">
        <template v-if="leftDetail && rightDetail">
          <div class="diff-headers">
            <div class="diff-header diff-header-left">
              V{{ leftDetail.versionNumber }}（旧版本）
              <a-tag :color="statusConfig[leftDetail.status]?.color">
                {{ statusConfig[leftDetail.status]?.text }}
              </a-tag>
            </div>
            <div class="diff-header diff-header-right">
              V{{ rightDetail.versionNumber }}（新版本）
              <a-tag :color="statusConfig[rightDetail.status]?.color">
                {{ statusConfig[rightDetail.status]?.text }}
              </a-tag>
            </div>
          </div>

          <!-- Schema 字段变更区 -->
          <section class="diff-section">
            <div class="diff-section-title">Schema 字段变更</div>
            <div class="diff-cols">
              <div class="diff-col">
                <div v-if="!fieldDiff.left.length" class="diff-empty">无字段</div>
                <div
                  v-for="row in fieldDiff.left" :key="'L' + row.field.key"
                  class="diff-item" :class="`diff-${row.status}`"
                >
                  <div class="diff-item-head">
                    <span class="diff-item-key">{{ row.field.key }}</span>
                    <span class="diff-item-label">{{ row.field.label }}</span>
                    <span class="diff-item-type">{{ row.field.type }}</span>
                    <span v-if="statusBadge(row.status)" class="badge" :class="statusBadge(row.status)!.cls">
                      {{ statusBadge(row.status)!.text }}
                    </span>
                  </div>
                  <ul v-if="row.status === 'modified' && row.changes.length" class="diff-changes">
                    <li v-for="(c, i) in row.changes" :key="i">{{ c }}</li>
                  </ul>
                </div>
              </div>
              <div class="diff-col">
                <div v-if="!fieldDiff.right.length" class="diff-empty">无字段</div>
                <div
                  v-for="row in fieldDiff.right" :key="'R' + row.field.key"
                  class="diff-item" :class="`diff-${row.status}`"
                >
                  <div class="diff-item-head">
                    <span class="diff-item-key">{{ row.field.key }}</span>
                    <span class="diff-item-label">{{ row.field.label }}</span>
                    <span class="diff-item-type">{{ row.field.type }}</span>
                    <span v-if="statusBadge(row.status)" class="badge" :class="statusBadge(row.status)!.cls">
                      {{ statusBadge(row.status)!.text }}
                    </span>
                  </div>
                  <ul v-if="row.status === 'modified' && row.changes.length" class="diff-changes">
                    <li v-for="(c, i) in row.changes" :key="i">{{ c }}</li>
                  </ul>
                </div>
              </div>
            </div>
          </section>

          <!-- 节点链变更区 -->
          <section class="diff-section">
            <div class="diff-section-title">节点链变更</div>
            <div class="diff-cols">
              <div class="diff-col">
                <div v-if="!stageDiff.left.length" class="diff-empty">无节点</div>
                <div
                  v-for="(row, i) in stageDiff.left" :key="'LS' + i"
                  class="diff-item" :class="`diff-${row.status}`"
                >
                  <div class="diff-item-head">
                    <span class="stage-order">#{{ row.stage.sortOrder }}</span>
                    <StatusTag :type="row.stage.type === 'cc' ? 'success' : 'info'">
                      {{ row.stage.type }}
                    </StatusTag>
                    <span class="diff-item-label">{{ row.stage.stageName }}</span>
                    <span v-if="statusBadge(row.status)" class="badge" :class="statusBadge(row.status)!.cls">
                      {{ statusBadge(row.status)!.text }}
                    </span>
                  </div>
                  <ul v-if="row.status === 'modified' && row.changes.length" class="diff-changes">
                    <li v-for="(c, idx) in row.changes" :key="idx">{{ c }}</li>
                  </ul>
                </div>
              </div>
              <div class="diff-col">
                <div v-if="!stageDiff.right.length" class="diff-empty">无节点</div>
                <div
                  v-for="(row, i) in stageDiff.right" :key="'RS' + i"
                  class="diff-item" :class="`diff-${row.status}`"
                >
                  <div class="diff-item-head">
                    <span class="stage-order">#{{ row.stage.sortOrder }}</span>
                    <StatusTag :type="row.stage.type === 'cc' ? 'success' : 'info'">
                      {{ row.stage.type }}
                    </StatusTag>
                    <span class="diff-item-label">{{ row.stage.stageName }}</span>
                    <span v-if="statusBadge(row.status)" class="badge" :class="statusBadge(row.status)!.cls">
                      {{ statusBadge(row.status)!.text }}
                    </span>
                  </div>
                  <ul v-if="row.status === 'modified' && row.changes.length" class="diff-changes">
                    <li v-for="(c, idx) in row.changes" :key="idx">{{ c }}</li>
                  </ul>
                </div>
              </div>
            </div>
          </section>

          <!-- 流程设置变更区 -->
          <section class="diff-section">
            <div class="diff-section-title">流程设置变更</div>
            <div v-if="!settingsDiff.length" class="diff-empty">无配置</div>
            <div v-else class="settings-table">
              <div class="settings-row settings-head">
                <div class="settings-key">配置项</div>
                <div class="settings-val">V{{ leftDetail.versionNumber }}</div>
                <div class="settings-val">V{{ rightDetail.versionNumber }}</div>
              </div>
              <div
                v-for="row in settingsDiff" :key="row.key"
                class="settings-row" :class="`diff-${row.status}`"
              >
                <div class="settings-key">
                  {{ row.key }}
                  <span v-if="statusBadge(row.status)" class="badge" :class="statusBadge(row.status)!.cls">
                    {{ statusBadge(row.status)!.text }}
                  </span>
                </div>
                <div class="settings-val"><pre class="kv-value">{{ formatVal(row.leftValue) }}</pre></div>
                <div class="settings-val"><pre class="kv-value">{{ formatVal(row.rightValue) }}</pre></div>
              </div>
            </div>
          </section>
        </template>
        <a-empty v-else-if="!diffLoading" description="无对比数据" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 16px;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}

.version-tag {
  font-weight: 600;
  color: var(--color-info);
  letter-spacing: 0.5px;
}

:deep(.row-current) > td {
  background: var(--color-primary-light) !important;
}
:deep(.row-current:hover) > td {
  background: var(--color-primary-border) !important;
}

.stage-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 0;
  border-bottom: 1px dashed var(--border);
  &:last-child { border-bottom: none; }
}
.stage-order {
  display: inline-block;
  min-width: 32px;
  color: var(--text-3);
  font-family: monospace;
}
.stage-name { font-weight: 500; color: var(--text-1); }
.stage-meta { color: var(--text-3); font-size: 12px; }

.json-preview {
  background: var(--bg-muted);
  padding: 12px;
  border-radius: 4px;
  font-size: 12px;
  max-height: 320px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 0;
}
.kv-value {
  margin: 0;
  font-family: 'JetBrains Mono', Consolas, monospace;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}

/* ===== Diff 视图 ===== */
.diff-headers {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 12px;
}
.diff-header {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 14px;
  font-size: 14px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
}
.diff-header-left { color: var(--text-2); }
.diff-header-right { color: var(--color-info); }

.diff-section {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 12px 14px;
  margin-bottom: 12px;
}
.diff-section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
  padding-bottom: 8px;
  margin-bottom: 10px;
  border-bottom: 1px solid var(--border);
}

.diff-cols {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.diff-col {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 40px;
}
.diff-empty {
  color: var(--text-3);
  font-size: 12px;
  padding: 12px;
  text-align: center;
  border: 1px dashed var(--border);
  border-radius: 4px;
}

.diff-item {
  background: var(--bg-muted);
  border-left: 3px solid var(--border);
  border-radius: 4px;
  padding: 8px 12px;
  font-size: 13px;
}
.diff-item-head {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.diff-item-key {
  font-family: monospace;
  color: var(--color-info);
  font-weight: 500;
}
.diff-item-label { color: var(--text-1); }
.diff-item-type {
  color: var(--text-3);
  font-size: 12px;
  padding: 0 6px;
  background: var(--bg-muted);
  border-radius: 2px;
}
.diff-changes {
  margin: 6px 0 0;
  padding-left: 18px;
  font-size: 12px;
  color: var(--text-2);
  font-family: 'JetBrains Mono', Consolas, monospace;
  li { line-height: 1.6; }
}

.diff-added {
  background: var(--color-success-light);
  border-left: 3px solid var(--color-success);
}
.diff-removed {
  background: var(--color-danger-light);
  border-left: 3px solid var(--color-danger);
}
.diff-modified {
  background: var(--color-warning-light);
  border-left: 3px solid var(--color-warning);
}
.diff-unchanged {
  opacity: 0.7;
}

.badge {
  display: inline-block;
  padding: 0 6px;
  font-size: 11px;
  line-height: 18px;
  border-radius: 2px;
  font-weight: 500;
}
.badge-added { background: var(--color-success); color: var(--text-on-accent); }
.badge-removed { background: var(--color-danger); color: var(--text-on-accent); }
.badge-modified { background: var(--color-warning); color: var(--text-on-accent); }

/* settings table */
.settings-table {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border);
  border-radius: 4px;
  overflow: hidden;
}
.settings-row {
  display: grid;
  grid-template-columns: 240px 1fr 1fr;
  border-bottom: 1px solid var(--border);
  &:last-child { border-bottom: none; }
}
.settings-row.settings-head {
  background: var(--bg-muted);
  font-weight: 600;
  font-size: 13px;
}
.settings-key {
  padding: 8px 12px;
  border-right: 1px solid var(--border);
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: monospace;
  color: var(--color-info);
}
.settings-val {
  padding: 8px 12px;
  border-right: 1px solid var(--border);
  &:last-child { border-right: none; }
}
.settings-row.diff-added,
.settings-row.diff-removed,
.settings-row.diff-modified {
  border-left: 3px solid transparent;
}
.settings-row.diff-added { background: var(--color-success-light); border-left-color: var(--color-success); }
.settings-row.diff-removed { background: var(--color-danger-light); border-left-color: var(--color-danger); }
.settings-row.diff-modified { background: var(--color-warning-light); border-left-color: var(--color-warning); }
</style>
