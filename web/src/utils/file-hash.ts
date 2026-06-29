/**
 * 文件哈希计算工具
 * 使用 Web Crypto API (SubtleCrypto) 计算文件 SHA-256 指纹
 */

const SMALL_FILE_THRESHOLD = 50 * 1024 * 1024 // 50MB
const SAMPLE_SIZE = 1 * 1024 * 1024 // 1MB

/**
 * 计算文件的 SHA-256 哈希值
 * - 小于50MB：读取完整文件计算
 * - 大于50MB：取头部1MB + 中间1MB + 尾部1MB + 文件大小 组合计算（快速指纹）
 */
export async function calculateFileHash(file: File): Promise<string> {
  const buffer = file.size <= SMALL_FILE_THRESHOLD
    ? await readEntireFile(file)
    : await readFileSamples(file)

  const hashBuffer = await crypto.subtle.digest('SHA-256', buffer)
  return arrayBufferToHex(hashBuffer)
}

/** 读取完整文件为 ArrayBuffer */
async function readEntireFile(file: File): Promise<ArrayBuffer> {
  return file.arrayBuffer()
}

/** 大文件快速指纹：头部 + 中间 + 尾部 + 文件大小 */
async function readFileSamples(file: File): Promise<ArrayBuffer> {
  const fileSize = file.size
  const midStart = Math.floor((fileSize - SAMPLE_SIZE) / 2)

  // 并行读取三段
  const [head, middle, tail] = await Promise.all([
    file.slice(0, SAMPLE_SIZE).arrayBuffer(),
    file.slice(midStart, midStart + SAMPLE_SIZE).arrayBuffer(),
    file.slice(fileSize - SAMPLE_SIZE, fileSize).arrayBuffer(),
  ])

  // 拼接三段 + 文件大小（8字节 BigUint64）
  const sizeBuffer = new ArrayBuffer(8)
  new DataView(sizeBuffer).setBigUint64(0, BigInt(fileSize))

  const totalLength = head.byteLength + middle.byteLength + tail.byteLength + 8
  const combined = new Uint8Array(totalLength)
  combined.set(new Uint8Array(head), 0)
  combined.set(new Uint8Array(middle), head.byteLength)
  combined.set(new Uint8Array(tail), head.byteLength + middle.byteLength)
  combined.set(new Uint8Array(sizeBuffer), head.byteLength + middle.byteLength + tail.byteLength)

  return combined.buffer
}

/** ArrayBuffer 转 hex 字符串 */
function arrayBufferToHex(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer)
  const hexArr: string[] = []
  for (let i = 0; i < bytes.length; i++) {
    hexArr.push(bytes[i].toString(16).padStart(2, '0'))
  }
  return hexArr.join('')
}
