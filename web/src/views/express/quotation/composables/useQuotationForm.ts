import { ref, reactive, computed, createVNode } from 'vue'
import { useRoute } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import dayjs from 'dayjs'
import {
  getQuotationDetail,
  createQuotation,
  updateQuotation,
  getProvinceList,
  getExpAgentList,
  getExpNetworkPointOptions,
  getExpFranchiseAreaOptions,
  getExpLastMileStationOptions,
  type ProvinceDto,
} from '@/api/express'
import { getCustomerList } from '@/api/crm'
import type {
  WeightSegment,
  PriceCell,
  ProvinceRow,
  QuotationFormData,
} from './usePriceMatrix'
import {
  toCreatePayload,
  toUpdatePayload,
  fromApiResponse,
  groupProvincesByRegion,
  initMatrixCells,
  addSegmentColumn,
  removeSegmentColumn,
  splitSegmentColumn,
  mergeSegmentColumn,
  getRegionList,
  countFilledCells,
  validateMatrixCompleteness,
} from './usePriceMatrix'

// ==================== 选项常量 ====================

export const weightStageOptions = [
  { value: 1, label: '揽收重量' },
  { value: 2, label: '中转重量' },
  { value: 3, label: '到件重量' },
  { value: 4, label: '集包重量' },
  { value: 5, label: '计泡重量' },
  { value: 6, label: '总部重量' },
  { value: 7, label: '取所有环节最大值' },
]

export const roundingMethodOptions = [
  { value: 1, label: '实际重量' },
  { value: 2, label: '四舍五入' },
  { value: 3, label: '点五进位' },
  { value: 4, label: '进位舍位' },
  { value: 5, label: '向上取整' },
  { value: 6, label: '分段进位' },
  { value: 7, label: '进舍百位' },
]

// ==================== Composable ====================

