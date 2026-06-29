import { ref, watch, type Ref } from 'vue'
import type { CostPlanMatrixDto, CostItemEntryDto, CostItemInput, CostSegmentInput, CostCellInput, ProvinceDto, CostItemDto } from '@/api/express'
import { hasCostCellValue } from '../utils/costPriceCell'

// ==================== 类型定义 ====================

/** 重量段定义（每个成本项目独立） */
export interface CostWeightSegment {
  index: number
  weightFrom: number
  weightTo: number | null    // null=开放段(10kg以上)
  roundingMethod: number    // 重量进位方式（默认1）
  truncParam: number | null // 舍位参数
  ceilParam: number | null  // 进位参数
}

/** 矩阵单元格 */
export interface CostCell {
  basePrice: number           // 所有 calcMethod 统一使用
  continuePrice: number       // calcMethod=3: 续重价格（0=固定单价）
  firstWeight: number         // 首重(kg)，默认0
  continueStep: number        // 续重步进(kg)，默认1
  isConfigured?: boolean      // 是否为用户显式填写/后端返回的单元格；用于区分 0 元与空值
  roundingMethodOverride?: number | null  // 进位方式覆盖
  truncParamOverride?: number | null  // 舍位参数覆盖
  ceilParamOverride?: number | null   // 进位参数覆盖
}

/** 省份行 */
export interface CostProvinceRow {
  provinceId: number
  provinceName: string
  shortName: string
  region: string
  regionIndex: number
  cells: Record<number, CostCell>  // key=segmentIndex
}

/** 成本项目矩阵配置 */
export interface CostItemMatrix {
  costItemId: number
  calcMethod: number              // 该项目统一的计费方式(1/2/3)
  pricingScope: 'national' | 'province' | 'city'  // 定价模式
  segments: CostWeightSegment[]   // 重量段定义（CalcMethod=1时为空数组）
  rows: CostProvinceRow[]         // 省份行数据
  nationalCell?: CostCell         // 全国统一模式时的单一价格
}

/** 批量填充选项 */
export interface BatchFillOptions {
  mode: 'column' | 'region' | 'all' | 'copyRow'
  segmentIndex?: number      // mode='column' 时指定段
  regionName?: string        // mode='region' 时指定大区
  sourceProvinceId?: number  // mode='copyRow' 时指定源
  targetProvinceIds?: number[] // mode='copyRow' 时指定目标
  cell: CostCell             // 要填充的价格值
  overwrite: boolean         // 是否覆盖已有值
}

// ==================== 常量 ====================

/** 大区排序映射（与 usePriceMatrix 保持一致） */
const REGION_ORDER: Record<string, number> = {
  '一区': 1, '二区': 2, '三区': 3, '四区': 4, '五区': 5,
  '六区': 6, '七区': 7, '八区': 8, '偏远': 99,
}

function getRegionIndex(region: string): number {
  return REGION_ORDER[region] ?? 50
}

/** 默认首重(kg) */
const DEFAULT_FIRST_WEIGHT = 0
/** 默认续重步进(kg) */
const DEFAULT_CONTINUE_STEP = 1
/** 默认进位方式 */
const DEFAULT_ROUNDING_METHOD = 1

// ==================== 工具函数 ====================

/** 将省份列表按大区分组排序 */
function buildProvinceRows(provinces: ProvinceDto[]): CostProvinceRow[] {
  return provinces
    .map(p => ({
      provinceId: p.id,
      provinceName: p.shortName || p.name,
      shortName: p.shortName || p.name,
      region: p.region,
      regionIndex: getRegionIndex(p.region),
      cells: {} as Record<number, CostCell>,
    }))
    .sort((a, b) => {
      if (a.regionIndex !== b.regionIndex) return a.regionIndex - b.regionIndex
      return a.provinceId - b.provinceId
    })
}

/** 判断单元格是否有值 */
function hasCellValue(cell: CostCell | undefined, calcMethod: number): boolean {
  void calcMethod
  return hasCostCellValue(cell)
}

