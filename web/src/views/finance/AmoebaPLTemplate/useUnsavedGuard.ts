/**
 * dirty 守卫 composable
 * - 路由内切换：vue-router beforeRouteLeave / onBeforeRouteLeave
 * - 关闭/刷新：window.beforeunload
 * 两个通道互斥，不重复弹窗
 */
import { onBeforeUnmount, watch, type Ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { Modal } from 'ant-design-vue'

export interface UnsavedGuardOptions {
  /** 当前是否有未保存修改 */
  isDirty: Ref<boolean>
  /** 路由内拦截后的提示文案 */
  routerLeaveMessage?: string
  /** 用户在路由内拦截弹窗中点击"放弃"后回调（用于清理 dirty 标记） */
  onDiscard?: () => void
}

export function useUnsavedGuard(options: UnsavedGuardOptions) {
  const {
    isDirty,
    routerLeaveMessage = '当前项目有未保存的修改，离开将丢失，是否继续？',
    onDiscard,
  } = options

  // ===== 关闭/刷新拦截 =====
  function handleBeforeUnload(e: BeforeUnloadEvent) {
    if (isDirty.value) {
      e.preventDefault()
      // 大多数浏览器忽略自定义文本，但需要返回非空字符串触发原生弹窗
      e.returnValue = ''
      return ''
    }
  }

  watch(
    isDirty,
    (dirty) => {
      if (dirty) {
        window.addEventListener('beforeunload', handleBeforeUnload)
      } else {
        window.removeEventListener('beforeunload', handleBeforeUnload)
      }
    },
    { immediate: true }
  )

  // ===== 路由内切换拦截 =====
  onBeforeRouteLeave((to, from, next) => {
    if (!isDirty.value) {
      next()
      return
    }
    Modal.confirm({
      title: '未保存的修改',
      content: routerLeaveMessage,
      okText: '放弃修改并离开',
      okType: 'danger',
      cancelText: '取消',
      onOk: () => {
        onDiscard?.()
        next()
      },
      onCancel: () => next(false),
    })
  })

  // ===== 卸载时彻底清理 =====
  onBeforeUnmount(() => {
    window.removeEventListener('beforeunload', handleBeforeUnload)
  })
}
