<template>
  <div class="expense-classification">
    <div class="sticky-header">
      <PageHeader>
        <template #actions>
          <a-button @click="router.back()">返回</a-button>
        </template>
      </PageHeader>

      <!-- 头部统计 -->
      <div class="summary-bar">
        <span class="summary-item"><span class="summary-label">批次ID:</span> {{ batchId }}</span>
        <span class="summary-item file-item"><span class="summary-label">文件:</span> <span class="file-name-text" :title="fileName">{{ fileName }}</span></span>
        <span class="summary-item"><span class="summary-label">已自动处理:</span> {{ data.autoProcessedRows }} 行</span>
        <span class="summary-item"><span class="summary-label">待确认:</span> {{ data.pendingRows }} 行</span>
      </div>
    </div>

    <!-- 分组确认区域 -->
    <a-card class="section-card" :loading="loading">
      <template #title>
        <span>关键词推荐分类</span>
        <StatusTag type="info" style="margin-left: 8px">{{ data.matched.length }} 组</StatusTag>
      </template>
      <template #extra>
        <a-button type="primary" size="small" :disabled="data.matched.length === 0" :loading="confirmAllLoading" @click="handleConfirmAll">
          全部确认
        </a-button>
      </template>

      <div v-if="data.matched.length === 0" style="color: var(--text-3); text-align: center; padding: 24px;">
        暂无推荐分类数据
      </div>

      <div v-else class="matched-list">
        <a-card v-for="(group, idx) in data.matched" :key="idx" size="small" class="matched-card" :bodyStyle="{ padding: '12px 16px' }">
          <div class="matched-card-header">
            <div class="matched-info">
              <a-select
                v-model:value="matchedCategoryMap[idx]"
                style="width: 200px"
                size="small"
                showSearch
                optionFilterProp="label"
              >
                <a-select-option v-for="cat in data.categories" :key="`${cat.name}|${cat.department}`" :value="`${cat.name}|${cat.department}`" :label="`${cat.name}(${cat.department})`">
                  {{ cat.name }}({{ cat.department }})
                </a-select-option>
              </a-select>
              <a-tag>{{ group.department }}</a-tag>
              <span class="account-name">→ {{ group.accountName }}</span>
              <a-badge :count="group.rowCount" :number-style="{ backgroundColor: 'var(--color-info)' }" style="margin-left: 8px" />
            </div>
            <div class="matched-actions">
              <a-button size="small" type="link" @click="toggleExpand(idx)">
                {{ expandedGroups.includes(idx) ? '收起' : '展开查看' }}
              </a-button>
              <a-button size="small" type="primary" :loading="confirmingGroups.includes(idx)" @click="handleConfirmGroup(group, idx)">
                确认
              </a-button>
            </div>
          </div>
          <!-- 展开的示例行 -->
          <div v-if="expandedGroups.includes(idx)" class="sample-rows">
            <a-table :columns="sampleColumns" :dataSource="group.sampleRows" :pagination="false" size="small" rowKey="id" />
          </div>
        </a-card>
      </div>
    </a-card>

    <!-- 未匹配行区域 -->
    <a-card class="section-card" :loading="loading">
      <template #title>
        <span>未匹配行</span>
        <StatusTag type="warning" style="margin-left: 8px">{{ data.unmatched.length }} 行</StatusTag>
      </template>
      <template #extra>
        <a-button size="small" :disabled="selectedUnmatchedKeys.length === 0" @click="showBatchCategoryModal">
          批量设置类别 ({{ selectedUnmatchedKeys.length }})
        </a-button>
      </template>

      <div v-if="data.unmatched.length === 0" style="color: var(--text-3); text-align: center; padding: 24px;">
        暂无未匹配行
      </div>

      <a-table
        v-else
        :columns="unmatchedColumns"
        :dataSource="pagedUnmatched"
        :pagination="false"
        :row-selection="{ selectedRowKeys: selectedUnmatchedKeys, onChange: onUnmatchedSelectChange }"
        size="small"
        rowKey="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'amount'">
            {{ record.amount?.toFixed(2) }}
          </template>
          <template v-else-if="column.dataIndex === 'category'">
            <a-select
              v-model:value="unmatchedCategoryMap[record.id]"
              placeholder="选择类别"
              style="width: 200px"
              size="small"
              allowClear
              showSearch
              optionFilterProp="label"
            >
              <a-select-option v-for="cat in data.categories" :key="`${cat.name}|${cat.department}`" :value="`${cat.name}|${cat.department}`" :label="`${cat.name}(${cat.department})`">
                {{ cat.name }}({{ cat.department }})
              </a-select-option>
            </a-select>
          </template>
        </template>
      </a-table>

      <div v-if="data.unmatched.length > pageSize" style="text-align: right; margin-top: 12px;">
        <a-pagination
          v-model:current="unmatchedPage"
          :total="data.unmatched.length"
          :pageSize="pageSize"
          size="small"
          showSizeChanger
          :pageSizeOptions="['20', '50', '100']"
          @showSizeChange="(_, size) => { pageSize = size; unmatchedPage = 1 }"
        />
      </div>

      <!-- 已设置类别的未匹配行确认 -->
      <div v-if="assignedUnmatchedCount > 0" style="margin-top: 12px; text-align: right;">
        <a-button type="primary" :loading="confirmUnmatchedLoading" @click="handleConfirmUnmatched">
          确认已设置的 {{ assignedUnmatchedCount }} 行
        </a-button>
      </div>
    </a-card>

    <!-- 底部操作区 -->
    <a-card class="action-card">
      <div style="display: flex; justify-content: space-between; align-items: center;">
        <span style="color: var(--text-2)">
          总计 {{ data.totalRows }} 行，已自动处理 {{ data.autoProcessedRows }} 行，待确认 {{ data.pendingRows }} 行
        </span>
        <a-button type="primary" size="large" :loading="generateLoading" @click="handleGenerateVoucher">
          生成凭证
        </a-button>
      </div>
    </a-card>

    <!-- 批量设置类别弹窗 -->
    <a-modal v-model:open="batchCategoryModalVisible" title="批量设置类别" @ok="handleBatchCategoryOk" :confirmLoading="batchCategoryConfirmLoading">
      <p>将为选中的 {{ selectedUnmatchedKeys.length }} 行设置类别：</p>
      <a-select
        v-model:value="batchCategoryValue"
        placeholder="选择类别"
        style="width: 100%"
        allowClear
        showSearch
        optionFilterProp="label"
      >
        <a-select-option v-for="cat in data.categories" :key="`${cat.name}|${cat.department}`" :value="`${cat.name}|${cat.department}`" :label="`${cat.name}(${cat.department})`">
          {{ cat.name }}({{ cat.department }})
        </a-select-option>
      </a-select>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { get, post } from '@/api/request'

