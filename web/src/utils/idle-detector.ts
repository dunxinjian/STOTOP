export type IdleEvent = 'idle-warning' | 'idle-lockscreen' | 'idle-logout'
export type IdleCallback = () => void

export interface IdleDetectorOptions {
  warningMinutes: number
  lockscreenMinutes: number
  logoutMinutes: number
}

class IdleDetector {
  private lastActivityTime: number = Date.now()
  private warningTimeout: number
  private lockscreenTimeout: number
  private logoutTimeout: number
  private timer: number | null = null
  private listeners: Map<IdleEvent, IdleCallback[]> = new Map()
  private events: string[] = ['mousemove', 'keydown', 'scroll', 'touchstart', 'click', 'mousedown']

  /** 节流标记：5秒内只更新一次 lastActivityTime */
  private throttleTimer: number | null = null
  private readonly THROTTLE_MS = 5000

  /** 已触发的事件标记，防止重复触发 */
  private firedEvents: Set<IdleEvent> = new Set()

  /** 绑定后的事件处理器引用，用于 removeEventListener */
  private boundHandleActivity: () => void

  constructor(options: IdleDetectorOptions) {
    this.warningTimeout = options.warningMinutes * 60 * 1000
    this.lockscreenTimeout = options.lockscreenMinutes * 60 * 1000
    this.logoutTimeout = options.logoutMinutes * 60 * 1000
    this.boundHandleActivity = this.handleActivity.bind(this)
  }

  /** 开始监听用户活动 */
  start(): void {
    this.lastActivityTime = Date.now()
    this.firedEvents.clear()

    // 绑定用户活动事件
    this.events.forEach((event) => {
      window.addEventListener(event, this.boundHandleActivity, { passive: true })
    })

    // 每秒检查一次空闲状态
    this.timer = window.setInterval(() => {
      this.checkIdle()
    }, 1000)
  }

  /** 停止监听 */
  stop(): void {
    // 移除用户活动事件
    this.events.forEach((event) => {
      window.removeEventListener(event, this.boundHandleActivity)
    })

    // 清除定时器
    if (this.timer !== null) {
      clearInterval(this.timer)
      this.timer = null
    }
    if (this.throttleTimer !== null) {
      clearTimeout(this.throttleTimer)
      this.throttleTimer = null
    }
  }

  /** 重置计时器（用户点击"继续"或解锁时调用） */
  reset(): void {
    this.lastActivityTime = Date.now()
    this.firedEvents.clear()
  }

  /** 注册事件监听 */
  on(event: IdleEvent, callback: IdleCallback): void {
    const callbacks = this.listeners.get(event) || []
    callbacks.push(callback)
    this.listeners.set(event, callbacks)
  }

  /** 移除事件监听 */
  off(event: IdleEvent, callback: IdleCallback): void {
    const callbacks = this.listeners.get(event)
    if (callbacks) {
      const index = callbacks.indexOf(callback)
      if (index !== -1) {
        callbacks.splice(index, 1)
      }
    }
  }

  /** 更新超时配置 */
  updateConfig(options: IdleDetectorOptions): void {
    this.warningTimeout = options.warningMinutes * 60 * 1000
    this.lockscreenTimeout = options.lockscreenMinutes * 60 * 1000
    this.logoutTimeout = options.logoutMinutes * 60 * 1000
    // 重置计时，用新配置重新开始计算
    this.reset()
  }

  /** 节流处理用户活动，5秒内只更新一次 */
  private handleActivity(): void {
    if (this.throttleTimer !== null) return

    this.lastActivityTime = Date.now()
    this.firedEvents.clear()

    this.throttleTimer = window.setTimeout(() => {
      this.throttleTimer = null
    }, this.THROTTLE_MS)
  }

  /** 定时检查空闲状态，按阈值触发事件（每个事件只触发一次） */
  private checkIdle(): void {
    const elapsed = Date.now() - this.lastActivityTime

    if (elapsed >= this.logoutTimeout && !this.firedEvents.has('idle-logout')) {
      this.firedEvents.add('idle-logout')
      this.emit('idle-logout')
    } else if (elapsed >= this.lockscreenTimeout && !this.firedEvents.has('idle-lockscreen')) {
      this.firedEvents.add('idle-lockscreen')
      this.emit('idle-lockscreen')
    } else if (elapsed >= this.warningTimeout && !this.firedEvents.has('idle-warning')) {
      this.firedEvents.add('idle-warning')
      this.emit('idle-warning')
    }
  }

  /** 触发事件回调 */
  private emit(event: IdleEvent): void {
    const callbacks = this.listeners.get(event)
    if (callbacks) {
      callbacks.forEach((cb) => {
        try {
          cb()
        } catch (e) {
          console.error(`[IdleDetector] Error in ${event} callback:`, e)
        }
      })
    }
  }
}

export default IdleDetector
