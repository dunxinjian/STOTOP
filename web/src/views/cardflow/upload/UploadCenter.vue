<template>
  <div class="upload-center">
    <PageHeader>
      <template #title-extra>
        <a-upload
          v-if="has(CardFlowPermissions.ImportUpload)"
          :multiple="true"
          :showUploadList="false"
          accept=".xlsx,.xls,.csv"
          :beforeUpload="onBeforeUpload"
          :disabled="uploadDisabled"
          class="inline-upload"
        >
          <a-button type="primary" :disabled="uploadDisabled"><UploadOutlined /> 选择文件</a-button>
        </a-upload>
      </template>
      <template #center>
        <span v-if="orgPreviewResult" class="toolbar-account-set">
          账套：{{ orgPreviewResult.resolvedAccountSetName || '未绑定' }}
        </span>
      </template>
      <template #actions>
        <!-- 批量管理按钮（正常模式） -->
        <a-button
          v-if="!batchMode"
          class="toolbar-btn"
          :disabled="deletableBatchCount === 0"
          @click="enterBatchMode"
        >
          <template #icon><AppstoreOutlined /></template>
          批量管理
        </a-button>
        <!-- 批量模式操作条 -->
        <template v-if="batchMode">
          <a-checkbox
            :checked="selectAllChecked"
            :indeterminate="selectAllIndeterminate"
            @change="toggleSelectAll"
          >全选</a-checkbox>
          <a-button
            danger
            :disabled="selectedIds.length === 0"
            :loading="batchRevoking"
            @click="handleBatchRevoke"
          >
            删除选中({{ selectedIds.length }})
          </a-button>
          <a-button type="text" @click="exitBatchMode">取消</a-button>
        </template>
        <a-badge :count="recycledBatches.length" :offset="[-4, 4]">
          <a-button shape="circle" @click="openRecycleBin" title="回收站">
            <template #icon><DeleteOutlined /></template>
          </a-button>
        </a-badge>
      </template>
    </PageHeader>

    <!-- 组织预检警告提示 -->
    <div v-if="orgPreviewWarnings.length > 0" class="org-preview-bar">
      <div class="org-preview-warnings">
        <WarningOutlined style="color: var(--color-danger); margin-right: 6px" />
        <span v-for="(w, idx) in orgPreviewWarnings" :key="idx" class="warning-text">{{ w }}</span>
      </div>
    </div>

    <!-- ===== 批量智能导入区（多文件内容路由） ===== -->
    <div v-if="has(CardFlowPermissions.ImportUpload)" class="auto-import-panel">
      <div class="auto-import-header">
        <div class="auto-import-title">
          <ThunderboltOutlined style="color: var(--color-primary); margin-right: 6px" />
          批量智能导入
          <span class="auto-import-hint">一次选多个文件，按表头内容自动路由到对应流程</span>
        </div>
        <div class="auto-import-actions">
          <a-upload
            :multiple="true"
            :showUploadList="false"
            accept=".xlsx,.xls,.csv"
            :beforeUpload="onAutoBeforeUpload"
            :disabled="uploadDisabled || autoImporting"
          >
            <a-button :disabled="uploadDisabled || autoImporting">
              <UploadOutlined /> 选择文件
            </a-button>
          </a-upload>
          <a-button
            type="primary"
            :disabled="autoFiles.length === 0 || uploadDisabled"
            :loading="autoImporting"
            @click="startAutoImport"
          >
            开始智能导入{{ autoFiles.length > 0 ? `（${autoFiles.length}）` : '' }}
          </a-button>
          <a-button
            v-if="autoFiles.length > 0 && !autoImporting"
            type="text"
            @click="clearAutoFiles"
          >清空</a-button>
        </div>
      </div>

      <!-- 已选文件列表 -->
      <div v-if="autoFiles.length > 0" class="auto-file-list">
        <a-tag
          v-for="(f, idx) in autoFiles"
          :key="idx"
          closable
          class="auto-file-tag"
          @close="removeAutoFile(idx)"
        >
          <FileExcelOutlined style="color: var(--color-success); margin-right: 4px" />
          {{ f.name }}
        </a-tag>
      </div>

      <!-- 结果分区展示 -->
      <div v-if="autoResult" class="auto-result">
        <!-- 已路由 -->
        <div v-if="autoResult.routed.length > 0" class="auto-result-section">
          <div class="auto-result-section-title">
            <StatusTag type="success">已路由</StatusTag>
            <span class="auto-result-count">{{ autoResult.routed.length }} 个文件已触发导入</span>
          </div>
          <a-table
            :columns="routedColumns"
            :data-source="autoResult.routed"
            :pagination="false"
            size="small"
            row-key="fileName"
          />
        </div>

        <!-- 待认领 -->
        <div v-if="autoResult.unmatched.length > 0" class="auto-result-section">
          <div class="auto-result-section-title">
            <StatusTag type="warning">待认领</StatusTag>
            <span class="auto-result-count">{{ autoResult.unmatched.length }} 个文件未匹配流程，需人工指派</span>
          </div>
          <a-list :data-source="autoResult.unmatched" size="small" :split="true">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <FileExcelOutlined style="color: var(--color-warning); margin-right: 6px" />
                    {{ item.fileName }}
                  </template>
                  <template #description>
                    <span class="auto-cols-label">识别到的列：</span>
                    <a-tag v-for="(c, ci) in item.columns" :key="ci" class="auto-col-tag">{{ c }}</a-tag>
                    <span v-if="item.columns.length === 0" class="auto-empty-cols">（未识别到列）</span>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </div>

        <!-- 多义 -->
        <div v-if="autoResult.ambiguous.length > 0" class="auto-result-section">
          <div class="auto-result-section-title">
            <StatusTag type="info">多义</StatusTag>
            <span class="auto-result-count">{{ autoResult.ambiguous.length }} 个文件命中多个流程，需人工抉择</span>
          </div>
          <a-list :data-source="autoResult.ambiguous" size="small" :split="true">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <FileExcelOutlined style="color: var(--color-info); margin-right: 6px" />
                    {{ item.fileName }}
                  </template>
                  <template #description>
                    <span class="auto-cols-label">候选流程：</span>
                    <StatusTag v-for="(c, ci) in item.candidates" :key="ci" type="info" class="auto-col-tag">
                      流程 #{{ c.flowDefinitionId }} / 规则 #{{ c.pluginRuleId }}
                    </StatusTag>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </div>

        <!-- 读取失败 -->
        <div v-if="autoResult.readErrors.length > 0" class="auto-result-section">
          <div class="auto-result-section-title">
            <StatusTag type="danger">读取失败</StatusTag>
            <span class="auto-result-count">{{ autoResult.readErrors.length }} 个文件无法读取</span>
          </div>
          <a-list :data-source="autoResult.readErrors" size="small" :split="true">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <FileExcelOutlined style="color: var(--color-danger); margin-right: 6px" />
                    {{ item.fileName }}
                  </template>
                  <template #description>
                    <span class="auto-error-text">{{ item.error }}</span>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </div>

        <!-- 触发失败 -->
        <div v-if="autoResult.triggerErrors.length > 0" class="auto-result-section">
          <div class="auto-result-section-title">
            <StatusTag type="danger">触发失败</StatusTag>
            <span class="auto-result-count">{{ autoResult.triggerErrors.length }} 个文件命中流程但触发导入失败</span>
          </div>
          <a-list :data-source="autoResult.triggerErrors" size="small" :split="true">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <FileExcelOutlined style="color: var(--color-danger); margin-right: 6px" />
                    {{ item.fileName }}
                    <StatusTag type="info" class="auto-col-tag">流程 #{{ item.flowDefinitionId }}</StatusTag>
                  </template>
                  <template #description>
                    <span class="auto-error-text">{{ item.error }}</span>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </div>
      </div>
    </div>

    <!-- 上传拖拽区（可折叠） -->
    <UploadDropZone
      v-if="has(CardFlowPermissions.ImportUpload)"
      v-model:collapsed="uploadCollapsed"
      :disabled="uploadDisabled"
      @upload="handleFileUpload"
    />

    <!-- 三合一内容区 -->
    <div class="unified-panel">
      <!-- 合并的头部行：左侧 KPI + 右侧筛选 -->
      <div class="header-row">
        <BatchStatsBar :kpi-items="roleKpi" />
        <BatchFilterBar
          :filters="filters"
          :view-mode="viewMode"
          :show-view-toggle="currentRole === 'manager'"
          @update:filters="filters = $event"
          @update:view-mode="viewMode = $event"
        />
      </div>

      <!-- ===== 看板视图（仅管理者） ===== -->
      <div v-if="currentRole === 'manager' && viewMode === 'board'" class="kanban-board">
        <div v-for="col in kanbanColumns" :key="col.title" class="kanban-column" :style="{ background: col.bg }">
          <div class="kanban-header">
            {{ col.title }}
            <span class="kanban-count">{{ col.cards.length }}</span>
          </div>
          <div
            v-for="kc in col.cards"
            :key="kc.id"
            class="kanban-card"
            :class="{ stale: (kc.waitHours ?? 0) >= 4 }"
            >
            <div class="kanban-card-name">
              <FileExcelOutlined style="color: var(--color-success); margin-right: 4px" />
              {{ kc.fileName }}
            </div>
            <div class="kanban-card-meta">
              <span class="card-status-dot" :class="getStatusDotClass(kc.status)"></span>
              <span>{{ getStatusText(kc.status) }}</span>
              <span
                v-if="kc.waitTime"
                class="wait-time"
                :class="{ warning: (kc.waitHours ?? 0) >= 4 }"
              >{{ kc.waitTime }}</span>
            </div>
            <div v-if="kc.assigneeName" class="kanban-card-handler">
              <span class="handler-avatar-mini" :style="{ background: avatarColor(kc.assigneeName) }">
                {{ kc.assigneeName?.[0] }}
              </span>
              {{ kc.assigneeName }}
            </div>
          </div>
        </div>
      </div>

      <!-- ===== 列表视图 ===== -->
      <div v-else class="card-list">
        <a-spin :spinning="loading">
          <BatchCard
            v-for="batch in displayBatches"
            :key="batch.id"
            :batch="batch"
            :selectable="batchMode"
            :checked="selectedIds.includes(batch.id)"
            @check="(id: number) => toggleBatchChecked(id, !selectedIds.includes(id))"
            @action="handleBatchAction"
          />
          <a-empty v-if="!loading && displayBatches.length === 0" description="暂无数据" />
        </a-spin>
      </div>
    </div><!-- /unified-panel -->

    <!-- ===== 回收站抽屉 ===== -->
    <a-drawer
      v-model:open="showRecycleBin"
      title="回收站"
      :width="700"
      placement="right"
    >
      <template #extra>
        <a-button
          v-if="recycledBatches.length > 0"
          type="text"
          danger
          :loading="clearingRecycleBin"
          @click="handleClearRecycleBin"
        >
          <template #icon><DeleteOutlined /></template>
          清空
        </a-button>
      </template>
      <a-spin :spinning="recycleBinLoading">
        <div v-if="recycledBatches.length === 0 && !recycleBinLoading" class="recycle-empty">
          <a-empty description="回收站为空" />
        </div>
        <div v-else class="recycle-list">
          <div
            v-for="item in recycledBatches"
            :key="item.id"
            class="recycle-item"
            :class="{ 'recycle-item--deleting': item.id === deletingBatchId }"
          >
            <div class="recycle-item-info">
              <div class="recycle-item-name">
                <FileExcelOutlined style="color: var(--color-success); margin-right: 6px" />
                <span class="recycle-batch-id">#{{ item.id }}</span>
                {{ item.fileName }}
              </div>
              <div class="recycle-item-meta">
                <span>{{ formatFileSize(item.fileSize) }}</span>
                <span v-if="item.totalRows"> · {{ item.totalRows }} 行</span>
                <span> · 撤销于 {{ formatTime(item.revokedTime) }}</span>
              </div>
              <div v-if="item.id === deletingBatchId" class="recycle-item-deleting-hint">
                <LoadingOutlined style="margin-right: 4px" />
                正在彻底删除...
              </div>
            </div>
            <div class="recycle-item-actions">
              <a-button
                type="link"
                size="small"
                :disabled="item.id === deletingBatchId || clearingRecycleBin"
                @click="handleRestoreBatch(item.id)"
              >恢复</a-button>
              <a-button
                type="link"
                size="small"
                danger
                :disabled="item.id === deletingBatchId || clearingRecycleBin"
                @click="handlePermanentDelete(item.id)"
              >彻底删除</a-button>
            </div>
          </div>
        </div>
      </a-spin>
    </a-drawer>

    <!-- ===== 分配/转交弹窗 ===== -->
    <a-modal
      v-model:open="assignModalVisible"
      :title="`${assignAction}处理人`"
      :ok-text="`确认${assignAction}`"
      cancel-text="取消"
      @ok="confirmAssign"
      :width="400"
    >
      <div class="assign-modal-body">
        <p>文件：{{ assignTarget?.fileName }}</p>
        <p v-if="assignTarget?.assigneeName && assignTarget.assigneeName !== '系统自动'" style="color: var(--text-3)">
          当前处理人：{{ assignTarget.assigneeName }}
        </p>
        <a-select v-model:value="assignTo" placeholder="请选择处理人" style="width: 100%; margin-top: 12px">
          <a-select-option v-for="u in assignUserOptions" :key="u" :value="u">{{ u }}</a-select-option>
        </a-select>
      </div>
    </a-modal>

    <!-- ===== 流程选择弹窗（未匹配流程时） ===== -->
    <a-modal
      v-model:open="flowSelectVisible"
      title="选择流程定义"
      ok-text="确认"
      cancel-text="取消"
      :confirm-loading="flowSelectLoading"
      :width="520"
      @ok="confirmFlowSelect"
    >
      <div class="flow-select-modal-body">
        <p style="color: var(--text-3); margin-bottom: 12px">
          文件 <strong>{{ unmatchedBatchInfo?.fileName }}</strong> 未匹配到流程，请手动选择：
        </p>
        <a-radio-group v-model:value="selectedFlowId" style="width: 100%">
          <div v-for="flow in flowCandidates" :key="flow.FID" class="flow-candidate-item">
            <a-radio :value="flow.FID">
              <span class="flow-candidate-name">{{ flow.FFlowName }}</span>
              <span v-if="flow.FFlowCode" class="flow-candidate-code">{{ flow.FFlowCode }}</span>
              <div v-if="flow.FDescription" class="flow-candidate-desc">{{ flow.FDescription }}</div>
            </a-radio>
          </div>
        </a-radio-group>
        <a-empty v-if="flowCandidates.length === 0" description="暂无可用流程定义" />
      </div>
    </a-modal>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  UploadOutlined, FileExcelOutlined, DeleteOutlined, LoadingOutlined, WarningOutlined,
  AppstoreOutlined, ThunderboltOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { usePermission, CardFlowPermissions } from '@/utils/permission'
