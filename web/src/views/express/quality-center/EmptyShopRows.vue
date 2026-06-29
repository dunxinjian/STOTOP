<template>
  <div :class="{ 'page-container': !embedded }">
    <PageHeader v-if="!embedded" title="空店铺账号运单">
      <template #actions>
        <a-button type="primary" :disabled="!selectedRowKeys.length" @click="openFillModal">
          批量补填店铺账号
        </a-button>
        <a-button :disabled="!selectedRowKeys.length" @click="openIgnoreModal">
          批量忽略
        </a-button>
        <span v-if="selectedRowKeys.length" style="color: #999; font-size: 13px;">
          已选 {{ selectedRowKeys.length }} 条
        </span>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input-number v-model:value="query.batchId" size="small" placeholder="批次ID" :min="1" style="width: 150px;" />
          <a-select v-model:value="query.dispatchStatus" size="small" placeholder="派发状态" allow-clear style="width: 140px;">
            <a-select-option value="Pending">Pending</a-select-option>
            <a-select-option value="Ignored">Ignored</a-select-option>
            <a-select-option value="Resolved">Resolved</a-select-option>
          </a-select>
          <a-button type="primary" size="small" @click="onSearch">查询</a-button>
          <a-button size="small" @click="onReset">重置</a-button>
        </div>
      </template>
    </PageHeader>
    <div v-else style="margin-bottom: 12px;">
      <a-button type="primary" size="small" :disabled="!selectedRowKeys.length" @click="openFillModal">批量补填店铺账号</a-button>
      <a-button size="small" :disabled="!selectedRowKeys.length" style="margin-left:8px" @click="openIgnoreModal">批量忽略</a-button>
      <span v-if="selectedRowKeys.length" style="color: #999; font-size: 13px; margin-left:8px">已选 {{ selectedRowKeys.length }} 条</span>
    </div>

    <a-alert
      message="以下运单的店铺账号为空，请选择「批量补填店铺账号」或「批量忽略」后，再对相关批次重新计费。"
      type="warning"
      show-icon
      style="margin-bottom: 16px;"
    />

    <a-table
      :columns="columns"
      :data-source="list"
      :pagination="pagination"
      :loading="loading"
      :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
      row-key="errorId"
      size="middle"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'dispatchStatus'">
          <a-tag v-if="record.dispatchStatus === 'Pending'" color="orange">Pending</a-tag>
          <a-tag v-else-if="record.dispatchStatus === 'Ignored'" color="default">Ignored</a-tag>
          <a-tag v-else-if="record.dispatchStatus === 'Resolved'" color="green">Resolved</a-tag>
          <span v-else>{{ record.dispatchStatus || '-' }}</span>
        </template>
        <template v-else-if="column.key === 'errorMessage'">
          <a-tooltip v-if="record.errorMessage && record.errorMessage.length > 40" :title="record.errorMessage">
            {{ record.errorMessage.slice(0, 40) }}...
          </a-tooltip>
          <span v-else>{{ record.errorMessage || '-' }}</span>
        </template>
      </template>
    </a-table>

    <!-- 批量补填店铺账号 弹窗 -->
    <a-modal
      v-model:open="fillModalVisible"
      title="批量补填店铺账号"
      :confirm-loading="submitting"
      @ok="submitFill"
    >
      <a-form :model="fillForm" ref="fillFormRef" layout="vertical">
        <a-form-item label="店铺账号" name="shopAccount" :rules="[{ required: true, message: '请输入店铺账号' }]">
          <a-input v-model:value="fillForm.shopAccount" placeholder="请输入店铺账号" />
        </a-form-item>
      </a-form>
      <a-alert :message="`将为已选的 ${selectedRowKeys.length} 条运单补填店铺账号`" type="info" show-icon />
    </a-modal>

    <!-- 批量忽略 弹窗 -->
    <a-modal
      v-model:open="ignoreModalVisible"
      title="批量忽略"
      :confirm-loading="submitting"
      @ok="submitIgnore"
    >
      <a-form :model="ignoreForm" layout="vertical">
        <a-form-item label="忽略原因（可选）">
          <a-input v-model:value="ignoreForm.reason" placeholder="选填忽略原因" />
        </a-form-item>
      </a-form>
      <a-alert :message="`将忽略已选的 ${selectedRowKeys.length} 条运单`" type="warning" show-icon />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import PageHeader from '@/components/PageHeader.vue'
