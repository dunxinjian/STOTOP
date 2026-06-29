import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useToolbarStore = defineStore('toolbar', () => {
  // 当前激活的页面标识
  const activePageId = ref<string | null>(null)

  // 实例级唯一 token：每次 register 时生成新 token。
  // PageHeader 实例通过对比自己持有的 token 与 store 中的 activeToken
  // 来决定是否允许 Teleport 渲染，从而保证只有「最后一个注册」的实例生效。
  // 这解决了 keep-alive 缓存中多个同路由名实例同时存活时，
  // 仅凭 pageId 字符串无法区分实例导致工具栏内容重复的问题。
  const activeToken = ref<number>(0)

  // 各区域是否有内容（由 PageHeader 声明）
  const hasRow1 = ref(false)
  const hasRow2 = ref(false)

  function register(pageId: string, options: { row2?: boolean } = {}): number {
    const token = ++activeToken.value
    activePageId.value = pageId
    hasRow1.value = true
    hasRow2.value = !!options.row2
    return token
  }

  function unregister(pageId: string, token: number) {
    // 只有持有当前 token 的实例才能注销，防止旧实例覆盖新实例的工具栏状态
    if (activeToken.value === token && activePageId.value === pageId) {
      activePageId.value = null
      hasRow1.value = false
      hasRow2.value = false
    }
  }

  // 整体是否显示工具栏容器
  const visible = computed(() => hasRow1.value || hasRow2.value)

  return { activePageId, activeToken, hasRow1, hasRow2, visible, register, unregister }
})
