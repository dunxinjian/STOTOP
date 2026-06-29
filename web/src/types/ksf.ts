// KSF 模块类型定义
export interface KsfIndicator {
  FID: number
  F编码: string
  F名称: string
  F计量单位: string
  F取数类型: number
  F方向: number
  F是否启用: boolean
}

export interface KsfPlan {
  FID: number
  F名称: string
  F岗位ID: number
  F启用状态: boolean
  F运行模式: number // 0=试运行 1=正式
  F岗位月加薪基数: number
}

export interface KsfResult {
  FID: number
  F员工ID: number
  F期间: string
  F固定部分: number
  F浮动部分: number
  F加薪: number
  F扣减: number
  F实发: number
  F状态: number // 1=试运行 2=正式 3=取数异常
}
