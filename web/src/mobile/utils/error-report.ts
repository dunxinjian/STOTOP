import type { Router } from 'vue-router'
import { reportError } from '@shared/api/mobile'

export function setupErrorReport(router: Router) {
  // 全局 JS 错误
  window.onerror = (message, _source, _line, _col, error) => {
    reportError({
      message: String(message),
      stack: error?.stack,
      route: router.currentRoute.value?.path,
      userAgent: navigator.userAgent,
    }).catch(() => {
      // 静默忽略上报失败
    })
  }

  // 未处理的 Promise 拒绝
  window.addEventListener('unhandledrejection', (event) => {
    reportError({
      message: event.reason?.message || String(event.reason),
      stack: event.reason?.stack,
      route: router.currentRoute.value?.path,
      userAgent: navigator.userAgent,
    }).catch(() => {
      // 静默忽略上报失败
    })
  })
}
