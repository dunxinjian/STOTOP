<template>
  <div class="page-container">
    <PageHeader :title="pageTitle" backLabel="返回">
      <template #left>
        <a-button type="link" @click="handleBack" style="padding: 0">
          <template #icon><ArrowLeftOutlined /></template>返回
        </a-button>
      </template>
      <template #actions>
        <a-button @click="importModalVisible = true">
          <template #icon><UploadOutlined /></template>导入
        </a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">
          <template #icon><SaveOutlined /></template>保存
        </a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <div class="edit-layout">
        <!-- 左侧主区域 -->
        <div class="edit-main">
          <div class="edit-scroll-wrapper">
            <!-- 基本信息条 -->
            <BasicInfoCard
              :form-data="formData"
              :is-client-locked="isClientLocked"
              :client-options="clientOptions"
              :client-search-loading="clientSearchLoading"
              @update:formData="handleFormDataUpdate"
              @openSettings="settingsDrawerVisible = true"
              @clientTypeChange="handleClientTypeChange"
              @clientSearch="handleClientSearch"
            />

            <a-divider style="margin: 0" />

            <!-- 价格矩阵（含内置工具栏、列头操作、右侧 aside slot） -->
            <div class="section-block section-matrix" v-if="segments.length > 0">
              <!-- 隐藏宿主：仅承载属性编辑/拆分弹窗，无可见 UI -->
              <WeightSegmentEditor
                ref="weightSegmentEditorRef"
                mode="dialog-only"
                :segments="segments"
                :matrix="matrix"
                @split="handleSplitSegment"
                @merge="handleMergeSegment"
                @update="updateSegment"
                @fillSegmentPrice="handleFillSegmentPrice"
              />
              <FixedPriceCostMatrix
                :segments="(segments as any)"
                :matrix="(matrix as any)"
                @cellChange="handleCellChange"
                @clearAll="handleClearAll"
                @headerClick="handleHeaderClick"
                @mergeLeft="handleMergeLeft"
                @mergeRight="handleMergeRight"
                @split="handleSplitFromHeader"
                @pasteSegments="handlePasteSegments"
              >
                <template #aside>
                  <!-- 关联店铺 -->
                  <div class="aside-section-title">关联店铺</div>
                  <ShopPanel :quotation-id="planId" :key="shopPanelKey" />
                  <!-- 功能入口 -->
                  <div class="aside-entries">
                    <div class="aside-entry" @click="openBatchShopModal">
                      <TeamOutlined class="entry-icon" />
                      <span>管理店铺与别名</span>
                      <RightOutlined class="entry-arrow" />
                    </div>
                    <div class="aside-entry" :class="{ disabled: isCreate }" @click="openCommissionDrawer">
                      <DollarOutlined class="entry-icon" />
                      <span>佣金配置</span>
                      <RightOutlined class="entry-arrow" />
                    </div>
                    <div class="aside-entry" :class="{ disabled: isCreate }" @click="openChangelogDrawer">
                      <FileTextOutlined class="entry-icon" />
                      <span>变更日志</span>
                      <RightOutlined class="entry-arrow" />
                    </div>
                    <div class="aside-entry" @click="surchargeDrawerVisible = true">
                      <span>加收方案</span>
                      <span class="surcharge-count" v-if="surchargeLinkedIds.length">({{ surchargeLinkedIds.length }})</span>
                      <RightOutlined class="entry-arrow" />
                    </div>
                  </div>
                </template>
              </FixedPriceCostMatrix>
            </div>
          </div>
        </div>
      </div>
    </a-spin>

    <!-- 导入弹窗 -->
    <ImportQuotationModal v-model:visible="importModalVisible" @success="handleImportSuccess" />

    <!-- 商务条款 Drawer -->
    <SettingsDrawer
      v-model:visible="settingsDrawerVisible"
      :form-data="formData"
      @save="handleSettingsSave"
    />

    <!-- 加收方案 Drawer -->
    <SurchargeDrawer
      v-model:visible="surchargeDrawerVisible"
      :linked-ids="surchargeLinkedIds"
      @link="handleSurchargeLink"
      @unlink="handleSurchargeUnlink"
    />

    <!-- 佣金配置 Drawer -->
    <a-drawer
      v-model:open="commissionDrawerVisible"
      title="佣金配置"
      :width="560"
      :destroy-on-close="true"
    >
      <CommissionConfigPanel :quotation-id="planId" />
    </a-drawer>

    <!-- 变更日志 Drawer -->
    <a-drawer
      v-model:open="changelogDrawerVisible"
      title="变更日志"
      :width="640"
      :destroy-on-close="true"
    >
      <ChangeLogPanel :quotation-id="planId" />
    </a-drawer>

    <!-- 批量店铺管理 Modal -->
    <BatchShopModal
      :open="batchShopModalVisible"
      :quotation-id="planId"
      :quotation-name="formData.planName || ''"
      :shared-shop-enabled="formData.sharedShopEnabled"
      @update:open="batchShopModalVisible = $event"
      @changed="handleShopChanged"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { UploadOutlined, SaveOutlined, RightOutlined, ArrowLeftOutlined, TeamOutlined, DollarOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import BasicInfoCard from './components/BasicInfoCard.vue'
import FixedPriceCostMatrix from './components/FixedPriceCostMatrix.vue'
import WeightSegmentEditor from './components/WeightSegmentEditor.vue'
import ShopPanel from './components/ShopPanel.vue'
import SettingsDrawer from './components/SettingsDrawer.vue'
import SurchargeDrawer from './components/SurchargeDrawer.vue'
import ChangeLogPanel from './components/ChangeLogPanel.vue'
import CommissionConfigPanel from './components/CommissionConfigPanel.vue'
import BatchShopModal from '../components/BatchShopModal.vue'
import ImportQuotationModal from './ImportQuotationModal.vue'
import { useQuotationForm } from './composables/useQuotationForm'
import type { QuotationFormData, PriceCell, WeightSegment } from './composables/usePriceMatrix'
import { mergeSegmentLeft, mergeSegmentRight } from './composables/usePriceMatrix'

const route = useRoute()
const router = useRouter()

// ==================== 路由参数 ====================

const planId = computed(() => {
  const id = Number(route.params.id)
  return isNaN(id) ? 0 : id
})

const isCreateRoute = computed(() => route.path.includes('/create'))

// ==================== Composable ====================

const {
  formData,
  segments,
  matrix,
  clientOptions,
  clientName,
  clientSearchLoading,
  isClientLocked,
  loading,
  saving,
  isCreate,
  init,
  loadPlan,
  savePlan,
  handleClientSearch,
  handleClientTypeChange,
  splitSegment,
  mergeSegment,
  updateSegment,
  setCellValue,
  setCellOverrides,
  batchFill,
} = useQuotationForm(isCreateRoute.value ? 0 : planId.value)

// ==================== 页面标题 ====================

const pageTitle = computed(() => {
  if (isCreate.value) return '新建报价'
  if (clientName.value) return `${clientName.value} 的报价`
  return '编辑报价'
})

// ==================== Drawer 状态 ====================

const settingsDrawerVisible = ref(false)
const surchargeDrawerVisible = ref(false)
const commissionDrawerVisible = ref(false)
const changelogDrawerVisible = ref(false)

// ==================== BatchShopModal ====================

const batchShopModalVisible = ref(false)
const shopPanelKey = ref(0)

function openBatchShopModal() {
  if (!planId.value || planId.value === 0) {
    message.warning('请先保存报价后再管理店铺与别名')
    return
  }
  batchShopModalVisible.value = true
}

function handleShopChanged() {
  // 通过更新 key 强制 ShopPanel 重新挂载以刷新数据
  shopPanelKey.value++
}

function openCommissionDrawer() {
  if (isCreate.value) return
  commissionDrawerVisible.value = true
}

function openChangelogDrawer() {
  if (isCreate.value) return
  changelogDrawerVisible.value = true
}

// ==================== 导入弹窗 ====================

const importModalVisible = ref(false)

// ==================== 脏数据标记 ====================

const isDirty = ref(false)

function markDirty() {
  isDirty.value = true
}

// ==================== 右侧面板 - 加收方案 ====================

const surchargeLinkedIds = ref<(number | string)[]>([])

function handleSurchargeLink(id: number | string) {
  if (!surchargeLinkedIds.value.includes(id)) {
    surchargeLinkedIds.value.push(id)
    markDirty()
  }
}

function handleSurchargeUnlink(id: number | string) {
  surchargeLinkedIds.value = surchargeLinkedIds.value.filter(i => i !== id)
  markDirty()
}

// ==================== 事件处理 ====================

function handleFormDataUpdate(data: QuotationFormData) {
  Object.assign(formData, data)
  markDirty()
}

function handleSettingsSave(data: Partial<QuotationFormData>) {
  Object.assign(formData, data)
  markDirty()
}

function handleSplitSegment(index: number, splitPoint: number) {
  splitSegment(index, splitPoint)
  markDirty()
}

function handleMergeSegment(index: number) {
  mergeSegment(index)
  markDirty()
}

function handleFillSegmentPrice(
  segmentIndex: number,
  price: { basePrice?: number; continuePrice?: number },
) {
  const seg = segments.value[segmentIndex]
  if (!seg) return

  batchFill({
    segmentIndex: seg.sortOrder,
    value1: price.basePrice ?? 0,
    value2: price.continuePrice,
  })
  markDirty()
  message.success('已填充该重量段所有省份价格')
}

function handleCellChange(provinceId: number, segmentIndex: number, cell: PriceCell) {
  setCellValue(segmentIndex, provinceId, 'basePrice', cell.basePrice)
  setCellValue(segmentIndex, provinceId, 'continuePrice', cell.continuePrice)
  // 首重/续重步进参与计费公式 base + ceil(max(0, w - firstWeight) / continueStep) * continuePrice，
  // 漏存会使 "3+(w-1)*5"、"3+5/0.5" 等输入的首重偏移与步进静默退化为默认值
  setCellValue(segmentIndex, provinceId, 'firstWeight', cell.firstWeight ?? 0)
  setCellValue(segmentIndex, provinceId, 'continueStep', cell.continueStep ?? 1)
  setCellOverrides(segmentIndex, provinceId, {
    roundingMethodOverride: cell.roundingMethodOverride,
    truncParamOverride: cell.truncParamOverride,
    ceilParamOverride: cell.ceilParamOverride,
  })
  markDirty()
}

// ==================== 列头点击 → 打开重量段属性编辑 Modal ====================

const weightSegmentEditorRef = ref<InstanceType<typeof WeightSegmentEditor> | null>(null)

function handleHeaderClick(segmentSortOrder: number) {
  const idx = segments.value.findIndex(s => s.sortOrder === segmentSortOrder)
  if (idx === -1) return
  weightSegmentEditorRef.value?.openPropertiesDialog(idx)
}

// ==================== 列头 ⊕ 点击 → 打开拆分重量段弹窗 ====================

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

function handleMergeLeft(index: number) {
  mergeSegmentLeft(segments.value, matrix.value, index)
  markDirty()
}

function handleMergeRight(index: number) {
  mergeSegmentRight(segments.value, matrix.value, index)
  markDirty()
}

function handlePasteSegments(newSegments: WeightSegment[]) {
  segments.value = newSegments
  // 重建矩阵单元格索引
  for (const row of matrix.value) {
    row.prices = {}
  }
  markDirty()
}

function handleClearAll() {
  for (const row of matrix.value) {
    for (const key of Object.keys(row.prices)) {
      const cell = row.prices[Number(key)]
      if (cell) {
        cell.basePrice = null
        cell.continuePrice = null
        cell.roundingMethodOverride = null
        cell.truncParamOverride = null
        cell.ceilParamOverride = null
      }
    }
  }
  message.success('已清空全部价格数据')
  markDirty()
}

// ==================== 保存 ====================

async function handleSave() {
  const ok = await savePlan()
  if (ok) {
    isDirty.value = false
    router.push('/express/quotation')
  }
}

// ==================== 返回 ====================

function handleBack() {
  if (isDirty.value) {
    Modal.confirm({
      title: '提示',
      content: '有未保存的修改，确定离开吗？',
      okText: '确定离开',
      cancelText: '继续编辑',
      onOk: () => router.push('/express/quotation'),
    })
  } else {
    router.push('/express/quotation')
  }
}

// ==================== 导入成功回调 ====================

function handleImportSuccess() {
  importModalVisible.value = false
  if (!isCreate.value && planId.value) {
    loadPlan(planId.value)
  }
}

// ==================== 初始化 ====================

onMounted(() => {
  init()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.edit-layout {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.edit-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.edit-scroll-wrapper {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.section-block {
  padding: 12px 16px;
}

// 矩阵区块：去除 section-block 的装饰（padding/background/border），
// 避免与 FixedPriceCostMatrix 内部卡片产生「卡片套卡片」多余留白
.section-matrix {
  padding: 0 !important;
  background: transparent !important;
  border: 0 !important;
}

// 右侧 aside 内“关联店铺”标题
.aside-section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
  margin-bottom: 10px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border);
}

// 功能入口区
.aside-entries {
  border-top: 1px solid var(--border);
  padding-top: 8px;
  margin-top: 12px;
}

.aside-entry {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 8px;
  border-radius: $border-radius-sm;
  cursor: pointer;
  font-size: $font-size-sm;
  color: $text-regular;
  transition: $transition-base;

  &:hover {
    background: $color-primary-bg;
    color: $color-primary;
  }

  &.disabled {
    opacity: 0.4;
    cursor: not-allowed;

    &:hover {
      background: none;
      color: $text-regular;
    }
  }

  .entry-icon {
    font-size: 12px;
    flex-shrink: 0;
  }

  .surcharge-count {
    color: $color-primary;
    font-weight: 500;
  }

  .entry-arrow {
    margin-left: auto;
    font-size: 10px;
    color: $text-secondary;
  }
}
</style>
