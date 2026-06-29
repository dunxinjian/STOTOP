import { formatPriceCell, parsePriceCell } from '../../shared/priceCellParser'
import type { CostCell } from '../composables/useCostMatrix'

function hasOverride(cell: Partial<CostCell>): boolean {
  return cell.roundingMethodOverride != null ||
    cell.truncParamOverride != null ||
    cell.ceilParamOverride != null
}

export function parseCostCellInput(value: string | null | undefined): CostCell | null {
  const parsed = parsePriceCell(value)
  if (!parsed) return null

  return {
    basePrice: parsed.basePrice,
    firstWeight: parsed.firstWeight,
    continuePrice: parsed.continuePrice,
    continueStep: parsed.continueStep,
  }
}

export function mergeCostCellInput(existingCell: CostCell | undefined, value: string | null | undefined): CostCell | null {
  const parsed = parseCostCellInput(value)
  if (!parsed) return null

  return {
    ...parsed,
    roundingMethodOverride: existingCell?.roundingMethodOverride ?? null,
    truncParamOverride: existingCell?.truncParamOverride ?? null,
    ceilParamOverride: existingCell?.ceilParamOverride ?? null,
  }
}

export function formatCostCell(cell: Partial<CostCell> | null | undefined): string {
  if (!cell || cell.basePrice == null) return ''

  return formatPriceCell({
    basePrice: cell.basePrice,
    firstWeight: cell.firstWeight ?? 0,
    continuePrice: cell.continuePrice ?? 0,
    continueStep: cell.continueStep ?? 1,
  })
}

export function hasCostCellValue(cell: Partial<CostCell> | null | undefined): boolean {
  if (!cell || cell.basePrice == null) return false
  if (cell.isConfigured) return true

  return cell.basePrice > 0 ||
    (cell.continuePrice ?? 0) > 0 ||
    (cell.firstWeight ?? 0) > 0 ||
    (cell.continueStep ?? 1) !== 1 ||
    hasOverride(cell)
}
