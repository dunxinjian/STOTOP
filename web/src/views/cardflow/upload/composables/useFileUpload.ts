/**
 * useFileUpload — 文件分片上传 + 进度追踪
 *
 * 职责：
 *   - 文件哈希计算（重复检测）
 *   - 分片上传逻辑（串行 chunk）
 *   - 上传进度实时更新到 store
 *   - 上传完成后订阅 SignalR
 *
 * Bug 修复记录：
 *   - [Fix#1] 上传进度连续追踪：利用 axios onUploadProgress 实现分片内字节级进度
 *   - [Fix#3] 异常处理安全解析：可选链访问 + 分类错误提示
 *   - [Fix#4] 分片重试机制：最多3次指数退避重试，保留已完成分片进度
 */
import { ref } from 'vue'
import { message, notification } from 'ant-design-vue'
import { useBatchStore } from '@/stores/batchStore'
import type { BatchItem, BatchStatus } from '@/stores/batchStore'
import { useBatchSync } from './useBatchSync'
import { mapStatus } from '../utils/batchMapping'
import {
  initChunkUpload,
  uploadChunk,
  completeChunkUpload,
} from '@/api/cardflow'
import type { BatchTriggerResultDto } from '@/api/cardflow'
import { calculateFileHash } from '@/utils/file-hash'
import { useOrgContextStore } from '@/stores/orgContext'

// ===== 常量 =====
const CHUNK_SIZE = 2 * 1024 * 1024 // 2MB
const MAX_CHUNK_RETRIES = 3        // [Fix#4] 单个分片最大重试次数
const RETRY_BASE_DELAY_MS = 1000   // [Fix#4] 重试基础延迟（指数退避）

// [Fix#5] 临时ID自增序列号，防止同毫秒多文件拖入时ID碰撞
let _tempIdSeq = 0

// ===== 工具函数 =====

/**
 * [Fix#3] 根据错误类型返回用户友好的提示信息
 */
function getUploadErrorMessage(e: any): string {
  // 网络断开（无 response）
  if (!e?.response && (e?.message === 'Network Error' || e?.message?.includes('network'))) {
    return '网络连接失败，请检查网络后重试'
  }
  // 请求超时
  if (e?.code === 'ECONNABORTED' || e?.message?.includes('timeout')) {
    return '上传超时，请检查网络后重试'
  }
  // 服务器返回的错误信息
  const serverMsg = e?.response?.data?.message ?? e?.response?.data?.error
  if (serverMsg) {
    return `服务器错误：${serverMsg}`
  }
  // 通用兜底
  return e?.message || '上传失败，请重试'
}

/**
 * [Fix#4] 带指数退避重试的分片上传
 * @param chunkParams  分片参数（每次重试重新构建 FormData）
 * @param onProgress   字节级进度回调
 */
async function uploadChunkWithRetry(
  chunkParams: {
    chunk: Blob
    uploadId: string
    chunkIndex: number
    totalChunks: number
    fileName: string
  },
  onProgress: (loaded: number) => void,
): Promise<void> {
  let lastError: any

  for (let attempt = 0; attempt <= MAX_CHUNK_RETRIES; attempt++) {
    try {
      // 每次重试重建 FormData，避免某些浏览器复用问题
      const formData = new FormData()
      formData.append('file', chunkParams.chunk)
      formData.append('uploadId', chunkParams.uploadId)
      formData.append('chunkIndex', String(chunkParams.chunkIndex))
      formData.append('totalChunks', String(chunkParams.totalChunks))
      formData.append('fileName', chunkParams.fileName)

      // [Fix#1] 利用 axios onUploadProgress 追踪单分片内字节级进度
      await uploadChunk(formData, {
        onUploadProgress: (evt: any) => {
          if (evt.loaded) {
            onProgress(evt.loaded)
          }
        },
      })
      return // 成功，退出重试循环
    } catch (e) {
      lastError = e
      if (attempt < MAX_CHUNK_RETRIES) {
        // 指数退避：1s, 2s, 4s
        const delay = RETRY_BASE_DELAY_MS * Math.pow(2, attempt)
        console.debug(`[FileUpload] 分片 ${chunkParams.chunkIndex} 第${attempt + 1}次重试，延迟 ${delay}ms`)
        await new Promise(resolve => setTimeout(resolve, delay))
      }
    }
  }
  // 所有重试耗尽
  throw lastError
}