/** 创建空矩阵 */
function createEmptyMatrix(costItemId: number, provinces: ProvinceDto[]): CostItemMatrix {
  return {
    costItemId,
    calcMethod: 1,
    pricingScope: 'national',
    segments: [],
    rows: buildProvinceRows(provinces),
    nationalCell: { basePrice: 0, continuePrice: 0, firstWeight: DEFAULT_FIRST_WEIGHT, continueStep: DEFAULT_CONTINUE_STEP },
  }
}

// ==================== 主 Composable ====================

export function useCostMatrix(provinces: Ref<ProvinceDto[]>) {
  /** 所有成本项目的矩阵数据（key=costItemId） */
  const matrices = ref<Record<number, CostItemMatrix>>({})

  /** 脏数据标记 */
  const isDirty = ref(false)

  /** 内部脏数据控制（避免初始化触发） */
  let suppressDirty = false
  function markDirty() {
    if (!suppressDirty) isDirty.value = true
  }

  /** 监听 provinces 变化时同步到所有矩阵的 rows */
  watch(provinces, (newProvinces) => {
    suppressDirty = true
    try {
      for (const id in matrices.value) {
        const m = matrices.value[id]
        const oldRowMap = new Map(m.rows.map(r => [r.provinceId, r]))
        m.rows = buildProvinceRows(newProvinces).map(r => {
          const old = oldRowMap.get(r.provinceId)
          if (old) r.cells = old.cells
          return r
        })
      }
    } finally {
      suppressDirty = false
    }
  }, { deep: false })

  // ==================== 矩阵获取/创建 ====================

  function getMatrix(costItemId: number): CostItemMatrix {
    if (!matrices.value[costItemId]) {
      matrices.value[costItemId] = createEmptyMatrix(costItemId, provinces.value)
    }
    return matrices.value[costItemId]
  }

  // ==================== 反序列化：从矩阵DTO构建内部模型 ====================
  
  function initFromMatrix(costItemId: number, entry: CostItemEntryDto): void {
    suppressDirty = true
    try {
      const matrix = createEmptyMatrix(costItemId, provinces.value)
  
      if (!entry || !entry.segments || entry.segments.length === 0) {
        matrices.value[costItemId] = matrix
        return
      }
  
      // 1. 定价模式直接取自DTO
      matrix.pricingScope = entry.pricingScope
  
      // 2. 从第一个段取计费方式（所有段统一）
      const firstSeg = entry.segments[0]
      const calcMethod = firstSeg.calcMethod
      matrix.calcMethod = calcMethod
  
      // 3. 映射重量段
      if (calcMethod === 1) {
        matrix.segments = []
      } else {
        matrix.segments = entry.segments.map((s, i) => ({
          index: s.segmentIndex ?? i,
          weightFrom: s.weightFrom ?? 0,
          weightTo: s.weightTo ?? null,
          roundingMethod: s.roundingMethod ?? DEFAULT_ROUNDING_METHOD,
          truncParam: s.truncParam ?? null,
          ceilParam: s.ceilParam ?? null,
        }))
      }
  
      // 4. 构建矩阵数据
      if (entry.pricingScope === 'national') {
        if (calcMethod === 1) {
          // 全国单一价格：取第一个段的第一个cell
          const seg = entry.segments[0]
          const cell = seg?.cells?.[0]
          if (cell) {
            matrix.nationalCell = {
              basePrice: cell.basePrice ?? 0,
              continuePrice: cell.continuePrice ?? 0,
              firstWeight: cell.firstWeight ?? DEFAULT_FIRST_WEIGHT,
              continueStep: cell.continueStep ?? DEFAULT_CONTINUE_STEP,
              isConfigured: true,
            }
          }
        } else {
          // 全国+段模式：创建虚拟全国行
          const virtualRow: CostProvinceRow = {
            provinceId: 0,
            provinceName: '全国',
            shortName: '全国',
            region: '全国',
            regionIndex: 0,
            cells: {},
          }
          for (let segI = 0; segI < entry.segments.length; segI++) {
            const seg = entry.segments[segI]
            const segIdx = seg.segmentIndex ?? segI
            // 全国模式：cells中只有一条（provinceId为null或不存在）
            const cellData = seg.cells?.[0]
            if (cellData) {
              const cell: CostCell = {
                basePrice: 0,
                continuePrice: 0,
                firstWeight: DEFAULT_FIRST_WEIGHT,
                continueStep: DEFAULT_CONTINUE_STEP,
                isConfigured: true,
              }
              if (cellData.basePrice != null) cell.basePrice = cellData.basePrice
              if (calcMethod === 3 && cellData.continuePrice != null) cell.continuePrice = cellData.continuePrice
              if (cellData.firstWeight != null) cell.firstWeight = cellData.firstWeight
              if (cellData.continueStep != null) cell.continueStep = cellData.continueStep
              if (cellData.roundingMethodOverride != null) cell.roundingMethodOverride = cellData.roundingMethodOverride
              if (cellData.truncParamOverride != null) cell.truncParamOverride = cellData.truncParamOverride
              if (cellData.ceilParamOverride != null) cell.ceilParamOverride = cellData.ceilParamOverride
              virtualRow.cells[segIdx] = cell
            }
          }
          matrix.rows = [virtualRow]
        }
      } else {
        // province 模式
        const rowMap = new Map(matrix.rows.map(r => [r.provinceId, r]))
  
        if (calcMethod === 1) {
          const seg = entry.segments[0]
          for (const cellData of (seg?.cells ?? [])) {
            if (cellData.provinceId == null) continue
            const row = rowMap.get(cellData.provinceId)
            if (!row) continue
            row.cells[0] = {
              basePrice: cellData.basePrice ?? 0,
              continuePrice: cellData.continuePrice ?? 0,
              firstWeight: cellData.firstWeight ?? DEFAULT_FIRST_WEIGHT,
              continueStep: cellData.continueStep ?? DEFAULT_CONTINUE_STEP,
              isConfigured: true,
            }
          }
        } else {
          for (let segI = 0; segI < entry.segments.length; segI++) {
            const seg = entry.segments[segI]
            const segIdx = seg.segmentIndex ?? segI
            for (const cellData of (seg?.cells ?? [])) {
              if (cellData.provinceId == null) continue
              const row = rowMap.get(cellData.provinceId)
              if (!row) continue
              const cell: CostCell = {
                basePrice: 0,
                continuePrice: 0,
                firstWeight: DEFAULT_FIRST_WEIGHT,
                continueStep: DEFAULT_CONTINUE_STEP,
                isConfigured: true,
              }
              if (cellData.basePrice != null) cell.basePrice = cellData.basePrice
              if (calcMethod === 3 && cellData.continuePrice != null) cell.continuePrice = cellData.continuePrice
              if (cellData.firstWeight != null) cell.firstWeight = cellData.firstWeight
              if (cellData.continueStep != null) cell.continueStep = cellData.continueStep
              if (cellData.roundingMethodOverride != null) cell.roundingMethodOverride = cellData.roundingMethodOverride
              if (cellData.truncParamOverride != null) cell.truncParamOverride = cellData.truncParamOverride
              if (cellData.ceilParamOverride != null) cell.ceilParamOverride = cellData.ceilParamOverride
              row.cells[segIdx] = cell
            }
          }
        }
      }
  
      matrices.value[costItemId] = matrix
    } finally {
      suppressDirty = false
    }
  }
  
  function initAllFromMatrix(matrixDto: CostPlanMatrixDto, costItems: CostItemDto[]): void {
    suppressDirty = true
    try {
      // 按 costItemId 索引
      const entryMap = new Map<number, CostItemEntryDto>()
      for (const entry of (matrixDto.costItems ?? [])) {
        entryMap.set(entry.costItemId, entry)
      }
  
      // 清空现有
      matrices.value = {}
  
      // 为每个成本项目初始化矩阵（包括无数据的项目，确保 sidebar Badge 正确）
      for (const item of costItems) {
        const entry = entryMap.get(item.id)
        if (entry) {
          initFromMatrix(item.id, entry)
        } else {
          matrices.value[item.id] = createEmptyMatrix(item.id, provinces.value)
        }
      }
  
      // 处理 costItems 中没有但 matrixDto 中有的项目（兜底）
      for (const [costItemId, entry] of entryMap) {
        if (!matrices.value[costItemId]) {
          initFromMatrix(costItemId, entry)
        }
      }
  
      isDirty.value = false
    } finally {
      suppressDirty = false
    }
  }

  // ==================== 序列化：内部模型 -> 矩阵DTO ====================

  function toMatrix(): CostPlanMatrixDto {
    const costItems: CostItemInput[] = []
    for (const id in matrices.value) {
      const matrix = matrices.value[id]
      const { costItemId, calcMethod, pricingScope, segments, rows, nationalCell } = matrix

      const itemInput: CostItemInput = {
        costItemId,
        pricingScope,
        segments: [],
      }

      if (pricingScope === 'national') {
        if (calcMethod === 1) {
          // 全国单一价格
          if (nationalCell && nationalCell.basePrice > 0) {
            itemInput.segments = [{
              segmentIndex: 0,
              calcMethod: 1,
              roundingMethod: DEFAULT_ROUNDING_METHOD,
              cells: [{
                provinceId: null,
                basePrice: nationalCell.basePrice,
                continuePrice: nationalCell.continuePrice ?? 0,
                firstWeight: nationalCell.firstWeight ?? DEFAULT_FIRST_WEIGHT,
                continueStep: nationalCell.continueStep ?? DEFAULT_CONTINUE_STEP,
              }],
            }]
          }
        } else {
          // 全国+段模式
          const virtualRow = rows[0]
          if (virtualRow) {
            for (const seg of segments) {
              const cell = virtualRow.cells[seg.index]
              if (!hasCellValue(cell, calcMethod)) continue
              const cellInput: CostCellInput = {
                provinceId: null,
                basePrice: cell!.basePrice,
                continuePrice: cell!.continuePrice,
                firstWeight: cell!.firstWeight,
                continueStep: cell!.continueStep,
                roundingMethodOverride: cell!.roundingMethodOverride ?? undefined,
                truncParamOverride: cell!.truncParamOverride ?? undefined,
                ceilParamOverride: cell!.ceilParamOverride ?? undefined,
              }
              const segInput: CostSegmentInput = {
                segmentIndex: seg.index,
                weightFrom: seg.weightFrom,
                weightTo: seg.weightTo,
                calcMethod,
                roundingMethod: seg.roundingMethod,
                truncParam: seg.truncParam ?? undefined,
                ceilParam: seg.ceilParam ?? undefined,
                cells: [cellInput],
              }
              itemInput.segments.push(segInput)
            }
          }
        }
      } else {
        // province 模式
        if (calcMethod === 1) {
          const cells: CostCellInput[] = []
          for (const row of rows) {
            const cell = row.cells[0]
            if (!hasCellValue(cell, 1)) continue
            cells.push({
              provinceId: row.provinceId,
              basePrice: cell!.basePrice,
              continuePrice: cell!.continuePrice,
              firstWeight: cell!.firstWeight,
              continueStep: cell!.continueStep,
              roundingMethodOverride: cell!.roundingMethodOverride ?? undefined,
              truncParamOverride: cell!.truncParamOverride ?? undefined,
              ceilParamOverride: cell!.ceilParamOverride ?? undefined,
            })
          }
          if (cells.length > 0) {
            itemInput.segments = [{
              segmentIndex: 0,
              calcMethod: 1,
              roundingMethod: DEFAULT_ROUNDING_METHOD,
              cells,
            }]
          }
        } else {
          for (const seg of segments) {
            const cells: CostCellInput[] = []
            for (const row of rows) {
              const cell = row.cells[seg.index]
              if (!hasCellValue(cell, calcMethod)) continue
              cells.push({
                provinceId: row.provinceId,
                basePrice: cell!.basePrice,
                continuePrice: cell!.continuePrice,
                firstWeight: cell!.firstWeight,
                continueStep: cell!.continueStep,
                roundingMethodOverride: cell!.roundingMethodOverride ?? undefined,
                truncParamOverride: cell!.truncParamOverride ?? undefined,
                ceilParamOverride: cell!.ceilParamOverride ?? undefined,
              })
            }
            const segInput: CostSegmentInput = {
              segmentIndex: seg.index,
              weightFrom: seg.weightFrom,
              weightTo: seg.weightTo,
              calcMethod,
              roundingMethod: seg.roundingMethod,
              truncParam: seg.truncParam ?? undefined,
              ceilParam: seg.ceilParam ?? undefined,
              cells,
            }
            itemInput.segments.push(segInput)
          }
        }
      }

      // 仅包含有数据的成本项目
      if (itemInput.segments.length > 0 && itemInput.segments.some(s => s.cells.length > 0)) {
        costItems.push(itemInput)
      }
    }
    return { costItems }
  }

  // ==================== 段管理 ====================

  function addSegment(costItemId: number, segment: Partial<CostWeightSegment>): void {
    const matrix = getMatrix(costItemId)
    const lastSeg = matrix.segments[matrix.segments.length - 1]
    const defaultFrom = lastSeg ? (lastSeg.weightTo ?? lastSeg.weightFrom + 1) : 0
    const newSeg: CostWeightSegment = {
      index: matrix.segments.length,
      weightFrom: segment.weightFrom ?? defaultFrom,
      weightTo: segment.weightTo === undefined ? defaultFrom + 1 : segment.weightTo,
      roundingMethod: segment.roundingMethod ?? DEFAULT_ROUNDING_METHOD,
      truncParam: segment.truncParam ?? null,
      ceilParam: segment.ceilParam ?? null,
    }
    // 自动切换 calcMethod 到 2（按重量）如果当前是 1（按单）
    if (matrix.calcMethod === 1) {
      matrix.calcMethod = 2
    }
    matrix.segments.push(newSeg)
    markDirty()
  }

  function removeSegment(costItemId: number, segIndex: number): void {
    const matrix = getMatrix(costItemId)
    if (segIndex < 0 || segIndex >= matrix.segments.length) return
    matrix.segments.splice(segIndex, 1)

    // 清理所有行中该索引的单元格，并重建索引
    const reindexCells = (cells: Record<number, CostCell>): Record<number, CostCell> => {
      const newCells: Record<number, CostCell> = {}
      for (const [k, v] of Object.entries(cells)) {
        const idx = Number(k)
        if (idx === segIndex) continue
        const newIdx = idx > segIndex ? idx - 1 : idx
        newCells[newIdx] = v
      }
      return newCells
    }

    matrix.segments.forEach((s, i) => { s.index = i })
    for (const row of matrix.rows) {
      row.cells = reindexCells(row.cells)
    }
    markDirty()
  }

  function updateSegment(costItemId: number, segIndex: number, updates: Partial<CostWeightSegment>): void {
    const matrix = getMatrix(costItemId)
    const seg = matrix.segments[segIndex]
    if (!seg) return
    if (updates.weightFrom !== undefined) seg.weightFrom = updates.weightFrom
    if (updates.weightTo !== undefined) seg.weightTo = updates.weightTo
    if (updates.roundingMethod !== undefined) seg.roundingMethod = updates.roundingMethod
    if (updates.truncParam !== undefined) seg.truncParam = updates.truncParam
    if (updates.ceilParam !== undefined) seg.ceilParam = updates.ceilParam
    markDirty()
  }

  // ==================== 单元格管理 ====================

  function setCell(costItemId: number, provinceId: number | null, segIndex: number, cell: CostCell): void {
    const matrix = getMatrix(costItemId)
    if (matrix.pricingScope === 'national') {
      if (matrix.calcMethod === 1) {
        matrix.nationalCell = { ...cell, isConfigured: true }
      } else {
        // 虚拟行
        if (!matrix.rows[0]) {
          matrix.rows = [{
            provinceId: 0,
            provinceName: '全国',
            shortName: '全国',
            region: '全国',
            regionIndex: 0,
            cells: {},
          }]
        }
        matrix.rows[0].cells[segIndex] = { ...cell, isConfigured: true }
      }
    } else {
      if (provinceId == null) return
      const row = matrix.rows.find(r => r.provinceId === provinceId)
      if (!row) return
      const idx = matrix.calcMethod === 1 ? 0 : segIndex
      row.cells[idx] = { ...cell, isConfigured: true }
    }
    markDirty()
  }

  // ==================== 批量填充 ====================

  function batchFill(costItemId: number, options: BatchFillOptions): void {
    const matrix = getMatrix(costItemId)
    const { mode, cell, overwrite } = options
    const calcMethod = matrix.calcMethod

    const applyCell = (target: CostCell | undefined, idx: number, row: CostProvinceRow) => {
      const existing = row.cells[idx]
      if (!overwrite && hasCellValue(existing, calcMethod)) return
      row.cells[idx] = { ...cell, isConfigured: true }
    }

    if (matrix.pricingScope === 'national') {
      // 全国模式下批量填充：仅作用于段维度
      if (calcMethod === 1) {
        if (overwrite || !hasCellValue(matrix.nationalCell, 1)) {
          matrix.nationalCell = { ...cell, isConfigured: true }
        }
      } else {
        if (!matrix.rows[0]) return
        const row = matrix.rows[0]
        if (mode === 'column' && options.segmentIndex != null) {
          applyCell(row.cells[options.segmentIndex], options.segmentIndex, row)
        } else if (mode === 'all') {
          for (const seg of matrix.segments) {
            applyCell(row.cells[seg.index], seg.index, row)
          }
        }
      }
      markDirty()
      return
    }

    // province 模式
    const targetSegIndices: number[] = calcMethod === 1
      ? [0]
      : matrix.segments.map(s => s.index)

    if (mode === 'column') {
      if (options.segmentIndex == null) return
      const idx = calcMethod === 1 ? 0 : options.segmentIndex
      for (const row of matrix.rows) {
        applyCell(row.cells[idx], idx, row)
      }
    } else if (mode === 'region') {
      if (!options.regionName) return
      for (const row of matrix.rows) {
        if (row.region !== options.regionName) continue
        for (const idx of targetSegIndices) {
          applyCell(row.cells[idx], idx, row)
        }
      }
    } else if (mode === 'all') {
      for (const row of matrix.rows) {
        for (const idx of targetSegIndices) {
          applyCell(row.cells[idx], idx, row)
        }
      }
    } else if (mode === 'copyRow') {
      if (options.sourceProvinceId == null || !options.targetProvinceIds || options.targetProvinceIds.length === 0) return
      const srcRow = matrix.rows.find(r => r.provinceId === options.sourceProvinceId)
      if (!srcRow) return
      const targetSet = new Set(options.targetProvinceIds)
      for (const row of matrix.rows) {
        if (!targetSet.has(row.provinceId)) continue
        for (const idx of targetSegIndices) {
          const srcCell = srcRow.cells[idx]
          if (!hasCellValue(srcCell, calcMethod)) continue
          if (!overwrite && hasCellValue(row.cells[idx], calcMethod)) continue
          row.cells[idx] = { ...srcCell, isConfigured: true }
        }
      }
    }
    markDirty()
  }

  // ==================== 定价模式切换 ====================

  function switchPricingScope(costItemId: number, newScope: 'national' | 'province'): void {
    const matrix = getMatrix(costItemId)
    if (matrix.pricingScope === newScope) return
    matrix.pricingScope = newScope
    // 切换时清空价格数据，避免语义混乱
    matrix.nationalCell = { basePrice: 0, continuePrice: 0, firstWeight: DEFAULT_FIRST_WEIGHT, continueStep: DEFAULT_CONTINUE_STEP }
    if (newScope === 'national') {
      if (matrix.calcMethod === 1) {
        matrix.rows = buildProvinceRows(provinces.value)  // 保留以便切回
      } else {
        matrix.rows = [{
          provinceId: 0,
          provinceName: '全国',
          shortName: '全国',
          region: '全国',
          regionIndex: 0,
          cells: {},
        }]
      }
    } else {
      matrix.rows = buildProvinceRows(provinces.value)
    }
    markDirty()
  }

  // ==================== 校验 ====================

  function validate(costItemId: number): { ok: boolean; errors: string[] } {
    const errors: string[] = []
    const matrix = matrices.value[costItemId]
    if (!matrix) {
      return { ok: false, errors: ['矩阵不存在'] }
    }
    const { calcMethod, pricingScope, segments, rows, nationalCell } = matrix

    // 1. 至少有一个有价格数据的单元格
    let hasAnyValue = false
    if (pricingScope === 'national' && calcMethod === 1) {
      hasAnyValue = hasCellValue(nationalCell, 1)
    } else if (pricingScope === 'national') {
      const row = rows[0]
      if (row) {
        for (const seg of segments) {
          if (hasCellValue(row.cells[seg.index], calcMethod)) {
            hasAnyValue = true
            break
          }
        }
      }
    } else {
      const segIndices = calcMethod === 1 ? [0] : segments.map(s => s.index)
      outer:
      for (const row of rows) {
        for (const idx of segIndices) {
          if (hasCellValue(row.cells[idx], calcMethod)) {
            hasAnyValue = true
            break outer
          }
        }
      }
    }
    if (!hasAnyValue) {
      errors.push('至少需要填写一个价格')
    }

    // 2. CalcMethod=2/3/4 需要段
    if ((calcMethod === 2 || calcMethod === 3 || calcMethod === 4) && segments.length === 0) {
      errors.push('按重量/首续重/扣除重量计费方式需至少配置一个重量段')
    }

    // 3. 段连续性检查
    if (segments.length > 1) {
      for (let i = 1; i < segments.length; i++) {
        const prev = segments[i - 1]
        const cur = segments[i]
        if (prev.weightTo == null) {
          errors.push(`重量段${i} 不能位于开放段之后`)
          break
        }
        if (cur.weightFrom !== prev.weightTo) {
          errors.push(`重量段不连续：第${i}段起始(${cur.weightFrom}) 与第${i - 1}段截止(${prev.weightTo}) 不一致`)
        }
      }
    }

    // 4. 必填价格字段完整性（计费方式=3 时，已填首重价不能缺续重价，反之亦然）
    if (calcMethod === 3) {
      const checkCell = (cell: CostCell | undefined, label: string) => {
        if (!cell) return
        const hasFirst = hasCellValue(cell, 3)
        const hasCont = cell.continuePrice > 0
        if (hasFirst !== hasCont && (hasFirst || hasCont)) {
          // 仅警告：允许只填首重（续重默认0），但若续重已填则首重必须填
          if (hasCont && !hasFirst) {
            errors.push(`${label} 续重价已填但首重价未填`)
          }
        }
      }
      if (pricingScope === 'national') {
        const row = rows[0]
        if (row) {
          for (const seg of segments) {
            checkCell(row.cells[seg.index], `全国 ${seg.weightFrom}-${seg.weightTo ?? '∞'}kg`)
          }
        }
      } else {
        for (const row of rows) {
          for (const seg of segments) {
            checkCell(row.cells[seg.index], `${row.provinceName} ${seg.weightFrom}-${seg.weightTo ?? '∞'}kg`)
          }
        }
      }
    }

    return { ok: errors.length === 0, errors }
  }

  // ==================== 明细数量统计 ====================

  function getDetailCount(costItemId: number): number {
    const matrix = matrices.value[costItemId]
    if (!matrix) return 0
    const { calcMethod, pricingScope, segments, rows, nationalCell } = matrix
    let count = 0
    if (pricingScope === 'national') {
      if (calcMethod === 1) {
        if (hasCellValue(nationalCell, 1)) count = 1
      } else {
        const row = rows[0]
        if (row) {
          for (const seg of segments) {
            if (hasCellValue(row.cells[seg.index], calcMethod)) count++
          }
        }
      }
    } else {
      if (calcMethod === 1) {
        for (const row of rows) {
          if (hasCellValue(row.cells[0], 1)) count++
        }
      } else {
        for (const row of rows) {
          for (const seg of segments) {
            if (hasCellValue(row.cells[seg.index], calcMethod)) count++
          }
        }
      }
    }
    return count
  }

  // ==================== 导出 ====================

  return {
    matrices,
    isDirty,
    getMatrix,
    initFromMatrix,
    initAllFromMatrix,
    toMatrix,
    addSegment,
    removeSegment,
    updateSegment,
    setCell,
    batchFill,
    switchPricingScope,
    validate,
    getDetailCount,
  }
}

export type UseCostMatrixReturn = ReturnType<typeof useCostMatrix>
