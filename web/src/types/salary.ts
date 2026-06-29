// Salary 模块类型定义

// 薪酬档位
export interface SalaryGrade {
  fid: number
  f组织ID: number
  f编码: string
  f名称: string
  f级别: number
  f基本工资: number
  f岗位ID: number | null
  f备注: string | null
  f启用状态: boolean
  f生效起期: string | null
  f生效止期: string | null
  f创建时间: string
  f更新时间: string
}

// 员工薪酬档案
export interface SalaryArchive {
  fid: number
  f组织ID: number
  f员工ID: number
  f员工姓名: string
  f档位ID: number
  f档位名称: string
  f基本工资: number
  f社保基数: number
  f公积金基数: number
  f银行账号: string | null
  f开户行: string | null
  f备注: string | null
  f启用状态: boolean
  f生效起期: string | null
  f生效止期: string | null
  f创建时间: string
  f更新时间: string
}

// 月度工资单
export interface SalaryPayroll {
  fid: number
  f组织ID: number
  f员工ID: number
  f员工姓名: string
  f期间: string
  f档位ID: number
  f基本工资: number
  fKSF金额: number
  fPPV金额: number
  f加项合计: number
  f扣项合计: number
  f社保扣款: number
  f公积金扣款: number
  f个税: number
  f应发合计: number
  f实发合计: number
  fB分兑换金额: number
  f状态: number // 0=草稿 1=待审 2=已审 3=已发放 4=已驳回
  f审核人ID: number | null
  f审核时间: string | null
  f发放时间: string | null
  f备注: string | null
  f创建时间: string
}

// 工资单明细
export interface SalaryPayrollDetail {
  fid: number
  f工资单ID: number
  f项目类型: number // 1=加项 2=扣项 3=基本 4=绩效 5=社保 6=公积金 7=税金 8=兑换
  f项目编码: string
  f项目名称: string
  f金额: number
  f备注: string | null
}

// 晋升规则
export interface PromotionRule {
  fid: number
  f组织ID: number
  f编码: string
  f名称: string
  f当前档位ID: number
  f当前档位名称: string
  f目标档位ID: number
  f目标档位名称: string
  fA分阈值: number
  f连续达标月数: number
  f备注: string | null
  f启用状态: boolean
  f创建时间: string
  f更新时间: string
}

// 晋升评审
export interface PromotionReview {
  fid: number
  f组织ID: number
  f员工ID: number
  f员工姓名: string
  f当前档位ID: number
  f当前档位名称: string
  f目标档位ID: number
  f目标档位名称: string
  f规则ID: number | null
  fA分累计: number
  f申请时间: string
  f审核状态: number // 0=待审 1=通过 2=驳回
  f审核人ID: number | null
  f审核时间: string | null
  f审核备注: string | null
  f备注: string | null
}

// 晋升历史
export interface PromotionHistory {
  fid: number
  f组织ID: number
  f员工ID: number
  f员工姓名: string
  f原档位ID: number
  f原档位名称: string
  f新档位ID: number
  f新档位名称: string
  f生效时间: string
  f评审ID: number | null
  f备注: string | null
  f创建时间: string
}

// B 分兑换
export interface SalaryBScoreConversion {
  fid: number
  f组织ID: number
  f员工ID: number
  f期间: string
  fB分数量: number
  f兑换比例: number
  f兑换金额: number
  f工资单ID: number | null
  f创建时间: string
}

// ===== 请求类型 =====

export interface CreateSalaryGradeRequest {
  f编码: string
  f名称: string
  f级别: number
  f基本工资: number
  f岗位ID?: number | null
  f备注?: string | null
  f生效起期?: string | null
  f生效止期?: string | null
}

export interface UpdateSalaryGradeRequest extends CreateSalaryGradeRequest {
  fid: number
  f启用状态?: boolean
}

export interface CreateSalaryArchiveRequest {
  f员工ID: number
  f档位ID: number
  f基本工资: number
  f社保基数: number
  f公积金基数: number
  f银行账号?: string | null
  f开户行?: string | null
  f备注?: string | null
  f生效起期?: string | null
}

export interface UpdateSalaryArchiveRequest extends CreateSalaryArchiveRequest {
  fid: number
}

export interface AuditPayrollRequest {
  approve: boolean
  remark?: string
}

export interface PayPayrollRequest {
  payDate: string
  remark?: string
}

export interface CreatePromotionRuleRequest {
  f编码: string
  f名称: string
  f当前档位ID: number
  f目标档位ID: number
  fA分阈值: number
  f连续达标月数: number
  f备注?: string | null
}

export interface UpdatePromotionRuleRequest extends CreatePromotionRuleRequest {
  fid: number
  f启用状态?: boolean
}

export interface ReviewPromotionRequest {
  approve: boolean
  remark?: string
}
