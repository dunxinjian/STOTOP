import type {
  QuotationDto,
  WeightSegmentDto,
  PriceCellDto,
  ProvinceDto,
  WeightSegmentInput,
  PriceCellInput,
  CreateQuotationRequest,
  UpdateQuotationRequest,
} from '@/api/express'
import {
  parsePriceCell,
  formatPriceCell,
  validatePriceCellInput,
  type ParsedCell,
} from '@/views/express/shared/priceCellParser'

// ==================== 类型定义 ====================

export interface WeightSegment {
  id?: number             // 后端ID（编辑模式下存在）
  sortOrder: number       // 段序号
  startWeight: number     // 起始重量
  endWeight: number | null // 截止重量（null = 开放段，如"3以上"）
  roundingMethod: number  // 重量进位方式（默认1=实际重量）
  truncParam: number | null  // 舍位参数
  ceilParam: number | null   // 进位参数
}

export interface PriceCell {
  segmentIndex: number    // 对应重量段索引
  provinceId: number      // 省份ID
  basePrice: number | null  // 基础价格（0表示未填，null表示清空/未编辑，提交时跳过）
  firstWeight: number     // 首重(kg)（默认0）
  continuePrice: number | null  // 续重价格（0或null=固定单价）
  continueStep: number    // 续重步进(kg)（默认1）
  roundingMethodOverride: number | null   // 进位方式覆盖
  truncParamOverride: number | null   // 舍位参数覆盖
  ceilParamOverride: number | null    // 进位参数覆盖
}

export interface ProvinceRow {
  provinceId: number
  provinceName: string
  region: string          // 大区名称（一区、二区等）
  regionIndex: number     // 大区序号（用于排序和分组）
  isRemote: boolean       // 是否偏远
  prices: Record<number, PriceCell>  // key = segmentIndex (sortOrder)
}

// ==================== 表单数据类型 ====================

export interface QuotationFormData {
  clientType?: string
  // 业务对象编号：KH/DL/YZ 等类型是字母前缀编号字符串（如 KH00000001），WD/CB 是数字ID。
  // 不可用 Number() 强转——字符串编号会变 NaN，保存时把绑定写成字面量 "NaN"
  clientId: string | number | undefined
  planName: string
  networkPointCode?: string
  sharedShopEnabled?: boolean
  settlementWeightStage: number
  // 商务条款字段
  paymentMode?: string        // 付款方式：prepay/postpay/mixed
  prepayRatio?: number        // 预付比例 %
  billingCycle?: string       // 账单周期：week/month/quarter/year
  billingDay?: number         // 出账日
  paymentDueDay?: number      // 付款截止日
  weightRoundingMethod?: number  // 重量进位方式（1实际/2四舍五入/3点五进位/4进位舍位/5向上取整/6分段进位/7进舍百位）
  throwRatio?: number         // 抛比（默认8000）
  insuranceRate?: number      // 保价费率 %
  remark?: string             // 备注
  oaApprovalInfo?: string     // OA审批信息（只读）
  effectiveDate?: string      // 生效日期 YYYY-MM-DD
}

// ==================== "X+Y" 格式解析和格式化 ====================

/**
 * 将 PriceCell 格式化为显示字符串（A3' 统一后 method 参数仅为向后兼容，实际以 continuePrice 判别）
 */
export function formatCellValue(cell: PriceCell, _method?: 1 | 3): string {
  if (cell.basePrice == null) return ''
  return formatPriceCell({
    basePrice: cell.basePrice,
    firstWeight: cell.firstWeight,
    continuePrice: cell.continuePrice ?? 0,
    continueStep: cell.continueStep,
  })
}

/**
 * 解析输入字符串为 PriceCell 部分字段
 * 统一使用 "a" / "a+b" 格式解析为 basePrice + continuePrice（method 仅为向后兼容）
 */
