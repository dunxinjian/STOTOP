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
  /** 每步的语义标签（与 stack 同步增删；[0]=初始态；C6 历史下拉消费） */
  const labels = ref<string[]>(['初始状态'])
  /** 下一次 commit 使用的标签（结构化操作入口先 setNextLabel 再触发变更） */
  let pendingLabel: string | null = null

  const canUndo = computed(() => cursor.value > 0)
  const canRedo = computed(() => cursor.value < stack.value.length - 1)

  function setNextLabel(label: string) {
    pendingLabel = label
  }

  /** 提交一次新状态（会丢弃当前 cursor 之后的 redo 分支） */
  function commit(state: T) {
    const last = stack.value[cursor.value] as T | undefined
    if (last !== undefined && equals(last, state)) return
    // 丢弃 redo 分支
    if (cursor.value < stack.value.length - 1) {
      stack.value = stack.value.slice(0, cursor.value + 1)
      labels.value = labels.value.slice(0, cursor.value + 1)
    }
    ;(stack.value as T[]).push(clone(state))
    labels.value.push(pendingLabel || '编辑')
    pendingLabel = null
    if (stack.value.length > maxSteps) {
      stack.value.shift()
      labels.value.shift()
    } else {
      cursor.value++
    }
  }

  /** 重置到初始状态（清空历史） */
  function reset(state: T) {
    stack.value = [clone(state)] as T[]
    labels.value = ['初始状态']
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

  /** 跳回历史任意步（C6 历史下拉）：返回目标状态，调用方负责写回 */
  function jumpTo(index: number): T | null {
    if (index < 0 || index >= stack.value.length || index === cursor.value) return null
    cursor.value = index
    return clone(stack.value[index] as T)
  }

  return { commit, reset, undo, redo, jumpTo, setNextLabel, canUndo, canRedo, stack, cursor, labels }
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
    timer = setTimeout(() => { timer = null; history.commit(val) }, delay)
  }, { deep: true })

  /** 立即提交防抖窗口内未入栈的编辑；undo/redo 前必须调用，否则最新编辑被跳过且无法 redo */
  function flushPending() {
    if (!timer) return
    clearTimeout(timer)
    timer = null
    history.commit(getter())
  }

  /** 在编程式回写状态时短暂关闭采集，避免 redo/undo 自身被记录 */
  async function silently(fn: () => void | Promise<void>) {
    // 回写前丢弃采集窗口内的旧定时器，避免它在 suppress 释放后把回写前的状态误提交
    if (timer) { clearTimeout(timer); timer = null }
    suppress.value = true
    try {
      await fn()
    } finally {
      // 等下一帧再放开
      setTimeout(() => { suppress.value = false }, 50)
    }
  }

  return { silently, flushPending }
}
