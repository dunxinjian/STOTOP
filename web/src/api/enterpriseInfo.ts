import { get, put } from './request'

/**
 * 企业信息接口定义
 */
export interface EnterpriseInfo {
  name: string
  shortName: string
  logoUrl?: string
}

/**
 * 企业信息更新请求
 */
export interface EnterpriseInfoUpdateRequest {
  name: string
  shortName: string
  logoUrl?: string
}

/**
 * 获取企业信息（无需认证）
 */
export function getEnterpriseInfo(): Promise<EnterpriseInfo> {
  return get<EnterpriseInfo>('/system/enterprise-info', undefined, { silent: true } as any)
}

/**
 * 更新企业信息（需管理员权限）
 */
export function updateEnterpriseInfo(data: EnterpriseInfoUpdateRequest): Promise<EnterpriseInfo> {
  return put<EnterpriseInfo>('/system/enterprise-info', data)
}
