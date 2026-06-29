import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  getFlowDefinitions,
  getFlowDefinition,
  getMyTodos,
  getTodoCount,
  getAvailableFlows,
} from '@/api/cardflow'
import type {
  FlowDefinitionDto,
  FlowDefinitionQueryRequest,
  TodoItemDto,
  TodoQueryRequest,
  TodoCountDto,
  AvailableFlowDto,
  PagedResult,
} from '@/types/cardflow'

// ===== sessionStorage 缓存常量 =====
const DEFINITIONS_CACHE_KEY = 'stotop_cardflow_definitions_cache'
const TODO_COUNT_CACHE_KEY = 'stotop_cardflow_todo_count_cache'
const CACHE_MAX_AGE = 5 * 60 * 1000 // 5 分钟

interface CacheItem<T> {
  data: T
  timestamp: number
}

function getCache<T>(key: string): T | null {
  try {
    const cached = sessionStorage.getItem(key)
    if (cached) {
      const { data, timestamp } = JSON.parse(cached) as CacheItem<T>
      if (Date.now() - timestamp < CACHE_MAX_AGE) {
        return data
      }
      sessionStorage.removeItem(key)
    }
  } catch { /* 静默 */ }
  return null
}

function setCache<T>(key: string, data: T): void {
  try {
    sessionStorage.setItem(key, JSON.stringify({ data, timestamp: Date.now() }))
  } catch { /* 静默 */ }
}

export const useCardFlowStore = defineStore('cardflow', () => {
  // ===== 状态 =====
  const definitions = ref<FlowDefinitionDto[]>([])
  const definitionsTotal = ref(0)
  const currentDefinition = ref<FlowDefinitionDto | null>(null)
  const todoList = ref<TodoItemDto[]>([])
  const todoTotal = ref(0)
  const todoCount = ref<TodoCountDto>({ todo: 0, initiated: 0, cc: 0 })
  const availableFlows = ref<AvailableFlowDto[]>([])
  const loading = ref(false)
  const signalRConnected = ref(false)

  // ===== Actions =====

  /** 加载流程定义列表 */
  async function loadDefinitions(params?: FlowDefinitionQueryRequest) {
    // 无筛选条件时尝试读取缓存
    if (!params || (!params.keyword && !params.status)) {
      const cached = getCache<PagedResult<FlowDefinitionDto>>(DEFINITIONS_CACHE_KEY)
      if (cached) {
        definitions.value = cached.items
        definitionsTotal.value = cached.total
        return cached
      }
    }

    loading.value = true
    try {
      const result = await getFlowDefinitions(params)
      definitions.value = result.items
      definitionsTotal.value = result.total
      // 缓存无筛选结果
      if (!params || (!params.keyword && !params.status)) {
        setCache(DEFINITIONS_CACHE_KEY, result)
      }
      return result
    } finally {
      loading.value = false
    }
  }

  /** 加载流程定义详情 */
  async function loadDefinition(id: number) {
    loading.value = true
    try {
      const result = await getFlowDefinition(id)
      currentDefinition.value = result
      return result
    } finally {
      loading.value = false
    }
  }

  /** 加载待办列表 */
  async function loadTodos(params?: TodoQueryRequest) {
    loading.value = true
    try {
      const result = await getMyTodos(params)
      todoList.value = result.items
      todoTotal.value = result.total
      return result
    } finally {
      loading.value = false
    }
  }

  /** 加载待办数量 */
  async function loadTodoCount() {
    const cached = getCache<TodoCountDto>(TODO_COUNT_CACHE_KEY)
    if (cached) {
      todoCount.value = cached
      return cached
    }

    const result = await getTodoCount()
    todoCount.value = result
    setCache(TODO_COUNT_CACHE_KEY, result)
    return result
  }

  /** 加载可发起的流程列表 */
  async function loadAvailableFlows(orgId: number) {
    const result = await getAvailableFlows(orgId)
    availableFlows.value = result
    return result
  }

  /** 清除缓存 */
  function clearCache() {
    try {
      sessionStorage.removeItem(DEFINITIONS_CACHE_KEY)
      sessionStorage.removeItem(TODO_COUNT_CACHE_KEY)
    } catch { /* 静默 */ }
  }

  /** 刷新待办数量（卡片操作后调用） */
  async function refreshTodoCount() {
    try { sessionStorage.removeItem(TODO_COUNT_CACHE_KEY) } catch { /* 静默 */ }
    return loadTodoCount()
  }

  /** 设置 SignalR 连接状态 */
  function setSignalRConnected(connected: boolean) {
    signalRConnected.value = connected
  }

  /** 重置状态 */
  function resetState() {
    definitions.value = []
    definitionsTotal.value = 0
    currentDefinition.value = null
    todoList.value = []
    todoTotal.value = 0
    todoCount.value = { todo: 0, initiated: 0, cc: 0 }
    availableFlows.value = []
    loading.value = false
    signalRConnected.value = false
    clearCache()
  }

  return {
    // 状态
    definitions,
    definitionsTotal,
    currentDefinition,
    todoList,
    todoTotal,
    todoCount,
    availableFlows,
    loading,
    signalRConnected,
    // Actions
    loadDefinitions,
    loadDefinition,
    loadTodos,
    loadTodoCount,
    loadAvailableFlows,
    clearCache,
    refreshTodoCount,
    setSignalRConnected,
    resetState,
  }
})
