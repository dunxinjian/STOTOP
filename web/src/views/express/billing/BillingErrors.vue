<template>
  <div class="page-container">
    <PageHeader title="计费异常处理">
    </PageHeader>

    <!-- 统计概览 -->
    <a-row :gutter="16" style="margin-bottom: 16px;">
      <a-col :span="6">
        <a-card>
          <a-statistic title="异常类型数" :value="errorGroups.length" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="异常运单总数" :value="totalErrors" :value-style="{ color: totalErrors > 0 ? 'var(--color-danger)' : 'var(--color-success-text)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="影响店铺数" :value="totalShops" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="最近异常日期" :value="latestDate || '-'" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 加载状态 -->
    <a-spin :spinning="loading">
      <EmptyState v-if="!loading && errorGroups.length === 0" title="暂无异常数据，所有运单计费正常" />

      <!-- 异常分组卡片 -->
      <a-card
        v-for="group in errorGroups"
        :key="group.errorCode"
        style="margin-bottom: 12px;"
      >
        <template #title>
          <a-badge :count="group.waybillCount" :overflow-count="99999">
            <span style="font-size: 15px; font-weight: 500;">{{ group.errorName }}</span>
          </a-badge>
          <a-tag color="red" style="margin-left: 12px;">{{ group.errorCode }}</a-tag>
        </template>
        <template #extra>
          <a-space>
            <a-button type="link" @click="showDetail(group.errorCode, group.errorName)">查看明细</a-button>
            <a-popconfirm
              :title="`确认对「${group.errorName}」类型的 ${group.waybillCount} 条运单触发重算吗？`"
              @confirm="retryBilling(group.errorCode)"
            >
              <a-button type="primary" :loading="retryLoading[group.errorCode]">
                触发重算
              </a-button>
            </a-popconfirm>
          </a-space>
        </template>

        <a-descriptions :column="2" size="small">
          <a-descriptions-item label="影响运单数">
            <span style="color: var(--color-danger); font-weight: 500;">{{ group.waybillCount }}</span> 条
          </a-descriptions-item>
          <a-descriptions-item label="日期范围">
            {{ group.dateRange?.from || '-' }} ~ {{ group.dateRange?.to || '-' }}
          </a-descriptions-item>
          <a-descriptions-item label="影响店铺" :span="2">
            <template v-if="group.shopNames && group.shopNames.length > 0">
              <a-tag v-for="shop in group.shopNames.slice(0, 5)" :key="shop">{{ shop }}</a-tag>
              <a-tag v-if="group.shopNames.length > 5" color="blue">等{{ group.shopNames.length }}个店铺</a-tag>
            </template>
            <span v-else>-</span>
          </a-descriptions-item>
        </a-descriptions>
      </a-card>
    </a-spin>

    <!-- 明细弹窗 -->
    <a-modal
      v-model:open="detailVisible"
      :title="`异常运单明细 - ${currentErrorName}`"
      width="950px"
      :footer="null"
      @cancel="detailVisible = false"
    >
      <a-table
        :columns="detailColumns"
        :data-source="detailData"
        :pagination="detailPagination"
        :loading="detailLoading"
        row-key="id"
        size="middle"
        @change="onDetailPageChange"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getBillingErrors,
  getBillingErrorDetail,
  retryBilling as retryBillingApi,
} from '@/api/express'

// ==================== 异常分组数据 ====================

interface ErrorGroup {
  errorCode: string
  errorName: string
  waybillCount: number
  shopNames: string[]
  clientType?: string
  quotationId?: number
  quotationCode?: string
  stepIndex?: number
  dateRange: { from: string; to: string }
}

const loading = ref(false)
const refreshLoading = ref(false)
const errorGroups = ref<ErrorGroup[]>([])
const retryLoading = reactive<Record<string, boolean>>({})

const totalErrors = computed(() => errorGroups.value.reduce((sum, g) => sum + g.waybillCount, 0))
const totalShops = computed(() => {
  const shops = new Set<string>()
  errorGroups.value.forEach(g => g.shopNames?.forEach(s => shops.add(s)))
  return shops.size
})
const latestDate = computed(() => {
  let latest = ''
  errorGroups.value.forEach(g => {
    if (g.dateRange?.to && g.dateRange.to > latest) latest = g.dateRange.to
  })
  return latest
})

async function loadErrors() {
  loading.value = true
  refreshLoading.value = true
  try {
    const res = await getBillingErrors()
    errorGroups.value = (res as any) || []
  } catch (e: any) {
    message.error('加载异常统计失败：' + (e.message || '未知错误'))
  } finally {
    loading.value = false
    refreshLoading.value = false
  }
}

// ==================== 重算 ====================

async function retryBilling(errorCode: string) {
  retryLoading[errorCode] = true
  try {
    await retryBillingApi({ errorCode })
    message.success('重算任务已触发，请稍后刷新查看结果')
    // 延迟刷新统计数据
    setTimeout(() => loadErrors(), 2000)
  } catch (e: any) {
    message.error('触发重算失败：' + (e.message || '未知错误'))
  } finally {
    retryLoading[errorCode] = false
  }
}

// ==================== 明细弹窗 ====================

const detailVisible = ref(false)
const detailLoading = ref(false)
const detailData = ref<any[]>([])
const currentErrorCode = ref('')
const currentErrorName = ref('')
const detailPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const detailColumns = [
  { title: '运单号', dataIndex: 'waybillNo', key: 'waybillNo', width: 180 },
  { title: '店铺名称', dataIndex: 'shopName', key: 'shopName', width: 150 },
  { title: '品牌', dataIndex: 'brandName', key: 'brandName', width: 100 },
  { title: '业务对象类型', dataIndex: 'clientType', key: 'clientType', width: 100 },
  { title: '报价编号', dataIndex: 'quotationCode', key: 'quotationCode', width: 140 },
  { title: '步骤', dataIndex: 'stepIndex', key: 'stepIndex', width: 60 },
  { title: '运单日期', dataIndex: 'waybillDate', key: 'waybillDate', width: 120 },
  { title: '异常信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
]

async function showDetail(errorCode: string, errorName: string) {
  currentErrorCode.value = errorCode
  currentErrorName.value = errorName
  detailPagination.current = 1
  detailVisible.value = true
  await loadDetail()
}

async function loadDetail() {
  detailLoading.value = true
  try {
    const res = await getBillingErrorDetail({
      errorCode: currentErrorCode.value,
      page: detailPagination.current,
      pageSize: detailPagination.pageSize,
    })
    const data = res as any
    detailData.value = data?.items || data || []
    detailPagination.total = data?.total || detailData.value.length
  } catch (e: any) {
    message.error('加载明细失败：' + (e.message || '未知错误'))
  } finally {
    detailLoading.value = false
  }
}

function onDetailPageChange(pagination: any) {
  detailPagination.current = pagination.current
  detailPagination.pageSize = pagination.pageSize
  loadDetail()
}

// ==================== 初始化 ====================

onMounted(() => {
  loadErrors()
})
</script>

<style scoped>
.page-container {
  padding: 0;
}
</style>
