<template>
  <div class="ksf-plans page-container">
    <a-page-header title="岗位方案" sub-title="KSF 岗位方案管理" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="searchKeyword" placeholder="方案名称" allow-clear style="width: 220px" />
          <a-select v-model:value="searchMode" placeholder="运行模式" allow-clear style="width: 140px" :options="modeOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary">
            <template #icon><PlusOutlined /></template>
            新增方案
          </a-button>
          <a-button type="primary" ghost>启用方案</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="false"
        row-key="FID"
        bordered
        size="small"
      >
        <template #bodyCell="{ column }">
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">编辑</a-button>
            <a-button type="link" size="small">查看明细</a-button>
            <a-button type="link" size="small" danger>禁用</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { KsfPlan } from '@/types/ksf'

const searchKeyword = ref<string>('')
const searchMode = ref<number | undefined>(undefined)

const modeOptions = [
  { label: '试运行', value: 0 },
  { label: '正式', value: 1 },
]

const columns = [
  { title: '方案名称', dataIndex: 'F名称', key: 'F名称' },
  { title: '岗位', dataIndex: 'F岗位ID', key: 'F岗位ID', width: 160 },
  { title: '运行模式', dataIndex: 'F运行模式', key: 'F运行模式', width: 120 },
  { title: '启用状态', dataIndex: 'F启用状态', key: 'F启用状态', width: 100 },
  { title: '生效日期', dataIndex: 'F生效日期', key: 'F生效日期', width: 140 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
]

const dataSource = ref<KsfPlan[]>([])
</script>

<style scoped>
.ksf-plans {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