export function useQuotationForm(planId?: number) {
  const route = useRoute()

  // ---- 预填充锁定状态 ----
  const isClientLocked = ref(false)

  // ---- 基本信息响应式状态 ----
  const formData = reactive<QuotationFormData>({
    clientType: undefined,
    clientId: undefined,
    planName: '',
    networkPointCode: '',
    sharedShopEnabled: false,
    settlementWeightStage: 1,
    weightRoundingMethod: 5,
    // 商务条款
    paymentMode: undefined,
    prepayRatio: undefined,
    billingCycle: undefined,
    billingDay: undefined,
    paymentDueDay: undefined,
    throwRatio: 8000,
    insuranceRate: undefined,
    remark: undefined,
    oaApprovalInfo: undefined,
    effectiveDate: dayjs().format('YYYY-MM-DD'),
  })

  // ---- 重量段列表 ----
  const segments = ref<WeightSegment[]>([])

  // ---- 矩阵明细 — ProvinceRow[] ----
  const matrix = ref<ProvinceRow[]>([])

  // ---- 省份原始列表 ----
  const provinces = ref<ProvinceDto[]>([])

  // ---- 客户 & 品牌下拉 ----
  const clientOptions = ref<{ id: number | string; name: string; code?: string }[]>([])
  const clientSearchLoading = ref(false)

  // ---- 业务对象名称（从 clientOptions 中查找；id 可能是字符串编号或数字ID，宽松比较） ----
  const clientName = computed(() => {
    if (!formData.clientId) return ''
    const found = clientOptions.value.find(c => String(c.id) === String(formData.clientId))
    return found?.name || ''
  })

  // ---- 加载状态 ----
  const loading = ref(false)
  const saving = ref(false)

  // ---- 计算属性 ----
  const isCreate = computed(() => !planId || planId === 0)
  const regionList = computed(() => getRegionList(matrix.value))
  const filledCellCount = computed(() => countFilledCells(matrix.value))

  // ==================== 客户搜索 ====================

  let clientSearchTimer: ReturnType<typeof setTimeout> | null = null

  function handleClientSearch(keyword: string) {
    const clientType = formData.clientType
    if (clientSearchTimer) clearTimeout(clientSearchTimer)
    clientSearchLoading.value = true
    const delay = keyword ? 300 : 0
    clientSearchTimer = setTimeout(async () => {
      try {
        const params = { keyword: keyword || undefined, pageIndex: 1, pageSize: 50 }
        let items: any[] = []
        if (clientType === 'DL') {
          const res = await getExpAgentList(params)
          const list = Array.isArray(res) ? res : (res.items || [])
          items = list.map((a: any) => ({ id: a.id, name: a.name, code: a.code }))
        } else if (clientType === 'WD') {
          const res = await getExpNetworkPointOptions(params)
          const list = Array.isArray(res) ? res : (res.items || [])
          items = list.map((n: any) => ({ id: n.id, name: n.manager || `网点${n.id}`, code: String(n.id) }))
        } else if (clientType === 'YW') {
          // ExpSalesmanList API 已废弃，待替换
          items = []
        } else if (clientType === 'CB') {
          const res = await getExpFranchiseAreaOptions(params)
          const list = Array.isArray(res) ? res : (res.items || [])
          items = list.map((f: any) => ({ id: f.id, name: f.contractor || `承包区${f.id}`, code: String(f.id) }))
        } else if (clientType === 'YZ') {
          const res = await getExpLastMileStationOptions(params)
          const list = Array.isArray(res) ? res : (res.items || [])
          items = list.map((s: any) => ({ id: s.id, name: s.name || `驿站${s.id}`, code: s.code }))
        } else {
          const res = await getCustomerList({ keyword: keyword || undefined, pageIndex: 1, pageSize: 50 })
          const list = Array.isArray(res) ? res : ((res as any)?.items || [])
          items = list.map((item: any) => ({ id: item.id, name: item.shortName, code: item.code }))
        }
        clientOptions.value = items
      } catch {
        clientOptions.value = []
      } finally {
        clientSearchLoading.value = false
      }
    }, delay)
  }

  /** clientType 变更时清空已选值并重新加载 */
  function handleClientTypeChange(newType: string) {
    formData.clientType = newType
    formData.clientId = undefined
    clientOptions.value = []
    handleClientSearch('')
  }

  // ==================== 品牌加载 (deprecated) ====================

  async function loadBrandOptions() {
    // Brand options removed - no longer needed for quotation
  }

  // ==================== 省份加载 ====================

  async function loadProvinces() {
    try {
      provinces.value = await getProvinceList()
      matrix.value = groupProvincesByRegion(provinces.value)
      // 如果已有 segments，初始化空单元格
      if (segments.value.length > 0) {
        initMatrixCells(matrix.value, segments.value)
      }
    } catch {
      message.error('获取省份列表失败')
    }
  }

  // ==================== 加载方案详情 ====================

  async function loadPlan(id: number) {
    loading.value = true
    try {
      const dto = await getQuotationDetail(id)
      const result = fromApiResponse(dto)

      // 回填基本信息
      Object.assign(formData, result.formData)

      // 回填后按 clientType 重新加载业务对象选项，以便找到名称
      handleClientSearch('')

      // 回填重量段
      segments.value = result.segments

      // 构建矩阵并填充价格数据
      matrix.value = groupProvincesByRegion(provinces.value)
      initMatrixCells(matrix.value, segments.value, result.cellMap)
    } catch {
      message.error('获取方案详情失败')
    } finally {
      loading.value = false
    }
  }

  // ==================== 保存方案 ====================

  async function savePlan(): Promise<boolean> {
    // 基本校验
    if (!formData.planName) { message.warning('请输入方案名称'); return false }
    if (isCreate.value && !formData.clientId) { message.warning('请选择业务对象'); return false }
    if (segments.value.length === 0) { message.warning('请至少添加一个重量段'); return false }

    // 矩阵完整性校验
    let allowIncomplete = false
    const gaps = validateMatrixCompleteness(segments.value, matrix.value)
    if (gaps.length > 0) {
      const lines = gaps.map(g => `【${g.segmentLabel}】缺少：${g.missingProvinces.join('、')}`)
      const html = `<div style="max-height:300px;overflow-y:auto;">`
        + `<p>以下重量段存在未填写价格的省份：</p>`
        + lines.map(l => `<p style="margin:4px 0;">${l}</p>`).join('')
        + `</div>`
      const confirmed = await confirmIncompleteMatrix('报价矩阵未完整填写', html)
      if (!confirmed) return false
      allowIncomplete = true
    }

    saving.value = true
    try {
      if (isCreate.value) {
        const payload = toCreatePayload(formData, segments.value, matrix.value)
        if (allowIncomplete) payload.allowIncomplete = true
        await createQuotation(payload)
      } else {
        const payload = toUpdatePayload(formData, segments.value, matrix.value)
        if (allowIncomplete) payload.allowIncomplete = true
        await updateQuotation(planId!, payload)
      }
      message.success('保存成功')
      return true
    } catch (e: any) {
      message.error(e.message || '保存失败')
      return false
    } finally {
      saving.value = false
    }
  }

  /** 矩阵不完整时的确认弹窗 */
  function confirmIncompleteMatrix(title: string, content: string): Promise<boolean> {
    return new Promise((resolve) => {
      Modal.confirm({
        title,
        content: createVNode('div', { innerHTML: content }),
        okText: '继续保存',
        cancelText: '返回修改',
        onOk: () => resolve(true),
        onCancel: () => resolve(false),
      })
    })
  }

  // ==================== 重量段操作 ====================

  /** 分割重量段：在指定段的 splitPoint 处一分为二 */
  function splitSegment(segmentIndex: number, splitPoint: number): boolean {
    const seg = segments.value[segmentIndex]
    if (!seg) {
      message.warning('未找到指定重量段')
      return false
    }

    // 校验分割点
    if (splitPoint <= seg.startWeight) {
      message.warning('分割点必须大于起始重量')
      return false
    }
    if (seg.endWeight != null && splitPoint >= seg.endWeight) {
      message.warning('分割点必须小于截止重量')
      return false
    }

    const ok = splitSegmentColumn(segments.value, matrix.value, segmentIndex, splitPoint)
    if (!ok) {
      message.error('分割失败')
    }
    return ok
  }

  /** 合并重量段：删除指定段并与相邻段合并 */
  function mergeSegment(index: number): boolean {
    if (segments.value.length <= 1) {
      message.warning('至少保留一个重量段')
      return false
    }
    return mergeSegmentColumn(segments.value, matrix.value, index)
  }

  function updateSegment(index: number, data: Partial<WeightSegment>) {
    const seg = segments.value[index]
    if (!seg) return

    Object.assign(seg, data)
  }

  // ==================== 单元格操作 ====================

  function getCellValue(segmentIndex: number, provinceId: number, field: keyof PriceCell): number | null {
    const row = matrix.value.find(r => r.provinceId === provinceId)
    if (!row) return null
    const cell = row.prices[segmentIndex]
    if (!cell) return null
    return cell[field] as number | null
  }

  function setCellValue(segmentIndex: number, provinceId: number, field: keyof PriceCell, value: number | null) {
    const row = matrix.value.find(r => r.provinceId === provinceId)
    if (!row) return
    if (!row.prices[segmentIndex]) {
      row.prices[segmentIndex] = {
        segmentIndex,
        provinceId,
        basePrice: null,
        continuePrice: null,
        firstWeight: 0,
        continueStep: 1,
        roundingMethodOverride: null,
        truncParamOverride: null,
        ceilParamOverride: null,
      }
    }
    ;(row.prices[segmentIndex] as any)[field] = value
  }

  /** 更新单元格的覆盖值 */
  function setCellOverrides(
    segmentIndex: number,
    provinceId: number,
    overrides: {
      roundingMethodOverride: number | null
      truncParamOverride: number | null
      ceilParamOverride: number | null
    },
  ) {
    const row = matrix.value.find(r => r.provinceId === provinceId)
    if (!row) return
    const cell = row.prices[segmentIndex]
    if (!cell) return
    cell.roundingMethodOverride = overrides.roundingMethodOverride
    cell.truncParamOverride = overrides.truncParamOverride
    cell.ceilParamOverride = overrides.ceilParamOverride
  }

  // ==================== 批量填充 ====================

  function batchFill(options: {
    region?: string
    segmentIndex: number
    value1: number
    value2?: number
  }) {
    const seg = segments.value.find(s => s.sortOrder === options.segmentIndex)
    if (!seg) return

    const targetRows = options.region
      ? matrix.value.filter(r => r.region === options.region)
      : matrix.value

    for (const row of targetRows) {
      if (!row.prices[seg.sortOrder]) {
        row.prices[seg.sortOrder] = {
          segmentIndex: seg.sortOrder,
          provinceId: row.provinceId,
          basePrice: null,
          continuePrice: null,
          firstWeight: 0,
          continueStep: 1,
          roundingMethodOverride: null,
          truncParamOverride: null,
          ceilParamOverride: null,
        }
      }
      const cell = row.prices[seg.sortOrder]
      // A3' 统一：basePrice 始终为首/单重价
      cell.basePrice = options.value1
      if (options.value2 != null) {
        cell.continuePrice = options.value2
      } else {
        cell.continuePrice = null
      }
    }
    message.success('批量填充完成')
  }

  // ==================== 批量调价 ====================

  function batchAdjustPrice(segmentIndex: number, delta: number, target: 'all' | 'first' | 'continue') {
    const seg = segments.value.find(s => s.sortOrder === segmentIndex)
    if (!seg) return

    for (const row of matrix.value) {
      const cell = row.prices[seg.sortOrder]
      if (!cell) continue

      // A3' 统一：basePrice 可调；continuePrice 有值时也调
      if (target === 'all' || target === 'first') {
        if (cell.basePrice != null) {
          cell.basePrice = Math.max(0, cell.basePrice + delta)
        }
      }
      if ((target === 'all' || target === 'continue') && cell.continuePrice != null) {
        cell.continuePrice = Math.max(0, cell.continuePrice + delta)
      }
    }
    message.success('批量调价完成')
  }

  // ==================== 初始化 ====================

  async function init() {
    await loadProvinces()
    await loadBrandOptions()
    // 预加载业务对象列表，确保下拉框打开时有数据
    handleClientSearch('')
    if (!isCreate.value && planId) {
      await loadPlan(planId)
    } else {
      // 新建时：检查 URL query 预填充业务对象
      const qClientType = route.query.clientType as string | undefined
      const qClientId = route.query.clientId as string | undefined
      const qClientName = route.query.clientName as string | undefined

      if (qClientType) {
        formData.clientType = qClientType
        if (qClientId) {
          // 数字ID（WD/CB）转 number 以匹配下拉 option；字母前缀编号（KH/DL/YZ）保留字符串
          formData.clientId = Number(qClientId) || qClientId
        }
        // 将预填充的名称注入到 clientOptions 中，确保下拉框能显示
        if (qClientId && qClientName) {
          const id = Number(qClientId) || qClientId
          clientOptions.value = [{ id, name: decodeURIComponent(qClientName), code: String(qClientId) }]
        }
        isClientLocked.value = true
        // 重新加载该类型的选项列表
        handleClientSearch('')
      }

      // 新建时默认一个段：0~999
      const defaultSeg: WeightSegment = {
        sortOrder: 1,
        startWeight: 0,
        endWeight: 999,
        roundingMethod: 1,
        truncParam: null,
        ceilParam: null,
      }
      addSegmentColumn(segments.value, matrix.value, defaultSeg)
    }
  }

  return {
    // 状态
    formData,
    segments,
    matrix,
    provinces,
    clientOptions,
    clientName,
    clientSearchLoading,
    isClientLocked,
    loading,
    saving,
    isCreate,
    regionList,
    filledCellCount,

    // 方法
    init,
    loadPlan,
    savePlan,
    loadProvinces,
    loadBrandOptions,
    handleClientSearch,
    handleClientTypeChange,

    // 重量段操作
    splitSegment,
    mergeSegment,
    updateSegment,

    // 单元格操作
    getCellValue,
    setCellValue,
    setCellOverrides,
    batchFill,
    batchAdjustPrice,
  }
}
