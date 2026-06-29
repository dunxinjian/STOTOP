<template>
  <div class="quotation-workbench">
    <PageHeader title="快递报价查询">
      <template #left>
        <div class="workbench-header-tabs">
          <a-tabs v-model:activeKey="activeTab" :tabBarGutter="24" size="small">
            <a-tab-pane key="byClient" tab="按对象查看" />
            <a-tab-pane key="withQuotation" tab="有报价的" />
            <a-tab-pane key="withoutQuotation" tab="无报价的" />
            <a-tab-pane key="all" tab="全部报价" />
            <a-tab-pane key="byShop" tab="按店铺查询" />
          </a-tabs>
        </div>
      </template>
    </PageHeader>

    <!-- Tab 1/2/3: Master-Detail 布局（按对象查看 / 有报价的 / 无报价的） -->
    <div v-show="isMasterDetailTab" class="tab-content">
        <div class="master-detail">
          <!-- 左侧面板 -->
          <div class="left-panel">
            <ClientPanel
              :selected-id="selectedClient?.id"
              :selected-type="selectedClient?.type"
              :quotation-filter="quotationFilter"
              @select="handleClientSelect"
            />
          </div>

          <!-- 右侧内容 -->
          <div class="right-content">
            <!-- 未选中状态 -->
            <div v-if="!selectedClient" class="empty-state">
              <a-empty description="请在左侧选择一个业务对象" />
            </div>

            <!-- 已选中 -->
            <template v-else>
              <!-- 信息栏 -->
              <div class="client-info-bar">
                <div class="client-info">
                  <span class="client-name">{{ selectedClient.name }}</span>
                  <a-tag :color="typeColorMap[selectedClient.type]">
                    {{ typeLabelMap[selectedClient.type] || selectedClient.type }}
                  </a-tag>
                  <span class="client-code">{{ selectedClient.code }}</span>
                </div>
                <div class="toolbar-actions">
                  <a-button @click="handleImport">
                    <template #icon><UploadOutlined /></template>导入
                  </a-button>
                  <a-button type="primary" @click="handleCreate">
                    <template #icon><PlusOutlined /></template>新建报价
                  </a-button>
                </div>
              </div>

              <!-- 报价表格 -->
              <div class="table-area">
                <a-table
                  :columns="detailColumns"
                  :data-source="detailDataSource"
                  :loading="detailLoading"
                  :pagination="detailPaginationConfig"
                  row-key="id"
                  bordered
                  size="middle"
                  :scroll="{ x: 1000 }"
                  @change="handleDetailTableChange"
                >
                  <template #bodyCell="{ column, record, index }">
                    <template v-if="column.dataIndex === 'index'">
                      {{ (detailPagination.pageIndex - 1) * detailPagination.pageSize + index + 1 }}
                    </template>
                    <template v-if="column.dataIndex === 'planName'">
                      <a class="plan-name-link" @click="router.push(`/express/quotation/edit/${record.id}`)">
                        {{ record.planName }}
                      </a>
                    </template>
                    <template v-if="column.dataIndex === 'status'">
                      <a-tag :color="getStatusColor(record.status)">
                        {{ getStatusText(record.status) }}
                      </a-tag>
                    </template>
                    <template v-if="column.dataIndex === 'action'">
                      <a-button type="link" size="small" @click="router.push(`/express/quotation/edit/${record.id}`)">编辑</a-button>
                      <a-button type="link" size="small" @click="handleCopy(record as QuotationListItemDto)">复制</a-button>
                      <a-popconfirm title="确定删除此报价？" @confirm="handleDelete(record as QuotationListItemDto)">
                        <a-button type="link" size="small" danger>删除</a-button>
                      </a-popconfirm>
                    </template>
                  </template>

                  <!-- 空状态 -->
                  <template #emptyText>
                    <div class="table-empty">
                      <p>该业务对象暂无报价方案</p>
                      <a-button type="primary" size="small" @click="handleCreate">
                        <template #icon><PlusOutlined /></template>新建报价
                      </a-button>
                    </div>
                  </template>
                </a-table>
              </div>
            </template>
          </div>
        </div>
    </div>

    <!-- Tab 4: 全部报价 -->
    <div v-show="activeTab === 'all'" class="tab-content">
        <div class="all-quotation-area">
          <!-- 工具栏 -->
          <div class="all-toolbar">
            <a-space>
              <a-input-search
                v-model:value="allSearchForm.keyword"
                placeholder="搜索报价名称"
                allow-clear
                style="width: 200px"
                @search="handleAllSearch"
              />
              <a-select v-model:value="allSearchForm.status" placeholder="状态" allow-clear style="width: 120px"
                :options="statusFilterOptions" @change="handleAllSearch" />
              <a-select v-model:value="allSearchForm.clientType" placeholder="业务对象类型" allow-clear style="width: 140px"
                :options="clientTypeOptions" @change="handleAllSearch" />
              <a-button @click="handleAllReset">重置</a-button>
            </a-space>
            <a-space>
              <a-button @click="allImportModalVisible = true">
                <template #icon><UploadOutlined /></template>导入Excel
              </a-button>
              <a-button type="primary" @click="router.push('/express/quotation/create')">
                <template #icon><PlusOutlined /></template>新建报价
              </a-button>
            </a-space>
          </div>

          <!-- 全量表格 -->
          <a-table
            :columns="allColumns"
            :data-source="allDataSource"
            :loading="allLoading"
            :pagination="allPaginationConfig"
            row-key="id"
            bordered
            :scroll="{ x: 1400 }"
            @change="handleAllTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (allPagination.pageIndex - 1) * allPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'planName'">
                <a class="plan-name-link" @click="router.push(`/express/quotation/edit/${record.id}`)">
                  {{ record.planName }}
                </a>
              </template>
              <template v-if="column.dataIndex === 'clientType'">
                <a-tag>{{ getClientTypeLabel(record.clientType) }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'sharedShopEnabled'">
                <a-tag :color="record.sharedShopEnabled ? 'green' : 'default'">
                  {{ record.sharedShopEnabled ? '已开启' : '未开启' }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="getStatusColor(record.status)">
                  {{ getStatusText(record.status) }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="router.push(`/express/quotation/edit/${record.id}`)">编辑</a-button>
                <a-button type="link" size="small" @click="handleAllCopy(record as QuotationListItemDto)">复制</a-button>
                <a-popconfirm title="确定删除此报价？" @confirm="handleAllDelete(record as QuotationListItemDto)">
                  <a-button type="link" size="small" danger>删除</a-button>
                </a-popconfirm>
              </template>
            </template>
          </a-table>
        </div>
    </div>

    <!-- Tab 5: 按店铺查询 -->
    <div v-show="activeTab === 'byShop'" class="tab-content">
      <ShopQueryPanel />
    </div>

    <!-- 导入弹窗（Tab 1 / Tab 2 共用） -->
    <ImportQuotationModal v-model:visible="allImportModalVisible" @success="handleImportSuccess" />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined, UploadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import ClientPanel from './components/ClientPanel.vue'
import ShopQueryPanel from './components/ShopQueryPanel.vue'
import ImportQuotationModal from './ImportQuotationModal.vue'
import {
  getQuotationList,
  copyQuotation,
  deleteQuotation,
  type QuotationListItemDto,
} from '@/api/express'

const router = useRouter()

// ==================== 常量 ====================

const typeColorMap: Record<string, string> = {
  KH: 'blue',
  DL: 'purple',
  WD: 'green',
  CB: 'orange',
  YZ: 'cyan',
}

const typeLabelMap: Record<string, string> = {
  KH: '客户',
  DL: '代理',
  WD: '网点',
  CB: '承包区',
  YZ: '驿站',
}

const clientTypeOptions = [
  { value: 'KH', label: '客户' },
  { value: 'DL', label: '代理' },
  { value: 'WD', label: '网点' },
  { value: 'YW', label: '业务员' },
  { value: 'CB', label: '承包区' },
  { value: 'YZ', label: '驿站' },
]

const statusOptions = [
  { value: 0, label: '草稿', color: 'default' },
  { value: 1, label: '待审批', color: 'processing' },
  { value: 2, label: '已通过', color: 'success' },
  { value: 3, label: '已驳回', color: 'error' },
  { value: 4, label: '已作废', color: 'warning' },
]

const statusFilterOptions = statusOptions.map(s => ({ label: s.label, value: s.value }))

function getStatusText(s: number) { return statusOptions.find(o => o.value === s)?.label ?? '未知' }
function getStatusColor(s: number) { return statusOptions.find(o => o.value === s)?.color ?? 'default' }
function getClientTypeLabel(type: string) {
  return clientTypeOptions.find(o => o.value === type)?.label ?? type
}

// ==================== Tab 状态 ====================

const activeTab = ref<string>('byClient')

// 判断当前是否为 Master-Detail 布局的 Tab
const isMasterDetailTab = computed(() =>
  ['byClient', 'withQuotation', 'withoutQuotation'].includes(activeTab.value)
)

// 根据当前 Tab 计算传给 ClientPanel 的过滤条件
const quotationFilter = computed<'all' | 'withQuotation' | 'withoutQuotation'>(() => {
  if (activeTab.value === 'withQuotation') return 'withQuotation'
  if (activeTab.value === 'withoutQuotation') return 'withoutQuotation'
  return 'all'
})



// ==================== Tab 1: 按对象查看 ====================

interface SelectedClient {
  id: string
  name: string
  code: string
  type: string
  quotationCount: number
}

const selectedClient = ref<SelectedClient | null>(null)
const detailLoading = ref(false)
const detailDataSource = ref<QuotationListItemDto[]>([])
const detailPagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const detailPaginationConfig = computed(() => ({
  current: detailPagination.pageIndex,
  pageSize: detailPagination.pageSize,
  total: detailPagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const detailColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '报价名称', dataIndex: 'planName', width: 200, ellipsis: true },
  { title: '方案编号', dataIndex: 'planCode', width: 160, ellipsis: true },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', width: 120, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

function handleClientSelect(item: SelectedClient) {
  selectedClient.value = item
  detailPagination.pageIndex = 1
  fetchDetailList()
}

async function fetchDetailList() {
  if (!selectedClient.value) return
  detailLoading.value = true
  try {
    const res = await getQuotationList({
      pageIndex: detailPagination.pageIndex,
      pageSize: detailPagination.pageSize,
      clientType: selectedClient.value.type,
      clientId: selectedClient.value.id,
    })
    detailDataSource.value = res.items
    detailPagination.total = res.total
  } catch {
    message.error('获取报价列表失败')
  } finally {
    detailLoading.value = false
  }
}

function handleDetailTableChange(pag: any) {
  detailPagination.pageIndex = pag.current
  detailPagination.pageSize = pag.pageSize
  fetchDetailList()
}

function handleCreate() {
  if (!selectedClient.value) return
  const { type, id, name } = selectedClient.value
  router.push(`/express/quotation/create?clientType=${type}&clientId=${id}&clientName=${encodeURIComponent(name)}`)
}

function handleImport() {
  allImportModalVisible.value = true
}

async function handleCopy(row: QuotationListItemDto) {
  try {
    await copyQuotation(row.id)
    message.success('复制成功')
    fetchDetailList()
  } catch { /* handled */ }
}

async function handleDelete(row: QuotationListItemDto) {
  try {
    await deleteQuotation(row.id)
    message.success('删除成功')
    fetchDetailList()
  } catch { /* handled */ }
}

// ==================== Tab 2: 全部报价 ====================

const allLoading = ref(false)
const allDataSource = ref<QuotationListItemDto[]>([])
const allPagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const allSearchForm = reactive({
  keyword: undefined as string | undefined,
  status: undefined as number | undefined,
  clientType: undefined as string | undefined,
})

const allPaginationConfig = computed(() => ({
  current: allPagination.pageIndex,
  pageSize: allPagination.pageSize,
  total: allPagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const allColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '报价名称', dataIndex: 'planName', width: 200, ellipsis: true },
  { title: '方案编号', dataIndex: 'planCode', width: 160, ellipsis: true },
  { title: '业务对象类型', dataIndex: 'clientType', width: 120, align: 'center' as const },
  { title: '业务对象ID', dataIndex: 'clientId', width: 120, ellipsis: true },
  { title: '网点编号', dataIndex: 'networkPointCode', width: 120, ellipsis: true },
  { title: '共享店铺', dataIndex: 'sharedShopEnabled', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

let allTabLoaded = false

async function fetchAllList() {
  allLoading.value = true
  try {
    const res = await getQuotationList({
      pageIndex: allPagination.pageIndex,
      pageSize: allPagination.pageSize,
      ...allSearchForm,
    })
    allDataSource.value = res.items
    allPagination.total = res.total
  } catch {
    message.error('获取报价列表失败')
  } finally {
    allLoading.value = false
  }
}

function handleAllTableChange(pag: any) {
  allPagination.pageIndex = pag.current
  allPagination.pageSize = pag.pageSize
  fetchAllList()
}

function handleAllSearch() {
  allPagination.pageIndex = 1
  fetchAllList()
}

function handleAllReset() {
  allSearchForm.keyword = undefined
  allSearchForm.status = undefined
  allSearchForm.clientType = undefined
  handleAllSearch()
}

async function handleAllCopy(row: QuotationListItemDto) {
  try {
    await copyQuotation(row.id)
    message.success('复制成功')
    fetchAllList()
  } catch { /* handled */ }
}

async function handleAllDelete(row: QuotationListItemDto) {
  try {
    await deleteQuotation(row.id)
    message.success('删除成功')
    fetchAllList()
  } catch { /* handled */ }
}

// Tab 切换逻辑
watch(activeTab, (tab, oldTab) => {
  // 切换到全部报价 Tab 时懒加载
  if (tab === 'all' && !allTabLoaded) {
    allTabLoaded = true
    fetchAllList()
  }
  // 在 Master-Detail Tab 之间切换时清除选中状态
  if (['byClient', 'withQuotation', 'withoutQuotation'].includes(tab) && tab !== oldTab) {
    selectedClient.value = null
    detailDataSource.value = []
  }
})

// ==================== 导入弹窗 ====================

const allImportModalVisible = ref(false)

function handleImportSuccess() {
  // 刷新当前激活 Tab 的数据
  if (isMasterDetailTab.value && selectedClient.value) {
    fetchDetailList()
  } else if (activeTab.value === 'all') {
    fetchAllList()
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.quotation-workbench {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.tab-content {
  flex: 1;
  min-height: 0;
  padding: 0;
}

// Tab 1: Master-Detail
.master-detail {
  display: flex;
  height: 100%;
  border: 1px solid $border-color-lighter;
  border-radius: 0;
  overflow: hidden;
  background: var(--bg-card);
}

.left-panel {
  width: 280px;
  flex-shrink: 0;
  overflow: hidden;
}

.right-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  padding: 16px;
  overflow: hidden;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.client-info-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid $border-color-lighter;

  .client-info {
    display: flex;
    align-items: center;
    gap: 8px;

    .client-name {
      font-size: 16px;
      font-weight: 600;
      color: $text-primary;
    }

    .client-code {
      font-size: $font-size-sm;
      color: $text-secondary;
    }
  }

  .toolbar-actions {
    display: flex;
    gap: 8px;
  }
}

.table-area {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.table-empty {
  padding: 24px;
  text-align: center;

  p {
    margin-bottom: 12px;
    color: $text-secondary;
  }
}

.plan-name-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

// Tab 2: 全部报价
.all-quotation-area {
  height: 100%;
  padding: 16px;
  background: var(--bg-card);
  border: 1px solid $border-color-lighter;
  border-radius: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.all-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

// PageHeader center slot 中的 tabs 样式
.workbench-header-tabs {
  display: flex;
  align-items: center;
  height: 48px;

  :deep(.ant-tabs) {
    height: 48px;
    line-height: 48px;
  }

  :deep(.ant-tabs-nav) {
    margin: 0;
    height: 48px;

    &::before {
      border-bottom: none;
    }
  }

  :deep(.ant-tabs-tab) {
    padding: 0;
    margin: 0 12px;
    font-size: 14px;
    color: rgba(0, 0, 0, 0.65);
  }

  :deep(.ant-tabs-tab-active .ant-tabs-tab-btn) {
    font-weight: 600;
    color: var(--color-primary);
  }

  :deep(.ant-tabs-ink-bar) {
    height: 3px;
    border-radius: 2px;
  }

  :deep(.ant-tabs-content-holder) {
    display: none;
  }
}
</style>
