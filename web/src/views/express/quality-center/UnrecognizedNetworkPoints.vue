<template>
  <div :class="{ 'page-container': !embedded }">
    <PageHeader v-if="!embedded" title="未识别网点">
      <template #actions>
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
          <a-input v-model:value="query.keyword" size="small" placeholder="搜索网点名称" allow-clear style="width: 180px;" />
          <a-button type="primary" size="small" @click="onSearch">查询</a-button>
          <a-button size="small" @click="onReset">重置</a-button>
        </div>
      </template>
    </PageHeader>
    <div v-else style="margin-bottom: 12px;">
      <a-button size="small" :disabled="!selectedRowKeys.length" @click="openIgnoreModal">批量忽略</a-button>
      <span v-if="selectedRowKeys.length" style="color: #999; font-size: 13px; margin-left:8px">已选 {{ selectedRowKeys.length }} 条</span>
    </div>

    <a-alert
      message="以下运单的网点名称无法在系统中识别。请「关联网点」将名称映射到已有网点，或「批量忽略」后重新计费。"
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
        <template v-else-if="column.key === 'action'">
          <a-button type="link" size="small" @click="openAssociateModal(record as UnrecognizedNetworkPointItemDto)">关联网点</a-button>
        </template>
      </template>
    </a-table>

    <!-- 关联网点弹窗 -->
    <a-modal
      v-model:open="associateModalVisible"
      title="关联网点"
      :confirm-loading="submitting"
      @ok="submitAssociate"
      width="520px"
    >
      <a-form layout="vertical">
        <a-form-item label="原始网点名称">
          <a-input :value="associateForm.networkPointName" disabled />
        </a-form-item>
        <a-form-item label="目标网点" :required="true">
          <a-select
            v-model:value="associateForm.networkPointCode"
            show-search
            placeholder="搜索并选择网点"
            :filter-option="false"
            :not-found-content="npSearching ? undefined : '无匹配网点'"
            @search="onNetworkPointSearch"
            style="width: 100%;"
          >
            <template #notFoundContent v-if="npSearching">
              <a-spin size="small" />
            </template>
            <a-select-option v-for="np in networkPointOptions" :key="np.code" :value="np.code">
              {{ np.code }} - {{ np.orgName || '' }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="dashed" @click="openCreateNpModal">+ 新增网点</a-button>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 新增网点弹窗 -->
    <a-modal
      v-model:open="createNpModalVisible"
      title="新增网点"
      :confirm-loading="creatingNp"
      @ok="submitCreateNp"
      width="420px"
    >
      <a-form layout="vertical" :model="createNpForm">
        <a-form-item label="网点编号" :required="true">
          <a-input v-model:value="createNpForm.code" placeholder="请输入网点编号" />
        </a-form-item>
        <a-form-item label="网点名称">
          <a-input v-model:value="createNpForm.name" placeholder="请输入网点名称（可选）" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 批量忽略弹窗 -->
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
      <a-alert :message="`将忽略已选的 ${selectedRowKeys.length} 条记录`" type="warning" show-icon />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import PageHeader from '@/components/PageHeader.vue'
import {
  getUnrecognizedNetworkPoints,
  associateNetworkPoint,
  ignoreNetworkPointErrors,
  type UnrecognizedNetworkPointItemDto,
  type UnrecognizedNetworkPointQueryRequest,
} from '@/api/qualityCenter'
import { getNetworkPointList, createNetworkPoint } from '@/api/express'

const props = withDefaults(defineProps<{ embedded?: boolean; initialQuery?: Record<string, any> }>(), { embedded: false })

// ==================== 列定义 ====================
const columns = [
  { title: '错误ID', dataIndex: 'errorId', key: 'errorId', width: 90 },
  { title: '批次ID', dataIndex: 'batchId', key: 'batchId', width: 90 },
  { title: '原始网点名称', dataIndex: 'networkPointName', key: 'networkPointName', width: 180 },
  { title: '运单编号', dataIndex: 'waybillNo', key: 'waybillNo', width: 160 },
  { title: '状态', key: 'dispatchStatus', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', key: 'action', width: 100, fixed: 'right' as const },
]

// ==================== 查询/分页 ====================
const loading = ref(false)
const list = ref<UnrecognizedNetworkPointItemDto[]>([])
const query = reactive<UnrecognizedNetworkPointQueryRequest>({ pageIndex: 1, pageSize: 20, batchId: undefined, keyword: undefined })
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
    const res = (await getUnrecognizedNetworkPoints(query)) as any
    list.value = res?.items || []
    pagination.total = res?.total || 0
  } catch (e: any) {
    message.error('加载未识别网点失败：' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function onSearch(external?: Record<string, any>) {
  if (external) {
    if ('batchId' in external) query.batchId = external.batchId
    if ('keyword' in external) query.keyword = external.keyword
  }
  pagination.current = 1
  loadList()
}

function onReset() {
  query.batchId = undefined
  query.keyword = undefined
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

// ==================== 关联网点弹窗 ====================
const associateModalVisible = ref(false)
const submitting = ref(false)
const associateForm = reactive({
  networkPointName: '',
  networkPointCode: undefined as string | undefined,
})

function openAssociateModal(record: UnrecognizedNetworkPointItemDto) {
  associateForm.networkPointName = record.networkPointName
  associateForm.networkPointCode = undefined
  networkPointOptions.value = []
  associateModalVisible.value = true
}

async function submitAssociate() {
  if (!associateForm.networkPointCode) {
    message.warning('请选择目标网点')
    return
  }
  submitting.value = true
  try {
    const res = (await associateNetworkPoint({
      networkPointName: associateForm.networkPointName,
      networkPointCode: associateForm.networkPointCode,
    })) as any
    message.success(res?.message || '关联成功')
    associateModalVisible.value = false
    selectedRowKeys.value = []
    loadList()
  } catch (e: any) {
    message.error('关联失败：' + (e?.message || '未知错误'))
  } finally {
    submitting.value = false
  }
}

// ==================== 网点搜索 ====================
const networkPointOptions = ref<{ code: string; orgName?: string }[]>([])
const npSearching = ref(false)
let npSearchTimer: ReturnType<typeof setTimeout> | null = null

function onNetworkPointSearch(val: string) {
  if (npSearchTimer) clearTimeout(npSearchTimer)
  npSearchTimer = setTimeout(async () => {
    if (!val || val.length < 1) {
      networkPointOptions.value = []
      return
    }
    npSearching.value = true
    try {
      const res = (await getNetworkPointList({ page: 1, pageSize: 20, keyword: val } as any)) as any
      networkPointOptions.value = (res?.items || []).map((item: any) => ({
        code: item.code || item.id?.toString(),
        orgName: item.manager || item.address || '',
      }))
    } catch {
      networkPointOptions.value = []
    } finally {
      npSearching.value = false
    }
  }, 300)
}

// ==================== 新增网点弹窗 ====================
const createNpModalVisible = ref(false)
const creatingNp = ref(false)
const createNpForm = reactive({ code: '', name: '' })

function openCreateNpModal() {
  createNpForm.code = ''
  createNpForm.name = associateForm.networkPointName || ''
  createNpModalVisible.value = true
}

async function submitCreateNp() {
  if (!createNpForm.code.trim()) {
    message.warning('请输入网点编号')
    return
  }
  creatingNp.value = true
  try {
    await createNetworkPoint({
      code: createNpForm.code.trim(),
      orgId: 0,
      pointLevel: 1,
      remark: createNpForm.name || undefined,
    })
    message.success('网点创建成功')
    createNpModalVisible.value = false
    // 自动选中新创建的网点
    associateForm.networkPointCode = createNpForm.code.trim()
    networkPointOptions.value = [{ code: createNpForm.code.trim(), orgName: createNpForm.name }]
  } catch (e: any) {
    message.error('创建网点失败：' + (e?.message || '未知错误'))
  } finally {
    creatingNp.value = false
  }
}

// ==================== 批量忽略弹窗 ====================
const ignoreModalVisible = ref(false)
const ignoreForm = reactive({ reason: '' })

function openIgnoreModal() {
  ignoreForm.reason = ''
  ignoreModalVisible.value = true
}

async function submitIgnore() {
  submitting.value = true
  try {
    const res = (await ignoreNetworkPointErrors({
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
