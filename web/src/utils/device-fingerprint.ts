const FINGERPRINT_KEY = 'stotop_device_fingerprint'

/**
 * 简易哈希函数（FNV-1a 变体），用于将字符串转为十六进制指纹
 */
function hashString(str: string): string {
  let hash = 0x811c9dc5 // FNV offset basis
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i)
    hash = (hash * 0x01000193) >>> 0 // FNV prime, 保持为无符号32位
  }
  // 用多轮混合增加熵
  const parts: number[] = []
  const chunkSize = Math.ceil(str.length / 4)
  for (let i = 0; i < 4; i++) {
    let h = 0x811c9dc5
    const chunk = str.substring(i * chunkSize, (i + 1) * chunkSize)
    for (let j = 0; j < chunk.length; j++) {
      h ^= chunk.charCodeAt(j)
      h = (h * 0x01000193) >>> 0
    }
    parts.push(h)
  }
  return parts.map((p) => p.toString(16).padStart(8, '0')).join('')
}

/**
 * 生成 Canvas 指纹
 */
function getCanvasFingerprint(): string {
  try {
    const canvas = document.createElement('canvas')
    canvas.width = 200
    canvas.height = 50
    const ctx = canvas.getContext('2d')
    if (!ctx) return ''

    // 绘制文字
    ctx.textBaseline = 'top'
    ctx.font = '14px Arial'
    ctx.fillStyle = '#f60'
    ctx.fillRect(50, 0, 100, 30)
    ctx.fillStyle = '#069'
    ctx.fillText('STOTOP fingerprint', 2, 15)
    ctx.fillStyle = 'rgba(102, 204, 0, 0.7)'
    ctx.fillText('STOTOP fingerprint', 4, 17)

    // 绘制图形
    ctx.beginPath()
    ctx.arc(50, 25, 10, 0, Math.PI * 2)
    ctx.strokeStyle = '#0ff'
    ctx.stroke()

    return canvas.toDataURL()
  } catch {
    return ''
  }
}

/**
 * 生成设备指纹
 */
export async function generateFingerprint(): Promise<string> {
  const components: string[] = [
    `screen:${screen.width}x${screen.height}`,
    `colorDepth:${screen.colorDepth}`,
    `timezone:${Intl.DateTimeFormat().resolvedOptions().timeZone}`,
    `timezoneOffset:${new Date().getTimezoneOffset()}`,
    `language:${navigator.language}`,
    `platform:${navigator.platform}`,
    `userAgent:${navigator.userAgent}`,
    `canvas:${getCanvasFingerprint()}`,
    `hardwareConcurrency:${navigator.hardwareConcurrency || 'unknown'}`,
    `deviceMemory:${(navigator as any).deviceMemory || 'unknown'}`,
    `maxTouchPoints:${navigator.maxTouchPoints || 0}`,
  ]

  const raw = components.join('|')

  // 优先使用 SubtleCrypto SHA-256
  if (window.crypto?.subtle) {
    try {
      const encoder = new TextEncoder()
      const data = encoder.encode(raw)
      const hashBuffer = await crypto.subtle.digest('SHA-256', data)
      const hashArray = Array.from(new Uint8Array(hashBuffer))
      return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('')
    } catch {
      // fallback
    }
  }

  // 降级为 FNV-1a 哈希
  return hashString(raw)
}

/**
 * 获取已存储的设备指纹
 */
export function getStoredFingerprint(): string | null {
  try {
    return localStorage.getItem(FINGERPRINT_KEY)
  } catch {
    return null
  }
}

/**
 * 获取或生成并缓存设备指纹
 */
export async function initFingerprint(): Promise<string> {
  const stored = getStoredFingerprint()
  if (stored) return stored

  const fp = await generateFingerprint()
  try {
    localStorage.setItem(FINGERPRINT_KEY, fp)
  } catch {
    // localStorage 不可用时静默忽略
  }
  return fp
}