import { useUploadCenter } from './composables/useUploadCenter'
import type { BatchItem, RoleType } from './composables/useUploadCenter'
import { getFlowDefinitionCandidates, assignPipeline, uploadAutoBatch } from '@/api/cardflow'
import type { FlowDefinitionCandidateDto, UploadAutoResult } from '@/api/cardflow'

import UploadDropZone from './components/UploadDropZone.vue'
import BatchStatsBar from './components/BatchStatsBar.vue'
import BatchFilterBar from './components/BatchFilterBar.vue'
import BatchCard from './components/BatchCard.vue'
import StatusTag from '@/components/StatusTag.vue'

const { has } = usePermission()
const router = useRouter()

const {
  loading,
  batches,
  stats,
  filters,
  currentRole,
  viewMode,
  uploadCollapsed,
  currentUserName,
  roleKpi,
  filteredBatches,
  displayBatches,
  loadBatches,
  getStatusText,
  getStatusDotClass,
  avatarColor,
  handleRetry,
  handleRecalculate,
  handleDelete,
  handleFileUpload,
  onUnmatchedBatch,
  // 组织预检
  orgPreviewResult,
  orgPreviewLoading,
  orgPreviewWarnings,
  uploadDisabled,
  // 批量操作
  batchMode,
  selectedIds,
  batchRevoking,
  clearingRecycleBin,
  deletableBatchCount,
  selectAllChecked,
  selectAllIndeterminate,
  enterBatchMode,
  exitBatchMode,
  toggleSelectAll,
  handleBatchRevoke,
  handleClearRecycleBin,
  // 回收站
  recycledBatches,
  showRecycleBin,
  recycleBinLoading,
  deletingBatchId,
  loadRecycledBatches,
  handleRestoreBatch,
  handlePermanentDelete,
} = useUploadCenter()

