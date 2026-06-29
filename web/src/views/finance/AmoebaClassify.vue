<template>
  <div class="page-container">
    <PageHeader title="阿米巴待分类列表">
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-range-picker v-model:value="dateRange" style="width: 240px" />
          </div>
          <div style="display:flex; gap:8px;">
            <a-button type="primary" :disabled="!selectedRowKeys.length" @click="handleBatchClassify">
              批量分类 ({{ selectedRowKeys.length }})
            </a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="false"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
        row-key="entryId"
        bordered
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'amount'">
            <span :style="{ color: record.amount >= 0 ? '#333' : 'var(--color-danger)' }">
              {{ formatAmount(record.amount) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-dropdown :trigger="['click']">
              <a-button type="link" size="small">
                分类 <DownOutlined />
              </a-button>
              <template #overlay>
                <a-menu @click="(info: any) => handleClassifySingle(record, info.key)">
                  <a-menu-item v-for="item in plItemOptions" :key="item.id">{{ item.label }}</a-menu-item>
                  <a-menu-item v-if="!plItemOptions.length" disabled>暂无可选项目</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无未分类数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 批量分类弹窗 -->
    <a-modal
      v-model:open="batchDialogVisible"
      title="批量分类"
      width="500px"
      :destroy-on-close="true"
    >
      <div style="padding: 10px 20px;">
        <p>已选择 <b>{{ selectedRowKeys.length }}</b> 条记录，请选择要归入的损益项目：</p>
        <a-tree-select
          v-model:value="batchPlItemId"
          :tree-data="plItemTreeOptions"
          placeholder="请选择损益项目"
          style="width: 100%"
          tree-default-expand-all
          allow-clear
        />
      </div>
      <template #footer>
        <a-button @click="batchDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="batchSubmitLoading" :disabled="!batchPlItemId" @click="handleSubmitBatchClassify">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DownOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getAmoebaUnclassified,
  batchAmoebaClassify,
  getAmoebaPLTemplates,
  getAmoebaPLTemplateById,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import type { Key } from 'ant-design-vue/es/table/interface'
import dayjs from 'dayjs'

const accountSetStore = useAccountSetStore()

// ==================== 筛选状态 ====================
const dateRange = ref<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().startOf('month'), dayjs()])

// ==================== 表格 ====================
const loading = ref(false)
const tableData = ref<any[]>([])
const selectedRowKeys = ref<number[]>([])

const columns = [
  { title: '日期', dataIndex: 'date', width: 100 },
  { title: '科目', dataIndex: 'accountName', width: 180 },
  { title: '摘要', dataIndex: 'summary', width: 200 },
  { title: '金额', dataIndex: 'amount', width: 120, align: 'right' as const },
  { title: '品牌', dataIndex: 'brandName', width: 100 },
  { title: '方向', dataIndex: 'direction', width: 80 },
  { title: '操作', dataIndex: 'action', width: 120, align: 'center' as const },
]

function onSelectChange(keys: Key[]) {
  selectedRowKeys.value = keys.map(Number)
}

function formatAmount(val: number | undefined | null): string {
  if (val === undefined || val === null) return '0.00'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

async function fetchData() {
  loading.value = true
  try {
    const [start, end] = dateRange.value
    const res = await getAmoebaUnclassified({
      startDate: start.format('YYYY-MM-DD'),
      endDate: end.format('YYYY-MM-DD'),
      accountSetId: accountSetStore.currentAccountSetId,
    })
    tableData.value = Array.isArray(res) ? res : (res as any)?.items || []
    selectedRowKeys.value = []
  } catch (e: any) {
    message.error(e?.message || '加载失败')
    tableData.value = []
  } finally {
    loading.value = false
  }
}

// ==================== 损益项选项 ====================
const plItemOptions = ref<{ id: number; label: string }[]>([])
const plItemTreeOptions = ref<any[]>([])

async function loadPLItems() {
  try {
    const templates = await getAmoebaPLTemplates({ accountSetId: accountSetStore.currentAccountSetId })
    const tplList = Array.isArray(templates) ? templates : (templates as any)?.items || []
    if (tplList.length > 0) {
      const detail = await getAmoebaPLTemplateById(tplList[0].id)
      const items = detail?.items || detail?.children || []
      plItemOptions.value = items.map((it: any) => ({
        id: it.id,
        label: it.name || it.fName,
      }))
      plItemTreeOptions.value = buildTreeSelect(items)
    }
  } catch {
    plItemOptions.value = []
  }
}

function buildTreeSelect(items: any[]): any[] {
  const map = new Map<number, any>()
  const roots: any[] = []
  items.forEach(item => {
    map.set(item.id, {
      title: item.name || item.fName,
      value: item.id,
      key: item.id,
      children: [],
    })
  })
  items.forEach(item => {
    const node = map.get(item.id)!
    const parentId = item.parentId || item.fParentId
    if (parentId && map.has(parentId)) {
      map.get(parentId)!.children.push(node)
    } else {
      roots.push(node)
    }
  })
  function clean(nodes: any[]) {
    nodes.forEach(n => {
      if (n.children.length === 0) delete n.children
      else clean(n.children)
    })
  }
  clean(roots)
  return roots
}

// ==================== 分类操作 ====================
async function handleClassifySingle(record: any, plItemId: number) {
  try {
    await batchAmoebaClassify({
      items: [{ entryId: record.entryId, plItemId }],
    })
    message.success('分类成功')
    // 从列表移除
    tableData.value = tableData.value.filter(r => r.entryId !== record.entryId)
  } catch (e: any) {
    message.error(e?.message || '分类失败')
  }
}

// 批量分类
const batchDialogVisible = ref(false)
const batchPlItemId = ref<number | null>(null)
const batchSubmitLoading = ref(false)

function handleBatchClassify() {
  batchPlItemId.value = null
  batchDialogVisible.value = true
}

async function handleSubmitBatchClassify() {
  if (!batchPlItemId.value) return
  batchSubmitLoading.value = true
  try {
    await batchAmoebaClassify({
      items: selectedRowKeys.value.map(entryId => ({
        entryId,
        plItemId: batchPlItemId.value!,
      })),
    })
    message.success(`成功分类 ${selectedRowKeys.value.length} 条记录`)
    batchDialogVisible.value = false
    // 从列表移除已分类项
    const classified = new Set(selectedRowKeys.value)
    tableData.value = tableData.value.filter(r => !classified.has(r.entryId))
    selectedRowKeys.value = []
  } catch (e: any) {
    message.error(e?.message || '批量分类失败')
  } finally {
    batchSubmitLoading.value = false
  }
}

// ==================== 生命周期 ====================
onMounted(() => {
  fetchData()
  loadPLItems()
})

watch(dateRange, () => {
  fetchData()
})

// 账套切换后，待分类列表与损益项下拉都需按新账套重载（否则下拉沿用旧账套损益项，可能把分录归错账套）
watch(
  () => accountSetStore.currentAccountSetId,
  () => {
    fetchData()
    loadPLItems()
  }
)
</script>
