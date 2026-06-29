<template>
  <div class="page-container">
    <a-alert
      message="数据质量中心汇总了阻断计费流程的数据质量问题，解决后可重新计费。"
      type="info"
      show-icon
      style="margin: 8px 16px 0;"
    />

    <a-spin :spinning="loading">
      <a-row :gutter="8" class="kpi-row">
        <a-col :span="4">
          <div class="kpi-card" @click="goto('pending-shops')">
            <span class="kpi-value" :style="{ color: overview.autoCreatedPendingCount > 0 ? 'var(--color-danger)' : 'var(--color-success-text)' }">{{ overview.autoCreatedPendingCount }}</span>
            <span class="kpi-suffix">个</span>
            <div class="kpi-title">待配置店铺（自动建档）</div>
          </div>
        </a-col>
        <a-col :span="4">
          <div class="kpi-card" @click="goto('pending-shops')">
            <span class="kpi-value">{{ overview.pendingShopCount }}</span>
            <span class="kpi-suffix">个</span>
            <div class="kpi-title">待配置店铺总数</div>
          </div>
        </a-col>
        <a-col :span="4">
          <div class="kpi-card" @click="goto('empty-shop-rows')">
            <span class="kpi-value" :style="{ color: overview.emptyShopRowCount > 0 ? 'var(--color-danger)' : 'var(--color-success-text)' }">{{ overview.emptyShopRowCount }}</span>
            <span class="kpi-suffix">条</span>
            <div class="kpi-title">空账号运单</div>
          </div>
        </a-col>
        <a-col :span="4">
          <div class="kpi-card" @click="goto('unrecognized')">
            <span class="kpi-value" :style="{ color: (overview.unrecognizedNetworkPointCount || 0) > 0 ? 'var(--color-danger)' : 'var(--color-success-text)' }">{{ overview.unrecognizedNetworkPointCount || 0 }}</span>
            <span class="kpi-suffix">条</span>
            <div class="kpi-title">未识别网点运单</div>
          </div>
        </a-col>
        <a-col :span="4">
          <div class="kpi-card" @click="goto('mismatch')">
            <span class="kpi-value" :style="{ color: (overview.networkPointMismatchCount || 0) > 0 ? 'var(--color-warning)' : 'var(--color-success-text)' }">{{ overview.networkPointMismatchCount || 0 }}</span>
            <span class="kpi-suffix">条</span>
            <div class="kpi-title">网点不一致</div>
          </div>
        </a-col>
        <a-col :span="4">
          <div class="kpi-card">
            <span class="kpi-value" :style="{ color: overview.affectedBatchCount > 0 ? 'var(--color-warning)' : 'var(--color-success-text)' }">{{ overview.affectedBatchCount }}</span>
            <span class="kpi-suffix">个</span>
            <div class="kpi-title">
              受阻批次
              <template v-if="overview.blockedBatchIds && overview.blockedBatchIds.length">
                <span class="kpi-tip">ID: {{ overview.blockedBatchIds.slice(0, 3).join(', ') }}<span v-if="overview.blockedBatchIds.length > 3"> 等</span></span>
              </template>
            </div>
          </div>
        </a-col>
      </a-row>
    </a-spin>

    <!-- Tab 切换各类问题详情 -->
    <a-card class="detail-card">
      <a-tabs v-model:activeKey="activeTab" class="detail-tabs">
        <template #tabBarExtraContent>
          <div class="tab-extra">
            <!-- pending-shops 筛选项 -->
            <template v-if="activeTab === 'pending-shops'">
              <a-input v-model:value="pendingQuery.keyword" size="small" placeholder="店铺名称 / 编码" allow-clear style="width: 180px;" />
              <a-input-number v-model:value="pendingQuery.batchId" size="small" placeholder="批次ID" :min="1" style="width: 110px;" />
              <a-select v-model:value="(pendingQuery as any).isAutoCreated" size="small" placeholder="建档方式" allow-clear style="width: 110px;">
                <a-select-option :value="true">仅自动建档</a-select-option>
                <a-select-option :value="false">仅手工建档</a-select-option>
              </a-select>
            </template>
            <!-- empty-shop-rows 筛选项 -->
            <template v-else-if="activeTab === 'empty-shop-rows'">
              <a-input-number v-model:value="emptyQuery.batchId" size="small" placeholder="批次ID" :min="1" style="width: 110px;" />
              <a-select v-model:value="emptyQuery.dispatchStatus" size="small" placeholder="派发状态" allow-clear style="width: 120px;">
                <a-select-option value="Pending">Pending</a-select-option>
                <a-select-option value="Ignored">Ignored</a-select-option>
                <a-select-option value="Resolved">Resolved</a-select-option>
              </a-select>
            </template>
            <!-- unrecognized 筛选项 -->
            <template v-else-if="activeTab === 'unrecognized'">
              <a-input-number v-model:value="unrecognizedQuery.batchId" size="small" placeholder="批次ID" :min="1" style="width: 110px;" />
              <a-input v-model:value="unrecognizedQuery.keyword" size="small" placeholder="搜索网点名称" allow-clear style="width: 160px;" />
            </template>
            <!-- mismatch 筛选项 -->
            <template v-else-if="activeTab === 'mismatch'">
              <a-input-number v-model:value="mismatchQuery.batchId" size="small" placeholder="批次ID" :min="1" style="width: 110px;" />
              <a-input v-model:value="mismatchQuery.waybillNo" size="small" placeholder="搜索运单编号" allow-clear style="width: 160px;" />
            </template>
            <a-button type="primary" size="small" @click="onChildSearch">查询</a-button>
            <a-button size="small" @click="onChildReset">重置</a-button>
            <a-divider type="vertical" style="height: 20px; margin: 0;" />
            <a class="flow-link" @click="showFlowDrawer = true"><AuditOutlined /> 处理流程</a>
          </div>
        </template>
        <a-tab-pane key="pending-shops" tab="待配置店铺">
          <PendingShops ref="pendingShopsRef" :embedded="true" :initial-query="pendingQuery" />
        </a-tab-pane>
        <a-tab-pane key="empty-shop-rows" tab="空账号运单">
          <EmptyShopRows ref="emptyShopRowsRef" :embedded="true" :initial-query="emptyQuery" />
        </a-tab-pane>
        <a-tab-pane key="unrecognized" tab="未识别网点">
          <UnrecognizedNetworkPoints ref="unrecognizedRef" :embedded="true" :initial-query="unrecognizedQuery" />
        </a-tab-pane>
        <a-tab-pane key="mismatch" tab="网点不一致">
          <NetworkPointMismatch ref="mismatchRef" :embedded="true" :initial-query="mismatchQuery" />
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 处理流程右侧浮窗 -->
    <a-drawer
      v-model:open="showFlowDrawer"
      title="处理流程"
      placement="right"
      :width="360"
      :body-style="{ paddingTop: '8px' }"
    >
      <a-steps :current="-1" direction="vertical" size="small">
        <a-step title="1. 检查空账号运单" description="识别 F店铺账号 为空的运单，选择「补填店铺账号」或「忽略」。" />
        <a-step title="2. 配置待配置店铺" description="为自动建档的店铺指定归属客户、可选报价方案，系统会补写归属并启用店铺。" />
        <a-step title="3. 重新计费" description="在对应批次上点击「重新计费」，Agent 会重新跑完整 Pipeline。" />
      </a-steps>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { AuditOutlined } from '@ant-design/icons-vue'
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { getQualityCenterOverview, type QualityCenterOverviewDto } from '@/api/qualityCenter'
import PendingShops from './PendingShops.vue'
import EmptyShopRows from './EmptyShopRows.vue'
import UnrecognizedNetworkPoints from './UnrecognizedNetworkPoints.vue'
import NetworkPointMismatch from './NetworkPointMismatch.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const activeTab = ref<string>((route.query.tab as string) || 'pending-shops')
const showFlowDrawer = ref(false)

