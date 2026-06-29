<template>
  <div class="batch-card" :class="cardClasses">
    <!-- 第一行：核心信息 -->
    <div class="card-row1">
      <a-checkbox
        v-if="selectable"
        :checked="checked"
        @change="emit('check', batch.id)"
        @click.stop
      />
      <span class="batch-id">#{{ batch.id }}</span>
      <a-tooltip :title="batch.fileName">
        <span class="file-name">{{ batch.fileName }}</span>
      </a-tooltip>
      <span class="file-meta">
        <span v-if="fileSizeText" class="meta-item">{{ fileSizeText }}</span>
        <span v-if="timeAgoText" class="meta-item">{{ timeAgoText }}</span>
        <span v-if="batch.flowName" class="meta-item meta-flow">{{ batch.flowName }}</span>
      </span>
      <!-- 状态标签 -->
      <span class="status-tag" :class="`status-tag--${batch.status}`">
        {{ STATUS_LABEL[batch.status] ?? batch.status }}
      </span>
      <div class="progress-section">
        <a-progress
          :percent="progressInfo.percent"
          :stroke-color="progressInfo.color"
          :show-info="false"
          size="small"
        />
        <span class="progress-text">{{ progressText }}</span>
      </div>
      <div class="action-buttons" @click.stop>
        <!-- pendingPipeline: 指定管道 -->
        <a-tooltip title="指定管道" v-if="(batch.status as string) === 'pendingPipeline'">
          <a-button type="text" size="small" @click="emit('action', { type: 'assignPipeline', batchId: batch.id })">
            <template #icon><BranchesOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="验证计算" v-if="batch.status !== 'uploading'">
          <a-button type="text" size="small" @click="emit('action', { type: 'validateCalculation', batchId: batch.id })">
            <template #icon><CalculatorOutlined /></template>
          </a-button>
        </a-tooltip>
        <!-- processing: 撤销 -->
        <a-tooltip title="撤销" v-if="batch.status === 'processing'">
          <a-button type="text" size="small" @click="emit('action', { type: 'revoke', batchId: batch.id })">
            <template #icon><StopOutlined /></template>
          </a-button>
        </a-tooltip>
        <!-- error/partial: 重试 + 删除 -->
        <template v-if="batch.status === 'error' || batch.status === 'partial'">
          <a-tooltip title="重试">
            <a-button type="text" size="small" @click="emit('action', { type: 'retry', batchId: batch.id })">
              <template #icon><RedoOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="删除">
            <a-button type="text" size="small" danger @click="emit('action', { type: 'delete', batchId: batch.id })">
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </a-tooltip>
        </template>
        <!-- success: 删除 -->
        <a-tooltip title="删除" v-if="batch.status === 'success'">
          <a-button type="text" size="small" @click="emit('action', { type: 'delete', batchId: batch.id })">
            <template #icon><DeleteOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 第二行：插件步骤轨迹（非终态 + 非错误 + 有 plugins 时显示） -->
    <BatchMiniStepper
      v-if="!isTerminal && batch.status !== 'error' && batch.autoPluginTrail?.autoPlugins?.length"
      :plugins="batch.autoPluginTrail!.autoPlugins"
    />
    <!-- error 状态优先显示错误信息 -->
    <div
      class="error-fallback"
      v-else-if="batch.status === 'error' && batch.errorMessage"
    >
      <WarningOutlined style="color: var(--color-danger); margin-right: 4px" />
      <a-tooltip :title="batch.errorMessage">
        <span class="error-text">{{ batch.errorMessage }}</span>
      </a-tooltip>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  StopOutlined,
  RedoOutlined,
  DeleteOutlined,
  BranchesOutlined,
  WarningOutlined,
  CalculatorOutlined,
} from '@ant-design/icons-vue'
import BatchMiniStepper from './BatchMiniStepper.vue'
import { getProgressBarInfo } from '../utils/batchStatus'
import type { BatchItem } from '@/stores/batchStore'

/** 状态标签文本映射 */
const STATUS_LABEL: Record<string, string> = {
  uploading: '上传中',
  processing: '处理中',
  pending: '待确认',
  error: '失败',
  success: '已完成',
  partial: '部分完成',
}

const props = withDefaults(defineProps<{
  batch: BatchItem
  selectable?: boolean
  checked?: boolean
}>(), {
  selectable: false,
  checked: false,
})

