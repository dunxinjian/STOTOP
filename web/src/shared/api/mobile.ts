import service, { get, post } from '@/api/request'
import type { KpiData, TrendPoint } from '../types'

/** 经营看板 KPI */
export function getDashboardKpi(period: string, orgId: number) {
  return get<KpiData>('/mobile/dashboard/kpi', { period, orgId })
}

/** 经营趋势 */
export function getDashboardTrend(days: number, metric: string, orgId: number) {
  return get<{ points: TrendPoint[] }>('/mobile/dashboard/trend', { days, metric, orgId })
}

/** 移动端文件上传 */
export function uploadMobileFile(file: File, onProgress?: (percent: number) => void) {
  const formData = new FormData()
  formData.append('file', file)
  return service.post('/upload/mobile', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (e: any) => {
      if (onProgress && e.total) {
        onProgress(Math.round((e.loaded / e.total) * 100))
      }
    },
  } as any) as Promise<any>
}

/** 钉钉 mediaId 转存 */
export function transferMedia(mediaId: string) {
  return post<{ url: string }>('/upload/media-transfer', { mediaId })
}

/** 版本检查 */
export function checkVersion() {
  return get<{ version: string; forceUpdate: boolean }>('/mobile/version')
}

/** 错误上报 */
export function reportError(data: { message: string; stack?: string; route?: string; userAgent?: string }) {
  return post('/mobile/error-report', data, { silent: true } as any)
}
