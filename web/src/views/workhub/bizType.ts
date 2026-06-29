import {
  AuditOutlined,
  FileTextOutlined,
  WarningOutlined,
  BellOutlined,
  CheckSquareOutlined,
  TrophyOutlined,
  ScheduleOutlined,
} from '@ant-design/icons-vue'
import type { Component } from 'vue'

interface BizTypeStyle {
  color: string
  icon: Component
}

// 按业务类型上色，彻底拆开旧 oa/cardflow 共用的 --biz-approval。
// 颜色复用 theme.ts 已有/新增的业务 token；前缀型 key（flow:xxx / wf:xxx）归入审批族。
const STYLE_BY_KEY: Record<string, BizTypeStyle> = {
  approval: { color: 'var(--biz-approval)', icon: AuditOutlined },
  voucher: { color: 'var(--biz-finance)', icon: FileTextOutlined },
  quality: { color: 'var(--biz-quality)', icon: WarningOutlined },
  contract: { color: 'var(--biz-contract)', icon: ScheduleOutlined },
  points: { color: 'var(--biz-points)', icon: TrophyOutlined },
  task: { color: 'var(--biz-task)', icon: CheckSquareOutlined },
  notification: { color: 'var(--biz-notification)', icon: BellOutlined },
}

const FALLBACK: BizTypeStyle = { color: 'var(--biz-approval)', icon: AuditOutlined }

/** 前缀型 key（flow:费用报销 / wf:用印申请）归并到审批族取色，但标签仍用后端给的细粒度文案。 */
export function bizTypeStyle(bizTypeKey: string | undefined): BizTypeStyle {
  if (!bizTypeKey) return FALLBACK
  if (bizTypeKey.startsWith('flow:') || bizTypeKey.startsWith('wf:')) return STYLE_BY_KEY.approval
  return STYLE_BY_KEY[bizTypeKey] ?? FALLBACK
}
