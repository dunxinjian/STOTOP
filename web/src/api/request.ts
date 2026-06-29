import axios, { type AxiosInstance, type AxiosRequestConfig, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import { message as antMessage } from 'ant-design-vue'
import { getToken, setToken, getRefreshToken, setRefreshToken, removeToken, removeUserInfo, removeRefreshToken, removeSessionId } from '@/utils/auth'
import { getStoredFingerprint } from '@/utils/device-fingerprint'

export let isRedirectingToLogin = false

/**
 * 封装 message.error：认证跳转期间抑制冗余提示，避免与 forceLogout 的提示重复
 */
const message = {
  ...antMessage,
  error(content: string, ...args: any[]) {
    if (isRedirectingToLogin) return
    return antMessage.error(content, ...args)
  },
} as typeof antMessage

// ===== Token 刷新队列机制 =====
let isRefreshing = false
let failedQueue: Array<{
  resolve: (value: any) => void
  reject: (reason?: any) => void
  config: InternalAxiosRequestConfig
}> = []

function processQueue(error: any, token: string | null): void {
  failedQueue.forEach(({ resolve, reject, config }) => {
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`
      resolve(service(config))
    } else {
      reject(error)
    }
  })
  failedQueue = []
}

const service: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 15000,
})

// 请求拦截器
service.interceptors.request.use(
  (config) => {
    const token = getToken()
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`
    }
    // 添加设备指纹请求头
    const fingerprint = getStoredFingerprint()
    if (fingerprint) {
      config.headers['X-Device-Fingerprint'] = fingerprint
    }
    // 添加组织上下文请求头
    try {
      const orgId = localStorage.getItem('stotop_current_org_id')
      if (orgId) {
        config.headers['X-Org-Context'] = orgId
      }
    } catch {
      // 忽略
    }
    // 添加账套上下文请求头
    try {
      const accountSetId = localStorage.getItem('currentAccountSetId')
      if (accountSetId) {
        config.headers['X-AccountSet-Id'] = accountSetId
      }
    } catch {
      // 忽略
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// 响应拦截器
service.interceptors.response.use(
  (response: AxiosResponse) => {
    // 滑动续期：后端通过响应头下发新 Token
    const newToken = response.headers['x-new-token']
    if (newToken) {
      setToken(newToken)
    }
    // Blob 响应（文件下载）直接返回，不走业务状态码解析
    if (response.config.responseType === 'blob') {
      return response.data
    }
    const res = response.data
    // 业务状态码判断（后端统一返回 { code, data, message }）
    if (res.code !== undefined && res.code !== 200 && res.code !== 0) {
      // 检查是否配置了静默模式
      const config = response.config as AxiosRequestConfig & { silent?: boolean }
      if (!config.silent) {
        message.error(res.message || '请求失败')
      }
      if (res.code === 401) {
        // 业务层401：尝试刷新Token
        return handle401(response.config as InternalAxiosRequestConfig)
      }
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    // 成功时返回实际业务数据
    return res.data
  },
  (error) => {
    // 检查是否配置了静默模式
    const config = error.config as AxiosRequestConfig & { silent?: boolean }
    const silent = config?.silent || false

    if (error.response) {
      const status = error.response.status
      if (status === 401) {
        const originalConfig = error.config as InternalAxiosRequestConfig
        const responseData = error.response.data
        const errorCode = responseData?.code

        // 明确的会话终止类错误 — 直接跳登录，不尝试刷新
        const kickReasonMap: Record<string, string> = {
          'SESSION_KICKED': '您的账号已在其他设备登录',
          'SESSION_EXPIRED': '登录已过期，请重新登录',
          'SESSION_INVALID': '会话已失效，请重新登录',
          'ANOMALY_IP_CHANGE': '检测到登录环境变化，请重新登录',
          'ANOMALY_DEVICE_CHANGE': '检测到设备变化，请重新登录',
        }

        if (errorCode && kickReasonMap[errorCode]) {
          forceLogout(silent ? undefined : (responseData?.message || kickReasonMap[errorCode]))
          return Promise.reject(error)
        }

        // 无明确 code 的 401 — 尝试刷新 Token
        return handle401(originalConfig)
      } else if (status === 403) {
        console.warn('Permission denied:', error.config?.url)
        if (!silent) {
          message.warning('暂无权限访问此功能')
          // 仅在非静默模式且无 X-Silent 头的页面级请求时跳转403页面
          if (!error.config?.headers?.['X-Silent']) {
            import('@/router').then(({ default: router }) => {
              router.push({ name: 'Forbidden' })
            })
          }
        }
      } else if (status === 404) {
        if (!silent) {
          message.error('请求资源不存在')
        }
      } else if (status >= 500) {
        if (!silent) {
          message.error('服务器内部错误，请稍后重试')
        }
      } else {
        if (!silent) {
          message.error(error.response.data?.message || `请求错误 ${status}`)
        }
      }
    } else if (error.code === 'ECONNABORTED' || error.message?.includes('timeout')) {
      if (!silent) {
        message.error('请求超时，服务器响应较慢，请稍后重试')
      }
      console.warn('[Request Timeout]', error.config?.url, `timeout=${error.config?.timeout}ms`)
    } else {
      if (!silent) {
        message.error('网络异常，请检查网络连接')
      }
    }
    return Promise.reject(error)
  }
)

export function get<T = any>(url: string, params?: object, config?: AxiosRequestConfig): Promise<T> {
  return service.get(url, { params, ...config })
}

export function post<T = any>(url: string, data?: object, config?: AxiosRequestConfig): Promise<T> {
  return service.post(url, data, config)
}

export function put<T = any>(url: string, data?: object, config?: AxiosRequestConfig): Promise<T> {
  return service.put(url, data, config)
}

export function del<T = any>(url: string, config?: AxiosRequestConfig): Promise<T> {
  return service.delete(url, config)
}

// ===== Token 刷新与强制登出 =====

/**
 * 带指数退避重试的 Token 刷新
 */
async function refreshWithRetry(refreshToken: string, maxRetries = 3): Promise<any> {
  const delays = [0, 1000, 2000, 4000] // 立即→1s→2s→4s
  let lastError: any = null

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (delays[attempt]) {
      await new Promise(resolve => setTimeout(resolve, delays[attempt]))
    }
    try {
      const { refreshTokenApi } = await import('@/api/security')
      return await refreshTokenApi(refreshToken)
    } catch (err: any) {
      lastError = err
      console.warn(`[Token Refresh] 第${attempt + 1}次刷新失败`, err?.message)
      // 401/400 = RefreshToken 本身无效，立即停止重试
      const status = err?.response?.status
      if (status === 401 || status === 400) throw err
      // 其他错误（网络/5xx）继续重试
    }
  }
  throw lastError
}

/**
 * 处理 401 错误：尝试刷新 Token，成功后重试原请求，失败则强制登出
 */
function handle401(originalConfig: InternalAxiosRequestConfig): Promise<any> {
  const refreshToken = getRefreshToken()

  // 没有 refreshToken，直接登出
  if (!refreshToken) {
    forceLogout('登录已过期，请重新登录')
    return Promise.reject(new Error('No refresh token'))
  }

  // 如果已经在刷新中，将请求加入队列
  if (isRefreshing) {
    return new Promise((resolve, reject) => {
      failedQueue.push({ resolve, reject, config: originalConfig })
    })
  }

  isRefreshing = true

  return refreshWithRetry(refreshToken)
    .then((result) => {
      const newToken = result.token
      const newRefreshToken = result.refreshToken

      // 更新存储
      setToken(newToken)
      setRefreshToken(newRefreshToken)

      // 处理队列中的请求
      processQueue(null, newToken)

      // 重试原请求
      originalConfig.headers['Authorization'] = `Bearer ${newToken}`
      return service(originalConfig)
    })
    .catch((err) => {
      // 刷新失败，所有队列请求都失败
      processQueue(err, null)
      forceLogout('登录凭证已失效，请重新登录')
      return Promise.reject(err)
    })
    .finally(() => {
      isRefreshing = false
    })
}

/**
 * 强制登出：清除认证数据并跳转登录页
 */
function forceLogout(msg?: string): void {
  if (isRedirectingToLogin) return
  isRedirectingToLogin = true

  // 先清除所有残留提示，避免组件 catch 产生的“加载XXX失败”与登出提示并存
  antMessage.destroy()

  if (msg) {
    antMessage.error(msg, 3)
  }

  removeToken()
  removeUserInfo()
  removeRefreshToken()
  removeSessionId()

  // 稍延跳转，确保提示消息能先显示
  const delay = msg ? 800 : 0
  setTimeout(() => {
    import('@/router').then(({ default: router }) => {
      router.push({ name: 'Login' })
      setTimeout(() => {
        isRedirectingToLogin = false
      }, 3000)
    })
  }, delay)
}

export default service
