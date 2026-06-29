import { parsePriceCell } from './priceCellParser'

export interface PastedPriceCellValue {
  segmentIndex: number
  provinceId: number
  basePrice: number
  firstWeight: number
  continuePrice: number
  continueStep: number
  roundingMethodOverride: number | null
  truncParamOverride: number | null
  ceilParamOverride: number | null
}

export function normalizePastedMatrixLines(text: string | null | undefined): string[] {
  if (text == null) return []

  return String(text)
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n')
    .map(line => line.replace(/[ \t]+$/g, ''))
    .filter(line => line.trim())
}

/**
 * 解析列头重量段文本，如 "0-1kg" -> { startWeight: 0, endWeight: 1 }
 * 支持格式: "0-1kg", "1-3kg", "30-999kg", "30kg以上" 等。
 */
export function parsePastedSegmentHeader(header: string): { startWeight: number; endWeight: number | null } | null {
  const trimmed = header.trim()
  const h = trimmed.replace(/kg/gi, '').replace(/以上/g, '')
  const m = h.match(/^([\d.]+)[-–]([\d.]+)$/)
  if (m) {
    return { startWeight: parseFloat(m[1]), endWeight: parseFloat(m[2]) }
  }

  const m2 = trimmed.match(/^([\d.]+)\s*kg?\s*以上$/)
  if (m2) {
    return { startWeight: parseFloat(m2[1]), endWeight: null }
  }

  const m3 = h.match(/^([\d.]+)$/)
  if (m3) {
    return { startWeight: parseFloat(m3[1]), endWeight: null }
  }

  return null
}

export function parsePastedPriceCellValue(
  value: string | null | undefined,
  segmentIndex: number,
  provinceId: number,
  // 只读取 override 三字段，价格字段允许为 null（与可清空的 PriceCell 兼容）
  existingCell?: {
    roundingMethodOverride?: number | null
    truncParamOverride?: number | null
    ceilParamOverride?: number | null
  } | null,
): PastedPriceCellValue | null {
  const parsed = parsePriceCell(value)
  if (!parsed) return null

  return {
    segmentIndex,
    provinceId,
    basePrice: parsed.basePrice,
    firstWeight: parsed.firstWeight,
    continuePrice: parsed.continuePrice,
    continueStep: parsed.continueStep,
    roundingMethodOverride: existingCell?.roundingMethodOverride ?? null,
    truncParamOverride: existingCell?.truncParamOverride ?? null,
    ceilParamOverride: existingCell?.ceilParamOverride ?? null,
  }
}
