import { defineStore } from 'pinia'
import { ref } from 'vue'
import { message } from 'ant-design-vue'
import IdleDetector from '@/utils/idle-detector'
import { getSecurityConfig, heartbeat } from '@/api/security'
import type { SecurityConfig } from '@/api/security'
import { removeToken, removeUserInfo, removeRefreshToken, removeSessionId } from '@/utils/auth'

export type IdleState = 'active' | 'warning' | 'locked' | 'expired'

// 默认安全配置（后端不可用时的降级配置）
const DEFAULT_CONFIG: SecurityConfig = {
  idleWarningMinutes: 25,
  idleLockscreenMinutes: 28,
  idleLogoutMinutes: 30,
  maxDevicesPerUser: 3,
  accessTokenMinutes: 30,
  refreshTokenDays: 7,
  ipChangeForceReauth: false,
  fingerprintChangeForceReauth: false,
  loginFailLockCount: 5,
  loginFailLockMinutes: 15,
}

export const useSecurityStore = defineStore('security', () => {
  // ===== 状态 =====
  const idleState = ref<IdleState>('active')
  const securityConfig = ref<SecurityConfig | null>(null)

  // ===== 内部变量（非响应式） =====
  let detector: IdleDetector | null = null
  let heartbeatTimer: number | null = null
  const HEARTBEAT_INTERVAL = 5 * 60 * 1000 // 5分钟

  // ===== 方法 =====

  /** 从后端获取安全配置 */
  async function fetchSecurityConfig(): Promise<void> {
    try {
      const config = await getSecurityConfig()
      securityConfig.value = config
      // 如果已有 detector 实例，更新配置
      if (detector) {
        detector.updateConfig({
          warningMinutes: config.idleWarningMinutes,
          lockscreenMinutes: config.idleLockscreenMinutes,
          logoutMinutes: config.idleLogoutMinutes,
        })
      }
    } catch (e) {
      console.warn('[SecurityStore] 获取安全配置失败，使用默认配置:', e)
      securityConfig.value = { ...DEFAULT_CONFIG }
    }
  }

  /** 初始化空闲检测（登录后调用） */
  function initIdleDetection(): void {
    // 如果已有实例，先停止
    stopIdleDetection()

    const config = securityConfig.value || DEFAULT_CONFIG

    detector = new IdleDetector({
      warningMinutes: config.idleWarningMinutes,
      lockscreenMinutes: config.idleLockscreenMinutes,
      logoutMinutes: config.idleLogoutMinutes,
    })

    // 注册事件回调
    detector.on('idle-warning', () => {
      if (idleState.value === 'active') {
        idleState.value = 'warning'
      }
    })

    detector.on('idle-lockscreen', () => {
      if (idleState.value === 'active' || idleState.value === 'warning') {
        lockScreen()
      }
    })

    detector.on('idle-logout', () => {
      expireSession()
    })

    detector.start()
    idleState.value = 'active'

    // 启动心跳
    startHeartbeat()
  }

  /** 停止空闲检测（退出时调用） */
  function stopIdleDetection(): void {
    if (detector) {
      detector.stop()
      detector = null
    }
    stopHeartbeat()
    idleState.value = 'active'
  }

  /** 重置为活跃状态（用户点击"继续"时调用） */
  function resetIdle(): void {
    idleState.value = 'active'
    if (detector) {
      detector.reset()
    }
    // 重新启动心跳
    startHeartbeat()
  }

  /** 进入锁屏状态 */
  function lockScreen(): void {
    idleState.value = 'locked'
    stopHeartbeat()
  }

  /** 解锁（密码验证成功后调用） */
  function unlock(): void {
    idleState.value = 'active'
    if (detector) {
      detector.reset()
    }
    startHeartbeat()
  }

  /** 会话过期，完全退出 */
  function expireSession(): void {
    idleState.value = 'expired'
    stopIdleDetection()

    // 清除认证数据
    removeToken()
    removeUserInfo()
    removeRefreshToken()
    removeSessionId()

    // 提示用户并延迟跳转登录页（确保提示能显示出来）
    message.warning('长时间未操作，已自动退出登录', 3)
    setTimeout(() => {
      import('@/router').then(({ default: router }) => {
        router.push('/login')
      })
    }, 800)
  }

  // ===== 心跳机制 =====

  /** 启动心跳（仅活跃状态发送） */
  function startHeartbeat(): void {
    stopHeartbeat()
    heartbeatTimer = window.setInterval(() => {
      if (idleState.value === 'active') {
        heartbeat().catch((e) => {
          console.warn('[SecurityStore] 心跳发送失败:', e)
        })
      }
    }, HEARTBEAT_INTERVAL)
  }

  /** 停止心跳 */
  function stopHeartbeat(): void {
    if (heartbeatTimer !== null) {
      clearInterval(heartbeatTimer)
      heartbeatTimer = null
    }
  }

  return {
    idleState,
    securityConfig,
    fetchSecurityConfig,
    initIdleDetection,
    stopIdleDetection,
    resetIdle,
    lockScreen,
    unlock,
    expireSession,
  }
})
