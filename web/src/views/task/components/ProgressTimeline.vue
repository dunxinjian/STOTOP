<template>
  <div class="progress-timeline">
    <a-spin :spinning="loading">
      <a-empty v-if="!loading && reports.length === 0" description="暂无进度上报记录" />
      <a-timeline v-else>
        <a-timeline-item
          v-for="report in reports"
          :key="report.id"
          :color="getTimelineColor(report.progress)"
        >
          <div class="progress-timeline__item">
            <div class="progress-timeline__header">
              <a-progress
                :percent="report.progress"
                :size="[120, 8]"
                :stroke-color="getProgressColor(report.progress)"
              />
              <span class="progress-timeline__reporter">{{ report.reporterName }}</span>
              <span class="progress-timeline__time">{{ formatTime(report.createTime) }}</span>
            </div>
            <div class="progress-timeline__content">{{ report.content }}</div>
            <div v-if="report.hours" class="progress-timeline__hours">
              投入工时：{{ report.hours }}h
            </div>
          </div>
        </a-timeline-item>
      </a-timeline>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { getProgressReports } from '@/api/task'
import type { ProgressReportListDto } from '@/types/task'
import dayjs from 'dayjs'

const props = defineProps<{
  taskId: number
}>()

const loading = ref(false)
const reports = ref<ProgressReportListDto[]>([])

function getTimelineColor(progress: number) {
  if (progress >= 100) return 'green'
  if (progress >= 50) return 'blue'
  return 'gray'
}

function getProgressColor(progress: number) {
  if (progress >= 100) return 'var(--color-success)'
  if (progress >= 50) return 'var(--color-info)'
  return 'var(--color-warning)'
}

function formatTime(time: string) {
  return dayjs(time).format('MM-DD HH:mm')
}

async function loadReports() {
  loading.value = true
  try {
    const res = await getProgressReports(props.taskId, { pageSize: 50, sortField: 'createTime', sortOrder: 'desc' })
    reports.value = res.items
  } catch {
    message.error('加载进度记录失败')
  } finally {
    loading.value = false
  }
}

watch(() => props.taskId, loadReports)
onMounted(loadReports)

defineExpose({ refresh: loadReports })
</script>

<style scoped lang="scss">
.progress-timeline {
  &__item {
    padding-bottom: 4px;
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
  }

  &__reporter {
    font-size: 13px;
    color: #595959;
    font-weight: 500;
  }

  &__time {
    font-size: 12px;
    color: #8c8c8c;
  }

  &__content {
    margin-top: 6px;
    font-size: 14px;
    color: #333;
    line-height: 1.6;
  }

  &__hours {
    margin-top: 4px;
    font-size: 12px;
    color: #8c8c8c;
  }
}
</style>
