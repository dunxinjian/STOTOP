// 共享 API 层 — 复用现有 axios 实例
// PC 端和移动端都通过此层调用后端

// 移动端使用的 request 实例（即 PC 端 axios service）
import service from '@/api/request'
export const request = service
export { service }

// 重新导出各模块 API 供移动端使用
export * from './auth'
export * from './cardflow'
export * from './mobile'
