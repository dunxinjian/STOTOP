<template>
  <div class="page-container cost-plan-detail-page">
    <PageHeader :title="pageTitle" back-to="/express/cost-plan">
      <template #actions>
        <a-button v-if="planStatus === 1" @click="handleDeactivate">停用方案</a-button>
        <a-popconfirm v-if="planStatus === 0" title="确定启用此方案？" @confirm="handleActivate">
          <a-button type="primary">启用方案</a-button>
        </a-popconfirm>
      </template>
    </PageHeader>

    <a-spin :spinning="loading">
      <!-- 方案基本信息 -->
      <a-card :bordered="false" class="plan-info-card">
        <div class="plan-info-header">
          <h2 class="plan-title">{{ planDetail.planName || '...' }}</h2>
          <a-tag :color="getStatusColor(planDetail.status)" class="status-tag">{{ getStatusText(planDetail.status) }}</a-tag>
        </div>
        <div class="plan-meta">
          <span>品牌: {{ planDetail.brandCode }}</span>
          <span class="meta-dot">·</span>
          <span>成本项: {{ items.length }} 项</span>
          <span class="meta-dot">·</span>
          <span>创建: {{ planDetail.createdTime?.slice(0, 10) || '-' }}</span>
        </div>
      </a-card>

      <!-- Tab 区域 -->
      <a-card :bordered="false">
        <a-tabs v-model:activeKey="activeTab">
          <!-- Tab 1: 成本项列表 -->
          <a-tab-pane key="items">
            <template #tab>成本项 ({{ items.length }})</template>
            <div style="margin-bottom: 8px;">
              <a-button type="primary" @click="openItemModal()">
                <template #icon><PlusOutlined /></template>新建成本项
              </a-button>
            </div>
            <a-empty v-if="items.length === 0 && !itemsLoading" description="暂无成本项，点击上方按钮新建" />
            <a-table
              v-else
              :columns="itemColumns"
              :data-source="items"
              :loading="itemsLoading"
              row-key="id"
              size="small"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'itemType'">
                  <a-tag :color="getItemTypeColor(record.itemType)">{{ getItemTypeText(record.itemType) }}</a-tag>
                </template>
                <template v-if="column.dataIndex === 'outletIds'">
                  <span v-if="(record.outletIds?.length ?? 0) > 0" class="count-badge">{{ record.outletIds.length }} 个网点</span>
                  <span v-else class="text-muted">—</span>
                </template>
                <template v-if="column.dataIndex === 'shopNames'">
                  <span v-if="(record.shopNames?.length ?? 0) > 0" class="count-badge">{{ record.shopNames.length }} 个店铺</span>
                  <span v-else class="text-muted">—</span>
                </template>
                <template v-if="column.dataIndex === 'periodCount'">
                  {{ record.periodCount ?? 0 }}
                </template>
                <template v-if="column.dataIndex === 'sortOrder'">
                  <span class="sort-cell">{{ record.sortOrder }}</span>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" @click="handleEditItem(record as CostPlanItemDto)"><EditOutlined /> 编辑</a-button>
                  <a-popconfirm title="确定删除此成本项？" @confirm="handleDeleteItem(record as CostPlanItemDto)">
                    <a-button type="link" size="small" danger><DeleteOutlined /> 删除</a-button>
                  </a-popconfirm>
                </template>
              </template>
            </a-table>
          </a-tab-pane>

          <!-- Tab 2: 互斥配置 -->
          <a-tab-pane key="exclusions">
            <template #tab>互斥配置 ({{ exclusions.length }})</template>
            <div style="margin-bottom: 8px;">
              <a-button type="primary" @click="openExclusionModal()">
                <template #icon><PlusOutlined /></template>新建互斥配置
              </a-button>
            </div>
            <a-empty v-if="exclusions.length === 0 && !exclusionsLoading" description="暂无互斥配置，点击上方按钮新建" />
            <a-table
              v-else
              :columns="exclusionColumns"
              :data-source="exclusions"
              :loading="exclusionsLoading"
              row-key="id"
              size="small"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'effectiveDate'">
                  {{ record.effectiveDate?.slice(0, 10) ?? '-' }}
                </template>
                <template v-if="column.dataIndex === 'exclusionRuleJson'">
                  <template v-if="getExcludedItemNames(record as CostPlanExclusionDto).length > 0">
                    <a-tag v-for="name in getExcludedItemNames(record as CostPlanExclusionDto)" :key="name" size="small" style="margin-bottom: 2px">{{ name }}</a-tag>
                  </template>
                  <span v-else class="text-muted">未配置</span>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" @click="openExclusionModal(record as CostPlanExclusionDto)"><EditOutlined /> 编辑</a-button>
                  <a-popconfirm title="确定删除此互斥配置？" @confirm="handleDeleteExclusion(record as CostPlanExclusionDto)">
                    <a-button type="link" size="small" danger><DeleteOutlined /> 删除</a-button>
                  </a-popconfirm>
                </template>
              </template>
            </a-table>
          </a-tab-pane>
        </a-tabs>
      </a-card>
    </a-spin>

    <!-- 成本项弹窗 -->
    <a-modal
      v-model:open="itemModalVisible"
      :title="itemModalTitle"
      ok-text="确定"
      cancel-text="取消"
      :confirm-loading="itemModalLoading"
      @ok="handleItemModalOk"
    >
      <a-form :label-col="{ span: 5 }" style="margin-top: 16px;">
        <a-form-item label="名称" required>
          <a-input v-model:value="itemForm.itemName" placeholder="成本项名称" allow-clear />
        </a-form-item>
        <a-form-item label="类型" required>
          <a-select v-model:value="itemForm.itemType" placeholder="请选择类型" :options="itemTypeOptions" />
        </a-form-item>
        <a-form-item label="排序号">
          <a-input-number v-model:value="itemForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="itemForm.itemType === 4" label="结算重量">
          <a-select
            v-model:value="itemForm.settlementWeightStage"
            :options="weightStageOptions"
            placeholder="请选择结算重量环节"
            style="width: 100%"
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 互斥配置弹窗 -->
    <a-modal
      v-model:open="exclusionModalVisible"
      :title="exclusionModalTitle"
      ok-text="确定"
      cancel-text="取消"
      :confirm-loading="exclusionModalLoading"
      @ok="handleExclusionModalOk"
    >
      <a-form :label-col="{ span: 5 }" style="margin-top: 16px;">
        <a-form-item label="生效日期" required>
          <a-date-picker v-model:value="exclusionForm.effectiveDate" style="width: 100%" />
        </a-form-item>
        <a-form-item label="排除的成本项">
          <a-checkbox-group v-model:value="exclusionForm.selectedItemIds" style="width: 100%">
            <div v-for="item in excludableItems" :key="item.id" class="exclusion-item-row">
              <a-checkbox :value="item.id">
                <span class="exclusion-item-name">{{ item.itemName }}</span>
                <a-tag :color="getItemTypeColor(item.itemType)" size="small" class="exclusion-item-type-tag">
                  {{ getItemTypeText(item.itemType) }}
                </a-tag>
              </a-checkbox>
            </div>
          </a-checkbox-group>
          <a-empty v-if="excludableItems.length === 0" description="暂无可排除的成本项" :image="null" style="margin-top: 8px" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { WEIGHT_STAGE_OPTIONS } from './constants'
