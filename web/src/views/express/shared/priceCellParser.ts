/**
 * 价格单元格统一解析/格式化工具（一口价 + 快递报价共享）
 *
 * 字段语义（A3' 方案）：
 *   - basePrice：必填基础价
 *   - firstWeight：首重(kg)，0=无首重偏移
 *   - continuePrice：续重单价，0=固定单价
 *   - continueStep：续重步进(kg)，1=默认步进
 *
 * 字符串格式：
 *   - "5"              → basePrice=5, firstWeight=0, continuePrice=0, continueStep=1
 *   - "3+5"            → basePrice=3, firstWeight=0, continuePrice=5, continueStep=1
 *   - "3+(w-1)*5"      → basePrice=3, firstWeight=1, continuePrice=5, continueStep=1
 *   - "3+5/0.5"        → basePrice=3, firstWeight=0, continuePrice=5, continueStep=0.5
 *   - "3+(w-1)*5/0.5"  → basePrice=3, firstWeight=1, continuePrice=5, continueStep=0.5
 */

export interface ParsedCell {
  basePrice: number
  firstWeight: number
  continuePrice: number
  continueStep: number
}

/** 去除不必要尾零：5.0 → "5"，5.10 → "5.1" */
function fmt(n: number): string {
  return parseFloat(n.toFixed(4)).toString()
}

/**
 * 解析价格字符串为 ParsedCell
 * - 空字符串 / null / undefined → null
 * - 格式不匹配 → null
 */
export function parsePriceCell(value: string | null | undefined): ParsedCell | null {
  if (value == null) return null
  const trimmed = String(value).trim()
  if (!trimmed) return null

  // 正则：^(\d+\.?\d*)(?:\+(?:\(w-(\d+\.?\d*)\)\*)?(\d+\.?\d*)(?:\/(\d+\.?\d*))?)?$
  const re = /^(\d+\.?\d*)(?:\+(?:\(w-(\d+\.?\d*)\)\*)?(\d+\.?\d*)(?:\/(\d+\.?\d*))?)?$/
  const m = trimmed.match(re)
  if (!m) return null

  const basePrice = parseFloat(m[1])
  if (isNaN(basePrice)) return null

  // 无 + 号部分：固定单价
  if (m[2] === undefined && m[3] === undefined) {
    return { basePrice, firstWeight: 0, continuePrice: 0, continueStep: 1 }
  }

  const firstWeight = m[2] !== undefined ? parseFloat(m[2]) : 0
  const continuePrice = parseFloat(m[3])
  const continueStep = m[4] !== undefined ? parseFloat(m[4]) : 1

  if (isNaN(firstWeight) || isNaN(continuePrice) || isNaN(continueStep)) return null
  if (continueStep === 0) return null

  return { basePrice, firstWeight, continuePrice, continueStep }
}

/**
 * 格式化 ParsedCell 为显示字符串
 *
 * 规则：
 *   - continuePrice==0                        → "a"
 *   - continuePrice>0, firstWeight==0, step==1 → "a+b"
 *   - continuePrice>0, firstWeight>0, step==1  → "a+(w-f)*b"
 *   - continuePrice>0, firstWeight==0, step!=1 → "a+b/s"
 *   - continuePrice>0, firstWeight>0, step!=1  → "a+(w-f)*b/s"
 */
export function formatPriceCell(cell: ParsedCell | null | undefined): string {
  if (!cell) return ''
  const { basePrice, firstWeight, continuePrice, continueStep } = cell

  if (continuePrice === 0) return fmt(basePrice)

  const base = fmt(basePrice)
  const cont = fmt(continuePrice)
  const step = continueStep !== 1 ? `/${fmt(continueStep)}` : ''
  const fw = firstWeight > 0 ? `(w-${fmt(firstWeight)})*` : ''

  return `${base}+${fw}${cont}${step}`
}

/** 判断单元格是否为"已填"（basePrice 有值且合法） */
export function isCellFilled(cell: ParsedCell | null | undefined): boolean {
  return cell != null && !isNaN(cell.basePrice)
}

/**
 * 校验输入字符串是否合法
 * 空值视为合法（用于"未填"语义）
 */
export function validatePriceCellInput(value: string | null | undefined): { valid: boolean; error?: string } {
  if (value == null) return { valid: true }
  const trimmed = String(value).trim()
  if (!trimmed) return { valid: true }

  const re = /^(\d+\.?\d*)(?:\+(?:\(w-(\d+\.?\d*)\)\*)?(\d+\.?\d*)(?:\/(\d+\.?\d*))?)?$/
  const m = trimmed.match(re)
  if (!m) {
    return { valid: false, error: '格式应为 "5"、"3+5"、"3+(w-1)*5"、"3+5/0.5" 或 "3+(w-1)*5/0.5"' }
  }

  const basePrice = parseFloat(m[1])
  if (isNaN(basePrice) || basePrice < 0) {
    return { valid: false, error: '基础价格无效' }
  }

  if (m[3] !== undefined) {
    const continuePrice = parseFloat(m[3])
    if (isNaN(continuePrice) || continuePrice < 0) {
      return { valid: false, error: '续重价格无效' }
    }
    if (m[2] !== undefined) {
      const firstWeight = parseFloat(m[2])
      if (isNaN(firstWeight) || firstWeight < 0) {
        return { valid: false, error: '首重无效' }
      }
    }
    if (m[4] !== undefined) {
      const continueStep = parseFloat(m[4])
      if (isNaN(continueStep) || continueStep <= 0) {
        return { valid: false, error: '续重步进必须大于0' }
      }
    }
  }

  return { valid: true }
}

/**
 * ChangeLog 三档兼容解析：用于报价变更日志的旧值/新值字段
 * - 优先解析为 ParsedCell
 * - 解析失败时返回原始字符串（保留兼容）
 */
export function parseChangeLogPriceCell(value: string | null | undefined): ParsedCell | string {
  if (value == null) return ''
  const parsed = parsePriceCell(value)
  if (parsed != null) return parsed
  return String(value)
}