// ===== 回收站 =====
function openRecycleBin() {
  showRecycleBin.value = true
  loadRecycledBatches()
}

// ===== 批量模式勾选 =====
function toggleBatchChecked(batchId: number, val: boolean) {
  if (val) {
    if (!selectedIds.value.includes(batchId)) {
      selectedIds.value = [...selectedIds.value, batchId]
    }
  } else {
    selectedIds.value = selectedIds.value.filter(id => id !== batchId)
  }
}

function formatFileSize(bytes: number): string {
  if (!bytes) return '0 B'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

function formatTime(timeStr: string): string {
  if (!timeStr) return '-'
  const d = new Date(timeStr)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

// ===== 看板列 =====
const kanbanColumns = computed(() => {
  const all = filteredBatches.value
  return [
    { title: '待分配', bg: 'var(--color-warning-light)', cards: all.filter(b => !b.assigneeName || b.assigneeName === '系统自动') },
    { title: '处理中', bg: 'var(--color-info-light)', cards: all.filter(b => b.assigneeName && b.assigneeName !== '系统自动' && b.status !== 'success') },
    { title: '已完成', bg: 'var(--color-success-light)', cards: all.filter(b => b.status === 'success') },
  ]
})

// ===== 分配/转交 =====
const assignModalVisible = ref(false)
const assignTarget = ref<BatchItem | null>(null)
const assignTo = ref('')
const assignAction = ref('分配')
const assignUserOptions = ['李会计', '张运营', '王文员', '赵经理'] // TODO: 从后端获取

function openAssignModal(batch: BatchItem, action: string) {
  assignTarget.value = batch
  assignTo.value = ''
  assignAction.value = action
  assignModalVisible.value = true
}

function confirmAssign() {
  if (!assignTo.value) {
    message.warning('请选择处理人')
    return
  }
  if (assignTarget.value) {
    assignTarget.value.assigneeName = assignTo.value
    message.success(`已${assignAction.value}给${assignTo.value}`)
  }
  assignModalVisible.value = false
}

// ===== 流程选择弹窗（未匹配流程时） =====
const flowSelectVisible = ref(false)
const flowSelectLoading = ref(false)
const flowCandidates = ref<FlowDefinitionCandidateDto[]>([])
const selectedFlowId = ref<number | null>(null)
const unmatchedBatchInfo = ref<{ batchId: number; fileName: string } | null>(null)

// 监听 onUnmatchedBatch 事件
watch(onUnmatchedBatch, (val) => {
  if (val) {
    unmatchedBatchInfo.value = val
    selectedFlowId.value = null
    flowSelectVisible.value = true
    // 加载候选流程列表
    getFlowDefinitionCandidates().then((list) => {
      flowCandidates.value = list || []
    }).catch(() => {
      flowCandidates.value = []
      message.error('获取流程列表失败')
    })
  }
})

async function confirmFlowSelect() {
  if (!selectedFlowId.value) {
    message.warning('请选择流程定义')
    return
  }
  if (!unmatchedBatchInfo.value) return

  flowSelectLoading.value = true
  try {
    await assignPipeline(unmatchedBatchInfo.value.batchId, selectedFlowId.value)
    message.success('已指定流程，开始处理')
    flowSelectVisible.value = false
    onUnmatchedBatch.value = null
  } catch (e: any) {
    message.error(e?.message || '指定流程失败')
  } finally {
    flowSelectLoading.value = false
  }
}

// ===== 上传按钮 =====
function onBeforeUpload(file: File) {
  handleFileUpload(file)
  return false
}

// ===== 批量智能导入（多文件内容路由） =====
const autoFiles = ref<File[]>([])
const autoImporting = ref(false)
const autoResult = ref<UploadAutoResult | null>(null)

const routedColumns = [
  { title: '文件名', dataIndex: 'fileName', key: 'fileName', ellipsis: true },
  { title: '批次号', dataIndex: 'batchId', key: 'batchId', width: 120 },
  { title: '流程定义', dataIndex: 'flowDefinitionId', key: 'flowDefinitionId', width: 120 },
]

// 收集文件、阻止 a-upload 自动上传
function onAutoBeforeUpload(file: File) {
  autoFiles.value = [...autoFiles.value, file]
  return false
}

function removeAutoFile(idx: number) {
  autoFiles.value = autoFiles.value.filter((_, i) => i !== idx)
}

function clearAutoFiles() {
  autoFiles.value = []
}

async function startAutoImport() {
  if (autoFiles.value.length === 0) {
    message.warning('请先选择文件')
    return
  }
  autoImporting.value = true
  try {
    const result = await uploadAutoBatch(autoFiles.value)
    autoResult.value = result
    const routed = result.routed.length
    const unmatched = result.unmatched.length
    const ambiguous = result.ambiguous.length
    const triggerErrors = result.triggerErrors.length
    message.success(
      `路由完成：触发 ${routed}，待认领 ${unmatched}，多义 ${ambiguous}，触发失败 ${triggerErrors}`
    )
    // 触发成功后清空已选文件，刷新批次列表
    autoFiles.value = []
    if (routed > 0) {
      loadBatches()
    }
  } catch (e: any) {
    message.error(e?.message || '智能导入失败')
  } finally {
    autoImporting.value = false
  }
}

// ===== 批次操作分发 =====
function handleBatchAction(payload: { type: string; batchId: number; errorId?: number; text?: string }) {
  // 对于仅需 batchId 的操作（delete/retry 等），无需查找 batch 对象即可执行
  // 对于需要 batch 对象的操作（urge/assign/addComment），需要查找
  const needsBatchObject = ['urge', 'assign', 'transfer', 'addComment'].includes(payload.type)

  let batch: (typeof batches.value)[number] | undefined
  if (needsBatchObject) {
    batch = batches.value.find(b => b.id === payload.batchId)
    if (!batch) {
      console.warn('[UploadCenter] handleBatchAction: 找不到批次', payload.batchId, '当前批次数:', batches.value.length)
      message.warning('操作失败：找不到对应批次，请刷新页面重试')
      return
    }
  }

  switch (payload.type) {
    case 'retry':
      handleRetry(payload.batchId)
      break
    case 'recalculate':
      handleRecalculate(payload.batchId)
      break
    case 'revoke':
    case 'delete':
      handleDelete(payload.batchId)
      break
    case 'cancelUpload':
      // 取消上传本质上就是删除卡片，复用现有删除流程
      handleDelete(payload.batchId)
      break
    case 'viewErrors':
      // 卡片不再支持展开/折叠，无操作
      break
    case 'viewVoucher':
      message.info('查看凭证功能开发中')
      break
    case 'validateCalculation':
      router.push({ path: `/cardflow/import-validation/${payload.batchId}` })
      break
    case 'urge': {
      const handler = batch!.assigneeName && batch!.assigneeName !== '系统自动' ? batch!.assigneeName : '处理人'
      message.success(`已向${handler}发送催办通知`)
      break
    }
    case 'assign':
      openAssignModal(batch!, '分配')
      break
    case 'transfer':
      openAssignModal(batch!, '转交')
      break
    case 'addComment':
      if (payload.text && batch) {
        const now = new Date()
        const time = `${now.getHours()}:${String(now.getMinutes()).padStart(2, '0')}`
        batch.comments ??= []
        batch.comments.push({
          author: currentUserName.value,
          time,
          text: payload.text,
        })
      }
      break
    case 'ignoreError':
      message.info('忽略异常功能开发中')
      break
    case 'fixError':
      message.info('修正异常功能开发中')
      break
  }
}
</script>

<style scoped lang="scss">
.upload-center {
  padding: 0;
}

// 三合一面板
.unified-panel {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-md);
  overflow: hidden;
}

.header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border);
}