export function parseCellValue(value: string, _method?: 1 | 3): Partial<PriceCell> {
  const parsed = parsePriceCell(value)
  if (!parsed) return {}
  return {
    basePrice: parsed.basePrice,
    firstWeight: parsed.firstWeight || 0,
    continuePrice: parsed.continuePrice || 0,
    continueStep: parsed.continueStep !== 1 ? parsed.continueStep : 1,
  }
}

/**
 * 校验输入字符串是否合法（统一使用 parser 的校验逻辑）
 */
export function validateCellInput(value: string, _method?: 1 | 3): { valid: boolean; error?: string } {
  return validatePriceCellInput(value)
}

// ==================== 单元格覆盖检测 ====================

/**
 * 检查单元格是否有覆盖值
 */
export function hasCellOverride(cell: PriceCell): boolean {
  return cell.roundingMethodOverride != null
    || cell.truncParamOverride != null
    || cell.ceilParamOverride != null
}

// ==================== 矩阵数据管理 ====================

/**
 * 添加重量段时扩展矩阵（为每个省份行添加新列的空单元格）
 */
export function addSegmentColumn(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  newSegment: WeightSegment,
): void {
  segments.push(newSegment)
  // 为每个省份行添加该段的空价格单元格
  for (const row of matrix) {
    row.prices[newSegment.sortOrder] = createEmptyCell(newSegment.sortOrder, row.provinceId)
  }
}

/**
 * 删除重量段时收缩矩阵
 * 删除后重新编号所有段及矩阵中的 key
 */
export function removeSegmentColumn(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  index: number,
): void {
  const removedSortOrder = segments[index].sortOrder
  segments.splice(index, 1)

  // 删除对应列的价格数据
  for (const row of matrix) {
    delete row.prices[removedSortOrder]
  }

  // 重新编号 sortOrder
  const oldPricesMap = new Map<number, Map<number, PriceCell>>() // provinceId -> old prices by old sortOrder
  for (const row of matrix) {
    oldPricesMap.set(row.provinceId, new Map())
    for (const [key, cell] of Object.entries(row.prices)) {
      oldPricesMap.get(row.provinceId)!.set(Number(key), cell)
    }
    row.prices = {}
  }

  segments.forEach((seg, i) => {
    const oldOrder = seg.sortOrder
    seg.sortOrder = i + 1
    for (const row of matrix) {
      const oldCell = oldPricesMap.get(row.provinceId)?.get(oldOrder)
      if (oldCell) {
        oldCell.segmentIndex = seg.sortOrder
        row.prices[seg.sortOrder] = oldCell
      } else {
        row.prices[seg.sortOrder] = createEmptyCell(seg.sortOrder, row.provinceId)
      }
    }
  })
}

/**
 * 分割重量段：在 splitPoint 处将段一分为二
 * 返回是否成功
 */
export function splitSegmentColumn(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  segmentIndex: number,
  splitPoint: number,
): boolean {
  const seg = segments[segmentIndex]
  if (!seg) return false

  // 校验分割点
  if (splitPoint <= seg.startWeight) return false
  if (seg.endWeight != null && splitPoint >= seg.endWeight) return false

  // 创建左段和右段
  const leftSeg: WeightSegment = {
    ...seg,
    endWeight: splitPoint,
  }
  const rightSeg: WeightSegment = {
    sortOrder: seg.sortOrder + 1,
    startWeight: splitPoint,
    endWeight: seg.endWeight,
    roundingMethod: seg.roundingMethod,
    truncParam: seg.truncParam,
    ceilParam: seg.ceilParam,
  }

  // 替换原段为左段
  segments[segmentIndex] = leftSeg

  // 在后面插入右段
  segments.splice(segmentIndex + 1, 0, rightSeg)

  // 重新编号 sortOrder 并更新矩阵
  reindexSegments(segments, matrix, segmentIndex)

  // 为右段复制左段的价格数据
  for (const row of matrix) {
    const leftCell = row.prices[leftSeg.sortOrder]
    if (leftCell) {
      row.prices[rightSeg.sortOrder] = { ...leftCell, segmentIndex: rightSeg.sortOrder }
    } else {
      row.prices[rightSeg.sortOrder] = createEmptyCell(rightSeg.sortOrder, row.provinceId)
    }
  }

  return true
}