const emit = defineEmits<{
  action: [payload: { type: string; batchId: number; errorId?: number; text?: string }]
  check: [id: number]
}>()

const progressInfo = computed(() => getProgressBarInfo(props.batch))

/** 终态判断：已完成/部分完成时不再显示第二行 */
const isTerminal = computed(() => ['success', 'partial'].includes(props.batch.status))

/** 文件大小格式化 */
const fileSizeText = computed(() => {
  const size = props.batch.fileSize
  if (!size) return ''
  if (size < 1024) return `${size}B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(0)}KB`
  return `${(size / 1024 / 1024).toFixed(1)}MB`
})

/** 相对时间 */
const timeAgoText = computed(() => {
  if (!props.batch.createTime) return ''
  const diff = Date.now() - new Date(props.batch.createTime).getTime()
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}小时前`
  const days = Math.floor(hours / 24)
  return `${days}天前`
})

const processedRows = computed(() => props.batch.processedRows ?? 0)
const totalRows = computed(() => props.batch.totalRows ?? 0)
const runningPlugin = computed(() => props.batch.autoPluginTrail?.autoPlugins?.find(plugin => plugin.status === 'Running'))

const progressText = computed(() => {
  if (props.batch.status === 'uploading') return `${props.batch.uploadProgress ?? 0}%`
  if ((props.batch.status as string) === 'pendingPipeline') return ''
  const plugin = runningPlugin.value
  if (plugin?.dataProgressTotal && plugin.dataProgressTotal > 0) {
    return `${plugin.dataProcessedCount ?? 0}/${plugin.dataProgressTotal}`
  }
  if (totalRows.value > 0) return `${processedRows.value}/${totalRows.value}`
  return ''
})

const cardClasses = computed(() => ({
  'batch-card--processing': props.batch.status === 'processing',
  'batch-card--error': props.batch.status === 'error',
  'batch-card--success': props.batch.status === 'success',
  'batch-card--partial': props.batch.status === 'partial',
}))
</script>

<style scoped lang="scss">
.batch-card {
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 0 16px;
  margin-bottom: 8px;
  background: var(--bg-card);
  transition: border-color 0.2s;

  &--error { border-left: 3px solid var(--color-danger); }
  &--partial { border-left: 3px solid var(--color-warning); }
  &--processing { border-left: 3px solid var(--color-info); }
  &--success { border-left: 3px solid var(--color-success); }
}

.card-row1 {
  display: flex;
  align-items: center;
  height: 44px;
  gap: 12px;
}

.batch-id {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  color: var(--text-3);
  font-size: 12px;
  flex-shrink: 0;
}

.file-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
  min-width: 0;
  max-width: 200px;
  flex-shrink: 1;
}

.file-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  font-size: 12px;
  color: var(--text-3);

  .meta-item {
    white-space: nowrap;
  }

  .meta-flow {
    color: var(--color-info);
  }
}

/* 状态标签 */
.status-tag {
  border-radius: 4px;
  padding: 1px 7px;
  font-size: 11px;
  font-weight: 500;
  flex-shrink: 0;
  white-space: nowrap;

  &--processing {
    background: var(--color-info-light);
    color: var(--color-info);
  }
  &--uploading {
    background: var(--color-info-light);
    color: var(--color-info);
  }
  &--success {
    background: var(--color-success-light);
    color: var(--color-success-text);
  }
  &--error {
    background: var(--color-danger-light);
    color: var(--color-danger-text);
  }
  &--partial {
    background: var(--color-warning-light);
    color: var(--color-warning-text);
  }
  &--pending {
    background: var(--bg-muted);
    color: var(--text-3);
  }
}

.progress-section {
  width: 140px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 6px;

  :deep(.ant-progress) {
    flex: 1;
    min-width: 0;
  }
}

.progress-text {
  font-size: 12px;
  color: var(--text-2);
  white-space: nowrap;
}

.action-buttons {
  flex-shrink: 0;
  display: flex;
  gap: 2px;
  margin-left: auto;
}

.error-fallback {
  display: flex;
  align-items: center;
  height: 28px;
  padding-left: 40px;
  font-size: 12px;
  padding-bottom: 4px;
}

.error-text {
  color: var(--color-danger-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 400px;
}
</style>