const route = useRoute()
const router = useRouter()

const batchId = computed(() => route.params.batchId as string)
const fileName = computed(() => (route.query.fileName as string) || '-')

// ==================== 状态 ====================
const loading = ref(false)
const confirmAllLoading = ref(false)
const confirmUnmatchedLoading = ref(false)
const generateLoading = ref(false)
const confirmingGroups = ref<number[]>([])
const expandedGroups = ref<number[]>([])

interface SampleRow {
  id: number
  summary: string
  amount: number
  costCenter: string
}

interface MatchedGroup {
  category: string
  department: string
  accountName: string
  rowCount: number
  rowIds: number[]
  sampleRows: SampleRow[]
}

interface UnmatchedRow {
  id: number
  summary: string
  amount: number
  costCenter: string
  applicant: string
}

interface CategoryOption {
  name: string
  department: string
}

interface ClassificationData {
  batchId: number
  totalRows: number
  autoProcessedRows: number
  pendingRows: number
  matched: MatchedGroup[]
  unmatched: UnmatchedRow[]
  categories: CategoryOption[]
}

const data = reactive<ClassificationData>({
  batchId: 0,
  totalRows: 0,
  autoProcessedRows: 0,
  pendingRows: 0,
  matched: [],
  unmatched: [],
  categories: [],
})

