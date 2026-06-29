import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr'

// ===== 类型定义 =====

export interface SignalRConnectionOptions {
  /** Hub URL 路径，如 '/hubs/workhub' */
  url: string
  /** 获取 JWT token 的工厂方法，默认从 localStorage 读取 */
  accessTokenFactory?: () => string
  /** 心跳间隔（毫秒），默认 15000 */
  keepAliveIntervalMs?: number
  /** 服务端超时（毫秒），默认 30000 */
  serverTimeoutMs?: number
  /** 手动重连最大尝试次数，默认 10 */
  maxManualRetries?: number
  /** 手动重连基础间隔（毫秒），默认 5000 */
  manualRetryBaseMs?: number
  /** 日志级别，默认 Warning */
  logLevel?: LogLevel
  /** 连接状态变化回调 */
  onStateChange?: (state: HubConnectionState) => void
}

// ===== 指数退避重连策略 =====

class ExponentialRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: RetryContext): number | null {
    // 指数退避：1s, 2s, 4s, 8s, 16s, 最大30s
    const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000)
    return delay
  }
}

// ===== 重连回调注册 =====

let reconnectCallbacks: Array<() => void> = []

/**
 * 注册重连成功后的回调（用于 useBatchSync 等场景重新订阅）
 * @returns 取消函数，调用后移除该回调
 */
export function onReconnected(cb: () => void): () => void {
  reconnectCallbacks.push(cb)
  return () => {
    reconnectCallbacks = reconnectCallbacks.filter(c => c !== cb)
  }
}

// ===== 连接管理器 =====

export interface SignalRManager {
  /** 底层 HubConnection 实例 */
  connection: HubConnection
  /** 启动连接 */
  start(): Promise<void>
  /** 停止连接并清理资源 */
  stop(): Promise<void>
  /** 当前是否已连接 */
  readonly isConnected: boolean
}

/**
 * 创建带完整监控和自动重连的 SignalR 连接管理器
 *
 * 特性：
 * - 指数退避自动重连（1s → 2s → 4s → ... → 30s）
 * - 自动重连失败后的手动重连兜底
 * - 心跳保活配置
 * - 连接状态变化回调
 */
