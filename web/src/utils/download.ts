/**
 * 通过 Blob 触发浏览器文件下载
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

/**
 * 从 API Response 中提取文件名（Content-Disposition header）
 */
export function extractFilenameFromResponse(response: any, fallback: string = 'download'): string {
  const disposition = response.headers?.['content-disposition'] || ''
  const match = disposition.match(/filename\*?=(?:UTF-8''|"?)([^";]+)/i)
  return match ? decodeURIComponent(match[1]) : fallback
}
