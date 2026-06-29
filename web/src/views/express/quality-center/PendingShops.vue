<template>
  <div :class="{ 'page-container': !embedded }">
    <PageHeader v-if="!embedded" title="待配置店铺">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="query.keyword" size="small" placeholder="店铺名称 / 编码" allow-clear style="width: 220px;" />
          <a-input-number v-model:value="query.batchId" size="small" placeholder="批次ID" :min="1" style="width: 150px;" />
          <a-select v-model:value="isAutoCreatedProxy" size="small" placeholder="自动建档" allow-clear style="width: 120px;">
            <a-select-option :value="1">仅自动建档</a-select-option>
            <a-select-option :value="0">仅手工建档</a-select-option>
          </a-select>
          <a-button type="primary" size="small" @click="onSearch">查询</a-button>
          <a-button size="small" @click="onReset">重置</a-button>
        </div>
      </template>
    </PageHeader>
    <div v-else style="display: none;"></div>

    <a-alert
      message="以下店铺由快递价格自动计算插件自动建档，请为其指定归属客户和报价方案后，再重新计费。"
      type="warning"
      show-icon
      style="margin-bottom: 12px;"
    />

    <a-table
      :columns="columns"
      :data-source="list"
      :pagination="pagination"
      :loading="loading"
      row-key="name"
      size="small"
      bordered
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'name'">
          <a-space>
            <span style="font-weight: 500;">{{ record.name }}</span>
            <a-tag v-if="record.isAutoCreated" color="orange">自动建档</a-tag>
            <a-tag v-if="record.needsAssignment" color="red">待归属</a-tag>
            <a-tag v-if="record.status === 0" color="default">未启用</a-tag>
          </a-space>
        </template>
        <template v-else-if="column.key === 'createdTime'">
          {{ record.createdTime ? dayjs(record.createdTime).format('YYYY-MM-DD HH:mm') : '-' }}
        </template>
        <template v-else-if="column.key === 'affectedWaybillCount'">
          <span
            v-if="record.affectedWaybillCount > 0"
            style="color: var(--color-danger); font-weight: 600; cursor: pointer; text-decoration: underline;"
          >{{ record.affectedWaybillCount }}</span>
          <span v-else style="color: rgba(0,0,0,0.25);">0</span>
        </template>
        <template v-else-if="column.key === 'affectedBatchIds'">
          <template v-if="record.affectedBatchIds && record.affectedBatchIds.length">
            <a-tag v-for="bid in record.affectedBatchIds.slice(0, 3)" :key="bid" color="blue">{{ bid }}</a-tag>
            <a-tag v-if="record.affectedBatchIds.length > 3">+{{ record.affectedBatchIds.length - 3 }}</a-tag>
          </template>
          <span v-else style="color: rgba(0,0,0,0.25);">无</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-button type="primary" size="small" @click="openCompleteDialog(record as PendingShopItemDto)">完成配置</a-button>
        </template>
      </template>
    </a-table>

    <!-- 完成配置 弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="`完成配置 - ${currentShop?.name || ''}`"
      :confirm-loading="submitting"
      width="600px"
      @ok="submitComplete"
    >
      <a-form :model="formData" :rules="formRules" ref="formRef" layout="vertical">
        <a-form-item label="归属客户" name="clientId" required>
          <a-select
            v-model:value="formData.clientId"
            placeholder="请选择归属客户"
            :options="clientOptions"
            :loading="clientLoading"
            show-search
            :filter-option="filterClient"
            allowClear
            @change="onClientChange"
          />
        </a-form-item>
        <a-form-item label="报价方案" name="pricePlanId">
          <a-select
            v-model:value="formData.pricePlanId"
            placeholder="请选择报价方案"
            :options="pricePlanOptions"
            :loading="pricePlanLoading"
            show-search
            :filter-option="filterPricePlan"
            allowClear
          />
        </a-form-item>
        <a-form-item label="到期日期" name="expiryDate">
          <a-date-picker v-model:value="formData.expiryDate" value-format="YYYY-MM-DD" style="width: 100%;" />
        </a-form-item>
        <a-form-item label="归属备注" name="assignmentRemark">
          <a-input v-model:value="formData.assignmentRemark" placeholder="选填" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="formData.skipPricePlanCheck">
            跳过报价方案检查（不推荐）
          </a-checkbox>
        </a-form-item>
      </a-form>
      <a-alert
        v-if="currentShop && currentShop.affectedWaybillCount > 0"
        :message="`该店铺影响 ${currentShop.affectedWaybillCount} 条运单，完成配置后请前往批次 ${(currentShop.affectedBatchIds || []).join(', ') || '-'} 重新计费。`"
        type="info"
        show-icon
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import dayjs from 'dayjs'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPendingShops,
  completeShopConfig,
  type PendingShopItemDto,
  type PendingShopQueryRequest,
  type CompleteShopConfigRequest,
} from '@/api/qualityCenter'
import { getQuotationList } from '@/api/express'
import { getAllEnabledCustomers } from '@/api/crm'

const props = withDefaults(defineProps<{ embedded?: boolean; initialQuery?: Record<string, any> }>(), { embedded: false })

