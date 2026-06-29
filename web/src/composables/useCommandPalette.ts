import { ref } from 'vue'

/**
 * 命令面板（全局搜索）共享状态
 *
 * 设计目的：
 * - 顶栏点击搜索栏 / Ctrl+K 快捷键 / 任意按钮调用 → 都通过这里直接控制可见性
 * - 避免依赖 keyboardManager.trigger 的间接桥接（合成 KeyboardEvent + 闭包查表 + 注册时序）
 * - 单一可信状态源，确保点击可靠地弹出 GlobalSearch 模态框
 */
const visible = ref(false)

export function useCommandPalette() {
  return {
    visible,
    open: () => {
      visible.value = true
    },
    close: () => {
      visible.value = false
    },
    toggle: () => {
      visible.value = !visible.value
    },
  }
}