// 未匹配行分页
const unmatchedPage = ref(1)
const pageSize = ref(20)
const pagedUnmatched = computed(() => {
  const start = (unmatchedPage.value - 1) * pageSize.value
  return data.unmatched.slice(start, start + pageSize.value)
})

// 未匹配行选择和类别设置
const selectedUnmatchedKeys = ref<number[]>([])
const unmatchedCategoryMap = reactive<Record<number, string>>({})
const matchedCategoryMap = ref<Record<number, string>>({})

const assignedUnmatchedCount = computed(() => {
  return Object.values(unmatchedCategoryMap).filter(v => !!v).length
})

// 批量设置弹窗
const batchCategoryModalVisible = ref(false)
const batchCategoryValue = ref<string | undefined>(undefined)
const batchCategoryConfirmLoading = ref(false)

// ==================== 表格列定义 ====================
const sampleColumns = [
  { title: 'ID', dataIndex: 'id', width: 60 },
  { title: '费用描述', dataIndex: 'summary', ellipsis: true },
  { title: '金额', dataIndex: 'amount', width: 100 },
  { title: '成本中心', dataIndex: 'costCenter', width: 120 },
]

const unmatchedColumns = [
  { title: '费用描述', dataIndex: 'summary', ellipsis: true },
  { title: '金额', dataIndex: 'amount', width: 100 },
  { title: '成本中心', dataIndex: 'costCenter', width: 120 },
  { title: '申请人', dataIndex: 'applicant', width: 80 },
  { title: '选择类别', dataIndex: 'category', width: 220 },
]

// ==================== 数据加载 ====================
async function loadData() {
  loading.value = true
  try {
    const res = await get(`/cardflow/expense-classification/${batchId.value}`)
    Object.assign(data, res)
    // 初始化推荐分类的可编辑映射
    const map: Record<number, string> = {}
    data.matched.forEach((g, idx) => { map[idx] = `${g.category}|${g.department}` })
    matchedCategoryMap.value = map
  } catch (e: any) {
    message.error(e?.message || '加载分类数据失败')
  } finally {
    loading.value = false
  }
}

// ==================== 交互逻辑 ====================
function toggleExpand(idx: number) {
  const i = expandedGroups.value.indexOf(idx)
  if (i >= 0) expandedGroups.value.splice(i, 1)
  else expandedGroups.value.push(idx)
}

function onUnmatchedSelectChange(keys: Key[]) {
  selectedUnmatchedKeys.value = keys.map(Number)
}

// 确认单组
async function handleConfirmGroup(group: MatchedGroup, idx: number) {
  confirmingGroups.value.push(idx)
  try {
    const selected = matchedCategoryMap.value[idx] || `${group.category}|${group.department}`
    const [cat, dept] = selected.split('|')
    await post(`/cardflow/expense-classification/${batchId.value}/confirm`, {
      items: [{ rowIds: group.rowIds, category: cat, department: dept }]
    })
    message.success(`已确认「${cat}」${group.rowCount} 行`)
    await loadData()
  } catch (e: any) {
    message.error(e?.message || '确认失败')
  } finally {
    confirmingGroups.value = confirmingGroups.value.filter(i => i !== idx)
  }
}

// 全部确认
async function handleConfirmAll() {
  confirmAllLoading.value = true
  try {
    const items = data.matched.map((g, idx) => {
      const selected = matchedCategoryMap.value[idx] || `${g.category}|${g.department}`
      const [category, department] = selected.split('|')
      return { rowIds: g.rowIds, category, department }
    })
    await post(`/cardflow/expense-classification/${batchId.value}/confirm`, { items })
    message.success('已确认所有推荐分类')
    await loadData()
  } catch (e: any) {
    message.error(e?.message || '确认失败')
  } finally {
    confirmAllLoading.value = false
  }
}