export function createSignalRConnection(options: SignalRConnectionOptions): SignalRManager {
  const {
    url,
    accessTokenFactory = () => localStorage.getItem('stotop_token') || '',
    keepAliveIntervalMs = 15000,
    serverTimeoutMs = 30000,
    maxManualRetries = 10,
    manualRetryBaseMs = 5000,
    logLevel = LogLevel.Warning,
    onStateChange,
  } = options

  let manualReconnectTimer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  // 创建连接
  const connection = new HubConnectionBuilder()
    .withUrl(url, { accessTokenFactory })
    .withAutomaticReconnect(new ExponentialRetryPolicy())
    .configureLogging(logLevel)
    .build()

  // 心跳保活配置
  connection.keepAliveIntervalInMilliseconds = keepAliveIntervalMs
  connection.serverTimeoutInMilliseconds = serverTimeoutMs

  // ===== 连接状态监控 =====

  connection.onreconnecting((error) => {
    console.warn(`[SignalR][${url}] 连接断开，正在重连...`, error?.message)
    onStateChange?.(HubConnectionState.Reconnecting)
  })

  connection.onreconnected((connectionId) => {
    console.info(`[SignalR][${url}] 重连成功:`, connectionId)
    onStateChange?.(HubConnectionState.Connected)
    // 触发所有注册的重连回调（如 useBatchSync 重新订阅）
    reconnectCallbacks.forEach(cb => cb())
  })

  connection.onclose((error) => {
    if (disposed) return
    console.error(`[SignalR][${url}] 连接关闭:`, error?.message)
    onStateChange?.(HubConnectionState.Disconnected)
    // withAutomaticReconnect 放弃重试后触发 onclose，启动手动重连兜底
    startManualReconnect()
  })

  // ===== 手动重连兜底 =====

  async function startManualReconnect() {
    if (disposed) return
    let retries = 0

    const tryConnect = async () => {
      if (disposed || retries >= maxManualRetries) {
        if (!disposed) {
          console.error(`[SignalR][${url}] 已放弃重连（${maxManualRetries} 次失败）`)
          onStateChange?.(HubConnectionState.Disconnected)
        }
        return
      }

      try {
        if (connection.state === HubConnectionState.Disconnected) {
          console.info(`[SignalR][${url}] 手动重连尝试 (${retries + 1}/${maxManualRetries})`)
          await connection.start()
          console.info(`[SignalR][${url}] 手动重连成功`)
          onStateChange?.(HubConnectionState.Connected)
          // 手动重连成功后也要触发重连回调（重新订阅 Group 等）
          reconnectCallbacks.forEach(cb => cb())
          return
        }
      } catch {
        retries++
        console.warn(`[SignalR][${url}] 手动重连失败 (${retries}/${maxManualRetries})`)
      }

      // 递增延迟
      const delay = manualRetryBaseMs * (retries + 1)
      manualReconnectTimer = setTimeout(tryConnect, delay)
    }

    // 首次延迟 5 秒后开始
    manualReconnectTimer = setTimeout(tryConnect, manualRetryBaseMs)
  }

  function cancelManualReconnect() {
    if (manualReconnectTimer) {
      clearTimeout(manualReconnectTimer)
      manualReconnectTimer = null
    }
  }

  // ===== 公共 API =====

  async function start() {
    if (connection.state !== HubConnectionState.Disconnected) return
    disposed = false
    try {
      await connection.start()
      console.info(`[SignalR][${url}] 连接已建立`)
      onStateChange?.(HubConnectionState.Connected)
    } catch (err) {
      console.error(`[SignalR][${url}] 初始连接失败:`, err)
      // 初始连接失败也启动手动重连
      startManualReconnect()
      throw err
    }
  }

  async function stop() {
    disposed = true
    cancelManualReconnect()
    if (connection.state !== HubConnectionState.Disconnected) {
      try {
        await connection.stop()
      } catch {
        // 忽略停止时的错误
      }
    }
    onStateChange?.(HubConnectionState.Disconnected)
  }

  return {
    connection,
    start,
    stop,
    get isConnected() {
      return connection.state === HubConnectionState.Connected
    },
  }
}

// ===== 向下兼容：Progress Hub 单例 =====

let progressManager: SignalRManager | null = null

/** 获取 Progress Hub 连接（单例） */
export function getConnection(): HubConnection {
  if (!progressManager) {
    progressManager = createSignalRConnection({ url: '/hubs/progress' })
  }
  return progressManager.connection
}

/** 确保 Progress Hub 已连接 */
export async function ensureConnected(): Promise<HubConnection> {
  if (!progressManager) {
    progressManager = createSignalRConnection({ url: '/hubs/progress' })
  }
  if (progressManager.connection.state === HubConnectionState.Disconnected) {
    await progressManager.start()
  }
  return progressManager.connection
}

/** 停止 Progress Hub 连接 */
export async function stopConnection(): Promise<void> {
  if (progressManager) {
    await progressManager.stop()
    progressManager = null
  }
}

// ===== CardFlow Hub 单例 =====

let cardFlowManager: SignalRManager | null = null

/** 获取 CardFlow Hub 连接（单例） */
export function getCardFlowConnection(): HubConnection {
  if (!cardFlowManager) {
    cardFlowManager = createSignalRConnection({ url: '/hubs/cardflow' })
  }
  return cardFlowManager.connection
}

/** 确保 CardFlow Hub 已连接 */
export async function ensureCardFlowConnected(): Promise<HubConnection> {
  if (!cardFlowManager) {
    cardFlowManager = createSignalRConnection({ url: '/hubs/cardflow' })
  }
  if (cardFlowManager.connection.state === HubConnectionState.Disconnected) {
    await cardFlowManager.start()
  }
  return cardFlowManager.connection
}

/** 停止 CardFlow Hub 连接 */
export async function stopCardFlowConnection(): Promise<void> {
  if (cardFlowManager) {
    await cardFlowManager.stop()
    cardFlowManager = null
  }
}
