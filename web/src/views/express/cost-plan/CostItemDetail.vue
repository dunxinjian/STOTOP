<template>
  <div class="page-container cost-item-detail-page">
    <PageHeader :title="pageTitle" :back-to="backRoute">
      <template #actions>
        <a-button @click="handleCancel">取消</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
      </template>
      <template #toolbar>
        <CostItemToolbar
          v-model:itemName="itemForm.itemName"
          v-model:itemType="itemForm.itemType"
          v-model:sortOrder="itemForm.sortOrder"
          v-model:settlementWeightStage="itemForm.settlementWeightStage"
        />
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 新增时间段弹窗 -->
      <a-modal
        v-model:open="newPeriodModalVisible"
        title="新增时间段"
        :confirm-loading="creatingPeriod"
        @ok="handleCreatePeriod"
        ok-text="确定"
        cancel-text="取消"
      >
        <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
          <a-form-item label="生效日期" required>
            <a-date-picker
              v-model:value="newPeriodDate"
              style="width: 100%"
              placeholder="请选择生效日期"
              value-format="YYYY-MM-DD"
            />
          </a-form-item>
        </a-form>
      </a-modal>

      <div class="edit-scroll-body">
        <!-- 时间段列表 -->
        <div class="period-section">
          <div class="period-header">
            <a-button size="small" type="primary" @click="openNewPeriodModal">
              <template #icon><PlusOutlined /></template>新增成本配置
            </a-button>
          </div>

          <a-table
            :columns="periodColumns"
            :data-source="periods"
            row-key="id"
            size="small"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'effectiveDate'">
                <a-date-picker
                  :value="record.effectiveDate?.slice(0, 10)"
                  size="small"
                  value-format="YYYY-MM-DD"
                  placeholder="选择生效日期"
                  @change="(_val: string | Dayjs, dateString: string) => handleUpdatePeriodDate(record as CostPlanItemPeriodDto, dateString)"
                />
              </template>
              <!-- 全国单价类型：直接在列表里显示价格输入 -->
              <template v-if="column.dataIndex === 'price'">
                <template v-if="itemForm.itemType === 1">
                  <a-input-number
                    :value="getPeriodPrice(record as CostPlanItemPeriodDto)"
                    :min="0"
                    :precision="2"
                    size="small"
                    style="width: 120px"
                    @change="(val: ValueType) => setPeriodPrice(record as CostPlanItemPeriodDto, val == null ? 0 : Number(val))"
                  />
                  <span style="margin-left: 4px; color: #999">元</span>
                </template>
                <template v-else>
                  <span style="color: #999">—</span>
                </template>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <!-- 矩阵类型才需要"编辑矩阵"按钮 -->
                <a-button
                  v-if="itemForm.itemType !== 1"
                  type="link"
                  size="small"
                  @click="openMatrixEditor(record as CostPlanItemPeriodDto)"
                >
                  <EditOutlined /> 编辑矩阵
                </a-button>
                <a-popconfirm
                  v-if="periods.length > 1"
                  title="确定删除此时间段？"
                  @confirm="handleDeletePeriod(record.id)"
                >
                  <a-button type="link" size="small" danger>
                    <DeleteOutlined /> 删除
                  </a-button>
                </a-popconfirm>
              </template>
            </template>
          </a-table>
        </div>
      </div>
    </a-spin>

    <!-- 全屏矩阵编辑弹窗 -->
    <a-modal
      v-model:open="matrixEditorVisible"
      :footer="null"
      :width="'100%'"
      wrap-class-name="fullscreen-matrix-modal"
      :destroy-on-close="true"
    >
      <template #title>
        <div class="matrix-modal-header">
          <span class="matrix-modal-title">编辑矩阵 - {{ editingPeriod?.effectiveDate?.slice(0, 10) || '' }}</span>
          <div class="matrix-modal-actions">
            <a-button size="small" @click="matrixEditorVisible = false">取消</a-button>
            <a-button size="small" type="primary" :loading="saving" @click="handleSaveMatrix">保存</a-button>
          </div>
        </div>
      </template>
      <div class="matrix-editor-wrapper">
        <div class="matrix-editor-body">
          <div :class="['matrix-layout', { 'has-shop-panel': itemForm.itemType === 4 }]">
            <!-- 左栏：矩阵编辑 -->
            <div class="matrix-main">
              <!-- type=2/3/4: 重量段 + 矩阵 -->
              <div v-if="segments.length > 0" class="section-block section-aligned">
                <WeightSegmentEditor
                  ref="weightSegmentEditorRef"
                  mode="dialog-only"
                  :segments="segments"
                  :matrix="itemForm.itemType === 2 || itemForm.itemType === 4 ? matrix : undefined"
                  @split="handleSplitSegment"
                  @merge="handleMergeSegment"
                  @update="handleUpdateSegment"
                  @fillSegmentPrice="handleFillSegmentPrice"
                />
                <FixedPriceCostMatrix
                  v-if="itemForm.itemType === 2 || itemForm.itemType === 4"
                  :segments="segments"
                  :matrix="matrix"
                  @cellChange="handleCellChange"
                  @clearAll="handleClearAll"
                  @headerClick="handleHeaderClick"
                  @mergeLeft="handleMergeLeft"
                  @mergeRight="handleMergeRight"
                  @split="handleSplitFromHeader"
                  @pasteSegments="handlePasteSegments"
                />
                <CityPriceMatrix
                  v-else-if="itemForm.itemType === 3"
                  :segments="segments"
                  :rows="cityRows"
                  @cellChange="handleCityCellChange"
                  @addCity="handleAddCity"
                  @removeCity="handleRemoveCity"
                />
              </div>
              <div v-else class="section-block">
                <WeightSegmentEditor
                  ref="weightSegmentEditorRef"
                  :segments="segments"
                  @split="handleSplitSegment"
                  @merge="handleMergeSegment"
                  @update="handleUpdateSegment"
                  @fillSegmentPrice="handleFillSegmentPrice"
                />
              </div>
            </div>

            <!-- 右栏：关联店铺（仅一口价类型） -->
            <div v-if="itemForm.itemType === 4" class="shop-panel">
              <div class="shop-panel-title">关联店铺</div>
              <div class="shop-input-row">
                <a-input
                  v-model:value="newShopName"
                  size="small"
                  placeholder="输入店铺名称"
                  @press-enter="handleAddShop"
                />
                <a-button size="small" type="primary" @click="handleAddShop">
                  <template #icon><PlusOutlined /></template>
                </a-button>
              </div>
              <div class="shop-list">
                <a-tag
                  v-for="shop in itemShops"
                  :key="shop"
                  closable
                  @close="handleRemoveShop(shop)"
                >
                  {{ shop }}
                </a-tag>
                <span v-if="itemShops.length === 0" class="shop-empty">暂无关联店铺</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { ValueType } from 'ant-design-vue/es/input-number/src/utils/MiniDecimal'
