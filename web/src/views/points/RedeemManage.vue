<template>
  <div class="redeem-manage-page">
    <PageHeader title="兑换商品管理">
      <template #actions>
        <a-button type="primary" @click="openItemDialog()">
          <template #icon><PlusOutlined /></template>新增商品
        </a-button>
      </template>
    </PageHeader>

    <a-tabs v-model:activeKey="activeTab">
      <!-- Tab 1: 商品管理 -->
      <a-tab-pane key="items" tab="商品管理">
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px; margin-bottom: 16px;">
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="商品名称" style="width: 180px" allowClear @keyup.enter="handleSearch" />
          <a-select v-model:value="searchForm.category" size="small" placeholder="分类" style="width: 130px" allowClear :options="categoryOptions" />
          <a-select v-model:value="searchForm.status" size="small" placeholder="状态" style="width: 120px" allowClear :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>

        <a-table
          :columns="itemColumns"
          :data-source="itemList"
          :loading="loadingItems"
          :pagination="itemPagination"
          row-key="id"
          bordered
          @change="handleItemTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'category'">
              {{ categoryMap[record.category] || '未知' }}
            </template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="record.status === 1 ? 'success' : 'default'">
                {{ record.status === 1 ? '上架中' : '已下架' }}
              </a-tag>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-button type="link" size="small" @click="openItemDialog(record as RedeemItemListDto)">编辑</a-button>
              <a-popconfirm
                :title="`确定${record.status === 1 ? '下架' : '上架'}「${record.name}」？`"
                @confirm="handleToggleItem(record as RedeemItemListDto)"
              >
                <a-button type="link" size="small">{{ record.status === 1 ? '下架' : '上架' }}</a-button>
              </a-popconfirm>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <!-- Tab 2: 兑换记录管理 -->
      <a-tab-pane key="records" tab="兑换记录">
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px; margin-bottom: 16px;">
          <a-input v-model:value="recordSearchForm.keyword" size="small" placeholder="员工/商品名" style="width: 180px" allowClear @keyup.enter="handleRecordSearch" />
          <a-select v-model:value="recordSearchForm.status" size="small" placeholder="状态" style="width: 120px" allowClear :options="recordStatusOptions" />
          <a-button type="primary" size="small" @click="handleRecordSearch">查询</a-button>
          <a-button size="small" @click="handleRecordReset">重置</a-button>
        </div>

        <a-table
          :columns="recordColumns"
          :data-source="recordList"
          :loading="loadingRecords"
          :pagination="recordPagination"
          row-key="id"
          bordered
          @change="handleRecordTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="recordStatusColor(record.status)">{{ recordStatusLabel(record.status) }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ formatTime(record.createTime) }}
            </template>
            <template v-if="column.dataIndex === 'issueTime'">
              {{ record.issueTime ? formatTime(record.issueTime) : '-' }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <template v-if="record.status === 0">
                <a-popconfirm title="确认发放该兑换？" @confirm="handleDeliver(record as RedeemRecordListDto)">
                  <a-button type="link" size="small">确认发放</a-button>
                </a-popconfirm>
                <a-popconfirm title="确定取消该兑换？积分将退回" @confirm="handleCancelRedeem(record as RedeemRecordListDto)">
                  <a-button type="link" size="small" danger>取消兑换</a-button>
                </a-popconfirm>
              </template>
              <span v-else class="text-muted">-</span>
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <!-- 新增/编辑商品弹窗 -->
    <a-modal
      v-model:open="itemDialogVisible"
      :title="editingItem ? '编辑商品' : '新增商品'"
      :confirm-loading="savingItem"
      @ok="handleSaveItem"
      :width="560"
      destroy-on-close
    >
      <a-form ref="itemFormRef" :model="itemForm" :rules="itemRules" layout="vertical">
        <a-form-item label="商品名称" name="name">
          <a-input v-model:value="itemForm.name" placeholder="请输入商品名称" :maxlength="100" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="分类" name="category">
              <a-select v-model:value="itemForm.category" placeholder="请选择分类" :options="categoryOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="所需积分" name="requiredPoints">
              <a-input-number v-model:value="itemForm.requiredPoints" :min="1" style="width: 100%" placeholder="所需积分" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="库存" name="stock">
              <a-input-number v-model:value="itemForm.stock" :min="0" style="width: 100%" placeholder="库存数量" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="排序" name="sortOrder">
              <a-input-number v-model:value="itemForm.sortOrder" :min="0" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="商品图片" name="image">
          <a-input v-model:value="itemForm.image" placeholder="图片URL（可选）" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="itemForm.description" :rows="3" placeholder="商品描述" :maxlength="500" show-count />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import PageHeader from '@/components/PageHeader.vue'
import {
  getRedeemItems, createRedeemItem, updateRedeemItem, toggleRedeemItem,
  getRedeemRecords, deliverRedeem, cancelRedeem,
} from '@/api/points'
import type { RedeemItemListDto, RedeemRecordListDto } from '@/types/points'

// ==================== 常量 ====================
const categoryMap: Record<number, string> = {
  0: '假期福利', 1: '生活补贴', 2: '学习培训',
  3: '团建活动', 4: '实物奖品', 5: '其他',
}

const categoryOptions = Object.entries(categoryMap).map(([k, v]) => ({ label: v, value: Number(k) }))

const statusOptions = [
  { label: '上架中', value: 1 },
  { label: '已下架', value: 0 },
]

const recordStatusOptions = [
  { label: '待发放', value: 0 },
  { label: '已发放', value: 1 },
  { label: '已取消', value: 2 },
]

// ==================== 商品管理 ====================
const activeTab = ref('items')
const loadingItems = ref(false)
const itemList = ref<RedeemItemListDto[]>([])
const itemPageIndex = ref(1)
const itemPageSize = ref(10)
const itemTotal = ref(0)

const searchForm = reactive({
  keyword: '',
  category: undefined as number | undefined,
  status: undefined as number | undefined,
})

const itemPagination = computed(() => ({
  current: itemPageIndex.value,
  pageSize: itemPageSize.value,
  total: itemTotal.value,
  showSizeChanger: true,
}))

const itemColumns = [
  { title: '商品名称', dataIndex: 'name', width: 180 },
  { title: '分类', dataIndex: 'category', width: 100, align: 'center' as const },
  { title: '所需积分', dataIndex: 'requiredPoints', width: 100, align: 'center' as const },
  { title: '库存', dataIndex: 'stock', width: 80, align: 'center' as const },
  { title: '已兑换', dataIndex: 'redeemedCount', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 150, align: 'center' as const },
]

async function loadItems() {
  loadingItems.value = true
  try {
    const res = await getRedeemItems({
      pageIndex: itemPageIndex.value,
      pageSize: itemPageSize.value,
      keyword: searchForm.keyword || undefined,
      category: searchForm.category ?? null,
      status: searchForm.status ?? null,
    })
    const data: any = (res as any)?.data ?? res
    itemList.value = data?.items ?? []
    itemTotal.value = data?.total ?? 0
  } catch (e: any) {
    message.error(e?.message || '加载商品列表失败')
  } finally {
    loadingItems.value = false
  }
}

function handleSearch() {
  itemPageIndex.value = 1
  loadItems()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.category = undefined
  searchForm.status = undefined
  handleSearch()
}

function handleItemTableChange(pagination: any) {
  itemPageIndex.value = pagination.current
  itemPageSize.value = pagination.pageSize
  loadItems()
}

async function handleToggleItem(record: RedeemItemListDto) {
  try {
    await toggleRedeemItem(record.id)
    message.success(record.status === 1 ? '已下架' : '已上架')
    await loadItems()
  } catch (e: any) {
    message.error(e?.message || '操作失败')
  }
}

// ==================== 商品表单 ====================
const itemDialogVisible = ref(false)
const savingItem = ref(false)
const editingItem = ref<RedeemItemListDto | null>(null)
const itemFormRef = ref<FormInstance>()

const itemForm = reactive({
  name: '',
  category: undefined as number | undefined,
  description: '',
  image: '',
  requiredPoints: undefined as number | undefined,
  stock: 0,
  sortOrder: 0,
})

const itemRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入商品名称', trigger: 'blur' }],
  category: [{ required: true, message: '请选择分类', trigger: 'change', type: 'number' }],
  requiredPoints: [{ required: true, message: '请输入所需积分', trigger: 'change', type: 'number' }],
  description: [{ required: true, message: '请输入描述', trigger: 'blur' }],
}