// 卡片列表
.card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
}

// 看板
.kanban-board {
  display: flex;
  gap: 12px;
  padding: 12px;
}

.kanban-column {
  flex: 1;
  border-radius: 8px;
  padding: 12px;
  min-height: 300px;
}

.kanban-header {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.kanban-count {
  background: color-mix(in srgb, var(--text-1) 6%, transparent);
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 12px;
  color: var(--text-3);
}

.kanban-card {
  background: var(--bg-card);
  border-radius: 6px;
  padding: 10px 12px;
  margin-bottom: 8px;
  box-shadow: var(--shadow-sm);
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    box-shadow: var(--shadow-md);
    transform: translateY(-1px);
  }

  &.stale {
    border-left: 3px solid var(--color-danger);
  }
}

.kanban-card-name {
  font-size: 13px;
  color: var(--text-1);
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-bottom: 6px;
}

.kanban-card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--text-3);
}

.kanban-card-handler {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 6px;
  font-size: 12px;
  color: var(--text-3);
}

.handler-avatar-mini {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  color: var(--text-on-accent);
}

.card-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;

  &.uploading, &.processing { background: var(--color-info); animation: pulse 1.5s infinite; }
  &.pending { background: var(--color-warning); }
  &.error { background: var(--color-danger); }
  &.success { background: var(--color-success); }
  &.partial { background: var(--color-warning); }
}