import {
  getCostPlanDetail,
  activateCostPlan,
  deactivateCostPlan,
  getCostPlanItems,
  createCostPlanItem,
  updateCostPlanItem,
  deleteCostPlanItem,
  getCostPlanExclusions,
  createCostPlanExclusion,
  updateCostPlanExclusion,
  deleteCostPlanExclusion,
  type CostPlanDetail as CostPlanDetailType,
  type CostPlanItemDto,
  type CostPlanExclusionDto,
} from '@/api/express'

const route = useRoute()
const router = useRouter()

const planId = computed(() => Number(route.params.id))
const loading = ref(false)
const activeTab = ref('items')

// ==================== 方案基本信息 ====================
const planDetail = reactive<CostPlanDetailType>({
  id: 0,
  brandCode: '',
  planName: '',
  status: 0,
  orgId: 0,
  createdTime: '',
  updatedTime: '',
  items: [],
  exclusions: [],
})

const planStatus = computed(() => planDetail.status)

const pageTitle = computed(() => {
  const suffix = planStatus.value === 1 ? '（启用中）' : planStatus.value === 2 ? '（已停用）' : '（草稿）'
  return `成本方案 - ${planDetail.planName || '...'}${suffix}`
})

// ==================== 状态映射 ====================
function getStatusText(s: number) {
  const map: Record<number, string> = { 0: '草稿', 1: '启用', 2: '停用' }
  return map[s] ?? '未知'
}
function getStatusColor(s: number) {
  const map: Record<number, string> = { 0: 'default', 1: 'green', 2: 'red' }
  return map[s] ?? 'default'
}

// ==================== 加载方案详情 ====================
async function loadDetail() {
  loading.value = true
  try {
    const res = await getCostPlanDetail(planId.value)
    Object.assign(planDetail, res)
    items.value = res.items ?? []
    exclusions.value = res.exclusions ?? []
  } catch {
    message.error('获取方案详情失败')
  } finally {
    loading.value = false
  }
}

// ==================== 启用/停用 ====================
async function handleActivate() {
  try {
    await activateCostPlan(planId.value)
    message.success('启用成功')
    loadDetail()
  } catch { /* handled */ }
}

