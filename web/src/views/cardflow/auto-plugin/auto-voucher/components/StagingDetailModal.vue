<template>
  <a-modal
    v-model:open="localVisible"
    :title="modalTitle"
    :width="1000"
    :destroyOnClose="true"
    :footer="null"
  >
    <a-table
      :columns="dynamicColumns"
      :dataSource="tableData"
      :rowKey="(_: unknown, index?: number) => index ?? 0"
      :loading="loading"
      :pagination="{
        current: page,
        pageSize: pageSize,
        total: total,
        showSizeChanger: true,
        showTotal: (t: number) => `共 ${t} 条`,
        pageSizeOptions: ['20', '50', '100']
      }"
      @change="handleTableChange"
      bordered
      size="small"
      :scroll="{ x: 'max-content', y: 400 }"
    />
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { get } from '@/api/request'

interface Props {
  visible: boolean
  tableName: string
  fieldName: string
  fieldValue: string
  batchId?: number
}

const props = defineProps<Props>()
const emit = defineEmits(['update:visible'])

const localVisible = computed({
  get: () => props.visible,
  set: (val) => emit('update:visible', val)
})

const modalTitle = computed(() => {
  const displayValue = props.fieldValue || '(空值)'
  return `${props.fieldName} = "${displayValue}" 的记录明细`
})

const tableData = ref<Record<string, any>[]>([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)

// 排除系统字段，动态生成列
const systemFields = new Set(['FID', 'F批次ID', 'F处理状态', 'F错误信息', 'F创建时间', 'F更新时间', 'F组织ID'])
const dynamicColumns = computed(() => {
  if (tableData.value.length === 0) return []
  const firstRow = tableData.value[0]
  return Object.keys(firstRow)
    .filter(k => !systemFields.has(k))
    .map(key => ({
      title: key.replace(/^F/, ''),
      dataIndex: key,
      key,
      ellipsis: true,
      width: 120
    }))
})

async function loadData() {
  loading.value = true
  try {
    const params: Record<string, any> = {
      page: page.value,
      pageSize: pageSize.value,
      fieldName: props.fieldName,
      fieldValue: props.fieldValue
    }
    if (props.batchId) params.batchId = props.batchId

    const res = await get(`/cardflow/staging/${encodeURIComponent(props.tableName)}`, params)
    const data = (res as any)?.data || res
    tableData.value = data.items || []
    total.value = data.total || 0
  } catch (e) {
    console.error('加载暂存表明细失败:', e)
    tableData.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  page.value = pag.current
  pageSize.value = pag.pageSize
  loadData()
}

watch(() => props.visible, (val) => {
  if (val) {
    page.value = 1
    loadData()
  }
})
</script>