function openItemDialog(record?: RedeemItemListDto) {
  editingItem.value = record ?? null
  if (record) {
    itemForm.name = record.name
    itemForm.category = record.category
    itemForm.requiredPoints = record.requiredPoints
    itemForm.stock = record.stock
    itemForm.sortOrder = record.sortOrder
    itemForm.image = record.image || ''
    itemForm.description = ''  // detail needs separate load, keep simple
  } else {
    itemForm.name = ''
    itemForm.category = undefined
    itemForm.description = ''
    itemForm.image = ''
    itemForm.requiredPoints = undefined
    itemForm.stock = 0
    itemForm.sortOrder = 0
  }
  itemDialogVisible.value = true
}

async function handleSaveItem() {
  try {
    await itemFormRef.value?.validate()
  } catch {
    return
  }
  savingItem.value = true
  try {
    if (editingItem.value) {
      await updateRedeemItem(editingItem.value.id, {
        name: itemForm.name,
        category: itemForm.category!,
        description: itemForm.description,
        image: itemForm.image || null,
        requiredPoints: itemForm.requiredPoints!,
        stock: itemForm.stock,
        sortOrder: itemForm.sortOrder,
        status: editingItem.value.status,
      })
    } else {
      await createRedeemItem({
        name: itemForm.name,
        category: itemForm.category!,
        description: itemForm.description,
        image: itemForm.image || null,
        requiredPoints: itemForm.requiredPoints!,
        stock: itemForm.stock,
        sortOrder: itemForm.sortOrder,
      })
    }
    message.success('保存成功')
    itemDialogVisible.value = false
    await loadItems()
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    savingItem.value = false
  }
}