/**
 * 合并重量段：删除指定段并与相邻段合并
 * 保留右侧段的价格数据（或左侧段如果删的是最后一个）
 */
export function mergeSegmentColumn(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  index: number,
): boolean {
  if (segments.length <= 1) return false

  const removedSeg = segments[index]

  if (index < segments.length - 1) {
    // 与右侧段合并：扩展右侧段的起始重量
    segments[index + 1].startWeight = removedSeg.startWeight
  } else {
    // 删除最后一段，与左侧段合并：扩展左侧段的截止重量
    segments[index - 1].endWeight = removedSeg.endWeight
  }

  // 删除段并移除对应列
  removeSegmentColumn(segments, matrix, index)
  return true
}

/**
 * 与左侧段合并：删除当前段，左侧段的 endWeight 扩展到当前段的 endWeight
 * 保留左侧段的价格数据与计价属性。
 * @returns true 表示成功；false 表示 index 不合法（首段不能左合并、或越界）
 */
export function mergeSegmentLeft(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  index: number,
): boolean {
  if (index <= 0 || index >= segments.length) return false
  const removedSeg = segments[index]
  segments[index - 1].endWeight = removedSeg.endWeight
  removeSegmentColumn(segments, matrix, index)
  return true
}

/**
 * 与右侧段合并：删除当前段，右侧段的 startWeight 收缩到当前段的 startWeight
 * 保留右侧段的价格数据与计价属性。
 * @returns true 表示成功；false 表示 index 不合法（末段不能右合并、或越界）
 */
export function mergeSegmentRight(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
  index: number,
): boolean {
  if (index < 0 || index >= segments.length - 1) return false
  const removedSeg = segments[index]
  segments[index + 1].startWeight = removedSeg.startWeight
  removeSegmentColumn(segments, matrix, index)
  return true
}

/**
 * 清空某列数据（计价方式切换时用）
 */
export function clearSegmentColumn(matrix: ProvinceRow[], segmentIndex: number): void {
  for (const row of matrix) {
    const cell = row.prices[segmentIndex]
    if (cell) {
      cell.basePrice = 0
      cell.continuePrice = 0
      cell.firstWeight = 0
      cell.continueStep = 1
      cell.roundingMethodOverride = null
      cell.truncParamOverride = null
      cell.ceilParamOverride = null
    }
  }
}

/** 重新编号段及矩阵中的 key（从 fromIndex 开始） */
function reindexSegments(segments: WeightSegment[], matrix: ProvinceRow[], _fromIndex: number): void {
  // 收集旧价格数据
  const oldPricesMap = new Map<number, Map<number, PriceCell>>()
  for (const row of matrix) {
    oldPricesMap.set(row.provinceId, new Map())
    for (const [key, cell] of Object.entries(row.prices)) {
      oldPricesMap.get(row.provinceId)!.set(Number(key), cell)
    }
    row.prices = {}
  }

  segments.forEach((seg, i) => {
    const oldOrder = seg.sortOrder
    seg.sortOrder = i + 1
    for (const row of matrix) {
      const oldCell = oldPricesMap.get(row.provinceId)?.get(oldOrder)
      if (oldCell) {
        oldCell.segmentIndex = seg.sortOrder
        row.prices[seg.sortOrder] = oldCell
      } else {
        row.prices[seg.sortOrder] = createEmptyCell(seg.sortOrder, row.provinceId)
      }
    }
  })
}

