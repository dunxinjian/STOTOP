import { ref, computed, watch, type Ref } from 'vue'

/**
 * 通用撤销/重做 composable。
 * - 维护操作栈（最多 maxSteps 步），每步存储一个深拷贝快照。
 * - 调用方在执行每个原子操作后调用 commit() 推入新状态。
 * - undo() / redo() 返回目标状态，调用方负责把它写回响应式数据。
 */
export interface UseUndoRedoOptions<T> {
  /** 最大保留步数 */
  maxSteps?: number
  /** 自定义克隆函数（默认 JSON 深拷贝） */
  clone?: (val: T) => T
  /** 自定义相等比较（避免重复推入） */
  equals?: (a: T, b: T) => boolean
}

export function useUndoRedo<T>(initial: T, options: UseUndoRedoOptions<T> = {}) {
  const maxSteps = options.maxSteps ?? 50
  const clone = options.clone ?? ((v: T) => JSON.parse(JSON.stringify(v)) as T)
  const equals = options.equals ?? ((a: T, b: T) => JSON.stringify(a) === JSON.stringify(b))

  // 使用 any 避免 Vue 的 UnwrapRefSimple 深展开与泛型 T 不兼容
  const stack = ref<any[]>([clone(initial)]) as Ref<T[]>
  const cursor = ref(0)

  const canUndo = computed(() => cursor.value > 0)
  const canRedo = computed(() => cursor.value < stack.value.length - 1)

  /** 提交一次新状态（会丢弃当前 cursor 之后的 redo 分支） */
  function commit(state: T) {
    const last = stack.value[cursor.value] as T | undefined
    if (last !== undefined && equals(last, state)) return
    // 丢弃 redo 分支
    if (cursor.value < stack.value.length - 1) {
      stack.value = stack.value.slice(0, cursor.value + 1)
    }
    ;(stack.value as T[]).push(clone(state))
    if (stack.value.length > maxSteps) {
      stack.value.shift()
    } else {
      cursor.value++
    }
  }

  /** 重置到初始状态（清空历史） */
  function reset(state: T) {
    stack.value = [clone(state)] as T[]
    cursor.value = 0
  }

  function undo(): T | null {
    if (!canUndo.value) return null
    cursor.value--
    return clone(stack.value[cursor.value] as T)
  }

  function redo(): T | null {
    if (!canRedo.value) return null
    cursor.value++
    return clone(stack.value[cursor.value] as T)
  }

  return { commit, reset, undo, redo, canUndo, canRedo, stack, cursor }
}

/**
 * 监听响应式源，自动 commit 到撤销栈。
 * 防抖 500ms，避免高频输入产生大量步骤。
 */
export function useAutoCommit<T>(
  getter: () => T,
  history: ReturnType<typeof useUndoRedo<T>>,
  delay = 500,
) {
  let timer: ReturnType<typeof setTimeout> | null = null
  const suppress = ref(false)

  watch(getter, (val) => {
    if (suppress.value) return
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => history.commit(val), delay)
  }, { deep: true })

  /** 在编程式回写状态时短暂关闭采集，避免 redo/undo 自身被记录 */
  async function silently(fn: () => void | Promise<void>) {
    suppress.value = true
    try {
      await fn()
    } finally {
      // 等下一帧再放开
      setTimeout(() => { suppress.value = false }, 50)
    }
  }

  return { silently }
}