// ===== 各Tab筛选状态（提升到父组件，不依赖ref挂载）=====
const pendingQuery = reactive({ keyword: undefined as string | undefined, batchId: undefined as number | undefined, isAutoCreated: true as boolean | undefined })
const emptyQuery = reactive({ batchId: undefined as number | undefined, dispatchStatus: undefined as string | undefined })
const unrecognizedQuery = reactive({ batchId: undefined as number | undefined, keyword: undefined as string | undefined })
const mismatchQuery = reactive({ batchId: undefined as number | undefined, waybillNo: undefined as string | undefined })

// 子组件引用
const pendingShopsRef = ref<InstanceType<typeof PendingShops> | null>(null)
const emptyShopRowsRef = ref<InstanceType<typeof EmptyShopRows> | null>(null)
const unrecognizedRef = ref<InstanceType<typeof UnrecognizedNetworkPoints> | null>(null)
const mismatchRef = ref<InstanceType<typeof NetworkPointMismatch> | null>(null)

function getActiveChild() {
  switch (activeTab.value) {
    case 'pending-shops': return pendingShopsRef.value
    case 'empty-shop-rows': return emptyShopRowsRef.value
    case 'unrecognized': return unrecognizedRef.value
    case 'mismatch': return mismatchRef.value
    default: return null
  }
}