/** 创建空单元格 */
function createEmptyCell(segmentIndex: number, provinceId: number): PriceCell {
  return {
    segmentIndex,
    provinceId,
    basePrice: 0,
    firstWeight: 0,
    continuePrice: 0,
    continueStep: 1,
    roundingMethodOverride: null,
    truncParamOverride: null,
    ceilParamOverride: null,
  }
}

// ==================== 后端 DTO 双向转换 ====================

// 付款方式/账单周期：前端字符串枚举 ⇄ 后端整数枚举
const PAYMENT_MODE_TO_INT: Record<string, number> = { prepay: 1, postpay: 2, mixed: 3 }
const PAYMENT_MODE_TO_STR: Record<number, string> = { 1: 'prepay', 2: 'postpay', 3: 'mixed' }
const BILLING_CYCLE_TO_INT: Record<string, number> = { week: 1, month: 2, quarter: 3, year: 4 }
const BILLING_CYCLE_TO_STR: Record<number, string> = { 1: 'week', 2: 'month', 3: 'quarter', 4: 'year' }

/**
 * 商务条款字段映射（UI 百分比 → 后端小数：0.3% 存为 0.003，计费引擎按声明价值×费率直接相乘）
 */
function buildCommercialTerms(formData: QuotationFormData) {
  return {
    paymentMode: formData.paymentMode ? PAYMENT_MODE_TO_INT[formData.paymentMode] ?? 2 : 2,
    prepayRatio: formData.prepayRatio != null ? formData.prepayRatio / 100 : undefined,
    billingCycle: formData.billingCycle ? BILLING_CYCLE_TO_INT[formData.billingCycle] ?? 2 : 2,
    billingDay: formData.billingDay,
    paymentDueDay: formData.paymentDueDay,
    throwRatio: formData.throwRatio ?? 8000,
    insuranceRate: formData.insuranceRate != null ? formData.insuranceRate / 100 : undefined,
    remark: formData.remark,
  }
}

/**
 * 将前端数据转为 API 创建请求格式
 */
export function toCreatePayload(
  formData: QuotationFormData,
  segments: WeightSegment[],
  matrix: ProvinceRow[],
): CreateQuotationRequest {
  return {
    clientId: formData.clientId != null ? String(formData.clientId) : undefined,
    clientType: formData.clientType || 'KH',
    planName: formData.planName,
    networkPointCode: formData.networkPointCode || undefined,
    sharedShopEnabled: formData.sharedShopEnabled,
    settlementWeightStage: formData.settlementWeightStage,
    weightRoundingMethod: formData.weightRoundingMethod,
    effectiveDate: formData.effectiveDate,
    ...buildCommercialTerms(formData),
    segments: buildSegmentInputs(segments, matrix),
  }
}

/**
 * 将前端数据转为 API 更新请求格式
 */
export function toUpdatePayload(
  formData: QuotationFormData,
  segments: WeightSegment[],
  matrix: ProvinceRow[],
): UpdateQuotationRequest {
  return {
    clientId: formData.clientId != null ? String(formData.clientId) : undefined,
    clientType: formData.clientType || undefined,
    planName: formData.planName,
    networkPointCode: formData.networkPointCode || undefined,
    sharedShopEnabled: formData.sharedShopEnabled,
    settlementWeightStage: formData.settlementWeightStage,
    weightRoundingMethod: formData.weightRoundingMethod,
    effectiveDate: formData.effectiveDate,
    ...buildCommercialTerms(formData),
    segments: buildSegmentInputs(segments, matrix),
  }
}

