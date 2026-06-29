<template>
  <a-button
    :type="pushed ? 'default' : 'primary'"
    :loading="loading"
    :disabled="pushed"
    size="small"
    @click="handlePush"
  >
    <template #icon><SendOutlined /></template>
    {{ pushed ? '已推送' : '推送到钉钉' }}
  </a-button>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { message } from 'ant-design-vue'
import { SendOutlined } from '@ant-design/icons-vue'

const props = defineProps<{
  taskId: number
  taskTitle: string
  pushApi?: (taskId: number) => Promise<any>
}>()

const emit = defineEmits<{
  (e: 'pushed'): void
}>()

const loading = ref(false)
const pushed = ref(false)

async function handlePush() {
  if (!props.pushApi) {
    message.warning('推送接口未配置')
    return
  }
  loading.value = true
  try {
    await props.pushApi(props.taskId)
    pushed.value = true
    message.success(`「${props.taskTitle}」已推送到钉钉`)
    emit('pushed')
  } catch {
    message.error('钉钉推送失败')
  } finally {
    loading.value = false
  }
}
</script>
