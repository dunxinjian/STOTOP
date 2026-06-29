/**
 * 阿米巴模板页面快捷键 composable
 * - Ctrl+S 保存
 * - Ctrl+K 打开命令面板（如可用）
 * - Esc 取消选择
 * - ? 显示快捷键速查浮层
 *
 * 注意：必须在 onBeforeUnmount 时显式注销，避免污染其他页面
 */
import { onBeforeUnmount, onMounted, ref } from 'vue'

export interface TemplateShortcutsOptions {
  onSave?: () => void
  onSearch?: () => void
  onEscape?: () => void
  onCopy?: () => void
}

export function useTemplateShortcuts(options: TemplateShortcutsOptions) {
  const helpVisible = ref(false)

  function isInEditableTarget(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) return false
    const tag = target.tagName
    return (
      tag === 'INPUT' ||
      tag === 'TEXTAREA' ||
      target.isContentEditable ||
      target.getAttribute('role') === 'combobox'
    )
  }

  function handler(e: KeyboardEvent) {
    const ctrl = e.ctrlKey || e.metaKey
    const key = e.key.toLowerCase()

    // Ctrl+S 保存
    if (ctrl && key === 's') {
      e.preventDefault()
      options.onSave?.()
      return
    }

    // Ctrl+K 打开命令面板
    if (ctrl && key === 'k') {
      e.preventDefault()
      options.onSearch?.()
      return
    }

    // Ctrl+D 复制当前项
    if (ctrl && key === 'd') {
      e.preventDefault()
      options.onCopy?.()
      return
    }

    // Esc 取消（只在非输入态触发）
    if (key === 'escape' && !isInEditableTarget(e.target)) {
      options.onEscape?.()
      return
    }

    // ? 显示快捷键速查（非输入态、Shift+/）
    if (key === '?' && !isInEditableTarget(e.target)) {
      e.preventDefault()
      helpVisible.value = !helpVisible.value
      return
    }
  }

  onMounted(() => {
    window.addEventListener('keydown', handler)
  })

  onBeforeUnmount(() => {
    window.removeEventListener('keydown', handler)
  })

  return { helpVisible }
}