function buildSegmentInputs(segments: WeightSegment[], matrix: ProvinceRow[]): WeightSegmentInput[] {
  return segments.map(seg => {
    const cells: PriceCellInput[] = []
    for (const row of matrix) {
      const cell = row.prices[seg.sortOrder]
      if (!cell) continue
      // basePrice 为 null（清空操作）或为 0 且无覆盖字段时跳过（未填）。
      // null 不跳过会序列化为 JSON null，后端 BasePrice 是非空 decimal，整个保存请求会 400。
      if (cell.basePrice == null) continue
      if (cell.basePrice === 0 && !hasCellOverride(cell)) continue
      cells.push({
        provinceId: row.provinceId,
        basePrice: cell.basePrice,
        continuePrice: cell.continuePrice ?? 0,
        firstWeight: cell.firstWeight ?? 0,
        continueStep: cell.continueStep ?? 1,
        roundingMethodOverride: cell.roundingMethodOverride ?? undefined,
        truncParamOverride: cell.truncParamOverride ?? undefined,
        ceilParamOverride: cell.ceilParamOverride ?? undefined,
      })
    }
    return {
      segmentIndex: seg.sortOrder,
      weightFrom: seg.startWeight,
      weightTo: seg.endWeight ?? undefined,
      roundingMethod: seg.roundingMethod,
      truncParam: seg.truncParam ?? undefined,
      ceilParam: seg.ceilParam ?? undefined,
      cells,
    }
  })
}

/**
 * 将 API 返回的 QuotationDto 转为前端格式
 */
export function fromApiResponse(dto: QuotationDto): {
  formData: QuotationFormData
  segments: WeightSegment[]
  cellMap: Map<string, PriceCell> // key = `${segmentIndex}_${provinceId}`
} {
  const formData: QuotationFormData = {
    clientType: dto.clientType || undefined,
    clientId: dto.clientId || undefined,
    planName: dto.planName,
    networkPointCode: dto.networkPointCode || '',
    sharedShopEnabled: dto.sharedShopEnabled ?? false,
    settlementWeightStage: dto.settlementWeightStage ?? 1,
    weightRoundingMethod: dto.weightRoundingMethod ?? 5,
    effectiveDate: dto.effectiveDate || undefined,
    // 商务条款回显（后端小数 → UI 百分比）
    paymentMode: dto.paymentMode != null ? PAYMENT_MODE_TO_STR[dto.paymentMode] : undefined,
    prepayRatio: dto.prepayRatio != null ? dto.prepayRatio * 100 : undefined,
    billingCycle: dto.billingCycle != null ? BILLING_CYCLE_TO_STR[dto.billingCycle] : undefined,
    billingDay: dto.billingDay ?? undefined,
    paymentDueDay: dto.paymentDueDay ?? undefined,
    throwRatio: dto.throwRatio ?? 8000,
    insuranceRate: dto.insuranceRate != null ? dto.insuranceRate * 100 : undefined,
    remark: dto.remark ?? undefined,
  }

  const segments: WeightSegment[] = (dto.segments || [])
    .sort((a, b) => a.segmentIndex - b.segmentIndex)
    .map(s => ({
      id: s.id,
      sortOrder: s.segmentIndex,
      startWeight: s.weightFrom,
      endWeight: s.weightTo ?? null,
      roundingMethod: s.roundingMethod ?? 1,
      truncParam: s.truncParam ?? null,
      ceilParam: s.ceilParam ?? null,
    }))

  // 构建 segmentId -> segmentIndex 映射
  const segIdToIndex = new Map<number, number>()
  ;(dto.segments || []).forEach(s => {
    segIdToIndex.set(s.id, s.segmentIndex)
  })

  const cellMap = new Map<string, PriceCell>()
  ;(dto.cells || []).forEach(c => {
    const segIndex = segIdToIndex.get(c.segmentId)
    if (segIndex == null) return
    const key = `${segIndex}_${c.provinceId}`
    cellMap.set(key, {
      segmentIndex: segIndex,
      provinceId: c.provinceId,
      basePrice: c.basePrice ?? 0,
      continuePrice: c.continuePrice ?? 0,
      firstWeight: c.firstWeight ?? 0,
      continueStep: c.continueStep ?? 1,
      roundingMethodOverride: c.roundingMethodOverride ?? null,
      truncParamOverride: c.truncParamOverride ?? null,
      ceilParamOverride: c.ceilParamOverride ?? null,
    })
  })

  return {
    formData,
    segments,
    cellMap,
  }
}

