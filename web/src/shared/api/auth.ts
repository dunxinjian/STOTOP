import { get, post } from '@/api/request'
import type { UserInfo } from '../types'

/** 钉钉免登 */
export function dingtalkLogin(authCode: string) {
  return post<{ token: string; refreshToken: string; user: UserInfo }>(
    '/dingtalk/auth/login',
    { authCode }
  )
}

/** 刷新 Token */
export function refreshToken(refreshToken: string) {
  return post<{ token: string; refreshToken: string }>(
    '/dingtalk/auth/refresh',
    { refreshToken }
  )
}

/** 获取 JSAPI 签名 */
export function getJsapiSignature(url: string) {
  return get<{ agentId: string; corpId: string; timeStamp: string; nonceStr: string; signature: string }>(
    '/dingtalk/jsapi-signature',
    { url }
  )
}