.wait-time {
  font-size: 12px;
  color: var(--text-3);
  &.warning { color: var(--color-danger-text); font-weight: 600; }
}

// 分配弹窗
.assign-modal-body {
  p {
    font-size: 13px;
    color: var(--text-2);
    margin-bottom: 4px;
  }
}

.inline-upload {
  display: inline-block;
}

// 批量智能导入区
.auto-import-panel {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);
  padding: 14px 20px;
  margin-bottom: 8px;
}

.auto-import-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}

.auto-import-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
  display: flex;
  align-items: center;
}

.auto-import-hint {
  font-size: 12px;
  font-weight: 400;
  color: var(--text-3);
  margin-left: 10px;
}

.auto-import-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.auto-file-list {
  margin-top: 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.auto-file-tag {
  display: inline-flex;
  align-items: center;
  max-width: 280px;
  margin: 0;
}

.auto-result {
  margin-top: 16px;
  border-top: 1px solid var(--border);
  padding-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.auto-result-section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.auto-result-count {
  font-size: 13px;
  color: var(--text-2);
}

.auto-cols-label {
  font-size: 12px;
  color: var(--text-3);
  margin-right: 4px;
}

.auto-col-tag {
  margin: 2px 4px 2px 0;
}

.auto-empty-cols {
  font-size: 12px;
  color: var(--text-3);
}

.auto-error-text {
  font-size: 12px;
  color: var(--color-danger-text);
}

.toolbar-btn {
  background: var(--color-primary-light);
  border: none;
  color: var(--color-primary);
  &:hover {
    background: var(--color-primary-border);
    color: var(--color-primary-hover);
  }
}

// 组织预检提示栏
.org-preview-bar {
  padding: 8px 20px;
  background: var(--bg-card);
  border-radius: 8px;
  margin-bottom: 8px;
  box-shadow: var(--shadow-sm);
}

.toolbar-account-set {
  font-size: 16px;
  font-weight: 500;
  color: var(--text-1);
}

.org-preview-warnings {
  font-size: 13px;
  color: var(--color-danger-text);
  display: flex;
  align-items: center;
  gap: 4px;
}

.warning-text {
  margin-right: 8px;
}

// 回收站
.recycle-empty {
  padding: 60px 0;
  text-align: center;
}

.recycle-list {
  display: flex;
  flex-direction: column;
  gap: 1px;
  background: var(--border);
  border-radius: 8px;
  overflow: hidden;
}

.recycle-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--bg-card);
  transition: background 0.2s, opacity 0.2s;

  &:hover {
    background: var(--bg-muted);
  }

  &--deleting {
    opacity: 1;
    pointer-events: none;
    background: var(--color-warning-light);
    border-left: 3px solid var(--color-warning);
  }
}

.recycle-item-deleting-hint {
  display: flex;
  align-items: center;
  margin-top: 6px;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-warning-text);
}

.recycle-item-info {
  flex: 1;
  min-width: 0;
}

.recycle-item-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.recycle-batch-id {
  color: var(--color-info);
  font-size: 13px;
  font-weight: 600;
  margin-right: 6px;
}

.recycle-item-meta {
  font-size: 12px;
  color: var(--text-3);
  margin-top: 4px;
}

.recycle-item-actions {
  flex-shrink: 0;
  margin-left: 12px;
  display: flex;
  gap: 4px;
}

// 流程选择弹窗
.flow-select-modal-body {
  max-height: 400px;
  overflow-y: auto;
}

.flow-candidate-item {
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  margin-bottom: 8px;
  transition: border-color 0.2s;

  &:hover {
    border-color: var(--color-primary);
  }
}

.flow-candidate-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
}

.flow-candidate-code {
  font-size: 12px;
  color: var(--text-3);
  margin-left: 8px;
}

.flow-candidate-desc {
  font-size: 12px;
  color: var(--text-3);
  margin-top: 4px;
  padding-left: 22px;
}

</style>
