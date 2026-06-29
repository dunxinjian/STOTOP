<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getPendingEmployees, rematchUnify } from '@/api/carrierQuality'

const list = ref<any[]>([])
const loading = ref(false)
const rematching = ref(false)

async function fetchPending() {
  loading.value = true
  try { list.value = await getPendingEmployees() || [] }
  catch { message.error('获取待认领清单失败') } finally { loading.value = false }
}

async function handleRematch() {
  rematching.value = true
  try {
    const r = await rematchUnify()
    message.success(`重跑回填完成：网点回填 ${r?.networkRebound ?? 0}，员工回填 ${r?.employeeRebound ?? 0}`)
    await fetchPending()
  } catch { message.error('重跑回填失败') } finally { rematching.value = false }
}

const columns = [
  { title: '员工姓名原文', dataIndex: 'nameRaw', width: 200 },
  { title: '网点编码', dataIndex: 'networkCode', width: 140 },
  { title: '待认领事件数', dataIndex: 'count', width: 140 },
  { title: '建议状态', dataIndex: 'suggestStatus', width: 120, customRender: ({ text }: any) => text === 3 ? '启发式候选' : '未匹配' },
]

onMounted(fetchPending)
</script>

<template>
  <div class="page">
    <PageHeader title="主数据认领">
      <template #left><span class="view-title">主数据认领</span></template>
      <template #actions>
        <div style="display:flex;align-items:center;gap:8px;">
          <a-button size="middle" :loading="loading" @click="fetchPending">刷新</a-button>
          <a-button type="primary" size="middle" :loading="rematching" @click="handleRematch">补别名后重跑回填</a-button>
        </div>
      </template>
    </PageHeader>

    <a-alert
      type="info" show-icon style="margin-bottom:12px;"
      message="脱名未绑定的质量事件按 (姓名原文 × 网点) 聚合在此。请到「员工别名 / 网点主数据」维护映射后，点击右上「重跑回填」使历史事件命中绑定。" />

    <a-card size="small">
      <a-table
        :columns="columns" :data-source="list" :loading="loading" row-key="nameRaw" size="small"
        :pagination="{ pageSize: 50, showSizeChanger: true, showQuickJumper: true, showTotal: (t: number) => `共 ${t} 条待认领` }" />
    </a-card>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
.page { padding: 12px; }
.view-title { font-weight: 600; }
</style>
