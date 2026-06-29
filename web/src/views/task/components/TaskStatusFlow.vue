<template>
  <div class="task-status-flow">
    <a-steps :current="currentIndex" size="small" @change="handleStepClick">
      <a-step
        v-for="step in steps"
        :key="step.status"
        :title="step.label"
        :status="getStepStatus(step.status)"
        :disabled="loading"
      />
    </a-steps>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { changeTaskStatus } from '@/api/task'

const props = defineProps<{
  currentStatus: number
  taskId: number
}>()

const emit = defineEmits<{
  (e: 'change', status: number): void
}>()

const loading = ref(false)

// Status: 0=待处理, 1=进行中, 2=已完成, 3=已取消, 4=已暂停
const steps = [
  { status: 0, label: '待处理' },
  { status: 1, label: '进行中' },
  { status: 2, label: '已完成' },
]

const currentIndex = computed(() => {
  const idx = steps.findIndex((s) => s.status === props.currentStatus)
  return idx >= 0 ? idx : 0
})

function getStepStatus(status: number): 'wait' | 'process' | 'finish' | 'error' {
  if (props.currentStatus === 3) return 'error'
  if (status < props.currentStatus) return 'finish'
  if (status === props.currentStatus) return 'process'
  return 'wait'
}

async function handleStepClick(stepIndex: number) {
  const targetStatus = steps[stepIndex].status
  if (targetStatus === props.currentStatus) return
  const targetLabel = steps[stepIndex].label
  Modal.confirm({
    title: '确认变更状态',
    content: `确定将任务状态变更为「${targetLabel}」吗？`,
    async onOk() {
      loading.value = true
      try {
        await changeTaskStatus(props.taskId, { status: targetStatus })
        message.success(`任务状态已变更为「${targetLabel}」`)
        emit('change', targetStatus)
      } catch {
        message.error('状态变更失败')
      } finally {
        loading.value = false
      }
    },
  })
}
</script>

<style scoped lang="scss">
.task-status-flow {
  padding: 8px 0;
}
</style>