export function useFileUpload() {
  const store = useBatchStore()
  const { subscribeBatch, registerIdMapping } = useBatchSync()
  const orgContextStore = useOrgContextStore()

  /** 未匹配流程的待处理批次事件（供外部监听并弹出流程选择弹窗） */
  const onUnmatchedBatch = ref<{ batchId: number; fileName: string } | null>(null)

  /** 检测业务类型 */
  function detectBizType(fileName: string): string {
    if (/费用|报销/.test(fileName)) return 'expense'
    if (/运费|快递|极兔|申通|韵达|圆通|中通|顺丰/.test(fileName)) return 'express'
    if (/客户/.test(fileName)) return 'customer'
    return 'other'
  }

  /**
   * 处理文件上传（分片上传完整流程）
   * 1. 创建临时 BatchItem → 插入 store
   * 2. 计算文件哈希
   * 3. 初始化上传（检测重复）
   * 4. 分片上传 + 进度更新（含重试）
   * 5. 完成上传 → 替换ID → 订阅 SignalR
   */
  async function handleFileUpload(file: File) {
    const tempId = -(Date.now() * 1000 + (++_tempIdSeq % 1000))
    const batchItem: BatchItem = {
      id: tempId,
      uid: `upload-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      batchNo: '',
      title: file.name,
      fileName: file.name,
      bizType: detectBizType(file.name),
      bizTag: '',
      tagColor: 'gray',
      status: 'uploading' as BatchStatus,
      priority: 0,
      createTime: new Date().toISOString(),
      totalRows: 0,
      errorCount: 0,
      processedRows: 0,
      successCount: 0,
      failedCount: 0,
      skippedCount: 0,
      progress: 0,
      uploadProgress: 0,
      waitHours: 0,
      comments: [],
    }

    // 插入 store（头部）
    store.applyUpdate(tempId, batchItem)

    try {
      // 计算文件哈希
      store.applyUpdate(tempId, { summary: '正在计算文件指纹...' })
      const fileHash = await calculateFileHash(file)

      const totalChunks = Math.ceil(file.size / CHUNK_SIZE)
      // 初始化上传
      store.applyUpdate(tempId, { summary: '正在初始化上传...' })
      const initRes = await initChunkUpload({
        fileName: file.name,
        fileSize: file.size,
        totalChunks,
        fileHash,
        ...(orgContextStore.currentOrgId ? { targetOrgId: orgContextStore.currentOrgId } : {}),
      })

      // 文件重复检测
      if ((initRes as any)?.isDuplicate) {
        const duplicateBatchNo = (initRes as any)?.duplicateBatchNo || '未知'
        message.warning(`该文件已在批次 #${duplicateBatchNo} 中上传过，不允许重复上传`)
        store.removeBatch(tempId)
        return
      }

      const uploadId = (initRes as any)?.uploadId

      // [Fix#1 + Fix#4] 分片上传：连续进度追踪 + 自动重试
      store.applyUpdate(tempId, { summary: '正在上传...' })
      for (let i = 0; i < totalChunks; i++) {
        const start = i * CHUNK_SIZE
        const end = Math.min(start + CHUNK_SIZE, file.size)
        const chunk = file.slice(start, end)

        // [Fix#1] 进度公式：(已完成分片 * 分片大小 + 当前分片已上传字节) / 总文件大小
        await uploadChunkWithRetry(
          { chunk, uploadId, chunkIndex: i, totalChunks, fileName: file.name },
          (loaded: number) => {
            const totalUploaded = i * CHUNK_SIZE + Math.min(loaded, chunk.size)
            const uploadProgress = Math.min(Math.round((totalUploaded / file.size) * 100), 99)
            store.applyUpdate(tempId, {
              uploadProgress,
              progress: uploadProgress,
              uploadChunkInfo: `分片 ${i + 1}/${totalChunks}`,
            })
          },
        )

        // 分片完成后更新最终进度
        const uploadProgress = Math.round(((i + 1) / totalChunks) * 100)
        store.applyUpdate(tempId, {
          uploadProgress: Math.min(uploadProgress, 99), // 保留最后1%给完成确认
          progress: Math.min(uploadProgress, 99),
          uploadChunkInfo: `分片 ${i + 1}/${totalChunks}`,
        })
      }

      // 所有分片上传完成
      store.applyUpdate(tempId, {
        uploadProgress: 100,
        progress: 100,
        summary: '正在完成上传...',
      })

      // 完成上传 → 返回 List<BatchTriggerResultDto>
      const completeRes = await completeChunkUpload({
        uploadId,
        fileName: file.name,
        totalChunks,
      })

      // 多流程触发结果列表
      const triggerResults: BatchTriggerResultDto[] = Array.isArray(completeRes) ? completeRes : (completeRes as any)?.data ?? []

      if (triggerResults.length === 0) {
        // 极端情况：后端返回空列表（理论上不会发生，后端会在无匹配时创建 pending 批次）
        store.removeBatch(tempId)
        message.warning('未找到匹配的流程定义')
        return
      }

      // 检查是否存在未匹配项（flowDefinitionId === 0）
      const unmatchedItems = triggerResults.filter(r => r.flowDefinitionId === 0)
      const matchedItems = triggerResults.filter(r => r.flowDefinitionId > 0)

      // === 情况1：未匹配流程 ===
      if (unmatchedItems.length > 0) {
        const pendingItem = unmatchedItems[0]
        const realBatchId = pendingItem.batchId

        // 替换临时ID为真实批次ID
        if (realBatchId && realBatchId > 0) {
          registerIdMapping(tempId, realBatchId)
          store.replaceBatchId(tempId, realBatchId, {
            uploadProgress: undefined,
            uploadChunkInfo: undefined,
            uploadCompletedAt: new Date().toISOString(),
            status: 'pending',
            summary: '未匹配流程，请选择流程定义',
          })
          subscribeBatch(realBatchId)
        }

        // 通知外部组件弹出流程选择弹窗
        onUnmatchedBatch.value = { batchId: realBatchId, fileName: file.name }
        return
      }

      // === 情况2：单个匹配（与之前行为一致） ===
      if (matchedItems.length === 1) {
        const item = matchedItems[0]
        const batchId = item.batchId

        // ID 替换 — 使用 store 原子方法 + 注册ID映射
        if (batchId && batchId > 0) {
          registerIdMapping(tempId, batchId)
          store.replaceBatchId(tempId, batchId, {
            batchNo: '',
            bizTag: item.flowName || '',
            tagColor: 'blue',
            uploadProgress: undefined,
            uploadChunkInfo: undefined,
            uploadCompletedAt: new Date().toISOString(),
          })
          subscribeBatch(batchId)
          store.applyUpdate(batchId, { status: 'processing' })
        }
        return
      }

      // === 情况3：多个匹配 ===
      if (matchedItems.length > 1) {
        // 第一个批次替换临时ID
        const firstItem = matchedItems[0]
        const firstBatchId = firstItem.batchId

        if (firstBatchId && firstBatchId > 0) {
          registerIdMapping(tempId, firstBatchId)
          store.replaceBatchId(tempId, firstBatchId, {
            batchNo: '',
            bizTag: firstItem.flowName || '',
            tagColor: 'blue',
            uploadProgress: undefined,
            uploadChunkInfo: undefined,
            uploadCompletedAt: new Date().toISOString(),
          })
          subscribeBatch(firstBatchId)
          store.applyUpdate(firstBatchId, { status: 'processing' })
        }

        // 为其余匹配项创建新的批次卡片
        for (let i = 1; i < matchedItems.length; i++) {
          const item = matchedItems[i]
          const newBatchId = item.batchId
          if (newBatchId && newBatchId > 0) {
            const extraBatch: BatchItem = {
              id: newBatchId,
              uid: `upload-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
              batchNo: '',
              title: file.name,
              fileName: file.name,
              bizType: detectBizType(file.name),
              bizTag: item.flowName || '',
              tagColor: 'blue',
              status: 'processing' as BatchStatus,
              priority: 0,
              createTime: new Date().toISOString(),
              totalRows: 0,
              errorCount: 0,
              processedRows: 0,
              successCount: 0,
              failedCount: 0,
              skippedCount: 0,
              progress: 0,
              uploadProgress: undefined,
              waitHours: 0,
              comments: [],
            }
            store.applyUpdate(newBatchId, extraBatch)
            subscribeBatch(newBatchId)
          }
        }

        // 多流程通知
        notification.info({
          message: `已触发 ${matchedItems.length} 个流程`,
          description: matchedItems.map(r => `${r.flowName}（批次 #${r.batchId}）`).join('\n'),
          duration: 6,
        })
      }
    } catch (e: any) {
      // [Fix#3] 安全的错误响应解析 — 使用可选链避免二次异常
      const errBatchId = e?.response?.data?.data?.batchId ?? e?.response?.data?.batchId
      const errorMsg = getUploadErrorMessage(e)

      if (errBatchId && errBatchId > 0) {
        // 即使异常，如果响应体中有 batchId 也要替换（防止临时负数ID残留）
        registerIdMapping(tempId, errBatchId)
        store.replaceBatchId(tempId, errBatchId, {
          status: 'error',
          summary: errorMsg,
        })
      } else {
        store.applyUpdate(tempId, {
          status: 'error',
          summary: errorMsg,
        })
      }
      message.error(`${file.name} 上传失败：${errorMsg}`)
    }
  }

  return {
    handleFileUpload,
    onUnmatchedBatch,
  }
}
