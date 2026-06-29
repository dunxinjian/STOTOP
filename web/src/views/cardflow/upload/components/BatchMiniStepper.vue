<template>
  <div v-if="plugins && plugins.length" class="batch-mini-stepper">
    <template v-for="(plugin, index) in plugins" :key="index">
      <!-- 节点 -->
      <div class="step-item">
        <!-- 圆点 / 状态图标 -->
        <a-tooltip
          v-if="plugin.status === 'Failed'"
          :title="plugin.currentStepName || plugin.pluginName"
          placement="top"
        >
          <span class="dot dot--failed">✕</span>
        </a-tooltip>
        <span v-else-if="plugin.status === 'Running'" class="dot dot--running dot--pulse" />
        <span v-else-if="plugin.status === 'Completed'" class="dot dot--completed" />
        <span v-else class="dot dot--pending" />

        <!-- 节点名 -->
        <span
          class="step-label"
          :class="{
            'step-label--completed': plugin.status === 'Completed',
            'step-label--running': plugin.status === 'Running',
            'step-label--failed': plugin.status === 'Failed',
            'step-label--pending': plugin.status === 'Pending' || !['Completed','Running','Failed'].includes(plugin.status),
          }"
        >
          {{ abbr(plugin.pluginName) }}
          <template v-if="plugin.status === 'Running' && plugin.currentStepIndex != null && plugin.totalSteps">
            <span class="step-progress">{{ plugin.currentStepIndex }}/{{ plugin.totalSteps }}</span>
          </template>
        </span>

        <div v-if="plugin.status === 'Running' && hasDataProgress(plugin)" class="plugin-data-progress">
          <a-progress
            :percent="plugin.dataProgressPercent || 0"
            :show-info="false"
            size="small"
          />
          <span class="plugin-data-progress__text">{{ dataProgressText(plugin) }}</span>
        </div>
      </div>

      <!-- 连线（最后一个节点后不渲染） -->
      <div
        v-if="index < plugins.length - 1"
        class="step-line"
        :class="{
          'step-line--completed': plugin.status === 'Completed',
          'step-line--running': plugin.status === 'Running',
        }"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
import type { AutoPluginTrailItemDto } from '@/stores/batchStore'

defineProps<{
  plugins?: AutoPluginTrailItemDto[]
}>()

/** 显示插件节点名称（完整显示） */
function abbr(name: string): string {
  return name ?? ''
}

function hasDataProgress(plugin: AutoPluginTrailItemDto): boolean {
  return plugin.dataProgressPercent != null || !!plugin.dataProgressTotal
}

function dataProgressText(plugin: AutoPluginTrailItemDto): string {
  const percent = plugin.dataProgressPercent ?? 0
  if (plugin.dataProgressTotal && plugin.dataProgressTotal > 0) {
    return `${plugin.dataProcessedCount ?? 0}/${plugin.dataProgressTotal}`
  }
  return `${percent}%`
}
</script>

<style scoped>
.batch-mini-stepper {
  display: flex;
  align-items: flex-start;
  min-height: 36px;
  padding-left: 40px;
  padding-bottom: 6px;
  font-size: 12px;
  overflow: hidden;
}

/* 单个步骤（圆点 + 标签垂直居中） */
.step-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
  max-width: 180px;
}

/* 连线 */
.step-line {
  flex: 1;
  height: 2px;
  min-width: 12px;
  background-color: var(--border);
  align-self: flex-start;
  margin-top: 3px; /* 让连线垂直对齐圆点中心 */
}
.step-line--completed {
  background-color: var(--color-success);
}
.step-line--running {
  background-color: var(--color-info);
}

/* 圆点基础 */
.dot {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot--completed {
  background-color: var(--color-success);
}

.dot--running {
  background-color: var(--color-info);
}

.dot--failed {
  width: auto;
  height: auto;
  border-radius: 0;
  font-size: 10px;
  font-weight: bold;
  color: var(--color-danger-text);
  line-height: 8px;
  cursor: default;
}

.dot--pending {
  background-color: transparent;
  border: 1.5px solid var(--border);
}

/* Pulse 动画 */
.dot--pulse {
  animation: pulse 1.4s ease-in-out infinite;
}
@keyframes pulse {
  0%, 100% {
    box-shadow: 0 0 0 0 var(--color-primary-border);
  }
  50% {
    box-shadow: 0 0 0 4px transparent;
  }
}

/* 节点标签 */
.step-label {
  font-size: 12px;
  white-space: nowrap;
  line-height: 1;
}
.step-label--completed {
  color: var(--color-success-text);
}
.step-label--running {
  color: var(--color-info);
}
.step-label--failed {
  color: var(--color-danger-text);
  cursor: default;
}
.step-label--pending {
  color: var(--text-3);
}

/* 进度子标签 */
.step-progress {
  font-size: 10px;
  opacity: 0.75;
  margin-left: 1px;
}

.plugin-data-progress {
  display: grid;
  grid-template-columns: minmax(56px, 88px) auto;
  align-items: center;
  gap: 4px;
  width: 128px;
  min-height: 14px;
}

.plugin-data-progress :deep(.ant-progress) {
  line-height: 1;
}

.plugin-data-progress__text {
  font-size: 10px;
  color: var(--text-2);
  white-space: nowrap;
}
</style>
