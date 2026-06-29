<script setup lang="ts">
/**
 * BatchManage.vue — 卡片流程上传中心页面
 *
 * 顶部：流程定义 / 状态筛选 + 上传按钮
 * 主表：批次列表，可展开为进度面板或暂存数据表格
 */
import { ref, computed, onMounted, watch } from 'vue'
import { Modal, message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  PlusOutlined,
  ReloadOutlined,
  DeleteOutlined,
  EyeOutlined,
} from '@ant-design/icons-vue'
import type {
  CfBatch,
  CfBatchStatus,
  FlowDefinitionDto,
  SchemaFieldDefinition,
} from '@/types/cardflow'
import {
  getBatches,
  revokeBatch,
  getFlowDefinitions,
  getFlowVersionDetail,
  getFlowDraftVersion,
} from '@/api/cardflow'
import BatchUploadDialog from '@/components/cardflow/BatchUploadDialog.vue'
import BatchProgressPanel from '@/components/cardflow/BatchProgressPanel.vue'
import BatchStagingTable from '@/components/cardflow/BatchStagingTable.vue'
import { parseCardSchemaFields } from '@/utils/cardflowSchema'

// ==================== 状态 ====================
const loading = ref(false)
const batches = ref<CfBatch[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

const filterFlowId = ref<number | undefined>(undefined)
const filterStatus = ref<CfBatchStatus | undefined>(undefined)

const uploadVisible = ref(false)
const flowOptions = ref<FlowDefinitionDto[]>([])

// 展开行 schema 缓存：flowDefinitionId -> Schema
const schemaCache = ref<Record<number, SchemaFieldDefinition[]>>({})
const expandedRowKeys = ref<number[]>([])

// ==================== 状态映射 ====================
const STATUS_MAP: Record<CfBatchStatus, { label: string; color: string }> = {
  0: { label: '解析中', color: 'processing' },
  1: { label: '已暂存', color: 'warning' },
  2: { label: '质检中', color: 'processing' },
  3: { label: '已创建卡片', color: 'cyan' },
  4: { label: '处理中', color: 'processing' },
  5: { label: '已完成', color: 'success' },
}

const TRIGGER_MAP: Record<string, string> = {
  human: '手动',
  fileUpload: '文件上传',
  scheduled: '定时',
  conditional: '条件',
}

// ==================== 列 ====================
const columns: TableColumnsType = [
  { title: 'ID', dataIndex: 'id', width: 80 },
  { title: '流程', dataIndex: 'flowName', ellipsis: true, width: 200 },
  { title: '触发类型', dataIndex: 'triggerType', width: 100 },
  { title: '触发时间', dataIndex: 'triggeredTime', width: 160 },
  { title: '总行数', dataIndex: 'totalRows', width: 90, align: 'right' },
  { title: '成功 / 失败', key: 'rowsResult', width: 130, align: 'right' },
  { title: '状态', dataIndex: 'status', width: 110 },
  { title: '操作', key: 'actions', width: 160, fixed: 'right' },
]

// ==================== 数据 ====================
async function loadFlows() {
  try {
    const res = await getFlowDefinitions({ pageSize: 200 })
    flowOptions.value = res.items || []
  } catch {
    /* 忽略 */
  }
}

async function load() {
  loading.value = true
  try {
    const res = await getBatches({
      flowDefinitionId: filterFlowId.value,
      status: filterStatus.value,
      page: page.value,
      pageSize: pageSize.value,
    })
    batches.value = res.items || []
    total.value = res.total || 0
  } catch {
    message.error('加载批次列表失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadFlows()
  load()
})

watch([filterFlowId, filterStatus, page, pageSize], load)

// ==================== Schema 加载（用于展开暂存表） ====================
function parseSchemaJson(json?: string | null): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

async function ensureSchema(flowDefinitionId: number) {
  if (schemaCache.value[flowDefinitionId]) return schemaCache.value[flowDefinitionId]
  try {
    let ver: any = null
    try {
      ver = await getFlowDraftVersion(flowDefinitionId)
    } catch {
      ver = null
    }
    if (!ver) {
      const flow: any = flowOptions.value.find((f) => f.id === flowDefinitionId)
      if (flow?.currentVersionId) {
        ver = await getFlowVersionDetail(flowDefinitionId, flow.currentVersionId)
      }
    }
    const fields = parseSchemaJson(ver?.cardSchemaJson)
    schemaCache.value[flowDefinitionId] = fields
    return fields
  } catch {
    schemaCache.value[flowDefinitionId] = []
    return []
  }
}

async function onExpand(expanded: boolean, record: CfBatch) {
  if (expanded) {
    await ensureSchema(record.flowDefinitionId)
    expandedRowKeys.value = [record.id]
  } else {
    expandedRowKeys.value = []
  }
}

// ==================== 操作 ====================
function onUploadSuccess(_batchId: number) {
  load()
}

function handleRevoke(record: CfBatch) {
  Modal.confirm({
    title: `撤销批次 #${record.id}？`,
    content: '撤销将回滚已生成的卡片，操作不可逆。',
    okText: '撤销',
    okType: 'danger',
    onOk: async () => {
      try {
        await revokeBatch(record.id)
        message.success('已撤销')
        await load()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '撤销失败')
      }
    },
  })
}

const showStagingForExpand = (b: CfBatch) => b.status === 1 || b.status === 2 || b.status === 3
</script>

<template>
  <div class="cf-batch-manage">
    <!-- 顶部工具栏 -->
    <div class="cf-batch-manage__toolbar">
      <a-select
        v-model:value="filterFlowId"
        class="cf-batch-manage__filter"
        placeholder="流程定义"
        allow-clear
        show-search
        style="width: 220px"
        :filter-option="(input: string, opt: any) => (opt.label || '').toLowerCase().includes(input.toLowerCase())"
        :options="flowOptions.map((f) => ({ label: f.flowName, value: f.id }))"
      />
      <a-select
        v-model:value="filterStatus"
        class="cf-batch-manage__filter"
        placeholder="状态"
        allow-clear
        style="width: 140px"
        :options="Object.entries(STATUS_MAP).map(([v, m]) => ({ label: m.label, value: Number(v) }))"
      />

      <span class="cf-batch-manage__spacer" />

      <a-button @click="load">
        <template #icon><reload-outlined /></template>
        刷新
      </a-button>
      <a-button type="primary" @click="uploadVisible = true">
        <template #icon><plus-outlined /></template>
        批量上传
      </a-button>
    </div>

    <!-- 批次列表 -->
    <a-table
      :loading="loading"
      :columns="columns"
      :data-source="batches"
      :row-key="(r: any) => r.id"
      :expanded-row-keys="expandedRowKeys"
      :pagination="{
        current: page,
        pageSize,
        total,
        showSizeChanger: true,
        onChange: (p: number, ps: number) => { page = p; pageSize = ps },
      }"
      :scroll="{ x: 'max-content' }"
      @expand="onExpand"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'triggerType'">
          {{ TRIGGER_MAP[record.triggerType] || record.triggerType }}
        </template>
        <template v-else-if="column.dataIndex === 'status'">
          <a-tag :color="STATUS_MAP[record.status as CfBatchStatus]?.color">
            {{ STATUS_MAP[record.status as CfBatchStatus]?.label || record.status }}
          </a-tag>
          <a-tag v-if="record.isRevoked" color="default">已撤销</a-tag>
        </template>
        <template v-else-if="column.key === 'rowsResult'">
          <span style="color: var(--color-success-text)">{{ record.successRows }}</span>
          <span style="margin: 0 4px; color: var(--text-3)">/</span>
          <span :style="{ color: record.failedRows > 0 ? 'var(--color-danger-text)' : 'var(--text-3)' }">
            {{ record.failedRows }}
          </span>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-button
            type="link"
            size="small"
            @click="onExpand(!expandedRowKeys.includes(record.id), record as CfBatch)"
          >
            <template #icon><eye-outlined /></template>
            {{ expandedRowKeys.includes(record.id) ? '收起' : '查看' }}
          </a-button>
          <a-button
            v-if="!record.isRevoked"
            type="link"
            size="small"
            danger
            @click="handleRevoke(record as CfBatch)"
          >
            <template #icon><delete-outlined /></template>
            撤销
          </a-button>
        </template>
      </template>

      <!-- 展开行 -->
      <template #expandedRowRender="{ record }">
        <div class="cf-batch-manage__expand">
          <BatchProgressPanel
            :batch-id="record.id"
            :batch-status="record.status"
            @view-failures="() => { /* 可扩展：跳转失败明细 */ }"
          />
          <BatchStagingTable
            v-if="showStagingForExpand(record)"
            :batch-id="record.id"
            :schema="schemaCache[record.flowDefinitionId] || []"
            :editable="record.status === 1 || record.status === 2"
            @confirmed="load"
            @changed="load"
          />
        </div>
      </template>
    </a-table>

    <!-- 上传弹窗 -->
    <BatchUploadDialog
      v-model:visible="uploadVisible"
      :flow-definition-id="filterFlowId ?? null"
      @success="onUploadSuccess"
    />
  </div>
</template>

<style scoped lang="scss">
.cf-batch-manage {
  padding: 16px;
  background: var(--bg-card);

  &__toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 12px;
  }

  &__spacer {
    flex: 1;
  }

  &__expand {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding: 8px 0;
  }
}
</style>
