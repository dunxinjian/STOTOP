import { ref, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import { ensureCardFlowConnected, getCardFlowConnection } from '@/utils/signalr'
import {
  acquireEditLock,
  heartbeatEditLock,
  requestEditLockTakeover,
  respondEditLockTakeover,
} from '@/api/cardflow'
import type { LockStateDto } from '@/types/cardflow'

/**
 * 流程定义编辑锁 composable（M7-1 / 设计 E7）。
 * 单写者并发锁 + 接管协议：心跳 30s、只读端 15s 重试抢占、接管经 SignalR 通知。
 *
 * 消费方（编辑页）职责：
 * - flush：接管弹窗出现时强制落草稿（E7：flush 在弹窗时而非同意时）
 * - reload：被移交锁的新持有端重载最新草稿
 */

const HEARTBEAT_MS = 30_000
const READONLY_RETRY_MS = 15_000
/** 被拒后本地冷却，避免向同一持锁端频繁申请（E7 防骚扰） */
const TAKEOVER_REJECT_COOLDOWN_MS = 30 * 60_000

export interface UseDefinitionEditLockOptions {
  definitionId: number
  currentUserId: number
  currentUserName: string
  /** 强制落草稿（接管弹窗出现时调用） */
  flush: () => Promise<void>
  /** 重载最新草稿（成为新持锁端时调用） */
  reload: () => Promise<void>
}

export function useDefinitionEditLock(opts: UseDefinitionEditLockOptions) {
  const { definitionId, currentUserId } = opts

  /** 是否只读（他人持锁时为 true） */
  const readOnly = ref(false)
  /** 当前持锁人（非自己时有值供横幅展示） */
  const holder = ref<{ id: number; name: string; heartbeatAt?: string | null } | null>(null)
  /** 收到的接管请求（持锁端弹窗用） */
  const takeoverIncoming = ref<{ requesterId: number; requesterName: string } | null>(null)
  /** 是否已发起接管申请（申请端等待中） */
  const takeoverPending = ref(false)

  let heartbeatTimer: ReturnType<typeof setInterval> | null = null
  let readonlyRetryTimer: ReturnType<typeof setInterval> | null = null
  let rejectCooldownUntil = 0
  let disposed = false
  let subscribed = false

  function applyState(state: LockStateDto) {
    if (!state.held) {
      // 无锁（异常场景，正常 acquire 后总有锁）——按可编辑处理
      readOnly.value = false
      holder.value = null
      return
    }
    if (state.isSelf) {
      readOnly.value = false
      holder.value = null
      stopReadonlyRetry()
      startHeartbeat()
      // 心跳兜底：即便 SignalR 掉线，heartbeat 也能带回接管请求 → 触发 flush+弹窗
      if (state.takeover && !takeoverIncoming.value) {
        onTakeoverRequested(state.takeover.requesterId, state.takeover.requesterName)
      }
    } else {
      readOnly.value = true
      holder.value = { id: state.holderId ?? 0, name: state.holderName ?? '', heartbeatAt: state.heartbeatAt }
      stopHeartbeat()
      startReadonlyRetry()
    }
  }

  // ===== 锁获取 / 心跳 =====

  async function acquire() {
    try {
      const state = await acquireEditLock(definitionId)
      if (disposed) return
      applyState(state)
    } catch {
      // 网络错误：保守转只读，避免误认为持锁而双写
      readOnly.value = true
    }
  }

  function startHeartbeat() {
    if (heartbeatTimer) return
    heartbeatTimer = setInterval(async () => {
      try {
        const state = await heartbeatEditLock(definitionId)
        if (disposed) return
        applyState(state)
      } catch {
        /* 单次心跳失败静默，下次重试 */
      }
    }, HEARTBEAT_MS)
  }

  function stopHeartbeat() {
    if (heartbeatTimer) {
      clearInterval(heartbeatTimer)
      heartbeatTimer = null
    }
  }

  function startReadonlyRetry() {
    if (readonlyRetryTimer) return
    // 只读端定期重试 acquire——持锁端可能已离开（心跳超时）→ 抢占
    readonlyRetryTimer = setInterval(() => { void acquire() }, READONLY_RETRY_MS)
  }

  function stopReadonlyRetry() {
    if (readonlyRetryTimer) {
      clearInterval(readonlyRetryTimer)
      readonlyRetryTimer = null
    }
  }

  // ===== 接管 =====

  /** 只读端发起接管申请 */
  async function requestTakeover() {
    if (Date.now() < rejectCooldownUntil) {
      message.warning('接管请求刚被拒绝，请稍后再试')
      return
    }
    try {
      takeoverPending.value = true
      await requestEditLockTakeover(definitionId)
      message.info('已发起接管请求，等待对方响应（60 秒未响应将自动移交）')
    } catch (e) {
      takeoverPending.value = false
      // 拦截器已弹后端消息（如"X 已有接管请求处理中"）
      console.error('[EditLock] 申请接管失败:', e)
    }
  }

  /** 持锁端响应接管请求 */
  async function respondTakeover(accept: boolean) {
    try {
      const state = await respondEditLockTakeover(definitionId, accept)
      takeoverIncoming.value = null
      if (disposed) return
      applyState(state)
    } catch (e) {
      console.error('[EditLock] 响应接管失败:', e)
    }
  }

  /** 收到接管请求（SignalR 或心跳兜底）→ 立即强制 flush + 弹窗（E7：flush 在弹窗时） */
  function onTakeoverRequested(requesterId: number, requesterName: string) {
    // 只有持锁端才处理；且请求非自己发起
    if (readOnly.value || requesterId === currentUserId) return
    if (takeoverIncoming.value) return // 幂等去重
    takeoverIncoming.value = { requesterId, requesterName }
    // E7 强制 flush：清防抖、未保存态落草稿——从此刻起丢失窗口=0
    void opts.flush()
  }

  // ===== SignalR =====

  async function setupSignalR() {
    try {
      await ensureCardFlowConnected()
      const conn = getCardFlowConnection()
      await conn.invoke('SubscribeFlowDef', definitionId)
      subscribed = true

      conn.on('takeoverRequested', (data: { requesterId: number; requesterName: string }) => {
        onTakeoverRequested(data.requesterId, data.requesterName)
      })
      conn.on('takeoverGranted', async (data: { newHolderId: number; newHolderName: string }) => {
        if (disposed) return
        if (data.newHolderId === currentUserId) {
          // 我成为新持锁端：重载最新草稿 + 转可编辑
          takeoverPending.value = false
          await opts.reload()
          await acquire()
          message.success('已获得编辑权，加载最新草稿')
        } else {
          // 我是旧持锁端（或旁观者）：转只读
          takeoverIncoming.value = null
          readOnly.value = true
          holder.value = { id: data.newHolderId, name: data.newHolderName }
          stopHeartbeat()
          startReadonlyRetry()
          message.warning(`已移交给 ${data.newHolderName} · 你的修改已保存至草稿`)
        }
      })
      conn.on('takeoverRejected', () => {
        if (disposed) return
        if (takeoverPending.value) {
          takeoverPending.value = false
          rejectCooldownUntil = Date.now() + TAKEOVER_REJECT_COOLDOWN_MS
          message.warning('对方拒绝了接管请求')
        }
      })
    } catch (e) {
      // SignalR 建连失败：降级到心跳/只读轮询兜底（heartbeat 仍会带回接管请求）
      console.warn('[EditLock] SignalR 订阅失败，降级到轮询兜底:', e)
    }
  }

  async function teardownSignalR() {
    if (!subscribed) return
    try {
      const conn = getCardFlowConnection()
      conn.off('takeoverRequested')
      conn.off('takeoverGranted')
      conn.off('takeoverRejected')
      await conn.invoke('UnsubscribeFlowDef', definitionId)
    } catch {
      /* 静默 */
    }
  }

  // ===== 生命周期 =====

  async function start() {
    await acquire()
    await setupSignalR()
  }

  function refresh() {
    void acquire()
  }

  onBeforeUnmount(() => {
    disposed = true
    stopHeartbeat()
    stopReadonlyRetry()
    void teardownSignalR()
    // 不显式 release：持锁端关闭页面后靠心跳超时（120s）自然释放锁——
    // beforeunload 显式释放不可靠（断网/崩溃收不到），心跳超时兜底更稳。
  })

  return {
    readOnly,
    holder,
    takeoverIncoming,
    takeoverPending,
    start,
    refresh,
    requestTakeover,
    respondTakeover,
  }
}
