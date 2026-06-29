import { defineStore } from 'pinia'
import { ref } from 'vue'
import { get } from '@/api/request'

export interface TodoCountSummary {
  uploadProcessing: number
  uploadException: number
  uploadStalled: number
  oaTodoCount: number
  total: number
}

export const useNotificationStore = defineStore('notification', () => {
  const todoCount = ref<TodoCountSummary>({
    uploadProcessing: 0,
    uploadException: 0,
    uploadStalled: 0,
    oaTodoCount: 0,
    total: 0
  })

  let timer: ReturnType<typeof setInterval> | null = null

  async function fetchTodoCount() {
    try {
      const res = await get<TodoCountSummary>('/user/todo-count')
      if (res) {
        todoCount.value = res
      }
    } catch {
      // 静默失败，不影响用户体验
    }
  }

  function startPolling(intervalMs = 60000) {
    stopPolling()
    fetchTodoCount()
    timer = setInterval(fetchTodoCount, intervalMs)
  }

  function stopPolling() {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  return { todoCount, fetchTodoCount, startPolling, stopPolling }
})
