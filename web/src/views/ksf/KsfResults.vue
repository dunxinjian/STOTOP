<template>
  <div class="ksf-results page-container">
    <a-page-header title="月度核算结果" sub-title="KSF 月度核算结果查询" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-month-picker v-model:value="searchPeriod" placeholder="选择期间" style="width: 160px" />
          <a-input v-model:value="searchEmployee" placeholder="员工姓名/工号" allow-clear style="width: 200px" />
          <a-select v-model:value="searchStatus" placeholder="状态筛选" allow-clear style="width: 140px" :options="statusOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button>导出</a-button>
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
            <a-button type="link" size="small">查看明细</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Dayjs } from 'dayjs'
import type { KsfResult } from '@/types/ksf'

const searchPeriod = ref<Dayjs | undefined>(undefined)
const searchEmployee = ref<string>('')
const searchStatus = ref<number | undefined>(undefined)

const statusOptions = [
  { label: '试运行', value: 1 },
  { label: '正式', value: 2 },
  { label: '取数异常', value: 3 },
]

const columns = [
  { title: '员工', dataIndex: 'F员工ID', key: 'F员工ID', width: 140 },
  { title: '期间', dataIndex: 'F期间', key: 'F期间', width: 120 },
  { title: '岗位', dataIndex: 'F岗位', key: 'F岗位', width: 140 },
  { title: '固定部分', dataIndex: 'F固定部分', key: 'F固定部分', width: 110 },
  { title: '浮动部分', dataIndex: 'F浮动部分', key: 'F浮动部分', width: 110 },
  { title: '加薪', dataIndex: 'F加薪', key: 'F加薪', width: 100 },
  { title: '扣减', dataIndex: 'F扣减', key: 'F扣减', width: 100 },
  { title: '实发', dataIndex: 'F实发', key: 'F实发', width: 110 },
  { title: '状态', dataIndex: 'F状态', key: 'F状态', width: 100 },
  { title: '操作', key: 'action', width: 120, fixed: 'right' as const },
]

const dataSource = ref<KsfResult[]>([])
</script>

<style scoped>
.ksf-results {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