// ==================== 列定义 ====================
const columns = [
  { title: '店铺名称', key: 'name', width: 300 },
  { title: '平台', dataIndex: 'platform', key: 'platform', width: 100 },
  { title: '影响运单数', key: 'affectedWaybillCount', width: 110 },
  { title: '受影响批次', key: 'affectedBatchIds', width: 200 },
  { title: '建档时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
  { title: '操作', key: 'action', width: 120, fixed: 'right' as const },
]

// ==================== 查询/分页 ====================
const loading = ref(false)
const list = ref<PendingShopItemDto[]>([])
const query = reactive<PendingShopQueryRequest>({ pageIndex: 1, pageSize: 20, keyword: undefined, batchId: undefined, isAutoCreated: true })
// AntD SelectValue 不接受裸 boolean（RawValue 仅 string|number），用 1/0 代理映射 true/false
const isAutoCreatedProxy = computed<number | undefined>({
  get: () => query.isAutoCreated == null ? undefined : (query.isAutoCreated ? 1 : 0),
  set: (val) => { query.isAutoCreated = val == null ? undefined : val === 1 },
})
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function loadList() {
  loading.value = true
  try {
    query.pageIndex = pagination.current
    query.pageSize = pagination.pageSize
    const res = (await getPendingShops(query)) as any
    list.value = res?.items || []
    pagination.total = res?.total || 0
  } catch (e: any) {
    message.error('加载待配置店铺失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function onSearch(external?: Record<string, any>) {
  if (external) {
    if ('keyword' in external) query.keyword = external.keyword
    if ('batchId' in external) query.batchId = external.batchId
    if ('isAutoCreated' in external) query.isAutoCreated = external.isAutoCreated
  }
  pagination.current = 1
  loadList()
}

function onReset() {
  query.keyword = undefined
  query.batchId = undefined
  query.isAutoCreated = true
  pagination.current = 1
  loadList()
}

function onTableChange(pg: any) {
  pagination.current = pg.current
  pagination.pageSize = pg.pageSize
  loadList()
}

// ==================== 完成配置弹窗 ====================
const dialogVisible = ref(false)
const submitting = ref(false)
const currentShop = ref<PendingShopItemDto | null>(null)
const formRef = ref<FormInstance>()
const formData = reactive<CompleteShopConfigRequest>({
  shopName: '',
  clientId: undefined,
  pricePlanId: undefined,
  expiryDate: undefined,
  assignmentRemark: undefined,
  skipPricePlanCheck: false,
})
const formRules: Record<string, Rule[]> = {
  clientId: [{ required: true, message: '请选择归属客户', trigger: 'change' }],
}

// 客户下拉
const clientOptions = ref<{ label: string; value: string }[]>([])
const clientLoading = ref(false)

// 报价方案下拉
const pricePlanOptions = ref<{ label: string; value: number }[]>([])
const pricePlanLoading = ref(false)

async function loadClients() {
  clientLoading.value = true
  try {
    const res = (await getAllEnabledCustomers()) as any
    const items = Array.isArray(res) ? res : (res?.items || [])
    clientOptions.value = items.map((item: any) => ({
      label: item.name,
      value: item.id,
    }))
  } catch (e: any) {
    message.error('加载客户列表失败：' + (e?.message || '未知错误'))
  } finally {
    clientLoading.value = false
  }
}

async function loadQuotations(clientId?: string) {
  pricePlanLoading.value = true
  try {
    const params: any = { pageIndex: 1, pageSize: 500, status: 1 } // status: 1=生效
    if (clientId) params.clientId = clientId
    const res = (await getQuotationList(params)) as any
    const items = res?.items || res || []
    pricePlanOptions.value = items.map((p: any) => ({
      label: p.planName,
      value: p.id,
    }))
  } catch (e: any) {
    message.error('加载报价方案失败：' + (e?.message || '未知错误'))
  } finally {
    pricePlanLoading.value = false
  }
}

function filterClient(input: string, option: any) {
  return option.label?.toLowerCase().includes(input.toLowerCase())
}

function filterPricePlan(input: string, option: any) {
  return option.label?.toLowerCase().includes(input.toLowerCase())
}

function onClientChange(val: any) {
  // 切换客户时联动刷新报价方案
  const clientId = val as string | undefined
  formData.pricePlanId = undefined
  pricePlanOptions.value = []
  if (clientId) {
    loadQuotations(clientId)
  }
}

function openCompleteDialog(shop: PendingShopItemDto) {
  currentShop.value = shop
  formData.shopName = shop.name
  formData.clientId = undefined
  formData.pricePlanId = undefined
  formData.expiryDate = undefined
  formData.assignmentRemark = undefined
  formData.skipPricePlanCheck = false
  dialogVisible.value = true
  pricePlanOptions.value = []
  if (!clientOptions.value.length) loadClients()
}

async function submitComplete() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const res = (await completeShopConfig(formData)) as any
    if (res?.pricePlanWarning) {
      message.warning(res.pricePlanWarning, 5)
    } else {
      message.success('店铺配置已完成')
    }
    dialogVisible.value = false
    loadList()
  } catch (e: any) {
    message.error('完成配置失败：' + (e?.message || '未知错误'))
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  loadList()
})

defineExpose({ onSearch, onReset, query })
</script>

<style scoped>
.page-container { padding: 0; }
</style>