// ==================== 省份分组 ====================

/** 大区排序映射（固定顺序，未知大区排最后） */
const REGION_ORDER: Record<string, number> = {
  '一区': 1, '二区': 2, '三区': 3, '四区': 4, '五区': 5,
  '六区': 6, '七区': 7, '八区': 8, '偏远': 99,
}

function getRegionIndex(region: string): number {
  return REGION_ORDER[region] ?? 50
}

/**
 * 将省份列表按大区分组排序，返回 ProvinceRow[]
 * 每行初始化空的 prices
 */
export function groupProvincesByRegion(provinces: ProvinceDto[]): ProvinceRow[] {
  return provinces
    .map(p => ({
      provinceId: p.id,
      provinceName: p.shortName || p.name,
      region: p.region,
      regionIndex: getRegionIndex(p.region),
      isRemote: p.isRemote,
      prices: {} as Record<number, PriceCell>,
    }))
    .sort((a, b) => {
      if (a.regionIndex !== b.regionIndex) return a.regionIndex - b.regionIndex
      return a.provinceId - b.provinceId
    })
}

/**
 * 为矩阵中所有省份行填充重量段的空单元格
 */
export function initMatrixCells(
  matrix: ProvinceRow[],
  segments: WeightSegment[],
  cellMap?: Map<string, PriceCell>,
): void {
  for (const row of matrix) {
    for (const seg of segments) {
      const key = `${seg.sortOrder}_${row.provinceId}`
      const existing = cellMap?.get(key)
      row.prices[seg.sortOrder] = existing
        ? { ...existing }
        : createEmptyCell(seg.sortOrder, row.provinceId)
    }
  }
}

/**
 * 获取所有不同的大区名称列表（保持排序）
 */
export function getRegionList(matrix: ProvinceRow[]): string[] {
  const seen = new Set<string>()
  const result: string[] = []
  for (const row of matrix) {
    if (!seen.has(row.region)) {
      seen.add(row.region)
      result.push(row.region)
    }
  }
  return result
}

// ==================== 矩阵完整性校验 ====================

export interface MatrixGap {
  segmentLabel: string    // 如 "0-1kg", "1-3kg", "3kg以上"
  segmentIndex: number
  missingProvinces: string[]  // 缺失的省份名称列表
}

/**
 * 校验矩阵完整性：检查每个重量段是否所有省份都填写了价格
 * 返回按重量段分组的缺失信息，空数组表示全部填写完整
 */
export function validateMatrixCompleteness(
  segments: WeightSegment[],
  matrix: ProvinceRow[],
): MatrixGap[] {
  const gaps: MatrixGap[] = []

  for (const seg of segments) {
    const missing: string[] = []

    for (const row of matrix) {
      const cell = row.prices[seg.sortOrder]
      // basePrice > 0 即视为已填；粘贴填充后未覆盖的省份 cell 可能为 undefined，需判空
      const hasPrice = cell != null && cell.basePrice != null && cell.basePrice > 0

      if (!hasPrice) {
        missing.push(row.provinceName)
      }
    }

    if (missing.length > 0) {
      const label = seg.endWeight != null
        ? `${seg.startWeight}-${seg.endWeight}kg`
        : `${seg.startWeight}kg以上`
      gaps.push({
        segmentLabel: label,
        segmentIndex: seg.sortOrder,
        missingProvinces: missing,
      })
    }
  }

  return gaps
}

/**
 * 统计已填充的单元格数量
 */
export function countFilledCells(matrix: ProvinceRow[]): number {
  let count = 0
  for (const row of matrix) {
    for (const cell of Object.values(row.prices)) {
      if (cell.basePrice != null && cell.basePrice > 0) {
        count++
      }
    }
  }
  return count
}
