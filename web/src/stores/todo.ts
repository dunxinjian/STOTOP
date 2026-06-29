import { defineStore } from 'pinia'
import { ref } from 'vue'
import { get } from '@/api/request'

export const useTodoStore = defineStore('todo', () => {
  const todoCount = ref(0)
  let timer: number | null = null

  async function fetchTodoCount() {
    try {
      const res = await get<any>('/user/todo-count', {}, { silent: true } as any)
      todoCount.value = Number(res?.total ?? res?.Total ?? 0) || 0
    } catch { /* ignore */ }
  }

  function startPolling() {
    fetchTodoCount()
    timer = window.setInterval(fetchTodoCount, 30000)
  }

  function stopPolling() {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  return { todoCount, fetchTodoCount, startPolling, stopPolling }
})