// ==================== 兑换记录管理 ====================
const loadingRecords = ref(false)
const recordList = ref<RedeemRecordListDto[]>([])
const recordPageIndex = ref(1)
const recordPageSize = ref(10)
const recordTotal = ref(0)

const recordSearchForm = reactive({
  keyword: '',
  status: undefined as number | undefined,
})

const recordPagination = computed(() => ({
  current: recordPageIndex.value,
  pageSize: recordPageSize.value,
  total: recordTotal.value,
  showSizeChanger: true,
}))

const recordColumns = [
  { title: '员工', dataIndex: 'userName', width: 120 },
  { title: '商品', dataIndex: 'itemName', width: 160 },
  { title: '消耗积分', dataIndex: 'deductedPoints', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', width: 100, align: 'center' as const },
  { title: '兑换时间', dataIndex: 'createTime', width: 170 },
  { title: '发放时间', dataIndex: 'issueTime', width: 170 },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '操作', dataIndex: 'action', width: 180, align: 'center' as const },
]

async function loadRecords() {
  loadingRecords.value = true
  try {
    const res = await getRedeemRecords({
      pageIndex: recordPageIndex.value,
      pageSize: recordPageSize.value,
      keyword: recordSearchForm.keyword || undefined,
      status: recordSearchForm.status ?? null,
    })
    const data: any = (res as any)?.data ?? res
    recordList.value = data?.items ?? []
    recordTotal.value = data?.total ?? 0
  } catch (e: any) {
    message.error(e?.message || '加载兑换记录失败')
  } finally {
    loadingRecords.value = false
  }
}

function handleRecordSearch() {
  recordPageIndex.value = 1
  loadRecords()
}

function handleRecordReset() {
  recordSearchForm.keyword = ''
  recordSearchForm.status = undefined
  handleRecordSearch()
}

function handleRecordTableChange(pagination: any) {
  recordPageIndex.value = pagination.current
  recordPageSize.value = pagination.pageSize
  loadRecords()
}

async function handleDeliver(record: RedeemRecordListDto) {
  try {
    await deliverRedeem(record.id)
    message.success('发放成功')
    await loadRecords()
  } catch (e: any) {
    message.error(e?.message || '发放失败')
  }
}

async function handleCancelRedeem(record: RedeemRecordListDto) {
  try {
    await cancelRedeem(record.id)
    message.success('已取消兑换')
    await loadRecords()
  } catch (e: any) {
    message.error(e?.message || '取消失败')
  }
}

function recordStatusColor(status: number): string {
  switch (status) {
    case 0: return 'processing'
    case 1: return 'success'
    case 2: return 'error'
    default: return 'default'
  }
}

function recordStatusLabel(status: number): string {
  switch (status) {
    case 0: return '待发放'
    case 1: return '已发放'
    case 2: return '已取消'
    default: return '未知'
  }
}

function formatTime(val: string) {
  if (!val) return ''
  return val.replace('T', ' ').substring(0, 19)
}

// ==================== Tab 切换加载 ====================
watch(activeTab, (val) => {
  if (val === 'records' && recordList.value.length === 0) {
    loadRecords()
  }
})

// ==================== 初始化 ====================
onMounted(() => {
  loadItems()
})
</script>

<style scoped lang="scss">
.redeem-manage-page {
  padding: 20px;
  background: #fff;
  border-radius: 8px;
  min-height: calc(100vh - 120px);
}

.text-muted {
  color: #999;
}
</style>
