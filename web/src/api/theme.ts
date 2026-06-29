import { get, put } from './request'

/** 获取主题配置 */
export function getThemeSettings(): Promise<string> {
  return get<string>('/system/theme-settings', undefined, { silent: true } as any)
}

/** 更新主题配置 */
export function updateThemeSettings(configJson: string): Promise<string> {
  return put<string>('/system/theme-settings', { configJson })
}