import {
  getEmptyShopRows,
  fillEmptyShopAccount,
  ignoreEmptyShopRows,
  type EmptyShopRowItemDto,
  type EmptyShopRowQueryRequest,
} from '@/api/qualityCenter'

const props = withDefaults(defineProps<{ embedded?: boolean; initialQuery?: Record<string, any> }>(), { embedded: false })

// ==================== 列定义 ====================
const columns = [
  { title: '错误ID', dataIndex: 'errorId', key: 'errorId', width: 90 },
  { title: '批次ID', dataIndex: 'batchId', key: 'batchId', width: 90 },
  { title: '运单编号', dataIndex: 'waybillNo', key: 'waybillNo', width: 160 },
  { title: '业务日期', dataIndex: 'waybillDate', key: 'waybillDate', width: 120 },
  { title: '错误信息', key: 'errorMessage', width: 280 },
  { title: '派发状态', key: 'dispatchStatus', width: 110 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
]

// ==================== 查询/分页 ====================
const loading = ref(false)
const list = ref<EmptyShopRowItemDto[]>([])
const query = reactive<EmptyShopRowQueryRequest>({ pageIndex: 1, pageSize: 20, batchId: undefined, dispatchStatus: undefined })
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
    const res = (await getEmptyShopRows(query)) as any
    list.value = res?.items || []
    pagination.total = res?.total || 0
  } catch (e: any) {
    message.error('加载空店铺账号运单失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function onSearch(external?: Record<string, any>) {
  if (external) {
    if ('batchId' in external) query.batchId = external.batchId
    if ('dispatchStatus' in external) query.dispatchStatus = external.dispatchStatus
  }
  pagination.current = 1
  loadList()
}

function onReset() {
  query.batchId = undefined
  query.dispatchStatus = undefined
  pagination.current = 1
  loadList()
}

function onTableChange(pg: any) {
  pagination.current = pg.current
  pagination.pageSize = pg.pageSize
  loadList()
}

// ==================== 行选择 ====================
const selectedRowKeys = ref<number[]>([])

function onSelectChange(keys: Key[]) {
  selectedRowKeys.value = keys.map(Number)
}

// ==================== 批量补填弹窗 ====================
const fillModalVisible = ref(false)
const fillFormRef = ref<FormInstance>()
const fillForm = reactive({ shopAccount: '' })

function openFillModal() {
  fillForm.shopAccount = ''
  fillModalVisible.value = true
}

async function submitFill() {
  try {
    await fillFormRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const res = (await fillEmptyShopAccount({
      errorIds: selectedRowKeys.value,
      shopAccount: fillForm.shopAccount,
    })) as any
    message.success(res?.message || `成功补填 ${res?.affectedCount || 0} 条`)
    fillModalVisible.value = false
    selectedRowKeys.value = []
    loadList()
  } catch (e: any) {
    message.error('补填失败：' + (e?.message || '未知错误'))
  } finally {
    submitting.value = false
  }
}

// ==================== 批量忽略弹窗 ====================
const ignoreModalVisible = ref(false)
const ignoreForm = reactive({ reason: '' })
const submitting = ref(false)

function openIgnoreModal() {
  ignoreForm.reason = ''
  ignoreModalVisible.value = true
}

async function submitIgnore() {
  submitting.value = true
  try {
    const res = (await ignoreEmptyShopRows({
      errorIds: selectedRowKeys.value,
      reason: ignoreForm.reason || undefined,
    })) as any
    message.success(res?.message || `成功忽略 ${res?.affectedCount || 0} 条`)
    ignoreModalVisible.value = false
    selectedRowKeys.value = []
    loadList()
  } catch (e: any) {
    message.error('忽略失败：' + (e?.message || '未知错误'))
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
