// PPV 模块类型定义
export interface PpvTemplate {
  fid: number
  f组织ID: number
  f名称: string
  f岗位ID: number
  f产值项编码: string
  f产值项名称: string
  f单价: number
  f计量单位: string
  f启用状态: boolean
  f生效起期: string | null
  f生效止期: string | null
  f创建时间: string
  f更新时间: string
}

export interface PpvRecord {
  fid: number
  f组织ID: number
  f员工ID: number
  f期间: string
  f模板ID: number
  f产值项编码: string
  f数量: number
  f产值金额: number
  f质量等级: number // 1=A 2=B 3=C 4=D
  f是否跨岗: boolean
  f审核状态: number // 0=待审核 1=已通过 2=已驳回
  f审核人ID: number | null
  f审核时间: string | null
  f审核备注: string | null
  f创建时间: string
}

export interface PpvMonthlyResult {
  fid: number
  f组织ID: number
  f员工ID: number
  f期间: string
  f总产值: number
  f本岗产值: number
  f跨岗产值: number
  f综合质量等级: number
  f是否跨岗清零: boolean
  f清零原因: string | null
  fB分变化: number
  fA分变化: number
  f岗位ID快照: number
  f部门ID快照: number
  f状态: number // 1=正常 2=清零 3=异常
  f创建时间: string
}

export interface PpvViolation {
  fid: number
  f组织ID: number
  f员工ID: number
  f期间: string
  f违规类型: number // 1=质量违规 2=客诉 3=其他
  f关联单据ID: string
  f清零金额: number
  f处理状态: number // 0=待确认 1=已确认 2=已申诉
  f备注: string | null
  f创建时间: string
}

// 请求类型
export interface CreatePpvRecordRequest {
  f模板ID: number
  f产值项编码: string
  f数量: number
  f质量等级: number
  f是否跨岗: boolean
}

export interface ReviewPpvRecordRequest {
  approve: boolean
  remark?: string
}