function onChildSearch() {
  const child = getActiveChild() as any
  switch (activeTab.value) {
    case 'pending-shops': child?.onSearch?.(pendingQuery); break
    case 'empty-shop-rows': child?.onSearch?.(emptyQuery); break
    case 'unrecognized': child?.onSearch?.(unrecognizedQuery); break
    case 'mismatch': child?.onSearch?.(mismatchQuery); break
  }
}

function onChildReset() {
  switch (activeTab.value) {
    case 'pending-shops':
      pendingQuery.keyword = undefined; pendingQuery.batchId = undefined; pendingQuery.isAutoCreated = undefined
      break
    case 'empty-shop-rows':
      emptyQuery.batchId = undefined; emptyQuery.dispatchStatus = undefined
      break
    case 'unrecognized':
      unrecognizedQuery.batchId = undefined; unrecognizedQuery.keyword = undefined
      break
    case 'mismatch':
      mismatchQuery.batchId = undefined; mismatchQuery.waybillNo = undefined
      break
  }
  const child = getActiveChild() as any
  child?.onReset?.()
}



const overview = reactive<QualityCenterOverviewDto>({
  pendingShopCount: 0,
  autoCreatedPendingCount: 0,
  emptyShopRowCount: 0,
  unrecognizedNetworkPointCount: 0,
  networkPointMismatchCount: 0,
  affectedBatchCount: 0,
  blockedBatchIds: [],
})

async function loadOverview() {
  loading.value = true
  try {
    const res = (await getQualityCenterOverview()) as any
    Object.assign(overview, res || {})
  } catch (e: any) {
    message.error('加载质量中心概览失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function goto(tab: string) {
  activeTab.value = tab
}

onMounted(() => {
  loadOverview()
})
</script>

<style scoped lang="scss">
.page-container {
  padding: 0;
  flex: 0 0 auto !important;
  overflow: visible !important;
  min-height: 0 !important;

  :deep(> .ant-card),
  :deep(> .detail-card) {
    flex: none !important;
    overflow: visible !important;
    min-height: auto !important;

    .ant-card-body {
      flex: none !important;
      overflow: visible !important;
      min-height: auto !important;
    }
  }

  :deep(.ant-spin-nested-loading),
  :deep(.ant-spin-container) {
    flex: none !important;
    overflow: visible !important;
    min-height: auto !important;
  }

  :deep(.ant-table-wrapper),
  :deep(.ant-table-container),
  :deep(.ant-table-body) {
    flex: none !important;
    overflow: visible !important;
    min-height: auto !important;
  }
}

// ===== KPI 卡片极致压缩 =====
.kpi-row {
  padding: 8px 16px 0;
}

.kpi-card {
  background: #fff;
  border-radius: 6px;
  padding: 8px 12px;
  cursor: pointer;
  transition: box-shadow 0.2s;
  border: 1px solid #f0f0f0;

  &:hover {
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.09);
  }
}

.kpi-value {
  font-size: 22px;
  font-weight: 600;
  line-height: 1.2;
  font-variant-numeric: tabular-nums;
}

.kpi-suffix {
  font-size: 12px;
  color: #999;
  margin-left: 2px;
}

.kpi-title {
  margin-top: 2px;
  font-size: 12px;
  color: #666;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.kpi-tip {
  color: #999;
  font-size: 11px;
}

// ===== 详情卡片 =====
.detail-card {
  margin: 8px 16px 16px;
  border-radius: 6px;

  // Tab直接展示时卡片body去除多余内边
  :deep(.ant-card-body) {
    padding: 0;
  }
}

.detail-tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: 0;
    padding: 0 12px;
  }

  :deep(.ant-tabs-tab) {
    padding: 10px 0;
    font-size: 13px;
  }

  :deep(.ant-tabs-content-holder) {
    padding: 12px;
  }
}

// ===== 处理流程链接 =====
.tab-extra {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-right: 4px;
}

.flow-link {
  font-size: 13px;
  color: var(--text-1);
  cursor: pointer;
  white-space: nowrap;

  &:hover {
    color: var(--color-primary);
  }
}
</style>