import type { Dayjs } from 'dayjs'
import { PlusOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import CostItemToolbar from './components/CostItemToolbar.vue'
import CityPriceMatrix from './components/CityPriceMatrix.vue'
import type { CityRow } from './components/CityPriceMatrix.vue'
import FixedPriceCostMatrix from '@/views/express/quotation/components/FixedPriceCostMatrix.vue'
import WeightSegmentEditor from '@/views/express/quotation/components/WeightSegmentEditor.vue'
import type { WeightSegment, PriceCell, ProvinceRow } from '@/views/express/quotation/composables/usePriceMatrix'
import {
  getCostPlanDetail,
  getCostItemMatrix,
  saveCostItemMatrix,
  updateCostPlanItem,
  getProvinceList,
  getCostPlanItemPeriods,
  createCostPlanItemPeriod,
  deleteCostPlanItemPeriod,
  updateCostPlanItemPeriod,
  getCostPlanItemShops,
  setCostPlanItemShops,
  type ProvinceDto,
  type CostItemEntryDto,
  type CostItemInput,
  type CostPlanItemPeriodDto,
  type CityDto,
} from '@/api/express'

// ==================== 基础状态 ====================

const route = useRoute()
const router = useRouter()

const planId = computed(() => Number(route.params.planId))
const itemId = computed(() => Number(route.params.itemId))

const loading = ref(false)
const saving = ref(false)

// 基本信息表单
const itemForm = reactive({
  itemName: '',
  itemType: 1 as number,
  sortOrder: 0,
  settlementWeightStage: undefined as number | undefined,
})

// 时间段管理
const periods = ref<CostPlanItemPeriodDto[]>([])
const activePeriodId = ref<number | null>(null)    // 仅在矩阵弹窗打开时使用
const newPeriodModalVisible = ref(false)
const newPeriodDate = ref<string>('')

// 矩阵弹窗状态
const matrixEditorVisible = ref(false)
const editingPeriod = ref<CostPlanItemPeriodDto | null>(null)

// 全国单价类型的内联价格缓存 — 每个 periodId 对应一个价格
const periodPrices = ref<Map<number, number>>(new Map())

// 各类型数据独立维护（仅在矩阵弹窗中使用）
const segments = ref<WeightSegment[]>([])
const matrix = ref<ProvinceRow[]>([])                        // type=2/4: 省份矩阵
const cityRows = ref<CityRow[]>([])                          // type=3: 城市加收

const pageTitle = computed(() => `编辑成本项 - ${itemForm.itemName || '...'}`)
const backRoute = computed(() => `/express/cost-plan/edit/${planId.value}`)

// WeightSegmentEditor ref
const weightSegmentEditorRef = ref<InstanceType<typeof WeightSegmentEditor> | null>(null)

// ==================== 时间段列表列定义（动态） ====================

const periodColumns = computed(() => {
  const cols: any[] = [
    { title: '生效日期', dataIndex: 'effectiveDate', width: 140 },
  ]
  if (itemForm.itemType === 1) {
    cols.push({ title: '全国单价', dataIndex: 'price', width: 200 })
  }
  cols.push({ title: '操作', dataIndex: 'action', width: 200 })
  return cols
})

// ==================== 全国单价内联编辑 ====================

function getPeriodPrice(period: CostPlanItemPeriodDto): number {
  return periodPrices.value.get(period.id) ?? 0
}

function setPeriodPrice(period: CostPlanItemPeriodDto, val: number) {
  periodPrices.value.set(period.id, val ?? 0)
}

// ==================== 数据加载 ====================

async function loadData() {
  loading.value = true
  try {
    const [provinceList, detail] = await Promise.all([
      getProvinceList(),
      getCostPlanDetail(planId.value),
    ])

    // 从方案详情中找到当前成本项信息
    const item = detail.items?.find((i: any) => i.id === itemId.value)
    if (item) {
      itemForm.itemName = item.itemName
      itemForm.itemType = item.itemType
      itemForm.sortOrder = item.sortOrder
      itemForm.settlementWeightStage = item.settlementWeightStage
    }

    // 始终构建省份行数据（type=2/4 使用，类型切换时也无需重新加载）
    matrix.value = buildProvinceRows(provinceList)

    // 加载时间段列表（正序排列：最早的在上面）
    try {
      const periodList = await getCostPlanItemPeriods(planId.value, itemId.value)
      periods.value = periodList.sort((a, b) => a.effectiveDate.localeCompare(b.effectiveDate))
    } catch {
      // 时间段列表加载失败时不阻塞
    }

    // 对于全国单价类型，逐个加载每个时间段的价格。
    // 加载失败的期间不写入缓存（保存时跳过），否则会把后端已有价格静默覆盖为 0
    if (itemForm.itemType === 1) {
      for (const p of periods.value) {
        try {
          const matrixData = await getCostItemMatrix(planId.value, itemId.value, p.effectiveDate?.slice(0, 10))
          const price = matrixData?.segments?.[0]?.cells?.[0]?.basePrice ?? 0
          periodPrices.value.set(p.id, price)
        } catch {
          // 留空：handleSave 仅保存缓存中存在的期间
        }
      }
    }
  } catch {
    message.error('加载数据失败')
  } finally {
    loading.value = false
  }
}

// ==================== 关联店铺管理（一口价专用） ====================

const itemShops = ref<string[]>([])
const newShopName = ref('')

async function loadItemShops() {
  try {
    itemShops.value = await getCostPlanItemShops(planId.value, itemId.value)
  } catch {
    itemShops.value = []
  }
}

function handleAddShop() {
  const name = newShopName.value.trim()
  if (!name) return
  if (itemShops.value.includes(name)) {
    message.warning('该店铺已存在')
    return
  }
  itemShops.value.push(name)
  newShopName.value = ''
}

function handleRemoveShop(shopName: string) {
  itemShops.value = itemShops.value.filter(s => s !== shopName)
}

async function saveItemShops() {
  await setCostPlanItemShops(planId.value, itemId.value, itemShops.value)
}

// ==================== 打开全屏矩阵编辑器 ====================

async function openMatrixEditor(period: CostPlanItemPeriodDto) {
  editingPeriod.value = period
  activePeriodId.value = period.id
  matrixEditorVisible.value = true
  // 加载该时间段的矩阵数据
  await loadMatrixByActivePeriod()
  // 一口价类型时加载关联店铺
  if (itemForm.itemType === 4) {
    await loadItemShops()
  }
}

/** 根据当前选中的时间段加载矩阵数据 */
async function loadMatrixByActivePeriod() {
  const activePeriod = periods.value.find(p => p.id === activePeriodId.value)
  const effectiveDate = activePeriod?.effectiveDate?.slice(0, 10)

  try {
    const matrixData = await getCostItemMatrix(planId.value, itemId.value, effectiveDate)
    deserializeMatrix(matrixData)
  } catch {
    // 矩阵不存在时，初始化一个默认重量段，并清空上一期间残留的价格
    for (const row of matrix.value) {
      row.prices = {}
    }
    cityRows.value = []
    segments.value = [{
      sortOrder: 0,
      startWeight: 0,
      endWeight: null,
      roundingMethod: 1,
      truncParam: null,
      ceilParam: null,
    }]
  }
}

// 省份数据转换为 ProvinceRow[]
function buildProvinceRows(provinces: ProvinceDto[]): ProvinceRow[] {
  const REGION_ORDER: Record<string, number> = {
    '一区': 1, '二区': 2, '三区': 3, '四区': 4, '五区': 5,
    '六区': 6, '七区': 7, '八区': 8, '偏远': 99,
  }
  return provinces
    .map(p => ({
      provinceId: p.id,
      provinceName: p.shortName || p.name,
      region: p.region,
      regionIndex: REGION_ORDER[p.region] ?? 50,
      isRemote: p.isRemote,
      prices: {} as Record<number, PriceCell>,
    }))
    .sort((a, b) => {
      if (a.regionIndex !== b.regionIndex) return a.regionIndex - b.regionIndex
      return a.provinceId - b.provinceId
    })
}

// ==================== 反序列化 ====================

function deserializeMatrix(entry: CostItemEntryDto) {
  // 切换时间段时必须先清空上一期间残留的价格，否则旧期间数据会"叠加"进当前期间并被保存（跨期串台）
  for (const row of matrix.value) {
    row.prices = {}
  }
  cityRows.value = []

  if (!entry || !entry.segments || entry.segments.length === 0) {
    segments.value = [{
      sortOrder: 0,
      startWeight: 0,
      endWeight: null,
      roundingMethod: 1,
      truncParam: null,
      ceilParam: null,
    }]
    return
  }

  // 反序列化重量段：按下界(weightFrom)排序，与后端计费缓存(CostPlanCache 按 WeightFrom 排序)一致。
  // 此前按 segmentIndex 排序，非末段二次拆分后列序会与实际重量区间错位，相邻合并会造出重叠/空洞段。
  segments.value = entry.segments
    .slice()
    .sort((a, b) => (a.weightFrom ?? 0) - (b.weightFrom ?? 0))
    .map(s => ({
      sortOrder: s.segmentIndex,
      startWeight: s.weightFrom ?? 0,
      endWeight: s.weightTo ?? null,
      roundingMethod: s.roundingMethod ?? 1,
      truncParam: s.truncParam ?? null,
      ceilParam: s.ceilParam ?? null,
    }))

  const scope = entry.pricingScope

  if (scope === 'province') {
    // type=2/4: 省份矩阵 — cells 有 provinceId
    const rowMap = new Map(matrix.value.map(r => [r.provinceId, r]))
    for (const seg of entry.segments) {
      const segIdx = seg.segmentIndex
      for (const cell of (seg.cells ?? [])) {
        if (cell.provinceId == null) continue
        const row = rowMap.get(cell.provinceId)
        if (!row) continue
        row.prices[segIdx] = {
          segmentIndex: segIdx,
          provinceId: cell.provinceId,
          basePrice: cell.basePrice ?? 0,
          firstWeight: cell.firstWeight ?? 0,
          continuePrice: cell.continuePrice ?? 0,
          continueStep: cell.continueStep ?? 1,
          roundingMethodOverride: cell.roundingMethodOverride ?? null,
          truncParamOverride: cell.truncParamOverride ?? null,
          ceilParamOverride: cell.ceilParamOverride ?? null,
        }
      }
    }
  } else if (scope === 'city') {
    // type=3: 城市加收 — cells 有 cityId + cityName
    // 城市加收不支持多重量段，CityPriceMatrix 始终将价格写入 prices[0]
    const cityMap = new Map<number, CityRow>()
    for (const seg of entry.segments) {
      for (const cell of (seg.cells ?? [])) {
        if (cell.cityId == null) continue
        let row = cityMap.get(cell.cityId)
        if (!row) {
          row = {
            cityId: cell.cityId,
            cityName: cell.cityName ?? '',
            provinceId: cell.provinceId ?? 0,
            prices: {},
          }
          cityMap.set(cell.cityId, row)
        }
        row.prices[0] = {
          segmentIndex: 0,
          provinceId: 0,
          basePrice: cell.basePrice ?? 0,
          firstWeight: cell.firstWeight ?? 0,
          continuePrice: cell.continuePrice ?? 0,
          continueStep: cell.continueStep ?? 1,
          roundingMethodOverride: cell.roundingMethodOverride ?? null,
          truncParamOverride: cell.truncParamOverride ?? null,
          ceilParamOverride: cell.ceilParamOverride ?? null,
        }
      }
    }
    cityRows.value = Array.from(cityMap.values())
  }
}

// ==================== 省份矩阵事件处理 (type=2/4) ====================

function handleCellChange(provinceId: number, segmentIndex: number, cell: PriceCell) {
  const row = matrix.value.find(r => r.provinceId === provinceId)
  if (row) row.prices[segmentIndex] = cell
}

function handleClearAll() {
  for (const row of matrix.value) {
    row.prices = {}
  }
}

function handleHeaderClick(segmentSortOrder: number) {
  const idx = segments.value.findIndex(s => s.sortOrder === segmentSortOrder)
  if (idx === -1) return
  weightSegmentEditorRef.value?.openPropertiesDialog(idx)
}

// ==================== 重量段操作（四种类型共用） ====================

function handleMergeLeft(segArrayIdx: number) {
  if (segArrayIdx <= 0) return
  const leftSeg = segments.value[segArrayIdx - 1]
  const rightSeg = segments.value[segArrayIdx]
  leftSeg.endWeight = rightSeg.endWeight
  segments.value.splice(segArrayIdx, 1)
  clearSegmentPrices(rightSeg.sortOrder)
}

function handleMergeRight(segArrayIdx: number) {
  if (segArrayIdx >= segments.value.length - 1) return
  const leftSeg = segments.value[segArrayIdx]
  const rightSeg = segments.value[segArrayIdx + 1]
  rightSeg.startWeight = leftSeg.startWeight
  segments.value.splice(segArrayIdx, 1)
  clearSegmentPrices(leftSeg.sortOrder)
}

function handlePasteSegments(newSegments: WeightSegment[]) {
  segments.value = newSegments
  // 重建矩阵单元格索引
  for (const row of matrix.value) {
    row.prices = {}
  }
}

/** 清除指定重量段在所有数据结构中的价格 */
function clearSegmentPrices(sortOrder: number) {
  for (const row of matrix.value) {
    delete row.prices[sortOrder]
  }
  for (const row of cityRows.value) {
    delete row.prices[sortOrder]
  }
}

// 常用快捷重量点
const SPLIT_SNAP_POINTS = [0.3, 0.5, 1, 1.5, 2, 3, 4, 5, 7, 10, 15, 20, 25, 30, 50]

function handleSplitFromHeader(segmentSortOrder: number) {
  const idx = segments.value.findIndex(s => s.sortOrder === segmentSortOrder)
  if (idx === -1) return
  const seg = segments.value[idx]

  const insidePoints = SPLIT_SNAP_POINTS.filter(sp => {
    if (sp <= seg.startWeight) return false
    if (seg.endWeight != null && sp >= seg.endWeight) return false
    return true
  })

  let defaultPoint: number
  if (insidePoints.length > 0) {
    defaultPoint = insidePoints[0]
  } else if (seg.endWeight != null) {
    defaultPoint = (seg.startWeight + seg.endWeight) / 2
  } else {
    defaultPoint = seg.startWeight + 1
  }
  defaultPoint = Math.round(defaultPoint * 100) / 100
  weightSegmentEditorRef.value?.openSplitDialog(idx, defaultPoint)
}

function handleSplitSegment(segIndex: number, splitWeight: number) {
  const seg = segments.value[segIndex]
  const newSortOrder = Math.max(...segments.value.map(s => s.sortOrder)) + 1
  const newSeg: WeightSegment = {
    sortOrder: newSortOrder,
    startWeight: splitWeight,
    endWeight: seg.endWeight,
    roundingMethod: seg.roundingMethod,
    truncParam: seg.truncParam,
    ceilParam: seg.ceilParam,
  }
  seg.endWeight = splitWeight
  segments.value.splice(segIndex + 1, 0, newSeg)
}

function handleMergeSegment(index: number) {
  // 与报价侧 usePriceMatrix.mergeSegmentColumn 对齐：删除 index 段、保留右邻段(index+1)及其价格；
  // index 为末段时退化为并入左邻段（扩左段 endWeight），避免读取 segments[length] 越界。
  if (segments.value.length <= 1) return
  if (index < segments.value.length - 1) {
    handleMergeRight(index)
  } else {
    handleMergeLeft(index)
  }
}

function handleUpdateSegment(segIndex: number, updates: Partial<WeightSegment>) {
  Object.assign(segments.value[segIndex], updates)
}

function handleFillSegmentPrice(segArrayIdx: number, price: { basePrice?: number; continuePrice?: number }) {
  // WeightSegmentEditor 的 fillSegmentPrice 约定传数组下标；成本侧 sortOrder 非连续（拆分取 max+1），
  // 必须先按下标取段再换算成 sortOrder，否则价格会写进旧段的列
  const targetSeg = segments.value[segArrayIdx]
  if (!targetSeg) return
  const segmentSortOrder = targetSeg.sortOrder
  const itemType = itemForm.itemType

  if (itemType === 2 || itemType === 4) {
    // 省份矩阵
    for (const row of matrix.value) {
      if (!row.prices[segmentSortOrder]) {
        row.prices[segmentSortOrder] = {
          segmentIndex: segmentSortOrder,
          provinceId: row.provinceId,
          basePrice: 0,
          firstWeight: 0,
          continuePrice: 0,
          continueStep: 1,
          roundingMethodOverride: null,
          truncParamOverride: null,
          ceilParamOverride: null,
        }
      }
      const cell = row.prices[segmentSortOrder]
      if (price.basePrice != null) cell.basePrice = price.basePrice
      if (price.continuePrice != null) cell.continuePrice = price.continuePrice
    }
  } else if (itemType === 3) {
    // 城市加收：始终写入 prices[0]
    for (const row of cityRows.value) {
      if (!row.prices[0]) {
        row.prices[0] = {
          segmentIndex: 0,
          provinceId: 0,
          basePrice: 0,
          firstWeight: 0,
          continuePrice: 0,
          continueStep: 1,
          roundingMethodOverride: null,
          truncParamOverride: null,
          ceilParamOverride: null,
        }
      }
      const cell = row.prices[0]
      if (price.basePrice != null) cell.basePrice = price.basePrice
      if (price.continuePrice != null) cell.continuePrice = price.continuePrice
    }
  }
}

// ==================== 城市加收事件处理 (type=3) ====================

function handleCityCellChange(cityId: number, segmentIndex: number, cell: PriceCell) {
  const row = cityRows.value.find(r => r.cityId === cityId)
  if (row) row.prices[segmentIndex] = cell
}

function handleAddCity(city: CityDto) {
  if (cityRows.value.some(r => r.cityId === city.id)) return
  const newRow: CityRow = {
    cityId: city.id,
    cityName: city.cityName,
    provinceId: city.provinceId,
    prices: {},
  }
  // 为各段初始化空价格（城市加收始终使用 prices[0]）
  for (const seg of segments.value) {
    newRow.prices[0] = {
      segmentIndex: 0,
      provinceId: 0,
      basePrice: 0,
      firstWeight: 0,
      continuePrice: 0,
      continueStep: 1,
      roundingMethodOverride: null,
      truncParamOverride: null,
      ceilParamOverride: null,
    }
  }
  cityRows.value.push(newRow)
}

function handleRemoveCity(cityId: number) {
  const idx = cityRows.value.findIndex(r => r.cityId === cityId)
  if (idx !== -1) cityRows.value.splice(idx, 1)
}

// ==================== 保存 ====================

async function handleSave() {
  if (!itemForm.itemName) {
    message.warning('请输入成本项名称')
    return
  }
  saving.value = true
  try {
    // 1. 保存基本信息
    await updateCostPlanItem(planId.value, itemId.value, {
      itemName: itemForm.itemName,
      itemType: itemForm.itemType,
      sortOrder: itemForm.sortOrder,
      settlementWeightStage: itemForm.itemType === 4 ? itemForm.settlementWeightStage : undefined,
    })

    // 2. 全国单价类型：保存每个时间段的价格（仅保存已加载/已编辑的期间，防止覆盖加载失败期间的已有价格）
    if (itemForm.itemType === 1) {
      for (const p of periods.value) {
        if (!periodPrices.value.has(p.id)) continue
        const price = periodPrices.value.get(p.id) ?? 0
        const payload: CostItemInput & { effectiveDate?: string } = {
          costItemId: itemId.value,
          pricingScope: 'national',
          effectiveDate: p.effectiveDate?.slice(0, 10),
          segments: [{
            segmentIndex: 1,
            weightFrom: 0,
            weightTo: null,
            calcMethod: 2,
            roundingMethod: 1,
            truncParam: null,
            ceilParam: null,
            cells: [{ basePrice: price, continuePrice: 0, firstWeight: 0, continueStep: 1 }],
          }],
        }
        await saveCostItemMatrix(planId.value, itemId.value, payload)
      }
    }

    message.success('保存成功')
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

// 校验重量段连续性（无重叠、无空洞；开放上限只能在末段）——与后端 ValidateSegmentContinuity 同口径。
// 不强制首段从 0 起（如"大货派费仅 >3kg"是合法配置）。落入空洞/重叠的重量计费会算 0，须在保存前拦截。
function validateSegmentContinuity(): string | null {
  const ordered = segments.value
    .slice()
    .sort((a, b) => (a.startWeight ?? 0) - (b.startWeight ?? 0))
  for (let i = 0; i < ordered.length; i++) {
    const s = ordered[i]
    const from = s.startWeight ?? 0
    if (s.endWeight != null && s.endWeight <= from) {
      return `重量段区间非法（上界须大于下界）：[${from}, ${s.endWeight})`
    }
    if (i > 0) {
      const prev = ordered[i - 1]
      if (prev.endWeight == null) {
        return '开放上限（无上界）的重量段只能是最末段，其后不得再有段'
      }
      if (from > prev.endWeight) {
        return `重量段存在空洞：[${prev.endWeight}, ${from}) 无覆盖，落入该区间的重量成本会算 0`
      }
      if (from < prev.endWeight) {
        return `重量段存在重叠：上一段止于 ${prev.endWeight}，本段从 ${from} 开始`
      }
    }
  }
  return null
}

async function handleSaveMatrix() {
  if (segments.value.length === 0) {
    message.warning('请至少添加一个重量段')
    return
  }
  const continuityError = validateSegmentContinuity()
  if (continuityError) {
    message.warning(continuityError)
    return
  }
  saving.value = true
  try {
    const payload = serializeMatrix()
    // 把当前编辑时间段的 effectiveDate 加到 payload
    if (editingPeriod.value) {
      (payload as any).effectiveDate = editingPeriod.value.effectiveDate?.slice(0, 10)
    }
    await saveCostItemMatrix(planId.value, itemId.value, payload)
    // 一口价类型同时保存关联店铺
    if (itemForm.itemType === 4) {
      await saveItemShops()
    }
    message.success('保存成功')
    matrixEditorVisible.value = false
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

function serializeMatrix() {
  const itemType = itemForm.itemType

  let pricingScope: 'national' | 'province' | 'city'
  if (itemType === 1) {
    pricingScope = 'national'
  } else if (itemType === 3) {
    pricingScope = 'city'
  } else {
    pricingScope = 'province'
  }

  // type=2/3/4: 重量段 + 矩阵
  return {
    costItemId: itemId.value,
    pricingScope,
    segments: segments.value.map(seg => {
      let cells: any[] = []

      if (itemType === 3) {
        // 城市加收：cells 有 cityId + cityName
        // 城市加收不支持多重量段，CityPriceMatrix 始终将价格写入 prices[0]
        // basePrice 为 null 的空壳单元格（点击过但未填值）必须过滤，否则后端非空 decimal 反序列化 400
        cells = cityRows.value
          .filter(row => row.prices[0] && row.prices[0].basePrice != null)
          .map(row => {
            const cell = row.prices[0]
            return {
              // 城市加收单元格必须携带 provinceId：后端 ValidatePricingScopeCells 强制要求，
              // 且计费引擎 FindCellCity 按 (provinceId, cityName) 匹配，缺失会导致保存 400 且永不命中
              provinceId: row.provinceId,
              cityId: row.cityId,
              cityName: row.cityName,
              basePrice: cell.basePrice,
              continuePrice: cell.continuePrice ?? 0,
              firstWeight: cell.firstWeight ?? 0,
              continueStep: cell.continueStep ?? 1,
              roundingMethodOverride: cell.roundingMethodOverride,
              truncParamOverride: cell.truncParamOverride,
              ceilParamOverride: cell.ceilParamOverride,
            }
          })
      } else {
        // type=2/4: 省份矩阵：cells 有 provinceId（basePrice 为 null 的空壳单元格过滤，避免后端 400）
        cells = matrix.value
          .filter(row => row.prices[seg.sortOrder] && row.prices[seg.sortOrder].basePrice != null)
          .map(row => {
            const cell = row.prices[seg.sortOrder]
            return {
              provinceId: row.provinceId,
              basePrice: cell.basePrice,
              continuePrice: cell.continuePrice ?? 0,
              firstWeight: cell.firstWeight ?? 0,
              continueStep: cell.continueStep ?? 1,
              roundingMethodOverride: cell.roundingMethodOverride,
              truncParamOverride: cell.truncParamOverride,
              ceilParamOverride: cell.ceilParamOverride,
            }
          })
      }

      return {
        segmentIndex: seg.sortOrder,
        weightFrom: seg.startWeight,
        weightTo: seg.endWeight,
        calcMethod: 2, // 阶梯模式
        roundingMethod: seg.roundingMethod,
        truncParam: seg.truncParam,
        ceilParam: seg.ceilParam,
        cells,
      }
    }),
  }
}

function handleCancel() {
  router.back()
}

// ==================== 时间段管理 ====================

const creatingPeriod = ref(false)

/** 打开新增时间段弹窗 */
function openNewPeriodModal() {
  newPeriodDate.value = ''
  newPeriodModalVisible.value = true
}

/** 创建时间段 */
async function handleCreatePeriod() {
  if (!newPeriodDate.value) {
    message.warning('请选择生效日期')
    return
  }
  creatingPeriod.value = true
  try {
    const newPeriod = await createCostPlanItemPeriod(planId.value, itemId.value, {
      effectiveDate: newPeriodDate.value,
    })
    message.success('时间段创建成功')
    newPeriodModalVisible.value = false

    // 刷新时间段列表（正序排列）
    const periodList = await getCostPlanItemPeriods(planId.value, itemId.value)
    periods.value = periodList.sort((a, b) => a.effectiveDate.localeCompare(b.effectiveDate))

    // 全国单价类型：为新时间段初始化价格为0
    if (itemForm.itemType === 1) {
      periodPrices.value.set(newPeriod.id, 0)
    }
  } catch {
    message.error('创建时间段失败')
  } finally {
    creatingPeriod.value = false
  }
}

/** 删除指定时间段 */
async function handleDeletePeriod(periodId: number) {
  try {
    await deleteCostPlanItemPeriod(planId.value, itemId.value, periodId)
    message.success('时间段已删除')

    // 刷新时间段列表（正序排列）
    const periodList = await getCostPlanItemPeriods(planId.value, itemId.value)
    periods.value = periodList.sort((a, b) => a.effectiveDate.localeCompare(b.effectiveDate))

    // 清除被删除时间段的价格缓存
    periodPrices.value.delete(periodId)

    // 如果删除的是当前矩阵弹窗编辑的时间段，关闭弹窗
    if (editingPeriod.value?.id === periodId) {
      matrixEditorVisible.value = false
      editingPeriod.value = null
    }
  } catch {
    message.error('删除时间段失败')
  }
}

async function handleUpdatePeriodDate(record: CostPlanItemPeriodDto, newDate: string) {
  if (!newDate) return
  try {
    // 仅传 effectiveDate；后端按部分更新只改日期、不再清空该期间的成本矩阵
    await updateCostPlanItemPeriod(planId.value, itemId.value, record.id, { effectiveDate: newDate })
    record.effectiveDate = newDate
    // 重新排序
    periods.value = [...periods.value].sort((a, b) => a.effectiveDate.localeCompare(b.effectiveDate))
  } catch {
    // 后端已由响应拦截器提示具体错误（如生效日期重复）；重新拉取以回滚日期选择器显示
    try {
      const periodList = await getCostPlanItemPeriods(planId.value, itemId.value)
      periods.value = periodList.sort((a, b) => a.effectiveDate.localeCompare(b.effectiveDate))
    } catch {
      /* 忽略回滚拉取失败 */
    }
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped lang="scss">
.cost-item-detail-page {
  display: flex;
  flex-direction: column;
  height: 100%;
}

:deep(.ant-spin-nested-loading),
:deep(.ant-spin-container) {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: visible;
  min-height: 0;
}

.edit-scroll-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  background: #f5f6f8;
}

.section-block {
  background: #fff;
  border-radius: 6px;
  padding: 16px;
  margin-bottom: 12px;
  border: 1px solid #f0f0f0;
}

.section-aligned {
  padding: 0 !important;
  background: transparent !important;
  border: 0 !important;
}

.period-section {
  background: #fff;
  border-radius: 6px;
  padding: 12px 16px;
  margin-bottom: 12px;
  border: 1px solid #f0f0f0;
}

.period-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.period-title {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.matrix-layout {
  display: flex;
  min-height: 0;
  flex: 1;
}

.matrix-layout .matrix-main {
  flex: 1;
  min-width: 0;
}

.matrix-layout.has-shop-panel .matrix-main {
  margin-right: 12px;
}

.shop-panel {
  width: 180px;
  flex-shrink: 0;
  background: #fff;
  border-radius: 6px;
  border: 1px solid #f0f0f0;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-self: flex-start;
  position: sticky;
  top: 0;
}

.shop-panel-title {
  font-size: 13px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.shop-panel .shop-input-row {
  display: flex;
  gap: 4px;
}

.shop-panel .shop-input-row .ant-input {
  flex: 1;
}

.shop-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.shop-list .ant-tag {
  margin: 0;
}

.shop-empty {
  color: #999;
  font-size: 12px;
}
</style>

<style lang="scss">
// 全屏 Modal（全局样式，不加 scoped）
.fullscreen-matrix-modal {
  .ant-modal {
    max-width: 100vw !important;
    top: 0 !important;
    padding: 0 !important;
    margin: 0 !important;
  }
  .ant-modal-content {
    height: 100vh;
    display: flex;
    flex-direction: column;
    border-radius: 0;
  }
  .ant-modal-header {
    padding: 8px 16px;
    margin: 0;
    border-bottom: 1px solid #f0f0f0;
    flex-shrink: 0;
  }
  .ant-modal-body {
    flex: 1;
    overflow: hidden;
    padding: 0;
  }
  .ant-modal-close {
    display: none;
  }
}

.matrix-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.matrix-modal-title {
  font-size: 14px;
  font-weight: 500;
}

.matrix-modal-actions {
  display: flex;
  gap: 8px;
}

.matrix-editor-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.matrix-editor-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 16px 16px;
}
</style>