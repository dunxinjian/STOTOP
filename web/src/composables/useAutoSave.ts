import { ref, watch, onMounted, onBeforeUnmount } from 'vue'

export type SaveState = 'saved' | 'saving' | 'dirty' | 'error'

export interface UseAutoSaveOptions {
  /** 自动保存间隔（毫秒），默认 30s */
  intervalMs?: number
  /** 是否启用 beforeunload 拦截 */
  blockUnload?: boolean
  /** 保存动作 */
  save: () => Promise<void>
  /** 是否处于"脏"状态 */
  isDirty: () => boolean
}

/**
 * 自动保存草稿 composable
 * - 周期性轮询 dirty 标记，dirty 时自动调用 save()
 * - 暴露 saveState 供 UI 显示（已保存 / 保存中 / 未保存 / 错误）
 * - 注册 beforeunload 拦截未保存离开
 */
export function useAutoSave(opts: UseAutoSaveOptions) {
  const interval = opts.intervalMs ?? 30_000
  const saveState = ref<SaveState>('saved')
  const lastSavedAt = ref<Date | null>(null)
  let timer: ReturnType<typeof setInterval> | null = null

  function markDirty() {
    if (saveState.value !== 'saving') saveState.value = 'dirty'
  }

  async function flush() {
    if (saveState.value === 'saving') return
    if (!opts.isDirty()) return
    saveState.value = 'saving'
    try {
      await opts.save()
      saveState.value = 'saved'
      lastSavedAt.value = new Date()
    } catch (e) {
      saveState.value = 'error'
    }
  }

  function onBeforeUnload(e: BeforeUnloadEvent) {
    if (opts.isDirty() || saveState.value === 'dirty') {
      e.preventDefault()
      e.returnValue = '有未保存的更改，确定离开？'
      return e.returnValue
    }
  }

  onMounted(() => {
    timer = setInterval(() => { void flush() }, interval)
    if (opts.blockUnload !== false) {
      window.addEventListener('beforeunload', onBeforeUnload)
    }
  })

  onBeforeUnmount(() => {
    if (timer) clearInterval(timer)
    window.removeEventListener('beforeunload', onBeforeUnload)
  })

  return { saveState, lastSavedAt, flush, markDirty }
}