async function handleDeactivate() {
  try {
    await deactivateCostPlan(planId.value)
    message.success('已停用')
    loadDetail()
  } catch { /* handled */ }
}

// ==================== 成本项管理 ====================
const items = ref<CostPlanItemDto[]>([])
const itemsLoading = ref(false)
const excludableItems = computed(() => items.value.filter(i => i.itemType !== 4))

const itemTypeOptions = [
  { label: '全国单价', value: 1 },
  { label: '省份矩阵', value: 2 },
  { label: '城市加收', value: 3 },
  { label: '一口价', value: 4 },
]

// 结算重量环节枚举（与 CostItemToolbar 保持一致）。该字段是环节码而非 kg 数值，
// 结算重量环节：单一真源 constants.ts（避免与 CostItemToolbar 两处硬编码漂移）。
// 旧版误用 kg 数值输入框会写入非法环节码，污染计费取重维度
const weightStageOptions = WEIGHT_STAGE_OPTIONS

function getItemTypeText(t: number) {
  return itemTypeOptions.find(o => o.value === t)?.label ?? '未知'
}
function getItemTypeColor(t: number) {
  const map: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'purple', 4: 'orange' }
  return map[t] ?? 'default'
}

const itemColumns = [
  { title: '名称', dataIndex: 'itemName', width: 180 },
  { title: '类型', dataIndex: 'itemType', width: 100, align: 'center' as const },
  { title: '应用网点', dataIndex: 'outletIds', width: 100, align: 'center' as const },
  { title: '关联店铺', dataIndex: 'shopNames', width: 100, align: 'center' as const },
  { title: '时间段数', dataIndex: 'periodCount', width: 90, align: 'center' as const },
  { title: '排序', dataIndex: 'sortOrder', width: 70, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 130, align: 'center' as const },
]

// 成本项弹窗
const itemModalVisible = ref(false)
const itemModalLoading = ref(false)
const editingItemId = ref<number | null>(null)
const itemForm = reactive({
  itemName: '',
  itemType: 1 as number,
  sortOrder: 0,
  settlementWeightStage: undefined as number | undefined,
})

const itemModalTitle = computed(() => editingItemId.value ? '编辑成本项' : '新建成本项')

function handleEditItem(record: CostPlanItemDto) {
  router.push(`/express/cost-plan/${planId.value}/item/${record.id}`)
}

function openItemModal(record?: CostPlanItemDto) {
  if (record) {
    editingItemId.value = record.id
    itemForm.itemName = record.itemName
    itemForm.itemType = record.itemType
    itemForm.sortOrder = record.sortOrder
    itemForm.settlementWeightStage = record.settlementWeightStage
  } else {
    editingItemId.value = null
    itemForm.itemName = ''
    itemForm.itemType = 1
    itemForm.sortOrder = items.value.length
    itemForm.settlementWeightStage = undefined
  }
  itemModalVisible.value = true
}

async function handleItemModalOk() {
  if (!itemForm.itemName) {
    message.warning('请输入成本项名称')
    return
  }
  itemModalLoading.value = true
  try {
    const payload: any = {
      itemName: itemForm.itemName,
      itemType: itemForm.itemType,
      sortOrder: itemForm.sortOrder,
    }
    if (itemForm.itemType === 4) {
      payload.settlementWeightStage = itemForm.settlementWeightStage
    }
    if (editingItemId.value) {
      await updateCostPlanItem(planId.value, editingItemId.value, payload)
      message.success('更新成功')
    } else {
      await createCostPlanItem(planId.value, payload)
      message.success('创建成功')
    }
    itemModalVisible.value = false
    loadDetail()
  } catch {
    message.error(editingItemId.value ? '更新失败' : '创建失败')
  } finally {
    itemModalLoading.value = false
  }
}

async function handleDeleteItem(record: CostPlanItemDto) {
  try {
    await deleteCostPlanItem(planId.value, record.id)
    message.success('删除成功')
    loadDetail()
  } catch { /* handled */ }
}

// ==================== 互斥配置管理 ====================
const exclusions = ref<CostPlanExclusionDto[]>([])
const exclusionsLoading = ref(false)

const exclusionColumns = [
  { title: '生效日期', dataIndex: 'effectiveDate', width: 140 },
  { title: '互斥规则', dataIndex: 'exclusionRuleJson', ellipsis: true },
  { title: '操作', dataIndex: 'action', width: 130, align: 'center' as const },
]

function getExcludedItemNames(record: CostPlanExclusionDto): string[] {
  if (!record.exclusionRuleJson) return []
  try {
    const parsed = JSON.parse(record.exclusionRuleJson)
    const ids: number[] = parsed.excludedCostItemIds ?? []
    return ids.map(id => {
      const item = items.value.find(i => i.id === id)
      return item ? item.itemName : `#${id}`
    })
  } catch {
    return []
  }
}