// 确认已设置的未匹配行
async function handleConfirmUnmatched() {
  const items: { rowIds: number[]; category: string; department: string }[] = []
  const grouped: Record<string, number[]> = {}

  for (const [idStr, catValue] of Object.entries(unmatchedCategoryMap)) {
    if (!catValue) continue
    if (!grouped[catValue]) grouped[catValue] = []
    grouped[catValue].push(Number(idStr))
  }

  for (const [key, rowIds] of Object.entries(grouped)) {
    const [category, department] = key.split('|')
    items.push({ rowIds, category, department })
  }

  if (items.length === 0) {
    message.warning('请先为行设置类别')
    return
  }

  confirmUnmatchedLoading.value = true
  try {
    await post(`/cardflow/expense-classification/${batchId.value}/confirm`, { items })
    message.success(`已确认 ${Object.keys(unmatchedCategoryMap).filter(k => unmatchedCategoryMap[Number(k)]).length} 行分类`)
    // 清除已确认的
    for (const key of Object.keys(unmatchedCategoryMap)) {
      delete unmatchedCategoryMap[Number(key)]
    }
    selectedUnmatchedKeys.value = []
    await loadData()
  } catch (e: any) {
    message.error(e?.message || '确认失败')
  } finally {
    confirmUnmatchedLoading.value = false
  }
}

// 批量设置类别弹窗
function showBatchCategoryModal() {
  batchCategoryValue.value = undefined
  batchCategoryModalVisible.value = true
}

async function handleBatchCategoryOk() {
  if (!batchCategoryValue.value) {
    message.warning('请选择类别')
    return
  }
  batchCategoryConfirmLoading.value = true
  try {
    for (const id of selectedUnmatchedKeys.value) {
      unmatchedCategoryMap[id] = batchCategoryValue.value
    }
    message.success(`已为 ${selectedUnmatchedKeys.value.length} 行设置类别`)
    batchCategoryModalVisible.value = false
  } finally {
    batchCategoryConfirmLoading.value = false
  }
}

// 生成凭证
async function handleGenerateVoucher() {
  if (data.pendingRows > 0) {
    Modal.confirm({
      title: '确认生成凭证',
      content: `仍有 ${data.pendingRows} 行未确认分类，确定继续生成凭证？`,
      okText: '继续',
      cancelText: '取消',
      onOk: doGenerateVoucher,
    })
  } else {
    await doGenerateVoucher()
  }
}

async function doGenerateVoucher() {
  generateLoading.value = true
  try {
    const res = await post(`/cardflow/expense-classification/${batchId.value}/generate-voucher`)
    Modal.success({
      title: '凭证生成成功',
      content: `成功生成 ${res?.voucherCount ?? ''} 张凭证`,
      onOk: () => router.back(),
    })
  } catch (e: any) {
    message.error(e?.message || '生成凭证失败')
  } finally {
    generateLoading.value = false
  }
}

// ==================== 初始化 ====================
onMounted(() => {
  loadData()
})
</script>

<style scoped>
.expense-classification {
  padding: 0;
}
.sticky-header {
  position: sticky;
  top: 0;
  z-index: 10;
  background: var(--bg-card);
}
.summary-bar {
  display: flex;
  align-items: center;
  gap: 28px;
  padding: 10px 20px;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  margin-bottom: 16px;
  font-size: 14px;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
}

.summary-item {
  flex-shrink: 0;
}

.summary-item.file-item {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.summary-label {
  color: var(--text-3);
  font-weight: normal;
}

.file-name-text {
  display: inline-block;
  max-width: 360px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
}
.section-card {
  margin-bottom: 16px;
}
.action-card {
  position: sticky;
  bottom: 0;
  z-index: 10;
  border-top: 2px solid var(--border);
  box-shadow: var(--shadow-sm);

  :deep(.ant-card-body) {
    padding: 8px 16px;
  }
}
.matched-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.matched-card {
  border: 1px solid var(--border);
}
.matched-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.matched-info {
  display: flex;
  align-items: center;
  gap: 4px;
}
.account-name {
  color: var(--text-2);
  font-size: 13px;
}
.matched-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}
.sample-rows {
  margin-top: 8px;
  border-top: 1px solid var(--border);
  padding-top: 8px;
}
</style>
