import { onMounted, onUnmounted } from 'vue'
import { keyboardManager } from '@/utils/keyboardManager'

/**
 * Vue Composable：在组件中快速注册页面级快捷键
 * 组件挂载时注册，卸载时自动注销
 */
export function useShortcut(
  key: string,
  handler: (e: KeyboardEvent) => void,
  options?: { label?: string; description?: string; scope?: 'global' | 'page' }
) {
  let unregister: (() => void) | null = null

  onMounted(() => {
    unregister = keyboardManager.register({
      key,
      label: options?.label ?? key,
      description: options?.description ?? '',
      scope: options?.scope ?? 'page',
      handler,
    })
  })

  onUnmounted(() => {
    unregister?.()
    unregister = null
  })
}