// 互斥配置弹窗
const exclusionModalVisible = ref(false)
const exclusionModalLoading = ref(false)
const editingExclusionId = ref<number | null>(null)
const exclusionForm = reactive({
  effectiveDate: undefined as Dayjs | undefined,
  selectedItemIds: [] as number[],
  exclusionRuleJson: '',
})

const exclusionModalTitle = computed(() => editingExclusionId.value ? '编辑互斥配置' : '新建互斥配置')

function openExclusionModal(record?: CostPlanExclusionDto) {
  if (record) {
    editingExclusionId.value = record.id
    exclusionForm.effectiveDate = record.effectiveDate ? dayjs(record.effectiveDate) : undefined
    exclusionForm.exclusionRuleJson = record.exclusionRuleJson ?? ''
    // 解析已有JSON回填选中ID
    if (exclusionForm.exclusionRuleJson) {
      try {
        const parsed = JSON.parse(exclusionForm.exclusionRuleJson)
        exclusionForm.selectedItemIds = parsed.excludedCostItemIds ?? []
      } catch {
        exclusionForm.selectedItemIds = []
      }
    } else {
      exclusionForm.selectedItemIds = []
    }
  } else {
    editingExclusionId.value = null
    exclusionForm.effectiveDate = undefined
    exclusionForm.exclusionRuleJson = ''
    exclusionForm.selectedItemIds = []
  }
  exclusionModalVisible.value = true
}

async function handleExclusionModalOk() {
  if (!exclusionForm.effectiveDate) {
    message.warning('请选择生效日期')
    return
  }
  // 将选中的成本项ID转换为JSON字符串
  const ruleJson = exclusionForm.selectedItemIds.length > 0
    ? JSON.stringify({ excludedCostItemIds: exclusionForm.selectedItemIds })
    : ''
  exclusionModalLoading.value = true
  try {
    const payload: any = {
      effectiveDate: exclusionForm.effectiveDate.format('YYYY-MM-DD'),
      exclusionRuleJson: ruleJson || undefined,
    }
    if (editingExclusionId.value) {
      await updateCostPlanExclusion(planId.value, editingExclusionId.value, payload)
      message.success('更新成功')
    } else {
      await createCostPlanExclusion(planId.value, payload)
      message.success('创建成功')
    }
    exclusionModalVisible.value = false
    loadDetail()
  } catch {
    message.error(editingExclusionId.value ? '更新失败' : '创建失败')
  } finally {
    exclusionModalLoading.value = false
  }
}

async function handleDeleteExclusion(record: CostPlanExclusionDto) {
  try {
    await deleteCostPlanExclusion(planId.value, record.id)
    message.success('删除成功')
    loadDetail()
  } catch { /* handled */ }
}

// ==================== 初始化 ====================
onMounted(() => {
  loadDetail()
})
</script>

<style scoped lang="scss">
.cost-plan-detail-page {
  overflow-y: auto;
}

/* 解除全局 .page-container 对 a-spin 包裹层的 flex/overflow:hidden 锁定，
   本页为多卡片纵向流式布局，由 .page-container 统一滚动 */
:deep(.ant-spin-nested-loading),
:deep(.ant-spin-container) {
  flex: none;
  display: block;
  overflow: visible;
  min-height: 0;
}

.plan-info-card {
  margin-bottom: 2px;

  :deep(.ant-card-body) {
    padding: 16px 24px;
  }
}

.plan-info-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 6px;
}

.plan-title {
  font-size: 18px;
  font-weight: 600;
  margin: 0;
  color: rgba(0, 0, 0, 0.88);
}

.status-tag {
  font-size: 12px;
}

.plan-meta {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  gap: 4px;
}

.meta-dot {
  margin: 0 4px;
}

.count-badge {
  color: var(--color-info);
  font-weight: 500;
}

.sort-cell {
  color: rgba(0, 0, 0, 0.35);
  font-size: 12px;
}

// 斑马纹
:deep(.ant-table-tbody > tr:nth-child(even) > td) {
  background: #fafafa;
}

// Tab 指示条橙色
:deep(.ant-tabs-ink-bar) {
  background: var(--color-primary);
}

// Tab 选中文字橙色
:deep(.ant-tabs-tab-active .ant-tabs-tab-btn) {
  color: var(--color-primary) !important;
}

.exclusion-item-row {
  display: flex;
  align-items: center;
  padding: 4px 0;
  border-bottom: 1px solid #f0f0f0;

  &:last-child {
    border-bottom: none;
  }
}

.exclusion-item-name {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.85);
}

.exclusion-item-type-tag {
  margin-left: 6px;
  font-size: 11px;
}

.text-muted {
  color: rgba(0, 0, 0, 0.35);
}


</style>
