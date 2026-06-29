<script setup lang="ts">
/**
 * BatchProgressPanel.vue — 批次进度面板
 *
 * - 环形进度（已完成/总数）
 * - 各阶段进度条
 * - 失败明细快速入口
 * - 5s 轮询刷新（运行中状态）
 */
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { ExclamationCircleOutlined } from '@ant-design/icons-vue'
import { getBatchProgress } from '@/api/cardflow'
import type { BatchProgressDto, CfBatchStatus } from '@/types/cardflow'

interface Props {
  batchId: number
  /** 父组件传入的批次状态，用于决定是否轮询 */
  batchStatus?: CfBatchStatus
  /** 轮询间隔（ms） */
  pollInterval?: number
}

const props = withDefaults(defineProps<Props>(), {
  pollInterval: 5000,
})

const emit = defineEmits<{
  (e: 'view-failures'): void
}>()

const progress = ref<BatchProgressDto | null>(null)
const loading = ref(false)
let timer: any = null

const completedRatio = computed(() => {
  if (!progress.value || progress.value.totalCards === 0) return 0
  return Math.floor((progress.value.completed / progress.value.totalCards) * 100)
})

const isRunning = computed(() => {
  // 0 解析中 / 2 质检中 / 4 处理中 → 轮询
  return props.batchStatus === 0 || props.batchStatus === 2 || props.batchStatus === 4
})

async function load() {
  loading.value = true
  try {
    progress.value = await getBatchProgress(props.batchId)
  } catch {
    // 静默失败，避免轮询提示扰民
  } finally {
    loading.value = false
  }
}

function startPolling() {
  stopPolling()
  if (!isRunning.value) return
  timer = setInterval(load, props.pollInterval)
}

function stopPolling() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

onMounted(() => {
  load()
  startPolling()
})

onBeforeUnmount(stopPolling)

watch(() => props.batchId, () => {
  load()
  startPolling()
})

watch(isRunning, (running) => {
  if (running) startPolling()
  else stopPolling()
})

const stageEntries = computed(() =>
  progress.value ? Object.entries(progress.value.stageProgress || {}) : [],
)
</script>

<template>
  <div class="cf-batch-progress">
    <a-spin :spinning="loading && !progress">
      <div v-if="progress" class="cf-batch-progress__body">
        <div class="cf-batch-progress__overview">
          <a-progress
            type="circle"
            :percent="completedRatio"
            :width="120"
            :stroke-color="completedRatio === 100 ? 'var(--color-success)' : 'var(--color-info)'"
          />
          <div class="cf-batch-progress__stats">
            <div class="cf-batch-progress__stat">
              <span class="cf-batch-progress__stat-label">总卡片</span>
              <span class="cf-batch-progress__stat-value">{{ progress.totalCards }}</span>
            </div>
            <div class="cf-batch-progress__stat">
              <span class="cf-batch-progress__stat-label">已完成</span>
              <span class="cf-batch-progress__stat-value cf-batch-progress__stat-value--ok">
                {{ progress.completed }}
              </span>
            </div>
            <div class="cf-batch-progress__stat">
              <span class="cf-batch-progress__stat-label">失败</span>
              <span class="cf-batch-progress__stat-value cf-batch-progress__stat-value--fail">
                {{ progress.failed }}
              </span>
              <a v-if="progress.failed > 0" class="cf-batch-progress__view-fail" @click="emit('view-failures')">
                <exclamation-circle-outlined />
                查看
              </a>
            </div>
          </div>
        </div>

        <div v-if="stageEntries.length > 0" class="cf-batch-progress__stages">
          <div class="cf-batch-progress__section-title">各阶段进度</div>
          <div
            v-for="[stage, completed] in stageEntries"
            :key="stage"
            class="cf-batch-progress__stage-row"
          >
            <span class="cf-batch-progress__stage-name">{{ stage }}</span>
            <a-progress
              :percent="progress.totalCards ? Math.floor((completed / progress.totalCards) * 100) : 0"
              :format="() => `${completed}/${progress!.totalCards}`"
              size="small"
              style="flex: 1"
            />
          </div>
        </div>
      </div>
      <a-empty v-else-if="!loading" description="暂无进度数据" />
    </a-spin>
  </div>
</template>

<style scoped lang="scss">
.cf-batch-progress {
  padding: 16px;

  &__body {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  &__overview {
    display: flex;
    align-items: center;
    gap: 24px;
  }

  &__stats {
    display: flex;
    flex-direction: column;
    gap: 8px;
    flex: 1;
  }

  &__stat {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  &__stat-label {
    width: 64px;
    color: var(--text-2);
    font-size: 13px;
  }

  &__stat-value {
    font-size: 18px;
    font-weight: 600;
    color: var(--text-1);

    &--ok { color: var(--color-success); }
    &--fail { color: var(--color-danger); }
  }

  &__view-fail {
    margin-left: 12px;
    color: var(--color-danger);
    cursor: pointer;
    font-size: 13px;
  }

  &__section-title {
    font-size: 14px;
    color: var(--text-1);
    font-weight: 500;
    margin-bottom: 8px;
  }

  &__stage-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 6px 0;
  }

  &__stage-name {
    width: 120px;
    color: var(--text-2);
    font-size: 13px;
  }
}
</style>
